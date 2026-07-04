using LabApi.Extensions;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;
using YamlDotNet.Serialization;
using Logger = LabApi.Extensions.Misc.iLogger;

namespace SCP_575.ConfigObjects
{
    /// <summary>
    /// Configuration for player sanity system behavior throughout the round.
    /// Controls decay, regeneration, medical recovery, and stage-based visual/physical effects.
    /// </summary>
    public sealed class PlayerSanityConfig
    {
        #region Sanity Flow Settings
        [Description("Initial sanity value (0–100) allocated instantly on role instantiation.")]
        public float InitialSanity { get; set; } = 100f;

        [Description("The flat value scale a player naturally loses PER MINUTE under standard baseline environments.")]
        public float BaseDecayPerMinute { get; set; } = 6.65f;

        [Description("Decay multiplication factor applied continuously during an active SCP-575 Blackout event cascade.")]
        public float DecayMultiplierBlackout { get; set; } = 1.65f;

        [Description("Severe decay multiplication factor applied when the player has NO active mobile light sources in complete darkness.")]
        public float DecayMultiplierDarkness { get; set; } = 2.55f;

        [Description("The flat value scale a player passively regenerates PER MINUTE while standing inside safe, active light zones.")]
        public float PassiveRegenPerMinute { get; set; } = 4.35f;

        [Description("Instant sanity drop applied the exact moment a player sustains damage from an anomalous entity source.")]
        public float ScpHitSanityDrop { get; set; } = 3f;
        #endregion

        #region Medical Recovery Settings
        [Description("Minimum sanity recovery allocation factor delivered by standard medical pills.")]
        public float PainkillersRestoreMin { get; set; } = 4f;

        [Description("Maximum sanity recovery allocation factor delivered by standard medical pills.")]
        public float PainkillersRestoreMax { get; set; } = 12f;

        [Description("Incremental baseline value injected into sanity tracking per second if the user digests pills within a lit room context.")]
        public float PainkillersExtraSanityRegen { get; set; } = 0.65f;

        [Description("The temporal duration track in seconds governing the extended pill-induced regeneration timeline.")]
        public float PainkillersRegenDuration { get; set; } = 12.75f;

        [Description("The absolute protection window in seconds during which SCP-575 ignores the player completely post-medication.")]
        public float PainkillersProtectionDuration { get; set; } = 3.35f;

        [Description("Minimum sanity structural restoration percentage delivered by consuming SCP-500.")]
        public float Scp500RestoreMin { get; set; } = 90f;

        [Description("Maximum sanity structural restoration percentage delivered by consuming SCP-500.")]
        public float Scp500RestoreMax { get; set; } = 100f;
        #endregion

        #region Runtime Backing Fields
        [YamlIgnore]
        public float DecayRateBase { get; private set; }

        [YamlIgnore]
        public float PassiveRegenRate { get; private set; }
        #endregion

        #region Stage Thresholds and Effects Matrix
        [Description("Temporal immunity window in seconds preventing rapid sensory burst feedback loop triggers on the same subject.")]
        public float EffectsBurstCooldown { get; set; } = 4.25f;

        [Description("Cooldown gate in seconds separating independent spatial audio ambient hit tracks tracking on the same player.")]
        public float AttackAudioCooldownSeconds { get; set; } = 1.25f;

        [Description("The orchestrated progression matrix mapping exact sanity thresholds directly onto specific status profile blocks.")]
        public List<PlayerSanityStageConfig> SanityStages { get; set; } = CreateDefaultStagesFactory();
        #endregion

        #region Pre-Calculated Frequency Rates Initialization
        public PlayerSanityConfig() => RecalculateRuntimeExecutionRates();

        private void RecalculateRuntimeExecutionRates()
        {
            DecayRateBase = BaseDecayPerMinute / 60f;
            PassiveRegenRate = PassiveRegenPerMinute / 60f;
        }
        #endregion

