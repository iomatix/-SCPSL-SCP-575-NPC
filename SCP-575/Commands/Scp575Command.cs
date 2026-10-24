using CommandSystem;
using LabApi.Extensions;
using LabApi.Extensions.Misc;
using LabApi.Features.Wrappers;
using RemoteAdmin;
using SCP_575.Npc;
using SCP_575.Shared;
using System;
using System.Collections.Generic;
using FacilityZone = MapGeneration.FacilityZone;

namespace SCP_575.Commands
{
    /// <summary>
    /// Administrative command router for SCP-575 plugin management utilizing heuristic fuzzy string parsing layouts
    /// and deterministic alias dictionaries for facility zones and neural sanity operations.
    /// </summary>
    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    [CommandHandler(typeof(GameConsoleCommandHandler))]
    public class Scp575Command : ICommand, IUsageProvider
    {
        #region Heuristic Internal Enums
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
            Sanity,
            Status
        }

        private enum SanityAction
        {
            Set,
            Add,
            Subtract
        }
        #endregion

        #region Zone & Action Aliases
        private static readonly IReadOnlyDictionary<string, FacilityZone> ZoneAliases =
            new Dictionary<string, FacilityZone>(StringComparer.OrdinalIgnoreCase)
            {
                { "lcz", FacilityZone.LightContainment },
                { "light", FacilityZone.LightContainment },
                { "hcz", FacilityZone.HeavyContainment },
                { "heavy", FacilityZone.HeavyContainment },
                { "ez", FacilityZone.Entrance },
                { "entrance", FacilityZone.Entrance },
                { "surface", FacilityZone.Surface },
                { "other", FacilityZone.Other }
            };

