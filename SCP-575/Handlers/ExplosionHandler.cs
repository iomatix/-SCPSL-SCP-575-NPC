namespace SCP_575.Handlers
{
    using LabApi.Events.Arguments.ServerEvents;
    using LabApi.Events.CustomHandlers;
    using SCP_575.Shared;
    using System;
    using Types;
    using UnityEngine;

    /// <summary>
    /// Intercepts facility explosion vectors, routing physical state updates to light controllers
    /// and offloading dramatic narrative audio consequences to the central Audio Director.
    /// </summary>
    public class ExplosionHandler : CustomEventsHandler
    {
        private readonly Plugin _plugin;
        private readonly LibraryLabAPI _lib;

        public ExplosionHandler(Plugin plugin)
        {
            _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
            _lib = plugin.LibraryLabAPI;
        }

        public override void OnServerExplosionSpawned(ExplosionSpawnedEventArgs ev)
        {
            if (!_plugin.IsEventActive || ev == null) return;

            var impactType = ScpProjectileImpactType.ClassifyExplosionImpact(ev.ExplosionType);
            ProcessImpact(ev.Position, impactType);
        }

        public override void OnServerProjectileExploded(ProjectileExplodedEventArgs ev)
        {
            if (!_plugin.IsEventActive || ev?.TimedGrenade == null) return;

            var impactType = ScpProjectileImpactType.ClassifyProjectileImpact(ev.TimedGrenade);
            ProcessImpact(ev.Position, impactType);
        }

        private void ProcessImpact(Vector3 position, ScpProjectileImpactType.ProjectileImpactType impactType)
        {
            var room = _lib.GetRoomAtPosition(position);
            if (room == null) return;

            bool isBlackoutActive = _plugin.Npc.Methods.IsBlackoutActive;

            if (impactType == ScpProjectileImpactType.ProjectileImpactType.Dangerous && room.LightController.LightsEnabled)
                return;

            // Hand over absolute command of the acoustic field to the director subsystem
            _plugin.AudioDirector?.ProcessExplosionImpact(position, impactType, isBlackoutActive);

            // Execute tactical infrastructure mutations
            switch (impactType)
            {
                case ScpProjectileImpactType.ProjectileImpactType.Helpful:
                    _lib.DisableRoomAndNeighborLights(room);

                    if (isBlackoutActive)
                    {
                        _plugin.Npc.Methods.StartTimedBlackoutBoost(
                            _plugin.Config.BlackoutConfig.DurationMin,
                            "ProjectileImpact",
                            $"Blackout intensified via tactical projectile! Current stacks: {_plugin.Npc.Methods.GetCurrentBlackoutStacks + 1}",
                            $"Tactical projectile blackout boost expired. Current stacks: {_plugin.Npc.Methods.GetCurrentBlackoutStacks}",
                            () => _plugin.AudioDirector?.ProcessExplosionImpactBoostFeedback()
                        );
                    }
                    break;

                case ScpProjectileImpactType.ProjectileImpactType.Dangerous:
                    _lib.EnableAndFlickerRoomAndNeighborLights(room, _plugin.Config.BlackoutConfig.ElevatorLockdownProbability);
                    break;
            }
        }
    }
}