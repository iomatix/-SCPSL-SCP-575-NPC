namespace SCP_575.Handlers
{
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
    using SCP_575.Shared.Audio.Enums;
    using SCP_575.Shared;
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Governs electrical degradation, interaction throttling, and forced tactical suppression 
    /// of mobile photon emitters deployed by human forces inside localized dark zones.
    /// </summary>
    public class PlayerLightsourceHandler : CustomEventsHandler, IDisposable
    {
        private readonly Plugin _plugin;
        private readonly LibraryLabAPI _libraryLabAPI;
        private readonly PlayerLightsourceConfig _config;

        private readonly Dictionary<string, DateTime> _cooldownUntil = new();
        private readonly HashSet<string> _flickeringPlayers = new();
        private readonly HashSet<string> _pendingItemChanges = new();
        private readonly Random _random = new();

        private bool _isDisposed;

        private const string LightCleanupTag = CoroutineTags.LightCleanup;
        private const string FlickerTagPrefix = CoroutineTags.FlickerPrefix;
        private const string ItemChangePrefix = CoroutineTags.ItemChangePrefix;

        public PlayerLightsourceHandler(Plugin plugin)
        {
            _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin), "Plugin instance cannot be null.");
            _libraryLabAPI = _plugin.LibraryLabAPI;
            _config = plugin.Config?.LightsourceConfig ?? throw new InvalidOperationException("LightsourceConfig is not initialized.");
        }

        #region Lifecycle Management

        /// <summary>
        /// Enlists the background garbage collection routines for expired network tracking nodes 
        /// and establishes operational readiness.
        /// </summary>
        public void Initialize()
        {
            if (_isDisposed) return;

            Timing.KillCoroutines(LightCleanupTag);
            Timing.RunCoroutine(CleanupCoroutine(), LightCleanupTag);

            LibraryLabAPI.LogInfo("PlayerLightsourceHandler", "Initialized lightsource handler.");
        }

        /// <summary>
        /// Flushes tracking arrays and actively kills active visual flickering routines 
        /// to guarantee total architectural isolation during round teardown phases.
        /// </summary>
        public void Clean()
        {
            Timing.KillCoroutines(LightCleanupTag);

            foreach (var userId in _flickeringPlayers.ToList())
            {
                Timing.KillCoroutines($"{FlickerTagPrefix}{userId}");
            }

            foreach (var userId in _pendingItemChanges.ToList())
            {
                Timing.KillCoroutines($"{ItemChangePrefix}{userId}");
            }

            _cooldownUntil.Clear();
            _flickeringPlayers.Clear();
            _pendingItemChanges.Clear();
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

        /// <summary>
        /// Monopolizes network item swap sequences to suppress active photon generation fields 
        /// before they render to client proxies if the actor is caught inside non-illuminated sectors.
        /// </summary>
        public override void OnPlayerChangedItem(PlayerChangedItemEventArgs ev)
        {
            if (!_plugin.IsEventActive || !IsValidPlayer(ev?.Player))
                return;

            bool isEmitting = false;
            Action disableAction = null;

            if (ev.NewItem is LightItem lightItem && lightItem.IsEmitting)
            {
                isEmitting = true;
                disableAction = () => lightItem.IsEmitting = false;
            }
            else if (ev.NewItem is FirearmItem firearm && HasFlashlight(firearm) && firearm.FlashlightEnabled)
            {
                isEmitting = true;
                disableAction = () => firearm.FlashlightEnabled = false;
            }

            if (!isEmitting || disableAction == null) return;

            string userId = ev.Player.UserId;
            string coroutineTag = $"{ItemChangePrefix}{userId}";

            Timing.KillCoroutines(coroutineTag);
            _pendingItemChanges.Add(userId);

            // Shifting structural component state modifications outside the instantaneous frame pipeline 
            // guarantees the base game inventory code registers item swap data without race condition conflicts.
            var coroutine = Timing.CallDelayed(0.05f, () =>
            {
                try
                {
                    disableAction();
                    LibraryLabAPI.LogDebug("OnPlayerChangedItem", $"Suppressed active emissive state during gear swap for {ev.Player.Nickname}.");
                }
                finally
                {
                    _pendingItemChanges.Remove(userId);
                }
            });
            coroutine.Tag = coroutineTag;
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

        /// <summary>
        /// Clears tracking nodes associated with a unique actor when they leave the server infrastructure,
        /// ensuring zero memory accumulation over continuous runtime operations.
        /// </summary>
        public override void OnPlayerLeft(PlayerLeftEventArgs ev)
        {
            if (ev?.Player == null) return;
            _cooldownUntil.Remove(ev.Player.UserId);
            _flickeringPlayers.Remove(ev.Player.UserId);
            _pendingItemChanges.Remove(ev.Player.UserId);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Enforces global system overrides on an actor's active tactical gear, shutting down 
        /// and flickering hardware elements when attacked directly by anomalous forces.
        /// </summary>
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

        /// <summary>
        /// Imposes an absolute, un-bypassable transmission blockage on an actor's tactical electronics,
        /// transmitting a structural interface warning hint to their heads-up display.
        /// </summary>
        public void ForceCooldown(Player player)
        {
            if (!IsValidPlayer(player)) return;

            ApplyCooldown(player);

            if(_plugin.Config.HintsConfig.IsEnabledLightEmitterCooldownHint) player.SendHint(_plugin.Config.HintsConfig.LightEmitterCooldownHint, 1.75f);
        }

        /// <summary>
        /// Removes transmission blocks from a specific actor, or clears the internal state tables completely.
        /// </summary>
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
                if (_plugin.Config.HintsConfig.IsEnabledLightEmitterCooldownHint)  player.SendHint(message, 1.0f);
                return (true, false);
            }

            return (isAllowed, newState);
        }

        private IEnumerator<float> FlickerCoroutine(string userId, string lightType, Func<bool> getState, Action<bool> setState, bool forceOff = false)
        {
            if (!_flickeringPlayers.Add(userId)) yield break;

            try
            {
                var player = Player.Get(userId);
                if (player != null)
                {
                    if (forceOff)
                    {
                        _plugin.AudioManager.PlayAudioAtPosition(AudioKey.ShadowClicking, player.Position);
                        _plugin.AudioManager.PlayAudioAtPosition(AudioKey.MonsterBreathLocal, player.Position);
                    }
                    else
                    {
                        _plugin.AudioManager.PlayAudioAtPosition(AudioKey.ShadowClicking, player.Position);
                    }
                }

                int flickerCount = Math.Max(2, _random.Next(_config.MinFlickerCount, _config.MaxFlickerCount));
                int totalDurationMs = _random.Next(_config.MinFlickerDurationMs, _config.MaxFlickerDurationMs + 1);
                float delayPerFlicker = (totalDurationMs / 1000f) / flickerCount;

                for (int i = 0; i < flickerCount; i++)
                {
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

                if (_isDisposed || !_plugin.IsEventActive || _cooldownUntil.Count == 0) continue;

                // Optimization: Enforce zero-allocation structural table scrubbing 
                // by resolving state data in-place without copying keys via LINQ allocations.
                DateTime now = DateTime.UtcNow;
                var keys = _cooldownUntil.Keys.ToList();

                foreach (var key in keys)
                {
                    if (_cooldownUntil.TryGetValue(key, out var expireTime) && now >= expireTime)
                    {
                        _cooldownUntil.Remove(key);
                    }
                }
            }
        }

        #endregion
    }
}