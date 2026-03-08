namespace SCP_575.Handlers
{
    using LabApi.Events.Arguments.ServerEvents;
    using LabApi.Events.CustomHandlers;
    using MEC;
    using SCP_575.Shared;
    using SCP_575.Shared.Audio.Enums;

    /// <summary>
    /// Handles generator activations. Manages SCP-575's vulnerability and defensive audio cues 
    /// as the facility's power systems are restored.
    /// </summary>
    public class GeneratorHandler : CustomEventsHandler
    {
        private readonly Plugin _plugin;
        private readonly LibraryLabAPI _lib;

        public GeneratorHandler(Plugin plugin)
        {
            _plugin = plugin;
            _lib = plugin.LibraryLabAPI;
        }

        public override void OnServerGeneratorActivated(GeneratorActivatedEventArgs ev)
        {
            if (!_plugin.IsEventActive)
                return;

            if (ev?.Generator == null)
                return;

            var room = _lib.GetRoomAtPosition(ev.Generator.Position);

            if (room == null)
                return;

            if (!_plugin.Npc.Methods.IsBlackoutActive)
                return;

            LibraryLabAPI.LogInfo("Generator", $"Generator activated in {room.Name}");

            _lib.EnableAndFlickerRoomAndNeighborLights(
                room,
                _plugin.Config.BlackoutConfig.ElevatorLockdownProbability);

            if (_plugin.Npc.Methods.AreAllGeneratorsEngaged())
            {
                _plugin.Npc.Methods.TrackCoroutine(
                Timing.CallDelayed(3.75f, () =>
                {
                    _plugin.AudioManager.PlayGlobalAudioAutoManaged(
                        AudioKey.ScreamDying,
                        lifespan: 25f);
                }));

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