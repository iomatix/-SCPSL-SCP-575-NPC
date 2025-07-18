namespace SCP_575.ConfigObjects
{
    using System.ComponentModel;
    using Exiled.API.Features;

    public class NpcConfig
    {
        //region General Settings
        [Description("Whether or not randomly timed events should occur. If false, all events will be at the same interval apart.")]
        public bool RandomEvents { get; private set; } = true;

        [Description("The delay before the first event of each round, in seconds.")]
        public float InitialDelay { get; private set; } = 300f;

        [Description("The minimum number of seconds a blackout event can last.")]
        public float DurationMin { get; private set; } = 30f;

        [Description("The maximum number of seconds a blackout event can last. If RandomEvents is disabled, this will be the duration for every event.")]
        public float DurationMax { get; private set; } = 90f;

        [Description("The minimum amount of seconds between each event.")]
        public int DelayMin { get; private set; } = 180;

        [Description("The maximum amount of seconds between each event. If RandomEvents is disabled, this will be the delay between every event.")]
        public int DelayMax { get; private set; } = 500;

        [Description("The percentage chance that SCP-575 events will occur in any particular round.")]
        public int SpawnChance { get; private set; } = 45;
        //endregion

        //region Facility Effects
        [Description("Whether or not tesla gates should be disabled during blackouts.")]
        public bool DisableTeslas { get; private set; } = true;

        [Description("Whether or not nuke detonation should be cancelled during blackouts.")]
        public bool DisableNuke { get; private set; } = true;

        [Description("Flicker lights when the event starts.")]
        public bool FlickerLights { get; private set; } = true;

        [Description("The number of seconds a first flickering lasts.")]
        public float FlickerLightsDuration { get; private set; } = 1.5f;
        //endregion

        //region SCP-575 Behavior
        [Description("Whether or not people in dark rooms should take damage if they have no light source in their hand.")]
        public bool EnableKeter { get; private set; } = true;

        [Description("Specifies whether SCP-575 can be terminated when all generators are engaged. If set to false, generator activation only halts SCP-575's behavior and resets its event state without killing it.")]
        public bool IsNpcKillable { get; private set; } = false;

        [Description("Determines how kills by SCP-575 handle ragdolls. If set to false, a skeleton ragdoll is spawned instead of the default one. If set to true, no ragdoll is created upon death.")]
        public bool DisableRagdolls { get; private set; } = false;

        [Description("Base damage per each stack of delay. The damage is inflicted if EnableKeter is set to true.")]
        public float KeterDamage { get; private set; } = 10f;

        [Description("Penetration modifier same as in FirearmsDamageHandler.")]
        public float KeterDamagePenetration { get; internal set; } = 0.75f;

        [Description("The delay of receiving damage.")]
        public float KeterDamageDelay { get; private set; } = 7.85f;

        [Description("Wheter or not to enable cooldown on the light source triggered on hit by SCP-575.")]
        public bool EnableKeterLightsourceCooldown { get; private set; } = true;

        [Description("Cooldown on the light source triggered on hit by SCP-575.")]
        public float KeterLightsourceCooldown { get; private set; } = 7.25f;

        [Description("The minimum modifier applied to ragdolls when they were damaged by SCP-575.")]
        public float KeterForceMinModifier { get; set; } = 0.75f;

        [Description("The maximum modifier applied to ragdolls when they were damaged by SCP-575.")]
        public float KeterForceMaxModifier { get; set; } = 2.35f;

        [Description("The modifier applied to velocity when players are damaged by SCP-575.")]
        public float KeterDamageVelocityModifier { get; set; } = 1.25f;
        //endregion

        //region Player Effects
        [Description("Whether or not to enable effects triggered by SCP-575 on player hurt.")]
        public bool EnableKeterOnDealDamageEffects { get; private set; } = true;

        [Description("Enable 'Ensnared' effect when damaged by SCP-575.")]
        public bool EnableEffectEnsnared { get; private set; } = true;

        [Description("Enable 'Flashed' effect when damaged by SCP-575.")]
        public bool EnableEffectFlashed { get; private set; } = true;

        [Description("Enable 'Blurred' effect when damaged by SCP-575.")]
        public bool EnableEffectBlurred { get; private set; } = true;

        [Description("Enable 'Deafened' effect when damaged by SCP-575.")]
        public bool EnableEffectDeafened { get; private set; } = true;

        [Description("Enable 'AmnesiaVision' effect when damaged by SCP-575.")]
        public bool EnableEffectAmnesiaVision { get; private set; } = true;

        [Description("Enable 'Sinkhole' effect when damaged by SCP-575.")]
        public bool EnableEffectSinkhole { get; private set; } = true;

        [Description("Enable 'Concussed' effect when damaged by SCP-575.")]
        public bool EnableEffectConcussed { get; private set; } = true;

        [Description("Enable 'Blindness' effect when damaged by SCP-575.")]
        public bool EnableEffectBlindness { get; private set; } = true;

        [Description("Enable 'Burned' effect when damaged by SCP-575.")]
        public bool EnableEffectBurned { get; private set; } = false;

        [Description("Enable 'AmnesiaItems' effect when damaged by SCP-575.")]
        public bool EnableEffectAmnesiaItems { get; private set; } = false;

        [Description("Enable 'Stained' effect when damaged by SCP-575.")]
        public bool EnableEffectStained { get; private set; } = true;

        [Description("Enable 'Asphyxiated' effect when damaged by SCP-575.")]
        public bool EnableEffectAsphyxiated { get; private set; } = false;

        [Description("Enable 'Bleeding' effect when damaged by SCP-575.")]
        public bool EnableEffectBleeding { get; private set; } = false;

        [Description("Enable 'Disabled' effect when damaged by SCP-575.")]
        public bool EnableEffectDisabled { get; private set; } = true;

        [Description("Enable 'Exhausted' effect when damaged by SCP-575.")]
        public bool EnableEffectExhausted { get; private set; } = true;

        [Description("Enable 'Traumatized' effect when damaged by SCP-575.")]
        public bool EnableEffectTraumatized { get; private set; } = true;
        //endregion

        //region Notifications and Sounds
        [Description("Whether or not to inform players about being damaged by SCP-575 via Hint messages.")]
        public bool EnableKeterHint { get; private set; } = true;

        [Description("Hint message shown when a player is damaged by SCP-575 if EnableKeterHint is set to true.")]
        public string KeterHint { get; set; } = "You were damaged by SCP-575! Equip a flashlight!";
        
        [Description("Whether or not to inform players about cooldown of the light emitter like flashlight or weapon module.")]
        public bool EnableLightEmitterCooldownHint { get; private set; } = true;

        [Description("Hint message shown when a player tries to use light source while on cooldown.")]
        public string LightEmitterCooldownHint { get; set; } = "Your light source is on cooldown!";

        [Description("Hint message shown when a player tries to use light source while disabled by SCP-575 event.")]
        public string LightEmitterDisabledHint { get; set; } = "Your light source has been disabled!";



        [Description("Whether or not SCP-575's sound effect should happen on the client damaged by the entity.")]
        public bool EnableScreamSound { get; private set; } = true;

        [Description("Play horror ambient sound on blackout.")]
        public bool KeterAmbient { get; set; } = true;

        [Description("Name displayed in player's death information.")]
        public string KilledBy { get; set; } = "SCP-575";

        [Description("Killed by message")]
        public string KilledByMessage { get; set; } = "Shredded apart by SCP-575";

        [Description("Ragdoll death information.")]
        public string RagdollInspectText { get; set; } = "Flesh stripped by shadow tendrils, leaving a shadowy skeleton.";
        //endregion

        //region CASSIE Messages
        [Description("Glitch chance during message per word in CASSIE sentence.")]
        public float GlitchChance { get; private set; } = 10f;

        [Description("Jam chance during message per word in CASSIE sentence.")]
        public float JamChance { get; private set; } = 5f;

        [Description("Message said by Cassie if no blackout occurs")]
        public string CassieMessageWrong { get; set; } = ". I have prevented the system failure . .g5 Sorry for a .g3 . false alert .";

        [Description("Message said by Cassie when a blackout starts.")]
        public string CassieMessageStart { get; set; } = "facility power system outage in 3 . 2 . 1 .";

        [Description("The time between the sentence and the 3 . 2 . 1 announcement")]
        public float TimeBetweenSentenceAndStart { get; set; } = 8.6f;

        [Description("Message said by Cassie just after the blackout.")]
        public string CassiePostMessage { get; set; } = "facility power system malfunction has been detected at .";

        [Description("The time between the sentence of the blockout end.")]
        public float TimeBetweenSentenceAndEnd { get; set; } = 7.0f;

        [Description("Message said by Cassie after CassiePostMessage if outage gonna occure at whole site.")]
        public string CassieMessageFacility { get; set; } = "The Facility .";

        [Description("Message said by Cassie after CassiePostMessage if outage gonna occure at the Entrance Zone.")]
        public string CassieMessageEntrance { get; set; } = "The Entrance Zone .";

        [Description("Message said by Cassie after CassiePostMessage if outage gonna occure at the Light Containment Zone.")]
        public string CassieMessageLight { get; set; } = "The Light Containment Zone .";

        [Description("Message said by Cassie after CassiePostMessage if outage gonna occure at the Heavy Containment Zone.")]
        public string CassieMessageHeavy { get; set; } = "The Heavy Containment Zone.";

        [Description("Message said by Cassie after CassiePostMessage if outage gonna occure at the entrance zone.")]
        public string CassieMessageSurface { get; set; } = "The Surface .";

        [Description("Message said by Cassie after CassiePostMessage if outage gonna occure at unknown type of zones or unspecified zones.")]
        public string CassieMessageOther { get; set; } = ". pitch_0.35 .g6 pitch_0.95 the malfunction is Unspecified .";

        [Description("The sound CASSIE will make during a blackout.")]
        public string CassieKeter { get; set; } = "pitch_0.15 .g7";

        [Description("The message CASSIE will say when a blackout ends.")]
        public string CassieMessageEnd { get; set; } = "facility power system now operational";

        [Description("Should cassie clear the messeage and broadcast cue before important message to prevent spam?")]
        public bool CassieMessageClearBeforeImportant { get; set; } = true;
        //endregion

        //region Zone Probabilities
        [Description("A blackout in the whole facility will occur if none of the zones is selected randomly and EnableFacilityBlackout is set to true.")]
        public bool EnableFacilityBlackout { get; private set; } = true;

        [Description("Percentage chance of an outage at the Heavy Containment Zone during the blackout.")]
        public int ChanceHeavy { get; set; } = 99;

        [Description("Percentage chance of an outage at the Light Containment Zone during the blackout.")]
        public int ChanceLight { get; set; } = 45;

        [Description("Percentage chance of an outage at the Entrance Zone during the blackout.")]
        public int ChanceEntrance { get; set; } = 65;

        [Description("Percentage chance of an outage at the Surface Zone during the blackout.")]
        public int ChanceSurface { get; set; } = 25;

        [Description("Percentage chance of an outage at an unknown and unspecified type of zone during the blackout.")]
        public int ChanceOther { get; set; } = 0;

        [Description("Change this to true if want to use per room probability settings instead of per zone settings. The script will check all rooms in the specified zone with its probability.")]
        public bool UsePerRoomChances { get; set; } = false;
        //endregion
    }
}