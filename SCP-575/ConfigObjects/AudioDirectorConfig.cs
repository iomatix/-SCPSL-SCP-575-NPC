namespace SCP_575.ConfigObjects
{
    using System.ComponentModel;
    using Exiled.API.Features;
    using UnityEngine;

    /// <summary>
    /// Configuration container mapping all parametric limits, spatial dimensions, 
    /// and threshold scaling curves utilized by the autonomous horror pacing engine.
    /// </summary>
    public sealed class AudioDirectorConfig
    {
        #region Tension Pacing Dynamics

        [Description("The speed modifier used to accelerate individual tension accumulation curves based on cognitive risk coefficients.")]
        public float TensionSanityRiskMultiplier { get; set; } = 3.5f;

        [Description("The passive drain coefficient applied to a subject's stress meter when standing inside safe, illuminated rooms.")]
        public float TensionPassiveDecayRate { get; set; } = 2.5f;

        [Description("The minimum randomized threshold limit needed to trigger a threshold-bound pacing event.")]
        public float TensionTriggerMinThreshold { get; set; } = 45.0f;

        [Description("The maximum randomized threshold limit needed to trigger a threshold-bound pacing event.")]
        public float TensionTriggerMaxThreshold { get; set; } = 85.0f;

        #endregion

        #region Neurological Integrity Thresholds

        [Description("The neurological integrity percentage (Sanity) below which the persistent tracking panic sub-drone loop activates.")]
        public float PanicDroneSanityThreshold { get; set; } = 15.0f;

        [Description("The linear volume fade-in duration (seconds) allocated to the persistent panic drone streaming session.")]
        public float PanicDroneFadeInDuration { get; set; } = 3.5f;

        [Description("The sanity limit below which high-amplitude psychological jump-scares (ShockStinger) are authorized.")]
        public float Tier4ShockStingerThreshold { get; set; } = 10.0f;

        [Description("The duration (seconds) of native engine audio blackouts applied to the target client during shock stingers.")]
        public float ShockStingerDeafenDuration { get; set; } = 1.5f;

        [Description("The sanity limit below which Tier 3 overlapping psychotic vocalizations are authorized.")]
        public float Tier3PsychoticWhispersThreshold { get; set; } = 25.0f;

        [Description("The sanity limit below which Tier 2 disturbed intermediate whispers are authorized.")]
        public float Tier2DisturbedWhispersThreshold { get; set; } = 55.0f;

        #endregion

        #region Acoustic Suppression & Mix Headroom

        [Description("The acoustic suppression lock duration (seconds) freezing psychological tracks after sustaining direct physical hits.")]
        public float DirectDamageSuppressionDuration { get; set; } = 3.5f;

        [Description("The environmental radius (meters) swept around an explosive flashpoint to suppress low-priority ambient cues.")]
        public float ExplosionSuppressionRadius { get; set; } = 28.0f;

        [Description("The acoustic suppression duration (seconds) freezing psychological tracks for everyone caught in an explosion radius.")]
        public float ExplosionSuppressionDuration { get; set; } = 5.0f;

        [Description("The mandatory temporal spacing lock (seconds) enforced between rapid sequential combat audio stingers.")]
        public float CombatStingerCooldown { get; set; } = 1.6f;

        #endregion

        #region Spatial Coordinates & Orbit Vectors

        [Description("Shock Stinger Orbit Metrics: Max Radius, Min Radius, Angular Orbit Speed.")]
        public float StingerMaxRadius { get; set; } = 2.2f;
        public float StingerMinRadius { get; set; } = 0.5f;
        public float StingerAngularSpeed { get; set; } = 2.5f;

        [Description("Helpful Projectile Vortex Metrics: Max Radius, Min Radius, Angular Speed, Spatial Convergence Approach Speed.")]
        public float HelpfulExplosionMaxRadius { get; set; } = 9.0f;
        public float HelpfulExplosionMinRadius { get; set; } = 0.5f;
        public float HelpfulExplosionAngularSpeed { get; set; } = 3.8f;
        public float HelpfulExplosionApproachSpeed { get; set; } = 2.5f;

        [Description("Dangerous Projectile Dispersion Metrics: Max Radius, Min Radius, Angular Speed, Spatial Approach Speed.")]
        public float DangerousExplosionMaxRadius { get; set; } = 12.0f;
        public float DangerousExplosionMinRadius { get; set; } = 1.0f;
        public float DangerousExplosionAngularSpeed { get; set; } = 1.2f;
        public float DangerousExplosionApproachSpeed { get; set; } = 1.4f;

        [Description("Substation Grid Retaliation Orbit Metrics: Max Radius, Min Radius, Angular Speed, Spatial Convergence Approach Speed.")]
        public float GeneratorMaxRadius { get; set; } = 7.5f;
        public float GeneratorMinRadius { get; set; } = 1.2f;
        public float GeneratorAngularSpeed { get; set; } = 2.4f;
        public float GeneratorApproachSpeed { get; set; } = 2.8f;

        [Description("Lightsource Anomalous Flicker - Entity Breath Spatial Orbit Coordinates.")]
        public float FlickerBreathMaxRadius { get; set; } = 2.2f;
        public float FlickerBreathMinRadius { get; set; } = 0.4f;
        public float FlickerBreathAngularSpeed { get; set; } = 2.8f;
        public float FlickerBreathApproachSpeed { get; set; } = 1.6f;

        [Description("Lightsource Anomalous Flicker - Shadow Clicking Localized Orbit Coordinates.")]
        public float FlickerClickingMaxRadius { get; set; } = 3.8f;
        public float FlickerClickingMinRadius { get; set; } = 0.7f;
        public float FlickerClickingAngularSpeed { get; set; } = 4.5f;
        public float FlickerClickingApproachSpeed { get; set; } = 2.2f;

        [Description("Anatomical Corpse Consumption Orbit Metrics: Max Radius, Min Radius, Angular Speed, Approach Speed, Spatial Height Offset.")]
        public float RagdollMaxRadius { get; set; } = 2.5f;
        public float RagdollMinRadius { get; set; } = 0.45f;
        public float RagdollAngularSpeed { get; set; } = 2.15f;
        public float RagdollApproachSpeed { get; set; } = 3.25f;
        public float RagdollHeightOffset { get; set; } = 0.15f;

        #endregion

        /// <summary>
        /// Validates parametric bounds to defend the client mixer space against hardware phasing anomalies.
        /// </summary>
        public void Validate()
        {
            // Hardware constraint protection: Panning vectors collapsing closer than 0.15m break sound-stage panning primitives
            StingerMinRadius = Mathf.Max(0.15f, StingerMinRadius);
            HelpfulExplosionMinRadius = Mathf.Max(0.15f, HelpfulExplosionMinRadius);
            DangerousExplosionMinRadius = Mathf.Max(0.15f, DangerousExplosionMinRadius);
            GeneratorMinRadius = Mathf.Max(0.15f, GeneratorMinRadius);
            FlickerBreathMinRadius = Mathf.Max(0.15f, FlickerBreathMinRadius);
            FlickerClickingMinRadius = Mathf.Max(0.15f, FlickerClickingMinRadius);
            RagdollMinRadius = Mathf.Max(0.15f, RagdollMinRadius);

            if (TensionTriggerMinThreshold > TensionTriggerMaxThreshold)
            {
                Log.Warn("[AudioDirectorConfig] TensionTriggerMinThreshold cannot exceed MaxThreshold. Performing defensive transposition.");
                float temp = TensionTriggerMinThreshold;
                TensionTriggerMinThreshold = TensionTriggerMaxThreshold;
                TensionTriggerMaxThreshold = temp;
            }

            TensionTriggerMinThreshold = Mathf.Clamp(TensionTriggerMinThreshold, 5f, 100f);
            TensionTriggerMaxThreshold = Mathf.Clamp(TensionTriggerMaxThreshold, 5f, 100f);
        }
    }
}