namespace SCP_575.ConfigObjects
{
    using CustomPlayerEffects;
    using System.Collections.Generic;
    using System.ComponentModel;

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
        public float DecayRateBase { get; set; } = 0.22f;

        /// <summary>
        /// Additional decay multiplier applied when SCP-575 is active (i.e. during blackout).
        /// </summary>
        [Description("Decay multiplier when SCP-575 is active.")]
        public float DecayMultiplierBlackout { get; set; } = 1.75f;

        /// <summary>
        /// Extra multiplier applied when player is in darkness (without active light source).
        /// </summary>
        [Description("Decay multiplier when player has no light source.")]
        public float DecayMultiplierDarkness { get; set; } = 2.0f;

        /// <summary>
        /// Amount of sanity regained passively per second outside blackout or danger zones.
        /// </summary>
        [Description("Passive sanity regen rate per second.")]
        public float PassiveRegenRate { get; set; } = 0.05f;

        #endregion

        #region Medical Recovery Settings

        /// <summary>
        /// Minimum sanity percentage restored from consuming Painkillers.
        /// </summary>
        [Description("Minimum sanity restore percent from medical pills.")]
        public float PillsRestoreMin { get; set; } = 3f;

        /// <summary>
        /// Maximum sanity percentage restored from consuming Painkillers.
        /// </summary>
        [Description("Maximum sanity restore percent from medical pills.")]
        public float PillsRestoreMax { get; set; } = 14f;

        /// <summary>
        /// Minimum sanity percentage restored by SCP-500 pills.
        /// </summary>
        [Description("Minimum sanity restore percent from SCP-500.")]
        public float SCP500RestoreMin { get; set; } = 75f;

        /// <summary>
        /// Maximum sanity percentage restored by SCP-500 pills.
        /// </summary>
        [Description("Maximum sanity restore percent from SCP-500.")]
        public float SCP500RestoreMax { get; set; } = 100f;

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
                MinThreshold = 75f,
                MaxThreshold = 100f,
                DamageOnStrike = 0f,
                Effects = new()
                {
                    new() { EffectType = typeof(SilentWalk), Duration = 5f, Intensity = 3 },
                    new() { EffectType = typeof(Slowness), Duration = 1.25f, Intensity = 30 },
                    new() { EffectType = typeof(Disabled), Duration = 5f},
                    new() { EffectType = typeof(Traumatized), Duration = 5f},
                    new() { EffectType = typeof(Blurred), Duration = 1f},
                    new() { EffectType = typeof(Concussed), Duration = 2f},
                    new() { EffectType = typeof(Blindness), Duration = 0.45f},
                    new() { EffectType = typeof(Deafened), Duration = 0.55f},
                    new() { EffectType = typeof(Flashed), Duration = 0.15f },
                }
            },
            new PlayerSanityStageConfig
            {
                MinThreshold = 50f,
                MaxThreshold = 75f,
                DamageOnStrike = 5f,
                Effects = new()
                {
                    new() { EffectType = typeof(SilentWalk), Duration = 5f, Intensity = 7 },
                    new() { EffectType = typeof(Slowness), Duration = 1.65f, Intensity = 40 },
                    new() { EffectType = typeof(Disabled), Duration = 7f},
                    new() { EffectType = typeof(Traumatized), Duration = 7f},
                    new() { EffectType = typeof(Exhausted), Duration = 1.5f},
                    new() { EffectType = typeof(Blurred), Duration = 2f},
                    new() { EffectType = typeof(Concussed), Duration = 3f},
                    new() { EffectType = typeof(Blindness), Duration = 0.75f},
                    new() { EffectType = typeof(Deafened), Duration = 1.25f},
                    new() { EffectType = typeof(Flashed), Duration = 0.25f },
                }
            },
            new PlayerSanityStageConfig
            {
                MinThreshold = 25f,
                MaxThreshold = 50f,
                DamageOnStrike = 12f,
                Effects = new()
                {
                    new() { EffectType = typeof(SilentWalk), Duration = 5f, Intensity = 9 },
                    new() { EffectType = typeof(Slowness), Duration = 2.45f, Intensity = 60 },
                    new() { EffectType = typeof(Disabled), Duration = 10f},
                    new() { EffectType = typeof(Traumatized), Duration = 10f},
                    new() { EffectType = typeof(Exhausted), Duration = 2.5f},
                    new() { EffectType = typeof(Blurred), Duration = 5f},
                    new() { EffectType = typeof(Concussed), Duration = 8f},
                    new() { EffectType = typeof(Blindness), Duration = 1.25f},
                    new() { EffectType = typeof(Deafened), Duration = 2.25f},
                    new() { EffectType = typeof(Flashed), Duration = 0.35f },


                }
            },
            new PlayerSanityStageConfig
            {
                MinThreshold = 0f,
                MaxThreshold = 25f,
                DamageOnStrike = 25f,
                Effects = new()
                {
                    new() { EffectType = typeof(SilentWalk), Duration = 5f, Intensity = 10 },
                    new() { EffectType = typeof(Slowness), Duration = 3.25f, Intensity = 70 },
                    new() { EffectType = typeof(Disabled), Duration = 20f},
                    new() { EffectType = typeof(Traumatized), Duration = 20f},
                    new() { EffectType = typeof(Exhausted), Duration = 4.75f},
                    new() { EffectType = typeof(Blurred), Duration = 7f},
                    new() { EffectType = typeof(Concussed), Duration = 10f},
                    new() { EffectType = typeof(Blindness), Duration = 1.45f},
                    new() { EffectType = typeof(Deafened), Duration = 3.5f},
                    new() { EffectType = typeof(Flashed), Duration = 0.65f },
                }
            },
        };

        #endregion
    }
}