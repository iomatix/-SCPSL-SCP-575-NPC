namespace SCP_575.Handlers
{
    using InventorySystem.Items.Firearms;
    using InventorySystem.Items.Firearms.Attachments;
    using InventorySystem.Items.ToggleableLights;
    using LabApi.Events.Arguments.PlayerEvents;
    using LabApi.Events.Arguments.ServerEvents;
    using LabApi.Events.CustomHandlers;
    using LabApi.Features.Wrappers;
    using MapGeneration;
    using MEC;
    using SCP_575.ConfigObjects;
    using SCP_575.Shared;
    using SCP_575.Shared.Audio.Enums;
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Governs electrical degradation, interaction throttling, and structural suppression 
    /// of mobile photon emitters deployed by human forces inside active threat zones.
    /// </summary>
    public class PlayerLightsourceHandler : CustomEventsHandler, IDisposable
    {
        private readonly Plugin _plugin;
        private readonly LibraryLabAPI _libraryLabAPI;
        private readonly PlayerLightsourceConfig _config;

        private readonly Dictionary<string, DateTime> _cooldownUntil = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, DateTime> _lastCooldownAudioTime = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, DateTime> _lastWeaponClickTime = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _flickeringPlayers = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _pendingItemChanges = new(StringComparer.OrdinalIgnoreCase);
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

            foreach (var userId in _pendingItemChanges.ToList())
            {
                Timing.KillCoroutines($"{ItemChangePrefix}{userId}");
            }

            _cooldownUntil.Clear();
            _flickeringPlayers.Clear();
            _pendingItemChanges.Clear();
            _lastCooldownAudioTime.Clear();
            _lastWeaponClickTime.Clear();
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
            if (!_plugin.IsEventActive || !IsValidPlayer(ev?.Player))
                return;

            string userId = ev.Player.UserId;

            Timing.KillCoroutines($"{FlickerTagPrefix}{userId}");
            _flickeringPlayers.Remove(userId);

            Action stateModificationActions = null;
            bool requiresIntervention = false;

            if (ev.OldItem is LightItem oldLight && oldLight.IsEmitting)
            {
                requiresIntervention = true;
                stateModificationActions += () => oldLight.IsEmitting = false;
            }
            else if (ev.OldItem is FirearmItem oldFirearm && HasFlashlight(oldFirearm) && oldFirearm.FlashlightEnabled)
            {
                requiresIntervention = true;
                stateModificationActions += () => oldFirearm.FlashlightEnabled = false;
            }

            if (ev.NewItem is LightItem newLight && newLight.IsEmitting)
            {
                requiresIntervention = true;
                stateModificationActions += () => newLight.IsEmitting = false;
            }
            else if (ev.NewItem is FirearmItem newFirearm && HasFlashlight(newFirearm) && newFirearm.FlashlightEnabled)
            {
                requiresIntervention = true;
                stateModificationActions += () => newFirearm.FlashlightEnabled = false;
            }

            if (!requiresIntervention || stateModificationActions == null) return;

            string coroutineTag = $"{ItemChangePrefix}{userId}";

            Timing.KillCoroutines(coroutineTag);
            _pendingItemChanges.Add(userId);

            // Shifting mechanical transform weights out of the instant frame loop completely 
            // eliminates race conditions with native client inventory swap packets.
            var coroutine = Timing.CallDelayed(0.05f, () =>
            {
                try
                {
                    stateModificationActions.Invoke();
                    LibraryLabAPI.LogDebug("OnPlayerChangedItem", $"Enforced dark-state on inventory swap for {ev.Player.Nickname}.");
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
            if (!_plugin.IsEventActive || !IsValidPlayer(ev?.Player) || !IsBlackout() || ev.Player.CurrentItem is not LightItem) return;

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
            if (ev?.Player == null || ev.FirearmItem == null || !HasFlashlight(ev.FirearmItem))
                return;

            DateTime now = DateTime.UtcNow;
            string userId = ev.Player.UserId;

            // Audio channel throttling: Suppresses rapid sequential inputs to protect client frame compilation pipelines
            if (!_lastWeaponClickTime.TryGetValue(userId, out DateTime lastClick) || (now - lastClick).TotalMilliseconds >= 110)
            {
                _lastWeaponClickTime[userId] = now;
                // FIXED: Mapped mechanical weapon toggle clicks onto the dedicated tactical interface clip instead of short circuits
                _plugin.AudioManager.PlayAtPosition(AudioKey.LightSwitch, ev.Player.Position, lifespan: 0.115f, isTransient: true, sourcePlayer: ev.Player);
            }

            if (!_plugin.IsEventActive || !ev.NewState || !IsPlayerInDarkRoom(ev.Player) || !IsBlackout())
                return;

            Timing.RunCoroutine(
                FlickerCoroutine(ev.Player.UserId, "WeaponFlashlight", () => ev.FirearmItem.FlashlightEnabled, state => ev.FirearmItem.FlashlightEnabled = state),
                $"{FlickerTagPrefix}{ev.Player.UserId}"
            );
        }

        public override void OnPlayerLeft(PlayerLeftEventArgs ev)
        {
            if (ev?.Player == null) return;
            _cooldownUntil.Remove(ev.Player.UserId);
            _flickeringPlayers.Remove(ev.Player.UserId);
            _pendingItemChanges.Remove(ev.Player.UserId);
            _lastCooldownAudioTime.Remove(ev.Player.UserId);
            _lastWeaponClickTime.Remove(ev.Player.UserId);
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

            if (_plugin.Config.HintsConfig.IsEnabledLightEmitterCooldownHint) player.SendHint(_plugin.Config.HintsConfig.LightEmitterCooldownHint, 1.75f);
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
            if (player.Room == null || player.Room.Name == RoomName.Pocket) return false;
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
                if (_plugin.Config.HintsConfig.IsEnabledLightEmitterCooldownHint) player.SendHint(message, 1.0f);

                PlayLightsourceErrorFeedback(player);

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
                    // FIXED: Offloaded tactical panic and paranoia soundtracks to the central Audio Director subsystem
                    _plugin.AudioDirector?.ProcessLightsourceFlicker(player);

                    if (forceOff)
                    {
                        PlayLightsourceErrorFeedback(player);
                    }
                }

                int flickerCount = Math.Max(2, _random.Next(_config.MinFlickerCount, _config.MaxFlickerCount));
                int totalDurationMs = _random.Next(_config.MinFlickerDurationMs, _config.MaxFlickerDurationMs + 1);
                float delayPerFlicker = (totalDurationMs / 1000f) / flickerCount;

                for (int i = 0; i < flickerCount; i++)
                {
                    if (!_plugin.IsEventActive) break;
                    var p = Player.Get(userId);

                    if (lightType == "WeaponFlashlight")
                    {
                        if (p?.CurrentItem is not FirearmItem f || !HasFlashlight(f)) break;
                    }
                    else if (lightType == "Flashlight")
                    {
                        if (p?.CurrentItem is not LightItem) break;
                    }

                    if (player != null)
                    {
                        bool isLastIteration = (i == flickerCount - 1);
                        if (!(isLastIteration && forceOff))
                        {
                            _plugin.AudioManager.PlayAtPosition(AudioKey.LightShortCircuit, player.Position, lifespan: 0.145f, isTransient: true, sourcePlayer: player);
                        }
                    }

                    setState(!getState());
                    yield return Timing.WaitForSeconds(delayPerFlicker);
                }

                if (forceOff && _plugin.IsEventActive && player != null)
                {
                    setState(false);
                    _plugin.AudioManager.PlayAtPosition(AudioKey.LightShortCircuit, player.Position, lifespan: 0.33f, isTransient: true, sourcePlayer: player);
                }
            }
            finally
            {
                _flickeringPlayers.Remove(userId);
            }
        }

        private bool HasFlashlight(FirearmItem firearm)
        {
            if (firearm?.Base?.Attachments == null) return false;

            // Queries native attachments via client network sync identity blocks rather than active visual transforms
            return firearm.Base.Attachments.Any(a => a != null && a.Name == AttachmentName.Flashlight && a.IsEnabled);
        }

        private void PlayLightsourceErrorFeedback(Player player)
        {
            if (player == null) return;

            DateTime now = DateTime.UtcNow;
            if (!_lastCooldownAudioTime.TryGetValue(player.UserId, out DateTime lastPlayTime) || (now - lastPlayTime).TotalSeconds >= 1.5)
            {
                _lastCooldownAudioTime[player.UserId] = now;
                _plugin.AudioManager.PlayTrackingAudio(
                    player: player,
                    audioKey: AudioKey.LightShortCircuit,
                    hearableForAllPlayers: true
                );
            }
        }

        private IEnumerator<float> CleanupCoroutine()
        {
            while (!_isDisposed)
            {
                yield return Timing.WaitForSeconds(CleanupInterval);

                if (_isDisposed || !_plugin.IsEventActive || _cooldownUntil.Count == 0) continue;

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