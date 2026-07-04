using LabApi.Extensions;
using System.Collections.Generic;
using System.ComponentModel;
using Logger = LabApi.Extensions.Misc.iLogger;

namespace SCP_575.ConfigObjects
{
    /// <summary>
    /// Defines an operational stage of player sanity, outlining threshold boundaries 
    /// and specific sensory status effects applied when sanity tracking drops within this range.
    /// </summary>
    public sealed class PlayerSanityStageConfig
    {
        #region Structural Thresholds & Modifiers
        [Description("Min sanity percentage required to activate this stage boundary.")]
        public float MinThreshold { get; set; }

        [Description("Max sanity percentage required to activate this stage boundary.")]
        public float MaxThreshold { get; set; }

        [Description("Base damage applied on a direct SCP-575 strike sequence at this sanity level.")]
        public float DamageOnStrike { get; set; }

        [Description("Additional damage applied on an SCP-575 strike sequence per each active stack of the blackout event.")]
        public float AdditionalDamagePerStack { get; set; }

        [Description("Damage applied on an SCP-575 strike sequence when the victim is holding an active light source in a dark room.")]
        public float DamageOnStrikeWhenLightsourceActive { get; set; }

        [Description("Additional stack damage applied on an SCP-575 strike sequence when the victim is holding an active light source in a dark room.")]
        public float AdditionalDamagePerStackWhenLightsourceActive { get; set; }

        [Description("If set to true, negative sanity effects and full strike damage ignore personal light source protections.")]
        public bool OverrideLightSourceSanityProtection { get; set; }

        [Description("List of distinct status effect configurations applied to the player during this tracking stage.")]
        public List<PlayerSanityEffectConfig> Effects { get; set; } = new();
        #endregion

        #region Validation Engine
        /// <summary>
        /// Validates stage threshold modifiers and applies fluent boundary guards onto tracking values.
        /// </summary>
        public void Validate()
        {
            // Fluent API Upgrade: Enforce non-negative baselines using primitive inline limit extensions
            DamageOnStrike = DamageOnStrike.LimitMin(0f);
            AdditionalDamagePerStack = AdditionalDamagePerStack.LimitMin(0f);

            // Audit operational light source configurations relative to base strike metrics
            if (DamageOnStrikeWhenLightsourceActive < 0f)
            {
                Logger.Warn(nameof(PlayerSanityStageConfig), $"DamageOnStrikeWhenLightsourceActive was negative for stage range [{MinThreshold}% - {MaxThreshold}%]. Resetting baseline to 0.");
                DamageOnStrikeWhenLightsourceActive = 0f;
            }
            else if (DamageOnStrikeWhenLightsourceActive > DamageOnStrike)
            {
                Logger.Warn(nameof(PlayerSanityStageConfig), $"DamageOnStrikeWhenLightsourceActive exceeded base DamageOnStrike for stage range [{MinThreshold}% - {MaxThreshold}%]. Clamping to balance constraints.");
                DamageOnStrikeWhenLightsourceActive = DamageOnStrike;
            }

            if (AdditionalDamagePerStackWhenLightsourceActive < 0f)
            {
                Logger.Warn(nameof(PlayerSanityStageConfig), $"AdditionalDamagePerStackWhenLightsourceActive was negative for stage range [{MinThreshold}% - {MaxThreshold}%]. Resetting baseline to 0.");
                AdditionalDamagePerStackWhenLightsourceActive = 0f;
            }
            else if (AdditionalDamagePerStackWhenLightsourceActive > AdditionalDamagePerStack)
            {
                Logger.Warn(nameof(PlayerSanityStageConfig), $"AdditionalDamagePerStackWhenLightsourceActive exceeded base AdditionalDamagePerStack for stage range [{MinThreshold}% - {MaxThreshold}%]. Clamping to balance constraints.");
                AdditionalDamagePerStackWhenLightsourceActive = AdditionalDamagePerStack;
            }

            // Propagate deep validation routines down nested matrix elements safely
            if (Effects == null) return;

            for (int i = 0; i < Effects.Count; i++)
            {
                Effects[i]?.Validate();
            }
        }
        #endregion
    }
}