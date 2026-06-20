namespace SCP_575.Handlers
{
    using LabApi.Events.Arguments.PlayerEvents;
    using LabApi.Events.Arguments.ServerEvents;
    using LabApi.Events.CustomHandlers;
    using MEC;
    using SCP_575.Shared;
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Intercepts network physiological trauma updates, filtering anomalous signatures 
    /// to manage acoustic headroom overrides and suppress low-priority psychological cues during combat.
    /// </summary>
    public class PlayerDamageHandler : CustomEventsHandler
    {
        private readonly Plugin _plugin;
        private const string ItemPhysicsTag = CoroutineTags.ItemPhysics;

        private readonly Dictionary<int, DateTime> _playerLastAttackAudioTime = new();
        private readonly TimeSpan _attackAudioCooldown = TimeSpan.FromSeconds(1.2);

        public PlayerDamageHandler(Plugin plugin)
        {
            _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
        }

        #region Lifecycle Management

        public override void OnServerRoundEnded(RoundEndedEventArgs ev) => Clean();
        public override void OnServerWaitingForPlayers() => Clean();

        /// <summary>
        /// Evicts disconnected network identifiers to prevent cumulative reference accumulation on the heap.
        /// </summary>
        public override void OnPlayerLeft(PlayerLeftEventArgs ev)
        {
            if (ev?.Player?.GameObject == null) return;

            _playerLastAttackAudioTime.Remove(ev.Player.GameObject.GetInstanceID());
        }

        private void Clean()
        {
            Timing.KillCoroutines(ItemPhysicsTag);
            _playerLastAttackAudioTime.Clear();
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Intercepts lethal context vectors immediately before physiological teardown execution
        /// to ensure critical post-mortem structural sound processing finishes deterministically.
        /// </summary>
        public override void OnPlayerDying(PlayerDyingEventArgs ev)
        {
            if (!_plugin.IsEventActive || ev?.Player == null || ev.DamageHandler == null) return;

            try
            {
                if (!Scp575DamageSystem.IsScp575Damage(ev.DamageHandler)) return;

                Scp575DamageSystem.ProcessLethalStrike(ev.Player, _plugin);
            }
            catch (Exception ex)
            {
                LibraryLabAPI.LogError("PlayerDamageHandler", $"Error while processing PlayerDying: {ex}");
            }
        }

        /// <summary>
        /// Evaluates defensive parameters during incoming hits to prioritize high-energy impact reactions
        /// over subtle psychological paranoia loops.
        /// </summary>
        public override void OnPlayerHurting(PlayerHurtingEventArgs ev)
        {
            if (!_plugin.IsEventActive || ev?.Player == null) return;

            bool isPhysicalScpAttack = ev.Attacker != null && ev.Attacker.IsSCP;

            if (isPhysicalScpAttack)
            {
                int instanceId = ev.Player.GameObject.GetInstanceID();

                if (!_playerLastAttackAudioTime.TryGetValue(instanceId, out var lastTime))
                {
                    lastTime = DateTime.MinValue;
                }

                DateTime tempTime = lastTime;
                Scp575DamageSystem.ProcessAnomalousTrauma(ev.Player, _plugin, ref tempTime, _attackAudioCooldown);
                _playerLastAttackAudioTime[instanceId] = tempTime;

                // Forces the Audio Director to clean up the mix, blinding out subtle whispers while raw meat-grinder sounds play out
                _plugin.AudioDirector?.SuppressPsychologicalAudio(ev.Player, 3.5f);
            }
        }

        #endregion
    }
}