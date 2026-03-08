namespace SCP_575.Handlers
{
    using LabApi.Events.Arguments.ServerEvents;
    using LabApi.Events.CustomHandlers;
    using SCP_575.Shared;
    using System;
    using UnityEngine;

    /// <summary>
    /// Orchestrates the plugin's state across round phases (Waiting, Started, Ended).
    /// Ensures clean state transitions and initial spawn rolls.
    /// </summary>
    public class LifecycleHandler : CustomEventsHandler
    {
        private readonly Plugin _plugin;

        public LifecycleHandler(Plugin plugin)
        {
            _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
        }

        public override void OnServerWaitingForPlayers()
        {
            _plugin.IsEventActive = false;
            _plugin.Npc?.Methods?.Disable();

            LibraryLabAPI.LogInfo("Lifecycle", "Round reset. SCP-575 ready.");
        }

        public override void OnServerRoundStarted()
        {
            float roll = UnityEngine.Random.Range(0f, 100f);

            LibraryLabAPI.LogDebug("Lifecycle", $"Spawn roll: {roll}");

            _plugin.Npc?.Methods?.Init(roll);
        }

        public override void OnServerRoundEnded(RoundEndedEventArgs ev)
        {
            _plugin.IsEventActive = false;

            _plugin.Npc?.Methods?.Disable();

            LibraryLabAPI.LogInfo("Lifecycle", "Round ended. SCP-575 disabled.");
        }
    }
}