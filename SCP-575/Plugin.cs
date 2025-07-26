namespace SCP_575
{
    using LabApi.Events.CustomHandlers;
    using MEC;
    using SCP_575.Handlers;
    using SCP_575.Shared;
    using SCP_575.Shared.Audio;

    /// <summary>
    /// The main plugin class for the SCP-575 NPC, responsible for managing event handlers and NPC behaviors.
    /// </summary>
    public class Plugin : Exiled.API.Features.Plugin<Config>
    {
        private EventHandler _eventHandler;
        private PlayerSanityHandler _sanityHandler;
        private PlayerLightsourceHandler _lightsourceHandler;
        private NestingObjects.Npc _npc;
        private Scp575AudioManager _audioManager;
        private Config _config;

        /// <summary>
        /// Gets the singleton instance of the SCP-575 plugin.
        /// </summary>
        public static Plugin Singleton { get; private set; }

        /// <summary>
        /// Gets the event handler instance for managing server and player events.
        /// </summary>
        public EventHandler EventHandler => _eventHandler;

        /// <summary>
        /// Gets the event handler of the player sanity mechanics.
        /// </summary>

        public PlayerSanityHandler SanityEventHandler => _sanityHandler;

        /// <summary>
        /// Gets the event handler of the player lightsource mechanics.
        /// </summary>

        public PlayerLightsourceHandler LightsourceHandler => _lightsourceHandler;


        /// <summary>
        /// Gets the NPC instance for managing SCP-575 behaviors.
        /// </summary>
        public NestingObjects.Npc Npc => _npc;

        public Scp575AudioManager AudioManager => _audioManager;

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
        public override System.Version Version => new(8,1,0);

        /// <summary>
        /// Gets the minimum required Exiled version for compatibility.
        /// </summary>
        public override System.Version RequiredExiledVersion => new(9, 6, 0);

        /// <summary>
        /// Called when the plugin is enabled, initializing components and registering event handlers.
        /// </summary>
        public override void OnEnabled()
        {
            try
            {
                Singleton = this;
                _eventHandler = new EventHandler(this);
                _audioManager = new Scp575AudioManager();
                _npc = new NestingObjects.Npc(this);
                _config = new Config();

                RegisterEvents();

                Library_ExiledAPI.LogInfo("Plugin.OnEnabled", "SCP-575 plugin enabled successfully.");
                base.OnEnabled();
            }
            catch (System.Exception ex)
            {
                Library_ExiledAPI.LogError("Plugin.OnEnabled", $"Failed to enable SCP-575 plugin: {ex.Message}");
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
                foreach (CoroutineHandle handle in _eventHandler.Coroutines)
                {
                    Timing.KillCoroutines(handle);
                }
                _eventHandler.Coroutines.Clear();

                UnregisterEvents();
                Singleton = null;
                _eventHandler = null;
                _npc = null;
                _audioManager.CleanupAllSpeakers();
                _audioManager = null;
                _config = null;

                Library_ExiledAPI.LogInfo("Plugin.OnDisabled", "SCP-575 plugin disabled successfully.");
                base.OnDisabled();
            }
            catch (System.Exception ex)
            {
                Library_ExiledAPI.LogError("Plugin.OnDisabled", $"Failed to disable SCP-575 plugin: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Registers event handlers for server and player-related events.
        /// </summary>
        private void RegisterEvents()
        {
            LabApi.Events.Handlers.ServerEvents.RoundStarted += _eventHandler.OnRoundStarted;
            LabApi.Events.Handlers.ServerEvents.RoundEnded += _eventHandler.OnRoundEnded;
            LabApi.Events.Handlers.ServerEvents.WaitingForPlayers += _eventHandler.OnWaitingForPlayers;
            LabApi.Events.Handlers.PlayerEvents.Hurting += _eventHandler.OnPlayerHurting;
            LabApi.Events.Handlers.PlayerEvents.Hurt += _eventHandler.OnPlayerHurt;
            LabApi.Events.Handlers.PlayerEvents.Dying += _eventHandler.OnPlayerDying;
            LabApi.Events.Handlers.PlayerEvents.Death += _eventHandler.OnPlayerDeath;
            Exiled.Events.Handlers.Player.SpawnedRagdoll += _eventHandler.OnSpawnedRagdoll;
            CustomHandlersManager.RegisterEventsHandler(_lightsourceHandler);
            CustomHandlersManager.RegisterEventsHandler(_sanityHandler);
            Library_ExiledAPI.LogDebug("Plugin.RegisterEvents", "Registered server and player event handlers.");
        }

        /// <summary>
        /// Unregisters event handlers to clean up resources.
        /// </summary>
        private void UnregisterEvents()
        {
            if (_eventHandler != null)
            {
                LabApi.Events.Handlers.ServerEvents.RoundStarted -= _eventHandler.OnRoundStarted;
                LabApi.Events.Handlers.ServerEvents.RoundEnded -= _eventHandler.OnRoundEnded;
                LabApi.Events.Handlers.ServerEvents.WaitingForPlayers -= _eventHandler.OnWaitingForPlayers;
                LabApi.Events.Handlers.PlayerEvents.Hurting -= _eventHandler.OnPlayerHurting;
                LabApi.Events.Handlers.PlayerEvents.Hurt -= _eventHandler.OnPlayerHurt;
                LabApi.Events.Handlers.PlayerEvents.Dying -= _eventHandler.OnPlayerDying;
                LabApi.Events.Handlers.PlayerEvents.Death -= _eventHandler.OnPlayerDeath;
                Exiled.Events.Handlers.Player.SpawnedRagdoll -= _eventHandler.OnSpawnedRagdoll;
                CustomHandlersManager.UnregisterEventsHandler(_lightsourceHandler);
                CustomHandlersManager.UnregisterEventsHandler(_sanityHandler);
                Library_ExiledAPI.LogDebug("Plugin.UnregisterEvents", "Unregistered server and player event handlers.");
            }
        }
    }
}