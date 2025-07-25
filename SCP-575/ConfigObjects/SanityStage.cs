/// <summary>
/// Configuration objects for the SCP-575 sanity system.
/// </summary>
namespace SCP_575.ConfigObjects
{
    using System.ComponentModel;

    /// <summary>
    /// Defines a stage of player sanity, including thresholds and which effects
    /// to apply when sanity falls within this range.
    /// </summary>
    public class SanityStage
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
        /// Whether to play faint whisper sound effects.
        /// </summary>
        [Description("Whether to apply faint whispers effect.")]
        public bool EnableWhispers { get; set; }

        /// <summary>
        /// Whether to apply a slight screen shake effect.
        /// </summary>
        [Description("Whether to apply slight screen shake.")]
        public bool EnableScreenShake { get; set; }

        /// <summary>
        /// Whether to apply audio distortion to the player's sound.
        /// </summary>
        [Description("Whether to apply audio distortion.")]
        public bool EnableAudioDistortion { get; set; }

        /// <summary>
        /// Whether to show hallucination visual effects.
        /// </summary>
        [Description("Whether to hallucinate visual effects.")]
        public bool EnableHallucinations { get; set; }

        /// <summary>
        /// Whether to distort the camera with jitter or forced tilt.
        /// </summary>
        [Description("Whether to cause camera jitter or forced tilt.")]
        public bool EnableCameraDistortion { get; set; }

        /// <summary>
        /// Whether movement should be slowed or made to stutter.
        /// </summary>
        [Description("Whether movement should be slowed or stuttered.")]
        public bool EnableMovementLag { get; set; }

        /// <summary>
        /// Whether to flash the screen in a panic effect.
        /// </summary>
        [Description("Whether to flash the screen in panic.")]
        public bool EnablePanicFlash { get; set; }

    }
}