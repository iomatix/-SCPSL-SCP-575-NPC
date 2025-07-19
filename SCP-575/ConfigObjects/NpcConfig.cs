namespace SCP_575.ConfigObjects
{
    using System.ComponentModel;
    using System;

    /// <summary>
    /// Configuration settings for SCP-575 NPC behavior, including damage, player effects, and notifications.
    /// </summary>
    public class NpcConfig
    {
        #region General Settings

        /// <summary>
        /// Gets or sets a value indicating whether randomly timed blackout events should occur.
        /// </summary>
        [Description("Whether or not randomly timed events should occur. If false, all events will be at the same interval apart.")]
        public bool RandomEvents { get; private set; } = true;

        /// <summary>
        /// Gets or sets the delay (in seconds) before the first blackout event of each round.
        /// </summary>
        [Description("The delay before the first event of each round, in seconds.")]
        public float InitialDelay
        {
            get => _initialDelay;
            private set => _initialDelay = value < 0f ? 0f : value;
        }
        private float _initialDelay = 300f;

        /// <summary>
        /// Gets or sets the minimum duration (in seconds) of a blackout event.
        /// </summary>
        [Description("The minimum number of seconds a blackout event can last.")]
        public float DurationMin
        {
            get => _durationMin;
            private set => _durationMin = value < 0f ? 0f : value;
        }
        private float _durationMin = 30f;

        /// <summary>
        /// Gets or sets the maximum duration (in seconds) of a blackout event.
        /// </summary>
        [Description("The maximum number of seconds a blackout event can last. If RandomEvents is disabled, this will be the duration for every event.")]
        public float DurationMax
        {
            get => _durationMax;
            private set => _durationMax = value < 0f ? 0f : value;
        }
        private float _durationMax = 90f;

        /// <summary>
        /// Gets or sets the minimum delay (in seconds) between blackout events.
        /// </summary>
        [Description("The minimum amount of seconds between each event.")]
        public int DelayMin
        {
            get => _delayMin;
            private set => _delayMin = value < 0 ? 0 : value;
        }
        private int _delayMin = 180;

        /// <summary>
        /// Gets or sets the maximum delay (in seconds) between blackout events.
        /// </summary>
        [Description("The maximum amount of seconds between each event. If RandomEvents is disabled, this will be the delay between every event.")]
        public int DelayMax
        {
            get => _delayMax;
            private set => _delayMax = value < 0 ? 0 : value;
        }
        private int _delayMax = 500;

        /// <summary>
        /// Gets or sets the percentage chance that SCP-575 events occur in a round.
        /// </summary>
        [Description("The percentage chance that SCP-575 events will occur in any particular round.")]
        public int SpawnChance
        {
            get => _spawnChance;
            private set => _spawnChance = value < 0 ? 0 : value > 100 ? 100 : value;
        }
        private int _spawnChance = 45;

        #endregion

        #region Facility Effects

        /// <summary>
        /// Gets or sets a value indicating whether Tesla gates should be disabled during blackouts.
        /// </summary>
        [Description("Whether or not tesla gates should be disabled during blackouts.")]
        public bool DisableTeslas { get; private set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether nuke detonation should be cancelled during blackouts.
        /// </summary>
        [Description("Whether or not nuke detonation should be cancelled during blackouts.")]
        public bool DisableNuke { get; private set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether lights should flicker when a blackout starts.
        /// </summary>
        [Description("Flicker lights when the event starts.")]
        public bool FlickerLights { get; private set; } = true;

        /// <summary>
        /// Gets or sets the duration (in seconds) of the initial light flickering.
        /// </summary>
        [Description("The number of seconds a first flickering lasts.")]
        public float FlickerLightsDuration
        {
            get => _flickerLightsDuration;
            private set => _flickerLightsDuration = value < 0f ? 0f : value;
        }
        private float _flickerLightsDuration = 1.5f;

        #endregion

        #region SCP-575 Behavior

        /// <summary>
        /// Gets or sets a value indicating whether players in dark rooms take damage from SCP-575 without a light source.
        /// </summary>
        [Description("Whether or not people in dark rooms should take damage if they have no light source in their hand.")]
        public bool EnableKeter { get; private set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether SCP-575 can be terminated when all generators are engaged.
        /// </summary>
        [Description("Specifies whether SCP-575 can be terminated when all generators are engaged. If set to false, generator activation only halts SCP-575's behavior and resets its event state without killing it.")]
        public bool IsNpcKillable { get; private set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether ragdolls are disabled for SCP-575 kills.
        /// </summary>
        [Description("Determines how kills by SCP-575 handle ragdolls. If set to false, a skeleton ragdoll is spawned instead of the default one. If set to true, no ragdoll is created upon death.")]
        public bool DisableRagdolls { get; private set; } = false;

        /// <summary>
        /// Gets or sets the base damage per stack dealt by SCP-575 when EnableKeter is true.
        /// </summary>
        [Description("Base damage per each stack of delay. The damage is inflicted if EnableKeter is set to true.")]
        public float KeterDamage
        {
            get => _keterDamage;
            private set => _keterDamage = value < 0f ? 0f : value;
        }
        private float _keterDamage = 10f;

        /// <summary>
        /// Gets or sets the penetration modifier for SCP-575 damage, similar to FirearmsDamageHandler.
        /// </summary>
        [Description("Penetration modifier same as in FirearmsDamageHandler.")]
        public float KeterDamagePenetration
        {
            get => _keterDamagePenetration;
            internal set => _keterDamagePenetration = value < 0f ? 0f : value > 1f ? 1f : value;
        }
        private float _keterDamagePenetration = 0.75f;

        /// <summary>
        /// Gets or sets the delay (in seconds) between SCP-575 damage ticks.
        /// </summary>
        [Description("The delay of receiving damage.")]
        public float KeterDamageDelay
        {
            get => _keterDamageDelay;
            private set => _keterDamageDelay = value < 0f ? 0f : value;
        }
        private float _keterDamageDelay = 7.85f;

        /// <summary>
        /// Gets or sets a value indicating whether a cooldown is applied to light sources after being hit by SCP-575.
        /// </summary>
        [Description("Whether or not to enable cooldown on the light source triggered on hit by SCP-575.")]
        public bool EnableKeterLightsourceCooldown { get; private set; } = true;

        /// <summary>
        /// Gets or sets the cooldown duration (in seconds) for light sources after being hit by SCP-575.
        /// </summary>
        [Description("Cooldown on the light source triggered on hit by SCP-575.")]
        public float KeterLightsourceCooldown
        {
            get => _keterLightsourceCooldown;
            private set => _keterLightsourceCooldown = value < 0f ? 0f : value;
        }
        private float _keterLightsourceCooldown = 7.25f;

        /// <summary>
        /// Gets or sets the minimum force modifier applied to ragdolls damaged by SCP-575.
        /// </summary>
        [Description("The minimum modifier applied to ragdolls when they were damaged by SCP-575.")]
        public float KeterForceMinModifier
        {
            get => _keterForceMinModifier;
            set => _keterForceMinModifier = value < 0f ? 0f : value;
        }
        private float _keterForceMinModifier = 0.75f;

        /// <summary>
        /// Gets or sets the maximum force modifier applied to ragdolls damaged by SCP-575.
        /// </summary>
        [Description("The maximum modifier applied to ragdolls when they were damaged by SCP-575.")]
        public float KeterForceMaxModifier
        {
            get => _keterForceMaxModifier;
            set => _keterForceMaxModifier = value < 0f ? 0f : value;
        }
        private float _keterForceMaxModifier = 2.35f;

        /// <summary>
        /// Gets or sets the velocity modifier applied to players damaged by SCP-575.
        /// </summary>
        [Description("The modifier applied to velocity when players are damaged by SCP-575.")]
        public float KeterDamageVelocityModifier
        {
            get => _keterDamageVelocityModifier;
            set => _keterDamageVelocityModifier = value < 0f ? 0f : value;
        }
        private float _keterDamageVelocityModifier = 1.25f;

        #endregion

        #region Player Effects

        /// <summary>
        /// Gets or sets a value indicating whether effects are applied to players when damaged by SCP-575.
        /// </summary>
        [Description("Whether or not to enable effects triggered by SCP-575 on player hurt.")]
        public bool EnableKeterOnDealDamageEffects { get; private set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether the Ensnared effect is applied when damaged by SCP-575.
        /// </summary>
        [Description("Enable 'Ensnared' effect when damaged by SCP-575.")]
        public bool EnableEffectEnsnared { get; private set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether the Flashed effect is applied when damaged by SCP-575.
        /// </summary>
        [Description("Enable 'Flashed' effect when damaged by SCP-575.")]
        public bool EnableEffectFlashed { get; private set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether the Blurred effect is applied when damaged by SCP-575.
        /// </summary>
        [Description("Enable 'Blurred' effect when damaged by SCP-575.")]
        public bool EnableEffectBlurred { get; private set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether the Deafened effect is applied when damaged by SCP-575.
        /// </summary>
        [Description("Enable 'Deafened' effect when damaged by SCP-575.")]
        public bool EnableEffectDeafened { get; private set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether the AmnesiaVision effect is applied when damaged by SCP-575.
        /// </summary>
        [Description("Enable 'AmnesiaVision' effect when damaged by SCP-575.")]
        public bool EnableEffectAmnesiaVision { get; private set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether the Sinkhole effect is applied when damaged by SCP-575.
        /// </summary>
        [Description("Enable 'Sinkhole' effect when damaged by SCP-575.")]
        public bool EnableEffectSinkhole { get; private set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether the Concussed effect is applied when damaged by SCP-575.
        /// </summary>
        [Description("Enable 'Concussed' effect when damaged by SCP-575.")]
        public bool EnableEffectConcussed { get; private set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether the Blindness effect is applied when damaged by SCP-575.
        /// </summary>
        [Description("Enable 'Blindness' effect when damaged by SCP-575.")]
        public bool EnableEffectBlindness { get; private set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether the Burned effect is applied when damaged by SCP-575.
        /// </summary>
        [Description("Enable 'Burned' effect when damaged by SCP-575.")]
        public bool EnableEffectBurned { get; private set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether the AmnesiaItems effect is applied when damaged by SCP-575.
        /// </summary>
        [Description("Enable 'AmnesiaItems' effect when damaged by SCP-575.")]
        public bool EnableEffectAmnesiaItems { get; private set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether the Stained effect is applied when damaged by SCP-575.
        /// </summary>
        [Description("Enable 'Stained' effect when damaged by SCP-575.")]
        public bool EnableEffectStained { get; private set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether the Asphyxiated effect is applied when damaged by SCP-575.
        /// </summary>
        [Description("Enable 'Asphyxiated' effect when damaged by SCP-575.")]
        public bool EnableEffectAsphyxiated { get; private set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether the Bleeding effect is applied when damaged by SCP-575.
        /// </summary>
        [Description("Enable 'Bleeding' effect when damaged by SCP-575.")]
        public bool EnableEffectBleeding { get; private set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether the Disabled effect is applied when damaged by SCP-575.
        /// </summary>
        [Description("Enable 'Disabled' effect when damaged by SCP-575.")]
        public bool EnableEffectDisabled { get; private set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether the Exhausted effect is applied when damaged by SCP-575.
        /// </summary>
        [Description("Enable 'Exhausted' effect when damaged by SCP-575.")]
        public bool EnableEffectExhausted { get; private set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether the Traumatized effect is applied when damaged by SCP-575.
        /// </summary>
        [Description("Enable 'Traumatized' effect when damaged by SCP-575.")]
        public bool EnableEffectTraumatized { get; private set; } = true;

        #endregion

        #region Notifications and Sounds

        /// <summary>
        /// Gets or sets a value indicating whether to show hint messages when players are damaged by SCP-575.
        /// </summary>
        [Description("Whether or not to inform players about being damaged by SCP-575 via Hint messages.")]
        public bool EnableKeterHint { get; private set; } = true;

        /// <summary>
        /// Gets or sets the hint message shown when a player is damaged by SCP-575.
        /// </summary>
        [Description("Hint message shown when a player is damaged by SCP-575 if EnableKeterHint is set to true.")]
        public string KeterHint { get; set; } = "You were damaged by SCP-575! Equip a flashlight!";

        /// <summary>
        /// Gets or sets a value indicating whether to show a hint when a light source is on cooldown.
        /// </summary>
        [Description("Whether or not to inform players about cooldown of the light emitter like flashlight or weapon module.")]
        public bool EnableLightEmitterCooldownHint { get; private set; } = true;

        /// <summary>
        /// Gets or sets the hint message shown when a light source is on cooldown.
        /// </summary>
        [Description("Hint message shown when a player tries to use light source while on cooldown.")]
        public string LightEmitterCooldownHint { get; set; } = "Your light source is on cooldown!";

        /// <summary>
        /// Gets or sets the hint message shown when a light source is disabled by SCP-575.
        /// </summary>
        [Description("Hint message shown when a player tries to use light source while disabled by SCP-575 event.")]
        public string LightEmitterDisabledHint { get; set; } = "Your light source has been disabled!";

        /// <summary>
        /// Gets or sets a value indicating whether to play a horror ambient sound during blackouts.
        /// </summary>
        [Description("Play horror ambient sound on blackout.")]
        public bool KeterAmbient { get; set; } = true;

        /// <summary>
        /// Gets or sets the name displayed in a player's death information.
        /// </summary>
        [Description("Name displayed in player's death information.")]
        public string KilledBy { get; set; } = "SCP-575";

        /// <summary>
        /// Gets or sets the message shown in a player's death information.
        /// </summary>
        [Description("Killed by message")]
        public string KilledByMessage { get; set; } = "Shredded apart by SCP-575";

        /// <summary>
        /// Gets or sets the text displayed when inspecting a ragdoll killed by SCP-575.
        /// </summary>
        [Description("Ragdoll death information.")]
        public string RagdollInspectText { get; set; } = "Flesh stripped by shadow tendrils, leaving a shadowy skeleton.";

        #endregion

        #region CASSIE Messages

        /// <summary>
        /// Gets or sets the glitch chance per word in CASSIE sentences.
        /// </summary>
        [Description("Glitch chance during message per word in CASSIE sentence.")]
        public float GlitchChance
        {
            get => _glitchChance;
            private set => _glitchChance = value < 0f ? 0f : value > 100f ? 100f : value;
        }
        private float _glitchChance = 10f;

        /// <summary>
        /// Gets or sets the jam chance per word in CASSIE sentences.
        /// </summary>
        [Description("Jam chance during message per word in CASSIE sentence.")]
        public float JamChance
        {
            get => _jamChance;
            private set => _jamChance = value < 0f ? 0f : value > 100f ? 100f : value;
        }
        private float _jamChance = 5f;

        /// <summary>
        /// Gets or sets the CASSIE message played when no blackout occurs.
        /// </summary>
        [Description("Message said by Cassie if no blackout occurs")]
        public string CassieMessageWrong { get; set; } = ". I have prevented the system failure . .g5 Sorry for a .g3 . false alert .";

        /// <summary>
        /// Gets or sets the CASSIE message played at the start of a blackout.
        /// </summary>
        [Description("Message said by Cassie when a blackout starts.")]
        public string CassieMessageStart { get; set; } = "facility power system outage in 3 . 2 . 1 .";

        /// <summary>
        /// Gets or sets the delay (in seconds) between the CASSIE message and the blackout start.
        /// </summary>
        [Description("The time between the sentence and the 3 . 2 . 1 announcement")]
        public float TimeBetweenSentenceAndStart
        {
            get => _timeBetweenSentenceAndStart;
            set => _timeBetweenSentenceAndStart = value < 0f ? 0f : value;
        }
        private float _timeBetweenSentenceAndStart = 8.6f;

        /// <summary>
        /// Gets or sets the CASSIE message played just after a blackout starts.
        /// </summary>
        [Description("Message said by Cassie just after the blackout.")]
        public string CassiePostMessage { get; set; } = "facility power system malfunction has been detected at .";

        /// <summary>
        /// Gets or sets the delay (in seconds) between the blackout end and the CASSIE end message.
        /// </summary>
        [Description("The time between the sentence of the blackout end.")]
        public float TimeBetweenSentenceAndEnd
        {
            get => _timeBetweenSentenceAndEnd;
            set => _timeBetweenSentenceAndEnd = value < 0f ? 0f : value;
        }
        private float _timeBetweenSentenceAndEnd = 7.0f;

        /// <summary>
        /// Gets or sets the CASSIE message played for a facility-wide blackout.
        /// </summary>
        [Description("Message said by Cassie after CassiePostMessage if outage occurs at whole site.")]
        public string CassieMessageFacility { get; set; } = "The Facility .";

        /// <summary>
        /// Gets or sets the CASSIE message played for an Entrance Zone blackout.
        /// </summary>
        [Description("Message said by Cassie after CassiePostMessage if outage occurs at the Entrance Zone.")]
        public string CassieMessageEntrance { get; set; } = "The Entrance Zone .";

        /// <summary>
        /// Gets or sets the CASSIE message played for a Light Containment Zone blackout.
        /// </summary>
        [Description("Message said by Cassie after CassiePostMessage if outage occurs at the Light Containment Zone.")]
        public string CassieMessageLight { get; set; } = "The Light Containment Zone .";

        /// <summary>
        /// Gets or sets the CASSIE message played for a Heavy Containment Zone blackout.
        /// </summary>
        [Description("Message said by Cassie after CassiePostMessage if outage occurs at the Heavy Containment Zone.")]
        public string CassieMessageHeavy { get; set; } = "The Heavy Containment Zone.";

        /// <summary>
        /// Gets or sets the CASSIE message played for a Surface Zone blackout.
        /// </summary>
        [Description("Message said by Cassie after CassiePostMessage if outage occurs at the Surface Zone.")]
        public string CassieMessageSurface { get; set; } = "The Surface .";

        /// <summary>
        /// Gets or sets the CASSIE message played for an unspecified zone blackout.
        /// </summary>
        [Description("Message said by Cassie after CassiePostMessage if outage occurs at unknown type of zones or unspecified zones.")]
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
        public string CassieMessageEnd { get; set; } = "facility power system now operational";

        /// <summary>
        /// Gets or sets a value indicating whether to clear the CASSIE message queue before important messages.
        /// </summary>
        [Description("Should cassie clear the message and broadcast cue before important message to prevent spam?")]
        public bool CassieMessageClearBeforeImportant { get; set; } = true;

        #endregion

        #region Zone Probabilities

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
        private int _chanceHeavy = 99;

        /// <summary>
        /// Gets or sets the percentage chance of a blackout in the Light Containment Zone.
        /// </summary>
        [Description("Percentage chance of an outage at the Light Containment Zone during the blackout.")]
        public int ChanceLight
        {
            get => _chanceLight;
            set => _chanceLight = value < 0 ? 0 : value > 100 ? 100 : value;
        }
        private int _chanceLight = 45;

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
        private int _chanceSurface = 25;

        /// <summary>
        /// Gets or sets the percentage chance of a blackout in an unspecified zone.
        /// </summary>
        [Description("Percentage chance of an outage at an unknown and unspecified type of zone during the blackout.")]
        public int ChanceOther
        {
            get => _chanceOther;
            set => _chanceOther = value < 0 ? 0 : value > 100 ? 100 : value;
        }
        private int _chanceOther = 0;

        /// <summary>
        /// Gets or sets a value indicating whether to use per-room probability settings instead of per-zone settings.
        /// </summary>
        [Description("Change this to true if want to use per room probability settings instead of per zone settings. The script will check all rooms in the specified zone with its probability.")]
        public bool UsePerRoomChances { get; set; } = false;

        #endregion

        #region Misc and Utils

        /// <summary>
        /// Gets or sets the interval (in seconds) for automatic cleanup of event handlers.
        /// </summary>
        [Description("Per how many seconds automatic cleanups of event handlers are called.")]
        public float HandlerCleanupInterval
        {
            get => _handlerCleanupInterval;
            set => _handlerCleanupInterval = value < 0f ? 0f : value;
        }
        private float _handlerCleanupInterval = 90f;

        #endregion

        /// <summary>
        /// Validates the configuration settings to ensure logical consistency.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if configuration settings are invalid.</exception>
        public void Validate()
        {
            if (_durationMin > _durationMax)
                throw new InvalidOperationException("DurationMin must not exceed DurationMax.");
            if (_delayMin > _delayMax)
                throw new InvalidOperationException("DelayMin must not exceed DelayMax.");
            if (_keterForceMinModifier > _keterForceMaxModifier)
                throw new InvalidOperationException("KeterForceMinModifier must not exceed KeterForceMaxModifier.");
        }
    }
}