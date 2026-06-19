namespace SCP_575.ConfigObjects
{
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

        /// <summary>
        /// Gets or sets the initial sanity percentage assigned to a human actor upon spawning.
        /// </summary>
        [Description("Initial sanity value (0–100) on spawn.")]
        public float InitialSanity { get; set; } = 100f;

        /// <summary>
        /// Gets or sets the baseline rate at which an actor's sanity naturally decays per minute under standard conditions.
        /// </summary>
        [Description("How much sanity (0-100) a player naturally loses PER MINUTE in baseline conditions.")]
        public float BaseDecayPerMinute { get; set; } = 7.5f;

        /// <summary>
        /// Gets or sets the additional decay multiplier applied to the baseline loss when SCP-575 is actively hunting during a blackout.
        /// </summary>
        [Description("Decay multiplier applied during an active SCP-575 Blackout event.")]
        public float DecayMultiplierBlackout { get; set; } = 1.5f; // 50% faster decay during global blackout

        /// <summary>
        /// Gets or sets the harsh penalty multiplier applied when a player stands in a dark room without any personal light source active (Pitch Black state).
        /// </summary>
        [Description("Harsh decay multiplier applied when the player has NO active personal light source (flashlight/weapon light) in the dark.")]
        public float DecayMultiplierDarkness { get; set; } = 2.25f; // 125% faster decay if completely blind in the dark

        /// <summary>
        /// Gets or sets the rate at which an actor passively recovers their sanity percentage per minute while standing inside safe, well-lit zones.
        /// </summary>
        [Description("How much sanity (0-100) a player passively regenerates PER MINUTE when inside safe, lit zones.")]
        public float PassiveRegenPerMinute { get; set; } = 3.55f;

        /// <summary>
        /// Gets or sets the discrete amount of sanity stripped instantly from an actor upon sustaining a direct physical attack from any SCP entity.
        /// </summary>
        [Description("Amount of sanity lost instantly when attacked/hit by any SCP entity.")]
        public float ScpHitSanityDrop { get; set; } = 3f;

        #endregion

        #region Medical Recovery Settings

        /// <summary>
        /// Gets or sets the minimum sanity percentage restored from consuming Painkillers.
        /// </summary>
        [Description("Minimum sanity restore percent from medical pills.")]
        public float PainkillersRestoreMin { get; set; } = 5f;

        /// <summary>
        /// Gets or sets the maximum sanity percentage restored from consuming Painkillers.
        /// </summary>
        [Description("Maximum sanity restore percent from medical pills.")]
        public float PainkillersRestoreMax { get; set; } = 15f;

        /// <summary>
        /// Gets or sets the additional flat sanity units injected into the passive regeneration routine per second while the actor is inside a lit room.
        /// </summary>
        [Description("Amount added to sanity per second if the player is in the bright room.")]
        public float PainkillersExtraSanityRegen { get; set; } = 0.5f;

        /// <summary>
        /// Gets or sets the lifespan threshold in seconds governing how long the amplified medical regeneration modifier remains active.
        /// </summary>
        [Description("Duration in seconds of the regen effect if the player is in the bright room.")]
        public float PainkillersRegenDuration { get; set; } = 12.5f;

        /// <summary>
        /// Gets or sets the lifespan threshold in seconds for the absolute protection shield. 
        /// While active, SCP-575 cannot inflict trauma, damage, or status afflictions onto the protected actor.
        /// </summary>
        [Description("Duration in seconds of the protection effect. SCP-575 will not deal any damage nor apply any effects to the player for this duration.")]
        public float PainkillersProtectionDuration { get; set; } = 3.25f;

        /// <summary>
        /// Gets or sets the maximum fallback sanity percentage allowed from standard medical pill categories.
        /// </summary>
        [Description("Maximum sanity restore percent from medical pills.")]
        public float PillsRestoreMax { get; set; } = 25f;

        /// <summary>
        /// Gets or sets the minimum sanity percentage restored by SCP-500 pills.
        /// </summary>
        [Description("Minimum sanity restore percent from SCP-500.")]
        public float Scp500RestoreMin { get; set; } = 85f;

        /// <summary>
        /// Gets or sets the maximum sanity percentage restored by SCP-500 pills.
        /// </summary>
        [Description("Maximum sanity restore percent from SCP-500.")]
        public float Scp500RestoreMax { get; set; } = 100f;

        #endregion

        #region Runtime Backing Fields (Pre-calculated for performance)

        /// <summary>
        /// Gets the pre-compiled baseline sanity decay units processed per single execution tick (second).
        /// </summary>
        public float DecayRateBase { get; private set; }

        /// <summary>
        /// Gets the pre-compiled baseline passive sanity recovery units processed per single execution tick (second).
        /// </summary>
        public float PassiveRegenRate { get; private set; }

        #endregion

        #region Stage Thresholds and Effects

        /// <summary>
        /// Gets or sets the list of sanity stages determining structural impairment profiles based on the player's remaining sanity range.
        /// </summary>
        [Description("Stages of sanity and their associated effects.")]
        public List<PlayerSanityStageConfig> SanityStages { get; set; } = new()
        {
            new PlayerSanityStageConfig
            {
                MinThreshold = 90f,
                MaxThreshold = 100f,
                DamageOnStrike = 1f,
                AdditionalDamagePerStack = 2f,
                DamageOnStrikeWhenLightsourceActive = 0f,
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
        /// Validates the player sanity configuration parameters and compiles intuitive human-readable metrics into high-speed runtime decimals.
        /// </summary>
        public void Validate()
        {
            InitialSanity = Mathf.Clamp(InitialSanity, 0f, 100f);

            if (BaseDecayPerMinute < 0f) BaseDecayPerMinute = 0f;
            if (PassiveRegenPerMinute < 0f) PassiveRegenPerMinute = 0f;
            if (DecayMultiplierBlackout < 0f) DecayMultiplierBlackout = 1f;
            if (DecayMultiplierDarkness < 0f) DecayMultiplierDarkness = 1f;

            // Compile high-level per-minute metrics into high-performance second-based decimals for core threads
            DecayRateBase = BaseDecayPerMinute / 60f;
            PassiveRegenRate = PassiveRegenPerMinute / 60f;

            if (PainkillersRestoreMin < 0f) PainkillersRestoreMin = 0f;
            if (PainkillersRestoreMax < 0f) PainkillersRestoreMax = 0f;

            if (PainkillersRestoreMin > PainkillersRestoreMax)
            {
                float temp = PainkillersRestoreMin;
                PainkillersRestoreMin = PainkillersRestoreMax;
                PainkillersRestoreMax = temp;
                Logger.Warn("[PlayerSanityConfig] PainkillersRestoreMin was greater than PainkillersRestoreMax. Values have been swapped.");
            }

            if (PillsRestoreMax < 0f) PillsRestoreMax = 0f;

            if (Scp500RestoreMin < 0f) Scp500RestoreMin = 0f;
            if (Scp500RestoreMax < 0f) Scp500RestoreMax = 0f;

            if (Scp500RestoreMin > Scp500RestoreMax)
            {
                float temp = Scp500RestoreMin;
                Scp500RestoreMin = Scp500RestoreMax;
                Scp500RestoreMax = temp;
                Logger.Warn("[PlayerSanityConfig] Scp500RestoreMin was greater than Scp500RestoreMax. Values have been swapped.");
            }

            if (SanityStages == null || SanityStages.Count == 0)
            {
                throw new System.InvalidOperationException("[PlayerSanityConfig] SanityStages list cannot be null or empty.");
            }

            if (ScpHitSanityDrop < 0f) ScpHitSanityDrop = 0f;

            // Sort stages by minimum threshold ascending to validate sequential coverage
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