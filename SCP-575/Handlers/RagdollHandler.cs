using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.CustomHandlers;
using System;
using UnityEngine;
using Logger = LabApi.Extensions.Misc.iLogger;

namespace SCP_575.Handlers
{
    /// <summary>
    /// Intercepts physical corpse serialization processes to evaluate damage source signatures and drive post-mortem logic.
    /// </summary>
    public class RagdollHandler : CustomEventsHandler
    {
        #region Fields
        private readonly Plugin _plugin;
        #endregion

        #region Constructor
        public RagdollHandler(Plugin plugin)
        {
            _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
        }
        #endregion

        #region Event Overrides
        /// <summary>
        /// Evaluates active structural remains upon actor expiration, clearing physical remains or handling sound consequences.
        /// </summary>
        public override void OnPlayerSpawnedRagdoll(PlayerSpawnedRagdollEventArgs ev)
        {
            if (!_plugin.IsEventActive || ev is null) return;

            try
            {
                if (ev.Player is null || ev.Ragdoll is null || ev.DamageHandler is null)
                    return;

                if (!_plugin.DamageSystem.IsScp575Damage(ev.DamageHandler))
                    return;

                if (_plugin.Npc.DisableRagdolls)
                {
                    Logger.Debug(nameof(RagdollHandler), "DisableRagdolls enabled. Destroying ragdoll infrastructure instance.", _plugin.Debug);
                    ev.Ragdoll.Destroy();
                }
                else
                {
                    Logger.Debug(nameof(RagdollHandler), $"Processing anomalous post-mortem sequence for target subject: {ev.Player.Nickname}", _plugin.Debug);

                    Vector3 position = ev.Ragdoll.Position;

                    // Forward absolute authority of the local acoustic field to the director subsystem
                    _plugin.AudioDirector?.ProcessRagdollConsumption(position);

                    // Execute downstream status tracking mechanics
                    _plugin.DamageSystem.RagdollProcessor(ev.Player, ev.Ragdoll);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(nameof(RagdollHandler), $"Anomalous ragdoll post-mortem sequencing pipeline interruption: {ex.Message}");
            }
        }
        #endregion
    }
}