namespace SCP_575.Handlers
{
    using LabApi.Events.Arguments.PlayerEvents;
    using LabApi.Events.Arguments.ServerEvents;
    using LabApi.Events.CustomHandlers;
    using MEC;
    using SCP_575.Shared;
    using SCP_575.Systems;
    using System;

    /// <summary>
    /// Handles all SCP-575 related player damage events.
    /// Responsible for validating SCP-575 damage and triggering
    /// additional effects when players are hurt or killed.
    /// </summary>
    public class PlayerDamageHandler : CustomEventsHandler
    {
        private readonly Plugin _plugin;
        private const string ItemPhysicsTag = "SCP575-ItemPhysics";

        public PlayerDamageHandler(Plugin plugin)
        {
            _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
        }

        #region Lifecycle Cleanup

        public override void OnServerRoundEnded(RoundEndedEventArgs ev)
        {
            Timing.KillCoroutines(ItemPhysicsTag);
        }

        public override void OnServerWaitingForPlayers()
        {
            Timing.KillCoroutines(ItemPhysicsTag);
        }

        #endregion

        #region Event Handlers

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