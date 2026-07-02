namespace SCP_575
{
    using LabApi.Events.CustomHandlers;
    using LabApi.Loader;
    using SCP_575.ConfigObjects;
    using SCP_575.Handlers;
    using SCP_575.Shared;
    using SCP_575.Shared.Audio;
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Runtime.InteropServices;
    using Logger = SCP_575.Shared.LibraryLabAPI;

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

        private NestingObjects.Npc _npcNestingObj;
        private Scp575DamageSystem _damageSystem;
        private Scp575AudioManager _audioManager;
        private Scp575AudioDirector _audioDirector;
        private LibraryLabAPI _libraryLabAPI;

        private bool _isEventActive = false;

        public static Plugin Singleton { get; private set; }

        private bool _isConfigLoaded = false;

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

        #region Operational API Properties

        public PlayerSanityHandler SanityEventHandler => _sanityHandler;
        public PlayerLightsourceHandler LightsourceHandler => _lightsourceHandler;
        public MapHandler MapHandler => _mapHandler;

        public bool IsEventActive
        {
            get => _isEventActive;
            set => _isEventActive = value;
        }

        public NestingObjects.Npc NpcNestingObj => _npcNestingObj;
        public Scp575DamageSystem DamageSystem => _damageSystem;
        public Scp575AudioManager AudioManager => _audioManager;
        public Scp575AudioDirector AudioDirector => _audioDirector;
        public LibraryLabAPI LibraryLabAPI => _libraryLabAPI;

        // Mandated LabAPI Abstract Overrides
        public override string Author => "iomatix";
        public override string Name => "SCP-575 NPC";
        public override string Description => "Advanced horror pacing sub-drone shadow entity that makes darkness dangerous.";
        public override Version Version => new(12, 0, 0);
        public override Version RequiredApiVersion => new(1, 0, 0);

        #endregion

        /// <summary>
        /// Native LabAPI configuration framework hook. Processes decoupled sub-configs.
        /// </summary>
        public override void LoadConfigs()
        {
            Logger.LogInfo(nameof(LoadConfigs), " started for SCP-575 NPC");

            base.LoadConfigs();
            Config.Validate();

            Audio = new AudioConfig();
            Blackout = new BlackoutConfig();
            FlashlightSpawn = new FlashlightSpawnConfig();
            Npc = new NpcConfig();
            Sanity = new PlayerSanityConfig();
            Lightsource = new PlayerLightsourceConfig();
            Hints = new HintsConfig();
            Cassie = new CassieConfig();

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
                Npc = loadedNpc ?? new NpcConfig();
                Npc.Validate();
                this.TrySaveConfig(Npc, "npc_behavior.yml");
            }

            if (this.TryLoadConfig("sanity_progression.yml", out PlayerSanityConfig loadedSanity))
            {
                Sanity = loadedSanity ?? new PlayerSanityConfig();
                Sanity.Validate();
                this.TrySaveConfig(Sanity, "sanity_progression.yml");
            }

            if (this.TryLoadConfig("player_lightsources.yml", out PlayerLightsourceConfig loadedLights))
            {
                Lightsource = loadedLights ?? new PlayerLightsourceConfig();
                Lightsource.Validate();
                this.TrySaveConfig(Lightsource, "player_lightsources.yml");
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

            _isConfigLoaded = true;
        }

        /// <summary>
        /// LabAPI structural entry point substitution for EXILED's OnEnabled.
        /// </summary>
        public override void Enable()
        {

            // To ensure Exiled compatibility, we will call LoadConfigs() because it hasn't been called.
            if (!_isConfigLoaded)
            {
                Logger.LogWarn(nameof(Enable), "LoadConfigs was not called before Enable(), calling it now for compatibility.");
                TryCreateExiledSymlink();
                LoadConfigs();
            }

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
                _damageSystem = new Scp575DamageSystem(this);
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

                _npcNestingObj = new NestingObjects.Npc(this);

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

            _npcNestingObj = null;
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

        private void TryCreateExiledSymlink()
        {
            try
            {
                DirectoryInfo labApiConfigDir = this.GetConfigDirectory(isGlobal: false);
                string exiledConfigPath = Path.Combine(
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SCP Secret Laboratory", ".EXILED", "Configs"),
                    Name
                );

                if (!Directory.Exists(exiledConfigPath) && Directory.Exists(labApiConfigDir.FullName))
                { 
                    bool success = CreateJunction(exiledConfigPath, labApiConfigDir.FullName);
                    if (success)
                        Logger.LogInfo(nameof(TryCreateExiledSymlink),$"Created junction from Exiled config to LabAPI");
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarn(nameof(TryCreateExiledSymlink),$"Failed to create Exiled junction: {ex.Message}");
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CreateSymbolicLink(
            string lpSymlinkFileName,
            string lpTargetFileName,
            int dwFlags);

        private bool CreateJunction(string junctionPath, string targetPath)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{junctionPath}\" \"{targetPath}\"")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                Process.Start(psi)?.WaitForExit();
                return Directory.Exists(junctionPath);
            }
            catch
            {
                return false;
            }
        }
}