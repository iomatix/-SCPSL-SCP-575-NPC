using LabApi.Extensions;
using LabApi.Extensions.Nesting;
using LabApi.Loader.Features.Plugins;
using SCP_575.Compatibility;
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
        private ExplosionHandler _explosionHandler;
        private PlayerDamageHandler _damageHandler;
        private RagdollHandler _ragdollHandler;
        private PlayerSanityHandler _sanityHandler;
        private PlayerLightsourceHandler _lightsourceHandler;
        private MapHandler _mapHandler;

        private Scp575DamageSystem _damageSystem;
        private Scp575AudioManager _audioManager;
        private Scp575AudioDirector _audioDirector;

        // Generic structural node completely replacing old custom NestingObjects.Npc boilerplate class
        private NestingNode<Plugin, Methods> _npcNode;

        private bool _isEventActive;
        private bool _isConfigLoaded;
        #endregion

        #region Operational API Properties
        /// <summary>
        /// Gets the global thread-safe singleton instance of the plugin context.
        /// </summary>
        public static Plugin Singleton { get; private set; }

        /// <summary>
        /// Gets the contextual event handler tracking player sanity metrics.
        /// </summary>
        public PlayerSanityHandler SanityEventHandler => _sanityHandler;

        /// <summary>
        /// Gets the contextual event handler tracking player light sources.
        /// </summary>
        public PlayerLightsourceHandler LightsourceHandler => _lightsourceHandler;

        /// <summary>
        /// Gets the master map modification handler tracking environmental illumination states.
        /// </summary>
        public MapHandler MapHandler => _mapHandler;

        /// <summary>
        /// Gets or sets a value indicating whether an active anomaly event cycle is currently executing.
        /// </summary>
        public bool IsEventActive
        {
            get => _isEventActive;
            set => _isEventActive = value;
        }

        /// <summary>
        /// Gets the decoupled composition node exposing core NPC execution methods safely.
        /// </summary>
        public NestingNode<Plugin, Methods> NpcNestingObj => _npcNode;

        public Scp575DamageSystem DamageSystem => _damageSystem;
        public Scp575AudioManager AudioManager => _audioManager;
        public Scp575AudioDirector AudioDirector => _audioDirector;

        public override string Author => "iomatix";
        public override string Name => "SCP-575 NPC";
        public override string Description => "Advanced horror pacing sub-drone shadow entity that makes darkness dangerous.";
        public override Version Version => new(12, 0, 0);
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

        #region Plugin Lifecycle Management
        /// <summary>
        /// Native LabAPI configuration framework hook. Processes decoupled sub-configs seamlessly.
        /// </summary>
        public override void LoadConfigs()
        {
            Logger.Info(nameof(Plugin), "Initializing sub-configuration matrix for SCP-575 NPC.");

            base.LoadConfigs();
            Config.Validate();

            // Fluent API Implementation: Manage all sub-configs atomically with absolute type safety
            Audio = this.LoadOrCreateSubConfig<Config, AudioConfig>("audio_settings.yml", config => config.Validate());
            Blackout = this.LoadOrCreateSubConfig<Config, BlackoutConfig>("blackout_engine.yml", config => config.Validate());
            FlashlightSpawn = this.LoadOrCreateSubConfig<Config, FlashlightSpawnConfig>("flashlight_spawning.yml", config => config.Validate());
            Npc = this.LoadOrCreateSubConfig<Config, NpcConfig>("npc_behavior.yml", config => config.Validate());
            Sanity = this.LoadOrCreateSubConfig<Config, PlayerSanityConfig>("sanity_progression.yml", config => config.Validate());
            Lightsource = this.LoadOrCreateSubConfig<Config, PlayerLightsourceConfig>("player_lightsources.yml", config => config.Validate());
            Hints = this.LoadOrCreateSubConfig<Config, HintsConfig>("hints_placement.yml", config => config.Validate());
            Cassie = this.LoadOrCreateSubConfig<Config, CassieConfig>("cassie_announcements.yml", config => config.Validate());

            _isConfigLoaded = true;
        }

        /// <summary>
        /// Instantiates runtime subsystems, registers pipeline hooks, and sets up the anomaly context.
        /// </summary>
        public override void Enable()
        {
            if (!_isConfigLoaded)
            {
                ExiledCompatibilityLayer.ExecuteFallback(this);
            }

            Singleton = this;

            try
            {
                // Instantiate core operational logic tracking pipelines
                _damageSystem = new Scp575DamageSystem(this);
                _audioManager = new Scp575AudioManager(this);
                _sanityHandler = new PlayerSanityHandler(this);
                _audioDirector = new Scp575AudioDirector(this, _audioManager, _sanityHandler);

                // Initialize runtime event handling proxies
                _lifecycleHandler = new LifecycleHandler(this);
                _generatorHandler = new GeneratorHandler(this);
                _explosionHandler = new ExplosionHandler(this);
                _damageHandler = new PlayerDamageHandler(this);
                _ragdollHandler = new RagdollHandler(this);
                _lightsourceHandler = new PlayerLightsourceHandler(this);
                _mapHandler = new MapHandler(this);

                // Fluent API Upgrade: Bind the logic layer safely without custom boilerplate files
                _npcNode = new NestingNode<Plugin, Methods>(this, plugin => new Methods(plugin));

                // Wake up complex nested tracking loops safely
                _sanityHandler.Initialize();
                _lightsourceHandler.Initialize();
                _audioDirector.Initialize();

                // Fluent API Upgrade: Atomic bulk event handler subscription registration via collection extensions
                HandlerExtensions.RegisterAll(
                    _lifecycleHandler, _generatorHandler, _explosionHandler, _damageHandler,
                    _ragdollHandler, _lightsourceHandler, _sanityHandler, _mapHandler
                );

                Logger.Info(nameof(Plugin), "SCP-575 runtime infrastructure successfully established and online.");
            }
            catch (Exception ex)
            {
                Logger.Error(nameof(Plugin), $"Critical failure during initialization cascade: {ex.Message}");
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
                // Atomic mass unregistration pattern safeguarding against leaked event handlers
                HandlerExtensions.UnregisterAll(
                    _lifecycleHandler, _generatorHandler, _explosionHandler, _damageHandler,
                    _ragdollHandler, _lightsourceHandler, _sanityHandler, _mapHandler
                );
            }
            catch (Exception ex) { Logger.Debug(nameof(Plugin), $"Suppressed event unregistration artifact: {ex.Message}", Config.Debug); }

            // Gracefully wind down independent tracking sub-modules defensively
            try { _audioDirector?.Dispose(); } catch (Exception ex) { Logger.Debug(nameof(Plugin), ex.Message, Config.Debug); }
            try { _audioManager?.Clean(fullShutdown: true); } catch (Exception ex) { Logger.Debug(nameof(Plugin), ex.Message, Config.Debug); }
            try { _sanityHandler?.Dispose(); } catch (Exception ex) { Logger.Debug(nameof(Plugin), ex.Message, Config.Debug); }
            try { _lightsourceHandler?.Dispose(); } catch (Exception ex) { Logger.Debug(nameof(Plugin), ex.Message, Config.Debug); }

            // Break reference maps down to let garbage collectors reclaim allocations cleanly
            _lightsourceHandler = null;
            _sanityHandler = null;
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

            Logger.Info(nameof(Plugin), "SCP-575 framework teardown completed successfully.");
        }
        #endregion
    }
}