using LabApi.Extensions;
using LabApi.Extensions.Nesting;
using LabApi.Extensions.Plugin;
using LabApi.Loader.Features.Plugins;
using SCP_575.ConfigObjects;
using SCP_575.Handlers;
using SCP_575.Npc;
using SCP_575.Shared;
using SCP_575.Shared.Audio;
using System;
using Logger = LabApi.Extensions.Misc.iLogger;

namespace SCP_575
{
    /// <summary>
    /// Central initialization bootstrap layer for the SCP-575 plugin ecosystem inside LabAPI.
    /// Deploys fluent builder pipelines and generic composition nodes to guarantee zero-boilerplate execution.
    /// </summary>
    public class Plugin : Plugin<Config>
    {
        #region Private Subsystem Handlers
        private LifecycleHandler _lifecycleHandler;
        private GeneratorHandler _generatorHandler;
        private ElevatorHandler _elevatorHandler;
        private ExplosionHandler _explosionHandler;
        private PlayerDamageHandler _damageHandler;
        private RagdollHandler _ragdollHandler;
        private PlayerSanityHandler _sanityHandler;
        private PlayerLightsourceHandler _lightsourceHandler;
        private MapHandler _mapHandler;

        private Scp575DamageSystem _damageSystem;
        private Scp575AudioManager _audioManager;
        private Scp575AudioDirector _audioDirector;

        private NestingNode<Plugin, Methods> _npcNode;

        private bool _isEventActive;
        private bool _isConfigLoaded;
        #endregion

        #region Operational API Properties
        /// <summary>
        /// Gets the global thread-safe singleton instance of the plugin context.
        /// </summary>
        public static Plugin Singleton { get; private set; }

        public PlayerSanityHandler SanityHandler => _sanityHandler;
        public PlayerLightsourceHandler LightsourceHandler => _lightsourceHandler;
        public LifecycleHandler LifecycleHandler => _lifecycleHandler;
        public GeneratorHandler GeneratorHandler => _generatorHandler;
        public ElevatorHandler ElevatorHandler => _elevatorHandler;
        public ExplosionHandler ExplosionHandler => _explosionHandler;
        public PlayerDamageHandler DamageHandler => _damageHandler;
        public RagdollHandler RagdollHandler => _ragdollHandler;
        public MapHandler MapHandler => _mapHandler;

        public bool IsEventActive
        {
            get => _isEventActive;
            set => _isEventActive = value;
        }

        public NestingNode<Plugin, Methods> NpcNestingObj => _npcNode;
        public Methods NpcLogic => _npcNode.Logic;
        public Scp575DamageSystem DamageSystem => _damageSystem;
        public Scp575AudioManager AudioManager => _audioManager;
        public Scp575AudioDirector AudioDirector => _audioDirector;

        public override string Author => "iomatix";
        public override string Name => "SCP-575 NPC";
        public override string Description => "Advanced horror pacing sub-drone shadow entity that makes darkness dangerous.";
        public override Version Version => new(13, 0, 0);
        public override Version RequiredApiVersion => new(1, 0, 0);
        #endregion

        #region Independent Sub-Configurations
        public AudioConfig Audio { get; private set; }
        public BlackoutConfig Blackout { get; private set; }
        public FlashlightSpawnConfig FlashlightSpawn { get; private set; }
        public NpcConfig Npc { get; private set; }
        public PlayerSanityConfig Sanity { get; private set; }
        public PlayerLightsourceConfig Lightsource { get; private set; }
        public HintsConfig Hints { get; private set; }
        public CassieConfig Cassie { get; private set; }
        #endregion

        public bool Debug => Config.Debug;

        #region Plugin Lifecycle Management
        /// <summary>
        /// Native LabAPI configuration framework hook. Processes decoupled sub-configs seamlessly using the builder.
        /// </summary>
        public override void LoadConfigs()
        {
            Logger.Info(nameof(Plugin), "Initializing sub-configuration matrix for SCP-575 NPC.");

            base.LoadConfigs();
            Config.Validate();

            // Fluent API Implementation: Chain-load all independent yml sub-configs using your actual BindSubConfig engine
            new PluginBuilder<Config>(this)
                .BindSubConfig<AudioConfig>("audio_settings.yml", cfg => Audio = cfg, cfg => cfg.Validate())
                .BindSubConfig<BlackoutConfig>("blackout_engine.yml", cfg => Blackout = cfg, cfg => cfg.Validate())
                .BindSubConfig<FlashlightSpawnConfig>("flashlight_spawning.yml", cfg => FlashlightSpawn = cfg, cfg => cfg.Validate())
                .BindSubConfig<NpcConfig>("npc_behavior.yml", cfg => Npc = cfg, cfg => cfg.Validate())
                .BindSubConfig<PlayerSanityConfig>("sanity_progression.yml", cfg => Sanity = cfg, cfg => cfg.Validate())
                .BindSubConfig<PlayerLightsourceConfig>("player_lightsources.yml", cfg => Lightsource = cfg, cfg => cfg.Validate())
                .BindSubConfig<HintsConfig>("hints_placement.yml", cfg => Hints = cfg, cfg => cfg.Validate())
                .BindSubConfig<CassieConfig>("cassie_announcements.yml", cfg => Cassie = cfg, cfg => cfg.Validate());

            _isConfigLoaded = true;
        }

