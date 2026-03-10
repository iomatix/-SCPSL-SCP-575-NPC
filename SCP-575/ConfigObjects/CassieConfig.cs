namespace SCP_575.ConfigObjects
{
    using System.ComponentModel;
    using Exiled.API.Features;
    using UnityEngine;

    public sealed class CassieConfig
    {
        #region General Options

        /// <summary>
        /// Enable Cassie’s countdown announcement.
        /// </summary>
        [Description("Enable Cassie countdown announcement.")]
        public bool IsCountdownEnabled { get; set; } = true;

        /// <summary>
        /// Clear Cassie’s message queue before important announcements.
        /// </summary>
        [Description("Clear message queue before important messages.")]
        public bool CassieMessageClearBeforeImportant { get; set; } = true;

        #endregion

        /// <summary>
        /// Priority level for Cassie’s important messages. Higher values will skip more of the message queue before playing these announcements.
        /// </summary>
        [Description("Priority for important Cassie messages.")]
        public float CassieMessagePriority { get; set; } = 3.1f;

        #region Countdown Message

        /// <summary>
        /// Cassie’s countdown just before blackout (3…2…1).
        /// </summary>
        [Description("Cassie countdown before blackout.")]
        public string CassieMessageCountdown { get; set; } = "pitch_0.9 power failure . pitch_1";

        #endregion

        #region Timing Settings

        /// <summary>
        /// Seconds between Cassie’s pre-countdown sentence and the countdown.
        /// </summary>
        [Description("Time between sentence and countdown.")]
        public float TimeBetweenSentenceAndStart { get; set; } = 5.0f;

        /// <summary>
        /// Seconds between blackout end and Cassie’s end message.
        /// </summary>
        [Description("Time between blackout end and end message.")]
        public float TimeBetweenSentenceAndEnd { get; set; } = 3.0f;

        #endregion

        #region Core Announcements

        /// <summary>
        /// Cassie’s message at blackout start.
        /// </summary>
        [Description("Cassie message at blackout start.")]
        public string CassieMessageStart { get; set; } = "warning . facility power grid failure imminent . anomalous activity detected .";

        /// <summary>
        /// Cassie’s follow-up message immediately after blackout begins.
        /// </summary>
        [Description("Cassie post-blackout-start message.")]
        public string CassiePostMessage { get; set; } = "pitch_0.8 darkness is no longer safe . stay in light areas . pitch_1";

        /// <summary>
        /// Cassie’s message if no blackout occurs.
        /// </summary>
        [Description("Cassie message if no blackout occurs.")]
        public string CassieMessageWrong { get; set; } = "pitch_1.1 . power grid stabilized . false alert detected . pitch_1";

        /// <summary>
        /// Cassie’s message when blackout ends.
        /// </summary>
        [Description("Cassie message at blackout end.")]
        public string CassieMessageEnd { get; set; } = "pitch_1.15 facility power system now operational . pitch_1";

        #endregion

        #region Zone-Specific Messages

        /// <summary>
        /// Facility-wide blackout announcement.
        /// </summary>
        [Description("Message for facility-wide blackout.")]
        public string CassieMessageFacility { get; set; } = "The Facility .";

        /// <summary>
        /// Entrance Zone blackout announcement.
        /// </summary>
        [Description("Message for Entrance Zone blackout.")]
        public string CassieMessageEntrance { get; set; } = "The Entrance Zone .";

        /// <summary>
        /// Light Containment Zone blackout announcement.
        /// </summary>
        [Description("Message for Light Containment Zone blackout.")]
        public string CassieMessageLight { get; set; } = "The Light Containment Zone .";

        /// <summary>
        /// Heavy Containment Zone blackout announcement.
        /// </summary>
        [Description("Message for Heavy Containment Zone blackout.")]
        public string CassieMessageHeavy { get; set; } = "The Heavy Containment Zone.";

        /// <summary>
        /// Surface Zone blackout announcement.
        /// </summary>
        [Description("Message for Surface Zone blackout.")]
        public string CassieMessageSurface { get; set; } = "The Surface .";

        /// <summary>
        /// Announcement for unspecified/other zones.
        /// </summary>
        [Description("Message for unspecified zone blackout.")]
        public string CassieMessageOther { get; set; } = ". pitch_0.35 .g6 pitch_0.95 the malfunction is Unspecified .";

        #endregion

        #region Audio Effects

        /// <summary>
        /// Chance (%) of a glitch per word in Cassie’s speech.
        /// </summary>
        [Description("Glitch chance per word in Cassie messages.")]
        public float GlitchChance { get; set; } = 15f;

        /// <summary>
        /// Chance (%) of jamming per word in Cassie’s speech.
        /// </summary>
        [Description("Jam chance per word in Cassie messages.")]
        public float JamChance { get; set; } = 10f;

        /// <summary>
        /// The “Keter” sound Cassie plays during blackout.
        /// </summary>
        [Description("Cassie Keter sound during blackout.")]
        public string CassieKeter { get; set; } = "pitch_0.15 .g7";

        #endregion

        /// <summary>
        /// Validates the Cassie configuration parameters and corrects invalid input.
        /// </summary>
        public void Validate()
        {
            if (TimeBetweenSentenceAndStart < 0f)
            {
                Log.Warn("[CassieConfig] TimeBetweenSentenceAndStart cannot be negative. Resetting to 0.");
                TimeBetweenSentenceAndStart = 0f;
            }

            if (TimeBetweenSentenceAndEnd < 0f)
            {
                Log.Warn("[CassieConfig] TimeBetweenSentenceAndEnd cannot be negative. Resetting to 0.");
                TimeBetweenSentenceAndEnd = 0f;
            }

            // Clamp probabilities between 0% and 100%
            GlitchChance = Mathf.Clamp(GlitchChance, 0f, 100f);
            JamChance = Mathf.Clamp(JamChance, 0f, 100f);
        }
    }
}