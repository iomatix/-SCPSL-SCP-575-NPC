namespace SCP_575.Handlers
{
    using LabApi.Events.Arguments.ServerEvents;
    using LabApi.Events.CustomHandlers;
    using MEC;
    using SCP_575.Shared;
    using SCP_575.Shared.Audio.Enums;
    using System;
    using UnityEngine;

    /// <summary>
    /// Orchestrates the entity's defensive reactions and containment mechanics as human forces 
    /// restore power sub-stations across the facility infrastructure.
    /// </summary>
    public class GeneratorHandler : CustomEventsHandler
    {
        private readonly Plugin _plugin;
        private readonly LibraryLabAPI _lib;

        private const string GeneratorAudioTag = CoroutineTags.GeneratorAudio;

        public GeneratorHandler(Plugin plugin)
        {
            _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
            _lib = _plugin.LibraryLabAPI;
        }

        #region Lifecycle Cleanup

        public override void OnServerRoundEnded(RoundEndedEventArgs ev) => Timing.KillCoroutines(GeneratorAudioTag);
        public override void OnServerWaitingForPlayers() => Timing.KillCoroutines(GeneratorAudioTag);

        #endregion

        /// <summary>
        /// Evaluates overall facility power status upon sub-station activation, triggering 
        /// structural lighting overrides and localized or map-wide acoustic defensive behaviors.
        /// </summary>
        /// <param name="ev">The event arguments containing generator telemetry and location.</param>
        public override void OnServerGeneratorActivated(GeneratorActivatedEventArgs ev)
        {
            if (!_plugin.IsEventActive || !_plugin.Npc.Methods.IsBlackoutActive)
                return;

            if (ev?.Generator == null)
                return;

            var room = _lib.GetRoomAtPosition(ev.Generator.Position);
            if (room == null)
                return;

            LibraryLabAPI.LogInfo("GeneratorHandler", $"Generator activated in {room.Name}");

            _lib.EnableAndFlickerRoomAndNeighborLights(
                room,
                _plugin.Config.BlackoutConfig.ElevatorLockdownProbability);

            // Trigger environmental feedback indicating that this specific sub-station has synchronized.
            _plugin.AudioManager.PlayAudioAtPosition(AudioKey.GeneratorHumDefense, ev.Generator.Position);

            if (_plugin.Npc.Methods.AreAllGeneratorsEngaged())
            {
                // FIXED: Played the signature lethal dying scream IMMEDIATELY to anchor spatial presence.
                // This ensures the audio wave enters the mixer engine before the lifecycle teardown executes.
                _plugin.AudioManager.PlayAudioAtPosition(AudioKey.ScreamDying, ev.Generator.Position);

                // FIXED: We delay the dynamic data and system structural teardown instead of the sound trigger.
                // This creates a flawless cinematic window where the monster roars in agony for 3.75s, 
                // and then the facility power systems drop the event state flags.
                var coroutine = Timing.CallDelayed(3.75f, () =>
                {
                    try
                    {
                        if (_plugin.Config.NpcConfig.IsNpcKillable)
                            _plugin.Npc.Methods.Kill575();
                        else
                            _plugin.Npc.Methods.Reset575();
                    }
                    catch (Exception ex)
                    {
                        LibraryLabAPI.LogError("GeneratorHandler.Teardown", $"Failed to execute post-mortem state change: {ex.Message}");
                    }
                });

                coroutine.Tag = GeneratorAudioTag;
            }
            else
            {
                // Dynamic acoustic rotation: pick randomly between behavioral screams and acute hurt feedback
                // to signal that the sudden influx of structural power is physically disrupting the entity.
                var randomScream = (AudioKey)UnityEngine.Random.Range((int)AudioKey.Scream_1, (int)AudioKey.ScreamHurt + 1);
                _plugin.AudioManager.PlayAudioAtPosition(randomScream, ev.Generator.Position);
            }
        }
    }
}