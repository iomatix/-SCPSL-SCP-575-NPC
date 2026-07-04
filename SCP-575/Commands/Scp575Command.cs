using CommandSystem;
using LabApi.Extensions;
using LabApi.Extensions.Misc;
using LabApi.Features.Wrappers;
using RemoteAdmin;
using SCP_575.Npc;
using System;
using FacilityZone = MapGeneration.FacilityZone;

namespace SCP_575.Commands
{
    /// <summary>
    /// Administrative command router for SCP-575 plugin management.
    /// Employs advanced heuristic parsing engines to deliver fuzzy string resolution, typo-tolerance, and automatic context mapping.
    /// </summary>
    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    [CommandHandler(typeof(GameConsoleCommandHandler))]
    public class Scp575Command : ICommand, IUsageProvider
    {
        #region Internal Heuristic Mapping Enums
        private enum SubCommand
        {
            Start,
            Init,
            Stop,
            Disable,
            Blackout,
            Trigger,
            SetStacks,
            ChangeStacks,
            Sanity
        }

        private enum SanityAction
        {
            Set,
            Add,
            Subtract
        }
        #endregion

        /// <inheritdoc />
        public string Command => "scp575";

        /// <inheritdoc />
        public string[] Aliases => new[] { "575", "ev575" };

        /// <inheritdoc />
        public string Description => "Administrative control interface for the SCP-575 anomalous system lifecycle built with fuzzy parsing.";

        /// <inheritdoc />
        public string[] Usage => new[] { "init/start", "blackout/trigger [zone]", "stop/disable", "setstacks [amount]", "changestacks [delta]", "sanity [player] [set/add/sub] [value]" };

        /// <inheritdoc />
        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            // Enforcement of permission controls over high-impact administrative triggers
            if (sender is PlayerCommandSender playerSender && !playerSender.CheckPermission(PlayerPermissions.FacilityManagement))
            {
                response = "Unauthorized Command Execution: You do not possess the required administrative clearance (FacilityManagement) to call this command.";
                return false;
            }

            if (arguments.Count == 0)
            {
                response = "Invalid Command Configuration. Available Subcommands (Typo-Tolerant):\n" +
                           " - init / start                    : Force-initializes the SCP-575 baseline framework.\n" +
                           " - blackout / trigger [zone/room]  : Requests instantaneous global or zone-specific blackout execution.\n" +
                           " - stop / disable                  : Safely aborts and purges persistent tracking hooks and timers.\n" +
                           " - setstacks [amount]              : Explicitly overrides the running atmospheric blackout stack registry.\n" +
                           " - changestacks [delta]            : Shifts running stacks up or down using relative integer modifiers.\n" +
                           " - sanity [player] [action] [val]  : Modifies a targeted player's sanity index metrics seamlessly.";
                return false;
            }

            var plugin = Plugin.Singleton;
            if (plugin == null || plugin.NpcNestingObj?.Logic == null)
            {
                response = "Execution Critical Failure: The central SCP-575 runtime singleton or core Methods logic module is offline.";
                return false;
            }

            // Heuristic Step 1: Interpret the raw subcommand argument via the fuzzy engine cascade
            var commandInterpretation = arguments.At(0).InterpretEnum<SubCommand>();

            if (commandInterpretation.Status == InterpretationStatus.NoMatchFound)
            {
                response = $"Syntax Interpretation Failure: '{arguments.At(0)}' is completely unrecognized. Use 'start', 'blackout', 'stop', 'setstacks', 'changestacks', or 'sanity'.";
                return false;
            }

            if (commandInterpretation.Status == InterpretationStatus.Ambiguous)
            {
                response = $"Ambiguous Subcommand Identified. Did you mean one of the following elements?\n" +
                           $" Candidates: {string.Join(", ", commandInterpretation.Candidates)}";
                return false;
            }

