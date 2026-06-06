namespace SCP_575.Shared.Audio.Enums
{
    /// <summary>
    /// Defines architectural registration keys for SCP-575 spatialized and global audio layers.
    /// Used by the audio management systems to map asset streams to runtime event triggers.
    /// </summary>
    public enum AudioKey
    {
        // ===================================================================
        // TRANSIENT VOCALIZATIONS & VOCAL ATTACKS
        // ===================================================================

        /// <summary>
        /// Primary entity vocalization variation used to prevent acoustic repetition during manifestations.
        /// </summary>
        Scream_1,

        /// <summary>
        /// Secondary entity vocalization variation used to prevent acoustic repetition during manifestations.
        /// </summary>
        Scream_2,

        /// <summary>
        /// Tertiary entity vocalization variation used to prevent acoustic repetition during manifestations.
        /// </summary>
        Scream_3,

        /// <summary>
        /// Localized hostile vocalization triggered when the entity experiences structural disruption from light sources.
        /// </summary>
        ScreamAngry,

        /// <summary>
        /// Map-wide auditory event signaling the onset of a successful attack on the anomalous entity.
        /// </summary>
        ScreamHurt,

        /// <summary>
        /// High-priority map-wide auditory event signaling the permanent termination of the anomalous entity.
        /// </summary>
        /// TODO
        ScreamDying,

        /// <summary>
        /// Low-frequency global acoustic roar designed to establish atmospheric dread across the zone layout.
        /// </summary>
        /// TODO
        MonsterRoarGlobal,

        // ===================================================================
        // SPATIALIZED PSYCHOLOGICAL FEEDBACK & PARANOIA
        // ===================================================================

        /// <summary>
        /// Ambient, disconnected non-directional vocal whispers signaling early cognitive degradation.
        /// </summary>
        Whispers_1,

        /// <summary>
        /// Ambient, disconnected non-directional vocal whispers signaling early cognitive degradation.
        /// </summary>
        Whispers_2,

        /// <summary>
        /// Ambient, disconnected non-directional vocal whispers signaling early cognitive degradation.
        /// </summary>
        Whispers_3,

        /// <summary>
        /// High-transient psychological audio node combining vocal whispers with sudden physical impact characteristics.
        /// </summary>
        /// TODO
        WhispersBang,

        /// <summary>
        /// Layered, multi-directional overlapping vocal whispers denoting advancing psychological deterioration.
        /// </summary>
        /// TODO
        WhispersMixed,

        /// <summary>
        /// Close-proximity, highly localized 3D respiratory sequence rendered strictly to an active target's position.
        /// </summary>
        /// TODO
        MonsterBreatheLocal,

        /// <summary>
        /// Transient chittering or bone-cracking textures indicating physical manipulation of shadows.
        /// </summary>
        /// TODO
        ShadowClicking,

        // ===================================================================
        // KINETIC TRAUMA & TACTICAL INTERACTION FEEDBACK
        // ===================================================================

        /// <summary>
        /// Signature high-velocity kinetic strike combined with a guttural growl texture.
        /// Deployed directly within damage events to provide deterministic auditory hit validation.
        /// </summary>
        ShadowStrike,

        /// <summary>
        /// Severe physical structural resonance emitted directly from a sub-station generator during grid synchronization.
        /// </summary>
        /// TODO
        GeneratorHumDefense,

        // ===================================================================
        // ENVIRONMENTAL ACOUSTIC BACKGROUNDS & ZONE STATE TRANSITIONS
        // ===================================================================

        /// <summary>
        /// Default continuous low-priority looping soundscape managing background tension values.
        /// </summary>
        /// TODO
        Ambience,

        /// <summary>
        /// Sub-bass drone frequency signature deployed to isolate a player's acoustic space during severe breakdown.
        /// </summary>
        /// TODO
        SanityLowDrone,

        /// <summary>
        /// High-energy, low-frequency cinematic sonic signature executing concurrently with emergency grid collapses.
        /// </summary>
        /// TODO
        BlackoutImpactGlobal
    }
}