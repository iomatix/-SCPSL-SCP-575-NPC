namespace SCP_575.ConfigObjects
{
    using System.ComponentModel;
    using UnityEngine;
    using Logger = SCP_575.Shared.LibraryLabAPI;

    public sealed class AudioConfig
    {
        #region Baseline Engine Options
        [Description("The cooldown time in seconds for global scream audio playback. Must be positive.")]
        public float GlobalScreamCooldown { get; set; } = 75f;

        [Description("The default duration in seconds for audio fade-in and fade-out effects. Must be non-negative.")]
        public float DefaultFadeDuration { get; set; } = 1f;
        #endregion

        #region Tension Pacing Dynamics
        public float TensionSanityRiskMultiplier { get; set; } = 3.5f;
        public float TensionPassiveDecayRate { get; set; } = 2.5f;
        public float TensionTriggerMinThreshold { get; set; } = 45.0f;
        public float TensionTriggerMaxThreshold { get; set; } = 85.0f;
        #endregion

        #region Neurological Integrity Thresholds
        public float PanicDroneSanityThreshold { get; set; } = 22.0f;
        public float PanicDroneFadeInDuration { get; set; } = 3.5f;
        public float Tier4ShockStingerThreshold { get; set; } = 10.0f;
        public float ShockStingerDeafenDuration { get; set; } = 1.5f;
        public float Tier3PsychoticWhispersThreshold { get; set; } = 25.0f;
        public float Tier2DisturbedWhispersThreshold { get; set; } = 55.0f;
        #endregion

        #region Acoustic Suppression & Mix Headroom
        public float DirectDamageSuppressionDuration { get; set; } = 3.5f;
        public float ExplosionSuppressionRadius { get; set; } = 28.0f;
        public float ExplosionSuppressionDuration { get; set; } = 5.0f;
        public float CombatStingerCooldown { get; set; } = 1.6f;
        #endregion

        #region Spatial Coordinates & Orbit Vectors
        public float StingerMaxRadius { get; set; } = 2.2f;
        public float StingerMinRadius { get; set; } = 0.5f;
        public float StingerAngularSpeed { get; set; } = 2.5f;

        public float BlackoutScreamMaxRadius { get; set; } = 5.5f;
        public float BlackoutScreamMinRadius { get; set; } = 0.6f;
        public float BlackoutScreamAngularSpeed { get; set; } = 3.4f;
        public float BlackoutScreamApproachSpeed { get; set; } = 1.8f;

        public float HunterBreathMaxRadius { get; set; } = 1.45f;
        public float HunterBreathMinRadius { get; set; } = 0.35f;
        public float HunterBreathAngularSpeed { get; set; } = 1.15f;
        public float HunterBreathApproachSpeed { get; set; } = 1.95f;

        public float HelpfulExplosionMaxRadius { get; set; } = 9.0f;
        public float HelpfulExplosionMinRadius { get; set; } = 0.5f;
        public float HelpfulExplosionAngularSpeed { get; set; } = 3.8f;
        public float HelpfulExplosionApproachSpeed { get; set; } = 2.5f;

        public float DangerousExplosionMaxRadius { get; set; } = 12.0f;
        public float DangerousExplosionMinRadius { get; set; } = 1.0f;
        public float DangerousExplosionAngularSpeed { get; set; } = 1.2f;
        public float DangerousExplosionApproachSpeed { get; set; } = 1.4f;

        public float GeneratorMaxRadius { get; set; } = 7.5f;
        public float GeneratorMinRadius { get; set; } = 1.2f;
        public float GeneratorAngularSpeed { get; set; } = 2.4f;
        public float GeneratorApproachSpeed { get; set; } = 2.8f;

        public float FlickerBreathMaxRadius { get; set; } = 2.2f;
        public float FlickerBreathMinRadius { get; set; } = 0.4f;
        public float FlickerBreathAngularSpeed { get; set; } = 2.8f;
        public float FlickerBreathApproachSpeed { get; set; } = 1.6f;

        public float FlickerClickingMaxRadius { get; set; } = 3.8f;
        public float FlickerClickingMinRadius { get; set; } = 0.7f;
        public float FlickerClickingAngularSpeed { get; set; } = 4.5f;
        public float FlickerClickingApproachSpeed { get; set; } = 2.2f;

        public float RagdollMaxRadius { get; set; } = 2.5f;
        public float RagdollMinRadius { get; set; } = 0.45f;
        public float RagdollAngularSpeed { get; set; } = 2.15f;
        public float RagdollApproachSpeed { get; set; } = 3.25f;
        public float RagdollHeightOffset { get; set; } = 0.15f;
        #endregion

        public void Validate()
        {
            // --- 1. Baseline Engine Options ---
            if (GlobalScreamCooldown <= 0f) GlobalScreamCooldown = 75f;
            if (DefaultFadeDuration < 0f) DefaultFadeDuration = 1f;

            // --- 2. Tension Pacing Dynamics ---
            TensionSanityRiskMultiplier = Mathf.Max(0f, TensionSanityRiskMultiplier);
            TensionPassiveDecayRate = Mathf.Max(0f, TensionPassiveDecayRate);

            if (TensionTriggerMinThreshold > TensionTriggerMaxThreshold)
            {
                float temp = TensionTriggerMinThreshold;
                TensionTriggerMinThreshold = TensionTriggerMaxThreshold;
                TensionTriggerMaxThreshold = temp;
            }
            TensionTriggerMinThreshold = Mathf.Clamp(TensionTriggerMinThreshold, 0f, 100f);
            TensionTriggerMaxThreshold = Mathf.Clamp(TensionTriggerMaxThreshold, 0f, 100f);

            // --- 3. Neurological Integrity Thresholds ---
            Tier4ShockStingerThreshold = Mathf.Clamp(Tier4ShockStingerThreshold, 0f, 100f);
            Tier3PsychoticWhispersThreshold = Mathf.Clamp(Tier3PsychoticWhispersThreshold, 0f, 100f);
            Tier2DisturbedWhispersThreshold = Mathf.Clamp(Tier2DisturbedWhispersThreshold, 0f, 100f);
            PanicDroneSanityThreshold = Mathf.Clamp(PanicDroneSanityThreshold, 0f, 100f);

            if (Tier4ShockStingerThreshold >= Tier3PsychoticWhispersThreshold)
                Tier4ShockStingerThreshold = Mathf.Max(0f, Tier3PsychoticWhispersThreshold - 5f);

            if (Tier3PsychoticWhispersThreshold >= Tier2DisturbedWhispersThreshold)
                Tier3PsychoticWhispersThreshold = Mathf.Max(Tier4ShockStingerThreshold + 5f, Tier2DisturbedWhispersThreshold - 5f);

            PanicDroneFadeInDuration = Mathf.Max(0f, PanicDroneFadeInDuration);
            ShockStingerDeafenDuration = Mathf.Max(0f, ShockStingerDeafenDuration);

            // --- 4. Acoustic Suppression & Mix Headroom ---
            DirectDamageSuppressionDuration = Mathf.Max(0f, DirectDamageSuppressionDuration);
            ExplosionSuppressionRadius = Mathf.Max(0f, ExplosionSuppressionRadius);
            ExplosionSuppressionDuration = Mathf.Max(0f, ExplosionSuppressionDuration);
            CombatStingerCooldown = Mathf.Max(0f, CombatStingerCooldown);

            // --- 5. Spatial Coordinates & Orbit Vectors (Clean ValueTuple Assignments) ---
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

            RagdollHeightOffset = Mathf.Clamp(RagdollHeightOffset, -5f, 5f);
        }

        private (float Min, float Max, float Ang, float App) ValidateOrbit(float minRadius, float maxRadius, float angularSpeed, float approachSpeed)
        {
            minRadius = Mathf.Max(0.1f, minRadius);

            if (maxRadius <= minRadius)
            {
                Logger.LogWarn(nameof(AudioConfig), $"[AudioConfig] MaxRadius ({maxRadius}) was upscaled past MinRadius ({minRadius}).");
                maxRadius = minRadius + 1.0f;
            }

            if (Mathf.Abs(angularSpeed) < 0.05f)
            {
                angularSpeed = angularSpeed >= 0 ? 0.05f : -0.05f;
            }

            return (minRadius, maxRadius, angularSpeed, Mathf.Max(0.01f, approachSpeed));
        }
    }
}