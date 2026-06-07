namespace SCP_575.ConfigObjects
{
    using CustomPlayerEffects;
    using Exiled.API.Features;
    using System.Collections.Generic;
    using System.ComponentModel;
    using UnityEngine;

    /// <summary>
    /// Configuration for player sanity system behavior throughout the round.
    /// Controls decay, regeneration, medical recovery, and stage-based visual/physical effects.
    /// </summary>
    public sealed class PlayerSanityConfig
    {
        #region Sanity Flow Settings

        /// <summary>
        /// Initial sanity percentage assigned when the player spawns.
        /// </summary>
        [Description("Initial sanity value (0–100) on spawn.")]
        public float InitialSanity { get; set; } = 100f;

        /// <summary>
        /// Base rate at which sanity naturally decays per second.
        /// </summary>
        [Description("Base sanity decay rate per second.")]
        public float DecayRateBase { get; set; } = 0.1075f;

        /// <summary>
        /// Additional decay multiplier applied when SCP-575 is active (i.e. during blackout).
        /// </summary>
        [Description("Decay multiplier when SCP-575 is active.")]
        public float DecayMultiplierBlackout { get; set; } = 1.33f;

        /// <summary>
        /// Extra multiplier applied when player is in darkness (without active light source).
        /// </summary>
        [Description("Decay multiplier when player has no light source.")]
        public float DecayMultiplierDarkness { get; set; } = 1.55f;

        /// <summary>
        /// Amount of sanity regained passively per second outside blackout or danger zones.
        /// </summary>
        [Description("Passive sanity regen rate per second.")]
        public float PassiveRegenRate { get; set; } = 0.081f;

        /// <summary>
        /// Gets or sets the amount of sanity lost immediately upon taking damage from any SCP entity.
        /// </summary>
        [Description("Amount of sanity lost instantly when attacked/hit by any SCP entity.")]
        public float ScpHitSanityDrop { get; set; } = 8f;

        #endregion

        #region Medical Recovery Settings

        /// <summary>
        /// Minimum sanity percentage restored from consuming Painkillers.
        /// </summary>
        [Description("Minimum sanity restore percent from medical pills.")]
        public float PillsRestoreMin { get; set; } = 15f;

        /// <summary>
        /// Maximum sanity percentage restored from consuming Painkillers.
        /// </summary>
        [Description("Maximum sanity restore percent from medical pills.")]
        public float PillsRestoreMax { get; set; } = 45f;

        /// <summary>
        /// Minimum sanity percentage restored by SCP-500 pills.
        /// </summary>
        [Description("Minimum sanity restore percent from SCP-500.")]
        public float Scp500RestoreMin { get; set; } = 85f;

        /// <summary>
        /// Maximum sanity percentage restored by SCP-500 pills.
        /// </summary>
        [Description("Maximum sanity restore percent from SCP-500.")]
        public float Scp500RestoreMax { get; set; } = 100f;

        #endregion

        #region Stage Thresholds and Effects

        /// <summary>
        /// List of sanity stages determining player effects based on their sanity range.
        /// </summary>
        [Description("Stages of sanity and their associated effects.")]
        public List<PlayerSanityStageConfig> SanityStages { get; set; } = new()
        {
            new PlayerSanityStageConfig
            {
                MinThreshold = 90f,
                MaxThreshold = 100f,
                DamageOnStrike = 0f,
                AdditionalDamagePerStack = 0f,
                DamageOnStrikeWhenLightsourceActive = 0f,
                AdditionalDamagePerStackWhenLightsourceActive = 0f,
                OverrideLightSourceSanityProtection = false,
                Effects = new()
                {
                    new() { EffectType = SanityEffectType.SilentWalk, Duration = 5f, Intensity = 3 },
                    new() { EffectType = SanityEffectType.Slowness, Duration = 1.25f, Intensity = 30 },
                    new() { EffectType = SanityEffectType.Blurred, Duration = 1f, Intensity = 1 },
                    new() { EffectType = SanityEffectType.Concussed, Duration = 2f, Intensity = 1 },
                    new() { EffectType = SanityEffectType.Blindness, Duration = 0.45f, Intensity = 1 },
                    new() { EffectType = SanityEffectType.Deafened, Duration = 0.55f, Intensity = 1 },
                    new() { EffectType = SanityEffectType.Flashed, Duration = 0.15f, Intensity = 1 },
                }
            },
            new PlayerSanityStageConfig
            {
                MinThreshold = 75f,
                MaxThreshold = 90f,
                DamageOnStrike = 3f,
                AdditionalDamagePerStack = 2f,
                DamageOnStrikeWhenLightsourceActive = 2f,
                AdditionalDamagePerStackWhenLightsourceActive = 1f,
                OverrideLightSourceSanityProtection = false,
                Effects = new()
                {
                    new() { EffectType = SanityEffectType.SilentWalk, Duration = 5f, Intensity = 3 },
                    new() { EffectType = SanityEffectType.Slowness, Duration = 1.25f, Intensity = 30 },
                    new() { EffectType = SanityEffectType.Blurred, Duration = 1f, Intensity = 1 },
                    new() { EffectType = SanityEffectType.Concussed, Duration = 2f, Intensity = 1 },
                    new() { EffectType = SanityEffectType.Blindness, Duration = 0.45f, Intensity = 1 },
                    new() { EffectType = SanityEffectType.Deafened, Duration = 0.55f, Intensity = 1 },
                    new() { EffectType = SanityEffectType.Flashed, Duration = 0.15f, Intensity = 1 },
                }
            },
            new PlayerSanityStageConfig
            {
                MinThreshold = 50f,
                MaxThreshold = 75f,
                DamageOnStrike = 5f,
                AdditionalDamagePerStack = 3f,
                DamageOnStrikeWhenLightsourceActive = 3f,
                AdditionalDamagePerStackWhenLightsourceActive = 2f,
                OverrideLightSourceSanityProtection = false,
                Effects = new()
                {
                    new() { EffectType = SanityEffectType.SilentWalk, Duration = 5f, Intensity = 7 },
                    new() { EffectType = SanityEffectType.Slowness, Duration = 1.65f, Intensity = 40 },
                    new() { EffectType = SanityEffectType.Disabled, Duration = 7f, Intensity = 1 },
                    new() { EffectType = SanityEffectType.Traumatized, Duration = 7f, Intensity = 1 },
                    new() { EffectType = SanityEffectType.Exhausted, Duration = 1.5f, Intensity = 1 },
                    new() { EffectType = SanityEffectType.Blurred, Duration = 2f, Intensity = 1 },
                    new() { EffectType = SanityEffectType.Concussed, Duration = 3f, Intensity = 1 },
                    new() { EffectType = SanityEffectType.Blindness, Duration = 0.75f, Intensity = 1 },
                    new() { EffectType = SanityEffectType.Deafened, Duration = 1.25f, Intensity = 1 },
                    new() { EffectType = SanityEffectType.Flashed, Duration = 0.25f, Intensity = 1 },
                }
            },
            new PlayerSanityStageConfig
            {
                MinThreshold = 25f,
                MaxThreshold = 50f,
                DamageOnStrike = 6f,
                AdditionalDamagePerStack = 5f,
                DamageOnStrikeWhenLightsourceActive = 4f,
                AdditionalDamagePerStackWhenLightsourceActive = 3f,
                OverrideLightSourceSanityProtection = false,
                Effects = new()
                {
                    new() { EffectType = SanityEffectType.SilentWalk, Duration = 5f, Intensity = 9 },
                    new() { EffectType = SanityEffectType.Slowness, Duration = 2.45f, Intensity = 55 },
                    new() { EffectType = SanityEffectType.Disabled, Duration = 9f, Intensity = 1 },
                    new() { EffectType = SanityEffectType.Traumatized, Duration = 9f, Intensity = 1 },
                    new() { EffectType = SanityEffectType.Exhausted, Duration = 2.5f, Intensity = 1 },
                    new() { EffectType = SanityEffectType.Blurred, Duration = 5f, Intensity = 1 },
                    new() { EffectType = SanityEffectType.Concussed, Duration = 8f, Intensity = 1 },
                    new() { EffectType = SanityEffectType.Blindness, Duration = 1.25f, Intensity = 1 },
                    new() { EffectType = SanityEffectType.Deafened, Duration = 2.25f, Intensity = 1 },
                    new() { EffectType = SanityEffectType.Flashed, Duration = 0.35f, Intensity = 1 },
                }
            },
            new PlayerSanityStageConfig
            {
                MinThreshold = 10f,
                MaxThreshold = 25f,
                DamageOnStrike = 8f,
                AdditionalDamagePerStack = 7f,
                DamageOnStrikeWhenLightsourceActive = 5f,
                AdditionalDamagePerStackWhenLightsourceActive = 5f,
                OverrideLightSourceSanityProtection = false,
                Effects = new()
                {
                    new() { EffectType = SanityEffectType.SilentWalk, Duration = 5f, Intensity = 10 },
                    new() { EffectType = SanityEffectType.Slowness, Duration = 3.25f, Intensity = 70 },
                    new() { EffectType = SanityEffectType.Disabled, Duration = 15f, Intensity = 1 },
                    new() { EffectType = SanityEffectType.Traumatized, Duration = 15f, Intensity = 1 },
                    new() { EffectType = SanityEffectType.Exhausted, Duration = 4.75f, Intensity = 1 },
                    new() { EffectType = SanityEffectType.Blurred, Duration = 7f, Intensity = 1 },
                    new() { EffectType = SanityEffectType.Concussed, Duration = 10f, Intensity = 1 },
                    new() { EffectType = SanityEffectType.Blindness, Duration = 1.45f, Intensity = 1 },
                    new() { EffectType = SanityEffectType.Deafened, Duration = 3.5f, Intensity = 1 },
                    new() { EffectType = SanityEffectType.Flashed, Duration = 0.65f, Intensity = 1 },
                }
            },
            new PlayerSanityStageConfig
            {
                MinThreshold = 0f,
                MaxThreshold = 10f,
                DamageOnStrike = 9f,
                AdditionalDamagePerStack = 8f,
                DamageOnStrikeWhenLightsourceActive = 6f,
                AdditionalDamagePerStackWhenLightsourceActive = 6f,
                OverrideLightSourceSanityProtection = true,
                Effects = new()
                {
                    new() { EffectType = SanityEffectType.SilentWalk, Duration = 5f, Intensity = 10 },
                    new() { EffectType = SanityEffectType.Slowness, Duration = 3.25f, Intensity = 70 },
                    new() { EffectType = SanityEffectType.Disabled, Duration = 15f, Intensity = 1 },
                    new() { EffectType = SanityEffectType.Traumatized, Duration = 15f, Intensity = 1 },
                    new() { EffectType = SanityEffectType.Exhausted, Duration = 4.75f, Intensity = 1 },
                    new() { EffectType = SanityEffectType.Blurred, Duration = 7f, Intensity = 1 },
                    new() { EffectType = SanityEffectType.Concussed, Duration = 10f, Intensity = 1 },
                    new() { EffectType = SanityEffectType.Blindness, Duration = 1.45f, Intensity = 1 },
                    new() { EffectType = SanityEffectType.Deafened, Duration = 3.5f, Intensity = 1 },
                    new() { EffectType = SanityEffectType.Flashed, Duration = 0.65f, Intensity = 1 },
                }
            },
        };

        #endregion

        /// <summary>
        /// Validates the player sanity configuration parameters and corrects invalid input.
        /// </summary>
        public void Validate()
        {
            InitialSanity = Mathf.Clamp(InitialSanity, 0f, 100f);

            if (DecayRateBase < 0f) DecayRateBase = 0f;
            if (DecayMultiplierBlackout < 0f) DecayMultiplierBlackout = 0f;
            if (DecayMultiplierDarkness < 0f) DecayMultiplierDarkness = 0f;
            if (PassiveRegenRate < 0f) PassiveRegenRate = 0f;

            if (PillsRestoreMin < 0f) PillsRestoreMin = 0f;
            if (PillsRestoreMax < 0f) PillsRestoreMax = 0f;

            if (PillsRestoreMin > PillsRestoreMax)
            {
                float temp = PillsRestoreMin;
                PillsRestoreMin = PillsRestoreMax;
                PillsRestoreMax = temp;
                Log.Warn("[PlayerSanityConfig] PillsRestoreMin was greater than PillsRestoreMax. Values have been swapped.");
            }

            if (Scp500RestoreMin < 0f) Scp500RestoreMin = 0f;
            if (Scp500RestoreMax < 0f) Scp500RestoreMax = 0f;

            if (Scp500RestoreMin > Scp500RestoreMax)
            {
                float temp = Scp500RestoreMin;
                Scp500RestoreMin = Scp500RestoreMax;
                Scp500RestoreMax = temp;
                Log.Warn("[PlayerSanityConfig] Scp500RestoreMin was greater than Scp500RestoreMax. Values have been swapped.");
            }

            if (SanityStages == null || SanityStages.Count == 0)
            {
                throw new System.InvalidOperationException("[PlayerSanityConfig] SanityStages list cannot be null or empty.");
            }

            if (ScpHitSanityDrop < 0f) ScpHitSanityDrop = 0f;

            // Sort stages by minimum threshold ascending to validate range coverage
            SanityStages.Sort((a, b) => a.MinThreshold.CompareTo(b.MinThreshold));

            if (SanityStages[0].MinThreshold > 0f || SanityStages[SanityStages.Count - 1].MaxThreshold < 100f)
            {
                throw new System.InvalidOperationException("[PlayerSanityConfig] SanityStages do not cover the full range from 0 to 100.");
            }

            for (int i = 0; i < SanityStages.Count - 1; i++)
            {
                if (SanityStages[i].MaxThreshold != SanityStages[i + 1].MinThreshold)
                {
                    throw new System.InvalidOperationException($"[PlayerSanityConfig] SanityStages have gaps or overlaps between {SanityStages[i].MaxThreshold} and {SanityStages[i + 1].MinThreshold}.");
                }
            }

            foreach (var stage in SanityStages) { stage?.Validate(); }

        }
    }
}