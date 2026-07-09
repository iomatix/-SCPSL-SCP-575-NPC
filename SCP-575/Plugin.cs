using LabApi.Extensions;
using LabApi.Extensions.Compatibility;
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

        private LabApi.Events.CustomHandlers.CustomEventsHandler[] _activeHandlers;
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
        public override Version Version => new(13, 1, 0);
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
            Config?.Validate();

            PluginBuilder.Create(this)
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
                ExiledCompatibilityLayer.ExecuteFallback(this);
            }

            Singleton = this;

            try
            {
                PluginBuilder.Create(this)
                    .InitializeModule(() =>
                    {
                        // Action 1: Instantiate core independent logic components
                        _damageSystem = new Scp575DamageSystem(this);
                        _audioManager = new Scp575AudioManager(this);
                        _elevatorHandler = new ElevatorHandler(this);
                        _sanityHandler = new PlayerSanityHandler(this);
                        _audioDirector = new Scp575AudioDirector(this, _audioManager, _sanityHandler);

                        // Action 2: Instantiate structural event proxy-handlers
                        _lifecycleHandler = new LifecycleHandler(this);
                        _generatorHandler = new GeneratorHandler(this);
                        _explosionHandler = new ExplosionHandler(this);
                        _damageHandler = new PlayerDamageHandler(this);
                        _ragdollHandler = new RagdollHandler(this);
                        _lightsourceHandler = new PlayerLightsourceHandler(this);
                        _mapHandler = new MapHandler(this);

                        // Action 3: Cache components to avoid duplicate references on teardown
                        _activeHandlers = new LabApi.Events.CustomHandlers.CustomEventsHandler[]
                        {
                            _lifecycleHandler, _generatorHandler, _explosionHandler, _damageHandler,
                            _ragdollHandler, _lightsourceHandler, _sanityHandler, _mapHandler
                        };

                        // Action 4: Commit internal behavior node composition
                        _npcNode = new NestingNode<Plugin, Methods>(this, plugin => new Methods(plugin));
                    })
                    .InitializeModule(() =>
                    {
                        // Action 5: Wake up specialized worker layers and bind event streams
                        _sanityHandler?.Initialize();
                        _lightsourceHandler?.Initialize();
                        _audioDirector?.Initialize();

                        if (_activeHandlers != null)
                            HandlerExtensions.RegisterAll(_activeHandlers);
                    });
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

            // 1. Centralized event unregistration track
            if (_activeHandlers != null)
            {
                SafeTeardown(() => HandlerExtensions.UnregisterAll(_activeHandlers), "Suppressed event unregistration artifact");
            }

            // 2. Ordered resource/worker disposal cascade
            SafeTeardown(() => _audioDirector?.Dispose());
            SafeTeardown(() => _audioManager?.Clean(fullShutdown: true));
            SafeTeardown(() => _sanityHandler?.Dispose());
            SafeTeardown(() => _lightsourceHandler?.Dispose());

            // 3. Clear system roots to prevent memory leaks across assembly reloads
            ResetSubsystemReferences();

            Logger.Info(nameof(Plugin), $"{Name} framework teardown completed successfully.");
        }

        /// <summary>
        /// Wraps teardown routines into guarded execution blocks to prevent pipeline interruption.
        /// </summary>
        private void SafeTeardown(Action cleanupAction, string errorMessageContext = null)
        {
            try
            {
                cleanupAction();
            }
            catch (Exception ex)
            {
                Logger.Debug(nameof(Plugin), $"{errorMessageContext ?? "Suppressed cleanup exception"}: {ex.Message}", Config.Debug);
            }
        }

        /// <summary>
        /// Flushes all subsystem fields back to factory baseline states.
        /// </summary>
        private void ResetSubsystemReferences()
        {
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
            _activeHandlers = null;
            Singleton = null;
        }
        #endregion
    }
}