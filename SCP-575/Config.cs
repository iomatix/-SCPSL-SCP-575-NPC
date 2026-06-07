namespace SCP_575
{
    using System.ComponentModel;
    using Exiled.API.Features;
    using Exiled.API.Interfaces;
    using SCP_575.ConfigObjects;

    /// <summary>
    /// Configuration settings for the SCP-575 plugin, controlling blackout events,
    /// NPC behavior, player systems, and message hints.
    /// </summary>
    public class Config : IConfig
    {
        #region General Settings

        [Description("Enable or disable SCP-575.")]
        public bool IsEnabled { get; set; } = true;

        [Description("Enable debug logging.")]
        public bool Debug { get; set; } = false;

        #endregion

        #region Blackout Settings

        [Description("Configuration settings for blackout mechanics.")]
        public BlackoutConfig BlackoutConfig { get; set; } = new BlackoutConfig();

        #endregion

        #region NPC Settings

        [Description("Configuration settings for SCP-575 NPC behaviors.")]
        public NpcConfig NpcConfig { get; set; } = new NpcConfig();

        #endregion

        #region Player Settings

        [Description("Sanity system configuration.")]
        public PlayerSanityConfig SanityConfig { get; set; } = new PlayerSanityConfig();

        [Description("Light source system configuration.")]
        public PlayerLightsourceConfig LightsourceConfig { get; set; } = new PlayerLightsourceConfig();

        #endregion

        #region Hint and Message Settings

        [Description("Hints configuration settings.")]
        public HintsConfig HintsConfig { get; set; } = new HintsConfig();

        [Description("Cassie announcement configuration settings.")]
        public CassieConfig CassieConfig { get; set; } = new CassieConfig();

        #endregion

        #region Audio

        [Description("Audio system configuration.")]
        public AudioConfig AudioConfig { get; set; } = new AudioConfig();

        #endregion

        #region Utilities

        [Description("Interval for automatic cleanup of event handlers (seconds).")]
        public float HandlerCleanupInterval { get; set; } = 90f;

        #endregion

        #region Validation

        public void Validate()
        {
            if (HandlerCleanupInterval < 0f)
            {
                Log.Warn("[Config] HandlerCleanupInterval cannot be negative. Resetting to default (90f).");
                HandlerCleanupInterval = 90f;
            }

            // FIXED: Added defensive fallback instantiation pattern during validation loop executions.
            // If the YAML parser deserializes a section as null due to formatting errors or omissions,
            // we force-rebuild the object configuration layout using standard default parameters to eliminate downstream NRE crashes.
            if (AudioConfig == null) AudioConfig = new AudioConfig();
            else AudioConfig.Validate();

            if (BlackoutConfig == null) BlackoutConfig = new BlackoutConfig();
            else BlackoutConfig.Validate();

            if (CassieConfig == null) CassieConfig = new CassieConfig();
            else CassieConfig.Validate();

            if (HintsConfig == null) HintsConfig = new HintsConfig();
            else HintsConfig.Validate();

            if (NpcConfig == null) NpcConfig = new NpcConfig();
            else NpcConfig.Validate();

            if (LightsourceConfig == null) LightsourceConfig = new PlayerLightsourceConfig();
            else LightsourceConfig.Validate();

            if (SanityConfig == null) SanityConfig = new PlayerSanityConfig();
            else SanityConfig.Validate();
        }

        #endregion
    }
}