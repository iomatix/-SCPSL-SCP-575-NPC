namespace SCP_575
{
    using LabApi.Events.CustomHandlers;
    using SCP_575.Handlers;
    using SCP_575.Shared;
    using SCP_575.Shared.Audio;
    using System;

    /// <summary>
    /// Central initialization bootstrap layer for the SCP-575 plugin ecosystem.
    /// Manages architectural dependencies, binds event tracking frameworks, and drives core subsystem lifecycles.
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
        private MapHandler _mapHandler;

        private NestingObjects.Npc _npc;
        private Scp575AudioManager _audioManager;
        private Scp575AudioDirector _audioDirector;
        private LibraryLabAPI _libraryLabAPI;

        private bool _isEventActive = false;

        public static Plugin Singleton { get; private set; }
        public PlayerSanityHandler SanityEventHandler => _sanityHandler;
        public PlayerLightsourceHandler LightsourceHandler => _lightsourceHandler;
        public MapHandler MapHandler => _mapHandler;

        public bool IsEventActive
        {
            get => _isEventActive;
            set => _isEventActive = value;
        }

        public NestingObjects.Npc Npc => _npc;
        public Scp575AudioManager AudioManager => _audioManager;
        public Scp575AudioDirector AudioDirector => _audioDirector;
        public LibraryLabAPI LibraryLabAPI => _libraryLabAPI;

        public override string Author => "iomatix";
        public override string Name => "SCP-575 NPC";
        public override string Prefix => "SCP575";
        public override System.Version Version => new(10, 2, 0);
        public override System.Version RequiredExiledVersion => new(9, 9, 3);

        public override void OnEnabled()
        {
            Singleton = this;

            try
            {
                _libraryLabAPI = new LibraryLabAPI(this);
                Config.Validate();
            }
            catch (Exception ex)
            {
                _libraryLabAPI = null;
                Singleton = null;

                Exiled.API.Features.Log.Error($"[SCP-575 Startup Aborted] Configuration validation failed: {ex.Message}");
                throw new InvalidOperationException("SCP-575 initialization aborted due to invalid plugin configuration.", ex);
            }

            try
            {
                // Structural allocation order: Dependencies must be instantiated before the director consumes them
                _audioManager = new Scp575AudioManager(this);
                _sanityHandler = new PlayerSanityHandler(this);
                _audioDirector = new Scp575AudioDirector(this, _audioManager, _sanityHandler);

                _lifecycleHandler = new LifecycleHandler(this);
                _generatorHandler = new GeneratorHandler(this);
                _explosionHandler = new ExplosionHandler(this);
                _damageHandler = new PlayerDamageHandler(this);
                _ragdollHandler = new RagdollHandler(this);
                _lightsourceHandler = new PlayerLightsourceHandler(this);
                _mapHandler = new MapHandler(this);

                _npc = new NestingObjects.Npc(this);

                // Wake up underlying process loops
                _sanityHandler?.Initialize();
                _lightsourceHandler?.Initialize();
                _audioDirector?.Initialize();

                RegisterEvents();

                LibraryLabAPI.LogInfo("Plugin.OnEnabled", "SCP-575 plugin enabled successfully.");
                base.OnEnabled();
            }
            catch (Exception ex)
            {
                LibraryLabAPI.LogError("Plugin.OnEnabled", $"Critical failure during handler initialization: {ex.Message}");
                throw;
            }
        }

        public override void OnDisabled()
        {
            _isEventActive = false;

            try
            {
                UnregisterEvents();
            }
            catch (Exception ex)
            {
                LibraryLabAPI.LogError("Plugin.OnDisabled", $"Error while unregistering events: {ex.Message}");
            }

            try
            {
                _audioDirector?.Dispose();
            }
            catch (Exception ex)
            {
                LibraryLabAPI.LogError("Plugin.OnDisabled", $"Error while disposing AudioDirector: {ex.Message}");
            }

            try
            {
                _audioManager?.Clean(fullShutdown: true);
            }
            catch (Exception ex)
            {
                LibraryLabAPI.LogError("Plugin.OnDisabled", $"Error while cleaning up Scp575AudioManager: {ex.Message}");
            }

            try
            {
                _sanityHandler?.Dispose();
            }
            catch (Exception ex)
            {
                LibraryLabAPI.LogError("Plugin.OnDisabled", $"Error while disposing SanityHandler: {ex.Message}");
            }

            try
            {
                _lightsourceHandler?.Dispose();
            }
            catch (Exception ex)
            {
                LibraryLabAPI.LogError("Plugin.OnDisabled", $"Error while disposing LightsourceHandler: {ex.Message}");
            }

            // Teardown sequence executed in reverse structural order to ensure safe resource releases
            _npc = null;
            _lightsourceHandler = null;
            _sanityHandler = null;
            _ragdollHandler = null;
            _damageHandler = null;
            _explosionHandler = null;
            _generatorHandler = null;
            _lifecycleHandler = null;
            _mapHandler = null;

            _audioDirector = null;
            _audioManager = null;
            _libraryLabAPI = null;
            Singleton = null;

            base.OnDisabled();
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
            CustomHandlersManager.RegisterEventsHandler(_mapHandler);
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
            CustomHandlersManager.UnregisterEventsHandler(_mapHandler);
            LibraryLabAPI.LogDebug("Plugin.UnregisterEvents", "Unregistered server and player event handlers.");
        }
    }
}