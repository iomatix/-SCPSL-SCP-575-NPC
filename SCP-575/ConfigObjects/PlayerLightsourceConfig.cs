namespace SCP_575.ConfigObjects
{
    using System.ComponentModel;
    using Exiled.API.Features;

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
            if (KeterLightsourceCooldown < 0f)
            {
                Log.Warn("[PlayerLightsourceConfig] KeterLightsourceCooldown cannot be negative. Resetting to 0.");
                KeterLightsourceCooldown = 0f;
            }

            if (MinFlickerCount < 0) MinFlickerCount = 0;
            if (MaxFlickerCount < 0) MaxFlickerCount = 0;

            if (MinFlickerCount > MaxFlickerCount)
            {
                int temp = MinFlickerCount;
                MinFlickerCount = MaxFlickerCount;
                MaxFlickerCount = temp;
                Log.Warn("[PlayerLightsourceConfig] MinFlickerCount was greater than MaxFlickerCount. Values have been swapped.");
            }

            if (MinFlickerDurationMs < 0) MinFlickerDurationMs = 0;
            if (MaxFlickerDurationMs < 0) MaxFlickerDurationMs = 0;

            if (MinFlickerDurationMs > MaxFlickerDurationMs)
            {
                int temp = MinFlickerDurationMs;
                MinFlickerDurationMs = MaxFlickerDurationMs;
                MaxFlickerDurationMs = temp;
                Log.Warn("[PlayerLightsourceConfig] MinFlickerDurationMs was greater than MaxFlickerDurationMs. Values have been swapped.");
            }
        }
    }
}