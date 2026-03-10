namespace SCP_575.ConfigObjects
{
    using System.ComponentModel;
    using Exiled.API.Features;

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

        /// <summary>
        /// Validates the audio configuration parameters and corrects invalid input.
        /// </summary>
        public void Validate()
        {
            if (GlobalScreamCooldown < 0f)
            {
                Log.Warn("[AudioConfig] GlobalScreamCooldown cannot be negative. Resetting to default (35f).");
                GlobalScreamCooldown = 35f;
            }

            if (DefaultFadeDuration < 0f)
            {
                Log.Warn("[AudioConfig] DefaultFadeDuration cannot be negative. Resetting to default (1f).");
                DefaultFadeDuration = 1f;
            }
        }
    }
}