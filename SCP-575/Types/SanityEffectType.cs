namespace SCP_575.Types
{
    /// <summary>
    /// Explicit tracking keys for status effect classifications mapping to game features.
    /// </summary>
    public enum SanityEffectType
    {
        // Visual Effects  
        Blurred, Blindness, Flashed,

        // Audio Effects  
        Deafened,

        // Movement Effects  
        Slowness, SilentWalk, Exhausted, Disabled,

        // Health Effects  
        Bleeding, Poisoned, Burned, Corroding,

        // Mental Effects  
        Concussed, Traumatized,

        // Special Effects  
        Invisible, Scp207, AntiScp207, MovementBoost, DamageReduction,
        RainbowTaste, BodyshotReduction, Scp1853, CardiacArrest,
        InsufficientLighting, SoundtrackMute, SpawnProtected, Ensnared,
        Ghostly, SeveredHands, Stained, Vitality, Asphyxiated,
        Decontaminating, PocketCorroding
    }
}