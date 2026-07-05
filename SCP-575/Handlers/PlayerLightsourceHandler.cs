using InventorySystem.Items.Firearms.Attachments;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.Arguments.ServerEvents;
using LabApi.Events.CustomHandlers;
using LabApi.Extensions;
using LabApi.Extensions.Misc;
using LabApi.Features.Wrappers;
using MEC;
using SCP_575.ConfigObjects;
using SCP_575.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using Logger = LabApi.Extensions.Misc.iLogger;

namespace SCP_575.Handlers
{
    /// <summary>
    /// Governs electrical degradation and structural suppression of mobile photon emitters deployed inside threat zones.
    /// </summary>
    public class PlayerLightsourceHandler : CustomEventsHandler, IDisposable
    {
        #region Fields & Registries
        private readonly Plugin _plugin;
        private readonly PlayerLightsourceConfig _lightSourceConfig;

        private readonly Dictionary<int, DateTime> _cooldownUntil = new();
        private readonly Dictionary<int, DateTime> _lastCooldownAudioTime = new();
        private readonly Dictionary<int, DateTime> _lastWeaponClickTime = new();
        private readonly HashSet<int> _flickeringPlayers = new();
        private readonly HashSet<int> _pendingItemChanges = new();

        private bool _isDisposed;
        private readonly object _lock = new();

        private const string LightCleanupTag = CoroutineTags.LightCleanup;
        private const string FlickerTagPrefix = CoroutineTags.FlickerPrefix;
        private const string ItemChangePrefix = CoroutineTags.ItemChangePrefix;
        #endregion

        #region Properties
        private float CleanupInterval => _plugin.Config?.HandlerCleanupInterval ?? 160f;
        private TimeSpan CooldownDuration => TimeSpan.FromSeconds(_lightSourceConfig.KeterLightsourceCooldown.LimitMin(1));
        #endregion

