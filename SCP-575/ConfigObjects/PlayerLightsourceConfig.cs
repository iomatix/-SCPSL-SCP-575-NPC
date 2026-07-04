using LabApi.Extensions;
using System.ComponentModel;
using Logger = LabApi.Extensions.Misc.iLogger;

namespace SCP_575.ConfigObjects
{
    /// <summary>
    /// Configuration profiles governing player personal light source lifecycles and tactical disruption matrices when targeted by SCP-575.
    /// </summary>
    public sealed class PlayerLightsourceConfig
    {
        #region Serialized Properties
        [Description("Cooldown duration in seconds imposed on personal light sources after sustaining an anomalous attack sequence from SCP-575.")]
        public float KeterLightsourceCooldown { get; set; } = 7.25f;

        [Description("Minimum number of random illumination flickers triggered during an environmental disruption burst.")]
        public int MinFlickerCount { get; set; } = 2;

        [Description("Maximum number of random illumination flickers triggered during an environmental disruption burst.")]
        public int MaxFlickerCount { get; set; } = 9;

        [Description("Minimum chronological lifespan duration of an individual flicker state loop in milliseconds.")]
        public int MinFlickerDurationMs { get; set; } = 850;

        [Description("Maximum chronological lifespan duration of an individual flicker state loop in milliseconds.")]
        public int MaxFlickerDurationMs { get; set; } = 1500;
        #endregion

        #region Validation Engine
        /// <summary>
        /// Validates player light source configuration parameters and applies type-safe fluent bounds adjustments.
        /// </summary>
        public void Validate()
        {
            // --- 1. Cooldown Integrity Safeguard ---
            // Fluent API Upgrade: Enforce non-negative temporal scales cleanly via math limits
            if (KeterLightsourceCooldown < 0f)
            {
                Logger.Warn(nameof(PlayerLightsourceConfig), $"KeterLightsourceCooldown ({KeterLightsourceCooldown}s) cannot evaluate to a negative scale. Normalizing to zero baseline.");
                KeterLightsourceCooldown = 0f;
            }

            // --- 2. Flicker Count Domain Enforcement ---
            // Ensure metrics never drop below 1 unit to insulate core processing routines against dead loops or exceptions
            MinFlickerCount = MinFlickerCount.LimitMin(1);
            MaxFlickerCount = MaxFlickerCount.LimitMin(1);

            if (MinFlickerCount > MaxFlickerCount)
            {
                Logger.Warn(nameof(PlayerLightsourceConfig), $"Flicker iteration count bounds out of sequence: MinFlickerCount ({MinFlickerCount}) was greater than Max ({MaxFlickerCount}). Executing tuple-swap correction...");
                (MinFlickerCount, MaxFlickerCount) = (MaxFlickerCount, MinFlickerCount);
            }

            // --- 3. Flicker Duration Safe Windows (Preventing Thread Freezes) ---
            // A hard minimum threshold of 50ms is mandated to guarantee engine coroutine pipelines avoid sub-frame processor starvation
            MinFlickerDurationMs = MinFlickerDurationMs.LimitMin(50);
            MaxFlickerDurationMs = MaxFlickerDurationMs.LimitMin(50);

            if (MinFlickerDurationMs > MaxFlickerDurationMs)
            {
                Logger.Warn(nameof(PlayerLightsourceConfig), $"Flicker duration bounds out of sequence: MinFlickerDurationMs ({MinFlickerDurationMs}ms) exceeded Max ({MaxFlickerDurationMs}ms). Executing tuple-swap correction...");
                (MinFlickerDurationMs, MaxFlickerDurationMs) = (MaxFlickerDurationMs, MinFlickerDurationMs);
            }

            // Prevent identical zero-range generation anomalies by inserting a healthy 100ms micro-variance envelope track
            if (MinFlickerDurationMs == MaxFlickerDurationMs)
            {
                Logger.Warn(nameof(PlayerLightsourceConfig), "Identical min/max flicker durations encountered. Injected a safe 100ms processing buffer variance to the maximum threshold boundary.");
                MaxFlickerDurationMs += 100;
            }
        }
        #endregion
    }
}