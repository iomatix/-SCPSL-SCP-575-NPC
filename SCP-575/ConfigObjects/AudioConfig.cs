using LabApi.Extensions;
using System;
using System.ComponentModel;
using Logger = LabApi.Extensions.Misc.iLogger;

namespace SCP_575.ConfigObjects
{
    /// <summary>
    /// Configuration model governing the acoustic prioritization, digital audio signal processing (DSP) pacing, 
    /// dynamic tension thresholds, and 3D spatial orbit coordinate vectors for the SCP-575 environment.
    /// </summary>
    public sealed class AudioConfig
    {
        #region Baseline Engine Options
        [Description("The cooldown interval in seconds for global scream audio playback streams. Ensures pacing control.")]
        public float GlobalScreamCooldown { get; set; } = 75f;

        [Description("The default duration track in seconds allocated for localized audio fade-in and fade-out DSP processing windows.")]
        public float DefaultFadeDuration { get; set; } = 1f;
        #endregion

        #region Tension Pacing Dynamics
        [Description("The modifier scaling factor applied to sanity loss metrics when dynamic horror tension states are engaged.")]
        public float TensionSanityRiskMultiplier { get; set; } = 3.5f;

        [Description("The flat value decay scale applied passively to player focus tracking per second during high tension tracks.")]
        public float TensionPassiveDecayRate { get; set; } = 2.5f;

        [Description("The lower boundary sanity percentage (0 - 100) required to initialize adaptive background tension audio states.")]
        public float TensionTriggerMinThreshold { get; set; } = 45.0f;

        [Description("The upper boundary sanity percentage (0 - 100) required to max out adaptive background tension audio states.")]
        public float TensionTriggerMaxThreshold { get; set; } = 85.0f;
        #endregion

        #region Neurological Integrity Thresholds (Sanity Milestones)
        [Description("Sanity percentage milestone floor below which the continuous claustrophobic panic drone looping track becomes audible.")]
        public float PanicDroneSanityThreshold { get; set; } = 22.0f;

        [Description("The interpolation timeline duration in seconds allocated to fade in the panic drone loop when crossing the threshold.")]
        public float PanicDroneFadeInDuration { get; set; } = 3.5f;

        [Description("Critical sanity percentage milestone floor (Tier 4) that triggers high-impact structural shock stinger cues.")]
        public float Tier4ShockStingerThreshold { get; set; } = 10.0f;

        [Description("The temporal duration track in seconds during which a player remains auditorily deafened post-shock-stinger impact.")]
        public float ShockStingerDeafenDuration { get; set; } = 0.75f;

        [Description("Sanity percentage milestone floor (Tier 3) governing the activation cascade of psychotic ambient whispering tracks.")]
        public float Tier3PsychoticWhispersThreshold { get; set; } = 25.0f;

        [Description("Sanity percentage milestone floor (Tier 2) governing the activation cascade of low-intensity disturbed whispering tracks.")]
        public float Tier2DisturbedWhispersThreshold { get; set; } = 55.0f;
        #endregion

        #region Acoustic Suppression & Mix Headroom
        [Description("The timeline duration window in seconds during which environment ambient tracks are ducked when a player sustains direct hit damage.")]
        public float DirectDamageSuppressionDuration { get; set; } = 3.5f;

        [Description("The spatial physical radius envelope in meters within which explosive detonations aggressively duck ongoing audio layers.")]
        public float ExplosionSuppressionRadius { get; set; } = 28.0f;

        [Description("The timeline duration window in seconds during which ambient layers stay suppressed post-explosion shockwave impact.")]
        public float ExplosionSuppressionDuration { get; set; } = 5.0f;

        [Description("The local cooldown gate in seconds separating consecutive procedural combat stinger cues on the same target space.")]
        public float CombatStingerCooldown { get; set; } = 1.6f;
        #endregion

        #region Spatial Coordinates & Orbit Vectors
        [Description("Maximum radius boundary in meters for procedural generic combat stinger spatial point sound allocations.")]
        public float StingerMaxRadius { get; set; } = 2.2f;

        [Description("Minimum radius boundary in meters for procedural generic combat stinger spatial point sound allocations.")]
        public float StingerMinRadius { get; set; } = 0.5f;

        [Description("Angular velocity coefficient tracking the rotational translation tracking speed of generic combat stingers.")]
        public float StingerAngularSpeed { get; set; } = 2.5f;

        [Description("Maximum orbital radius in meters for high-intensity blackout scream spatial tracking sources.")]
        public float BlackoutScreamMaxRadius { get; set; } = 5.5f;

