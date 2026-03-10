namespace SCP_575.ConfigObjects
{
    using Exiled.API.Features;
    using System.Collections.Generic;
    using System.ComponentModel;

    /// <summary>
    /// Defines a stage of player sanity, including thresholds and which effects
    /// to apply when sanity falls within this range.
    /// </summary>
    public sealed class PlayerSanityStageConfig
    {
        [Description("Min sanity % to activate this stage.")]
        public float MinThreshold { get; set; }

        [Description("Max sanity % to activate this stage.")]
        public float MaxThreshold { get; set; }

        [Description("Damage to apply on SCP-575 strike at this sanity level.")]
        public float DamageOnStrike { get; set; }

        [Description("Additional damage to apply on SCP-575 strike at this sanity level per each active stack of the blackout event.")]
        public float AdditionalDamagePerStack { get; set; }

        [Description("Determines whether negative sanity effects should be applied even when the player is holding a lightsource in the room with lights off.")]
        public bool OverrideLightSourceSanityProtection { get; set; }

        [Description("List of effects to apply to the player during this sanity stage.")]
        public List<PlayerSanityEffectConfig> Effects { get; set; } = new();

        public void Validate()
        {
            if (DamageOnStrike < 0f)
            {
                Log.Warn($"[SanityStageConfig] DamageOnStrike cannot be negative for stage {MinThreshold}-{MaxThreshold}. Resetting to 0.");
                DamageOnStrike = 0f;
            }

            if (AdditionalDamagePerStack < 0f)
            {
                Log.Warn($"[SanityStageConfig] AdditionalDamagePerStack cannot be negative for stage {MinThreshold}-{MaxThreshold}. Resetting to 0.");
                AdditionalDamagePerStack = 0f;
            }

            if (Effects != null)
            {
                foreach (var effect in Effects)
                {
                    effect?.Validate();
                }
            }
        }
    }
}