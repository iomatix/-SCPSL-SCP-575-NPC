namespace SCP_575.Handlers
{
    using LabApi.Events.Arguments.ServerEvents;
    using LabApi.Events.CustomHandlers;
    using SCP_575.Shared;
    using SCP_575.Shared.Audio.Enums;
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
            bool blackout = _plugin.Npc.Methods.IsBlackoutActive;

            // Prevent executing expensive spatial queries if a tactical threat 
            // is introduced while the entity is dormant or unable to react.
            if (impactType == ScpProjectileImpactType.ProjectileImpactType.Dangerous && !blackout)
                return;

            var room = _lib.GetRoomAtPosition(position);
            if (room == null) return;

            switch (impactType)
            {
                case ScpProjectileImpactType.ProjectileImpactType.Helpful:
                    _lib.DisableRoomAndNeighborLights(room);

                    // Local tactical blackout only triggers the close-up psychological node.
                    _plugin.AudioManager.PlayAudioAtPosition(AudioKey.WhispersBang, position, isTransient: true);
                    break;

                case ScpProjectileImpactType.ProjectileImpactType.Dangerous:
                    if (room.LightController.LightsEnabled) return;

                    _lib.EnableAndFlickerRoomAndNeighborLights(
                        room,
                        _plugin.Config.BlackoutConfig.ElevatorLockdownProbability);

                    // Randomly select between defensive rage or acute pain feedback to avoid overlapping artifacts.
                    AudioKey selectedScream = UnityEngine.Random.value > 0.45f ? AudioKey.ScreamAngry : AudioKey.ScreamHurt;
                    _plugin.AudioManager.PlayAudioAtPosition(selectedScream, position, isTransient: true);
                    break;

                default:
                    if (room.LightController.LightsEnabled) return;

                    // Default baseline paranoia feedback for non-tactical explosive events.
                    _plugin.AudioManager.PlayAudioAtPosition(AudioKey.Whispers_1, position);
                    break;
            }
        }
    }
}