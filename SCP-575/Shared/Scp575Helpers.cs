namespace SCP_575.Shared
{
    using System;
    using UnityEngine;

    public static class Scp575Helpers
    {
        public enum Scp575ImpactType
        {
            Helpful,
            Dangerous,
            Neutral,
            Unknown
        }

        public static Scp575ImpactType ClassifyProjectileImpact(LabApi.Features.Wrappers.TimedGrenadeProjectile projectile)
        {
            if (projectile == null) return Scp575ImpactType.Unknown;

            Library_ExiledAPI.LogDebug("Scp575ImpactType", $"ItemType is {projectile.Type}");
            return projectile.Type switch
            {
                // ToDo: Blocked for now. Its workaround from this known issue (https://github.com/northwood-studios/LabAPI/issues/219#issuecomment-3091106438)
                ItemType.SCP2176 => Scp575ImpactType.Helpful,
                ItemType.GrenadeFlash => Scp575ImpactType.Dangerous,
                //ItemType.GrenadeHE => Scp575ImpactType.Dangerous,
                //ItemType.SCP018 => Scp575ImpactType.Dangerous,
                //ItemType.ParticleDisruptor => Scp575ImpactType.Dangerous,
                _ => Scp575ImpactType.Neutral
            };
        }

        public static Scp575ImpactType ClassifyExplosionImpact(ExplosionType type)
        {
            if (type == null) return Scp575ImpactType.Unknown;

            Library_ExiledAPI.LogDebug("Scp575ImpactType", $"ExplosionType is {type}");
            return type switch
            {
                ExplosionType.Grenade => Scp575ImpactType.Dangerous,
                ExplosionType.Disruptor => Scp575ImpactType.Dangerous,
                ExplosionType.SCP018 => Scp575ImpactType.Dangerous,
                _ => Scp575ImpactType.Neutral
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