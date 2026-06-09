namespace SCP_575.Shared
{
    /// <summary>
    /// Centralized class for defining coroutine tags used throughout the SCP-575 plugin. 
    /// Using consistent tags allows for easy management and cleanup of coroutines, especially during round transitions or when the event is deactivated.
    /// </summary>
    public static class CoroutineTags
    {
        // ===================================================================
        // MAIN LOGIC LOOPS
        // ===================================================================
        public const string BlackoutLoop = "SCP575-BlackoutLoop";
        public const string ActionLoop = "SCP575-ActionLoop";

        // ===================================================================
        // SYSTEMS AND PLAYERS HANDLERS
        // ===================================================================
        public const string SanityHandler = "SCP575-SanityHandler";
        public const string CassieCooldown = "SCP575-CassieCd";
        public const string ItemPhysics = "SCP575-ItemPhysics";
        public const string RagdollPhysics = "SCP575-RagdollPhys";

        // ===================================================================
        // ENVIRONMENTAL EFFECTS
        // ===================================================================
        public const string ElevatorLocks = "SCP575-ElevatorLocks";
        public const string BlackoutStacks = "SCP575-BlackoutStacks";
        public const string LightCleanup = "SCP575-LightCleanup";
        public const string MapCoroutines = "SCP575-MapCoroutines";

        // ===================================================================
        // AUDIO AND SOUNDSCAPES
        // ===================================================================
        public const string AmbienceTracking = "SCP575-AmbienceTracking";
        public const string AudioCoroutines = "SCP575-AudioCoroutines";
        public const string GeneratorAudio = "SCP575-GeneratorAudio";

        /// <summary>
        /// General catch-all tag allocated for temporary, short-lived or single-frame operational coroutines to guarantee zero memory leaks.
        /// </summary>
        public const string Temp = "SCP575-Temp";

        // ===================================================================
        // DYNAMIC PREFIXES (Requires tracking structures for manual cleanup)
        // ===================================================================

        /// <summary>
        /// Prefix for dynamically generated per-player or per-room light flickering execution routines.
        /// </summary>
        public const string FlickerPrefix = "SCP575-Flicker-";

        /// <summary>
        /// Prefix for dynamically generated per-player physical inventory propulsion routines.
        /// </summary>
        public const string ItemChangePrefix = "SCP575-ItemChange-";

        /// <summary>
        /// Returns an array of all static coroutine tags for bulk cleanup operations. 
        /// This can be used in lifecycle handlers to ensure all relevant coroutines are stopped when the round ends or resets.
        /// </summary>
        public static readonly string[] AllStaticTags =
        {
            BlackoutLoop, ActionLoop, SanityHandler, CassieCooldown,
            ItemPhysics, RagdollPhysics, ElevatorLocks, BlackoutStacks,
            LightCleanup, AudioCoroutines, GeneratorAudio, Temp
        };
    }
}