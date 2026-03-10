namespace SCP_575.Handlers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using InventorySystem.Items.Firearms;
    using InventorySystem.Items.Firearms.Attachments;
    using InventorySystem.Items.ToggleableLights;
    using LabApi.Events.Arguments.PlayerEvents;
    using LabApi.Events.Arguments.ServerEvents;
    using LabApi.Events.CustomHandlers;
    using LabApi.Features.Wrappers;
    using MEC;
    using SCP_575.ConfigObjects;
    using SCP_575.Shared;
    using SCP575.Shared;

    /// <summary>
    /// Manages restrictions on player flashlights and weapon flashlights affected by SCP-575, including cooldowns, flickering effects, and forced disables during attacks.
    /// </summary>
    public class PlayerLightsourceHandler : CustomEventsHandler, IDisposable
    {
        private readonly Plugin _plugin;
        private readonly LibraryLabAPI _libraryLabAPI;
        private readonly PlayerLightsourceConfig _config;

        // Replaced ConcurrentDictionary with standard Dictionary (MEC runs on the Main Thread)
        private readonly Dictionary<string, DateTime> _cooldownUntil = new();
        private readonly HashSet<string> _flickeringPlayers = new();
        private readonly Random _random = new();

        private bool _isDisposed;

        private const string LightCleanupTag = CoroutineTags.LightCleanup;
        private const string FlickerTagPrefix = CoroutineTags.FlickerPrefix;

        public PlayerLightsourceHandler(Plugin plugin)
        {
            _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin), "Plugin instance cannot be null.");
            _libraryLabAPI = _plugin.LibraryLabAPI;
            _config = plugin.Config?.LightsourceConfig ?? throw new InvalidOperationException("LightsourceConfig is not initialized.");
        }

        #region Lifecycle Management

        public void Initialize()
        {
            if (_isDisposed) return;

            Timing.KillCoroutines(LightCleanupTag);
            Timing.RunCoroutine(CleanupCoroutine(), LightCleanupTag);

            LibraryLabAPI.LogInfo("PlayerLightsourceHandler", "Initialized lightsource handler.");
        }

        public void Clean()
        {
            Timing.KillCoroutines(LightCleanupTag);

            foreach (var userId in _flickeringPlayers.ToList())
            {
                Timing.KillCoroutines($"{FlickerTagPrefix}{userId}");
            }

            _cooldownUntil.Clear();
            _flickeringPlayers.Clear();
        }

        public override void OnServerRoundEnded(RoundEndedEventArgs ev) => Clean();
        public override void OnServerWaitingForPlayers() => Clean();

        public void Dispose()
        {
            if (_isDisposed) return;
            Clean();
            _isDisposed = true;

            LibraryLabAPI.LogInfo("PlayerLightsourceHandler", "Disposed lightsource handler.");
        }

        #endregion

        #region Event Handlers

        public override void OnPlayerChangedItem(PlayerChangedItemEventArgs ev)
        {
            if (!_plugin.IsEventActive || !IsValidPlayer(ev?.Player) || ev.NewItem is not LightItem lightItem || !lightItem.IsEmitting)
                return;

            Timing.CallDelayed(0.05f, () =>
            {
                if (lightItem != null)
                {
                    lightItem.IsEmitting = false;
                    LibraryLabAPI.LogDebug("OnPlayerChangedItem", $"Disabled flashlight for {ev.Player.Nickname}.");
                }
            });
        }

        public override void OnPlayerTogglingFlashlight(PlayerTogglingFlashlightEventArgs ev)
        {
            if (!_plugin.IsEventActive || !IsValidPlayer(ev?.Player) || !IsBlackout()) return;

            (ev.IsAllowed, ev.NewState) = HandleLightToggling(ev.Player, ev.IsAllowed, ev.NewState, _plugin.Config.HintsConfig.LightEmitterCooldownHint);
        }

        public override void OnPlayerTogglingWeaponFlashlight(PlayerTogglingWeaponFlashlightEventArgs ev)
        {
            if (!_plugin.IsEventActive || !IsValidPlayer(ev?.Player) || ev?.FirearmItem == null || !IsBlackout() || !HasFlashlight(ev.FirearmItem))
                return;

            (ev.IsAllowed, ev.NewState) = HandleLightToggling(ev.Player, ev.IsAllowed, ev.NewState, _plugin.Config.HintsConfig.LightEmitterCooldownHint);
        }

        public override void OnPlayerToggledFlashlight(PlayerToggledFlashlightEventArgs ev)
        {
            if (!_plugin.IsEventActive || !IsValidPlayer(ev?.Player) || !ev.NewState || !IsPlayerInDarkRoom(ev.Player) || !IsBlackout())
                return;

            Timing.RunCoroutine(
                FlickerCoroutine(ev.Player.UserId, "Flashlight", () => ev.LightItem.IsEmitting, state => ev.LightItem.IsEmitting = state),
                $"{FlickerTagPrefix}{ev.Player.UserId}"
            );
        }

        public override void OnPlayerToggledWeaponFlashlight(PlayerToggledWeaponFlashlightEventArgs ev)
        {
            if (!_plugin.IsEventActive || !IsValidPlayer(ev?.Player) || ev?.FirearmItem == null || !HasFlashlight(ev.FirearmItem) || !ev.NewState || !IsPlayerInDarkRoom(ev.Player) || !IsBlackout())
                return;

            Timing.RunCoroutine(
                FlickerCoroutine(ev.Player.UserId, "WeaponFlashlight", () => ev.FirearmItem.FlashlightEnabled, state => ev.FirearmItem.FlashlightEnabled = state),
                $"{FlickerTagPrefix}{ev.Player.UserId}"
            );
        }

        #endregion

        #region Public Methods

        public void ApplyLightsourceEffects(Player target)
        {
            if (!IsValidPlayer(target)) return;

            ApplyCooldown(target);
            switch (target.CurrentItem)
            {
                case LightItem lightItem:
                    lightItem.IsEmitting = false;
                    Timing.RunCoroutine(
                        FlickerCoroutine(target.UserId, "Flashlight", () => lightItem.IsEmitting, state => lightItem.IsEmitting = state, forceOff: true),
                        $"{FlickerTagPrefix}{target.UserId}"
                    );
                    break;
                case FirearmItem firearm when HasFlashlight(firearm):
                    firearm.FlashlightEnabled = false;
                    Timing.RunCoroutine(
                        FlickerCoroutine(target.UserId, "WeaponFlashlight", () => firearm.FlashlightEnabled, state => firearm.FlashlightEnabled = state, forceOff: true),
                        $"{FlickerTagPrefix}{target.UserId}"
                    );
                    break;
            }
        }

        public void ForceCooldown(Player player)
        {
            if (!IsValidPlayer(player)) return;

            ApplyCooldown(player);
            player.SendHint(_plugin.Config.HintsConfig.LightEmitterCooldownHint, 1.75f);
        }

        public void ClearCooldown(Player player = null)
        {
            if (player == null)
            {
                _cooldownUntil.Clear();
            }
            else if (IsValidPlayer(player))
            {
                _cooldownUntil.Remove(player.UserId);
            }
        }

        #endregion

        #region Helper Methods

        private bool IsValidPlayer(Player player) => player?.UserId != null;
        private bool IsBlackout() => _plugin.Npc?.Methods?.IsBlackoutActive == true;

        private bool IsPlayerInDarkRoom(Player player)
        {
            if (player.Room == null || player.Room.Name == MapGeneration.RoomName.Pocket) return false;
            return _libraryLabAPI.IsPlayerInDarkRoom(player);
        }

        private float CleanupInterval => _plugin.Config?.HandlerCleanupInterval ?? 160f;
        private TimeSpan CooldownDuration => TimeSpan.FromSeconds(Math.Max(1, _config.KeterLightsourceCooldown));

        private void ApplyCooldown(Player player)
        {
            _cooldownUntil[player.UserId] = DateTime.UtcNow + CooldownDuration;
        }

        private (bool IsAllowed, bool NewState) HandleLightToggling(Player player, bool isAllowed, bool newState, string message)
        {
            if (!newState || !IsBlackout())
                return (isAllowed, newState);

            if (_flickeringPlayers.Contains(player.UserId))
                return (false, false);

            if (_cooldownUntil.TryGetValue(player.UserId, out var until) && DateTime.UtcNow < until)
            {
                player.SendHint(message, 1.0f);
                return (true, false);
            }

            return (isAllowed, newState);
        }

        private IEnumerator<float> FlickerCoroutine(string userId, string lightType, Func<bool> getState, Action<bool> setState, bool forceOff = false)
        {
            if (!_flickeringPlayers.Add(userId)) yield break;

            try
            {
                int flickerCount = Math.Max(2, _random.Next(_config.MinFlickerCount, _config.MaxFlickerCount));
                int totalDurationMs = _random.Next(_config.MinFlickerDurationMs, _config.MaxFlickerDurationMs + 1);
                float delayPerFlicker = (totalDurationMs / 1000f) / flickerCount;

                for (int i = 0; i < flickerCount; i++)
                {
                    // Exit cleanly if round ends mid-flicker
                    if (!_plugin.IsEventActive) break;

                    if (lightType == "WeaponFlashlight")
                    {
                        var p = Player.Get(userId);
                        if (p?.CurrentItem is not FirearmItem f || !HasFlashlight(f)) break;
                    }

                    setState(!getState());
                    yield return Timing.WaitForSeconds(delayPerFlicker);
                }

                if (forceOff && _plugin.IsEventActive) setState(false);
            }
            finally
            {
                _flickeringPlayers.Remove(userId);
            }
        }

        private bool HasFlashlight(FirearmItem firearm)
        {
            if (firearm?.Base == null) return false;
            return firearm.Attachments != null && firearm.Attachments.Any(a => a.Name == AttachmentName.Flashlight);
        }

        private IEnumerator<float> CleanupCoroutine()
        {
            while (!_isDisposed)
            {
                yield return Timing.WaitForSeconds(CleanupInterval);

                if (_isDisposed || !_plugin.IsEventActive) continue;

                var cutoff = DateTime.UtcNow - CooldownDuration;

                // Remove expired cooldowns
                var expiredKeys = _cooldownUntil.Where(kvp => kvp.Value < cutoff).Select(kvp => kvp.Key).ToList();
                foreach (var key in expiredKeys)
                {
                    _cooldownUntil.Remove(key);
                }
            }
        }

        #endregion
    }
}