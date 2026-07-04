using LabApi.Loader.Features.Configuration;
using System.ComponentModel;
using Logger = LabApi.Extensions.Misc.iLogger;

namespace SCP_575
{
    /// <summary>
    /// Master configuration settings for the SCP-575 plugin ecosystem inside LabAPI.
    /// Handles main lifecycle switches, debug telemetry toggles, and performance cleanup parameters.
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

        #region Validation Engine
        /// <summary>
        /// Validates master tracking configurations and forces fail-safe adjustments on anomalous input values.
        /// </summary>
        public void Validate()
        {
            // Structural Guard: Validate threshold boundaries using a clear fallback evaluation path
            if (HandlerCleanupInterval < 5f)
            {
                Logger.Warn(nameof(Config), $"HandlerCleanupInterval ({HandlerCleanupInterval}s) dropped below the absolute 5-second minimum threshold. Reverting to safe default factory baseline (90f).");
                HandlerCleanupInterval = 90f;
            }
        }
        #endregion
    }
}