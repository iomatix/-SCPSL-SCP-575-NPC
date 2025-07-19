namespace SCP_575.Shared
{
    using System;
    using UnityEngine;

    public static class Helpers
    {

        /// <summary>
        /// Calculates the Euclidean distance between two 3D points.
        /// </summary>
        /// <param name="a">The first point.</param>
        /// <param name="b">The second point.</param>
        /// <returns>The distance between the two points.</returns>
        /// <exception cref="ArgumentNullException">Thrown if either vector is null.</exception>
        public static float Distance(Vector3 a, Vector3 b)
        {
            try
            {
                if (a == null || b == null)
                    throw new ArgumentNullException(a == null ? nameof(a) : nameof(b), "Vector cannot be null.");

                float distance = Vector3.Distance(a, b);
                Library_ExiledAPI.LogDebug("Helpers.Distance", $"Calculated distance between {a} and {b}: {distance}");
                return distance;
            }
            catch (Exception ex)
            {
                Library_ExiledAPI.LogError("Helpers.Distance", $"Failed to calculate distance between {a} and {b}: {ex.Message}\nStackTrace: {ex.StackTrace}");
                return 0f;
            }
        }

    }
}