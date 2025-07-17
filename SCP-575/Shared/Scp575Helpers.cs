namespace SCP_575.Shared
{
    using MEC;
    using Mirror;
    using PlayerRoles.PlayableScps.Scp3114;
    using PlayerRoles.Ragdolls;
    using ProgressiveCulling;
    using System;
    using System.Linq;
    using UnityEngine;
    using LabApi.Features.Wrappers;
    using LabApi.Events.Handlers;
    using LabApi.Events.Arguments.PlayerEvents;

    public static class Scp575Helpers
    {

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