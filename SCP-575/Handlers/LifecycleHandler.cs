namespace SCP_575.Handlers
{
    using LabApi.Events.Arguments.ServerEvents;
    using LabApi.Events.CustomHandlers;
    using SCP_575.Shared;
    using System;

    /// <summary>
    /// Coordinates the core state machines of the plugin across macro round boundaries,
    /// establishing pristine lifecycle resets and managing initial systemic generation rolls.
    /// </summary>
    public class LifecycleHandler : CustomEventsHandler
    {
        private readonly Plugin _plugin;

        public LifecycleHandler(Plugin plugin)
        {
            _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
        }

        /// <summary>
        /// Purges any active runtime behaviors and structures when the server reverts 
        /// to a dormant state, ensuring no memory stagnation carries into the next session.
        /// </summary>
        public override void OnServerWaitingForPlayers()
        {
            _plugin.IsEventActive = false;
            _plugin.Npc?.Methods?.Disable();

            LibraryLabAPI.LogInfo("Lifecycle", "Round reset. SCP-575 ready.");
        }

        /// <summary>
        /// Computes initialization probabilities at the precise instant the gameplay loop activates,
        /// injecting seed variables into the primary orchestration systems.
        /// </summary>
        public override void OnServerRoundStarted()
        {
            float roll = UnityEngine.Random.Range(0f, 100f);

            LibraryLabAPI.LogDebug("Lifecycle", $"Spawn roll: {roll}");

            _plugin.Npc?.Methods?.Init(roll);
        }

        /// <summary>
        /// Forces absolute teardown of all environmental and tracking systems immediately upon 
        /// round completion to prevent trailing asynchronous operations from affecting post-round states.
        /// </summary>
        /// <param name="ev">Telemetry data regarding the round finalization state.</param>
        public override void OnServerRoundEnded(RoundEndedEventArgs ev)
        {
            _plugin.IsEventActive = false;
            _plugin.Npc?.Methods?.Disable();

            LibraryLabAPI.LogInfo("Lifecycle", "Round ended. SCP-575 disabled.");
        }
    }
}