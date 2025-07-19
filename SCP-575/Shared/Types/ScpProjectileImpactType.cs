namespace SCP_575.Shared.Types
{
    using System;

    /// <summary>
    /// Provides helper methods for classifying projectile and explosion impacts on SCP-575.
    /// </summary>
    public static class ScpProjectileImpactType
    {
        /// <summary>
        /// Defines the impact type of a projectile or explosion on SCP-575.
        /// </summary>
        public enum ProjectileImpactType
        {
            /// <summary>
            /// The projectile or explosion is beneficial to SCP-575, calling blackout event (e.g., SCP-2176).
            /// </summary>
            Helpful,

            /// <summary>
            /// The projectile or explosion is harmful to SCP-575 (e.g., grenades, flashbangs).
            /// </summary>
            Dangerous,

            /// <summary>
            /// The projectile or explosion has no significant effect on SCP-575.
            /// </summary>
            Neutral,

            /// <summary>
            /// The projectile or explosion type is unknown or invalid.
            /// </summary>
            Unknown
        }

        /// <summary>
        /// Classifies the impact of an explosion on SCP-575 based on its type.
        /// </summary>
        /// <param name="type">The type of explosion to classify.</param>
        /// <returns>The <see cref="ProjectileImpactType"/> indicating the explosion's effect on SCP-575.</returns>
        public static ProjectileImpactType ClassifyExplosionImpact(ExplosionType type)
        {
            try
            {
                if (type == null)
                {
                    Library_ExiledAPI.LogDebug("ScpProjectileImpactType.ClassifyExplosionImpact", "Explosion type is null. Returning Unknown.");
                    return ProjectileImpactType.Unknown;
                }

                Library_ExiledAPI.LogDebug("ScpProjectileImpactType.ClassifyExplosionImpact", $"Classifying explosion type: {type}");
                return type switch
                {
                    ExplosionType.Grenade => ProjectileImpactType.Dangerous,
                    ExplosionType.SCP018 => ProjectileImpactType.Dangerous,
                    ExplosionType.Jailbird => ProjectileImpactType.Dangerous,
                    ExplosionType.Disruptor => ProjectileImpactType.Dangerous,
                    _ => ProjectileImpactType.Neutral
                };
            }
            catch (Exception ex)
            {
                Library_ExiledAPI.LogError("ScpProjectileImpactType.ClassifyExplosionImpact", $"Failed to classify explosion impact for type {type}: {ex.Message}\nStackTrace: {ex.StackTrace}");
                return ProjectileImpactType.Unknown;
            }
        }

        /// <summary>
        /// Classifies the impact of a projectile on SCP-575 based on its type.
        /// </summary>
        /// <param name="projectile">The projectile to classify.</param>
        /// <returns>The <see cref="ProjectileImpactType"/> indicating the projectile's effect on SCP-575.</returns>
        public static ProjectileImpactType ClassifyProjectileImpact(LabApi.Features.Wrappers.TimedGrenadeProjectile projectile)
        {
            try
            {
                if (projectile == null)
                {
                    Library_ExiledAPI.LogDebug("ScpProjectileImpactType.ClassifyProjectileImpact", "Projectile is null. Returning Unknown.");
                    return ProjectileImpactType.Unknown;
                }

                ItemType type = projectile.Type;
                Library_ExiledAPI.LogDebug("ScpProjectileImpactType.ClassifyProjectileImpact", $"Classifying projectile with ItemType: {type}");

                return type switch
                {
                    ItemType.SCP2176 => ProjectileImpactType.Helpful,
                    ItemType.GrenadeFlash => ProjectileImpactType.Dangerous,
                    _ => ProjectileImpactType.Neutral
                };
            }
            catch (Exception ex)
            {
                Library_ExiledAPI.LogError("ScpProjectileImpactType.ClassifyProjectileImpact", $"Failed to classify projectile impact: {ex.Message}\nStackTrace: {ex.StackTrace}");
                return ProjectileImpactType.Unknown;
            }
        }
    }
}