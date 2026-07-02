namespace SCP_575.ConfigObjects
{
    using System;
    using System.ComponentModel;
    using Exiled.API.Features;
    using UnityEngine;

    public sealed class PlayerLightsourceConfig
    {
        /// <summary>
        /// Gets or sets the cooldown duration (in seconds) for light sources after being hit by SCP-575.
        /// </summary>
        [Description("Cooldown on the light source triggered on hit by SCP-575.")]
        public float KeterLightsourceCooldown { get; set; } = 7.25f;

        /// <summary>
        /// Gets or sets the minimum number of flickers triggered by SCP-575.
        /// </summary>
        [Description("Minimum number of flickers caused by SCP-575.")]
        public int MinFlickerCount { get; set; } = 2;

        /// <summary>
        /// Gets or sets the maximum number of flickers triggered by SCP-575.
        /// </summary>
        [Description("Maximum number of flickers caused by SCP-575.")]
        public int MaxFlickerCount { get; set; } = 9;

        /// <summary>
        /// Gets or sets the minimum duration of the flicker effect in milliseconds.
        /// </summary>
        [Description("Minimum duration of the flicker effect in milliseconds.")]
        public int MinFlickerDurationMs { get; set; } = 850;

        /// <summary>
        /// Gets or sets the maximum duration of the flicker effect in milliseconds.
        /// </summary>
        [Description("Maximum duration of the flicker effect in milliseconds.")]
        public int MaxFlickerDurationMs { get; set; } = 1500;

        /// <summary>
        /// Validates the player lightsource configuration parameters and corrects invalid input.
        /// </summary>
        public void Validate()
        {
            // --- 1. Cooldown Integrity Safeguard ---
            if (KeterLightsourceCooldown < 0f)
            {
                Log.Warn("[PlayerLightsourceConfig] KeterLightsourceCooldown cannot be negative. Resetting to 0f.");
                KeterLightsourceCooldown = 0f;
            }

            // --- 2. Flicker Count Domain Enforcement ---
            // Ensure values never drop below 1 to prevent inactive loop states or exceptions in game logic
            MinFlickerCount = Mathf.Max(1, MinFlickerCount);
            MaxFlickerCount = Mathf.Max(1, MaxFlickerCount);

            if (MinFlickerCount > MaxFlickerCount)
            {
                Log.Warn("[PlayerLightsourceConfig] MinFlickerCount was greater than MaxFlickerCount. Swapping boundaries.");
                (MinFlickerCount, MaxFlickerCount) = (MaxFlickerCount, MinFlickerCount);
            }

            // --- 3. Flicker Duration Safe Windows (Preventing Thread Freezes) ---
            // A minimal delay window (e.g., 50ms) is strictly required to prevent sub-frame coroutine execution starvation
            MinFlickerDurationMs = Mathf.Max(50, MinFlickerDurationMs);
            MaxFlickerDurationMs = Mathf.Max(50, MaxFlickerDurationMs);

            if (MinFlickerDurationMs > MaxFlickerDurationMs)
            {
                Log.Warn("[PlayerLightsourceConfig] MinFlickerDurationMs was greater than MaxFlickerDurationMs. Swapping boundaries.");
                (MinFlickerDurationMs, MaxFlickerDurationMs) = (MaxFlickerDurationMs, MinFlickerDurationMs);
            }

            // Prevent identical zero-range anomalies by providing a healthy micro-variance window
            if (MinFlickerDurationMs == MaxFlickerDurationMs)
            {
                MaxFlickerDurationMs += 100;
            }
        }
    }
}