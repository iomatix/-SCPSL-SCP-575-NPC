namespace SCP_575.Handlers
{
    using LabApi.Events.Arguments.PlayerEvents;
    using LabApi.Events.CustomHandlers;
    using SCP_575.Shared;
    using SCP_575.Systems;
    using System;

    /// <summary>
    /// Intercepts physical corpse serialization processes to execute advanced anatomical modifications 
    /// or clean up physical remains upon lethal event executions.
    /// </summary>
    public class RagdollHandler : CustomEventsHandler
    {
        private readonly Plugin _plugin;

        public RagdollHandler(Plugin plugin)
        {
            _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
        }

        /// <summary>
        /// Inspects structural ragdoll telemetry upon player expiration, verifying source vectors 
        /// before applying configured visual suppression or advanced anatomical configurations.
        /// </summary>
        /// <param name="ev">Operational structural details concerning the newly initialized corpse physics matrix.</param>
        public override void OnPlayerSpawnedRagdoll(PlayerSpawnedRagdollEventArgs ev)
        {
            if (!_plugin.IsEventActive) return;

            try
            {
                if (ev?.Player == null || ev.Ragdoll == null || ev.DamageHandler == null)
                    return;

                if (!Scp575DamageSystem.IsScp575Damage(ev.DamageHandler))
                    return;

                if (_plugin.Config.NpcConfig.DisableRagdolls)
                {
                    LibraryLabAPI.LogDebug("RagdollHandler", "DisableRagdolls enabled. Destroying ragdoll.");
                    ev.Ragdoll.Destroy();
                }
                else
                {
                    LibraryLabAPI.LogDebug("RagdollHandler", $"Processing SCP-575 ragdoll for {ev.Player.Nickname}.");
                    Scp575DamageSystem.RagdollProcessor(ev.Player, ev.Ragdoll);
                }
            }
            catch (Exception ex)
            {
                LibraryLabAPI.LogError("RagdollHandler", $"Failed to process ragdoll event: {ex}");
            }
        }
    }
}