using LabApi.Events.Arguments.ServerEvents;
using LabApi.Events.CustomHandlers;
using LabApi.Extensions;
using LabApi.Features.Wrappers;
using SCP_575.Types;
using System;
using UnityEngine;

namespace SCP_575.Handlers
{
    /// <summary>
    /// Intercepts facility explosion vectors, cascading blackout overrides across room and elevator topologies.
    /// </summary>
    public class ExplosionHandler : CustomEventsHandler
    {
        #region Fields
        private readonly Plugin _plugin;
        #endregion

        #region Constructor
        public ExplosionHandler(Plugin plugin)
        {
            _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
        }
        #endregion

        #region Event Overrides
        public override void OnServerExplosionSpawned(ExplosionSpawnedEventArgs ev)
        {
            if (!_plugin.IsEventActive || ev is null) return;

            var impactType = ScpProjectileImpactType.ClassifyExplosionImpact(ev.ExplosionType, _plugin.Debug);
            ProcessImpact(ev.Position, impactType);
        }

        public override void OnServerProjectileExploded(ProjectileExplodedEventArgs ev)
        {
            if (!_plugin.IsEventActive || ev?.TimedGrenade is null) return;

            var impactType = ScpProjectileImpactType.ClassifyProjectileImpact(ev.TimedGrenade, _plugin.Debug);
            ProcessImpact(ev.Position, impactType);
        }
        #endregion

        #region Core Impact Processor
        private void ProcessImpact(Vector3 position, ScpProjectileImpactType.ProjectileImpactType impactType)
        {
            Room room = position.GetRoom();
            if (room is null) return;

            bool isBlackoutActive = _plugin.NpcLogic.IsBlackoutActive;

            if (impactType is ScpProjectileImpactType.ProjectileImpactType.Dangerous && room.LightController.LightsEnabled)
                return;

            _plugin.AudioDirector?.ProcessExplosionImpact(position, impactType, isBlackoutActive);

            switch (impactType)
            {
                case ScpProjectileImpactType.ProjectileImpactType.Helpful:
                    room.TurnOffRoomAndNeighborLights(_plugin.Blackout.DurationMin, forced: true);

                    // Fluent API Upgrade: Propagate the blackout to all elevators currently located at this floor cluster seamlessly
                    room.ExecuteActionOnRoomAndNeighbors(targetRoom =>
                    {
                        foreach (Elevator elevator in targetRoom.GetElevatorsConnectedToRoom())
                        {
                            elevator.TurnOffLights(_plugin.Blackout.DurationMin);
                        }
                    });

                    if (isBlackoutActive)
                    {
                        _plugin.NpcLogic.StartTimedBlackoutBoost(
                            _plugin.Blackout.DurationMin,
                            nameof(ExplosionHandler),
                            $"Blackout intensified via tactical projectile. Stacks: {_plugin.NpcLogic.GetCurrentBlackoutStacks + 1}",
                            $"Tactical projectile blackout boost expired. Stacks: {_plugin.NpcLogic.GetCurrentBlackoutStacks}",
                            () => _plugin.AudioDirector?.ProcessExplosionImpactBoostFeedback()
                        );
                    }
                    break;

                case ScpProjectileImpactType.ProjectileImpactType.Dangerous:
                    room.TurnOnRoomAndNeighborLights(1.35f);

                    // Fluent API Upgrade: Restore standard operational power to elevators resting at the current floor cluster
                    room.ExecuteActionOnRoomAndNeighbors(targetRoom =>
                    {
                        foreach (Elevator elevator in targetRoom.GetElevatorsConnectedToRoom())
                        {
                            elevator.TurnOnLights();
                        }
                    });
                    break;
            }
        }
        #endregion
    }
}