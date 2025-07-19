namespace SCP_575.Shared
{
    using System;
    using UnityEngine;

    public static class Scp575Helpers
    {
        public enum ProjectileImpactType
        {
            Helpful,
            Dangerous,
            Neutral,
            Unknown
        }

        public static ProjectileImpactType ClassifyProjectileImpact(LabApi.Features.Wrappers.TimedGrenadeProjectile projectile)
        {
            if (projectile == null) return ProjectileImpactType.Unknown;

            Library_ExiledAPI.LogDebug("Scp575ImpactType", $"ItemType is {projectile.Type}");
            return projectile.Type switch
            {
                // ToDo: Blocked for now. Its workaround from this known issue (https://github.com/northwood-studios/LabAPI/issues/219#issuecomment-3091106438)
                ItemType.SCP2176 => ProjectileImpactType.Helpful,
                ItemType.GrenadeFlash => ProjectileImpactType.Dangerous,
                //ItemType.GrenadeHE => ProjectileImpactType.Dangerous,
                //ItemType.SCP018 => ProjectileImpactType.Dangerous,
                //ItemType.ParticleDisruptor => ProjectileImpactType.Dangerous,
                _ => ProjectileImpactType.Neutral
            };
        }

        public static ProjectileImpactType ClassifyExplosionImpact(ExplosionType type)
        {
            if (type == null) return ProjectileImpactType.Unknown;

            Library_ExiledAPI.LogDebug("Scp575ImpactType", $"ExplosionType is {type}");
            return type switch
            {
                ExplosionType.Grenade => ProjectileImpactType.Dangerous,
                ExplosionType.Disruptor => ProjectileImpactType.Dangerous,
                ExplosionType.SCP018 => ProjectileImpactType.Dangerous,
                _ => ProjectileImpactType.Neutral
            };
        }



        /// <summary>
        /// Calculates Euclidean distance between two Vector3 points.
        /// </summary>
        public static float Distance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dy = a.y - b.y;
            float dz = a.z - b.z;
            return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

    }
}