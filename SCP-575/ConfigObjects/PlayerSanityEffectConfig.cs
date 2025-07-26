namespace SCP_575.ConfigObjects
{
    using System;
    using System.ComponentModel;


    public enum SanityEffectType
    {
        // Visual Effects  
        Blurred,
        Blindness,
        Flashed,

        // Audio Effects  
        Deafened,

        // Movement Effects  
        Slowness,
        SilentWalk,
        Exhausted,
        Disabled,

        // Health Effects  
        Bleeding,
        Poisoned,
        Burned,
        Corroding,

        // Mental Effects  
        Concussed,
        Traumatized,

        // Special Effects  
        Invisible,
        Scp207,
        AntiScp207,
        MovementBoost,
        DamageReduction,
        RainbowTaste,
        BodyshotReduction,
        Scp1853,
        CardiacArrest,
        InsufficientLighting,
        SoundtrackMute,
        SpawnProtected,
        Ensnared,
        Ghostly,
        SeveredHands,
        Stained,
        Vitality,
        Asphyxiated,
        Decontaminating,
        PocketCorroding
    }

    /// <summary>
    /// Configuration for a sanity status effect, defining its type, duration, and intensity.
    /// </summary>
    [Description("Configuration for a player sanity effect.")]
    public sealed class PlayerSanityEffectConfig
    {
        [Description("Specifies the status effect type to apply.")]
        public SanityEffectType EffectType { get; set; }

        [Description("Duration of the effect in seconds.")]
        public float Duration { get; set; } = 3f;

        [Description("Intensity level of the status effect.")]
        public byte Intensity { get; set; } = 1;

        public static Type GetEffectType(SanityEffectType effectType)
        {
            return effectType switch
            {
                // Visual Effects  
                SanityEffectType.Blurred => typeof(CustomPlayerEffects.Blurred),
                SanityEffectType.Blindness => typeof(CustomPlayerEffects.Blindness),
                SanityEffectType.Flashed => typeof(CustomPlayerEffects.Flashed),

                // Audio Effects  
                SanityEffectType.Deafened => typeof(CustomPlayerEffects.Deafened),

                // Movement Effects  
                SanityEffectType.Slowness => typeof(CustomPlayerEffects.Slowness),
                SanityEffectType.SilentWalk => typeof(CustomPlayerEffects.SilentWalk),
                SanityEffectType.Exhausted => typeof(CustomPlayerEffects.Exhausted),
                SanityEffectType.Disabled => typeof(CustomPlayerEffects.Disabled),

                // Health Effects  
                SanityEffectType.Bleeding => typeof(CustomPlayerEffects.Bleeding),
                SanityEffectType.Poisoned => typeof(CustomPlayerEffects.Poisoned),
                SanityEffectType.Burned => typeof(CustomPlayerEffects.Burned),
                SanityEffectType.Corroding => typeof(CustomPlayerEffects.Corroding),

                // Mental Effects  
                SanityEffectType.Concussed => typeof(CustomPlayerEffects.Concussed),
                SanityEffectType.Traumatized => typeof(CustomPlayerEffects.Traumatized),

                // Special Effects  
                SanityEffectType.Invisible => typeof(CustomPlayerEffects.Invisible),
                SanityEffectType.Scp207 => typeof(CustomPlayerEffects.Scp207),
                SanityEffectType.AntiScp207 => typeof(CustomPlayerEffects.AntiScp207),
                SanityEffectType.MovementBoost => typeof(CustomPlayerEffects.MovementBoost),
                SanityEffectType.DamageReduction => typeof(CustomPlayerEffects.DamageReduction),
                SanityEffectType.RainbowTaste => typeof(CustomPlayerEffects.RainbowTaste),
                SanityEffectType.BodyshotReduction => typeof(CustomPlayerEffects.BodyshotReduction),
                SanityEffectType.Scp1853 => typeof(CustomPlayerEffects.Scp1853),
                SanityEffectType.CardiacArrest => typeof(CustomPlayerEffects.CardiacArrest),
                SanityEffectType.InsufficientLighting => typeof(CustomPlayerEffects.InsufficientLighting),
                SanityEffectType.SoundtrackMute => typeof(CustomPlayerEffects.SoundtrackMute),
                SanityEffectType.SpawnProtected => typeof(CustomPlayerEffects.SpawnProtected),
                SanityEffectType.Ensnared => typeof(CustomPlayerEffects.Ensnared),
                SanityEffectType.Ghostly => typeof(CustomPlayerEffects.Ghostly),
                SanityEffectType.SeveredHands => typeof(CustomPlayerEffects.SeveredHands),
                SanityEffectType.Stained => typeof(CustomPlayerEffects.Stained),
                SanityEffectType.Vitality => typeof(CustomPlayerEffects.Vitality),
                SanityEffectType.Asphyxiated => typeof(CustomPlayerEffects.Asphyxiated),
                SanityEffectType.Decontaminating => typeof(CustomPlayerEffects.Decontaminating),
                SanityEffectType.PocketCorroding => typeof(CustomPlayerEffects.PocketCorroding),

                _ => throw new ArgumentException($"Unknown effect type: {effectType}")
            };
        }

        // Add a convenience method to get the actual Type for this config  
        public Type GetActualEffectType() => GetEffectType(EffectType);
    }
}