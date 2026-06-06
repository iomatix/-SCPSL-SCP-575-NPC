namespace SCP_575.Handlers
{
    using LabApi.Events.Arguments.ServerEvents;
    using LabApi.Events.CustomHandlers;
    using SCP_575.Shared;
    using SCP_575.Shared.Audio.Enums;
    using Types;
    using UnityEngine;

    /// <summary>
    /// Manages explosion-related interactions, including light manipulation and acoustic responses
    /// when grenades or special items are used during a blackout.
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
            bool blackout = _plugin.Npc.Methods.IsBlackoutActive;

            // Early exit for dangerous impacts if there is no blackout,
            // saving the cost of a spatial room query.
            if (impactType == ScpProjectileImpactType.ProjectileImpactType.Dangerous && !blackout)
                return;

            var room = _lib.GetRoomAtPosition(position);
            if (room == null) return;

            switch (impactType)
            {
                case ScpProjectileImpactType.ProjectileImpactType.Helpful:
                    _lib.DisableRoomAndNeighborLights(room);
                    _plugin.AudioManager.PlayAudioAtPosition(AudioKey.WhispersBang, position);
                    _plugin.AudioManager.PlayAmbience();
                    break;

                case ScpProjectileImpactType.ProjectileImpactType.Dangerous:
                    // We already checked !blackout earlier, so we only need to check lights now.
                    if (room.LightController.LightsEnabled) return;

                    _lib.EnableAndFlickerRoomAndNeighborLights(
                        room,
                        _plugin.Config.BlackoutConfig.ElevatorLockdownProbability);
                    _plugin.AudioManager.PlayAudioAtPosition(AudioKey.ScreamAngry, position);
                    break;

                default:
                    _plugin.AudioManager.PlayAudioAtPosition(AudioKey.Whispers, position);
                    break;
            }
        }
    }
}