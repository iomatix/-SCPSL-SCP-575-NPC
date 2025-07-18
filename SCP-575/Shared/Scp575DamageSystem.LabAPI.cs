namespace SCP_575.Shared
{
    using CustomPlayerEffects;
    using LabApi.Features.Wrappers;
    using SCP_575.ConfigObjects;
    using System;
    using System.Collections.Generic;


    /// <summary>
    /// Provides Lab API–based utility methods for handling visual and debug feedback 
    /// during SCP-575 damage processing.
    /// </summary>
    public static class Scp575DamageSystem_LabAPI
    {

        public static void ApplyDamageEffects(Player player)
        {

            var effectActions = new List<(bool Enabled, Action Apply)>
            {

                (Library_LabAPI.NpcConfig.EnableEffectEnsnared,      () => player.EnableEffect<Ensnared>(duration: 0.35f)),
                (Library_LabAPI.NpcConfig.EnableEffectFlashed,       () => player.EnableEffect<Flashed>(duration: 0.075f)),
                (Library_LabAPI.NpcConfig.EnableEffectBlurred,       () => player.EnableEffect<Blurred>(duration: 0.25f)),
                (Library_LabAPI.NpcConfig.EnableEffectDeafened,      () => player.EnableEffect<Deafened>(duration: 3.75f)),
                (Library_LabAPI.NpcConfig.EnableEffectAmnesiaVision, () => player.EnableEffect<AmnesiaVision>(duration: 3.65f)),
                (Library_LabAPI.NpcConfig.EnableEffectSinkhole,      () => player.EnableEffect<Sinkhole>(duration: 3.25f)),
                (Library_LabAPI.NpcConfig.EnableEffectConcussed,     () => player.EnableEffect<Concussed>(duration: 3.15f)),
                (Library_LabAPI.NpcConfig.EnableEffectBlindness,     () => player.EnableEffect<Blindness>(duration: 2.65f)),
                (Library_LabAPI.NpcConfig.EnableEffectBurned,        () => player.EnableEffect<Burned>(duration: 2.5f, intensity: 3)),
                (Library_LabAPI.NpcConfig.EnableEffectAmnesiaItems,  () => player.EnableEffect<AmnesiaItems>(duration: 1.65f)),
                (Library_LabAPI.NpcConfig.EnableEffectStained,       () => player.EnableEffect<Stained>(duration: 0.75f)),
                (Library_LabAPI.NpcConfig.EnableEffectAsphyxiated,   () => player.EnableEffect<Asphyxiated>(duration: 1.25f, intensity: 3)),
                (Library_LabAPI.NpcConfig.EnableEffectBleeding,      () => player.EnableEffect<Bleeding>(duration: 3.65f, intensity: 3)),
                (Library_LabAPI.NpcConfig.EnableEffectDisabled,      () => player.EnableEffect<Disabled>(duration: 4.75f, intensity: 1)),
                (Library_LabAPI.NpcConfig.EnableEffectExhausted,     () => player.EnableEffect<Exhausted>(duration: 6.75f)),
                (Library_LabAPI.NpcConfig.EnableEffectTraumatized,   () => player.EnableEffect<Traumatized>(duration: 9.5f)),
            };

            foreach (var (enabled, apply) in effectActions)
            {
                if (enabled)
                    apply();
            }
        }
    }
}