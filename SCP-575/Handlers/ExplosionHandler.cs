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

            if (impactType == ScpProjectileImpactType.ProjectileImpactType.Dangerous && room.LightController.LightsEnabled)
                return;

            switch (impactType)
            {
                case ScpProjectileImpactType.ProjectileImpactType.Helpful:
                    // 1. Structural impact physical sound anchor
                    _plugin.AudioManager.PlayAudioAtPosition(AudioKey.AnomalousImpact, position, isTransient: true);

                    // 2. Swirling spatial vortex that aggressively collapses into the point of impact
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
                    // 1. Solid impact audio feedback
                    _plugin.AudioManager.PlayAudioAtPosition(AudioKey.AnomalousImpact, position, isTransient: true);

                    // 2. Dynamic evaluation: select an explicit acoustic retaliation response based on RNG
                    AudioKey dynamicPainScream = UnityEngine.Random.Range(0, 2) == 0 ? AudioKey.ScreamAngry : AudioKey.ScreamHurt;

                    // 3. Wide, chaotic sound dispersion rotating heavily around the detonation origin point
                    _plugin.AudioManager.PlayOrbitingAudio(
                        staticPosition: position,
                        audioKey: dynamicPainScream,
                        lifespan: 4.5f,
                        maxRadius: 8.5f,
                        minRadius: 1.5f,
                        angularSpeed: 4.5f,
                        approachSpeed: 1.5f
                    );

                    LibraryLabAPI.LogInfo("ProjectileImpact", $"Dangerous impact spatialized at coordinates via tracking key: {dynamicPainScream}");
                    break;
            }
        }

    }
}