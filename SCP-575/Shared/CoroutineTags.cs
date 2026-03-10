namespace SCP575.Shared
{
    /// <summary>
    /// Centralized class for defining coroutine tags used throughout the SCP-575 plugin. Using consistent tags allows for easy management and cleanup of coroutines, especially during round transitions or when the event is deactivated.
    /// </summary>
    public static class CoroutineTags
    {
        // Main Logic Loops
        public const string BlackoutLoop = "SCP575-BlackoutLoop";
        public const string ActionLoop = "SCP575-ActionLoop";

        // Systems and Players handlers
        public const string SanityHandler = "SCP575-SanityHandler";
        public const string CassieCooldown = "SCP575-CassieCd";
        public const string ItemPhysics = "SCP575-ItemPhysics";
        public const string RagdollPhysics = "SCP575-RagdollPhys";

        // Environmental Effects
        public const string ElevatorLocks = "SCP575-ElevatorLocks";
        public const string BlackoutStacks = "SCP575-BlackoutStacks";
        public const string LightCleanup = "SCP575-LightCleanup";

        // Audio
        public const string AudioCoroutines = "SCP575-AudioCoroutines";
        public const string GeneratorAudio = "SCP575-GeneratorAudio";

        /// <summary>
        /// All other temporary or one-off coroutines that don't fit into the above categories can use this general tag for easy cleanup
        /// </summary>
        public const string Temp = "SCP575-Temp";

        // Dynamic tags
        
        /// <summary>
        /// This prefix can be used for dynamically generated coroutine tags related to flickering effects, allowing for multiple independent flicker coroutines to be tracked and cleaned up as needed. For example, you could create tags like "SCP575-Flicker-Player1" or "SCP575-Flicker-RoomA" to manage specific flicker instances.
        /// </summary>
        public const string FlickerPrefix = "SCP575-Flicker-";

        /// <summary>
        /// This prefix can be used for dynamically generated coroutine tags related to item changes, such as when items are dropped or pushed due to SCP-575's effects. For example, you could create tags like "SCP575-ItemChange-Player1" or "SCP575-ItemChange-RoomA" to manage specific item-related coroutines.
        /// </summary>
        public const string ItemChangePrefix = "SCP575-ItemChange-";

        /// <summary>
        /// Returns an array of all static coroutine tags for bulk cleanup operations. This can be used in lifecycle handlers to ensure all relevant coroutines are stopped when the round ends or resets.
        /// </summary>
        public static readonly string[] AllStaticTags =
        {
            BlackoutLoop, ActionLoop, SanityHandler, CassieCooldown,
            ItemPhysics, RagdollPhysics, ElevatorLocks, BlackoutStacks,
            LightCleanup, AudioCoroutines, GeneratorAudio, Temp
        };
    }
}