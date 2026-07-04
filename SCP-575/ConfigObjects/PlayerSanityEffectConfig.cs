using LabApi.Extensions;
using System.ComponentModel;
using Logger = LabApi.Extensions.Misc.iLogger;

namespace SCP_575.ConfigObjects
{
    /// <summary>
    /// Configuration profile for an individual player sanity status effect, 
    /// binding natively to the central framework's unified tracking keys.
    /// </summary>
    [Description("Configuration blueprint detailing an individual player sanity status effect modifier layer.")]
    public sealed class PlayerSanityEffectConfig
    {
        #region Serialized Properties
        [Description("Specifies the native status effect type token to apply to the targeted human subject.")]
        public FacilityEffectType EffectType { get; set; }

        [Description("Duration window of the applied status effect in seconds.")]
        public float Duration { get; set; } = 3f;

        [Description("Intensity coefficient layer of the applied status effect (1 - 255).")]
        public byte Intensity { get; set; } = 1;
        #endregion

        #region Validation Engine
        /// <summary>
        /// Ensures structural parameter bounds match server constraints using inline fluent limiters.
        /// </summary>
        public void Validate()
        {
            // Fluent API Implementation: Sanitize execution parameters natively via math extensions
            Duration = Duration.LimitMin(0f);

            if (Intensity < 1)
            {
                Logger.Warn(nameof(PlayerSanityEffectConfig), $"Effect token [{EffectType}] was initialized with an illegal intensity level of 0. Normalizing up to 1.");
                Intensity = 1;
            }
        }
        #endregion
    }
}