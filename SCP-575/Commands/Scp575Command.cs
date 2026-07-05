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
    /// Administrative command router for SCP-575 plugin management utilizing heuristic fuzzy string parsing layouts.
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
            Sanity
        }

        private enum SanityAction
        {
            Set,
            Add,
            Subtract
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
        public string[] Usage => new[] { "init/start", "blackout/trigger [zone]", "stop/disable", "setstacks [amount]", "changestacks [delta]", "sanity [player] [set/add/sub] [value]" };
        #endregion

        #region Execution Core Pipeline
        /// <inheritdoc />
        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            if (sender is PlayerCommandSender playerSender && !playerSender.CheckPermission(PlayerPermissions.FacilityManagement))
            {
                response = "Unauthorized Command Execution: You do not possess the required administrative clearance (FacilityManagement) to call this command.";
                return false;
            }

            if (arguments.Count == 0)
            {
                response = "Invalid Command Configuration. Refer to standard usage paradigms.";
                return false;
            }

            Plugin plugin = Plugin.Singleton;
            if (plugin?.NpcLogic is null)
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
                    response = "SUCCESS: Explicit runtime teardown completed.";
                    return true;

                case SubCommand.Blackout:
                case SubCommand.Trigger:
                    if (!plugin.IsEventActive)
                    {
                        response = "Trigger Denied: The global event framework is offline.";
                        return false;
                    }

                    if (arguments.Count >= 2)
                    {
                        InterpretationResult<FacilityZone> zoneInterpretation = arguments.At(1).InterpretEnum<FacilityZone>();

                        if (zoneInterpretation.Status is InterpretationStatus.NoMatchFound)
                        {
                            response = $"Zone Selection Failed: '{arguments.At(1)}' is invalid.";
                            return false;
                        }

                        if (zoneInterpretation.Status is InterpretationStatus.Ambiguous)
                        {
                            response = $"Ambiguous Sector Identification. Candidates: {string.Join(", ", zoneInterpretation.Candidates)}";
                            return false;
                        }

                        plugin.NpcLogic.ForceZoneBlackoutEvent(zoneInterpretation.Value);
                        response = $"SUCCESS: Targeted blackout sequence dispatched onto zone sector: [{zoneInterpretation.Value}].";
                        return true;
                    }

                    plugin.NpcLogic.ForceGlobalBlackoutEvent();
                    response = "SUCCESS: Global power grid collapse triggered.";
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

                    int calculatedTarget = (plugin.NpcLogic.GetCurrentBlackoutStacks + stackDelta).LimitMin(0);
                    UpdateBlackoutStackMatrix(plugin.NpcLogic, calculatedTarget);

                    response = $"SUCCESS: Absolute blackout stack accumulation updated to: {plugin.NpcLogic.GetCurrentBlackoutStacks}";
                    return true;

                case SubCommand.Sanity:
                    if (!plugin.IsEventActive)
                    {
                        response = "Sanity Mutation Denied: Neural metric tracking arrays are offline.";
                        return false;
                    }

                    if (arguments.Count < 4)
                    {
                        response = "Syntax Compilation Error. Correct usage pattern: scp575 sanity [player] [set/add/sub] [value]";
                        return false;
                    }

                    int valIndex = arguments.Count - 1;
                    int actionIndex = arguments.Count - 2;
                    int namePartsCount = actionIndex - 1;

                    if (namePartsCount <= 0)
                    {
                        response = "Syntax Compilation Error. Invalid argument formatting structural layers.";
                        return false;
                    }

                    string[] rawNameParts = new string[namePartsCount];
                    for (int i = 0; i < namePartsCount; i++)
                    {
                        rawNameParts[i] = arguments.At(1 + i);
                    }
                    string playerIdentifier = string.Join(" ", rawNameParts).Trim();

                    // Complete elimination of algorithmic boilerplate inside the command handler.
                    // The lookup pipeline maps fluently straight through our newly expanded API Extension channels securely.
                    if (!Player.ReadyList.TryResolveFuzzy(playerIdentifier, out Player targetPlayer, out string playerError))
                    {
                        response = playerError;
                        return false;
                    }

                    InterpretationResult<SanityAction> actionInterpretation = arguments.At(actionIndex).InterpretEnum<SanityAction>();
                    if (actionInterpretation.Status is InterpretationStatus.NoMatchFound)
                    {
                        response = $"Action Translation Failed: Operation wrapper '{arguments.At(actionIndex)}' is unknown.";
                        return false;
                    }
                    if (actionInterpretation.Status is InterpretationStatus.Ambiguous)
                    {
                        response = $"Ambiguous Operation Entry. Candidates: {string.Join(", ", actionInterpretation.Candidates)}";
                        return false;
                    }

                    if (!float.TryParse(arguments.At(valIndex), out float sanityValue) || sanityValue < 0f)
                    {
                        response = "Value Validation Error: Expected non-negative decimal factor.";
                        return false;
                    }

                    if (plugin.SanityHandler is null)
                    {
                        response = "Execution Failure: The PlayerSanityHandler tracking core instance is currently unavailable.";
                        return false;
                    }

                    float currentSanity = plugin.SanityHandler.GetCurrentSanity(targetPlayer);
                    float finalSanity = currentSanity;

                    switch (actionInterpretation.Value)
                    {
                        case SanityAction.Set: finalSanity = sanityValue; break;
                        case SanityAction.Add: finalSanity = currentSanity + sanityValue; break;
                        case SanityAction.Subtract: finalSanity = currentSanity - sanityValue; break;
                    }

                    finalSanity = finalSanity.Clamp(0f, 100f);
                    plugin.SanityHandler.SetPlayerSanity(targetPlayer, finalSanity);

                    response = $"SUCCESS: Neuro-integrity mapping updated for player [{targetPlayer.Nickname}]. Sanity adjusted from {currentSanity}% to {finalSanity}%.";
                    return true;

                default:
                    response = "System Internal Mapping Exception: Unhandled command route structural fallback tree reached.";
                    return false;
            }
        }
        #endregion

        #region Private Infrastructure Syncs
        private static void UpdateBlackoutStackMatrix(Methods logic, int targetLevel)
        {
            while (logic.GetCurrentBlackoutStacks < targetLevel) logic.IncrementBlackoutStack();
            while (logic.GetCurrentBlackoutStacks > targetLevel) logic.DecrementBlackoutStack();
        }
        #endregion
    }
}