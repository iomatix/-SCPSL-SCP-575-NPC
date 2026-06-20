namespace SCP_575.Shared.Audio.Enums
{
    /// <summary>
    /// Defines semantic identifiers for the SCP-575 audio subsystem.
    /// Decouples external event triggers from physical file names, allowing the audio manager 
    /// to handle randomized asset shuffling and protect channels from resource degradation.
    /// </summary>
    public enum AudioKey
    {
        // ===================================================================
        // ENTITY VOCALIZATIONS & REACTIONS
        // ===================================================================

        /// <summary>
        /// Standard dynamic vocalizations or close-proximity warning cues emitted during active stalk phases.
        /// Draws from a shuffle pool to prevent predictable acoustic signatures.
        /// </summary>
        ScreamStandard,

        /// <summary>
        /// Violent auditory retaliation triggered when the entity is forced to break a stalk or retreat.
        /// </summary>
        ScreamAngry,

        /// <summary>
        /// Distress vocal feedback indicating physical trauma sustained from flashlights, flashbangs, or headlights.
        /// </summary>
        ScreamHurt,

        /// <summary>
        /// Critical frequency decay emitted during permanent substation containment grid activation sequences.
        /// </summary>
        ScreamDying,

        /// <summary>
        /// Non-spatialized map-wide notification marking event initialization or global lifecycle changes.
        /// </summary>
        MonsterRoarGlobal,

        /// <summary>
        /// Localized breathing cues indicating close-range hunting positioning within completely unlit zones.
        /// </summary>
        MonsterBreathLocal,

        // ===================================================================
        // PSYCHOLOGICAL PARANOIA & SANITY DECAY (TIERED INTENSITY)
        // ===================================================================

        /// <summary>
        /// Tier 1: Distant, subtle vocal anomalies tracking early stages of client neurological breakdown.
        /// </summary>
        WhispersSubtle,

        /// <summary>
        /// Tier 2: Heavy multi-layered intrusive vocal entities handling intermediate cognitive decay.
        /// </summary>
        WhispersDisturbed,

        /// <summary>
        /// Tier 3: Overlapping, chaotic cognitive failure voices processing critical sanity thresholds.
        /// </summary>
        WhispersPsychotic,

        /// <summary>
        /// Tier 4: High-amplitude physical jump-scare stingers designed to force immediate panic reactions.
        /// Strictly separated from looping logic to prevent resource clumping.
        /// </summary>
        WhispersShockStinger,

        /// <summary>
        /// Continuous spatialized tracking sub-drone indicating extreme neurological decay state overrides.
        /// Constrained as a permanent asset layer with explicit lifecycle cleanup routes.
        /// </summary>
        WhispersPanicDrone,

        /// <summary>
        /// Rhythmic environmental click warnings tracking early shadow materialization states inside dark zones.
        /// </summary>
        ShadowClicking,

        // ===================================================================
        // KINETIC TRAUMA & TACTICAL INTERACTION FEEDBACK
        // ===================================================================

        /// <summary>
        /// Physical kinetic transaction acoustics mapping entity claw strikes onto human targets.
        /// </summary>
        ShadowStrike,

        /// <summary>
        /// Continuous biological textures tracking the organic consumption of dead human targets.
        /// </summary>
        ShadowConsumingBody,

        /// <summary>
        /// Structural pressure cues tracking tactical explosive payload or flashbang impacts across rooms.
        /// </summary>
        AnomalousImpact,

        /// <summary>
        /// Continuous localized electrical energy fields emitted by active facility power stations.
        /// </summary>
        GeneratorHumDefense,

        /// <summary>
        /// Intense over-voltage failure indications tracking localized infrastructure breakdowns or burnt bulbs.
        /// </summary>
        LightShortCircuit,

        /// <summary>
        /// Transient acoustic feedback tracking tactical firearm-attached flashlight power state updates.
        /// </summary>
        LightSwitch,

        /// <summary>
        /// Minimal low-voltage field degradation tracking fading environmental illumination assets.
        /// </summary>
        StaticBuzz,

        // ===================================================================
        // ENVIRONMENTAL ACOUSTIC BACKGROUNDS & STATE TRANSITIONS
        // ===================================================================

        /// <summary>
        /// Global atmospheric baseline texture tracking active darkness exposure state gates.
        /// </summary>
        Ambience,

        /// <summary>
        /// Unspatialized low-frequency tension sub-drones tracking deep psychological threat levels.
        /// </summary>
        SanityLowDrone,

        /// <summary>
        /// High-energy baseline facility impact marking initial environmental blackout state shifts.
        /// </summary>
        BlackoutImpactGlobal
    }
}