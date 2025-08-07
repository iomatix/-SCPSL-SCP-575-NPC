namespace SCP_575.ConfigObjects
{
    using System.ComponentModel;

    public sealed class PlayerLightsourceConfig
    {
        /// <summary>
        /// Gets or sets the cooldown duration (in seconds) for light sources after being hit by SCP-575.
        /// </summary>
        [Description("Cooldown on the light source triggered on hit by SCP-575.")]
        public float KeterLightsourceCooldown
        {
            get => _keterLightsourceCooldown;
            private set => _keterLightsourceCooldown = value < 0f ? 0f : value;
        }
        private float _keterLightsourceCooldown = 7.25f;

        /// <summary>
        /// Gets or sets the minimum number of flickers triggered by SCP-575.
        /// </summary>
        [Description("Minimum number of flickers caused by SCP-575.")]
        public int MinFlickerCount
        {
            get => _minFlickerCount;
            private set => _minFlickerCount = value < 0 ? 0 : value;
        }
        private int _minFlickerCount = 3;


        /// <summary>
        /// Gets or sets the maximum number of flickers triggered by SCP-575.
        /// </summary>
        [Description("Maximum number of flickers caused by SCP-575.")]
        public int MaxFlickerCount
        {
            get => _maxFlickerCount;
            private set => _maxFlickerCount = value < _minFlickerCount ? _minFlickerCount : value;
        }
        private int _maxFlickerCount = 11;

        /// <summary>
        /// Gets or sets the minimum duration of the flicker effect in milliseconds.
        /// </summary>
        [Description("Minimum duration of the flicker effect in milliseconds.")]
        public int MinFlickerDurationMs
        {
            get => _minFlickerDurationMs;
            private set => _minFlickerDurationMs = value < 0 ? 0 : value;
        }
        private int _minFlickerDurationMs = 1500;

        /// <summary>
        /// Gets or sets the maximum duration of the flicker effect in milliseconds.
        /// </summary>
        [Description("Maximum duration of the flicker effect in milliseconds.")]
        public int MaxFlickerDurationMs
        {
            get => _maxFlickerDurationMs;
            private set => _maxFlickerDurationMs = value < _minFlickerDurationMs ? _minFlickerDurationMs : value;
        }
        private int _maxFlickerDurationMs = 2500;
    }

}