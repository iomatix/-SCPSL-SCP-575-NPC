namespace SCP_575.ConfigObjects
{
    using System.ComponentModel;
    using Exiled.API.Features;
    using UnityEngine;

    public sealed class BlackoutConfig
    {
        #region General Settings

        /// <summary>
        /// Gets or sets the percentage chance that a round includes SCP-575 blackout events.
        /// </summary>
        [Description("The chance that a Round even has SCP-575 blackouts")]
        public float EventChance { get; set; } = 55f;

        /// <summary>
        /// Whether random blackout events should occur.
        /// </summary>
        [Description("Enable or disable randomly timed blackout events.")]
        public bool RandomEvents { get; set; } = true;

        /// <summary>
        /// Delay before the first blackout event of each round, in seconds.
        /// </summary>
        [Description("Delay before first event of each round.")]
        public float InitialDelay { get; set; } = 300f;

        /// <summary>
        /// Minimum duration of a blackout event, in seconds.
        /// </summary>
        [Description("Minimum blackout duration in seconds.")]
        public float DurationMin { get; set; } = 220f;

        /// <summary>
        /// Maximum duration of a blackout event, in seconds.
        /// </summary>
        [Description("Maximum blackout duration in seconds.")]
        public float DurationMax { get; set; } = 620f;

        /// <summary>
        /// Minimum delay between blackout events, in seconds.
        /// </summary>
        [Description("Minimum delay between events in seconds.")]
        public int DelayMin { get; set; } = 340;

        /// <summary>
        /// Maximum delay between blackout events, in seconds.
        /// </summary>
        [Description("Maximum delay between events in seconds.")]
        public int DelayMax { get; set; } = 1080;

        #endregion

        #region Zone Probabilities

        /// <summary>
        /// Whether a facility-wide blackout occurs if no zones are selected.
        /// </summary>
        [Description("Enable facility-wide blackout if no zones selected.")]
        public bool EnableFacilityBlackout { get; set; } = true;

        /// <summary>
        /// Chance (%) of a blackout in the Heavy Containment Zone.
        /// </summary>
        [Description("Chance (%) of outage in Heavy Containment Zone.")]
        public float ChanceHeavy { get; set; } = 85f;

        /// <summary>
        /// Chance (%) of a blackout in the Light Containment Zone.
        /// </summary>
        [Description("Chance (%) of outage in Light Containment Zone.")]
        public float ChanceLight { get; set; } = 35f;

        /// <summary>
        /// Chance (%) of a blackout in the Entrance Zone.
        /// </summary>
        [Description("Chance (%) of outage in Entrance Zone.")]
        public float ChanceEntrance { get; set; } = 65f;

        /// <summary>
        /// Chance (%) of a blackout in the Surface Zone.
        /// </summary>
        [Description("Chance (%) of outage in Surface Zone.")]
        public float ChanceSurface { get; set; } = 15f;

        /// <summary>
        /// Chance (%) of a blackout in an unspecified zone.
        /// </summary>
        [Description("Chance (%) of outage in unspecified zones.")]
        public float ChanceOther { get; set; } = 0f;

        /// <summary>
        /// Probability (0–100%) that connected elevators will be locked down when a room loses power.
        /// </summary>
        [Description("Elevator lockdown probability (%) when a connected room loses power")]
        public float ElevatorLockdownProbability { get; set; } = 35f;

        /// <summary>
        /// Use per-room probability settings instead of per-zone.
        /// </summary>
        [Description("Use per-room chance settings instead of per-zone.")]
        public bool UsePerRoomChances { get; set; } = true;

        #endregion

        #region Facility Effects

        /// <summary>
        /// Disable Tesla gates during blackouts.
        /// </summary>
        [Description("Disable Tesla gates during blackout.")]
        public bool DisableTeslas { get; set; } = true;

        /// <summary>
        /// Cancel nuke detonation during blackouts.
        /// </summary>
        [Description("Cancel nuke detonation during blackout.")]
        public bool DisableNuke { get; set; } = true;

        [Description("If true, activating a generator will make SCP-575 retaliate by aggressively forcing a blackout in that sector and adding a global blackout stack (increasing its rage and damage).")]
        public bool GeneratorActivationRetaliation { get; set; } = true;

        /// <summary>
        /// The duration (in seconds) the generator room remains in darkness during the stabilization phase.
        /// </summary>
        [Description("The duration (in seconds) the generator room remains in darkness during the stabilization phase if retaliation is active.")]
        public float GeneratorStabilizationDuration { get; set; } = 20f;

        /// <summary>
        /// Flicker lights at blackout start.
        /// </summary>
        [Description("Flicker lights when blackout starts.")]
        public bool FlickerLights { get; set; } = true;

        /// <summary>
        /// Duration of initial light flickering, in seconds.
        /// </summary>
        [Description("Duration of initial light flickering in seconds.")]
        public float FlickerDuration { get; set; } = 2.35f;

        /// <summary>
        /// Frequency of light flickering during a blackout.
        /// </summary>
        [Description("Frequency of light flickering.")]
        public float FlickerFrequency { get; set; } = 1.35f;

        /// <summary>
        /// Red channel of lights color during blackout.
        /// </summary>
        [Description("Red channel of lights color during blackout.")]
        public float LightsColorR { get; set; } = 0.9f;

        /// <summary>
        /// Green channel of lights color during blackout.
        /// </summary>
        [Description("Green channel of lights color during blackout.")]
        public float LightsColorG { get; set; } = 0.05f;

        /// <summary>
        /// Blue channel of lights color during blackout.
        /// </summary>
        [Description("Blue channel of lights color during blackout.")]
        public float LightsColorB { get; set; } = 0.2f;

        #endregion

        /// <summary>
        /// Validates the blackout configuration parameters and corrects invalid input.
        /// </summary>
        public void Validate()
        {
            // Validate Durations and Delays
            if (DurationMin < 0f) DurationMin = 0f;
            if (DurationMax < 0f) DurationMax = 0f;

            if (DurationMin > DurationMax)
            {
                float temp = DurationMin;
                DurationMin = DurationMax;
                DurationMax = temp;
                Log.Warn("[BlackoutConfig] DurationMin was greater than DurationMax. Values have been swapped.");
            }

            if (DelayMin < 0) DelayMin = 0;
            if (DelayMax < 0) DelayMax = 0;

            if (DelayMin > DelayMax)
            {
                int temp = DelayMin;
                DelayMin = DelayMax;
                DelayMax = temp;
                Log.Warn("[BlackoutConfig] DelayMin was greater than DelayMax. Values have been swapped.");
            }

            if (InitialDelay < 0f) InitialDelay = 0f;

            // Validate Zone Probabilities (0-100)
            EventChance = Mathf.Clamp(EventChance, 0f, 100f);
            ChanceHeavy = Mathf.Clamp(ChanceHeavy, 0f, 100f);
            ChanceLight = Mathf.Clamp(ChanceLight, 0f, 100f);
            ChanceEntrance = Mathf.Clamp(ChanceEntrance, 0f, 100f);
            ChanceSurface = Mathf.Clamp(ChanceSurface, 0f, 100f);
            ChanceOther = Mathf.Clamp(ChanceOther, 0f, 100f);
            ElevatorLockdownProbability = Mathf.Clamp(ElevatorLockdownProbability, 0f, 100f);

            // Validate Visual Effects
            if (FlickerDuration < 0f) FlickerDuration = 0f;

            if (FlickerFrequency <= 0f)
            {
                FlickerFrequency = 1.5f;
                Log.Warn("[BlackoutConfig] FlickerFrequency must be greater than 0. Resetting to default (1.5f).");
            }

            LightsColorR = Mathf.Clamp(LightsColorR, 0f, 1f);
            LightsColorG = Mathf.Clamp(LightsColorG, 0f, 1f);
            LightsColorB = Mathf.Clamp(LightsColorB, 0f, 1f);
        }
    }
}