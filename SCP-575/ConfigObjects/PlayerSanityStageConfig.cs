namespace SCP_575.ConfigObjects
{
    using System.Collections.Generic;
    using System.ComponentModel;
    using Logger = SCP_575.Shared.LibraryLabAPI;
    using UnityEngine;

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

        [Description("Damage to apply on SCP-575 strike at this sanity level when the player is holding a lightsource ON in the room with lights off.")]
        public float DamageOnStrikeWhenLightsourceActive { get; set; }

        [Description("Additional damage to apply on SCP-575 strike at this sanity level per each active stack of the blackout event when the player is holding a lightsource ON in the room with lights off.")]
        public float AdditionalDamagePerStackWhenLightsourceActive { get; set; }

        [Description("Determines whether negative sanity effects and full damage should be applied even when the player is holding a lightsource in the room with lights off.")]
        public bool OverrideLightSourceSanityProtection { get; set; }

        [Description("List of effects to apply to the player during this sanity stage.")]
        public List<PlayerSanityEffectConfig> Effects { get; set; } = new();

        public void Validate()
        {
            DamageOnStrike = Mathf.Max(0f, DamageOnStrike);
            AdditionalDamagePerStack = Mathf.Max(0f, AdditionalDamagePerStack);

            if (DamageOnStrikeWhenLightsourceActive < 0f)
            {
                Logger.LogWarn(nameof(PlayerSanityStageConfig), $"DamageOnStrikeWhenLightsourceActive cannot be negative for stage {MinThreshold}-{MaxThreshold}. Resetting to 0.");
                DamageOnStrikeWhenLightsourceActive = 0f;
            }
            else if (DamageOnStrikeWhenLightsourceActive > DamageOnStrike)
            {
                Logger.LogWarn(nameof(PlayerSanityStageConfig), $"DamageOnStrikeWhenLightsourceActive cannot be greater than DamageOnStrike for stage {MinThreshold}-{MaxThreshold}. Adjusting to equal DamageOnStrike.");
                DamageOnStrikeWhenLightsourceActive = DamageOnStrike;
            }

            if (AdditionalDamagePerStackWhenLightsourceActive < 0f)
            {
                Logger.LogWarn(nameof(PlayerSanityStageConfig), $"AdditionalDamagePerStackWhenLightsourceActive cannot be negative for stage {MinThreshold}-{MaxThreshold}. Resetting to 0.");
                AdditionalDamagePerStackWhenLightsourceActive = 0f;
            }
            else if (AdditionalDamagePerStackWhenLightsourceActive > AdditionalDamagePerStack)
            {
                Logger.LogWarn(nameof(PlayerSanityStageConfig), $"AdditionalDamagePerStackWhenLightsourceActive cannot be greater than AdditionalDamagePerStack for stage {MinThreshold}-{MaxThreshold}. Adjusting to equal AdditionalDamagePerStack.");
                AdditionalDamagePerStackWhenLightsourceActive = AdditionalDamagePerStack;
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