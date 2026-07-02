namespace SCP_575.Commands
{
    using CommandSystem;
    using RemoteAdmin;
    using System;

    /// <summary>
    /// Administrative command router for SCP-575 plugin management.
    /// Handles manual runtime initialization, blackout execution, lifecycle termination, and stack overrides.
    /// </summary>
    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    [CommandHandler(typeof(GameConsoleCommandHandler))]
    public class Scp575Command : ICommand, IUsageProvider
    {
        /// <inheritdoc />
        public string Command => "scp575";

        /// <inheritdoc />
        public string[] Aliases => new[] { "575", "ev575" };

        /// <inheritdoc />
        public string Description => "Administrative control interface for the SCP-575 anomalous system lifecycle.";

        /// <inheritdoc />
        public string[] Usage => new[] { "init/start", "blackout/trigger", "stop/disable", "setstacks [amount]" };

        /// <inheritdoc />
        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            // Enforcement of permission controls over high-impact administrative triggers
            if (sender is PlayerCommandSender playerSender && !playerSender.CheckPermission(PlayerPermissions.FacilityManagement))
            {
                response = "Transhandling rejected. You do not possess the required administrative clearance (FacilityManagement) to call this command.";
                return false;
            }

            if (arguments.Count == 0)
            {
                response = "Invalid command configuration. Structural sub-commands available:\n" +
                           " - init / start    : Force-initializes the SCP-575 baseline framework if it skipped early-round rolling.\n" +
                           " - blackout / trigger : Requests an instantaneous execution of a global facility blackout sequence.\n" +
                           " - stop / disable  : Safely aborts and de-allocates all persistent tracking hooks and background timers.\n" +
                           " - setstacks [val] : Explicitly updates the running atmospheric blackout stack accumulation multiplier.";
                return false;
            }

            var plugin = Plugin.Singleton;
            if (plugin == null || plugin.Npc?.Methods == null)
            {
                response = "Execution critical failure: The core SCP-575 runtime singleton or NPC system wrapper is unavailable.";
                return false;
            }

            string subAction = arguments.At(0).ToLower();

            switch (subAction)
            {
                case "init":
                case "start":
                    if (plugin.IsEventActive)
                    {
                        response = "Initialization canceled: SCP-575 mechanisms are already fully active and ticking within this round state.";
                        return false;
                    }

                    // Passing default parameter triggers absolute bypass of standard configuration roll percentage weights
                    plugin.Npc.Methods.Init();
                    response = "SUCCESS: SCP-575 event architecture has been forced online. Environmental trackers, audio emitters, and decay graphs are now initialized.";
                    return true;

                case "blackout":
                case "trigger":
                    if (!plugin.IsEventActive)
                    {
                        response = "Trigger denied: The global event framework is offline. Execute 'scp575 init' to prepare core systems first.";
                        return false;
                    }

                    // Enforces the immediate invocation of the CASSIE notification and light-suppression state pipelines
                    plugin.Npc.Methods.ForceGlobalBlackoutEvent();
                    response = "SUCCESS: Immediate global blackout event dispatched. Processing soundscape triggers and network illumination updates.";
                    return true;

                case "stop":
                case "disable":
                    if (!plugin.IsEventActive)
                    {
                        response = "Teardown aborted: The SCP-575 handler engine is already resting in an un-initialized state.";
                        return false;
                    }

                    plugin.Npc.Methods.Disable();
                    response = "SUCCESS: Explicit runtime teardown committed. Active visual flickers, audio processing modules, and context dictionaries purged.";
                    return true;

                case "setstacks":
                    if (!plugin.IsEventActive)
                    {
                        response = "Stack adjustment denied: The system environment must be active to balance multiplier weights.";
                        return false;
                    }

                    if (arguments.Count < 2 || !int.TryParse(arguments.At(1), out int targetStacks) || targetStacks < 0)
                    {
                        response = "Syntax compilation error. Expected usage pattern: scp575 setstacks [positive integer value]";
                        return false;
                    }

                    if (targetStacks == 0)
                    {
                        plugin.Npc.Methods.Reset575();
                        response = "SUCCESS: Blackout stack register cleared to 0. Facility electrical frameworks and illumination fields restored.";
                        return true;
                    }

                    // Increment or decrement the locked atomic state tracking cells until alignment with the admin input is verified
                    while (plugin.Npc.Methods.GetCurrentBlackoutStacks < targetStacks)
                    {
                        plugin.Npc.Methods.IncrementBlackoutStack();
                    }
                    while (plugin.Npc.Methods.GetCurrentBlackoutStacks > targetStacks)
                    {
                        plugin.Npc.Methods.DecrementBlackoutStack();
                    }

                    response = $"SUCCESS: Environmental threat matrix compiled. Current blackout stack accumulation locked at: {plugin.Npc.Methods.GetCurrentBlackoutStacks}";
                    return true;

                default:
                    response = $"Syntax interpretation failure: '{subAction}' is not a recognized operational subcommand. Use 'init', 'blackout', 'stop', or 'setstacks'.";
                    return false;
            }
        }
    }
}