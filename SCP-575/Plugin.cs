namespace SCP_575
{
    using LabApi.Events.CustomHandlers;
    using MEC;
    using SCP_575.Handlers;
    using SCP_575.Shared;
    using SCP_575.Shared.Audio;
    using System.Collections.Generic;

    /// <summary>
    /// The main plugin class for the SCP-575 NPC, responsible for managing event handlers and NPC behaviors.
    /// </summary>
    public class Plugin : Exiled.API.Features.Plugin<Config>
    {
        private LifecycleHandler _lifecycleHandler;
        private GeneratorHandler _generatorHandler;
        private ExplosionHandler _explosionHandler;
        private PlayerDamageHandler _damageHandler;
        private RagdollHandler _ragdollHandler;
        private PlayerSanityHandler _sanityHandler;
        private PlayerLightsourceHandler _lightsourceHandler;

        private NestingObjects.Npc _npc;
        private Scp575AudioManager _audioManager;
        private LibraryLabAPI _libraryLabAPI;
        private Config _config;

        private bool _isEventActive = false;

        /// <summary>
        /// Gets the singleton instance of the SCP-575 plugin.
        /// </summary>
        public static Plugin Singleton { get; private set; }

        /// <summary>
        /// Gets the event handler of the player sanity mechanics.
        /// </summary>

        public PlayerSanityHandler SanityEventHandler => _sanityHandler;

        /// <summary>
        /// Gets the event handler of the player lightsource mechanics.
        /// </summary>

        public PlayerLightsourceHandler LightsourceHandler => _lightsourceHandler;

        /// <summary>
        /// Gets or sets the blackout event status of the current round.
        /// </summary>
        public bool IsEventActive
        {
            get => _isEventActive;
            set => _isEventActive = value;
        }

        /// <summary>
        /// Gets the NPC instance for managing SCP-575 behaviors.
        /// </summary>
        public NestingObjects.Npc Npc => _npc;

        public Scp575AudioManager AudioManager => _audioManager;

        public LibraryLabAPI LibraryLabAPI => _libraryLabAPI;

        /// <summary>
        /// Gets the author of the plugin.
        /// </summary>
        public override string Author => "iomatix";

        /// <summary>
        /// Gets the name of the plugin.
        /// </summary>
        public override string Name => "SCP-575 NPC";

        /// <summary>
        /// Gets the prefix used for configuration and logging.
        /// </summary>
        public override string Prefix => "SCP575";

        /// <summary>
        /// Gets the version of the plugin.
        /// </summary>
        public override System.Version Version => new(9, 0, 0);

        /// <summary>
        /// Gets the minimum required Exiled version for compatibility.
        /// </summary>
        public override System.Version RequiredExiledVersion => new(9, 9, 2);

        /// <summary>
        /// Called when the plugin is enabled, initializing components and registering event handlers.
        /// </summary>
        public override void OnEnabled()
        {
            try
            {
                Singleton = this;
                _audioManager = new Scp575AudioManager(this);
                _libraryLabAPI = new LibraryLabAPI(this);
                _config = new Config();

                // Initialize the custom handlers BEFORE registering events
                _lifecycleHandler = new LifecycleHandler(this);
                _generatorHandler = new GeneratorHandler(this);
                _explosionHandler = new ExplosionHandler(this);
                _damageHandler = new PlayerDamageHandler(this);
                _ragdollHandler = new RagdollHandler(this);
                _sanityHandler = new PlayerSanityHandler(this);
                _lightsourceHandler = new PlayerLightsourceHandler(this);

                // Nexting Objects
                _npc = new NestingObjects.Npc(this);

                _sanityHandler?.Initialize();
                _lightsourceHandler?.Initialize();

                // Register event handlers after all components are initialized
                RegisterEvents();             

                LibraryLabAPI.LogInfo("Plugin.OnEnabled", "SCP-575 plugin enabled successfully.");
                base.OnEnabled();
            }
            catch (System.Exception ex)
            {
                LibraryLabAPI.LogError("Plugin.OnEnabled", $"Failed to enable SCP-575 plugin: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Called when the plugin is disabled, unregistering event handlers and cleaning up resources.
        /// </summary>
        public override void OnDisabled()
        {
            try
            {
                _isEventActive = false;

                UnregisterEvents();

                // Dispose handlers properly  
                _sanityHandler?.Dispose();
                _lightsourceHandler?.Dispose();

                Singleton = null;
                _sanityHandler = null;
                _lightsourceHandler = null;
                _npc = null;
                _audioManager = null;
                _config = null;

                LibraryLabAPI.LogInfo("Plugin.OnDisabled", "SCP-575 plugin disabled successfully.");
                base.OnDisabled();
            }
            catch (System.Exception ex)
            {
                LibraryLabAPI.LogError("Plugin.OnDisabled", $"Failed to disable SCP-575 plugin: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Registers event handlers for server and player-related events.
        /// </summary>
        private void RegisterEvents()
        {
            CustomHandlersManager.RegisterEventsHandler(_lifecycleHandler);
            CustomHandlersManager.RegisterEventsHandler(_generatorHandler);
            CustomHandlersManager.RegisterEventsHandler(_explosionHandler);
            CustomHandlersManager.RegisterEventsHandler(_damageHandler);
            CustomHandlersManager.RegisterEventsHandler(_ragdollHandler);
            CustomHandlersManager.RegisterEventsHandler(_lightsourceHandler);
            CustomHandlersManager.RegisterEventsHandler(_sanityHandler);
            LibraryLabAPI.LogDebug("Plugin.RegisterEvents", "Registered server and player event handlers.");
        }

        /// <summary>
        /// Unregisters event handlers to clean up resources.
        /// </summary>
        private void UnregisterEvents()
        {

            CustomHandlersManager.UnregisterEventsHandler(_lifecycleHandler);
            CustomHandlersManager.UnregisterEventsHandler(_generatorHandler);
            CustomHandlersManager.UnregisterEventsHandler(_explosionHandler);
            CustomHandlersManager.UnregisterEventsHandler(_damageHandler);
            CustomHandlersManager.UnregisterEventsHandler(_ragdollHandler);
            CustomHandlersManager.UnregisterEventsHandler(_lightsourceHandler);
            CustomHandlersManager.UnregisterEventsHandler(_sanityHandler);
            LibraryLabAPI.LogDebug("Plugin.UnregisterEvents", "Unregistered server and player event handlers.");
        }
    }
}