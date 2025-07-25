namespace SCP_575.ConfigObjects
{
    using System.Collections.Generic;
    using System.ComponentModel;
    public class SanityConfig
    {
        [Description("Enable sanity system.")]
        public bool IsEnabled { get; set; } = true;

        [Description("Initial sanity value (0–100) on spawn.")]
        public float InitialSanity { get; set; } = 100f;

        [Description("Base sanity decay rate per second.")]
        public float DecayRateBase { get; set; } = 0.2f;

        [Description("Decay multiplier when SCP-575 is active.")]
        public float DecayMultiplierBlackout { get; set; } = 1.5f;

        [Description("Decay multiplier when player has no light source.")]
        public float DecayMultiplierDarkness { get; set; } = 2.0f;

        [Description("Passive sanity regen rate per second.")]
        public float PassiveRegenRate { get; set; } = 0.05f;

        [Description("Cooldown after using sanity recovery item.")]
        public float RegenCooldown { get; set; } = 8f;

        [Description("Minimum sanity restore percent from medical pills.")]
        public float PillsRestoreMin { get; set; } = 2f;

        [Description("Maximum sanity restore percent from medical pills.")]
        public float PillsRestoreMax { get; set; } = 10f;

        [Description("Minimum sanity restore percent from SCP-500.")]
        public float SCP500RestoreMin { get; set; } = 80f;

        [Description("Maximum sanity restore percent from SCP-500.")]
        public float SCP500RestoreMax { get; set; } = 100f;

        [Description("Stages of sanity and their associated effects.")]
        public List<SanityStage> SanityStages { get; set; } = new()
    {
        new SanityStage
        {
            MinThreshold = 75f,
            MaxThreshold = 100f,
            EnableWhispers = true,
            EnableScreenShake = false,
            EnableAudioDistortion = false,
            EnableHallucinations = false,
            EnableCameraDistortion = false,
            EnableMovementLag = false,
            EnablePanicFlash = false,
            DamageOnStrike = 0f,
        },
        new SanityStage
        {
            MinThreshold = 50f,
            MaxThreshold = 75f,
            EnableWhispers = true,
            EnableScreenShake = true,
            EnableAudioDistortion = true,
            EnableHallucinations = false,
            EnableCameraDistortion = false,
            EnableMovementLag = false,
            EnablePanicFlash = false,
            DamageOnStrike = 5f,
        },
        new SanityStage
        {
            MinThreshold = 25f,
            MaxThreshold = 50f,
            EnableWhispers = true,
            EnableScreenShake = true,
            EnableAudioDistortion = true,
            EnableHallucinations = true,
            EnableCameraDistortion = true,
            EnableMovementLag = true,
            EnablePanicFlash = false,
            DamageOnStrike = 12f,
        },
        new SanityStage
        {
            MinThreshold = 0f,
            MaxThreshold = 25f,
            EnableWhispers = true,
            EnableScreenShake = true,
            EnableAudioDistortion = true,
            EnableHallucinations = true,
            EnableCameraDistortion = true,
            EnableMovementLag = true,
            EnablePanicFlash = true,
            DamageOnStrike = 25f,
        },
    };
    }
}