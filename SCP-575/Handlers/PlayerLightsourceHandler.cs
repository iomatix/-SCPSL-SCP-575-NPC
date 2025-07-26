namespace SCP_575.Handlers
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using InventorySystem.Items.Firearms;
    using InventorySystem.Items.Firearms.Attachments;
    using InventorySystem.Items.Firearms.Attachments.Components;
    using InventorySystem.Items.ToggleableLights;
    using LabApi.Events.Arguments.PlayerEvents;
    using LabApi.Events.CustomHandlers;
    using LabApi.Features.Wrappers;
    using MEC;
    using SCP_575.ConfigObjects;
    using SCP_575.Shared;
    using Utils.Networking;

    // TODO: Refactor weapon flashlight handling to use direct API calls like flashlight when LabAPI provides unified toggleable light support: https://github.com/northwood-studios/LabAPI/issues/220

    /// <summary>
    /// Manages restrictions on player flashlights and weapon flashlights affected by SCP-575, including cooldowns, flickering effects, and forced disables during attacks.
    /// </summary>
    public class PlayerLightsourceHandler : CustomEventsHandler, IDisposable
    {
        private readonly Plugin _plugin;
        private readonly PlayerLightsourceConfig _config;
        private readonly ConcurrentDictionary<string, DateTime> _cooldownUntil = new();
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _flickerTokens = new();
        private readonly HashSet<string> _flickeringPlayers = new();
        private readonly Random _random = new();
        private readonly HashSet<string> _playersWithActiveWeaponFlashlight = new();
        private CoroutineHandle _cleanupCoroutine;

        /// <summary>
        /// Initializes a new instance of the <see cref="PlayerLightsourceHandler"/> class.
        /// </summary>
        /// <param name="plugin">The plugin instance providing access to configuration.</param>
        /// <exception cref="ArgumentNullException">Thrown if the plugin instance is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown if configuration is null or properties are missing.</exception>
        public PlayerLightsourceHandler(Plugin plugin)
        {
            _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin), "Plugin instance cannot be null.");
            _config = plugin.Config?.LightsourceConfig ?? throw new InvalidOperationException("LightsourceConfig is not initialized.");
        }

        #region Lifecycle Management

        /// <summary>
        /// Initializes the handler and starts the cleanup coroutine.
        /// </summary>
        public void Initialize()
        {
            if (_cleanupCoroutine.IsRunning) return;
            _cleanupCoroutine = Timing.RunCoroutine(CleanupCoroutine(), "SCP575LightCleanup");
            Library_ExiledAPI.LogInfo("PlayerLightsourceHandler.Initialize", "Initialized lightsource handler.");
        }

        /// <summary>
        /// Disposes the handler, stopping coroutines and clearing resources.
        /// </summary>
        public void Dispose()
        {
            Timing.KillCoroutines(_cleanupCoroutine);
            foreach (var cts in _flickerTokens.Values)
            {
                cts?.Cancel();
                cts?.Dispose();
            }
            _flickerTokens.Clear();
            _cooldownUntil.Clear();
            _flickeringPlayers.Clear();
            Library_ExiledAPI.LogInfo("PlayerLightsourceHandler.Dispose", "Disposed lightsource handler.");
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Disables flashlights by default when equipped to prevent bypassing SCP-575 mechanics.
        /// </summary>
        /// <param name="ev">Event arguments for the item changed event.</param>
        public override void OnPlayerChangedItem(PlayerChangedItemEventArgs ev)
        {
            if (!IsValidPlayer(ev?.Player) || ev.NewItem is not LightItem lightItem || !lightItem.IsEmitting) return;

            Timing.CallDelayed(0.05f, () =>
            {
                lightItem.IsEmitting = false;
                Library_ExiledAPI.LogDebug("OnPlayerChangedItem", $"Disabled flashlight for {ev.Player.Nickname}.");
            });
        }

        /// <summary>
        /// Restricts flashlight toggling during cooldowns or active flickers.
        /// </summary>
        /// <param name="ev">Event arguments containing player and toggle state.</param>
        public override void OnPlayerTogglingFlashlight(PlayerTogglingFlashlightEventArgs ev)
        {
            if (!IsValidPlayer(ev?.Player) || !IsBlackout()) return;

            (ev.IsAllowed, ev.NewState) = HandleLightToggling(ev.Player, ev.IsAllowed, ev.NewState, _plugin.Config.HintsConfig.LightEmitterCooldownHint);
            Library_ExiledAPI.LogDebug("OnPlayerToggling", $"Flashlight toggle: {ev.Player.Nickname}: IsAllowed={ev.IsAllowed}, NewState={ev.NewState}");
        }

        /// <summary>
        /// Restricts weapon flashlight toggling during a cooldowns period or active flickers. 
        /// </summary>
        /// <param name="ev"> The event arguments containing a player and toggle state. </param>
        public override void OnPlayerTogglingWeaponFlashlight(PlayerTogglingWeaponFlashlightEventArgs ev)
        {
            if (!IsValidPlayer(ev?.Player) || !IsBlackout()) return;

            (ev.IsAllowed, ev.NewState) = HandleLightToggling(ev.Player, ev.IsAllowed, ev.NewState, _plugin.Config.HintsConfig.LightEmitterCooldownHint);
            Library_ExiledAPI.LogDebug("OnPlayerTogglingWeaponFlashlight", $"Weapon flashlight toggle: {ev.Player.Nickname}: IsAllowed={ev.IsAllowed}, NewState={ev.NewState}");
        }

        /// <summary>
        /// Triggers a flicker effect for a flashlight toggled on in a dark room during a blackout.
        /// </summary>
        /// <param name="ev"> The event arguments containing a player and a flashlight item. </param>
        public override void OnPlayerToggledFlashlight(PlayerToggledFlashlightEventArgs ev)
        {
            if (!IsValidPlayer(ev?.Player) || !ev.NewState || !IsPlayerInDarkRoom(ev.Player) || !IsBlackout()) return;

            _ = StartFlickerEffectAsync(ev.Player.UserId, "Flashlight", () => ev.LightItem.IsEmitting, state => ev.LightItem.IsEmitting = state);
        }

        /// <summary>
        /// Triggers a flicker effect for a weapon flashlight toggled on in a dark room during a blackout, if supported.
        /// </summary>
        /// <param name="ev"> The event arguments containing a player and a firearm item. </param>
        public override void OnPlayerToggledWeaponFlashlight(PlayerToggledWeaponFlashlightEventArgs ev)
        {
            // Track weapon flashlight state  
            if (ev.NewState)
                _playersWithActiveWeaponFlashlight.Add(ev.Player.UserId);
            else
                _playersWithActiveWeaponFlashlight.Remove(ev.Player.UserId);

            // Your existing flicker logic  
            if (!IsValidPlayer(ev?.Player) || !ev.NewState || !IsPlayerInDarkRoom(ev.Player) || !IsBlackout()) return;
            _ = StartFlickerEffectAsync(ev.Player.UserId, "WeaponFlashlight",
                () => _playersWithActiveWeaponFlashlight.Contains(ev.Player.UserId),
                state => ToggleWeaponFlashlightViaEvent(ev.FirearmItem, state));
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Disables a player's light source, triggers a flicker effect, and applies a cooldown when attacked by SCP-575.
        /// </summary>
        /// <param name="target">The player attacked by SCP-575.</param>
        public void OnScp575AttacksPlayer(Player target)
        {
            if (!IsValidPlayer(target)) return;

            ApplyCooldown(target);
            switch (target.CurrentItem)
            {
                case LightItem lightItem:
                    lightItem.IsEmitting = false;
                    Library_ExiledAPI.LogDebug("OnScp575AttacksPlayer", $"Forced off flashlight for {target.Nickname}");
                    _ = StartFlickerEffectAsync(target.UserId, "Flashlight", () => lightItem.IsEmitting, state => lightItem.IsEmitting = state);
                    break;
                case FirearmItem firearm when HasFlashlight(firearm):
                    ToggleWeaponFlashlight(firearm, false, nameof(OnScp575AttacksPlayer));
                    Library_ExiledAPI.LogDebug("OnScp575AttacksPlayer", $"Forced off weapon flashlight for {target.Nickname}");
                    _ = StartFlickerEffectAsync(target.UserId, "WeaponFlashlight", () => GetWeaponFlashlightState(firearm), state => ToggleWeaponFlashlight(firearm, state, nameof(OnScp575AttacksPlayer)));
                    break;
            }
        }

        /// <summary>
        /// Forces a cooldown on a player's light source, preventing immediate reactivation.
        /// </summary>
        /// <param name="player">The player to apply the cooldown to.</param>
        public void ForceCooldown(Player player)
        {
            if (!IsValidPlayer(player)) return;

            ApplyCooldown(player);
            player.SendHint(_plugin.Config.HintsConfig.LightEmitterDisabledHint, 1.75f);
            Library_ExiledAPI.LogDebug("ForceCooldown", $"Forced cooldown on {player.Nickname}");
        }

        /// <summary>
        /// Clears cooldowns for a specific player or all players if null.
        /// </summary>
        /// <param name="player">The player to clear the cooldown for, or null to clear all.</param>
        public void ClearCooldown(Player player = null)
        {
            if (player == null)
            {
                _cooldownUntil.Clear();
                Library_ExiledAPI.LogDebug("ClearCooldown", "Cleared cooldown for all players");
            }
            else if (IsValidPlayer(player))
            {
                _cooldownUntil.TryRemove(player.UserId, out _);
                Library_ExiledAPI.LogDebug("ClearCooldown", $"Cleared cooldown for {player.Nickname}");
            }
        }

        #endregion

        #region Helper Methods

        private bool IsValidPlayer(Player player)
        {
            if (player?.UserId == null)
            {
                Library_ExiledAPI.LogDebug("IsValidPlayer", "Player or UserId is null.");
                return false;
            }
            return true;
        }

        private bool IsBlackout()
        {
            return _plugin.Npc?.Methods?.IsBlackoutActive == true;
        }

        private bool IsPlayerInDarkRoom(Player player)
        {
            return Library_LabAPI.IsPlayerInDarkRoom(player);
        }

        private float CleanupInterval => _plugin.Config?.HandlerCleanupInterval ?? 160f;

        private TimeSpan CooldownDuration => TimeSpan.FromSeconds(Math.Max(1, _config.KeterLightsourceCooldown));

        private void ApplyCooldown(Player player)
        {
            var cooldownUntil = DateTime.UtcNow + CooldownDuration;
            _cooldownUntil[player.UserId] = cooldownUntil;
            Library_ExiledAPI.LogDebug("ApplyCooldown", $"Applied cooldown to {player.Nickname} until {cooldownUntil:T}");
        }

        private (bool IsAllowed, bool NewState) HandleLightToggling(Player player, bool isAllowed, bool newState, string message)
        {
            if (!newState || !IsBlackout())
                return (isAllowed, newState);

            if (_flickeringPlayers.Contains(player.UserId))
            {
                Library_ExiledAPI.LogDebug("HandleLightToggling", $"Blocked toggle for {player.Nickname} due to active flicker.");
                return (false, false);
            }

            if (_cooldownUntil.TryGetValue(player.UserId, out var until) && DateTime.UtcNow < until)
            {
                player.SendHint(message, 1.0f);
                Library_ExiledAPI.LogDebug("HandleLightToggling", $"Blocked toggle for {player.Nickname} due to cooldown ({(until - DateTime.UtcNow).TotalSeconds:F1}s left).");
                return (true, false);
            }

            return (isAllowed, newState);
        }

        private async Task StartFlickerEffectAsync(string userId, string lightType, Func<bool> getState, Action<bool> setState)
        {
            if (!_flickeringPlayers.Add(userId))
            {
                Library_ExiledAPI.LogDebug("StartFlickerEffectAsync", $"Flicker already active for {userId}. Skipping.");
                return;
            }

            var cts = new CancellationTokenSource();
            _flickerTokens[userId] = cts;
            Library_ExiledAPI.LogDebug("StartFlickerEffectAsync", $"Started {lightType} flicker for {userId}");

            try
            {
                int flickerCount = _random.Next(3, 11);
                for (int i = 0; i < flickerCount && !cts.Token.IsCancellationRequested; i++)
                {
                    setState(!getState());
                    await Task.Delay(_random.Next(100, 450), cts.Token);
                }
                setState(false);
            }
            catch (TaskCanceledException)
            {
                setState(false);
                Library_ExiledAPI.LogDebug("StartFlickerEffectAsync", $"Flicker cancelled for {userId}.");
            }
            finally
            {
                _flickeringPlayers.Remove(userId);
                _flickerTokens.TryRemove(userId, out var disposedCts);
                disposedCts?.Dispose();
                Library_ExiledAPI.LogDebug("StartFlickerEffectAsync", $"Ended {lightType} flicker for {userId}");
            }
        }

        private bool HasFlashlight(FirearmItem firearm)
        {
            if (firearm?.Base == null)
            {
                Library_ExiledAPI.LogDebug("HasFlashlight", $"Invalid firearm.");
                return false;
            }

            Attachment[] attachments = firearm.Base.Attachments;
            bool hasFlashlight = attachments != null && attachments.Any(a => a.Name == AttachmentName.Flashlight);
            if (!hasFlashlight)
                Library_ExiledAPI.LogDebug("HasFlashlight", $"No flashlight attachment found for {firearm.Base.GetType().Name}.");
            return hasFlashlight;
        }

        private bool GetWeaponFlashlightState(FirearmItem firearm)
        {
            // Simplified - just check if player has active weapon flashlight  
            var player = Player.List.FirstOrDefault(p => p.CurrentItem == firearm);
            return player != null && _playersWithActiveWeaponFlashlight.Contains(player.UserId);
        }

        private void ToggleWeaponFlashlightViaEvent(FirearmItem firearm, bool enabled)
        {
            // Use LabAPI's network message system directly  
            new FlashlightNetworkHandler.FlashlightMessage(firearm.Serial, enabled).SendToAuthenticated();
            Library_ExiledAPI.LogDebug("ToggleWeaponFlashlight", $"Toggled weapon flashlight to {enabled} via network message.");
        }

        public bool HasActiveWeaponFlashlight(string userId)
        {
            return _playersWithActiveWeaponFlashlight.Contains(userId);
        }

        private void ToggleWeaponFlashlight(FirearmItem firearm, bool enabled, string context)
        {
            if (!HasFlashlight(firearm))
                return;

            var attachments = firearm.Base.Attachments;
            var flashlightAttachment = attachments.First(a => a.Name == AttachmentName.Flashlight);

            new FlashlightNetworkHandler.FlashlightMessage(firearm.Serial, enabled).SendToAuthenticated();
            Library_ExiledAPI.LogDebug("ToggleWeaponFlashlight", $"Toggled flashlight emission state to {enabled} for {firearm.Base.GetType().Name} in context {context}.");
        }

        private IEnumerator<float> CleanupCoroutine()
        {
            while (true)
            {
                yield return Timing.WaitForSeconds(CleanupInterval);
                var cutoff = DateTime.UtcNow - CooldownDuration;
                foreach (var kvp in _cooldownUntil.Where(k => k.Value < cutoff).ToList())
                    _cooldownUntil.TryRemove(kvp.Key, out _);

                foreach (var kvp in _flickerTokens.Where(k => !Player.List.Any(p => p.UserId == k.Key)).ToList())
                    if (_flickerTokens.TryRemove(kvp.Key, out var cts))
                    {
                        cts.Cancel();
                        cts.Dispose();
                    }

                Library_ExiledAPI.LogDebug("CleanupCoroutine", "Completed cleanup of expired cooldowns and flicker tokens.");
            }
        }

        #endregion
    }
}