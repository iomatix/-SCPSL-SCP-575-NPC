namespace SCP_575
{
    using SCP_575.ConfigObjects;
    using System.ComponentModel;
    using YamlDotNet.Serialization;
    using Logger = SCP_575.Shared.LibraryLabAPI;

    /// <summary>
    /// Master configuration settings for the SCP-575 plugin ecosystem inside LabAPI.
    /// </summary>
    public sealed class Config
    {
        #region General Settings

        [Description("Enable or disable the SCP-575 plugin infrastructure entirely.")]
        public bool IsEnabled { get; set; } = true;

        [Description("Enable enhanced debug logging statements within the server console.")]
        public bool Debug { get; set; } = false;

        #endregion

        #region Utilities

        [Description("Interval in seconds for automatic execution of internal memory and event handler cleanup loops.")]
        public float HandlerCleanupInterval { get; set; } = 90f;

        #endregion

        #region Backwards Compatibility Redirect Proxies (Ignored by YAML Serializer)

        [YamlIgnore]
        public BlackoutConfig BlackoutConfig => Plugin.Singleton.Blackout;
        [YamlIgnore]
        public FlashlightSpawnConfig FlashlightSpawnConfig => Plugin.Singleton.FlashlightSpawn;
        [YamlIgnore]
        public NpcConfig NpcConfig => Plugin.Singleton.NpcConfig;
        [YamlIgnore]
        public PlayerSanityConfig SanityConfig => Plugin.Singleton.Sanity;
        [YamlIgnore]
        public PlayerLightsourceConfig LightsourceConfig => Plugin.Singleton.LightsourceConfig;
        [YamlIgnore]
        public AudioConfig AudioConfig => Plugin.Singleton.Audio;
        [YamlIgnore]
        public HintsConfig HintsConfig => Plugin.Singleton.Hints;
        [YamlIgnore]
        public CassieConfig CassieConfig => Plugin.Singleton.Cassie;

        #endregion

        #region Validation

        /// <summary>
        /// Validates master tracking configurations and forces fail-safe adjustments on invalid values.
        /// </summary>
        public void Validate()
        {
            if (HandlerCleanupInterval < 5f)
            {
                Logger.LogWarn(nameof(Config), "[Config] HandlerCleanupInterval cannot be less than 5 seconds. Resetting to safe baseline (90f).");
                HandlerCleanupInterval = 90f;
            }
        }

        #endregion
    }
}