        #region Constructor
        public PlayerLightsourceHandler(Plugin plugin)
        {
            _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin), "Plugin instance cannot be null.");
            _lightSourceConfig = _plugin.Lightsource ?? throw new InvalidOperationException("LightsourceConfig is not initialized.");
        }
        #endregion

        #region Lifecycle Management
        public void Initialize()
        {
            if (_isDisposed) return;

            LightCleanupTag.KillCoroutine();
            Timing.RunCoroutine(CleanupCoroutine(), LightCleanupTag);

            Logger.Info(nameof(PlayerLightsourceHandler), "Initialized lightsource tracking subsystem engine channels.");
        }

        public void Clean()
        {
            LightCleanupTag.KillCoroutine();

            lock (_lock)
            {
                foreach (int instanceId in _flickeringPlayers.ToList())
                {
                    $"{FlickerTagPrefix}{instanceId}".KillCoroutine();
                }

                foreach (int instanceId in _pendingItemChanges.ToList())
                {
                    $"{ItemChangePrefix}{instanceId}".KillCoroutine();
                }

                _cooldownUntil.Clear();
                _flickeringPlayers.Clear();
                _pendingItemChanges.Clear();
                _lastCooldownAudioTime.Clear();
                _lastWeaponClickTime.Clear();
            }
        }

        public override void OnServerRoundEnded(RoundEndedEventArgs ev) => Clean();
        public override void OnServerWaitingForPlayers() => Clean();

        public void Dispose()
        {
            if (_isDisposed) return;
            Clean();
            _isDisposed = true;
            Logger.Info(nameof(PlayerLightsourceHandler), "Disposed lightsource tracking handler successfully.");
        }
        #endregion

        #region Event Overrides
        public override void OnPlayerChangedItem(PlayerChangedItemEventArgs ev)
        {
            if (!_plugin.IsEventActive || ev?.Player?.GameObject is null) return;

            int instanceId = ev.Player.GameObject.GetInstanceID();
            $"{FlickerTagPrefix}{instanceId}".KillCoroutine();

            lock (_lock) _flickeringPlayers.Remove(instanceId);

            // Fluent API Upgrade: Checking current light emission state through unifed abstractions
            if (!ev.Player.GetHeldLightSourceState()) return;

            string coroutineTag = $"{ItemChangePrefix}{instanceId}";
            coroutineTag.KillCoroutine();

            lock (_lock) _pendingItemChanges.Add(instanceId);

            CoroutineHandle coroutine = Timing.CallDelayed(0.05f, () =>
            {
                try
                {
                    // Fluent API Upgrade: Unfied dark state enforcement regardless of item tracks
                    ev.Player.SetHeldLightSourceState(false);
                    Logger.Debug(nameof(PlayerLightsourceHandler), $"Enforced dark-state on inventory swap for {ev.Player.Nickname}.", _plugin.Debug);
                }
                finally
                {
                    lock (_lock) _pendingItemChanges.Remove(instanceId);
                }
            });
            coroutine.Tag = coroutineTag;
        }

        public override void OnPlayerTogglingFlashlight(PlayerTogglingFlashlightEventArgs ev)
        {
            if (!_plugin.IsEventActive || ev?.Player?.GameObject is null || !_plugin.NpcLogic.IsBlackoutActive || ev.Player.CurrentItem is not LightItem) return;

            (ev.IsAllowed, ev.NewState) = HandleLightToggling(ev.Player, ev.IsAllowed, ev.NewState, _plugin.Hints.LightEmitterCooldownHint);
        }

        public override void OnPlayerTogglingWeaponFlashlight(PlayerTogglingWeaponFlashlightEventArgs ev)
        {
            if (!_plugin.IsEventActive || ev?.Player?.GameObject is null || ev.FirearmItem is null || !_plugin.NpcLogic.IsBlackoutActive || !ev.FirearmItem.HasAttachment(AttachmentName.Flashlight))
                return;

            (ev.IsAllowed, ev.NewState) = HandleLightToggling(ev.Player, ev.IsAllowed, ev.NewState, _plugin.Hints.LightEmitterCooldownHint);
        }

        public override void OnPlayerToggledFlashlight(PlayerToggledFlashlightEventArgs ev)
        {
            if (!_plugin.IsEventActive || ev?.Player?.GameObject is null || !ev.NewState || !ev.Player.IsInDarkRoom() || !_plugin.NpcLogic.IsBlackoutActive) return;

            TriggerLightsourceFlickerPipeline(ev.Player);
        }

        public override void OnPlayerToggledWeaponFlashlight(PlayerToggledWeaponFlashlightEventArgs ev)
        {
            if (ev?.Player?.GameObject is null || ev.FirearmItem is null || !ev.FirearmItem.HasAttachment(AttachmentName.Flashlight)) return;

            int instanceId = ev.Player.GameObject.GetInstanceID();

            if (_lastWeaponClickTime.TryAcquireLock(instanceId, TimeSpan.FromMilliseconds(110)))
            {
                _plugin.AudioDirector?.ProcessLightSwitchClick(ev.Player.Position);
            }

            if (!_plugin.IsEventActive || !ev.NewState || !ev.Player.IsInDarkRoom() || !_plugin.NpcLogic.IsBlackoutActive) return;

            TriggerLightsourceFlickerPipeline(ev.Player);
        }

        public override void OnPlayerLeft(PlayerLeftEventArgs ev)
        {
            if (ev?.Player?.GameObject is null) return;
            int instanceId = ev.Player.GameObject.GetInstanceID();

            lock (_lock)
            {
                _cooldownUntil.Remove(instanceId);
                _flickeringPlayers.Remove(instanceId);
                _pendingItemChanges.Remove(instanceId);
                _lastCooldownAudioTime.Remove(instanceId);
                _lastWeaponClickTime.Remove(instanceId);
            }
        }
        #endregion

        #region Public Interface API
        public void ApplyLightsourceEffects(Player target)
        {
            if (target?.GameObject is null) return;

            ApplyCooldown(target);
            TriggerLightsourceFlickerPipeline(target, forceOff: true);
        }

        public void ForceCooldown(Player player)
        {
            if (player?.GameObject is null) return;

            ApplyCooldown(player);
            if (_plugin.Hints.IsEnabledLightEmitterCooldownHint)
                player.SendHint(_plugin.Hints.LightEmitterCooldownHint, 1.75f);
        }

        public void ClearCooldown(Player player = null)
        {
            lock (_lock)
            {
                if (player is null) _cooldownUntil.Clear();
                else if (player.GameObject is not null) _cooldownUntil.Remove(player.GameObject.GetInstanceID());
            }
        }
        #endregion

        #region Internal Diagnostics & Orchestration
        private void ApplyCooldown(Player player)
        {
            lock (_lock) _cooldownUntil[player.GameObject.GetInstanceID()] = DateTime.UtcNow + CooldownDuration;
        }

        private (bool IsAllowed, bool NewState) HandleLightToggling(Player player, bool isAllowed, bool newState, string message)
        {
            if (!newState || !_plugin.NpcLogic.IsBlackoutActive || player?.GameObject is null) return (isAllowed, newState);

            int instanceId = player.GameObject.GetInstanceID();
            if (_flickeringPlayers.Contains(instanceId)) return (false, false);

            if (_cooldownUntil.IsCooldownActive(instanceId))
            {
                if (_plugin.Hints.IsEnabledLightEmitterCooldownHint) player.SendHint(message, 1.0f);
                PlayLightsourceErrorFeedback(player, instanceId);
                return (true, false);
            }

            return (isAllowed, newState);
        }

        /// <summary>
        /// Unified pipeline launcher to execute flicker animations using extended core API abstractions cleanly.
        /// </summary>
        private void TriggerLightsourceFlickerPipeline(Player player, bool forceOff = false)
        {
            int instanceId = player.GameObject.GetInstanceID();

            lock (_lock)
            {
                if (!_flickeringPlayers.Add(instanceId)) return;
            }

            // Calculation maps using SafeRandom primitives completely insulated from standard garbage collection allocations
            int flickerCount = SafeRandom.Next(_lightSourceConfig.MinFlickerCount, _lightSourceConfig.MaxFlickerCount).LimitMin(2);
            int totalDurationMs = SafeRandom.Next(_lightSourceConfig.MinFlickerDurationMs, _lightSourceConfig.MaxFlickerDurationMs + 1);
            float delayPerFlicker = (totalDurationMs / 1000f) / flickerCount;

            _plugin.AudioDirector?.ProcessLightsourceFlicker(player);
            if (forceOff) PlayLightsourceErrorFeedback(player, instanceId);

            // Fluent API Upgrade: Invoking the unified extension pipeline with a dynamic feedback delegate matrix
            Timing.RunCoroutine(
                player.FlickerHeldLightSourceCoroutine(flickerCount, delayPerFlicker, forceOff, (targetPlayer, isFinalBlow) =>
                {
                    _plugin.AudioDirector?.ProcessLightsourceSparkFeedback(targetPlayer, isFinalBlow);
                }),
                $"{FlickerTagPrefix}{instanceId}"
            );

            // Register completion tracking out-of-frame to drop instance gates seamlessly
            Timing.CallDelayed(totalDurationMs / 1000f + 0.05f, () => { lock (_lock) _flickeringPlayers.Remove(instanceId); });
        }

        private void PlayLightsourceErrorFeedback(Player player, int instanceId)
        {
            if (_lastCooldownAudioTime.TryAcquireLock(instanceId, TimeSpan.FromSeconds(1.5)))
            {
                _plugin.AudioDirector?.ProcessLightsourceErrorFeedback(player);
            }
        }

        private IEnumerator<float> CleanupCoroutine()
        {
            while (!_isDisposed)
            {
                yield return Timing.WaitForSeconds(CleanupInterval);

                if (_isDisposed || !_plugin.IsEventActive || _cooldownUntil.Count == 0) continue;

                lock (_lock) _cooldownUntil.PruneExpired(DateTime.UtcNow);
            }
        }
        #endregion
    }
}