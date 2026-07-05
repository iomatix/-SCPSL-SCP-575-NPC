using LabApi.Extensions;
using System.ComponentModel;
using Logger = LabApi.Extensions.Misc.iLogger;

namespace SCP_575.ConfigObjects
{
    /// <summary>
    /// Configuration settings managing the central temporal loops, zone-based outage probabilities, 
    /// interactive environment triggers, and color spectrum rendering for blackout events.
    /// </summary>
    public sealed class BlackoutConfig
    {
        #region General Settings
        [Description("The percentage probability chance (0% - 100%) that a round lifecycle will permit SCP-575 blackout event cascades.")]
        public float EventChance { get; set; } = 55f;

        [Description("Enable or disable randomly timed automated blackout events throughout the round state.")]
        public bool RandomEvents { get; set; } = true;

        [Description("Standby delay interval in seconds executing on round start before the first blackout threat assessment begins.")]
        public float InitialDelay { get; set; } = 300f;

        [Description("Minimum total operational duration window in seconds for an individual blackout event track.")]
        public float DurationMin { get; set; } = 220f;

        [Description("Maximum total operational duration window in seconds for an individual blackout event track.")]
        public float DurationMax { get; set; } = 620f;

        [Description("Minimum legal delay window in seconds enforced between successive blackout event cycles.")]
        public int DelayMin { get; set; } = 340;

        [Description("Maximum legal delay window in seconds enforced between successive blackout event cycles.")]
        public int DelayMax { get; set; } = 1080;
        #endregion

        #region Zone Probabilities
        [Description("Force a unified, facility-wide blackout sequence if all individual sector probability evaluations roll a failure state.")]
        public bool EnableFacilityBlackout { get; set; } = true;

        [Description("Outage probability percentage chance (0% - 100%) calculated per individual room located inside the Heavy Containment Zone.")]
        public float ChanceHeavy { get; set; } = 85f;

        [Description("Outage probability percentage chance (0% - 100%) calculated per individual room located inside the Light Containment Zone.")]
        public float ChanceLight { get; set; } = 35f;

        [Description("Outage probability percentage chance (0% - 100%) calculated per individual room located inside the Entrance Zone.")]
        public float ChanceEntrance { get; set; } = 65f;

        [Description("Outage probability percentage chance (0% - 100%) calculated per individual room mapped onto Surface sector boundaries.")]
        public float ChanceSurface { get; set; } = 15f;

        [Description("Outage probability percentage chance (0% - 100%) calculated per individual room inside unindexed or custom zone spaces.")]
        public float ChanceOther { get; set; } = 0f;

        [Description("If true, the generation engine runs independent probability assessments per room node instead of macro sector zone evaluations.")]
        public bool UsePerRoomChances { get; set; } = true;
        #endregion

        #region Facility Effects & Interactivity
        [Description("Forcibly put all active facility Tesla gates into a safe, completely inactive operational cooldown loop throughout a blackout.")]
        public bool DisableTeslas { get; set; } = true;

        [Description("Forcibly abort and completely cancel ongoing Alpha Warhead detonation countdown timelines if a blackout event triggers.")]
        public bool DisableNuke { get; set; } = true;

        [Description("If true, fully engaging a facility generator provokes immediate SCP-575 retaliation, forcing an isolated blackout loop onto that sector.")]
        public bool GeneratorActivationRetaliation { get; set; } = true;

        [Description("The chronological duration window in seconds that a retaliated generator sector stays locked in pitch darkness before stabilization completes.")]
        public float GeneratorStabilizationDuration { get; set; } = 20f;

        [Description("Trigger a rapid environmental lighting flicker effect across target nodes the exact millisecond a blackout event initiates.")]
        public bool FlickerLights { get; set; } = true;

        [Description("Total duration window in seconds assigned for the initial light flickering execution phase loop.")]
        public float FlickerDuration { get; set; } = 2.35f;

        [Description("Frequency modulation coefficient determining the rapid velocity pacing of the initial light flickering loops.")]
        public float FlickerFrequency { get; set; } = 1.35f;

