namespace SCP_575.ConfigObjects
{
    using SCP_575.Types;
    using System.Collections.Generic;
    using System.ComponentModel;
    using UnityEngine;
    using Logger = LabApi.Features.Console.Logger;

    /// <summary>
    /// Configuration for player sanity system behavior throughout the round.
    /// Controls decay, regeneration, medical recovery, and stage-based visual/physical effects.
    /// </summary>
    public sealed class PlayerSanityConfig
    {
        #region Sanity Flow Settings

        [Description("Initial sanity value (0–100) on spawn.")]
        public float InitialSanity { get; set; } = 100f;

        [Description("How much sanity (0-100) a player naturally loses PER MINUTE in baseline conditions.")]
        public float BaseDecayPerMinute { get; set; } = 5.65f;

        [Description("Decay multiplier applied during an active SCP-575 Blackout event.")]
        public float DecayMultiplierBlackout { get; set; } = 1.65f;

        [Description("Harsh decay multiplier applied when the player has NO active personal light source (flashlight/weapon light) in the dark.")]
        public float DecayMultiplierDarkness { get; set; } = 2.45f;

        [Description("How much sanity (0-100) a player passively regenerates PER MINUTE when inside safe, lit zones.")]
        public float PassiveRegenPerMinute { get; set; } = 3.38f;

        [Description("Amount of sanity lost instantly when attacked/hit by any SCP entity.")]
        public float ScpHitSanityDrop { get; set; } = 4f;

        #endregion

        #region Medical Recovery Settings

        [Description("Minimum sanity restore percent from medical pills.")]
        public float PainkillersRestoreMin { get; set; } = 4f;

        [Description("Maximum sanity restore percent from medical pills.")]
        public float PainkillersRestoreMax { get; set; } = 12f;

        [Description("Amount added to sanity per second if the player is in the bright room.")]
        public float PainkillersExtraSanityRegen { get; set; } = 0.65f;

        [Description("Duration in seconds of the regen effect if the player is in the bright room.")]
        public float PainkillersRegenDuration { get; set; } = 13.5f;

        [Description("Duration in seconds of the protection effect. SCP-575 will not deal any damage nor apply any effects to the player for this duration.")]
        public float PainkillersProtectionDuration { get; set; } = 3.25f;

        [Description("Minimum sanity restore percent from SCP-500.")]
        public float Scp500RestoreMin { get; set; } = 85f;

        [Description("Maximum sanity restore percent from SCP-500.")]
        public float Scp500RestoreMax { get; set; } = 100f;

        #endregion

        #region Runtime Backing Fields (Pre-calculated for performance)

        [Description("Decay rate per second is calculated, changing this value doesn't affect decay rate.")]
        public float DecayRateBase { get; private set; }

        [Description("Regen rate per second is calculated, changing this value doesn't affect decay rate.")]
        public float PassiveRegenRate { get; private set; }

        #endregion

        #region Stage Thresholds and Effects

        [Description("Duration in seconds a player is protected from consecutive sensory effect bursts (e.g. blur spams) after the last burst sequence.")]
        public float EffectsBurstCooldown { get; set; } = 3.35f;

        [Description("Cooldown in seconds between consecutive anomalous impact sound triggers on the same player.")]
        public float AttackAudioCooldownSeconds { get; set; } = 1.25f;

