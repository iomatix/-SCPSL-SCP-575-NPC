namespace SCP_575
{
    using LabApi.Loader.Features.Configuration;
    using System.ComponentModel;
    using Logger = LabApi.Extensions.Misc.iLogger;

    /// <summary>
    /// Master configuration settings for the SCP-575 plugin ecosystem inside LabAPI.
    /// </summary>
    public sealed class Config : LabApiConfig
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

        #region Validation

        /// <summary>
        /// Validates master tracking configurations and forces fail-safe adjustments on invalid values.
        /// </summary>
        public void Validate()
        {
            if (HandlerCleanupInterval < 5f)
            {
                Logger.Warn(nameof(Config), "HandlerCleanupInterval cannot be less than 5 seconds. Resetting to safe baseline (90f).");
                HandlerCleanupInterval = 90f;
            }
        }

        #endregion
    }
}