namespace SCP_575
{
    using Exiled.API.Interfaces;
    using SCP_575.ConfigObjects;
    using System.ComponentModel;

    /// <summary>
    /// Defines the type of SCP-575 instance to spawn.
    /// </summary>
    public enum InstanceType
    {
        /// <summary>
        /// Spawns SCP-575 as an NPC.
        /// </summary>
        Npc,

        /// <summary>
        /// Randomly decides whether to spawn SCP-575 based on configuration.
        /// </summary>
        Random
    }

    /// <summary>
    /// Configuration settings for the SCP-575 plugin, controlling blackout events and door behaviors.
    /// </summary>
    public class Config : IConfig
    {
        #region General Settings

        /// <summary>
        /// Gets or sets a value indicating whether the SCP-575 plugin is enabled.
        /// </summary>
        [Description("Enable or disable SCP-575.")]
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether debug logging is enabled.
        /// </summary>
        [Description("Enables debugging.")]
        public bool Debug { get; set; } = true;

        /// <summary>
        /// Gets or sets the type of SCP-575 instance to spawn.
        /// </Description>
        [Description("Determines whether SCP-575 spawns as an NPC or randomly based on a chance.")]
        public InstanceType SpawnType { get; set; } = InstanceType.Npc;

        /// <summary>
        /// Gets or sets the configuration for SCP-575 NPC behaviors.
        /// </summary>
        [Description("Configuration settings for SCP-575 NPC behaviors, such as blackout and damage mechanics.")]
        public NpcConfig NpcConfig { get; set; } = new NpcConfig();

        /// <summary>
        /// Gets or sets the percentage chance that a round includes SCP-575 blackout events.
        /// </summary>
        [Description("The chance that a Round even has SCP-575 blackouts")]
        public float Spawnchance
        {
            get => _spawnchance;
            set => _spawnchance = value < 0f ? 0f : value > 100f ? 100f : value;
        }
        private float _spawnchance = 55f;

        #endregion

        #region Door Settings