        [Description("Stages of sanity and their associated effects.")]
        public List<PlayerSanityStageConfig> SanityStages { get; set; } = new()
        {
            new PlayerSanityStageConfig
            {
                MinThreshold = 90f, MaxThreshold = 100f, DamageOnStrike = 4f, AdditionalDamagePerStack = 2f,
                DamageOnStrikeWhenLightsourceActive = 0f, AdditionalDamagePerStackWhenLightsourceActive = 1f,
                OverrideLightSourceSanityProtection = false,
                Effects = new()
                {
                    new() { EffectType = SanityEffectType.SilentWalk, Duration = 0.35f, Intensity = 2 },
                    new() { EffectType = SanityEffectType.Slowness, Duration = 0.5f, Intensity = 15 },
                    new() { EffectType = SanityEffectType.Blurred, Duration = 0.45f, Intensity = 1 },
                    new() { EffectType = SanityEffectType.Deafened, Duration = 0.25f, Intensity = 1 }
                }
            },
            new PlayerSanityStageConfig
            {
                MinThreshold = 75f, MaxThreshold = 90f, DamageOnStrike = 8f, AdditionalDamagePerStack = 3f,
                DamageOnStrikeWhenLightsourceActive = 2f, AdditionalDamagePerStackWhenLightsourceActive = 2f,
                OverrideLightSourceSanityProtection = false,
                Effects = new()
                {
                    new() { EffectType = SanityEffectType.SilentWalk, Duration = 0.65f, Intensity = 4 },
                    new() { EffectType = SanityEffectType.Slowness, Duration = 0.85f, Intensity = 25 },
                    new() { EffectType = SanityEffectType.Blurred, Duration = 0.75f, Intensity = 1 },
                    new() { EffectType = SanityEffectType.Blindness, Duration = 0.25f, Intensity = 1 },
                    new() { EffectType = SanityEffectType.Deafened, Duration = 0.45f, Intensity = 1 }
                }
            },
            new PlayerSanityStageConfig
            {
                MinThreshold = 50f, MaxThreshold = 75f, DamageOnStrike = 12f, AdditionalDamagePerStack = 4f,
                DamageOnStrikeWhenLightsourceActive = 4f, AdditionalDamagePerStackWhenLightsourceActive = 3f,
                OverrideLightSourceSanityProtection = false,
                Effects = new()
                {
                    new() { EffectType = SanityEffectType.SilentWalk, Duration = 1.15f, Intensity = 6 },
                    new() { EffectType = SanityEffectType.Slowness, Duration = 1.35f, Intensity = 35 },
                    new() { EffectType = SanityEffectType.Blurred, Duration = 1.25f, Intensity = 1 },
                    new() { EffectType = SanityEffectType.Blindness, Duration = 0.35f, Intensity = 1 },
                    new() { EffectType = SanityEffectType.Deafened, Duration = 0.95f, Intensity = 1 }
                }
            },
            new PlayerSanityStageConfig
            {
                MinThreshold = 25f, MaxThreshold = 50f, DamageOnStrike = 20f, AdditionalDamagePerStack = 6f,
                DamageOnStrikeWhenLightsourceActive = 5f, AdditionalDamagePerStackWhenLightsourceActive = 4f,
                OverrideLightSourceSanityProtection = false,
                Effects = new()
                {
                    new() { EffectType = SanityEffectType.SilentWalk, Duration = 1.45f, Intensity = 8 },
                    new() { EffectType = SanityEffectType.Slowness, Duration = 1.85f, Intensity = 45 },
                    new() { EffectType = SanityEffectType.Exhausted, Duration = 0.25f, Intensity = 1 },
                    new() { EffectType = SanityEffectType.Blurred, Duration = 1.65f, Intensity = 1 },
                    new() { EffectType = SanityEffectType.Blindness, Duration = 0.55f, Intensity = 1 },
                    new() { EffectType = SanityEffectType.Deafened, Duration = 1.45f, Intensity = 1 },
                    new() { EffectType = SanityEffectType.Flashed, Duration = 0.25f, Intensity = 1 }
                }
            },
            new PlayerSanityStageConfig
            {
                MinThreshold = 10f, MaxThreshold = 25f, DamageOnStrike = 30f, AdditionalDamagePerStack = 9f,
                DamageOnStrikeWhenLightsourceActive = 8f, AdditionalDamagePerStackWhenLightsourceActive = 6f,
                OverrideLightSourceSanityProtection = false,
                Effects = new()
                {
                    new() { EffectType = SanityEffectType.SilentWalk, Duration = 1.85f, Intensity = 9 },
                    new() { EffectType = SanityEffectType.Slowness, Duration = 2.15f, Intensity = 60 },
                    new() { EffectType = SanityEffectType.Exhausted, Duration = 0.35f, Intensity = 1 },
                    new() { EffectType = SanityEffectType.Blurred, Duration = 1.95f, Intensity = 1 },
                    new() { EffectType = SanityEffectType.Blindness, Duration = 0.75f, Intensity = 1 },
                    new() { EffectType = SanityEffectType.Deafened, Duration = 1.75f, Intensity = 1 },
                    new() { EffectType = SanityEffectType.Flashed, Duration = 0.35f, Intensity = 1 }
                }
            },
            new PlayerSanityStageConfig
            {
                MinThreshold = 0f, MaxThreshold = 10f, DamageOnStrike = 45f, AdditionalDamagePerStack = 15f,
                DamageOnStrikeWhenLightsourceActive = 10f, AdditionalDamagePerStackWhenLightsourceActive = 8f,
                OverrideLightSourceSanityProtection = true,
                Effects = new()
                {
                    new() { EffectType = SanityEffectType.SilentWalk, Duration = 3.05f, Intensity = 10 },
                    new() { EffectType = SanityEffectType.Slowness, Duration = 3.15f, Intensity = 75 },
                    new() { EffectType = SanityEffectType.Exhausted, Duration = 0.65f, Intensity = 1 },
                    new() { EffectType = SanityEffectType.Blurred, Duration = 2.65f, Intensity = 1 },
                    new() { EffectType = SanityEffectType.Blindness, Duration = 1.25f, Intensity = 1 },
                    new() { EffectType = SanityEffectType.Deafened, Duration = 2.75f, Intensity = 1 },
                    new() { EffectType = SanityEffectType.Flashed, Duration = 0.55f, Intensity = 1 }
                }
            }
        };