        private static bool TryResolveZone(string input, out FacilityZone zone)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                zone = FacilityZone.None;
                return false;
            }

            if (ZoneAliases.TryGetValue(input, out zone))
            {
                return true;
            }

            var interpretation = input.InterpretEnum<FacilityZone>();
            if (interpretation.Status is not (InterpretationStatus.NoMatchFound or InterpretationStatus.Ambiguous))
            {
                zone = interpretation.Value;
                return true;
            }

            zone = FacilityZone.None;
            return false;
        }

        private static bool TryResolveSanityAction(string input, out SanityAction action)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                action = default;
                return false;
            }

            if (input.Equals("sub", StringComparison.OrdinalIgnoreCase))
            {
                action = SanityAction.Subtract;
                return true;
            }

            var interpretation = input.InterpretEnum<SanityAction>();
            if (interpretation.Status is not (InterpretationStatus.NoMatchFound or InterpretationStatus.Ambiguous))
            {
                action = interpretation.Value;
                return true;
            }

            action = default;
            return false;
        }
        #endregion

        #region Command Meta Properties
        /// <inheritdoc />
        public string Command => "scp575";

        /// <inheritdoc />
        public string[] Aliases => new[] { "575", "ev575" };

        /// <inheritdoc />
        public string Description => "Administrative control interface for the SCP-575 anomalous system lifecycle built with fuzzy parsing.";

        /// <inheritdoc />
        public string[] Usage => new[]
        {
            "init / start",
            "blackout / trigger [zone]",
            "stop / disable",
            "status",
            "setstacks [amount]",
            "changestacks [delta]",
            "sanity [player] (displays sanity)",
            "sanity [player] [set / add / sub] [value]"
        };
        #endregion

        #region Execution Core Pipeline
        /// <inheritdoc />
        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            if (!sender.ConfirmPermission(PlayerPermissions.FacilityManagement, out response))
            {
                return false;
            }

            if (arguments.Count == 0)
            {
                response = "Invalid Command Configuration. Available structural subcommands:\n" +
                           " - init / start                            : Forces the SCP-575 anomaly lifecycle online.\n" +
                           " - blackout / trigger [zone]               : Triggers a facility-wide or zone-specific blackout.\n" +
                           " - stop / disable                          : Aborts active blackouts, restores power, and halts loops.\n" +
                           " - status                                  : Displays running anomaly metrics and stack counters.\n" +
                           " - setstacks [amount]                      : Sets the blackout stack accumulation counter directly.\n" +
                           " - changestacks [delta]                    : Shifts the blackout stack counter by a relative value.\n" +
                           " - sanity [player]                         : Retrieves current sanity metric for a given player.\n" +
                           " - sanity [player] [set/add/sub] [value]   : Modifies player neuro-integrity metric (0% - 100%).";
                return false;
            }

            Plugin plugin = Plugin.Instance;
            if (plugin?.NpcLogic == null)
            {
                response = "Execution Critical Failure: The central SCP-575 runtime singleton or unified core NpcLogic module is offline.";
                return false;
            }

            InterpretationResult<SubCommand> commandInterpretation = arguments.At(0).InterpretEnum<SubCommand>();

            if (commandInterpretation.Status is InterpretationStatus.NoMatchFound)
            {
                response = $"Syntax Interpretation Failure: '{arguments.At(0)}' is completely unrecognized.";
                return false;
            }

            if (commandInterpretation.Status is InterpretationStatus.Ambiguous)
            {
                response = $"Ambiguous Subcommand Identified. Candidates: {string.Join(", ", commandInterpretation.Candidates)}";
                return false;
            }

            switch (commandInterpretation.Value)
            {
                case SubCommand.Init:
                case SubCommand.Start:
                    if (plugin.IsEventActive)
                    {
                        response = "Initialization Cancelled: SCP-575 horror pacing mechanisms are already active.";
                        return false;
                    }

                    plugin.NpcLogic.Init();
                    response = "SUCCESS: SCP-575 anomaly ecosystem forced online.";
                    return true;

                case SubCommand.Stop:
                case SubCommand.Disable:
                    if (!plugin.IsEventActive)
                    {
                        response = "Teardown Aborted: The SCP-575 execution framework is already resting.";
                        return false;
                    }

                    plugin.NpcLogic.Disable();
                    response = "SUCCESS: Explicit runtime teardown completed. Standard facility illumination restored.";
                    return true;

                case SubCommand.Status:
                    response = "SCP-575 Telemetry:\n" +
                               $" - Event Active          : {plugin.IsEventActive}\n" +
                               $" - Blackout Active       : {plugin.NpcLogic.IsBlackoutActive}\n" +
                               $" - Active Blackout Stacks: {plugin.NpcLogic.GetCurrentBlackoutStacks}";
                    return true;

                case SubCommand.Blackout:
                case SubCommand.Trigger:
                    if (!plugin.IsEventActive)
                    {
                        response = "Trigger Denied: The global event framework is offline. Run 'scp575 start' first.";
                        return false;
                    }

                    if (arguments.Count >= 2)
                    {
                        int zoneArgIndex = 1;
                        if (arguments.At(1).Equals("zone", StringComparison.OrdinalIgnoreCase))
                        {
                            if (arguments.Count < 3)
                            {
                                response = $"Syntax error. Usage: scp575 {arguments.At(0)} zone [ {string.Join(" | ", ZoneAliases.Keys)} ]";
                                return false;
                            }
                            zoneArgIndex = 2;
                        }

                        string rawZoneInput = arguments.At(zoneArgIndex);
                        if (!TryResolveZone(rawZoneInput, out FacilityZone targetZone) || targetZone == FacilityZone.None)
                        {
                            response = $"Zone Selection Failed: '{rawZoneInput}' could not be resolved into a valid FacilityZone.";
                            return false;
                        }

                        plugin.NpcLogic.ForceZoneBlackoutEvent(targetZone);
                        response = $"SUCCESS: Targeted blackout sequence dispatched onto zone sector: [{targetZone}].";
                        return true;
                    }

                    plugin.NpcLogic.ForceGlobalBlackoutEvent();
                    response = "SUCCESS: Global power grid collapse triggered across all facility sectors.";
                    return true;

                case SubCommand.SetStacks:
                    if (!plugin.IsEventActive)
                    {
                        response = "Stack Adjustment Denied: The anomaly system must be active.";
                        return false;
                    }

                    if (arguments.Count < 2 || !int.TryParse(arguments.At(1), out int targetStacks) || targetStacks < 0)
                    {
                        response = "Syntax Compilation Error. Correct usage pattern: scp575 setstacks [positive integer value]";
                        return false;
                    }

                    targetStacks = targetStacks.Clamp(0, 100);

                    if (targetStacks == 0)
                    {
                        plugin.NpcLogic.Reset575();
                        response = "SUCCESS: Blackout stack accumulation counter cleared to 0.";
                        return true;
                    }

                    UpdateBlackoutStackMatrix(plugin.NpcLogic, targetStacks);
                    response = $"SUCCESS: Running blackout stack count locked at: {plugin.NpcLogic.GetCurrentBlackoutStacks}";
                    return true;

                case SubCommand.ChangeStacks:
                    if (!plugin.IsEventActive)
                    {
                        response = "Relative Shift Denied: The anomaly system must be active.";
                        return false;
                    }

                    if (arguments.Count < 2 || !int.TryParse(arguments.At(1), out int stackDelta))
                    {
                        response = "Syntax Compilation Error. Correct usage pattern: scp575 changestacks [signed integer delta]";
                        return false;
                    }

                    int calculatedTarget = (plugin.NpcLogic.GetCurrentBlackoutStacks + stackDelta).Clamp(0, 100);
                    UpdateBlackoutStackMatrix(plugin.NpcLogic, calculatedTarget);

                    response = $"SUCCESS: Absolute blackout stack accumulation updated to: {plugin.NpcLogic.GetCurrentBlackoutStacks}";
                    return true;

                case SubCommand.Sanity:
                    if (!plugin.IsEventActive)
                    {
                        response = "Sanity Mutation Denied: Neural metric tracking arrays are offline.";
                        return false;
                    }

                    if (plugin.SanityHandler is null)
                    {
                        response = "Execution Failure: The PlayerSanityHandler tracking core instance is currently unavailable.";
                        return false;
                    }

                    if (arguments.Count < 2)
                    {
                        response = "Syntax Error. Correct usage patterns:\n" +
                                   " - scp575 sanity [player]                         (query current value)\n" +
                                   " - scp575 sanity [player] [set/add/sub] [value]   (mutate value)";
                        return false;
                    }

                    if (arguments.Count == 2 || (arguments.Count == 3 && !TryResolveSanityAction(arguments.At(arguments.Count - 2), out _)))
                    {
                        string queryIdentifier = string.Join(" ", GetSubArray(arguments, 1, arguments.Count - 1)).Trim();
                        if (!Player.ReadyList.TryResolveFuzzy(queryIdentifier, out Player queriedPlayer, out string queryError))
                        {
                            response = queryError;
                            return false;
                        }

                        float sanityLevel = plugin.SanityHandler.GetCurrentSanity(queriedPlayer);
                        response = $"Neural Metric: Player [{queriedPlayer.Nickname}] current sanity is at {sanityLevel:F1}%.";
                        return true;
                    }

                    if (arguments.Count < 4)
                    {
                        response = "Syntax Error. Correct usage pattern: scp575 sanity [player] [set/add/sub] [value]";
                        return false;
                    }

                    int valIndex = arguments.Count - 1;
                    int actionIndex = arguments.Count - 2;
                    int namePartsCount = actionIndex - 1;

                    string[] rawNameParts = new string[namePartsCount];
                    for (int i = 0; i < namePartsCount; i++)
                    {
                        rawNameParts[i] = arguments.At(1 + i);
                    }
                    string playerIdentifier = string.Join(" ", rawNameParts).Trim();

                    if (!Player.ReadyList.TryResolveFuzzy(playerIdentifier, out Player targetPlayer, out string playerError))
                    {
                        response = playerError;
                        return false;
                    }

                    if (!TryResolveSanityAction(arguments.At(actionIndex), out SanityAction resolvedAction))
                    {
                        response = $"Action Translation Failed: Operation '{arguments.At(actionIndex)}' is unrecognized. Expected: set, add, sub/subtract.";
                        return false;
                    }

                    if (!float.TryParse(arguments.At(valIndex), out float sanityValue) || sanityValue < 0f)
                    {
                        response = "Value Validation Error: Expected non-negative decimal value.";
                        return false;
                    }

                    float currentSanity = plugin.SanityHandler.GetCurrentSanity(targetPlayer);
                    float finalSanity = currentSanity;

                    switch (resolvedAction)
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

                    finalSanity = finalSanity.Clamp(0f, 100f);
                    plugin.SanityHandler.SetPlayerSanity(targetPlayer, finalSanity);

                    response = $"SUCCESS: Neuro-integrity mapping updated for [{targetPlayer.Nickname}]. Sanity adjusted from {currentSanity:F1}% to {finalSanity:F1}%.";
                    return true;

                default:
                    response = "System Internal Mapping Exception: Unhandled command route structural fallback tree reached.";
                    return false;
            }
        }
        #endregion

        #region Private Infrastructure Syncs
        private static void UpdateBlackoutStackMatrix(SCP_575.Npc.Methods logic, int targetLevel)
        {
            int safeTarget = targetLevel.Clamp(0, 100);
            while (logic.GetCurrentBlackoutStacks < safeTarget)
            {
                logic.IncrementBlackoutStack();
            }

            while (logic.GetCurrentBlackoutStacks > safeTarget)
            {
                logic.DecrementBlackoutStack();
            }
        }

        private static string[] GetSubArray(ArraySegment<string> segment, int startIndex, int length)
        {
            string[] result = new string[length];
            for (int i = 0; i < length; i++)
            {
                result[i] = segment.At(startIndex + i);
            }
            return result;
        }
        #endregion
    }
}