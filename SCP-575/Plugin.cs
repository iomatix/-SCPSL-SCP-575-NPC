namespace SCP_575
{
    using LabApi.Events.CustomHandlers;
    using LabApi.Features.Console;
    using LabApi.Loader;
    using LabApi.Loader.Features.Plugins;
    using SCP_575.ConfigObjects;
    using SCP_575.Handlers;
    using SCP_575.Shared;
    using SCP_575.Shared.Audio;
    using System;

    /// <summary>
    /// Central initialization bootstrap layer for the SCP-575 plugin ecosystem inside LabAPI.
    /// </summary>
    public class Plugin : LabApi.Loader.Features.Plugins.Plugin<Config>
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

        #region Independent Sub-Configurations

        public AudioConfig Audio { get; private set; }
        public BlackoutConfig Blackout { get; private set; }
        public FlashlightSpawnConfig FlashlightSpawn { get; private set; }
        public NpcConfig NpcConfig { get; private set; }
        public PlayerSanityConfig Sanity { get; private set; }
        public PlayerLightsourceConfig LightsourceConfig { get; private set; }
        public HintsConfig Hints { get; private set; }
        public CassieConfig Cassie { get; private set; }

        #endregion

        #region Operational API Properties

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

        // Mandated LabAPI Abstract Overrides
        public override string Author => "iomatix";
        public override string Name => "SCP-575 NPC";
        public override string Description => "Advanced horror pacing sub-drone shadow entity that makes darkness dangerous.";
        public override Version Version => new(10, 3, 0);
        public override Version RequiredApiVersion => new(1, 0, 0);

        #endregion

        /// <summary>
        /// Native LabAPI configuration framework hook. Processes decoupled sub-configs.
        /// </summary>
        public override void LoadConfigs()
        {
            base.LoadConfigs();
            Config.Validate();

            if (this.TryLoadConfig("audio_settings.yml", out AudioConfig loadedAudio))
            {
                Audio = loadedAudio ?? new AudioConfig();
                Audio.Validate();
                this.TrySaveConfig(Audio, "audio_settings.yml");
            }

            if (this.TryLoadConfig("blackout_engine.yml", out BlackoutConfig loadedBlackout))
            {
                Blackout = loadedBlackout ?? new BlackoutConfig();
                Blackout.Validate();
                this.TrySaveConfig(Blackout, "blackout_engine.yml");
            }

            if (this.TryLoadConfig("flashlight_spawning.yml", out FlashlightSpawnConfig loadedFlashlight))
            {
                FlashlightSpawn = loadedFlashlight ?? new FlashlightSpawnConfig();
                FlashlightSpawn.Validate();
                this.TrySaveConfig(FlashlightSpawn, "flashlight_spawning.yml");
            }

            if (this.TryLoadConfig("npc_behavior.yml", out NpcConfig loadedNpc))
            {
                NpcConfig = loadedNpc ?? new NpcConfig();
                NpcConfig.Validate();
                this.TrySaveConfig(NpcConfig, "npc_behavior.yml");
            }

            if (this.TryLoadConfig("sanity_progression.yml", out PlayerSanityConfig loadedSanity))
            {
                Sanity = loadedSanity ?? new PlayerSanityConfig();
                Sanity.Validate();
                this.TrySaveConfig(Sanity, "sanity_progression.yml");
            }

            if (this.TryLoadConfig("player_lightsources.yml", out PlayerLightsourceConfig loadedLights))
            {
                LightsourceConfig = loadedLights ?? new PlayerLightsourceConfig();
                LightsourceConfig.Validate();
                this.TrySaveConfig(LightsourceConfig, "player_lightsources.yml");
            }

            if (this.TryLoadConfig("hints_placement.yml", out HintsConfig loadedHints))
            {
                Hints = loadedHints ?? new HintsConfig();
                Hints.Validate();
                this.TrySaveConfig(Hints, "hints_placement.yml");
            }

            if (this.TryLoadConfig("cassie_announcements.yml", out CassieConfig loadedCassie))
            {
                Cassie = loadedCassie ?? new CassieConfig();
                Cassie.Validate();
                this.TrySaveConfig(Cassie, "cassie_announcements.yml");
            }
        }

        /// <summary>
        /// LabAPI structural entry point substitution for EXILED's OnEnabled.
        /// </summary>
        public override void Enable()
        {
            Singleton = this;

            try
            {
                _libraryLabAPI = new LibraryLabAPI(this);
            }
            catch (Exception ex)
            {
                _libraryLabAPI = null;
                Singleton = null;
                return;
            }

            try
            {
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

                _sanityHandler?.Initialize();
                _lightsourceHandler?.Initialize();
                _audioDirector?.Initialize();

                RegisterEvents();
            }
            catch (Exception)
            {
                throw;
            }
        }

        /// <summary>
        /// LabAPI structural teardown substitution for EXILED's OnDisabled.
        /// </summary>
        public override void Disable()
        {
            _isEventActive = false;

            try { UnregisterEvents(); }
            catch (Exception) { }

            try { _audioDirector?.Dispose(); }
            catch (Exception) { }

            try { _audioManager?.Clean(fullShutdown: true); }
            catch (Exception) { }

            try { _sanityHandler?.Dispose(); }
            catch (Exception) { }

            try { _lightsourceHandler?.Dispose(); }
            catch (Exception) { }

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
        }
    }
}