        #endregion

        /// <summary>
        /// Validates the player sanity configuration parameters and compiles intuitive human-readable metrics into high-speed runtime decimals.
        /// </summary>
        public void Validate()
        {
            // --- 1. Basic Boundary Adjustments ---
            InitialSanity = Mathf.Clamp(InitialSanity, 0f, 100f);

            BaseDecayPerMinute = Mathf.Max(0f, BaseDecayPerMinute);
            PassiveRegenPerMinute = Mathf.Max(0f, PassiveRegenPerMinute);

            DecayMultiplierBlackout = Mathf.Max(0f, DecayMultiplierBlackout);
            DecayMultiplierDarkness = Mathf.Max(0f, DecayMultiplierDarkness);

            // Thread performance conversions
            DecayRateBase = BaseDecayPerMinute / 60f;
            PassiveRegenRate = PassiveRegenPerMinute / 60f;

            ScpHitSanityDrop = Mathf.Max(0f, ScpHitSanityDrop);
            EffectsBurstCooldown = Mathf.Max(0f, EffectsBurstCooldown);
            AttackAudioCooldownSeconds = Mathf.Max(0f, AttackAudioCooldownSeconds);

            // --- 2. Medical Item Clamping & ValueTuple Swapping ---
            PainkillersRestoreMin = Mathf.Clamp(PainkillersRestoreMin, 0f, 100f);
            PainkillersRestoreMax = Mathf.Clamp(PainkillersRestoreMax, 0f, 100f);

            if (PainkillersRestoreMin > PainkillersRestoreMax)
            {
                Logger.Warn("[PlayerSanityConfig] PainkillersRestoreMin was greater than Max. Swapping boundaries.");
                (PainkillersRestoreMin, PainkillersRestoreMax) = (PainkillersRestoreMax, PainkillersRestoreMin);
            }

            PainkillersExtraSanityRegen = Mathf.Max(0f, PainkillersExtraSanityRegen);
            PainkillersRegenDuration = Mathf.Max(0f, PainkillersRegenDuration);
            PainkillersProtectionDuration = Mathf.Max(0f, PainkillersProtectionDuration);

            Scp500RestoreMin = Mathf.Clamp(Scp500RestoreMin, 0f, 100f);
            Scp500RestoreMax = Mathf.Clamp(Scp500RestoreMax, 0f, 100f);

            if (Scp500RestoreMin > Scp500RestoreMax)
            {
                Logger.Warn("[PlayerSanityConfig] Scp500RestoreMin was greater than Max. Swapping boundaries.");
                (Scp500RestoreMin, Scp500RestoreMax) = (Scp500RestoreMax, Scp500RestoreMin);
            }

            // --- 3. Matrix Hierarchy Collection Auditing ---
            if (SanityStages == null || SanityStages.Count == 0)
            {
                Logger.Warn("[PlayerSanityConfig] SanityStages matrix was missing or empty. Re-injecting full 6-tier orchestration schema.");
                InjectDefaultSanityStages();
                return;
            }

            // Sort ascending to perform domain continuity audits
            SanityStages.Sort((a, b) => a.MinThreshold.CompareTo(b.MinThreshold));

            bool generationFaultDetected = false;

            // Audit bound boundaries 
            if (SanityStages[0].MinThreshold > 0f || SanityStages[SanityStages.Count - 1].MaxThreshold < 100f)
            {
                generationFaultDetected = true;
            }

            // Audit contiguous flow matrix gaps
            for (int i = 0; i < SanityStages.Count - 1; i++)
            {
                if (Mathf.Abs(SanityStages[i].MaxThreshold - SanityStages[i + 1].MinThreshold) > 0.01f)
                {
                    generationFaultDetected = true;
                    break;
                }
            }

            if (generationFaultDetected)
            {
                Logger.Warn("[PlayerSanityConfig] Configured thresholds do not contiguously frame the full 0-100 range. Forcing baseline 6-tier rewrite script.");
                InjectDefaultSanityStages();
                return;
            }

            // Complete standard verification down individual elements inside collection safely
            foreach (var stage in SanityStages)
            {
                if (stage == null) continue;

                // Ensure internal collection instantiations are clean
                if (stage.Effects == null)
                    stage.Effects = new List<PlayerSanityEffectConfig>();

                stage.Validate();
            }
        }

