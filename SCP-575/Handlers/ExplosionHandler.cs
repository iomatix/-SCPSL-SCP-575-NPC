namespace SCP_575.Handlers
{
    using LabApi.Events.Arguments.ServerEvents;
    using LabApi.Events.CustomHandlers;
    using SCP_575.Shared;
    using SCP_575.Shared.Audio.Enums;
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
                    _plugin.AudioManager.PlayAudioAtPosition(AudioKey.AnomalousImpact, position, isTransient: true);
                    _plugin.AudioManager.PlayGlobalAudioAutoManaged(AudioKey.Scream_1);
                    _lib.DisableRoomAndNeighborLights(room);

                    if (isBlackoutActive)
                    {
                        float boostDuration = _plugin.Config.BlackoutConfig.DurationMin;

                        MEC.Timing.RunCoroutine(TriggerTacticalBlackoutBoost(boostDuration), CoroutineTags.BlackoutStacks);
                    }
                    break;

                case ScpProjectileImpactType.ProjectileImpactType.Dangerous:
                    AudioKey selectedScream = UnityEngine.Random.value > 0.45f ? AudioKey.ScreamAngry : AudioKey.ScreamHurt;
                    _plugin.AudioManager.PlayAudioAtPosition(selectedScream, position, isTransient: true);

                    _lib.EnableAndFlickerRoomAndNeighborLights(room, _plugin.Config.BlackoutConfig.ElevatorLockdownProbability);
                    break;

                default:
                    if (room.LightController.LightsEnabled) return;

                    _plugin.AudioManager.PlayAudioAtPosition(AudioKey.Whispers_1, position);
                    break;
            }
        }

        /// <summary>
        /// Coroutine temporary boosts the intensity of the tactical blackout.
        /// </summary>
        private IEnumerator<float> TriggerTacticalBlackoutBoost(float duration)
        {
            _plugin.Npc.Methods.IncrementBlackoutStack();
            LibraryLabAPI.LogInfo("ProjectileImpact", $"Blackout intensified via tactical projectile! Current stacks: {_plugin.Npc.Methods.GetCurrentBlackoutStacks}");

            _plugin.AudioManager.PlayGlobalAudioAutoManaged(AudioKey.MonsterRoarGlobal);

            yield return MEC.Timing.WaitForSeconds(duration);

            _plugin.Npc.Methods.DecrementBlackoutStack();
            LibraryLabAPI.LogInfo("ProjectileImpact", $"Tactical projectile blackout boost expired. Current stacks: {_plugin.Npc.Methods.GetCurrentBlackoutStacks}");
        }
    }
}