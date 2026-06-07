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
            if (!_plugin.IsEventActive || !_plugin.Npc.Methods.IsBlackoutActive)
                return;

            if (ev?.Generator == null)
                return;

            var room = _lib.GetRoomAtPosition(ev.Generator.Position);
            if (room == null)
                return;

            LibraryLabAPI.LogInfo("GeneratorHandler", $"Generator activated in {room.Name}");

            // 1. If it's the final generator, execute standard death sequence
            if (_plugin.Npc.Methods.AreAllGeneratorsEngaged())
            {
                _plugin.AudioManager.PlayAudioAtPosition(AudioKey.ScreamDying, ev.Generator.Position, isTransient: true);

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
                return;
            }

            // ===================================================================
            // MONSTER RETALIATION / RAGE MECHANIC
            // ===================================================================
            if (_plugin.Config.BlackoutConfig.GeneratorActivationRetaliation)
            {
                // The monster gets furious. It instantly snuffs out the lights in the sector,
                // overrides the sync, and commands a localized blackout that adds a global stack.
                _lib.DisableRoomAndNeighborLights(room, _plugin.Config.BlackoutConfig.DurationMin);

                // Play a high-priority angry scream directly at the source of the provocation
                _plugin.AudioManager.PlayAudioAtPosition(AudioKey.ScreamAngry, ev.Generator.Position, isTransient: true);

                LibraryLabAPI.LogInfo("GeneratorHandler", $"SCP-575 retaliated! Generator activation at {room.Name} triggered rage stack expansion.");
            }
            else
            {
                // Standard behavior: lights flicker on, protecting the room temporarily
                _lib.EnableAndFlickerRoomAndNeighborLights(room, _plugin.Config.BlackoutConfig.ElevatorLockdownProbability);
                _plugin.AudioManager.PlayAudioAtPosition(AudioKey.GeneratorHumDefense, ev.Generator.Position);

                // Normal warning scream
                var randomScream = (AudioKey)UnityEngine.Random.Range((int)AudioKey.Scream_1, (int)AudioKey.ScreamHurt + 1);
                _plugin.AudioManager.PlayAudioAtPosition(randomScream, ev.Generator.Position, isTransient: true);
            }
        }
    }
}