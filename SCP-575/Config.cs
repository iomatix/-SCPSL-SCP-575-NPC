namespace SCP_575
{
    using System.ComponentModel;
    using Exiled.API.Interfaces;
    using SCP_575.ConfigObjects;

    /// <summary>
    /// Configuration settings for the SCP-575 plugin, controlling blackout events,
    /// NPC behavior, player systems, and message hints.
    /// </summary>
    public class Config : IConfig
    {
        #region General Settings

        /// <summary>
        /// Gets or sets a value indicating whether the SCP-575 plugin is enabled.
        /// </summary>
        [Description("Enable or disable SCP-575.")]
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether debug logging is enabled.
        /// </summary>
        [Description("Enable debug logging.")]
        public bool Debug { get; set; } = false;

        #endregion

        #region Blackout Settings

        /// <summary>
        /// Gets or sets the configuration for blackout event mechanics.
        /// </summary>
        [Description("Configuration settings for blackout mechanics.")]
        public BlackoutConfig BlackoutConfig { get; set; } = new BlackoutConfig();

        #endregion

        #region NPC Settings

        /// <summary>
        /// Gets or sets the configuration for SCP-575 NPC behaviors.
        /// </summary>
        [Description("Configuration settings for SCP-575 NPC behaviors.")]
        public NpcConfig NpcConfig { get; set; } = new NpcConfig();

        #endregion

        #region Player Settings

        /// <summary>
        /// Gets or sets the configuration for the player sanity system.
        /// </summary>
        [Description("Sanity system configuration.")]
        public PlayerSanityConfig SanityConfig { get; set; } = new PlayerSanityConfig();

        /// <summary>
        /// Gets or sets the configuration for the player light source system.
        /// </summary>
        [Description("Light source system configuration.")]
        public PlayerLightsourceConfig LightsourceConfig { get; set; } = new PlayerLightsourceConfig();

        #endregion

        #region Hint and Message Settings

        /// <summary>
        /// Gets or sets the configuration for player hints.
        /// </summary>
        [Description("Hints configuration settings.")]
        public HintsConfig HintsConfig { get; set; } = new HintsConfig();

        /// <summary>
        /// Gets or sets the configuration for Cassie announcement messages.
        /// </summary>
        [Description("Cassie announcement configuration settings.")]
        public CassieConfig CassieConfig { get; set; } = new CassieConfig();

        #endregion

        #region Utilities

        /// <summary>
        /// Gets or sets the interval, in seconds, for automatic cleanup of event handlers.
        /// </summary>
        [Description("Interval for automatic cleanup of event handlers (seconds).")]
        public float HandlerCleanupInterval
        {
            get => _handlerCleanupInterval;
            set => _handlerCleanupInterval = value < 0f ? 0f : value;
        }
        private float _handlerCleanupInterval = 90f;

        #endregion
    }
}