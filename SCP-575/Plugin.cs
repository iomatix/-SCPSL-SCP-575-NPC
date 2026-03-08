namespace SCP_575
{
    using LabApi.Events.CustomHandlers;
    using SCP_575.Handlers;
    using SCP_575.Shared;
    using SCP_575.Shared.Audio;

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

        private bool _isEventActive = false;

        public static Plugin Singleton { get; private set; }
        public PlayerSanityHandler SanityEventHandler => _sanityHandler;
        public PlayerLightsourceHandler LightsourceHandler => _lightsourceHandler;

        public bool IsEventActive
        {
            get => _isEventActive;
            set => _isEventActive = value;
        }

        public NestingObjects.Npc Npc => _npc;
        public Scp575AudioManager AudioManager => _audioManager;
        public LibraryLabAPI LibraryLabAPI => _libraryLabAPI;

        public override string Author => "iomatix";
        public override string Name => "SCP-575 NPC";
        public override string Prefix => "SCP575";
        public override System.Version Version => new(9, 0, 1);
        public override System.Version RequiredExiledVersion => new(9, 9, 2);

        public override void OnEnabled()
        {
            try
            {
                Singleton = this;

                // Note: _config = new Config(); was removed. Exiled populates the Config property automatically.
                _audioManager = new Scp575AudioManager(this);
                _libraryLabAPI = new LibraryLabAPI(this);

                _lifecycleHandler = new LifecycleHandler(this);
                _generatorHandler = new GeneratorHandler(this);
                _explosionHandler = new ExplosionHandler(this);
                _damageHandler = new PlayerDamageHandler(this);
                _ragdollHandler = new RagdollHandler(this);
                _sanityHandler = new PlayerSanityHandler(this);
                _lightsourceHandler = new PlayerLightsourceHandler(this);

                _npc = new NestingObjects.Npc(this);

                _sanityHandler?.Initialize();
                _lightsourceHandler?.Initialize();

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

        public override void OnDisabled()
        {
            try
            {
                _isEventActive = false;

                UnregisterEvents();

                _sanityHandler?.Dispose();
                _lightsourceHandler?.Dispose();

                // Nullify in reverse order of initialization
                _npc = null;
                _lightsourceHandler = null;
                _sanityHandler = null;
                _ragdollHandler = null;
                _damageHandler = null;
                _explosionHandler = null;
                _generatorHandler = null;
                _lifecycleHandler = null;

                _libraryLabAPI = null;
                _audioManager = null;
                Singleton = null;

                LibraryLabAPI.LogInfo("Plugin.OnDisabled", "SCP-575 plugin disabled successfully.");
                base.OnDisabled();
            }
            catch (System.Exception ex)
            {
                LibraryLabAPI.LogError("Plugin.OnDisabled", $"Failed to disable SCP-575 plugin: {ex.Message}");
                throw;
            }
        }

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