        [Description("Minimum orbital radius in meters for high-intensity blackout scream spatial tracking sources.")]
        public float BlackoutScreamMinRadius { get; set; } = 0.6f;

        [Description("Angular velocity coefficient tracking the rotational translation tracking speed of blackout screams.")]
        public float BlackoutScreamAngularSpeed { get; set; } = 3.4f;

        [Description("Linear translation approach velocity tracking how fast a blackout scream source closes the gap toward the subject coordinates.")]
        public float BlackoutScreamApproachSpeed { get; set; } = 1.8f;

        [Description("Maximum orbital tracking radius in meters allocated to standard entity anomaly hunting breath sources.")]
        public float HunterBreathMaxRadius { get; set; } = 1.45f;

        [Description("Minimum orbital tracking radius in meters allocated to standard entity anomaly hunting breath sources.")]
        public float HunterBreathMinRadius { get; set; } = 0.35f;

        [Description("Angular velocity coefficient tracking the rotational translation tracking speed of hunting breath nodes.")]
        public float HunterBreathAngularSpeed { get; set; } = 1.15f;

        [Description("Linear translation approach velocity tracking how fast a hunting breath node tracks directly toward targeted positions.")]
        public float HunterBreathApproachSpeed { get; set; } = 1.95f;

        [Description("Maximum orbital tracking radius in meters mapped to low-threat environmental explosion acoustic signatures.")]
        public float HelpfulExplosionMaxRadius { get; set; } = 9.0f;

        [Description("Minimum orbital tracking radius in meters mapped to low-threat environmental explosion acoustic signatures.")]
        public float HelpfulExplosionMinRadius { get; set; } = 0.5f;

        [Description("Angular velocity coefficient tracking the rotational tracking velocity of low-threat explosion signatures.")]
        public float HelpfulExplosionAngularSpeed { get; set; } = 3.8f;

        [Description("Linear translation approach velocity tracking how fast low-threat explosion signatures translate across audio channels.")]
        public float HelpfulExplosionApproachSpeed { get; set; } = 2.5f;

        [Description("Maximum orbital tracking radius in meters mapped to high-threat tactical or structural explosion signatures.")]
        public float DangerousExplosionMaxRadius { get; set; } = 12.0f;

        [Description("Minimum orbital tracking radius in meters mapped to high-threat tactical or structural explosion signatures.")]
        public float DangerousExplosionMinRadius { get; set; } = 1.0f;

        [Description("Angular velocity coefficient tracking the rotational tracking velocity of high-threat explosion signatures.")]
        public float DangerousExplosionAngularSpeed { get; set; } = 1.2f;

        [Description("Linear translation approach velocity tracking how fast high-threat explosion signatures translate across audio channels.")]
        public float DangerousExplosionApproachSpeed { get; set; } = 1.4f;

        [Description("Maximum orbital space radius in meters allocated to active tactical facility generator humming loops.")]
        public float GeneratorMaxRadius { get; set; } = 7.5f;

        [Description("Minimum orbital space radius in meters allocated to active tactical facility generator humming loops.")]
        public float GeneratorMinRadius { get; set; } = 1.2f;

        [Description("Angular velocity coefficient tracking the tracking rotational velocity of facility generator humming loops.")]
        public float GeneratorAngularSpeed { get; set; } = 2.4f;

        [Description("Linear translation approach velocity tracking how fast generator hum frequencies travel through local acoustic spaces.")]
        public float GeneratorApproachSpeed { get; set; } = 2.8f;

        [Description("Maximum orbital tracking space in meters assigned to flickering light emission micro-breathing cues.")]
        public float FlickerBreathMaxRadius { get; set; } = 2.2f;

        [Description("Minimum orbital tracking space in meters assigned to flickering light emission micro-breathing cues.")]
        public float FlickerBreathMinRadius { get; set; } = 0.4f;

        [Description("Angular velocity coefficient tracking the tracking rotational velocity of flickering micro-breathing loops.")]
        public float FlickerBreathAngularSpeed { get; set; } = 2.8f;

        [Description("Linear translation approach velocity tracking how fast flickering micro-breathing loops expand or contract spatial fields.")]
        public float FlickerBreathApproachSpeed { get; set; } = 1.6f;

        [Description("Maximum orbital tracking space in meters assigned to mechanical lighting ballast flicker-clicking relays.")]
        public float FlickerClickingMaxRadius { get; set; } = 3.8f;

        [Description("Minimum orbital tracking space in meters assigned to mechanical lighting ballast flicker-clicking relays.")]
        public float FlickerClickingMinRadius { get; set; } = 0.7f;

