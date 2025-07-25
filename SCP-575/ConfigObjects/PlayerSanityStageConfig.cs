/// <summary>
/// Configuration objects for the SCP-575 sanity system.
/// </summary>
namespace SCP_575.ConfigObjects
{
    using System.Collections.Generic;
    using System.ComponentModel;

    /// <summary>
    /// Defines a stage of player sanity, including thresholds and which effects
    /// to apply when sanity falls within this range.
    /// </summary>
    public sealed class PlayerSanityStageConfig
    {
        /// <summary>
        /// Minimum sanity percentage to activate this stage.
        /// </summary>
        [Description("Min sanity % to activate this stage.")]
        public float MinThreshold { get; set; }


        /// <summary>
        /// Maximum sanity percentage to activate this stage.
        /// </summary>
        [Description("Max sanity % to activate this stage.")]
        public float MaxThreshold { get; set; }

        /// <summary>
        /// Damage to apply on an SCP-575 strike at this sanity level.
        /// </summary>
        [Description("Damage to apply on SCP-575 strike at this sanity level.")]
        public float DamageOnStrike { get; set; }

        /// <summary>
        /// Gets or sets the list of custom sanity effects applied during this stage.
        /// </summary>

        [Description("List of effects to apply to the player during this sanity stage.")]
        public List<PlayerSanityEffectConfig> Effects { get; set; } = new();

    }
}