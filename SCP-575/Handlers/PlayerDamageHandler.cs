namespace SCP_575.Handlers
{
    using LabApi.Events.Arguments.PlayerEvents;
    using LabApi.Events.Arguments.ServerEvents;
    using LabApi.Events.CustomHandlers;
    using MEC;
    using SCP_575.Shared;
    using System;

    /// <summary>
    /// Intercepts incoming environmental and entity damage updates to evaluate 
    /// anomalous signatures, routing subsequent consequences to dedicated shared sub-systems.
    /// </summary>
    public class PlayerDamageHandler : CustomEventsHandler
    {
        private readonly Plugin _plugin;
        private const string ItemPhysicsTag = CoroutineTags.ItemPhysics;

        // Anti-spam configuration: Tracks timestamps to rate-limit repetitive non-lethal attack audio hooks
        private DateTime _lastAttackAudioTime = DateTime.MinValue;
        private readonly TimeSpan _attackAudioCooldown = TimeSpan.FromSeconds(1.0);

        public PlayerDamageHandler(Plugin plugin)
        {
            _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
        }

        #region Lifecycle Cleanup

        public override void OnServerRoundEnded(RoundEndedEventArgs ev) => Timing.KillCoroutines(ItemPhysicsTag);
        public override void OnServerWaitingForPlayers() => Timing.KillCoroutines(ItemPhysicsTag);

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

                // Offload the downstream lethal actions entirely to the centralized damage manager
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

            // Identify whether the damage vector originates from an active SCP player or the plugin's custom hazard system
            bool isAnomalousAttack = (ev.Attacker != null && ev.Attacker.IsSCP) ||
                                     (ev.DamageHandler != null && Scp575DamageSystem.IsScp575Damage(ev.DamageHandler));

            if (isAnomalousAttack)
            {
                // Pass the timestamp tracking variable by reference ('ref') so the sub-system can directly refresh the cooldown
                Scp575DamageSystem.ProcessAnomalousTrauma(ev.Player, _plugin, ref _lastAttackAudioTime, _attackAudioCooldown);
            }
        }

        #endregion
    }
}