            // Process definitive matches through the optimized structural action map
            switch (commandInterpretation.Value)
            {
                case SubCommand.Init:
                case SubCommand.Start:
                    if (plugin.IsEventActive)
                    {
                        response = "Initialization Cancelled: SCP-575 horror pacing mechanisms are already active and ticking inside this round state.";
                        return false;
                    }

                    plugin.NpcNestingObj.Logic.Init();
                    response = "SUCCESS: SCP-575 anomaly ecosystem forced online. Decoupled environmental trackers, acoustic generators, and data registries initialized.";
                    return true;

                case SubCommand.Stop:
                case SubCommand.Disable:
                    if (!plugin.IsEventActive)
                    {
                        response = "Teardown Aborted: The SCP-575 execution framework is already resting in an uninitialized baseline state.";
                        return false;
                    }

                    plugin.NpcNestingObj.Logic.Disable();
                    response = "SUCCESS: Explicit runtime teardown completed. Active visual flickers, audio processing coroutines, and memory maps purged cleanly.";
                    return true;

                case SubCommand.Blackout:
                case SubCommand.Trigger:
                    if (!plugin.IsEventActive)
                    {
                        response = "Trigger Denied: The global event framework is offline. Execute 'scp575 start' to wake up core systems first.";
                        return false;
                    }

                    // Heuristic Step 2: Contextual evaluation of optional zone-specific blackout parameters
                    if (arguments.Count >= 2)
                    {
                        var zoneInterpretation = arguments.At(1).InterpretEnum<FacilityZone>();

                        if (zoneInterpretation.Status == InterpretationStatus.NoMatchFound)
                        {
                            response = $"Zone Selection Failed: '{arguments.At(1)}' does not map to any recognized facility sector coordinates.";
                            return false;
                        }

                        if (zoneInterpretation.Status == InterpretationStatus.Ambiguous)
                        {
                            response = $"Ambiguous Sector Identification. Multi-grid overlap detected across candidates:\n" +
                                       $" Candidates: {string.Join(", ", zoneInterpretation.Candidates)}";
                            return false;
                        }

                        // Execute target-focused localized blackout routine inside the verified definitive FacilityZone match
                        plugin.NpcNestingObj.Logic.ForceZoneBlackoutEvent(zoneInterpretation.Value);
                        response = $"SUCCESS: Targeted blackout sequence dispatched onto definitive matched zone sector: [{zoneInterpretation.Value}].";
                        return true;
                    }

                    // Default to global grid collapse layout if no zoning modifier arguments are appended
                    plugin.NpcNestingObj.Logic.ForceGlobalBlackoutEvent();
                    response = "SUCCESS: Global power grid collapse triggered. Dispatched soundscape modules and network illumination updates facility-wide.";
                    return true;

                case SubCommand.SetStacks:
                    if (!plugin.IsEventActive)
                    {
                        response = "Stack Adjustment Denied: The anomaly system must be active to balance multiplier weights.";
                        return false;
                    }

                    if (arguments.Count < 2 || !int.TryParse(arguments.At(1), out int targetStacks) || targetStacks < 0)
                    {
                        response = "Syntax Compilation Error. Correct usage pattern: scp575 setstacks [positive integer value]";
                        return false;
                    }

                    if (targetStacks == 0)
                    {
                        plugin.NpcNestingObj.Logic.Reset575();
                        response = "SUCCESS: Blackout stack accumulation counter cleared to 0. Illumination networks and containment fields stabilized.";
                        return true;
                    }

                    // Dynamically iterate stack allocations until perfect synchronization with the administrative target is verified
                    UpdateBlackoutStackMatrix(plugin.NpcNestingObj.Logic, targetStacks);

                    response = $"SUCCESS: Environmental atmospheric threat matrix updated. Running blackout stack count locked at: {plugin.NpcNestingObj.Logic.GetCurrentBlackoutStacks}";
                    return true;

                case SubCommand.ChangeStacks:
                    if (!plugin.IsEventActive)
                    {
                        response = "Relative Shift Denied: The anomaly system must be active to balance multiplier weights.";
                        return false;
                    }

                    if (arguments.Count < 2 || !int.TryParse(arguments.At(1), out int stackDelta))
                    {
                        response = "Syntax Compilation Error. Correct usage pattern: scp575 changestacks [signed integer delta (e.g. -2 or 3)]";
                        return false;
                    }

                    int calculatedTarget = (plugin.NpcNestingObj.Logic.GetCurrentBlackoutStacks + stackDelta).LimitMin(0);
                    UpdateBlackoutStackMatrix(plugin.NpcNestingObj.Logic, calculatedTarget);

                    response = $"SUCCESS: Shift modifier applied. Absolute blackout stack accumulation updated to: {plugin.NpcNestingObj.Logic.GetCurrentBlackoutStacks} (Shift Delta: {stackDelta:+#;-#;0})";
                    return true;

                case SubCommand.Sanity:
                    if (!plugin.IsEventActive)
                    {
                        response = "Sanity Mutation Denied: Neural metric tracking arrays are offline because the main plugin context is disabled.";
                        return false;
                    }

                    if (arguments.Count < 4)
                    {
                        response = "Syntax Compilation Error. Correct usage pattern: scp575 sanity [player name/id] [set/add/sub] [value (0-100)]";
                        return false;
                    }

                    // Fetch targeted player wrapper using native framework search paths
                    Player targetPlayer = Player.Get(arguments.At(1));
                    if (targetPlayer == null)
                    {
                        response = $"Target Assignment Failed: No active player matching identifier literal '{arguments.At(1)}' could be located.";
                        return false;
                    }

                    // Heuristic Step 3: Parse modification method via fuzzy enum matching
                    var actionInterpretation = arguments.At(2).InterpretEnum<SanityAction>();
                    if (actionInterpretation.Status == InterpretationStatus.NoMatchFound)
                    {
                        response = $"Action Translation Failed: Operation wrapper '{arguments.At(2)}' is unknown. Use 'set', 'add', or 'sub'.";
                        return false;
                    }
                    if (actionInterpretation.Status == InterpretationStatus.Ambiguous)
                    {
                        response = $"Ambiguous Operation Entry. Matched conflicting processing candidates: {string.Join(", ", actionInterpretation.Candidates)}";
                        return false;
                    }

                    if (!float.TryParse(arguments.At(3), out float sanityValue) || sanityValue < 0f)
                    {
                        response = $"Value Validation Error: '{arguments.At(3)}' is an invalid sanity value primitive. Expected non-negative decimal scaling factor.";
                        return false;
                    }

                    // Query current status tracking maps from the background handler context
                    float currentSanity = plugin.SanityEventHandler.GetCurrentSanity(targetPlayer);
                    float finalSanity = currentSanity;

                    switch (actionInterpretation.Value)
                    {
                        case SanityAction.Set:
                            finalSanity = sanityValue;
                            break;
                        case SanityAction.Add:
                            finalSanity = currentSanity + sanityValue;
                            break;
                        case SanityAction.Subtract:
                            finalSanity = currentSanity - sanityValue;
                            break;
                    }

                    // Enforce absolute physiological bounds tracking decimals inline natively
                    finalSanity = finalSanity.Clamp(0f, 100f);
                    plugin.SanityEventHandler.SetSanity(targetPlayer, finalSanity);

                    response = $"SUCCESS: Neuro-integrity mapping updated for player [{targetPlayer.Nickname}]. Sanity adjusted from {currentSanity}% to {finalSanity}% via {actionInterpretation.Value} vector tracking.";
                    return true;

                default:
                    response = "System Internal Mapping Exception: Unhandled command route structural fallback tree reached.";
                    return false;
            }
        }

        /// <summary>
        /// Synchronizes the running logic instance's stack tracking parameters with a targeted absolute index baseline.
        /// </summary>
        private static void UpdateBlackoutStackMatrix(Methods logic, int targetLevel)
        {
            while (logic.GetCurrentBlackoutStacks < targetLevel)
            {
                logic.IncrementBlackoutStack();
            }
            while (logic.GetCurrentBlackoutStacks > targetLevel)
            {
                logic.DecrementBlackoutStack();
            }
        }
    }
}