        [Description("Angular velocity coefficient tracking the tracking rotational velocity of ballast flicker-clicking relays.")]
        public float FlickerClickingAngularSpeed { get; set; } = 4.5f;

        [Description("Linear translation approach velocity tracking how fast ballast flicker-clicking updates propagate across the listener vectors.")]
        public float FlickerClickingApproachSpeed { get; set; } = 2.2f;

        [Description("Maximum orbital spatial radius in meters tracked over generated subject ragdoll skeletal positions.")]
        public float RagdollMaxRadius { get; set; } = 2.5f;

        [Description("Minimum orbital spatial radius in meters tracked over generated subject ragdoll skeletal positions.")]
        public float RagdollMinRadius { get; set; } = 0.45f;

        [Description("Angular velocity coefficient tracking the rotational audio translation speed circling a physical ragdoll mesh.")]
        public float RagdollAngularSpeed { get; set; } = 2.15f;

        [Description("Linear translation approach velocity tracking how fast ragdoll positional audio nodes align to target transformations.")]
        public float RagdollApproachSpeed { get; set; } = 3.25f;

        [Description("Vertical altitude translation vector offset in meters applied to acoustic coordinates relative to ragdoll floor roots.")]
        public float RagdollHeightOffset { get; set; } = 0.15f;
        #endregion

