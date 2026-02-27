namespace SCP_575.ConfigObjects
{
    using System.ComponentModel;
    public sealed class HintsConfig
    {
        #region Player Hints

        /// <summary>
        /// Whether to show hint messages when players are affected by SCP-575 actions.
        /// </summary>
        [Description("Inform players when affected by SCP-575 via hint messages.")]
        public bool IsEnabledKeterHint { get; private set; } = true;

        /// <summary>
        /// Whether to show hint messages when players sanity is affected.
        /// </summary>
        [Description("Inform players when thier sanity is affected.")]
        public bool IsEnabledSanityHint { get; private set; } = true;

        /// <summary>  
        /// Hint message shown when a player's sanity decreases.  
        /// </summary>  
        [Description("Hint shown when player's sanity level decreases. {0} = current sanity value")]
        public string SanityDecreasedHint { get; set; } =
            "Your sanity is decreasing!\n Sanity: {0}. Find light sources or medical items to recover.";

        /// <summary>  
        /// Hint message shown when a player's sanity increases from medical items.  
        /// </summary>  
        [Description("Hint shown when player's sanity recovers from medical treatment. {0} = new sanity value")]
        public string SanityIncreasedHint { get; set; } =
            "Your sanity is recovering!\n Sanity: {0} thanks to medical treatment!";

        /// <summary>
        /// Hint message shown when a player is affected by SCP-575 action.
        /// </summary>
        [Description("Hint shown when player is affected by SCP-575.")]
        public string KeterHint { get; set; } =
            "You were affected by actions of SCP-575! Equip a flashlight!";

        /// <summary>
        /// Whether to show a hint when a light source is on cooldown.
        /// </summary>
        [Description("Inform players about cooldown of light emitter.")]
        public bool IsEnabledLightEmitterCooldownHint { get; private set; } = true;

        /// <summary>
        /// Hint message shown when a light source is on cooldown.
        /// </summary>
        [Description("Hint shown when using light source on cooldown.")]
        public string LightEmitterCooldownHint { get; set; } =
            "Your light source is on cooldown!";

        /// <summary>
        /// Hint message shown when a light source is disabled by SCP-575.
        /// </summary>
        [Description("Hint shown when light source is disabled by SCP-575.")]
        public string LightEmitterDisabledHint { get; set; } =
            "Your light source has been disabled!";

        #endregion

        #region Death Information

        /// <summary>
        /// Name displayed in a player's death information.
        /// </summary>
        [Description("Name displayed in death info.")]
        public string KilledBy { get; set; } = "SCP-575";

        /// <summary>
        /// Message shown in a player's death information.
        /// </summary>
        [Description("Message displayed when killed by SCP-575.")]
        public string KilledByMessage { get; set; } =
            "Shredded apart by SCP-575";

        /// <summary>
        /// Text displayed when inspecting a ragdoll killed by SCP-575.
        /// </summary>
        [Description("Ragdoll inspection text after death by SCP-575.")]
        public string RagdollInspectText { get; set; } =
            "Flesh stripped by shadow tendrils, leaving a shadowy skeleton.";

        #endregion
    }
}