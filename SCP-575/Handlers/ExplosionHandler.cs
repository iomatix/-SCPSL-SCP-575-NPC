namespace SCP_575.Handlers
{
    using LabApi.Events.Arguments.ServerEvents;
    using LabApi.Events.CustomHandlers;
    using SCP_575.Shared;
    using SCP_575.Shared.Audio.Enums;
    using System;
    using System.Collections.Generic;
    using Types;
    using UnityEngine;

    /// <summary>
    /// Coordinates environmental reactions to explosive payloads, adjusting local facility lighting 
    /// and spatial acoustic tension based on the tactical utility of the detonated projectile.
    /// </summary>
    public class ExplosionHandler : CustomEventsHandler
    {
        private readonly Plugin _plugin;
        private readonly LibraryLabAPI _lib;

        public ExplosionHandler(Plugin plugin)
        {
            _plugin = plugin;
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

            // RESTORED: Core guard clause for dangerous impact type
            if (impactType == ScpProjectileImpactType.ProjectileImpactType.Dangerous && room.LightController.LightsEnabled)
                return;

            switch (impactType)
            {
                case ScpProjectileImpactType.ProjectileImpactType.Helpful:
                    _plugin.AudioManager.PlayAtPosition(AudioKey.AnomalousImpact, position);

                    // Enhanced spatialized vortex layered smoothly over your baseline logic
                    _plugin.AudioManager.PlayOrbitingAudio(
                        staticPosition: position,
                        audioKey: AudioKey.ScreamAngry,
                        maxRadius: 8.5f,
                        minRadius: 0.5f,
                        angularSpeed: 3.5f,
                        approachSpeed: 2.75f
                    );

                    _plugin.AudioManager.PlayGlobal(AudioKey.Whispers_2);
                    _lib.DisableRoomAndNeighborLights(room);

                    if (isBlackoutActive)
                    {
                        float boostDuration = _plugin.Config.BlackoutConfig.DurationMin;

                        // Thread-safe centralized boost execution protecting stack states
                        _plugin.Npc.Methods.StartTimedBlackoutBoost(
                            boostDuration,
                            "ProjectileImpact",
                            $"Blackout intensified via tactical projectile! Current stacks: {_plugin.Npc.Methods.GetCurrentBlackoutStacks + 1}",
                            $"Tactical projectile blackout boost expired. Current stacks: {_plugin.Npc.Methods.GetCurrentBlackoutStacks}",
                            () => _plugin.AudioManager.PlayGlobal(AudioKey.MonsterBreathLocal)
                        );
                    }
                    break;

                case ScpProjectileImpactType.ProjectileImpactType.Dangerous:

                    AudioKey selectedScream = UnityEngine.Random.value > 0.45f ? AudioKey.ScreamAngry : AudioKey.ScreamHurt;
                    _plugin.AudioManager.PlayAtPosition(AudioKey.AnomalousImpact, position);

                    _plugin.AudioManager.PlayOrbitingAudio(
                        staticPosition: position,
                        audioKey: selectedScream,
                        maxRadius: 9.5f,
                        minRadius: 0.8f,
                        angularSpeed: 2.65f,
                        approachSpeed: 2.55f
                    );

                    _lib.EnableAndFlickerRoomAndNeighborLights(room, _plugin.Config.BlackoutConfig.ElevatorLockdownProbability);
                    break;

                default:
                    if (room.LightController.LightsEnabled) return;

                    _plugin.AudioManager.PlayAtPosition(AudioKey.Whispers_1, position);
                    break;
            }
        }
    }
}