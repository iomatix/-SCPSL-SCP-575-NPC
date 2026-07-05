using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.Arguments.ServerEvents;
using LabApi.Events.CustomHandlers;
using LabApi.Extensions;
using SCP_575.Shared;
using System;
using System.Collections.Generic;
using Logger = LabApi.Extensions.Misc.iLogger;

namespace SCP_575.Handlers
{
    /// <summary>
    /// Intercepts network physiological trauma updates, filtering anomalous signatures to manage acoustic headroom overrides.
    /// </summary>
    public class PlayerDamageHandler : CustomEventsHandler
    {
        #region Fields & Registries
        private readonly Plugin _plugin;
        private readonly Dictionary<int, DateTime> _playerLastAttackAudioTime = new();
        private const string ItemPhysicsTag = CoroutineTags.ItemPhysics;
        #endregion

        #region Constructor
        public PlayerDamageHandler(Plugin plugin)
        {
            _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
        }
        #endregion

        #region Lifecycle Management
        public override void OnServerRoundEnded(RoundEndedEventArgs ev) => Clean();
        public override void OnServerWaitingForPlayers() => Clean();

        public override void OnPlayerLeft(PlayerLeftEventArgs ev)
        {
            if (ev?.Player?.GameObject is null) return;

            _playerLastAttackAudioTime.Remove(ev.Player.GameObject.GetInstanceID());
        }

        private void Clean()
        {
            // Fluent API Alignment: Leverage native string token extensions for coroutine evictions
            ItemPhysicsTag.KillCoroutine();
            _playerLastAttackAudioTime.Clear();
        }
        #endregion

        #region Event Overrides
        public override void OnPlayerDying(PlayerDyingEventArgs ev)
        {
            if (!_plugin.IsEventActive || ev is null) return;

            try
            {
                if (ev.Player is null || ev.DamageHandler is null) return;
                if (!_plugin.DamageSystem.IsScp575Damage(ev.DamageHandler)) return;

                _plugin.DamageSystem.ProcessLethalStrike(ev.Player);
            }
            catch (Exception ex)
            {
                Logger.Error(nameof(PlayerDamageHandler), $"Processing failure inside OnPlayerDying sequence: {ex.Message}");
            }
        }

        public override void OnPlayerHurting(PlayerHurtingEventArgs ev)
        {
            if (!_plugin.IsEventActive || ev is null) return;

            bool isPhysicalScpAttack = ev.Attacker is not null && ev.Attacker.IsSCP;
            bool isCustom575Attack = ev.DamageHandler is not null && _plugin.DamageSystem.IsScp575Damage(ev.DamageHandler);

            if (isCustom575Attack)
            {
                if (ev.Player?.GameObject is null) return;
                int instanceId = ev.Player.GameObject.GetInstanceID();

                // Architectural Optimization: 'out' parameter initialization implicitly defaults to DateTime.MinValue
                // if lookup fails, eliminating redundant conditional verification branches completely.
                _playerLastAttackAudioTime.TryGetValue(instanceId, out DateTime lastTime);

                TimeSpan audioCooldownWindow = TimeSpan.FromSeconds(_plugin.Sanity.AttackAudioCooldownSeconds);

                _plugin.DamageSystem.ProcessAnomalousTrauma(ev.Player, ref lastTime, audioCooldownWindow);
                _playerLastAttackAudioTime[instanceId] = lastTime;

                _plugin.AudioDirector?.SuppressPsychologicalAudio(ev.Player, 3.5f);
            }
            else if (isPhysicalScpAttack)
            {
                float dropAmount = _plugin.Sanity.ScpHitSanityDrop;
                if (dropAmount > 0f)
                {
                    // Unified Architecture Hook: Routing relative drop metrics straight to the zf-aligned Sanity Handler
                    _plugin.SanityHandler?.ChangeSanityValue(ev.Player, -dropAmount);
                }
            }
        }
        #endregion
    }
}