using LabApi.Events.Arguments.ServerEvents;
using LabApi.Events.CustomHandlers;
using LabApi.Extensions;
using LabApi.Extensions.Misc;
using SCP_575.Shared;
using System;
using Logger = LabApi.Extensions.Misc.iLogger;

namespace SCP_575.Handlers
{
    /// <summary>
    /// Coordinates the core state machines of the plugin across macro round boundaries, establishing pristine lifecycle resets.
    /// </summary>
    public class LifecycleHandler : CustomEventsHandler
    {
        #region Fields
        private readonly Plugin _plugin;
        #endregion

        #region Constructor
        public LifecycleHandler(Plugin plugin)
        {
            _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
        }
        #endregion

        #region Event Overrides
        /// <summary>
        /// Purges active runtime behaviors when the server reverts to a dormant state.
        /// </summary>
        public override void OnServerWaitingForPlayers()
        {
            _plugin.Disable();
            Logger.Info(nameof(LifecycleHandler), "Round reset. SCP-575 ready for next session.");
        }

        /// <summary>
        /// Computes initialization probabilities introduces a safety temporal buffer before injecting seed variables.
        /// </summary>
        public override void OnServerRoundStarted()
        {
            // Fluent API Alignment: Leverage thread-isolated secure random generation loops cleanly
            float roll = SafeRandom.Range(0f, 100f);

            // ===================================================================
            // DEBUG CONDITIONAL OVERRIDE
            // ===================================================================
#if DEBUG
            roll = -1f;

            Logger.Info(nameof(LifecycleHandler), "========================================================================");
            Logger.Info(nameof(LifecycleHandler), "       [DEVELOPER ENVIRONMENT DETECTED - FORCING 100% SPAWN CHANCE]     ");
            Logger.Info(nameof(LifecycleHandler), "  SCP-575 event roll has been bypassed. Blackout loop will trigger automatically. ");
            Logger.Info(nameof(LifecycleHandler), "========================================================================");
#endif

            Logger.Debug(nameof(LifecycleHandler), $"Spawn roll calculated: {roll}%. Scheduling safe initialization buffer.", _plugin.Debug);

            // Fluent API Alignment: Upgraded generic structural delays to conditional gate execution pipelines safely
            TimingExtensions.CallDelayedIf(1.0f, () => !_plugin.IsEventActive && _plugin.NpcLogic is not null, () =>
            {
                try
                {
                    _plugin.NpcLogic?.Init(roll);
                }
                catch (Exception ex)
                {
                    Logger.Error("Lifecycle.RoundStarted", $"Asynchronous initialization buffer failed: {ex.Message}");
                }
            }, CoroutineTags.Temp);
        }

        /// <summary>
        /// Forces absolute teardown of environmental and tracking systems immediately upon round completion.
        /// </summary>
        public override void OnServerRoundEnded(RoundEndedEventArgs ev)
        {
            _plugin.NpcLogic?.Disable();
            Logger.Info(nameof(LifecycleHandler), "Round ended confirmed. SCP-575 systems safely disabled.");
        }
        #endregion
    }
}