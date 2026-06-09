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
                    _plugin.AudioManager.PlayAudioAtPosition(AudioKey.AnomalousImpact, position, isTransient: true);

                    // Enhanced spatialized vortex layered smoothly over your baseline logic
                    _plugin.AudioManager.PlayOrbitingAudio(
                        staticPosition: position,
                        audioKey: AudioKey.WhispersMixed,
                        lifespan: 4.0f,
                        maxRadius: 7.0f,
                        minRadius: 0.5f,
                        angularSpeed: 6.0f,
                        approachSpeed: 2.2f
                    );

                    _plugin.AudioManager.PlayGlobalAudioAutoManaged(AudioKey.Scream_1);
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
                            () => _plugin.AudioManager.PlayGlobalAudioAutoManaged(AudioKey.MonsterRoarGlobal)
                        );
                    }
                    break;

                case ScpProjectileImpactType.ProjectileImpactType.Dangerous:
                    // RESTORED: Your exact, highly dynamic RNG check
                    AudioKey selectedScream = UnityEngine.Random.value > 0.45f ? AudioKey.ScreamAngry : AudioKey.ScreamHurt;
                    _plugin.AudioManager.PlayAudioAtPosition(AudioKey.AnomalousImpact, position, isTransient: true);

                    // RESTORED: Your exact custom speed parameters that made the audio trajectory feel punchy
                    _plugin.AudioManager.PlayOrbitingAudio(
                        staticPosition: position,
                        audioKey: selectedScream,
                        lifespan: null,
                        maxRadius: 5.5f,
                        minRadius: 0.8f,
                        angularSpeed: 4.5f,
                        approachSpeed: 5.2f
                    );

                    // RESTORED: Your operational facility grid manipulation method
                    _lib.EnableAndFlickerRoomAndNeighborLights(room, _plugin.Config.BlackoutConfig.ElevatorLockdownProbability);
                    break;

                default:
                    // RESTORED: Fallback evaluation logic for non-standard impacts inside unlit zones
                    if (room.LightController.LightsEnabled) return;

                    _plugin.AudioManager.PlayAudioAtPosition(AudioKey.Whispers_1, position);
                    break;
            }
        }
    }
}