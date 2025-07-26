using System.ComponentModel;

namespace SCP_575.ConfigObjects
{
    public sealed class BlackoutConfig
    {
        #region General Settings

        /// <summary>
        /// Gets or sets the percentage chance that a round includes SCP-575 blackout events.
        /// </summary>
        [Description("The chance that a Round even has SCP-575 blackouts")]
        public float EventChance
        {
            get => _eventChance;
            set => _eventChance = value < 0f ? 0f : value > 100f ? 100f : value;
        }
        private float _eventChance = 55f;

        /// <summary>
        /// Whether random blackout events should occur.
        /// </summary>
        [Description("Enable or disable randomly timed blackout events.")]
        public bool RandomEvents { get; private set; } = true;

        /// <summary>
        /// Delay before the first blackout event of each round, in seconds.
        /// </summary>
        [Description("Delay before first event of each round.")]
        public float InitialDelay
        {
            get => _initialDelay;
            private set => _initialDelay = value < 0f ? 0f : value;
        }
        private float _initialDelay = 300f;

        /// <summary>
        /// Minimum duration of a blackout event, in seconds.
        /// </summary>
        [Description("Minimum blackout duration in seconds.")]
        public float DurationMin
        {
            get => _durationMin;
            private set => _durationMin = value < 0f ? 0f : value;
        }
        private float _durationMin = 30f;

        /// <summary>
        /// Maximum duration of a blackout event, in seconds.
        /// </summary>
        [Description("Maximum blackout duration in seconds.")]
        public float DurationMax
        {
            get => _durationMax;
            private set => _durationMax = value < 0f ? 0f : value;
        }
        private float _durationMax = 90f;

        /// <summary>
        /// Minimum delay between blackout events, in seconds.
        /// </summary>
        [Description("Minimum delay between events in seconds.")]
        public int DelayMin
        {
            get => _delayMin;
            private set => _delayMin = value < 0 ? 0 : value;
        }
        private int _delayMin = 180;

        /// <summary>
        /// Maximum delay between blackout events, in seconds.
        /// </summary>
        [Description("Maximum delay between events in seconds.")]
        public int DelayMax
        {
            get => _delayMax;
            private set => _delayMax = value < 0 ? 0 : value;
        }
        private int _delayMax = 500;

        /// <summary>
        /// Chance (%) that blackout events occur each round.
        /// </summary>
        [Description("Chance (%) that blackout events occur each round.")]
        public int SpawnChance
        {
            get => _spawnChance;
            private set => _spawnChance = value < 0 ? 0 : value > 100 ? 100 : value;
        }
        private int _spawnChance = 45;

        #endregion

        #region Zone Probabilities

        /// <summary>
        /// Whether a facility-wide blackout occurs if no zones are selected.
        /// </summary>
        [Description("Enable facility-wide blackout if no zones selected.")]
        public bool EnableFacilityBlackout { get; private set; } = true;

        /// <summary>
        /// Chance (%) of a blackout in the Heavy Containment Zone.
        /// </summary>
        [Description("Chance (%) of outage in Heavy Containment Zone.")]
        public int ChanceHeavy
        {
            get => _chanceHeavy;
            set => _chanceHeavy = value < 0 ? 0 : value > 100 ? 100 : value;
        }
        private int _chanceHeavy = 99;

        /// <summary>
        /// Chance (%) of a blackout in the Light Containment Zone.
        /// </summary>
        [Description("Chance (%) of outage in Light Containment Zone.")]
        public int ChanceLight
        {
            get => _chanceLight;
            set => _chanceLight = value < 0 ? 0 : value > 100 ? 100 : value;
        }
        private int _chanceLight = 45;

        /// <summary>
        /// Chance (%) of a blackout in the Entrance Zone.
        /// </summary>
        [Description("Chance (%) of outage in Entrance Zone.")]
        public int ChanceEntrance
        {
            get => _chanceEntrance;
            set => _chanceEntrance = value < 0 ? 0 : value > 100 ? 100 : value;
        }
        private int _chanceEntrance = 65;

        /// <summary>
        /// Chance (%) of a blackout in the Surface Zone.
        /// </summary>
        [Description("Chance (%) of outage in Surface Zone.")]
        public int ChanceSurface
        {
            get => _chanceSurface;
            set => _chanceSurface = value < 0 ? 0 : value > 100 ? 100 : value;
        }
        private int _chanceSurface = 25;

        /// <summary>
        /// Chance (%) of a blackout in an unspecified zone.
        /// </summary>
        [Description("Chance (%) of outage in unspecified zones.")]
        public int ChanceOther
        {
            get => _chanceOther;
            set => _chanceOther = value < 0 ? 0 : value > 100 ? 100 : value;
        }
        private int _chanceOther = 0;

        /// <summary>
        /// Use per-room probability settings instead of per-zone.
        /// </summary>
        [Description("Use per-room chance settings instead of per-zone.")]
        public bool UsePerRoomChances { get; set; } = false;

        #endregion

        #region Facility Effects

        /// <summary>
        /// Disable Tesla gates during blackouts.
        /// </summary>
        [Description("Disable Tesla gates during blackout.")]
        public bool DisableTeslas { get; private set; } = true;

        /// <summary>
        /// Cancel nuke detonation during blackouts.
        /// </summary>
        [Description("Cancel nuke detonation during blackout.")]
        public bool DisableNuke { get; private set; } = true;

        /// <summary>
        /// Flicker lights at blackout start.
        /// </summary>
        [Description("Flicker lights when blackout starts.")]
        public bool FlickerLights { get; private set; } = true;

        /// <summary>
        /// Duration of initial light flickering, in seconds.
        /// </summary>
        [Description("Duration of initial light flickering in seconds.")]
        public float FlickerDuration
        {
            get => _flickerDuration;
            private set => _flickerDuration = value < 0f ? 0f : value;
        }
        private float _flickerDuration = 1.5f;

        /// <summary>
        /// Frequency of light flickering during a blackout.
        /// </summary>
        [Description("Frequency of light flickering.")]
        public float FlickerFrequency
        {
            get => _flickerFrequency;
            set => _flickerFrequency = value < 0f ? 0f : value;
        }
        private float _flickerFrequency = 1.5f;

        /// <summary>
        /// Red channel of lights color during blackout.
        /// </summary>
        [Description("Red channel of lights color during blackout.")]
        public float LightsColorR
        {
            get => _lightsColorR;
            set => _lightsColorR = value < 0f ? 0f : value > 1f ? 1f : value;
        }
        private float _lightsColorR = 0.9f;

        /// <summary>
        /// Green channel of lights color during blackout.
        /// </summary>
        [Description("Green channel of lights color during blackout.")]
        public float LightsColorG
        {
            get => _lightsColorG;
            set => _lightsColorG = value < 0f ? 0f : value > 1f ? 1f : value;
        }
        private float _lightsColorG = 0.05f;

        /// <summary>
        /// Blue channel of lights color during blackout.
        /// </summary>
        [Description("Blue channel of lights color during blackout.")]
        public float LightsColorB
        {
            get => _lightsColorB;
            set => _lightsColorB = value < 0f ? 0f : value > 1f ? 1f : value;
        }
        private float _lightsColorB = 0.2f;

        #endregion
    }
}