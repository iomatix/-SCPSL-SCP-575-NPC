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
            if (!_plugin.IsEventActive)
                return;

            HandleExplosionEvent(ev, null);
        }

        public override void OnServerProjectileExploded(ProjectileExplodedEventArgs ev)
        {
            if (!_plugin.IsEventActive)
                return;

            HandleExplosionEvent(null, ev);
        }

        private void HandleExplosionEvent(
            ExplosionSpawnedEventArgs explosionEv,
            ProjectileExplodedEventArgs projectileEv)
        {
            Vector3 position;
            ScpProjectileImpactType.ProjectileImpactType impactType;

            if (explosionEv != null)
            {
                position = explosionEv.Position;

                impactType =
                    ScpProjectileImpactType.ClassifyExplosionImpact(
                        explosionEv.ExplosionType);
            }
            else
            {
                position = projectileEv.Position;

                impactType =
                    ScpProjectileImpactType.ClassifyProjectileImpact(
                        projectileEv.TimedGrenade);
            }

            var room = _lib.GetRoomAtPosition(position);

            if (room == null)
                return;

            bool blackout = _plugin.Npc.Methods.IsBlackoutActive;

            switch (impactType)
            {
                case ScpProjectileImpactType.ProjectileImpactType.Helpful:

                    _lib.DisableRoomAndNeighborLights(room);

                    _plugin.AudioManager.PlayAudioAutoManaged(
                        null,
                        AudioKey.WhispersBang,
                        position: position,
                        hearableForAllPlayers: true,
                        lifespan: 25f);

                    _plugin.AudioManager.PlayAmbience();

                    break;

                case ScpProjectileImpactType.ProjectileImpactType.Dangerous:

                    if (!blackout || room.LightController.LightsEnabled)
                        return;

                    _lib.EnableAndFlickerRoomAndNeighborLights(
                        room,
                        _plugin.Config.BlackoutConfig.ElevatorLockdownProbability);

                    _plugin.AudioManager.PlayAudioAutoManaged(
                        null,
                        AudioKey.ScreamAngry,
                        position: position,
                        hearableForAllPlayers: true,
                        lifespan: 25f);

                    break;

                default:

                    _plugin.AudioManager.PlayAudioAutoManaged(
                        null,
                        AudioKey.Whispers,
                        position: position,
                        hearableForAllPlayers: true,
                        lifespan: 25f);

                    break;
            }
        }
    }
}