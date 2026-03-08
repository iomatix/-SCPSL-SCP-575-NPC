namespace SCP_575.Handlers
{
    using LabApi.Events.Arguments.ServerEvents;
    using LabApi.Events.CustomHandlers;
    using MEC;
    using SCP_575.Shared;
    using SCP_575.Shared.Audio.Enums;
    using System;

    /// <summary>
    /// Handles generator activations. Manages SCP-575's vulnerability and defensive audio cues 
    /// as the facility's power systems are restored.
    /// </summary>
    public class GeneratorHandler : CustomEventsHandler
    {
        private readonly Plugin _plugin;
        private readonly LibraryLabAPI _lib;

        private const string GeneratorAudioTag = "SCP575-GeneratorAudio";

        public GeneratorHandler(Plugin plugin)
        {
            _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
            _lib = _plugin.LibraryLabAPI;
        }

        #region Lifecycle Cleanup

        public override void OnServerRoundEnded(RoundEndedEventArgs ev)
        {
            Timing.KillCoroutines(GeneratorAudioTag);
        }

        public override void OnServerWaitingForPlayers()
        {
            Timing.KillCoroutines(GeneratorAudioTag);
        }

        #endregion

        public override void OnServerGeneratorActivated(GeneratorActivatedEventArgs ev)
        {
            // 1. Cheap checks first: Ensure plugin and blackout events are active
            if (!_plugin.IsEventActive || !_plugin.Npc.Methods.IsBlackoutActive)
                return;

            if (ev?.Generator == null)
                return;

            // 2. Expensive spatial query last
            var room = _lib.GetRoomAtPosition(ev.Generator.Position);
            if (room == null)
                return;

            LibraryLabAPI.LogInfo("GeneratorHandler", $"Generator activated in {room.Name}");

            _lib.EnableAndFlickerRoomAndNeighborLights(
                room,
                _plugin.Config.BlackoutConfig.ElevatorLockdownProbability);

            if (_plugin.Npc.Methods.AreAllGeneratorsEngaged())
            {
                // Replaced deprecated TrackCoroutine with MEC tag system

                var coroutine = Timing.CallDelayed(3.75f, () =>
                {
                    _plugin.AudioManager.PlayGlobalAudioAutoManaged(
                        AudioKey.ScreamDying,
                        lifespan: 25f);
                });

                coroutine.Tag = GeneratorAudioTag;

                if (_plugin.Config.NpcConfig.IsNpcKillable)
                    _plugin.Npc.Methods.Kill575();
                else
                    _plugin.Npc.Methods.Reset575();
            }
            else
            {
                _plugin.AudioManager.PlayGlobalAudioAutoManaged(
                    AudioKey.ScreamAngry,
                    lifespan: 25f);
            }
        }
    }
}