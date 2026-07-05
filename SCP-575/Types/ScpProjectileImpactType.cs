namespace SCP_575.Types
{
    using LabApi.Features.Wrappers;
    using System;
    using Logger = LabApi.Extensions.Misc.iLogger;

    /// <summary>
    /// Classifies environmental projectile and explosion impacts on SCP-575.
    /// </summary>
    public static class ScpProjectileImpactType
    {
        /// <summary>
        /// Specifies the impact category of a weapon or anomaly effect on SCP-575.
        /// </summary>
        public enum ProjectileImpactType
        {
            /// <summary> Triggers or extends a blackout event layer. </summary>
            Helpful,

            /// <summary> Inflicts psychological trauma or structural retreat on the entity. </summary>
            Dangerous,

            /// <summary> Evaluates with zero mechanical or visual footprint on the entity. </summary>
            Neutral,

            /// <summary> Unresolved or unvalidated impact context baseline. </summary>
            Unknown,

            /// <summary> Explicitly suppressed to isolate logic and avoid duplicated processing. </summary>
            Disabled,
        }

        /// <summary>
        /// Classifies an ExplosionType based on its threat profile against SCP-575.
        /// </summary>
        /// <returns>The resolved threat impact classification tier.</returns>
        public static ProjectileImpactType ClassifyExplosionImpact(ExplosionType? type, bool debug = false)
        {
            try
            {
                if (type is null)
                {
                    Logger.Debug(nameof(ScpProjectileImpactType), "Explosion type is null. Returning Unknown.", debug);
                    return ProjectileImpactType.Unknown;
                }

                Logger.Debug(nameof(ScpProjectileImpactType), $"Classifying explosion type: {type}", debug);

                return type switch
                {
                    ExplosionType.Grenade or ExplosionType.SCP018 or ExplosionType.Jailbird or ExplosionType.Disruptor => ProjectileImpactType.Dangerous,
                    _ => ProjectileImpactType.Neutral
                };
            }
            catch (Exception ex)
            {
                Logger.Error(nameof(ScpProjectileImpactType), $"Explosion classification processing failure for type {type}: {ex.Message}");
                return ProjectileImpactType.Unknown;
            }
        }

        /// <summary>
        /// Classifies a TimedGrenadeProjectile based on its underlying ItemType wrapper property.
        /// </summary>
        /// <returns>The resolved threat impact classification tier.</returns>
        public static ProjectileImpactType ClassifyProjectileImpact(TimedGrenadeProjectile projectile, bool debug = false)
        {
            try
            {
                if (projectile is null)
                {
                    Logger.Debug(nameof(ScpProjectileImpactType), "Projectile is null. Returning Unknown.", debug);
                    return ProjectileImpactType.Unknown;
                }

                ItemType type = projectile.Type;
                Logger.Debug(nameof(ScpProjectileImpactType), $"Classifying projectile with ItemType: {type}", debug);

                return type switch
                {
                    ItemType.SCP2176 => ProjectileImpactType.Helpful,
                    ItemType.GrenadeFlash => ProjectileImpactType.Dangerous,
                    ItemType.GrenadeHE or ItemType.ParticleDisruptor or ItemType.Jailbird or ItemType.SCP018 => ProjectileImpactType.Disabled,
                    _ => ProjectileImpactType.Neutral
                };
            }
            catch (Exception ex)
            {
                Logger.Error(nameof(ScpProjectileImpactType), $"Projectile classification processing failure: {ex.Message}");
                return ProjectileImpactType.Unknown;
            }
        }
    }
}