        /// <summary>
        /// Instantly mirrors your exact custom 6-tier horror architecture to recover from a production layout formatting failure.
        /// </summary>
        private void InjectDefaultSanityStages()
        {
            SanityStages = new List<PlayerSanityStageConfig>
            {
                new PlayerSanityStageConfig
                {
                    MinThreshold = 0f, MaxThreshold = 10f, DamageOnStrike = 45f, AdditionalDamagePerStack = 15f,
                    DamageOnStrikeWhenLightsourceActive = 10f, AdditionalDamagePerStackWhenLightsourceActive = 8f,
                    OverrideLightSourceSanityProtection = true,
                    Effects = new()
                    {
                        new() { EffectType = SanityEffectType.SilentWalk, Duration = 3.05f, Intensity = 10 },
                        new() { EffectType = SanityEffectType.Slowness, Duration = 3.15f, Intensity = 75 },
                        new() { EffectType = SanityEffectType.Exhausted, Duration = 0.65f, Intensity = 1 },
                        new() { EffectType = SanityEffectType.Blurred, Duration = 2.65f, Intensity = 1 },
                        new() { EffectType = SanityEffectType.Blindness, Duration = 1.25f, Intensity = 1 },
                        new() { EffectType = SanityEffectType.Deafened, Duration = 2.75f, Intensity = 1 },
                        new() { EffectType = SanityEffectType.Flashed, Duration = 0.55f, Intensity = 1 }
                    }
                },
                new PlayerSanityStageConfig
                {
                    MinThreshold = 10f, MaxThreshold = 25f, DamageOnStrike = 30f, AdditionalDamagePerStack = 9f,
                    DamageOnStrikeWhenLightsourceActive = 8f, AdditionalDamagePerStackWhenLightsourceActive = 6f,
                    OverrideLightSourceSanityProtection = false,
                    Effects = new()
                    {
                        new() { EffectType = SanityEffectType.SilentWalk, Duration = 1.85f, Intensity = 9 },
                        new() { EffectType = SanityEffectType.Slowness, Duration = 2.15f, Intensity = 60 },
                        new() { EffectType = SanityEffectType.Exhausted, Duration = 0.35f, Intensity = 1 },
                        new() { EffectType = SanityEffectType.Blurred, Duration = 1.95f, Intensity = 1 },
                        new() { EffectType = SanityEffectType.Blindness, Duration = 0.75f, Intensity = 1 },
                        new() { EffectType = SanityEffectType.Deafened, Duration = 1.75f, Intensity = 1 },
                        new() { EffectType = SanityEffectType.Flashed, Duration = 0.35f, Intensity = 1 }
                    }
                },
                new PlayerSanityStageConfig
                {
                    MinThreshold = 25f, MaxThreshold = 50f, DamageOnStrike = 20f, AdditionalDamagePerStack = 6f,
                    DamageOnStrikeWhenLightsourceActive = 5f, AdditionalDamagePerStackWhenLightsourceActive = 4f,
                    OverrideLightSourceSanityProtection = false,
                    Effects = new()
                    {
                        new() { EffectType = SanityEffectType.SilentWalk, Duration = 1.45f, Intensity = 8 },
                        new() { EffectType = SanityEffectType.Slowness, Duration = 1.85f, Intensity = 45 },
                        new() { EffectType = SanityEffectType.Exhausted, Duration = 0.25f, Intensity = 1 },
                        new() { EffectType = SanityEffectType.Blurred, Duration = 1.65f, Intensity = 1 },
                        new() { EffectType = SanityEffectType.Blindness, Duration = 0.55f, Intensity = 1 },
                        new() { EffectType = SanityEffectType.Deafened, Duration = 1.45f, Intensity = 1 },
                        new() { EffectType = SanityEffectType.Flashed, Duration = 0.25f, Intensity = 1 }
                    }
                },
                new PlayerSanityStageConfig
                {
                    MinThreshold = 50f, MaxThreshold = 75f, DamageOnStrike = 12f, AdditionalDamagePerStack = 4f,
                    DamageOnStrikeWhenLightsourceActive = 4f, AdditionalDamagePerStackWhenLightsourceActive = 3f,
                    OverrideLightSourceSanityProtection = false,
                    Effects = new()
                    {
                        new() { EffectType = SanityEffectType.SilentWalk, Duration = 1.15f, Intensity = 6 },
                        new() { EffectType = SanityEffectType.Slowness, Duration = 1.35f, Intensity = 35 },
                        new() { EffectType = SanityEffectType.Blurred, Duration = 1.25f, Intensity = 1 },
                        new() { EffectType = SanityEffectType.Blindness, Duration = 0.35f, Intensity = 1 },
                        new() { EffectType = SanityEffectType.Deafened, Duration = 0.95f, Intensity = 1 }
                    }
                },
                new PlayerSanityStageConfig
                {
                    MinThreshold = 75f, MaxThreshold = 90f, DamageOnStrike = 8f, AdditionalDamagePerStack = 3f,
                    DamageOnStrikeWhenLightsourceActive = 2f, AdditionalDamagePerStackWhenLightsourceActive = 2f,
                    OverrideLightSourceSanityProtection = false,
                    Effects = new()
                    {
                        new() { EffectType = SanityEffectType.SilentWalk, Duration = 0.65f, Intensity = 4 },
                        new() { EffectType = SanityEffectType.Slowness, Duration = 0.85f, Intensity = 25 },
                        new() { EffectType = SanityEffectType.Blurred, Duration = 0.75f, Intensity = 1 },
                        new() { EffectType = SanityEffectType.Blindness, Duration = 0.25f, Intensity = 1 },
                        new() { EffectType = SanityEffectType.Deafened, Duration = 0.45f, Intensity = 1 }
                    }
                },
                new PlayerSanityStageConfig
                {
                    MinThreshold = 90f, MaxThreshold = 100f, DamageOnStrike = 4f, AdditionalDamagePerStack = 2f,
                    DamageOnStrikeWhenLightsourceActive = 0f, AdditionalDamagePerStackWhenLightsourceActive = 1f,
                    OverrideLightSourceSanityProtection = false,
                    Effects = new()
                    {
                        new() { EffectType = SanityEffectType.SilentWalk, Duration = 0.35f, Intensity = 2 },
                        new() { EffectType = SanityEffectType.Slowness, Duration = 0.5f, Intensity = 15 },
                        new() { EffectType = SanityEffectType.Blurred, Duration = 0.45f, Intensity = 1 },
                        new() { EffectType = SanityEffectType.Deafened, Duration = 0.25f, Intensity = 1 }
                    }
                }
            };

            foreach (var stage in SanityStages) stage.Validate();
        }
    }
}