        /// <summary>
        /// Instantiates runtime subsystems, registers pipeline hooks, and sets up the anomaly context.
        /// </summary>
        public override void Enable()
        {
            if (!_isConfigLoaded)
            {
                LoadConfigs();
            }

            Singleton = this;

            try
            {
                // Fluent API Implementation: Utilize continuous InitializeModule pipelines sequentially to enforce structural separation
                new PluginBuilder<Config>(this)
                    .InitializeModule(() =>
                    {
                        // Step 1: Allocate operational logic processors maintaining concrete injection order
                        _damageSystem = new Scp575DamageSystem(this);
                        _audioManager = new Scp575AudioManager(this);
                        _elevatorHandler = new ElevatorHandler(this);
                        _sanityHandler = new PlayerSanityHandler(this);
                        _audioDirector = new Scp575AudioDirector(this, _audioManager, _sanityHandler);

                        // Step 2: Allocate proxies to capture structural game events
                        _lifecycleHandler = new LifecycleHandler(this);
                        _generatorHandler = new GeneratorHandler(this);
                        _explosionHandler = new ExplosionHandler(this);
                        _damageHandler = new PlayerDamageHandler(this);
                        _ragdollHandler = new RagdollHandler(this);
                        _lightsourceHandler = new PlayerLightsourceHandler(this);
                        _mapHandler = new MapHandler(this);

                        // Step 3: Attach the core NPC execution layer to the generic composition nesting node
                        _npcNode = new NestingNode<Plugin, Methods>(this, plugin => new Methods(plugin));
                    })
                    .InitializeModule(() =>
                    {
                        // Step 4: Awaken background processing threads, registries, and visual directors safely
                        _sanityHandler?.Initialize();
                        _lightsourceHandler?.Initialize();
                        _audioDirector?.Initialize();
                    })
                    .InitializeModule(() =>
                    {
                        // Step 5: Route proxies directly into the central event engine using atomic array subscription extensions
                        HandlerExtensions.RegisterAll(
                            _lifecycleHandler, _generatorHandler, _explosionHandler, _damageHandler,
                            _ragdollHandler, _lightsourceHandler, _sanityHandler, _mapHandler
                        );
                    });

                Logger.Info(nameof(Plugin), $"{Name} v{Version} - master architecture successfully generated and verified online via Fluent Builder.");
            }
            catch (Exception ex)
            {
                Logger.Error(nameof(Plugin), $"Critical failure caught during fluent module build cascade: {ex.Message}");
                Disable();
                throw;
            }
        }

        /// <summary>
        /// Discharges active streaming references, unbinds system events, and purges the operational singleton context.
        /// </summary>
        public override void Disable()
        {
            _isEventActive = false;

            try
            {
                HandlerExtensions.UnregisterAll(
                    _lifecycleHandler, _generatorHandler, _explosionHandler, _damageHandler,
                    _ragdollHandler, _lightsourceHandler, _sanityHandler, _mapHandler
                );
            }
            catch (Exception ex) { Logger.Debug(nameof(Plugin), $"Suppressed event unregistration artifact: {ex.Message}", Config.Debug); }

            try { _audioDirector?.Dispose(); } catch (Exception ex) { Logger.Debug(nameof(Plugin), ex.Message, Config.Debug); }
            try { _audioManager?.Clean(fullShutdown: true); } catch (Exception ex) { Logger.Debug(nameof(Plugin), ex.Message, Config.Debug); }
            try { _sanityHandler?.Dispose(); } catch (Exception ex) { Logger.Debug(nameof(Plugin), ex.Message, Config.Debug); }
            try { _lightsourceHandler?.Dispose(); } catch (Exception ex) { Logger.Debug(nameof(Plugin), ex.Message, Config.Debug); }

            _lightsourceHandler = null;
            _sanityHandler = null;
            _elevatorHandler = null;
            _ragdollHandler = null;
            _damageHandler = null;
            _explosionHandler = null;
            _generatorHandler = null;
            _lifecycleHandler = null;
            _mapHandler = null;

            _damageSystem = null;
            _audioDirector = null;
            _audioManager = null;
            _npcNode = null;
            Singleton = null;

            Logger.Info(nameof(Plugin), $"{Name} framework teardown completed successfully.");
        }
        #endregion
    }
}