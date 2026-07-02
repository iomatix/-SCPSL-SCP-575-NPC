namespace SCP_575.Handlers
{
    using InventorySystem.Items.Firearms.Attachments;
    using LabApi.Events.Arguments.PlayerEvents;
    using LabApi.Events.Arguments.ServerEvents;
    using LabApi.Events.CustomHandlers;
    using LabApi.Features.Wrappers;
    using MapGeneration;
    using MEC;
    using SCP_575.ConfigObjects;
    using SCP_575.Shared;
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

        private readonly Dictionary<int, DateTime> _cooldownUntil = new();
        private readonly Dictionary<int, DateTime> _lastCooldownAudioTime = new();
        private readonly Dictionary<int, DateTime> _lastWeaponClickTime = new();
        private readonly HashSet<int> _flickeringPlayers = new();
        private readonly HashSet<int> _pendingItemChanges = new();
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

            foreach (var instanceId in _flickeringPlayers.ToList())
            {
                Timing.KillCoroutines($"{FlickerTagPrefix}{instanceId}");
            }

            foreach (var instanceId in _pendingItemChanges.ToList())
            {
                Timing.KillCoroutines($"{ItemChangePrefix}{instanceId}");
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
            if (!_plugin.IsEventActive || !IsValidPlayer(ev?.Player)) return;

            int instanceId = ev.Player.GameObject.GetInstanceID();

            Timing.KillCoroutines($"{FlickerTagPrefix}{instanceId}");
            _flickeringPlayers.Remove(instanceId);

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

            string coroutineTag = $"{ItemChangePrefix}{instanceId}";

            Timing.KillCoroutines(coroutineTag);
            _pendingItemChanges.Add(instanceId);

            var coroutine = Timing.CallDelayed(0.05f, () =>
            {
                try
                {
                    stateModificationActions.Invoke();
                    if (_plugin.Config.Debug) LibraryLabAPI.LogDebug("OnPlayerChangedItem", $"Enforced dark-state on inventory swap for {ev.Player.Nickname}.");
                }
                finally
                {
                    _pendingItemChanges.Remove(instanceId);
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
            if (!_plugin.IsEventActive || !IsValidPlayer(ev?.Player) || !ev.NewState || !IsPlayerInDarkRoom(ev.Player) || !IsBlackout()) return;

            int instanceId = ev.Player.GameObject.GetInstanceID();
            Timing.RunCoroutine(
                FlickerCoroutine(instanceId, "Flashlight", () => ev.LightItem.IsEmitting, state => ev.LightItem.IsEmitting = state),
                $"{FlickerTagPrefix}{instanceId}"
            );
        }

        public override void OnPlayerToggledWeaponFlashlight(PlayerToggledWeaponFlashlightEventArgs ev)
        {
            if (ev?.Player?.GameObject == null || ev.FirearmItem == null || !HasFlashlight(ev.FirearmItem)) return;

            DateTime now = DateTime.UtcNow;
            int instanceId = ev.Player.GameObject.GetInstanceID();

            if (!_lastWeaponClickTime.TryGetValue(instanceId, out DateTime lastClick) || (now - lastClick).TotalMilliseconds >= 110)
            {
                _lastWeaponClickTime[instanceId] = now;
                _plugin.AudioDirector?.ProcessLightSwitchClick(ev.Player.Position);
            }

            if (!_plugin.IsEventActive || !ev.NewState || !IsPlayerInDarkRoom(ev.Player) || !IsBlackout()) return;

            Timing.RunCoroutine(
                FlickerCoroutine(instanceId, "WeaponFlashlight", () => ev.FirearmItem.FlashlightEnabled, state => ev.FirearmItem.FlashlightEnabled = state),
                $"{FlickerTagPrefix}{instanceId}"
            );
        }

        public override void OnPlayerLeft(PlayerLeftEventArgs ev)
        {
            if (ev?.Player?.GameObject == null) return;
            int instanceId = ev.Player.GameObject.GetInstanceID();

            _cooldownUntil.Remove(instanceId);
            _flickeringPlayers.Remove(instanceId);
            _pendingItemChanges.Remove(instanceId);
            _lastCooldownAudioTime.Remove(instanceId);
            _lastWeaponClickTime.Remove(instanceId);
        }

        #endregion

        #region Public Methods

        public void ApplyLightsourceEffects(Player target)
        {
            if (!IsValidPlayer(target) || target?.GameObject == null) return;

            ApplyCooldown(target);
            int instanceId = target.GameObject.GetInstanceID();

            switch (target.CurrentItem)
            {
                case LightItem lightItem:
                    lightItem.IsEmitting = false;
                    Timing.RunCoroutine(
                        FlickerCoroutine(instanceId, "Flashlight", () => lightItem.IsEmitting, state => lightItem.IsEmitting = state, forceOff: true),
                        $"{FlickerTagPrefix}{instanceId}"
                    );
                    break;
                case FirearmItem firearm when HasFlashlight(firearm):
                    firearm.FlashlightEnabled = false;
                    Timing.RunCoroutine(
                        FlickerCoroutine(instanceId, "WeaponFlashlight", () => firearm.FlashlightEnabled, state => firearm.FlashlightEnabled = state, forceOff: true),
                        $"{FlickerTagPrefix}{instanceId}"
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
            if (player == null) _cooldownUntil.Clear();
            else if (player?.GameObject != null) _cooldownUntil.Remove(player.GameObject.GetInstanceID());
        }

        #endregion

        #region Helper Methods

        private bool IsValidPlayer(Player player) => player?.GameObject != null;
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
            _cooldownUntil[player.GameObject.GetInstanceID()] = DateTime.UtcNow + CooldownDuration;
        }

        private (bool IsAllowed, bool NewState) HandleLightToggling(Player player, bool isAllowed, bool newState, string message)
        {
            if (!newState || !IsBlackout() || player?.GameObject == null) return (isAllowed, newState);

            int instanceId = player.GameObject.GetInstanceID();

            if (_flickeringPlayers.Contains(instanceId)) return (false, false);

            if (_cooldownUntil.TryGetValue(instanceId, out var until) && DateTime.UtcNow < until)
            {
                if (_plugin.Config.HintsConfig.IsEnabledLightEmitterCooldownHint) player.SendHint(message, 1.0f);
                PlayLightsourceErrorFeedback(player, instanceId);
                return (true, false);
            }

            return (isAllowed, newState);
        }

        private IEnumerator<float> FlickerCoroutine(int playerInstanceId, string lightType, Func<bool> getState, Action<bool> setState, bool forceOff = false)
        {
            if (!_flickeringPlayers.Add(playerInstanceId)) yield break;

            try
            {
                var player = Player.ReadyList.FirstOrDefault(p => p.GameObject != null && p.GameObject.GetInstanceID() == playerInstanceId);
                if (player != null)
                {
                    _plugin.AudioDirector?.ProcessLightsourceFlicker(player);
                    if (forceOff) PlayLightsourceErrorFeedback(player, playerInstanceId);
                }

                int flickerCount = Math.Max(2, _random.Next(_config.MinFlickerCount, _config.MaxFlickerCount));
                int totalDurationMs = _random.Next(_config.MinFlickerDurationMs, _config.MaxFlickerDurationMs + 1);
                float delayPerFlicker = (totalDurationMs / 1000f) / flickerCount;

                var targetPlayer = Player.ReadyList.FirstOrDefault(p => p.GameObject != null && p.GameObject.GetInstanceID() == playerInstanceId);
                if (targetPlayer == null) yield break;

                for (int i = 0; i < flickerCount; i++)
                {
                    if (!_plugin.IsEventActive) break;
                    if (!targetPlayer.IsReady || !targetPlayer.IsAlive) break;

                    if (lightType == "WeaponFlashlight")
                    {
                        if (targetPlayer.CurrentItem is not FirearmItem f || !HasFlashlight(f)) break;
                    }
                    else if (lightType == "Flashlight")
                    {
                        if (targetPlayer.CurrentItem is not LightItem) break;
                    }

                    if (targetPlayer != null)
                    {
                        bool isLastIteration = (i == flickerCount - 1);
                        if (!(isLastIteration && forceOff))
                        {
                            _plugin.AudioDirector?.ProcessLightsourceSparkFeedback(targetPlayer, isFinalBlow: false);
                        }
                    }

                    setState(!getState());
                    yield return Timing.WaitForSeconds(delayPerFlicker);
                }

                var finalPlayer = Player.ReadyList.FirstOrDefault(target => target.GameObject != null && target.GameObject.GetInstanceID() == playerInstanceId);
                if (forceOff && _plugin.IsEventActive && finalPlayer != null)
                {
                    setState(false);
                    _plugin.AudioDirector?.ProcessLightsourceSparkFeedback(finalPlayer, isFinalBlow: true);
                }
            }
            finally
            {
                _flickeringPlayers.Remove(playerInstanceId);
            }
        }

        private bool HasFlashlight(FirearmItem firearm)
        {
            if (firearm?.Base?.Attachments == null) return false;

            // Queries native attachments via client network sync identity blocks rather than active visual transforms
            return firearm.Base.Attachments.Any(a => a != null && a.Name == AttachmentName.Flashlight && a.IsEnabled);
        }
        private void PlayLightsourceErrorFeedback(Player player, int instanceId)
        {
            DateTime now = DateTime.UtcNow;
            if (!_lastCooldownAudioTime.TryGetValue(instanceId, out DateTime lastPlayTime) || (now - lastPlayTime).TotalSeconds >= 1.5)
            {
                _lastCooldownAudioTime[instanceId] = now;
                _plugin.AudioDirector?.ProcessLightsourceErrorFeedback(player);
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

                foreach (var instanceId in keys)
                {
                    if (_cooldownUntil.TryGetValue(instanceId, out var expireTime) && now >= expireTime)
                    {
                        _cooldownUntil.Remove(instanceId);
                    }
                }
            }
        }

        #endregion
    }
}