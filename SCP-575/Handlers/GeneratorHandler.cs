namespace SCP_575.Handlers
{
    using LabApi.Events.Arguments.ServerEvents;
    using LabApi.Events.CustomHandlers;
    using MEC;
    using SCP_575.Shared;
    using System;

    /// <summary>
    /// Orchestrates tactical facility infrastructure mutations and power state responses 
    /// while offloading all emotional and defensive audio presentations to the central Audio Director.
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

        public override void OnServerGeneratorActivated(GeneratorActivatedEventArgs ev)
        {
            if (!_plugin.IsEventActive || ev?.Generator == null)
                return;

            var room = _lib.GetRoomAtPosition(ev.Generator.Position);
            if (room == null)
                return;

            LibraryLabAPI.LogInfo("GeneratorHandler", $"Power substation initialized inside zone room: {room.Name}");

            // Overrides local dark zones by establishing a persistent illumination safety baseline
            _lib.EnableAndFlickerRoomAndNeighborLights(room, _plugin.Config.BlackoutConfig.ElevatorLockdownProbability);

            bool allEngaged = _plugin.Npc.Methods.AreAllGeneratorsEngaged();
            bool retaliationConfigured = _plugin.Config.BlackoutConfig.GeneratorActivationRetaliation;

            // Delegate all audio feedback loops and environmental sound cues to the director layer
            _plugin.AudioDirector?.ProcessGeneratorActivation(ev.Generator.Position, allEngaged, retaliationConfigured);

            if (allEngaged)
            {
                var coroutine = Timing.CallDelayed(3.75f, () =>
                {
                    try
                    {
                        if (_plugin.Config.NpcConfig.IsNpcKillable)
                        {
                            _plugin.Npc.Methods.Kill575();
                            LibraryLabAPI.LogInfo("GeneratorHandler", "SCP-575 permanently terminated via core power grid restoration.");
                        }
                        else
                        {
                            _plugin.Npc.Methods.Reset575();
                            LibraryLabAPI.LogInfo("GeneratorHandler", "Facility grid operational. SCP-575 suppressed, background loops preserved.");
                        }
                    }
                    catch (Exception ex)
                    {
                        LibraryLabAPI.LogError("GeneratorHandler.Teardown", $"Failed to execute post-mortem state change: {ex.Message}");
                    }
                });

                coroutine.Tag = GeneratorAudioTag;
                return;
            }

            if (retaliationConfigured)
            {
                if (!_plugin.Npc.Methods.IsBlackoutActive)
                {
                    _plugin.Npc.Methods.StartTimedBlackoutBoost(
                        _plugin.Config.BlackoutConfig.DurationMin,
                        "GeneratorHandler",
                        "Dormant SCP-575 awakened. Triggering emergency facility-wide blackout.",
                        null,
                        () => LabApi.Features.Wrappers.Map.TurnOffLights(_plugin.Config.BlackoutConfig.DurationMin)
                    );
                }
                else
                {
                    _lib.DisableRoomAndNeighborLights(room, _plugin.Config.BlackoutConfig.DurationMin);
                    LibraryLabAPI.LogInfo("GeneratorHandler", "SCP-575 escalated localized darkness during active blackout.");
                }
            }
        }
    }
}