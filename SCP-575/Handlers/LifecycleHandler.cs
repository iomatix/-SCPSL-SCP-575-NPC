namespace SCP_575.Handlers
{
    using LabApi.Events.Arguments.ServerEvents;
    using LabApi.Events.CustomHandlers;
    using MEC;
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
            _plugin.NpcNestingObj?.Methods?.Disable();
            LibraryLabAPI.LogInfo("Lifecycle", "Round reset. SCP-575 ready for next session.");
        }

        /// <summary>
        /// Computes initialization probabilities at the precise instant the gameplay loop activates,
        /// introducing a safety temporal buffer before injecting seed variables into active subsystems.
        /// </summary>
        public override void OnServerRoundStarted()
        {
            float roll = UnityEngine.Random.Range(0f, 100f);

            // ===================================================================
            // DEBUG CONDITIONAL OVERRIDE
            // ===================================================================
#if DEBUG
            // Forcing the roll to -1f guarantees success in Methods.Init (since -1f is always <= EventChance, even if chance is 0%).
            roll = -1f;

            LibraryLabAPI.LogInfo("Lifecycle", "========================================================================");
            LibraryLabAPI.LogInfo("Lifecycle", "       [DEVELOPER ENVIRONMENT DETECTED - FORCING 100% SPAWN CHANCE]     ");
            LibraryLabAPI.LogInfo("Lifecycle", "  SCP-575 event roll has been bypassed. Blackout loop will trigger automatically. ");
            LibraryLabAPI.LogInfo("Lifecycle", "========================================================================");
#endif

            LibraryLabAPI.LogDebug("Lifecycle", $"Spawn roll calculated: {roll}%. Scheduling safe initialization buffer.");

            // Introduced a 1-second delay execution layout to completely mitigate Frame-0 race conditions.
            Timing.CallDelayed(1.0f, () =>
            {
                try
                {
                    if (_plugin.IsEventActive || _plugin.NpcNestingObj?.Methods == null) return;
                    _plugin.NpcNestingObj.Methods.Init(roll);
                }
                catch (Exception ex)
                {
                    LibraryLabAPI.LogError("Lifecycle.RoundStarted", $"Asynchronous initialization buffer failed: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// Forces absolute teardown of all environmental and tracking systems immediately upon 
        /// round completion to prevent trailing asynchronous operations from affecting post-round states.
        /// </summary>
        /// <param name="ev">Telemetry data regarding the round finalization state.</param>
        public override void OnServerRoundEnded(RoundEndedEventArgs ev)
        {
            _plugin.NpcNestingObj?.Methods?.Disable();
            LibraryLabAPI.LogInfo("Lifecycle", "Round ended confirmed. SCP-575 systems safely disabled.");
        }
    }
}