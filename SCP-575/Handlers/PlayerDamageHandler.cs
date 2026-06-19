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
    /// Intercepts incoming environmental and entity damage updates to evaluate 
    /// anomalous signatures, routing subsequent consequences to dedicated shared sub-systems.
    /// </summary>
    public class PlayerDamageHandler : CustomEventsHandler
    {
        private readonly Plugin _plugin;
        private const string ItemPhysicsTag = CoroutineTags.ItemPhysics;

        // FIX: Replaced global timestamp with a per-player dictionary to prevent cross-player audio suppression
        private readonly Dictionary<string, DateTime> _playerLastAttackAudioTime = new(StringComparer.OrdinalIgnoreCase);
        private readonly TimeSpan _attackAudioCooldown = TimeSpan.FromSeconds(1.2); // Slightly increased for better acoustic spacing

        public PlayerDamageHandler(Plugin plugin)
        {
            _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
        }

        #region Lifecycle Cleanup

        public override void OnServerRoundEnded(RoundEndedEventArgs ev) => Clean();
        public override void OnServerWaitingForPlayers() => Clean();

        /// <summary>
        /// Flushes all tracking nodes to prevent continuous heap allocation accumulation.
        /// </summary>
        private void Clean()
        {
            Timing.KillCoroutines(ItemPhysicsTag);
            _playerLastAttackAudioTime.Clear();
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Intercepts the final physiological state of a human actor before death confirmation,
        /// forwarding validated SCP-575 fatal blows to the specialized damage processing sub-system.
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
        /// Intercepts early-stage damage hooks to filter anomalous traumas, processing sanity shifts
        /// and dynamic sensory feedback loops for living victims.
        /// </summary>
        public override void OnPlayerHurting(PlayerHurtingEventArgs ev)
        {
            if (!_plugin.IsEventActive || ev?.Player == null) return;

            bool isPhysicalScpAttack = ev.Attacker != null && ev.Attacker.IsSCP;

            if (isPhysicalScpAttack)
            {
                string userId = ev.Player.UserId;

                if (!_playerLastAttackAudioTime.TryGetValue(userId, out var lastTime))
                {
                    lastTime = DateTime.MinValue;
                }

                // FIX: Use a local proxy copy to safely bypass C# 'ref' restrictions on dictionary values
                DateTime tempTime = lastTime;
                Scp575DamageSystem.ProcessAnomalousTrauma(ev.Player, _plugin, ref tempTime, _attackAudioCooldown);
                _playerLastAttackAudioTime[userId] = tempTime;
            }
        }

        #endregion
    }
}