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
        public float BaseDecayPerMinute { get; set; } = 5.65f;

        /// <summary>
        /// Gets or sets the additional decay multiplier applied to the baseline loss when SCP-575 is actively hunting during a blackout.
        /// </summary>
        [Description("Decay multiplier applied during an active SCP-575 Blackout event.")]
        public float DecayMultiplierBlackout { get; set; } = 1.65f;

        /// <summary>
        /// Gets or sets the harsh penalty multiplier applied when a player stands in a dark room without any personal light source active (Pitch Black state).
        /// </summary>
        [Description("Harsh decay multiplier applied when the player has NO active personal light source (flashlight/weapon light) in the dark.")]
        public float DecayMultiplierDarkness { get; set; } = 2.45f;

        /// <summary>
        /// Gets or sets the rate at which an actor passively recovers their sanity percentage per minute while standing inside safe, well-lit zones.
        /// </summary>
        [Description("How much sanity (0-100) a player passively regenerates PER MINUTE when inside safe, lit zones.")]
        public float PassiveRegenPerMinute { get; set; } = 3.38f;

        /// <summary>
        /// Gets or sets the discrete amount of sanity stripped instantly from an actor upon sustaining a direct physical attack from any SCP entity.
        /// </summary>
        [Description("Amount of sanity lost instantly when attacked/hit by any SCP entity.")]
        public float ScpHitSanityDrop { get; set; } = 4f;

        #endregion

        #region Medical Recovery Settings

        /// <summary>
        /// Gets or sets the minimum sanity percentage restored from consuming Painkillers.
        /// </summary>
        [Description("Minimum sanity restore percent from medical pills.")]
        public float PainkillersRestoreMin { get; set; } = 4f;

        /// <summary>
        /// Gets or sets the maximum sanity percentage restored from consuming Painkillers.
        /// </summary>
        [Description("Maximum sanity restore percent from medical pills.")]
        public float PainkillersRestoreMax { get; set; } = 12f;

        /// <summary>
        /// Gets or sets the additional flat sanity units injected into the passive regeneration routine per second while the actor is inside a lit room.
        /// </summary>
        [Description("Amount added to sanity per second if the player is in the bright room.")]
        public float PainkillersExtraSanityRegen { get; set; } = 0.65f;

        /// <summary>
        /// Gets or sets the lifespan threshold in seconds governing how long the amplified medical regeneration modifier remains active.
        /// </summary>
        [Description("Duration in seconds of the regen effect if the player is in the bright room.")]
        public float PainkillersRegenDuration { get; set; } = 13.5f;

        /// <summary>
        /// Gets or sets the lifespan threshold in seconds for the absolute protection shield. 
        /// While active, SCP-575 cannot inflict trauma, damage, or status afflictions onto the protected actor.
        /// </summary>
        [Description("Duration in seconds of the protection effect. SCP-575 will not deal any damage nor apply any effects to the player for this duration.")]
        public float PainkillersProtectionDuration { get; set; } = 3.25f;

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
        [Description("Decay rate per second is calculated, changing this value doesn't affect decay rate.")]
        public float DecayRateBase { get; private set; }

        /// <summary>
        /// Gets the pre-compiled baseline passive sanity recovery units processed per single execution tick (second).
        /// </summary>
        [Description("Regen rate per second is calculated, changing this value doesn't affect decay rate.")]
        public float PassiveRegenRate { get; private set; }

        #endregion

        #region Stage Thresholds and Effects

        /// <summary>
        /// Gets or sets the protection cooldown window in seconds to prevent multiple high-intensity sensory effects (like screen blur) from stacking up during rapid successive hits.
        /// </summary>
        [Description("Duration in seconds a player is protected from consecutive sensory effect bursts (e.g. blur spams) after the last burst sequence.")]
        public float EffectsBurstCooldown { get; set; } = 3.35f;

        /// <summary>
        /// Gets or sets the audio rate-limiting window in seconds to intercept sound distortion or clipping during rapid combat sequences.
        /// </summary>
        [Description("Cooldown in seconds between consecutive anomalous impact sound triggers on the same player.")]
        public float AttackAudioCooldownSeconds { get; set; } = 1.25f;

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
                DamageOnStrike = 4f,
                AdditionalDamagePerStack = 2f,
                DamageOnStrikeWhenLightsourceActive = 0f,
                AdditionalDamagePerStackWhenLightsourceActive = 1f,
                OverrideLightSourceSanityProtection = false,
                Effects = new()
                {
                    new() { EffectType = SanityEffectType.SilentWalk, Duration = 0.35f, Intensity = 2 },
                    new() { EffectType = SanityEffectType.Slowness, Duration = 0.5f, Intensity = 15 },
                    new() { EffectType = SanityEffectType.Blurred, Duration = 0.45f, Intensity = 1 },
                    new() { EffectType = SanityEffectType.Deafened, Duration = 0.25f, Intensity = 1 },
                }
            },
            new PlayerSanityStageConfig
            {
                MinThreshold = 75f,
                MaxThreshold = 90f,
                DamageOnStrike = 8f,
                AdditionalDamagePerStack = 3f,
                DamageOnStrikeWhenLightsourceActive = 2f,
                AdditionalDamagePerStackWhenLightsourceActive = 2f,
                OverrideLightSourceSanityProtection = false,
                Effects = new()
                {
                    new() { EffectType = SanityEffectType.SilentWalk, Duration = 0.65f, Intensity = 4 },
                    new() { EffectType = SanityEffectType.Slowness, Duration = 0.85f, Intensity = 25 },
                    new() { EffectType = SanityEffectType.Blurred, Duration = 0.75f, Intensity = 1 },
                    new() { EffectType = SanityEffectType.Blindness, Duration = 0.25f, Intensity = 1 },
                    new() { EffectType = SanityEffectType.Deafened, Duration = 0.45f, Intensity = 1 },
                }
            },
            new PlayerSanityStageConfig
            {
                MinThreshold = 50f,
                MaxThreshold = 75f,
                DamageOnStrike = 12f,
                AdditionalDamagePerStack = 4f,
                DamageOnStrikeWhenLightsourceActive = 4f,
                AdditionalDamagePerStackWhenLightsourceActive = 3f,
                OverrideLightSourceSanityProtection = false,
                Effects = new()
                {
                    new() { EffectType = SanityEffectType.SilentWalk, Duration = 1.15f, Intensity = 6 },
                    new() { EffectType = SanityEffectType.Slowness, Duration = 1.35f, Intensity = 35 },
                    new() { EffectType = SanityEffectType.Blurred, Duration = 1.25f, Intensity = 1 },
                    new() { EffectType = SanityEffectType.Blindness, Duration = 0.35f, Intensity = 1 },
                    new() { EffectType = SanityEffectType.Deafened, Duration = 0.95f, Intensity = 1 },
                }
            },
            new PlayerSanityStageConfig
            {
                MinThreshold = 25f,
                MaxThreshold = 50f,
                DamageOnStrike = 20f,
                AdditionalDamagePerStack = 6f,
                DamageOnStrikeWhenLightsourceActive = 5f,
                AdditionalDamagePerStackWhenLightsourceActive = 4f,
                OverrideLightSourceSanityProtection = false,
                Effects = new()
                {
                    new() { EffectType = SanityEffectType.SilentWalk, Duration = 1.45f, Intensity = 8 },
                    new() { EffectType = SanityEffectType.Slowness, Duration = 1.85f, Intensity = 45 },
                    new() { EffectType = SanityEffectType.Exhausted, Duration = 0.25f, Intensity = 1 },
                    new() { EffectType = SanityEffectType.Blurred, Duration = 1.65f, Intensity = 1 },
                    new() { EffectType = SanityEffectType.Blindness, Duration = 0.55f, Intensity = 1 },
                    new() { EffectType = SanityEffectType.Deafened, Duration = 1.45f, Intensity = 1 },
                    new() { EffectType = SanityEffectType.Flashed, Duration = 0.25f, Intensity = 1 },
                }
            },
            new PlayerSanityStageConfig
            {
                MinThreshold = 10f,
                MaxThreshold = 25f,
                DamageOnStrike = 30f,
                AdditionalDamagePerStack = 9f,
                DamageOnStrikeWhenLightsourceActive = 8f,
                AdditionalDamagePerStackWhenLightsourceActive = 6f,
                OverrideLightSourceSanityProtection = false,
                Effects = new()
                {
                    new() { EffectType = SanityEffectType.SilentWalk, Duration = 1.85f, Intensity = 9 },
                    new() { EffectType = SanityEffectType.Slowness, Duration = 2.15f, Intensity = 60 },
                    new() { EffectType = SanityEffectType.Exhausted, Duration = 0.35f, Intensity = 1 },
                    new() { EffectType = SanityEffectType.Blurred, Duration = 1.95f, Intensity = 1 },
                    new() { EffectType = SanityEffectType.Blindness, Duration = 0.75f, Intensity = 1 },
                    new() { EffectType = SanityEffectType.Deafened, Duration = 1.75f, Intensity = 1 },
                    new() { EffectType = SanityEffectType.Flashed, Duration = 0.35f, Intensity = 1 },
                }
            },
            new PlayerSanityStageConfig
            {
                MinThreshold = 0f,
                MaxThreshold = 10f,
                DamageOnStrike = 45f,
                AdditionalDamagePerStack = 15f,
                DamageOnStrikeWhenLightsourceActive = 10f,
                AdditionalDamagePerStackWhenLightsourceActive = 8f,
                OverrideLightSourceSanityProtection = true,
                Effects = new()
                {
                    new() { EffectType = SanityEffectType.SilentWalk, Duration = 3.05f, Intensity = 10 },
                    new() { EffectType = SanityEffectType.Slowness, Duration = 3.15f, Intensity = 75 },
                    new() { EffectType = SanityEffectType.Exhausted, Duration = 0.65f, Intensity = 1 },
                    new() { EffectType = SanityEffectType.Blurred, Duration = 2.65f, Intensity = 1 },
                    new() { EffectType = SanityEffectType.Blindness, Duration = 1.25f, Intensity = 1 },
                    new() { EffectType = SanityEffectType.Deafened, Duration = 2.75f, Intensity = 1 },
                    new() { EffectType = SanityEffectType.Flashed, Duration = 0.55f, Intensity = 1 },
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

            if (Scp500RestoreMin < 0f) Scp500RestoreMin = 0f;
            if (Scp500RestoreMax < 0f) Scp500RestoreMax = 0f;

            if (Scp500RestoreMin > Scp500RestoreMax)
            {
                float temp = Scp500RestoreMin;
                Scp500RestoreMin = Scp500RestoreMax;
                Scp500RestoreMax = temp;
                Logger.Warn("[PlayerSanityConfig] Scp500RestoreMin was greater than Scp500RestoreMax. Values have been swapped.");
            }

            if (EffectsBurstCooldown < 0f) EffectsBurstCooldown = 0f;
            if (AttackAudioCooldownSeconds < 0f) AttackAudioCooldownSeconds = 0f;

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