        #region Validation Engine
        /// <summary>
        /// Validates sanity metrics, sanitizes medical sub-threshold distributions, 
        /// and conducts a linear mathematical audit confirming the contiguous continuity of the 0-100 threshold range.
        /// </summary>
        public void Validate()
        {
            // Fluent API Upgrade: Sanitize standard boundaries with high-speed inline primitive math extensions
            InitialSanity = InitialSanity.Clamp(0f, 100f);

            BaseDecayPerMinute = BaseDecayPerMinute.LimitMin(0f);
            PassiveRegenPerMinute = PassiveRegenPerMinute.LimitMin(0f);
            DecayMultiplierBlackout = DecayMultiplierBlackout.LimitMin(0f);
            DecayMultiplierDarkness = DecayMultiplierDarkness.LimitMin(0f);

            RecalculateRuntimeExecutionRates();

            ScpHitSanityDrop = ScpHitSanityDrop.LimitMin(0f);
            EffectsBurstCooldown = EffectsBurstCooldown.LimitMin(0f);
            AttackAudioCooldownSeconds = AttackAudioCooldownSeconds.LimitMin(0f);

            PainkillersRestoreMin = PainkillersRestoreMin.Clamp(0f, 100f);
            PainkillersRestoreMax = PainkillersRestoreMax.Clamp(0f, 100f);

            // Modern Tuple Swap Pattern to secure min-max boundary relationships flawlessly
            if (PainkillersRestoreMin > PainkillersRestoreMax)
            {
                Logger.Warn(nameof(PlayerSanityConfig), "Pill restoration threshold out of order: PainkillersRestoreMin was greater than Max. Executing tuple-swap correction...");
                (PainkillersRestoreMin, PainkillersRestoreMax) = (PainkillersRestoreMax, PainkillersRestoreMin);
            }

            PainkillersExtraSanityRegen = PainkillersExtraSanityRegen.LimitMin(0f);
            PainkillersRegenDuration = PainkillersRegenDuration.LimitMin(0f);
            PainkillersProtectionDuration = PainkillersProtectionDuration.LimitMin(0f);

            Scp500RestoreMin = Scp500RestoreMin.Clamp(0f, 100f);
            Scp500RestoreMax = Scp500RestoreMax.Clamp(0f, 100f);

            if (Scp500RestoreMin > Scp500RestoreMax)
            {
                Logger.Warn(nameof(PlayerSanityConfig), "SCP-500 restoration threshold out of order: Scp500RestoreMin was greater than Max. Executing tuple-swap correction...");
                (Scp500RestoreMin, Scp500RestoreMax) = (Scp500RestoreMax, Scp500RestoreMin);
            }

            // High-precision linear boundary continuity audit
            if (SanityStages == null || SanityStages.Count == 0)
            {
                Logger.Warn(nameof(PlayerSanityConfig), "SanityStages matrix evaluates to null or empty space. Force re-injecting pristine 6-tier configuration layout map.");
                InjectDefaultSanityStages();
                return;
            }

            // Order ascending using clean collection sorting predicates
            SanityStages.Sort((a, b) => a.MinThreshold.CompareTo(b.MinThreshold));

            bool flowMatrixFaultDetected = SanityStages[0].MinThreshold > 0f || SanityStages[^1].MaxThreshold < 100f;

            if (!flowMatrixFaultDetected)
            {
                for (int i = 0; i < SanityStages.Count - 1; i++)
                {
                    // Audit contiguous flow matrix gaps with a tight float variance allowance limit
                    if (Mathf.Abs(SanityStages[i].MaxThreshold - SanityStages[i + 1].MinThreshold) > 0.01f)
                    {
                        flowMatrixFaultDetected = true;
                        break;
                    }
                }
            }

            if (flowMatrixFaultDetected)
            {
                Logger.Warn(nameof(PlayerSanityConfig), "Configured sanity thresholds do not contiguously frame the full 0-100% range. Overriding matrix with standard 6-tier fallback blueprint.");
                InjectDefaultSanityStages();
                return;
            }

            // Safe element-wise internal object array validation step
            for (int i = 0; i < SanityStages.Count; i++)
            {
                var stage = SanityStages[i];
                if (stage == null) continue;

                stage.Effects ??= new List<PlayerSanityEffectConfig>();
                stage.Validate();
            }
        }

        /// <summary>
        /// Recovers the configuration matrix back into a contiguous layout to resolve deployment formatting anomalies.
        /// </summary>
        private void InjectDefaultSanityStages()
        {
            SanityStages = CreateDefaultStagesFactory();

            for (int i = 0; i < SanityStages.Count; i++)
            {
                SanityStages[i].Validate();
            }
        }
        #endregion

