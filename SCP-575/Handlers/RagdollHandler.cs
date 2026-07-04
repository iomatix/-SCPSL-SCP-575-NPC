namespace SCP_575.Handlers
{
    using LabApi.Events.Arguments.PlayerEvents;
    using LabApi.Events.CustomHandlers;
    using System;
    using UnityEngine;

    /// <summary>
    /// Intercepts physical corpse serialization processes to evaluate damage source signatures,
    /// routing macro graphic modifications and post-mortem presentation logic to specialized subsystems.
    /// </summary>
    public class RagdollHandler : CustomEventsHandler
    {
        private readonly Plugin _plugin;

        public RagdollHandler(Plugin plugin)
        {
            _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
        }

        /// <summary>
        /// Evaluates active structural remains upon actor expiration, clearing physical remains 
        /// if configured, or delegating localized traumatic audio cues to the central Audio Director.
        /// </summary>
        public override void OnPlayerSpawnedRagdoll(PlayerSpawnedRagdollEventArgs ev)
        {
            if (!_plugin.IsEventActive) return;

            try
            {
                if (ev?.Player == null || ev.Ragdoll == null || ev.DamageHandler == null)
                    return;

                if (!_plugin.DamageSystem.IsScp575Damage(ev.DamageHandler))
                    return;

                if (_plugin.Npc.DisableRagdolls)
                {
                    LibraryLabAPI.LogDebug("RagdollHandler", "DisableRagdolls enabled. Destroying ragdoll.");
                    ev.Ragdoll.Destroy();
                }
                else
                {
                    LibraryLabAPI.LogDebug("RagdollHandler", $"Processing anomalous post-mortem sequence for target: {ev.Player.Nickname}");

                    Vector3 position = ev.Ragdoll.Position;

                    // Forward absolute authority of the local acoustic field to the director subsystem
                    _plugin.AudioDirector?.ProcessRagdollConsumption(position);

                    // Execute downstream physiological status tracking updates
                    _plugin.DamageSystem.RagdollProcessor(ev.Player, ev.Ragdoll);
                }
            }
            catch (Exception ex)
            {
                LibraryLabAPI.LogError("RagdollHandler", $"Failed to process ragdoll event: {ex}");
            }
        }
    }
}