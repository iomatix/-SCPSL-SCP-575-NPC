using System;
using System.ComponentModel;
using Logger = LabApi.Extensions.Misc.iLogger;

namespace SCP_575.ConfigObjects
{
    /// <summary>
    /// Configuration settings managing visual on-screen user hints, role disruption messages, 
    /// and contextual ragdoll description tracking metrics for the SCP-575 plugin ecosystem.
    /// </summary>
    public sealed class HintsConfig
    {
        #region Factory Baseline Constants
        private const string DefaultSanityDecreasedHint = "Your sanity is decreasing!\n Sanity: {0}.";
        private const string DefaultSanityIncreasedHint = "Your sanity is recovering!\n Sanity: {0}.";
        private const string DefaultSanityIncreasedMedicalHint = "Your sanity is recovering!\n Sanity: {0}.";
        private const string DefaultKilledBy = "SCP-575";
        private const string DefaultKilledByMessage = "Shredded apart by SCP-575";
        private const string DefaultRagdollInspectText = "Flesh stripped by shadow tendrils.";
        #endregion

        #region Player Hints Settings
        [Description("Enable or disable text hints broadcasted to players targeted or damaged by SCP-575.")]
        public bool IsEnabledKeterHint { get; set; } = true;

        [Description("Enable or disable continuous text hints tracking real-time sanity drops or recovery states.")]
        public bool IsEnabledSanityHint { get; set; } = true;

        [Description("On-screen hint message layout displayed when a subject's sanity level decreases. Requires the '{0}' token placeholder.")]
        public string SanityDecreasedHint { get; set; } = DefaultSanityDecreasedHint;

        [Description("On-screen hint message layout displayed when a subject's sanity level passively recovers. Requires the '{0}' token placeholder.")]
        public string SanityIncreasedHint { get; set; } = DefaultSanityIncreasedHint;

        [Description("On-screen hint message layout displayed when a subject's sanity level recovers via medical pill consumption. Requires the '{0}' token placeholder.")]
        public string SanityIncreasedMedicalHint { get; set; } = DefaultSanityIncreasedMedicalHint;

        [Description("On-screen text hint broadcasted instantly to a player when disrupted by standard sensory action tracks.")]
        public string KeterHint { get; set; } = "You were affected by actions of SCP-575!";

        [Description("Enable or disable notification hints informing players that their active light source is locked on cooldown.")]
        public bool IsEnabledLightEmitterCooldownHint { get; set; } = true;

        [Description("Text hint layout informing a subject that their active mobile flashlight or weapon light modification is currently cooling down.")]
        public string LightEmitterCooldownHint { get; set; } = "Your light source is on cooldown!";

        [Description("Text hint layout informing a subject that their active light source asset has been forcibly shut off by an environmental strike.")]
        public string LightEmitterDisabledHint { get; set; } = "Your light source has been disabled!";
        #endregion

        #region Death Information & Ragdoll Metrics
        [Description("The standard structural identity label assigned as the killer name inside death logs and combat interfaces.")]
        public string KilledBy { get; set; } = DefaultKilledBy;

        [Description("The detailed custom death notification string presented on the victim's screen layout upon termination.")]
        public string KilledByMessage { get; set; } = DefaultKilledByMessage;

        [Description("Custom interaction tracking text appended directly onto generated player ragdoll transforms to provide deep horror pacing.")]
        public string RagdollInspectText { get; set; } = DefaultRagdollInspectText;
        #endregion

        #region Validation Engine
        /// <summary>
        /// Validates text hint structures, trims formatting spaces, and conducts format integrity audits to isolate faulty tokens.
        /// </summary>
        public void Validate()
        {
            // Clean carriage returns and layout white spaces across all configuration fields
            SanityDecreasedHint = SanitizeHintString(SanityDecreasedHint);
            SanityIncreasedHint = SanitizeHintString(SanityIncreasedHint);
            SanityIncreasedMedicalHint = SanitizeHintString(SanityIncreasedMedicalHint);
            KeterHint = SanitizeHintString(KeterHint);
            LightEmitterCooldownHint = SanitizeHintString(LightEmitterCooldownHint);
            LightEmitterDisabledHint = SanitizeHintString(LightEmitterDisabledHint);

            // Enforce fail-safe boundaries for structural text strings to prevent blank fields
            KilledBy = string.IsNullOrWhiteSpace(KilledBy) ? DefaultKilledBy : KilledBy.Trim();
            KilledByMessage = string.IsNullOrWhiteSpace(KilledByMessage) ? DefaultKilledByMessage : KilledByMessage.Trim();
            RagdollInspectText = string.IsNullOrWhiteSpace(RagdollInspectText) ? DefaultRagdollInspectText : RagdollInspectText.Trim();

            // Perform deep structural validation checks on parameter tokens to guarantee format string compliance
            SanityDecreasedHint = GetValidatedTokenString(SanityDecreasedHint, "{0}", DefaultSanityDecreasedHint);
            SanityIncreasedHint = GetValidatedTokenString(SanityIncreasedHint, "{0}", DefaultSanityIncreasedHint);
            SanityIncreasedMedicalHint = GetValidatedTokenString(SanityIncreasedMedicalHint, "{0}", DefaultSanityIncreasedMedicalHint);
        }

        /// <summary>
        /// Scrubs raw string properties, eliminating platform-specific spacing anomalies natively.
        /// </summary>
        private static string SanitizeHintString(string rawHint) =>
            string.IsNullOrWhiteSpace(rawHint) ? string.Empty : rawHint.Replace("\r", "").Trim();

        /// <summary>
        /// Audits a targeted configuration hint layout string to guarantee it cleanly parses required structural formatting tokens.
        /// </summary>
        private static string GetValidatedTokenString(string sourceHint, string expectedToken, string safeFallback)
        {
            if (string.IsNullOrEmpty(sourceHint))
                return string.Empty;

            // Verify the placeholder token is explicitly defined within the layout configuration string
            if (!sourceHint.Contains(expectedToken))
            {
                Logger.Warn(nameof(HintsConfig), $"Formatting compliance validation failure: Mandatory token identifier '{expectedToken}' is missing from string context layout. Reverting to factory baseline configuration.");
                return safeFallback;
            }

            try
            {
                // Execute a dry-run test format sweep to verify the string configuration doesn't break string parsing engines
                _ = string.Format(sourceHint, "100");
                return sourceHint;
            }
            catch (FormatException)
            {
                Logger.Error(nameof(HintsConfig), $"Severe formatting syntax mutation detected inside layout template context. Overriding target with a secure fallback pattern to preserve runtime integrity.");
                return safeFallback;
            }
        }
        #endregion
    }
}