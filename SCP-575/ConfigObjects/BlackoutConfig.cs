namespace SCP_575.ConfigObjects
{
    using System.ComponentModel;
    using Exiled.API.Features;
    using UnityEngine;

    public sealed class BlackoutConfig
    {
        #region General Settings

        [Description("The chance that a Round even has SCP-575 blackouts")]
        public float EventChance { get; set; } = 55f;

        [Description("Enable or disable randomly timed blackout events.")]
        public bool RandomEvents { get; set; } = true;

        [Description("Delay before first event of each round.")]
        public float InitialDelay { get; set; } = 300f;

        [Description("Minimum blackout duration in seconds.")]
        public float DurationMin { get; set; } = 220f;

        [Description("Maximum blackout duration in seconds.")]
        public float DurationMax { get; set; } = 620f;

        [Description("Minimum delay between events in seconds.")]
        public int DelayMin { get; set; } = 340;

        [Description("Maximum delay between events in seconds.")]
        public int DelayMax { get; set; } = 1080;

        #endregion

        #region Zone Probabilities

        [Description("Enable facility-wide blackout if no zones selected.")]
        public bool EnableFacilityBlackout { get; set; } = true;

        [Description("Chance (%) of outage in Heavy Containment Zone.")]
        public float ChanceHeavy { get; set; } = 85f;

        [Description("Chance (%) of outage in Light Containment Zone.")]
        public float ChanceLight { get; set; } = 35f;

        [Description("Chance (%) of outage in Entrance Zone.")]
        public float ChanceEntrance { get; set; } = 65f;

        [Description("Chance (%) of outage in Surface Zone.")]
        public float ChanceSurface { get; set; } = 15f;

        [Description("Chance (%) of outage in unspecified zones.")]
        public float ChanceOther { get; set; } = 0f;

        [Description("Elevator lockdown probability (%) when a connected room loses power")]
        public float ElevatorLockdownProbability { get; set; } = 35f;

        [Description("Use per-room chance settings instead of per-zone.")]
        public bool UsePerRoomChances { get; set; } = true;

        #endregion

        #region Facility Effects

        [Description("Disable Tesla gates during blackout.")]
        public bool DisableTeslas { get; set; } = true;

        [Description("Cancel nuke detonation during blackout.")]
        public bool DisableNuke { get; set; } = true;

        [Description("If true, activating a generator will make SCP-575 retaliate by aggressively forcing a blackout in that sector and adding a global blackout stack (increasing its rage and damage).")]
        public bool GeneratorActivationRetaliation { get; set; } = true;

        [Description("The duration (in seconds) the generator room remains in darkness during the stabilization phase if retaliation is active.")]
        public float GeneratorStabilizationDuration { get; set; } = 20f;

        [Description("Flicker lights when blackout starts.")]
        public bool FlickerLights { get; set; } = true;

        [Description("Duration of initial light flickering in seconds.")]
        public float FlickerDuration { get; set; } = 2.35f;

        [Description("Frequency of light flickering.")]
        public float FlickerFrequency { get; set; } = 1.35f;

        [Description("Red channel of lights color during blackout.")]
        public float LightsColorR { get; set; } = 0.9f;

        [Description("Green channel of lights color during blackout.")]
        public float LightsColorG { get; set; } = 0.05f;

        [Description("Blue channel of lights color during blackout.")]
        public float LightsColorB { get; set; } = 0.2f;

        #endregion

        /// <summary>
        /// Validates the blackout configuration parameters and corrects invalid input.
        /// </summary>
        public void Validate()
        {
            // --- 1. Timing and Duration Safe Guards ---
            if (DurationMin < 5f)
            {
                Log.Warn($"[BlackoutConfig] DurationMin ({DurationMin}s) is too low for SCP-575 gameplay pacing. Forcing minimum of 5s.");
                DurationMin = 5f;
            }

            if (DurationMax < DurationMin)
            {
                Log.Warn("[BlackoutConfig] DurationMin was greater than DurationMax. Swapping boundaries.");
                float temp = DurationMin;
                DurationMin = DurationMax;
                DurationMax = temp;
            }

            // Guard against identical boundaries or sub-zero constraints to protect UnityEngine.Random.Range
            if (DelayMin < 10)
            {
                Log.Warn($"[BlackoutConfig] DelayMin ({DelayMin}s) cannot be lower than 10 seconds. Adjusting.");
                DelayMin = 10;
            }

            if (DelayMax <= DelayMin)
            {
                Log.Warn($"[BlackoutConfig] DelayMax ({DelayMax}s) must be strictly greater than DelayMin ({DelayMin}s) to prevent coroutine thread choking. Adjusting.");
                DelayMax = DelayMin + 30; // Safe window offset
            }

            if (InitialDelay < 0f)
            {
                Log.Warn($"[BlackoutConfig] InitialDelay cannot be negative. Resetting to 0f.");
                InitialDelay = 0f;
            }

            // --- 2. Probability Bounds Validation ---
            EventChance = Mathf.Clamp(EventChance, 0f, 100f);
            ChanceHeavy = Mathf.Clamp(ChanceHeavy, 0f, 100f);
            ChanceLight = Mathf.Clamp(ChanceLight, 0f, 100f);
            ChanceEntrance = Mathf.Clamp(ChanceEntrance, 0f, 100f);
            ChanceSurface = Mathf.Clamp(ChanceSurface, 0f, 100f);
            ChanceOther = Mathf.Clamp(ChanceOther, 0f, 100f);
            ElevatorLockdownProbability = Mathf.Clamp(ElevatorLockdownProbability, 0f, 100f);

            // Fail-safe logic check: if all zone probabilities are 0 and facility blackout is disabled, plugin becomes dead weight.
            if (!EnableFacilityBlackout && ChanceHeavy <= 0f && ChanceLight <= 0f && ChanceEntrance <= 0f && ChanceSurface <= 0f && ChanceOther <= 0f)
            {
                Log.Error("[BlackoutConfig] Critical Configuration Flaw: All zone chances are 0% AND EnableFacilityBlackout is false! Forcing EnableFacilityBlackout to TRUE to avoid empty events.");
                EnableFacilityBlackout = true;
            }

            // --- 3. Environmental Interactivity Tuning ---
            if (GeneratorStabilizationDuration < 0f)
            {
                Log.Warn("[BlackoutConfig] GeneratorStabilizationDuration cannot be negative. Forcing 0s.");
                GeneratorStabilizationDuration = 0f;
            }

            // --- 4. Coroutine Render Controls (Flicker & Vertex Shaders) ---
            if (FlickerDuration < 0f)
            {
                Log.Warn("[BlackoutConfig] FlickerDuration cannot be negative. Forcing 0s.");
                FlickerDuration = 0f;
            }

            // Crucial: if frequency is too low or negative, 1/f calculations for coroutine loop pacing will throw infinity anomalies
            if (FlickerFrequency < 0.1f)
            {
                Log.Warn($"[BlackoutConfig] FlickerFrequency ({FlickerFrequency}) is too low and would freeze light loops. Forcing stable minimum (1.0f).");
                FlickerFrequency = 1.0f;
            }

            // Clamp HDR colors safely within normalized float structures for Map Generation Shaders
            LightsColorR = Mathf.Clamp01(LightsColorR);
            LightsColorG = Mathf.Clamp01(LightsColorG);
            LightsColorB = Mathf.Clamp01(LightsColorB);
        }
    }
}