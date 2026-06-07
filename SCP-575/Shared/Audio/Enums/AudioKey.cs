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

        Scream_1,


        Scream_2,


        Scream_3,

        ScreamAngry,

        ScreamHurt,

        ScreamDying,


        MonsterRoarGlobal,

        // ===================================================================
        // SPATIALIZED PSYCHOLOGICAL FEEDBACK & PARANOIA
        // ===================================================================

        Whispers_1,
        Whispers_2,
        Whispers_3,
        WhispersBang,
        WhispersMixed,
        MonsterBreathLocal,
        ShadowClicking,

        // ===================================================================
        // KINETIC TRAUMA & TACTICAL INTERACTION FEEDBACK
        // ===================================================================


        ShadowStrike,

  
        GeneratorHumDefense,

        // ===================================================================
        // ENVIRONMENTAL ACOUSTIC BACKGROUNDS & ZONE STATE TRANSITIONS
        // ===================================================================


        Ambience,

        SanityLowDrone,

        BlackoutImpactGlobal
    }
}