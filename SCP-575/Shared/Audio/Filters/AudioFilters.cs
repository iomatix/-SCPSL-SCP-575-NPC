namespace SCP_575.Shared.Audio.Filters
{
    using System;
    using LabApi.Features.Wrappers;
    using PlayerRoles;
    using UnityEngine;

    /// <summary>
    /// Provides common SCP:SL-specific audio filters for use with <see cref="SetValidPlayers(IEnumerable{Func{Player, bool}})"/>.
    /// </summary>
    public static class AudioFilters
    {
        /// <summary>
        /// Filters players by their role type.
        /// </summary>
        /// <param name="roleType">The role type to allow.</param>
        /// <returns>A filter function for the specified role.</returns>
        public static Func<Player, bool> ByRole(RoleTypeId roleType)
        {
            return player => player.Role == roleType;
        }

        /// <summary>
        /// Filters players by their team.
        /// </summary>
        /// <param name="team">The team to allow.</param>
        /// <returns>A filter function for the specified team.</returns>
        public static Func<Player, bool> ByTeam(Team team)
        {
            return player => player.Team == team;
        }

        /// <summary>
        /// Filters players within a specified distance from the speaker.
        /// </summary>
        /// <param name="speakerPosition">The position of the speaker.</param>
        /// <param name="maxDistance">The maximum distance in Unity units.</param>
        /// <returns>A filter function for players within the specified distance.</returns>
        public static Func<Player, bool> ByDistance(Vector3 speakerPosition, float maxDistance)
        {
            return player =>
            {
                if (player.Position == null) return false;
                float distance = Vector3.Distance(speakerPosition, player.Position);
                return distance <= maxDistance;
            };
        }

        /// <summary>
        /// Filters players who are alive.
        /// </summary>
        /// <returns>A filter function for living players.</returns>
        public static Func<Player, bool> IsAlive()
        {
            return player => player.IsAlive;
        }

        /// <summary>
        /// Filters players who are alive, in a dark room (lights disabled), and a condition is true.
        /// </summary>
        /// <param name="isConditionTrue">The boolean condition to check (e.g., blackout event active).</param>
        /// <returns>A filter function for players meeting all criteria.</returns>
        public static Func<Player, bool> InDarkRoomAliveAndCondition(bool isConditionTrue)
        {
            return player =>
            {
                if (!isConditionTrue) return false; // Early exit if condition is false
                if (!player.IsAlive) return false; // Check if player is alive
                if (player.Room == null || player.Room.LightController == null) return false; // Ensure player is in a valid room with a LightController
                return !player.Room.LightController.LightsEnabled; // Check if room lights are disabled
            };
        }
    }
}
