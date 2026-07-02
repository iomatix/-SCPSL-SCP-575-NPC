namespace SCP_575.ConfigObjects
{
    using System;
    using System.ComponentModel;
    using Logger = SCP_575.Shared.LibraryLabAPI;

    public sealed class HintsConfig
    {
        #region Player Hints
        public bool IsEnabledKeterHint { get; set; } = true;
        public bool IsEnabledSanityHint { get; set; } = true;
        public string SanityDecreasedHint { get; set; } = "Your sanity is decreasing!\n Sanity: {0}.";
        public string SanityIncreasedHint { get; set; } = "Your sanity is recovering!\n Sanity: {0}.";
        public string SanityIncreasedMedicalHint { get; set; } = "Your sanity is recovering!\n Sanity: {0}.";
        public string KeterHint { get; set; } = "You were affected by actions of SCP-575!";
        public bool IsEnabledLightEmitterCooldownHint { get; set; } = true;
        public string LightEmitterCooldownHint { get; set; } = "Your light source is on cooldown!";
        public string LightEmitterDisabledHint { get; set; } = "Your light source has been disabled!";
        #endregion

        #region Death Information
        public string KilledBy { get; set; } = "SCP-575";
        public string KilledByMessage { get; set; } = "Shredded apart by SCP-575";
        public string RagdollInspectText { get; set; } = "Flesh stripped by shadow tendrils.";
        #endregion

        public void Validate()
        {
            SanityDecreasedHint = SanitizeHintString(SanityDecreasedHint);
            SanityIncreasedHint = SanitizeHintString(SanityIncreasedHint);
            SanityIncreasedMedicalHint = SanitizeHintString(SanityIncreasedMedicalHint);
            KeterHint = SanitizeHintString(KeterHint);
            LightEmitterCooldownHint = SanitizeHintString(LightEmitterCooldownHint);
            LightEmitterDisabledHint = SanitizeHintString(LightEmitterDisabledHint);

            KilledBy = string.IsNullOrWhiteSpace(KilledBy) ? "SCP-575" : KilledBy.Trim();
            KilledByMessage = string.IsNullOrWhiteSpace(KilledByMessage) ? "Shredded apart by SCP-575" : KilledByMessage.Trim();
            RagdollInspectText = string.IsNullOrWhiteSpace(RagdollInspectText) ? "Flesh stripped by shadow tendrils." : RagdollInspectText.Trim();

            // Safe value assignments returning checked configurations
            SanityDecreasedHint = GetValidatedTokenString(SanityDecreasedHint, "{0}", "Your sanity is decreasing! Sanity: {0}.");
            SanityIncreasedHint = GetValidatedTokenString(SanityIncreasedHint, "{0}", "Your sanity is recovering! Sanity: {0}.");
            SanityIncreasedMedicalHint = GetValidatedTokenString(SanityIncreasedMedicalHint, "{0}", "Your sanity is recovering! Sanity: {0}.");
        }

        private string SanitizeHintString(string rawHint)
        {
            return string.IsNullOrWhiteSpace(rawHint) ? string.Empty : rawHint.Replace("\r", "").Trim();
        }

        private string GetValidatedTokenString(string sourceHint, string expectedToken, string safeFallback)
        {
            if (string.IsNullOrEmpty(sourceHint)) return string.Empty;

            if (!sourceHint.Contains(expectedToken))
            {
                Logger.LogWarn(nameof(HintsConfig), $"Missing token '{expectedToken}' inside layout string. Reverting.");
                return safeFallback;
            }

            try
            {
                _ = string.Format(sourceHint, "100");
                return sourceHint;
            }
            catch (FormatException)
            {
                Logger.LogError(nameof(HintsConfig), "Malformed formatting syntax intercepted. Reverting to fallback configuration.");
                return safeFallback;
            }
        }
    }
}