        #region Static Factory Data Source
        /// <summary>
        /// Creates a fresh instance of the default 6-tier horror progression matrix layout cleanly mapping FacilityEffectType components.
        /// </summary>
        private static List<PlayerSanityStageConfig> CreateDefaultStagesFactory()
        {
            return new List<PlayerSanityStageConfig>
            {
                new()
                {
                    MinThreshold = 0f, MaxThreshold = 10f, DamageOnStrike = 45f, AdditionalDamagePerStack = 15f,
                    DamageOnStrikeWhenLightsourceActive = 10f, AdditionalDamagePerStackWhenLightsourceActive = 8f,
                    OverrideLightSourceSanityProtection = true,
                    Effects = new()
                    {
                        new() { EffectType = FacilityEffectType.SilentWalk, Duration = 3.05f, Intensity = 10 },
                        new() { EffectType = FacilityEffectType.Slowness, Duration = 3.15f, Intensity = 75 },
                        new() { EffectType = FacilityEffectType.Exhausted, Duration = 0.65f, Intensity = 1 },
                        new() { EffectType = FacilityEffectType.Blurred, Duration = 2.65f, Intensity = 1 },
                        new() { EffectType = FacilityEffectType.Blindness, Duration = 1.25f, Intensity = 1 },
                        new() { EffectType = FacilityEffectType.Deafened, Duration = 2.75f, Intensity = 1 },
                        new() { EffectType = FacilityEffectType.Flashed, Duration = 0.55f, Intensity = 1 }
                    }
                },
                new()
                {
                    MinThreshold = 10f, MaxThreshold = 25f, DamageOnStrike = 30f, AdditionalDamagePerStack = 9f,
                    DamageOnStrikeWhenLightsourceActive = 8f, AdditionalDamagePerStackWhenLightsourceActive = 6f,
                    OverrideLightSourceSanityProtection = false,
                    Effects = new()
                    {
                        new() { EffectType = FacilityEffectType.SilentWalk, Duration = 1.85f, Intensity = 9 },
                        new() { EffectType = FacilityEffectType.Slowness, Duration = 2.15f, Intensity = 60 },
                        new() { EffectType = FacilityEffectType.Exhausted, Duration = 0.35f, Intensity = 1 },
                        new() { EffectType = FacilityEffectType.Blurred, Duration = 1.95f, Intensity = 1 },
                        new() { EffectType = FacilityEffectType.Blindness, Duration = 0.75f, Intensity = 1 },
                        new() { EffectType = FacilityEffectType.Deafened, Duration = 1.75f, Intensity = 1 },
                        new() { EffectType = FacilityEffectType.Flashed, Duration = 0.35f, Intensity = 1 }
                    }
                },
                new()
                {
                    MinThreshold = 25f, MaxThreshold = 50f, DamageOnStrike = 20f, AdditionalDamagePerStack = 6f,
                    DamageOnStrikeWhenLightsourceActive = 5f, AdditionalDamagePerStackWhenLightsourceActive = 4f,
                    OverrideLightSourceSanityProtection = false,
                    Effects = new()
                    {
                        new() { EffectType = FacilityEffectType.SilentWalk, Duration = 1.45f, Intensity = 8 },
                        new() { EffectType = FacilityEffectType.Slowness, Duration = 1.85f, Intensity = 45 },
                        new() { EffectType = FacilityEffectType.Exhausted, Duration = 0.25f, Intensity = 1 },
                        new() { EffectType = FacilityEffectType.Blurred, Duration = 1.65f, Intensity = 1 },
                        new() { EffectType = FacilityEffectType.Blindness, Duration = 0.55f, Intensity = 1 },
                        new() { EffectType = FacilityEffectType.Deafened, Duration = 1.45f, Intensity = 1 },
                        new() { EffectType = FacilityEffectType.Flashed, Duration = 0.25f, Intensity = 1 }
                    }
                },
                new()
                {
                    MinThreshold = 50f, MaxThreshold = 75f, DamageOnStrike = 12f, AdditionalDamagePerStack = 4f,
                    DamageOnStrikeWhenLightsourceActive = 4f, AdditionalDamagePerStackWhenLightsourceActive = 3f,
                    OverrideLightSourceSanityProtection = false,
                    Effects = new()
                    {
                        new() { EffectType = FacilityEffectType.SilentWalk, Duration = 1.15f, Intensity = 6 },
                        new() { EffectType = FacilityEffectType.Slowness, Duration = 1.35f, Intensity = 35 },
                        new() { EffectType = FacilityEffectType.Blurred, Duration = 1.25f, Intensity = 1 },
                        new() { EffectType = FacilityEffectType.Blindness, Duration = 0.35f, Intensity = 1 },
                        new() { EffectType = FacilityEffectType.Deafened, Duration = 0.95f, Intensity = 1 }
                    }
                },
                new()
                {
                    MinThreshold = 75f, MaxThreshold = 90f, DamageOnStrike = 8f, AdditionalDamagePerStack = 3f,
                    DamageOnStrikeWhenLightsourceActive = 2f, AdditionalDamagePerStackWhenLightsourceActive = 2f,
                    OverrideLightSourceSanityProtection = false,
                    Effects = new()
                    {
                        new() { EffectType = FacilityEffectType.SilentWalk, Duration = 0.65f, Intensity = 4 },
                        new() { EffectType = FacilityEffectType.Slowness, Duration = 0.85f, Intensity = 25 },
                        new() { EffectType = FacilityEffectType.Blurred, Duration = 0.75f, Intensity = 1 },
                        new() { EffectType = FacilityEffectType.Blindness, Duration = 0.25f, Intensity = 1 },
                        new() { EffectType = FacilityEffectType.Deafened, Duration = 0.45f, Intensity = 1 }
                    }
                },
                new()
                {
                    MinThreshold = 90f, MaxThreshold = 100f, DamageOnStrike = 4f, AdditionalDamagePerStack = 2f,
                    DamageOnStrikeWhenLightsourceActive = 0f, AdditionalDamagePerStackWhenLightsourceActive = 1f,
                    OverrideLightSourceSanityProtection = false,
                    Effects = new()
                    {
                        new() { EffectType = FacilityEffectType.SilentWalk, Duration = 0.35f, Intensity = 2 },
                        new() { EffectType = FacilityEffectType.Slowness, Duration = 0.5f, Intensity = 15 },
                        new() { EffectType = FacilityEffectType.Blurred, Duration = 0.45f, Intensity = 1 },
                        new() { EffectType = FacilityEffectType.Deafened, Duration = 0.25f, Intensity = 1 }
                    }
                }
            };
        }
        #endregion
    }
}