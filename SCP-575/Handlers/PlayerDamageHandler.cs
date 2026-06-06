namespace SCP_575.Handlers
{
    using LabApi.Events.Arguments.PlayerEvents;
    using LabApi.Events.Arguments.ServerEvents;
    using LabApi.Events.CustomHandlers;
    using MEC;
    using SCP_575.Shared;
    using SCP_575.Systems;
    using SCP575.Shared;
    using System;

    /// <summary>
    /// Intercepts severe structural trauma vectors directed at human actors to evaluate 
    /// if the source matches anomalous physical manifestations, governing post-mortem kinetic overrides.
    /// </summary>
    public class PlayerDamageHandler : CustomEventsHandler
    {
        private readonly Plugin _plugin;
        private const string ItemPhysicsTag = CoroutineTags.ItemPhysics;

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
        /// triggering asynchronous inventory propulsion calculations if the kill vector belongs to the entity.
        /// </summary>
        /// <param name="ev">The operational state arguments containing player data and lethal damage tracking signatures.</param>
        public override void OnPlayerDying(PlayerDyingEventArgs ev)
        {
            if (!_plugin.IsEventActive) return;

            try
            {
                if (ev?.Player == null || ev.DamageHandler == null)
                    return;

                if (!Scp575DamageSystem.IsScp575Damage(ev.DamageHandler))
                    return;

                LibraryLabAPI.LogDebug("PlayerDamageHandler", $"Death confirmed from {Scp575DamageSystem.IdentifierName} for {ev.Player.Nickname}. Triggering item physics.");

                // Offloading item kinetic scatter behavior to an isolated coroutine guarantees 
                // the main physics thread is not blocked during complex kinematic evaluations.
                Timing.RunCoroutine(Scp575DamageSystem.DropAndPushItems(ev.Player), ItemPhysicsTag);
            }
            catch (Exception ex)
            {
                LibraryLabAPI.LogError("PlayerDamageHandler", $"Error while processing PlayerDying: {ex}");
            }
        }

        #endregion
    }
}