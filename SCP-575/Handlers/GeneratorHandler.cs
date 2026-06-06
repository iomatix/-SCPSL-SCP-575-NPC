namespace SCP_575.Handlers
{
    using LabApi.Events.Arguments.ServerEvents;
    using LabApi.Events.CustomHandlers;
    using MEC;
    using SCP_575.Shared;
    using SCP_575.Shared.Audio.Enums;
    using SCP575.Shared;
    using System;

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

            if (_plugin.Npc.Methods.AreAllGeneratorsEngaged())
            {
                // Delay the termination audio sequence slightly to allow the structural 
                // power restoration soundscapes to establish narrative precedence.
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
                // Spatializing the roar at the activation site alerts human teams to the exact 
                // point of friction, emphasizing local threat presence rather than a global event.
                _plugin.AudioManager.PlayAudioAtPosition(AudioKey.ScreamAngry, ev.Generator.Position);
            }
        }
    }
}