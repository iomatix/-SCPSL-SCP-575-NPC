namespace SCP_575.ConfigObjects
{
    using System;
    using System.ComponentModel;

    /// <summary>
    /// Configuration for a sanity status effect, defining its type, duration, and intensity.
    /// </summary>
    [Description("Configuration for a player sanity effect.")]
    public sealed class PlayerSanityEffectConfig
    {
        /// <summary>
        /// Gets or sets the type of status effect to apply to the player.
        /// </summary>
        [Description("Specifies the status effect type to apply.")]
        public Type EffectType { get; set; }

        /// <summary>
        /// Gets or sets the duration, in seconds, of the effect.
        /// </summary>
        [Description("Duration of the effect in seconds.")]
        public float Duration { get; set; } = 3f;

        /// <summary>
        /// Gets or sets the intensity level of the effect.
        /// </summary>
        [Description("Intensity level of the status effect.")]
        public byte Intensity { get; set; } = 1;
    }
}