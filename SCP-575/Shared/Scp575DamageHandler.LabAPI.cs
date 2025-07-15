namespace SCP_575.Shared
{
    using CustomPlayerEffects;
    using LabApi.Features.Wrappers;
    using SCP_575.ConfigObjects;
    using System;
    using System.Collections.Generic;


    public static class Scp575DamageHandler_LabAPI
    {

        public static Plugin Plugin => Plugin.Singleton;
        public static NpcConfig NpcConfig => Plugin.Config.NpcConfig;
        public static Config Config => Plugin.Config;

        public static void ApplyDamageEffects(Player player)
        {
            // Play horror sound effect
            if (NpcConfig.EnableScreamSound)
            {
                AudioManager.PlayScreamAutoManaged(player, customLifespan: 15f);
            }

            var effectActions = new List<(bool Enabled, Action Apply)>
            {
                (NpcConfig.EnableEffectEnsnared,      () => player.EnableEffect<Ensnared>(duration: 0.35f)),
                (NpcConfig.EnableEffectFlashed,       () => player.EnableEffect<Flashed>(duration: 0.075f)),
                (NpcConfig.EnableEffectBlurred,       () => player.EnableEffect<Blurred>(duration: 0.25f)),
                (NpcConfig.EnableEffectDeafened,      () => player.EnableEffect<Deafened>(duration: 3.75f)),
                (NpcConfig.EnableEffectAmnesiaVision, () => player.EnableEffect<AmnesiaVision>(duration: 3.65f)),
                (NpcConfig.EnableEffectSinkhole,      () => player.EnableEffect<Sinkhole>(duration: 3.25f)),
                (NpcConfig.EnableEffectConcussed,     () => player.EnableEffect<Concussed>(duration: 3.15f)),
                (NpcConfig.EnableEffectBlindness,     () => player.EnableEffect<Blindness>(duration: 2.65f)),
                (NpcConfig.EnableEffectBurned,        () => player.EnableEffect<Burned>(duration: 2.5f, intensity: 3)),
                (NpcConfig.EnableEffectAmnesiaItems,  () => player.EnableEffect<AmnesiaItems>(duration: 1.65f)),
                (NpcConfig.EnableEffectStained,       () => player.EnableEffect<Stained>(duration: 0.75f)),
                (NpcConfig.EnableEffectAsphyxiated,   () => player.EnableEffect<Asphyxiated>(duration: 1.25f, intensity: 3)),
                (NpcConfig.EnableEffectBleeding,      () => player.EnableEffect<Bleeding>(duration: 3.65f, intensity: 3)),
                (NpcConfig.EnableEffectDisabled,      () => player.EnableEffect<Disabled>(duration: 4.75f, intensity: 1)),
                (NpcConfig.EnableEffectExhausted,     () => player.EnableEffect<Exhausted>(duration: 6.75f)),
                (NpcConfig.EnableEffectTraumatized,   () => player.EnableEffect<Traumatized>(duration: 9.5f)),
            };

            foreach (var (enabled, apply) in effectActions)
            {
                if (enabled)
                    apply();
            }
        }
    }
}