        /// <summary>
        /// Gets or sets a value indicating whether doors should close during a blackout.
        /// </summary>
        [Description("Should doors close during blackout?")]
        public bool CloseDoors { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether nuke surface door and HCZ elevator should be ignored.
        /// </summary>
        [Description("Should nuke surface door and hcz elevator be ignored?")]
        public bool SkipNukeDoors { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether unknown doors and elevators should be ignored.
        /// </summary>
        [Description("Should unknown doors and elevators be ignored?")]
        public bool SkipUnknownDoors { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether all elevators should be ignored.
        /// </summary>
        [Description("Should all elevators be ignored?")]
        public bool SkipElevators { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether all airlocks should be ignored.
        /// </summary>
        [Description("Should all airlocks be ignored?")]
        public bool SkipAirlocks { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether all SCP rooms should be ignored.
        /// </summary>
        [Description("Should all scp rooms be ignored?")]
        public bool SkipSCPRooms { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether all armory doors should be ignored.
        /// </summary>
        [Description("Should all armory doors be ignored?")]
        public bool SkipArmory { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether all checkpoint doors should be ignored.
        /// </summary>
        [Description("Should all checkpoints doors be ignored?")]
        public bool SkipCheckpoints { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether checkpoint gates should be ignored, independent of SkipCheckpoints.
        /// </summary>
        [Description("Should checkpoints gates be ignored? Independent from SkipCheckpoints")]
        public bool SkipCheckpointsGate { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether doors within a room should be disabled randomly based on ChancePerDoor.
        /// </summary>
        [Description("Change this to true if want to disable doors randomly within the room.")]
        public bool UsePerDoorChance { get; set; } = true;

        /// <summary>
        /// Gets or sets the percentage chance of disabling a door when UsePerDoorChance is true.
        /// </summary>
        [Description("Percentage chance of an outage per door if UsePerDoorChance is set to true.")]
        public int ChancePerDoor
        {
            get => _chancePerDoor;
            set => _chancePerDoor = value < 0 ? 0 : value > 100 ? 100 : value;
        }
        private int _chancePerDoor = 65;

        #endregion

        #region Timing Settings

        /// <summary>
        /// Gets or sets the initial delay (in seconds) before the first blackout can occur.
        /// </summary>
        [Description("The initial delay (in seconds) before the first blackout can happen")]
        public int InitialDelay
        {
            get => _initialDelay;
            set => _initialDelay = value < 0 ? 0 : value;
        }
        private int _initialDelay = 25;

        /// <summary>
        /// Gets or sets the minimum duration (in seconds) of a blackout.
        /// </summary>
        [Description("The minimum duration of the blackout")]
        public int DurationMin
        {
            get => _durationMin;
            set => _durationMin = value < 0 ? 0 : value;
        }
        private int _durationMin = 24;

        /// <summary>
        /// Gets or sets the maximum duration (in seconds) of a blackout.
        /// </summary>
        [Description("The maximum duration of the blackout")]
        public int DurationMax
        {
            get => _durationMax;
            set => _durationMax = value < 0 ? 0 : value;
        }
        private int _durationMax = 86;

        /// <summary>
        /// Gets or sets the minimum delay (in seconds) before the next blackout.
        /// </summary>
        [Description("The minimum delay before the next blackout")]
        public int DelayMin
        {
            get => _delayMin;
            set => _delayMin = value < 0 ? 0 : value;
        }
        private int _delayMin = 145;

        /// <summary>
        /// Gets or sets the maximum delay (in seconds) before the next blackout.
        /// </summary>
        [Description("The maximum delay before the next blackout")]
        public int DelayMax
        {
            get => _delayMax;
            set => _delayMax = value < 0 ? 0 : value;
        }
        private int _delayMax = 460;

        /// <summary>
        /// Gets or sets a value indicating whether randomized delays are used between blackout events.
        /// </summary>
        [Description("Enable or disable randomized delay between blackout events. If set to false the InitialDelay would be used instead to keep regular events.")]
        public bool RandomEvents { get; set; } = true;

        #endregion

        #region Lighting Settings

        /// <summary>
        /// Gets or sets a value indicating whether lights should flicker during a blackout.
        /// </summary>
        [Description("Flicker lights when the blackout starts.")]
        public bool Flicker { get; set; } = true;

        /// <summary>
        /// Gets or sets the frequency of light flickering during a blackout. Higher values mean faster flickering.
        /// </summary>
        [Description("Flickering frequency. Higher the value faster the flickering.")]
        public float FlickerFrequency
        {
            get => _flickerFrequency;
            set => _flickerFrequency = value < 0f ? 0f : value;
        }
        private float _flickerFrequency = 1.5f;

        /// <summary>
        /// Gets or sets the red channel of the room lights' color during a blackout (0.0 to 1.0).
        /// </summary>
        [Description("Red channel of the lights color in the room during blackout")]
        public float LightsColorR
        {
            get => _lightsColorR;
            set => _lightsColorR = value < 0f ? 0f : value > 1f ? 1f : value;
        }
        private float _lightsColorR = 0.9f;

        /// <summary>
        /// Gets or sets the green channel of the room lights' color during a blackout (0.0 to 1.0).
        /// </summary>
        [Description("Green channel of the lights color in the room during blackout")]
        public float LightsColorG
        {
            get => _lightsColorG;
            set => _lightsColorG = value < 0f ? 0f : value > 1f ? 1f : value;
        }
        private float _lightsColorG = 0.05f;

        /// <summary>
        /// Gets or sets the blue channel of the room lights' color during a blackout (0.0 to 1.0).
        /// </summary>
        [Description("Blue channel of the lights color in the room during blackout")]
        public float LightsColorB
        {
            get => _lightsColorB;
            set => _lightsColorB = value < 0f ? 0f : value > 1f ? 1f : value;
        }
        private float _lightsColorB = 0.2f;

        #endregion

        #region CASSIE Settings

        /// <summary>
        /// Gets or sets a value indicating whether to clear the CASSIE message queue before important messages to prevent spam.
        /// </summary>
        [Description("Should cassie clear the message and broadcast cue before important message to prevent spam?")]
        public bool CassieMessageClearBeforeImportant { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether the countdown announcement is enabled.
        /// </summary>
        [Description("Enable CassieMessageCountdown announcement")]
        public bool IsCountdownEnabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the delay (in seconds) between the countdown and start messages when IsCountdownEnabled is true.
        /// </summary>
        [Description("The delay between the CassieMessageCountdown and the CassieMessageStart if IsCountdownEnabled is enabled.")]
        public float TimeBetweenSentenceAndStart
        {
            get => _timeBetweenSentenceAndStart;
            set => _timeBetweenSentenceAndStart = value < 0f ? 0f : value;
        }
        private float _timeBetweenSentenceAndStart = 15f;

        /// <summary>
        /// Gets or sets the glitch chance per word in CASSIE sentences.
        /// </summary>
        [Description("Glitch chance during message per word in CASSIE sentence.")]
        public float GlitchChance
        {
            get => _glitchChance;
            private set => _glitchChance = value < 0f ? 0f : value > 100f ? 100f : value;
        }
        private float _glitchChance = 4f;

        /// <summary>
        /// Gets or sets the jam chance per word in CASSIE sentences.
        /// </summary>
        [Description("Jam chance during message per word in CASSIE sentence.")]
        public float JamChance
        {
            get => _jamChance;
            private set => _jamChance = value < 0f ? 0f : value > 100f ? 100f : value;
        }
        private float _jamChance = 3f;

        /// <summary>
        /// Gets or sets the CASSIE message played when no blackout occurs.
        /// </summary>
        [Description("Message said by Cassie if no blackout occurs")]
        public string CassieMessageWrong { get; set; } = ".g5 . Avoided the malfunction of the control system . .g3";

        /// <summary>
        /// Gets or sets the CASSIE countdown message played before a blackout.
        /// </summary>
        [Description("Message said by Cassie just before a blackout starts - Countdown - 3 . 2 . 1 announcement")]
        public string CassieMessageCountdown { get; set; } = "pitch_0.2 .g4 . .g4 pitch_1 door control system pitch_0.25 .g1 pitch_0.9 malfunction pitch_1 . initializing repair";

        /// <summary>
        /// Gets or sets the CASSIE message played at the start of a blackout.
        /// </summary>
        [Description("Message said by Cassie on the blackout start, delayed by time set on TimeBetweenSentenceAndStart if the countdown is enabled.")]
        public string CassieMessageStart { get; set; } = "pitch_0.25 .g4 . pitch_0.45 .g3 pitch_0.95 . ATTENTION . AN IMPORTANT . MESSAGE . pitch_0.98 the facility control system pitch_0.25 .g1 pitch_0.93 critical failure";

        /// <summary>
        /// Gets or sets the CASSIE message played for a facility-wide blackout.
        /// </summary>
        [Description("Message said by Cassie after CassiePostMessage if blackout occurs at whole site.")]
        public string CassieMessageFacility { get; set; } = "The Facility .";

        /// <summary>
        /// Gets or sets the CASSIE message played for an Entrance Zone blackout.
        /// </summary>
        [Description("Message said by Cassie after CassiePostMessage if blackout occurs at the Entrance Zone.")]
        public string CassieMessageEntrance { get; set; } = "The Entrance Zone .";

        /// <summary>
        /// Gets or sets the CASSIE message played for a Light Containment Zone blackout.
        /// </summary>
        [Description("Message said by Cassie after CassiePostMessage if blackout occurs at the Light Containment Zone.")]
        public string CassieMessageLight { get; set; } = "The Light Containment Zone .";

        /// <summary>
        /// Gets or sets the CASSIE message played for a Heavy Containment Zone blackout.
        /// </summary>
        [Description("Message said by Cassie after CassiePostMessage if blackout occurs at the Heavy Containment Zone.")]
        public string CassieMessageHeavy { get; set; } = "pitch_0.95 jam_045_2 .G4 .G3 . door control system patching in . progress jam_025_2 .G5 . pitch_1.5 .G5 . .G5 . .G5 . .G5 . .G5 . .G5 . .G5 jam_035_3 .G3";

        /// <summary>
        /// Gets or sets the CASSIE message played for a Surface Zone blackout.
        /// </summary>
        [Description("Message said by Cassie after CassiePostMessage if blackout occurs at the Surface Zone.")]
        public string CassieMessageSurface { get; set; } = "The Surface .";

        /// <summary>
        /// Gets or sets the CASSIE message played for an unspecified zone blackout.
        /// </summary>
        [Description("Message said by Cassie after CassiePostMessage if blackout occurs at random rooms in facility when zone is unknown or unspecified.")]
        public string CassieMessageOther { get; set; } = ". pitch_0.35 .g6 pitch_0.95 the malfunction is Unspecified .";

        /// <summary>
        /// Gets or sets the CASSIE sound played during a blackout.
        /// </summary>
        [Description("The sound CASSIE will make during a blackout.")]
        public string CassieKeter { get; set; } = "pitch_0.15 .g7";

        /// <summary>
        /// Gets or sets the CASSIE message played when a blackout ends.
        /// </summary>
        [Description("The message CASSIE will say when a blackout ends.")]
        public string CassieMessageEnd { get; set; } = "pitch_0.45 .g4 pitch_0.65 . .g3 .g1 pitch_1.0 . IMPORTANT MESSAGE . pitch_0.98 the facility . door control system . is now . pitch_0.95 operational";

        #endregion

        #region Probability Settings

        /// <summary>
        /// Gets or sets a value indicating whether a facility-wide blackout occurs if no zones are selected.
        /// </summary>
        [Description("A blackout in the whole facility will occur if none of the zones is selected randomly and EnableFacilityBlackout is set to true.")]
        public bool EnableFacilityBlackout { get; private set; } = true;

        /// <summary>
        /// Gets or sets the percentage chance of a blackout in the Heavy Containment Zone.
        /// </summary>
        [Description("Percentage chance of an outage at the Heavy Containment Zone during the blackout.")]
        public int ChanceHeavy
        {
            get => _chanceHeavy;
            set => _chanceHeavy = value < 0 ? 0 : value > 100 ? 100 : value;
        }
        private int _chanceHeavy = 75;

        /// <summary>
        /// Gets or sets the percentage chance of a blackout in the Light Containment Zone.
        /// </summary>
        [Description("Percentage chance of an outage at the Light Containment Zone during the blackout.")]
        public int ChanceLight
        {
            get => _chanceLight;
            set => _chanceLight = value < 0 ? 0 : value > 100 ? 100 : value;
        }
        private int _chanceLight = 35;

        /// <summary>
        /// Gets or sets the percentage chance of a blackout in the Entrance Zone.
        /// </summary>
        [Description("Percentage chance of an outage at the Entrance Zone during the blackout.")]
        public int ChanceEntrance
        {
            get => _chanceEntrance;
            set => _chanceEntrance = value < 0 ? 0 : value > 100 ? 100 : value;
        }
        private int _chanceEntrance = 65;

        /// <summary>
        /// Gets or sets the percentage chance of a blackout in the Surface Zone.
        /// </summary>
        [Description("Percentage chance of an outage at the Surface Zone during the blackout.")]
        public int ChanceSurface
        {
            get => _chanceSurface;
            set => _chanceSurface = value < 0 ? 0 : value > 100 ? 100 : value;
        }
        private int _chanceSurface = 20;

        /// <summary>
        /// Gets or sets the percentage chance of a blackout in an unspecified zone.
        /// </summary>
        [Description("Percentage chance of an outage at an unknown and unspecified type of zone during the blackout.")]
        public int ChanceOther
        {
            get => _chanceOther;
            set => _chanceOther = value < 0 ? 0 : value > 100 ? 100 : value;
        }
        private int _chanceOther = 15;

        /// <summary>
        /// Gets or sets a value indicating whether to use per-room probability settings instead of per-zone settings.
        /// </summary>
        [Description("Change this to true if want to use per room probability settings instead of per zone settings. The script will check all rooms in the specified zone with its probability.")]
        public bool UsePerRoomChances { get; set; } = true;

        #endregion
    }
}