        #region Validation Engine
        /// <summary>
        /// Validates mechanical audio parameters, resolves contiguous threshold sequences, 
        /// and runs matrix checks across 3D orbit spaces using fluent primitive extensions.
        /// </summary>
        public void Validate()
        {
            // --- 1. Baseline Engine Options ---
            if (GlobalScreamCooldown <= 0f)
                GlobalScreamCooldown = 75f;

            DefaultFadeDuration = DefaultFadeDuration.LimitMin(0f);

            // --- 2. Tension Pacing Dynamics ---
            TensionSanityRiskMultiplier = TensionSanityRiskMultiplier.LimitMin(0f);
            TensionPassiveDecayRate = TensionPassiveDecayRate.LimitMin(0f);

            // Fluent API Upgrade: Execute clean, variable-less atomic tuple swapping on reversed boundary entries
            if (TensionTriggerMinThreshold > TensionTriggerMaxThreshold)
            {
                Logger.Warn(nameof(AudioConfig), $"Tension parameter bounds out of sequence: TensionTriggerMinThreshold ({TensionTriggerMinThreshold}) exceeded Max ({TensionTriggerMaxThreshold}). Executing tuple-swap correction...");
                (TensionTriggerMinThreshold, TensionTriggerMaxThreshold) = (TensionTriggerMaxThreshold, TensionTriggerMinThreshold);
            }
            TensionTriggerMinThreshold = TensionTriggerMinThreshold.Clamp(0f, 100f);
            TensionTriggerMaxThreshold = TensionTriggerMaxThreshold.Clamp(0f, 100f);

            // --- 3. Neurological Integrity Thresholds ---
            Tier4ShockStingerThreshold = Tier4ShockStingerThreshold.Clamp(0f, 100f);
            Tier3PsychoticWhispersThreshold = Tier3PsychoticWhispersThreshold.Clamp(0f, 100f);
            Tier2DisturbedWhispersThreshold = Tier2DisturbedWhispersThreshold.Clamp(0f, 100f);
            PanicDroneSanityThreshold = PanicDroneSanityThreshold.Clamp(0f, 100f);

            // Enforce safe contiguous downward progression steps between horror tiers to preserve tracking continuity
            if (Tier4ShockStingerThreshold >= Tier3PsychoticWhispersThreshold)
            {
                Logger.Warn(nameof(AudioConfig), "Neurological milestones overlap: Tier 4 shock threshold climbed above Tier 3 whispers. Forcing safe step delta.");
                Tier4ShockStingerThreshold = (Tier3PsychoticWhispersThreshold - 5f).LimitMin(0f);
            }

            if (Tier3PsychoticWhispersThreshold >= Tier2DisturbedWhispersThreshold)
            {
                Logger.Warn(nameof(AudioConfig), "Neurological milestones overlap: Tier 3 psychotic threshold climbed above Tier 2 disturbed whispers. Forcing safe step delta.");
                Tier3PsychoticWhispersThreshold = (Tier2DisturbedWhispersThreshold - 5f).LimitMin(Tier4ShockStingerThreshold + 5f);
            }

            PanicDroneFadeInDuration = PanicDroneFadeInDuration.LimitMin(0f);
            ShockStingerDeafenDuration = ShockStingerDeafenDuration.LimitMin(0f);

            // --- 4. Acoustic Suppression & Mix Headroom ---
            DirectDamageSuppressionDuration = DirectDamageSuppressionDuration.LimitMin(0f);
            ExplosionSuppressionRadius = ExplosionSuppressionRadius.LimitMin(0f);
            ExplosionSuppressionDuration = ExplosionSuppressionDuration.LimitMin(0f);
            CombatStingerCooldown = CombatStingerCooldown.LimitMin(0f);

            // --- 5. Spatial Coordinates & Orbit Vectors (Fluent Matrix Pipeline Extraction) ---
            (StingerMinRadius, StingerMaxRadius, StingerAngularSpeed, _) =
                ValidateOrbit(StingerMinRadius, StingerMaxRadius, StingerAngularSpeed, 1f);

            (BlackoutScreamMinRadius, BlackoutScreamMaxRadius, BlackoutScreamAngularSpeed, BlackoutScreamApproachSpeed) =
                ValidateOrbit(BlackoutScreamMinRadius, BlackoutScreamMaxRadius, BlackoutScreamAngularSpeed, BlackoutScreamApproachSpeed);

            (HunterBreathMinRadius, HunterBreathMaxRadius, HunterBreathAngularSpeed, HunterBreathApproachSpeed) =
                ValidateOrbit(HunterBreathMinRadius, HunterBreathMaxRadius, HunterBreathAngularSpeed, HunterBreathApproachSpeed);

            (HelpfulExplosionMinRadius, HelpfulExplosionMaxRadius, HelpfulExplosionAngularSpeed, HelpfulExplosionApproachSpeed) =
                ValidateOrbit(HelpfulExplosionMinRadius, HelpfulExplosionMaxRadius, HelpfulExplosionAngularSpeed, HelpfulExplosionApproachSpeed);

            (DangerousExplosionMinRadius, DangerousExplosionMaxRadius, DangerousExplosionAngularSpeed, DangerousExplosionApproachSpeed) =
                ValidateOrbit(DangerousExplosionMinRadius, DangerousExplosionMaxRadius, DangerousExplosionAngularSpeed, DangerousExplosionApproachSpeed);

            (GeneratorMinRadius, GeneratorMaxRadius, GeneratorAngularSpeed, GeneratorApproachSpeed) =
                ValidateOrbit(GeneratorMinRadius, GeneratorMaxRadius, GeneratorAngularSpeed, GeneratorApproachSpeed);

            (FlickerBreathMinRadius, FlickerBreathMaxRadius, FlickerBreathAngularSpeed, FlickerBreathApproachSpeed) =
                ValidateOrbit(FlickerBreathMinRadius, FlickerBreathMaxRadius, FlickerBreathAngularSpeed, FlickerBreathApproachSpeed);

            (FlickerClickingMinRadius, FlickerClickingMaxRadius, FlickerClickingAngularSpeed, FlickerClickingApproachSpeed) =
                ValidateOrbit(FlickerClickingMinRadius, FlickerClickingMaxRadius, FlickerClickingAngularSpeed, FlickerClickingApproachSpeed);

            (RagdollMinRadius, RagdollMaxRadius, RagdollAngularSpeed, RagdollApproachSpeed) =
                ValidateOrbit(RagdollMinRadius, RagdollMaxRadius, RagdollAngularSpeed, RagdollApproachSpeed);

            RagdollHeightOffset = RagdollHeightOffset.Clamp(-5f, 5f);
        }

        /// <summary>
        /// Audits a 3D polar orbit system matrix using zero-allocation C# math parameters to insulate spatialization coroutines.
        /// </summary>
        private static (float Min, float Max, float Ang, float App) ValidateOrbit(float minRadius, float maxRadius, float angularSpeed, float approachSpeed)
        {
            // Enforce minimum tracking bounds to avoid spatial singular coordinate computation collapses
            minRadius = minRadius.LimitMin(0.1f);

            if (maxRadius <= minRadius)
            {
                Logger.Warn(nameof(AudioConfig), $"Spatial vector anomaly caught: MaxRadius ({maxRadius}m) collapsed inside MinRadius ({minRadius}m). Auto-expanding coordinate envelope threshold.");
                maxRadius = minRadius + 1.0f;
            }

            // Shield against stalling orbital rotation calculations: ensure angular translation speed never locks at 0
            if (Math.Abs(angularSpeed) < 0.05f)
            {
                angularSpeed = angularSpeed >= 0f ? 0.05f : -0.05f;
            }

            return (minRadius, maxRadius, angularSpeed, approachSpeed.LimitMin(0.01f));
        }
        #endregion
    }
}