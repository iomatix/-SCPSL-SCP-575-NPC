using LabApi.Extensions;
using System.ComponentModel;
using Logger = LabApi.Extensions.Misc.iLogger;

namespace SCP_575.ConfigObjects
{
    /// <summary>
    /// Configuration settings managing public public address announcements, vocal matrices, 
    /// queue priorities, and real-time corruption distortions for CASSIE transmissions.
    /// </summary>
    public sealed class CassieConfig
    {
        #region Factory Baseline Constants
        private const string DefaultStart = "$pitch_0.89 warning . $pitch_0.95 facility power supply $pitch_0.92 unit $pitch_0.87 failure . $pitch_0.96 danger $pitch_0.92 anomaly $pitch_0.95 detected .";
        private const string DefaultPost = "$pitch_0.85 the dark $pitch_0.97 is not safe . stay in the $pitch_0.85 light $pitch_0.95 area . $pitch_1";
        private const string DefaultWrong = "$pitch_1.05 . power supply unit stabilized . false alert detected . $pitch_1";
        private const string DefaultEnd = "$pitch_1.05 facility power system now operational . $pitch_1";
        private const string DefaultFacility = "The Facility .";
        private const string DefaultEntrance = "The Entrance Zone .";
        private const string DefaultLight = "The Light Containment Zone .";
        private const string DefaultHeavy = "The Heavy Containment Zone.";
        private const string DefaultSurface = "The Surface .";
        private const string DefaultOther = ". $pitch_0.35 .g6 $pitch_0.95 the malfunction is Unspecified .";
        private const string DefaultKeter = "$pitch_0.15 .g7";
        #endregion

        #region General Options
        [Description("Enable Cassie's countdown announcement loop shortly before an environmental blackout triggers.")]
        public bool IsCountdownEnabled { get; set; } = true;

        [Description("Force-flush the active global CASSIE broadcast queue aggressively before executing critical anomaly announcements.")]
        public bool CassieMessageClearBeforeImportant { get; set; } = false;

        [Description("Priority scheduling weight coefficient assigned to important announcements. Higher channels bypass more queued elements.")]
        public float CassieMessagePriority { get; set; } = 3.1f;
        #endregion

        #region Core Announcements
        [Description("The foundational emergency CASSIE notification sequence delivered when the anomaly event initiates.")]
        public string CassieMessageStart { get; set; } = DefaultStart;

        [Description("Follow-up tactical warning broadcasted immediately across global public address systems after power grids collapse.")]
        public string CassiePostMessage { get; set; } = DefaultPost;

        [Description("Fallback notification broadcasted if an active initialization countdown is stabilized or cancelled.")]
        public string CassieMessageWrong { get; set; } = DefaultWrong;

        [Description("Resolution notification broadcasted to all active facility grids indicating that power grids are restored to a stable operational baseline.")]
        public string CassieMessageEnd { get; set; } = DefaultEnd;
        #endregion

        #region Zone-Specific Messages
        [Description("Vocal identifier token appended to announcements impacting the entire facility grid structure simultaneously.")]
        public string CassieMessageFacility { get; set; } = DefaultFacility;

        [Description("Vocal identifier token appended to announcements impacting the Entrance Zone grid infrastructure.")]
        public string CassieMessageEntrance { get; set; } = DefaultEntrance;

        [Description("Vocal identifier token appended to announcements impacting the Light Containment Zone grid infrastructure.")]
        public string CassieMessageLight { get; set; } = DefaultLight;

        [Description("Vocal identifier token appended to announcements impacting the Heavy Containment Zone grid infrastructure.")]
        public string CassieMessageHeavy { get; set; } = DefaultHeavy;

        [Description("Vocal identifier token appended to announcements impacting Surface sector structural quadrants.")]
        public string CassieMessageSurface { get; set; } = DefaultSurface;

        [Description("Vocal identifier token appended to announcements targeting unmapped, custom, or custom-built facility zone spaces.")]
        public string CassieMessageOther { get; set; } = DefaultOther;
        #endregion

        #region Audio Effects & Anomaly Chances
        [Description("The probability percentage chance (0% - 100%) of an individual word experiencing structural vocal glitch modulation.")]
        public float GlitchChance { get; set; } = 0.15f;

        [Description("The probability percentage chance (0% - 100%) of an individual word sustaining terminal audio jamming degradation.")]
        public float JamChance { get; set; } = 0.10f;

        [Description("The dedicated low-frequency structural background environmental tracking overlay sound triggered during a blackout.")]
        public string CassieKeter { get; set; } = DefaultKeter;
        #endregion

        #region Validation Engine
        /// <summary>
        /// Validates CASSIE message profiles, sanitizes probability scales via fluent math primitives, 
        /// and scrubs spacing corruptions to insulate vocal synthesizers against sub-frame execution failures.
        /// </summary>
        public void Validate()
        {
            // --- 1. Priority & Scale Bounds Safeguards ---
            // Fluent API Upgrade: Enforce non-negative priority indices cleanly via math limit extensions
            if (CassieMessagePriority < 0f)
            {
                Logger.Warn(nameof(CassieConfig), $"CassieMessagePriority ({CassieMessagePriority}) cannot evaluate to a negative metric index track. Resetting back to safe factory default baseline (3.1f).");
                CassieMessagePriority = 3.1f;
            }

            // Fluent API Upgrade: Clamp audio effect probabilities smoothly into real percentage spaces (0% - 100%)
            GlitchChance = GlitchChance.Clamp(0f, 100f);
            JamChance = JamChance.Clamp(0f, 100f);

            // --- 2. Complete DRY-Compliant String Sanitization Matrix ---
            // Clean out line breaks and white space parameters safely while guaranteeing string validity
            CassieMessageCountdown = CassieMessageCountdown.SanitizeCassieString();
            CassieMessageStart = CassieMessageStart.SanitizeCassieString();
            CassiePostMessage = CassiePostMessage.SanitizeCassieString();
            CassieMessageWrong = CassieMessageWrong.SanitizeCassieString();
            CassieMessageEnd = CassieMessageEnd.SanitizeCassieString();

            CassieMessageFacility = CassieMessageFacility.SanitizeCassieString();
            CassieMessageEntrance = CassieMessageEntrance.SanitizeCassieString();
            CassieMessageLight = CassieMessageLight.SanitizeCassieString();
            CassieMessageHeavy = CassieMessageHeavy.SanitizeCassieString();
            CassieMessageSurface = CassieMessageSurface.SanitizeCassieString();
            CassieMessageOther = CassieMessageOther.SanitizeCassieString();
            CassieKeter = CassieKeter.SanitizeCassieString();
        }
        #endregion
    }
}