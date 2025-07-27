namespace SCP_575.ConfigObjects
{
    using System.ComponentModel;

    /// <summary>
    /// Configuration settings for SCP-575 audio management.
    /// </summary>
    public sealed class AudioConfig
    {
        /// <summary>
        /// Gets or sets the cooldown time in seconds for global scream audio playback.
        /// </summary>
        [Description("The cooldown time in seconds for global scream audio playback. Must be positive.")]
        public float GlobalScreamCooldown { get; set; } = 35f;

        /// <summary>
        /// Gets or sets the default duration in seconds for audio fade-in and fade-out effects.
        /// </summary>
        [Description("The default duration in seconds for audio fade-in and fade-out effects. Must be non-negative.")]
        public float DefaultFadeDuration { get; set; } = 1f;
    }
}