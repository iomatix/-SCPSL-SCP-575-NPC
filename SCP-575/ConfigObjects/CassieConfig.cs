namespace SCP_575.ConfigObjects
{
    using System.ComponentModel;
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

        #region Countdown Message

        /// <summary>
        /// Cassie’s countdown just before blackout (3…2…1).
        /// </summary>
        [Description("Cassie countdown before blackout.")]
        public string CassieMessageCountdown { get; set; } =
            "pitch_0.2 .g4 . .g4 pitch_1 door control system pitch_0.25 .g1 pitch_0.9 malfunction pitch_1 . initializing repair";

        #endregion

        #region Timing Settings

        /// <summary>
        /// Seconds between Cassie’s pre-countdown sentence and the countdown.
        /// </summary>
        [Description("Time between sentence and countdown.")]
        public float TimeBetweenSentenceAndStart
        {
            get => _timeBetweenSentenceAndStart;
            set => _timeBetweenSentenceAndStart = value < 0f ? 0f : value;
        }
        private float _timeBetweenSentenceAndStart = 8.6f;

        /// <summary>
        /// Seconds between blackout end and Cassie’s end message.
        /// </summary>
        [Description("Time between blackout end and end message.")]
        public float TimeBetweenSentenceAndEnd
        {
            get => _timeBetweenSentenceAndEnd;
            set => _timeBetweenSentenceAndEnd = value < 0f ? 0f : value;
        }
        private float _timeBetweenSentenceAndEnd = 7.0f;

        #endregion

        #region Core Announcements

        /// <summary>
        /// Cassie’s message at blackout start.
        /// </summary>
        [Description("Cassie message at blackout start.")]
        public string CassieMessageStart { get; set; } =
            "facility power system outage in 3 . 2 . 1 .";

        /// <summary>
        /// Cassie’s follow-up message immediately after blackout begins.
        /// </summary>
        [Description("Cassie post-blackout-start message.")]
        public string CassiePostMessage { get; set; } =
            "facility power system malfunction has been detected at .";

        /// <summary>
        /// Cassie’s message if no blackout occurs.
        /// </summary>
        [Description("Cassie message if no blackout occurs.")]
        public string CassieMessageWrong { get; set; } =
            ". I have prevented the system failure . .g5 Sorry for a .g3 . false alert .";

        /// <summary>
        /// Cassie’s message when blackout ends.
        /// </summary>
        [Description("Cassie message at blackout end.")]
        public string CassieMessageEnd { get; set; } =
            "facility power system now operational";

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
        public string CassieMessageOther { get; set; } =
            ". pitch_0.35 .g6 pitch_0.95 the malfunction is Unspecified .";

        #endregion

        #region Audio Effects

        /// <summary>
        /// Chance (%) of a glitch per word in Cassie’s speech.
        /// </summary>
        [Description("Glitch chance per word in Cassie messages.")]
        public float GlitchChance
        {
            get => _glitchChance;
            private set => _glitchChance = value < 0f ? 0f : value > 100f ? 100f : value;
        }
        private float _glitchChance = 10f;

        /// <summary>
        /// Chance (%) of jamming per word in Cassie’s speech.
        /// </summary>
        [Description("Jam chance per word in Cassie messages.")]
        public float JamChance
        {
            get => _jamChance;
            private set => _jamChance = value < 0f ? 0f : value > 100f ? 100f : value;
        }
        private float _jamChance = 5f;

        /// <summary>
        /// The “Keter” sound Cassie plays during blackout.
        /// </summary>
        [Description("Cassie Keter sound during blackout.")]
        public string CassieKeter { get; set; } = "pitch_0.15 .g7";

        #endregion
    }
}