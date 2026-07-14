using LabApi.Events.Arguments.ServerEvents;
using LabApi.Events.CustomHandlers;
using LabApi.Extensions;
using LabApi.Features.Wrappers;
using SCP_575.Shared;
using System;
using UnityEngine;
using Logger = LabApi.Extensions.Misc.iLogger;

namespace SCP_575.Handlers
{
    /// <summary>
    /// Intercepts facility power generator activations, executing infrastructure overrides via Fluent API extensions.
    /// </summary>
    public class GeneratorHandler : CustomEventsHandler
    {
        #region Fields
        private readonly Plugin _plugin;
        private const string GeneratorAudioTag = CoroutineTags.GeneratorAudio;
        #endregion

        #region Constructor
        public GeneratorHandler(Plugin plugin)
        {
            _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
        }
        #endregion

        #region Lifecycle Cleanup
        public override void OnServerRoundEnded(RoundEndedEventArgs ev) => GeneratorAudioTag.Kill();
        public override void OnServerWaitingForPlayers() => GeneratorAudioTag.Kill();
        #endregion

        #region Event Overrides
        public override void OnServerGeneratorActivated(GeneratorActivatedEventArgs ev)
        {
            if (!_plugin.IsEventActive || ev?.Generator is null) return;

            Vector3 position = ev.Generator.Position;

            // Fluent API Alignment: Resolving active room object seamlessly from raw vector coordinates
            Room room = position.GetRoom();
            if (room is null) return;

            Logger.Info(nameof(GeneratorHandler), $"Power substation initialized inside room: {room.Name}");

            // Fluent API Alignment: Restore standard operational power spectrum maps across room and neighbors
            room.TurnOnRoomAndNeighborLights(0.65f);

            bool allEngaged = _plugin.NpcLogic.AreAllGeneratorsEngaged();
            bool retaliationConfigured = _plugin.Blackout.GeneratorActivationRetaliation;

            _plugin.AudioDirector?.ProcessGeneratorActivation(position, allEngaged, retaliationConfigured);

            if (allEngaged)
            {
                _plugin.NpcLogic.ProcessFullGridRestorationTeardown();
                return;
            }

            if (retaliationConfigured)
            {
                _plugin.NpcLogic.StartTimedBlackoutBoost(
                    _plugin.Blackout.DurationMin,
                    nameof(GeneratorHandler),
                    $"Emergency blackout surge triggered in {room.Name} due to generator activation retaliation.",
                    null,
                    () => _plugin.NpcLogic.ExecuteLocalizedRetaliationSurge(room)
                );
            }
        }
        #endregion
    }
}