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
        public float GlobalScreamCooldown { get; set; } = 75f;

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
            // FIXED: Changed operator to <= 0f to prevent configuration bypass 
            // that triggers a critical plugin crash inside Scp575AudioManager.
            if (GlobalScreamCooldown <= 0f)
            {
                Log.Warn("[AudioConfig] GlobalScreamCooldown must be positive. Resetting to default (75f).");
                GlobalScreamCooldown = 75f;
            }

            if (DefaultFadeDuration < 0f)
            {
                Log.Warn("[AudioConfig] DefaultFadeDuration cannot be negative. Resetting to default (1f).");
                DefaultFadeDuration = 1f;
            }
        }
    }
}