        [Description("Normalized Red spectrum value channel (0.0 - 1.0) applied to light controllers during an active blackout event track.")]
        public float LightsColorR { get; set; } = 0.9f;

        [Description("Normalized Green spectrum value channel (0.0 - 1.0) applied to light controllers during an active blackout event track.")]
        public float LightsColorG { get; set; } = 0.05f;

        [Description("Normalized Blue spectrum value channel (0.0 - 1.0) applied to light controllers during an active blackout event track.")]
        public float LightsColorB { get; set; } = 0.2f;
        #endregion

        #region Validation Engine
        /// <summary>
        /// Validates blackout timelines, clamps probability matrices via fluent extensions, 
        /// and sanitizes light frequency rendering channels to insulate threads against math anomalies.
        /// </summary>
        public void Validate()
        {
            // --- 1. Timing and Duration Safe Guards ---
            // Fluent API Upgrade: Enforce strict minimum pacing limits inline using math extensions
            DurationMin = DurationMin.LimitMin(5f);

            // Type-Safe Upgrade: Replace legacy temporary assignment loops with pure atomic tuple boundary swaps
            if (DurationMin > DurationMax)
            {
                Logger.Warn(nameof(BlackoutConfig), $"Blackout duration boundaries out of order: DurationMin ({DurationMin}s) was greater than Max ({DurationMax}s). Executing tuple-swap correction...");
                (DurationMin, DurationMax) = (DurationMax, DurationMin);
            }

            // Guard against identical boundaries or sub-zero constraints to insulate safe random tracking generations
            DelayMin = DelayMin.LimitMin(10);

            if (DelayMax <= DelayMin)
            {
                Logger.Warn(nameof(BlackoutConfig), $"DelayMax ({DelayMax}s) must evaluate to a scale strictly greater than DelayMin ({DelayMin}s) to isolate loops against thread choking anomalies. Adjusting window envelope.");
                DelayMax = DelayMin + 30;
            }

            InitialDelay = InitialDelay.LimitMin(0f);

            // --- 2. Probability Bounds Validation ---
            // Fluent API Upgrade: Clamp all chance parameters cleanly using fluent single-precision primitive math limits
            EventChance = EventChance.Clamp(0f, 100f);
            ChanceHeavy = ChanceHeavy.Clamp(0f, 100f);
            ChanceLight = ChanceLight.Clamp(0f, 100f);
            ChanceEntrance = ChanceEntrance.Clamp(0f, 100f);
            ChanceSurface = ChanceSurface.Clamp(0f, 100f);
            ChanceOther = ChanceOther.Clamp(0f, 100f);

            // Structural Integrity Safe Check: If all sub-zone distribution channels are muted and facility-wide fallout is disabled,
            // the plugin runs as complete dead server weight. Intercept execution boundaries and force-revert to preserve stability.
            if (!EnableFacilityBlackout && ChanceHeavy <= 0f && ChanceLight <= 0f && ChanceEntrance <= 0f && ChanceSurface <= 0f && ChanceOther <= 0f)
            {
                Logger.Error(nameof(BlackoutConfig), "Critical Configuration Matrix Flaw: Every localized zone chance evaluates to 0% AND EnableFacilityBlackout is toggled false! Forcibly re-activating EnableFacilityBlackout to prevent dead execution events.");
                EnableFacilityBlackout = true;
            }

            // --- 3. Environmental Interactivity Tuning ---
            GeneratorStabilizationDuration = GeneratorStabilizationDuration.LimitMin(0f);

            // --- 4. Coroutine Render Controls (Flicker Calculations) ---
            FlickerDuration = FlickerDuration.LimitMin(0f);

            // Crucial: Enforce strict minimum frequency limits. Values collapsing below 0.1f induce infinity exceptions during reciprocal loop delays (1/f)
            FlickerFrequency.LimitMin(0.15f);

            // Fluent API Upgrade: Clamp color spectrum arrays inline inside safe byte parameters (0.0 - 1.0)
            LightsColorR = LightsColorR.Clamp(0f, 1f);
            LightsColorG = LightsColorG.Clamp(0f, 1f);
            LightsColorB = LightsColorB.Clamp(0f, 1f);
        }
        #endregion
    }
}