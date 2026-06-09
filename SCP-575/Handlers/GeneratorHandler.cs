namespace SCP_575.Handlers
{
    using LabApi.Events.Arguments.ServerEvents;
    using LabApi.Events.CustomHandlers;
    using MEC;
    using SCP_575.Shared;
    using SCP_575.Shared.Audio.Enums;
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
            // Prevent interaction overhead if the SCP-575 lifecycle is not initialized for this round.
            if (!_plugin.IsEventActive)
                return;

            if (ev?.Generator == null)
                return;

            var room = _lib.GetRoomAtPosition(ev.Generator.Position);
            if (room == null)
                return;

            LibraryLabAPI.LogInfo("GeneratorHandler", $"Generator activated in {room.Name}");

            // Establish the substation as a persistent grid safety point that resists future blackouts.
            _lib.EnableAndFlickerRoomAndNeighborLights(room, _plugin.Config.BlackoutConfig.ElevatorLockdownProbability);
            _plugin.AudioManager.PlayAudioAutoManaged(
                            player: null,
                            audioKey: AudioKey.GeneratorHumDefense,
                            position: ev.Generator.Position,
                            lifespan: null,
                            hearableForAllPlayers: true
                        );

            // Evaluate final containment criteria before processing standard retaliation loops.
            if (_plugin.Npc.Methods.AreAllGeneratorsEngaged())
            {
                _plugin.AudioManager.PlayAudioAtPosition(AudioKey.ScreamDying, ev.Generator.Position, isTransient: true);

                var coroutine = Timing.CallDelayed(3.75f, () =>
                {
                    try
                    {
                        // Handle permanent entity termination or suppress current wave while preserving random event loops.
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

            // Process systemic entity aggression if configured for mechanical counter-play.
            if (_plugin.Config.BlackoutConfig.GeneratorActivationRetaliation)
            {
                _plugin.AudioManager.PlayOrbitingAudio(
                    staticPosition: ev.Generator.Position,
                    audioKey: AudioKey.ScreamAngry,
                    lifespan: null,
                    maxRadius: 6.5f,
                    minRadius: 1.5f,
                    angularSpeed: 3.5f,
                    approachSpeed: 2.8f
                );

                // Force a global state shift if the entity is currently dormant to penalize early activation.
                if (!_plugin.Npc.Methods.IsBlackoutActive)
                {
                    // Delegating stack mutation and safety tagging to the central method to prevent cross-round dangling timers
                    _plugin.Npc.Methods.StartTimedBlackoutBoost(
                        _plugin.Config.BlackoutConfig.DurationMin,
                        "GeneratorHandler",
                        "Dormant SCP-575 awakened. Triggering emergency facility-wide blackout.",
                        null, // Expiration log not requested for generator context
                        () => LabApi.Features.Wrappers.Map.TurnOffLights(_plugin.Config.BlackoutConfig.DurationMin)
                    );
                }
                else
                {
                    // Escalate localized structural failure if an environmental blackout is already active.
                    _lib.DisableRoomAndNeighborLights(room, _plugin.Config.BlackoutConfig.DurationMin);
                    LibraryLabAPI.LogInfo("GeneratorHandler", "SCP-575 escalated localized darkness during active blackout.");
                }
            }
            else
            {
                var randomScream = (AudioKey)UnityEngine.Random.Range((int)AudioKey.Scream_1, (int)AudioKey.ScreamHurt + 1);
                _plugin.AudioManager.PlayAudioAtPosition(randomScream, ev.Generator.Position, isTransient: true);
            }
        }
    }
}