namespace SCP_575.Shared
{
    using System;
    using UnityEngine;

    public static class Helpers
    {
        /// <summary>
        /// Checks if a player is human and not holding an active light source.
        /// </summary>
        /// <param name="player">The player to check.</param>
        /// <returns>True if the player is human without an active light source, false otherwise.</returns>
        public static bool IsHumanWithoutLight(LabApi.Features.Wrappers.Player player)
        {
            try
            {
                var exiledPlayer = Library_ExiledAPI.ToExiledPlayer(player);
                if (!player.IsHuman || exiledPlayer.HasFlashlightModuleEnabled) return false;

                if (player.CurrentItem?.Base is InventorySystem.Items.ToggleableLights.ToggleableLightItemBase lightItem)
                    return !lightItem.IsEmittingLight;

                return true;
            }
            catch (Exception ex)
            {
                Library_ExiledAPI.LogError("Helpers.IsHumanWithoutLight", $"Failed to check player {player?.UserId ?? "null"} ({player?.Nickname ?? "unknown"}): {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Checks if a player is in a dark room (lights disabled).
        /// </summary>
        /// <param name="player">The player to check.</param>
        /// <returns>True if the player is in a dark room, false otherwise.</returns>
        public static bool IsInDarkRoom(LabApi.Features.Wrappers.Player player)
        {
            try
            {
                if (player == null)
                {
                    Library_ExiledAPI.LogDebug("Helpers.IsInDarkRoom", "Player is null.");
                    return false;
                }
                var room = player.Room;
                Library_ExiledAPI.LogDebug("Helpers.IsInDarkRoom", $"Player {player.UserId} ({player.Nickname ?? "unknown"}): Room is {(room != null ? "non-null" : "null")}");
                if (room == null || room.LightController == null)
                {
                    Library_ExiledAPI.LogDebug("Helpers.IsInDarkRoom", $"Player {player.UserId} ({player.Nickname ?? "unknown"}): Room or LightController is null, returning false.");
                    return false;
                }
                bool isDark = !room.LightController.LightsEnabled;
                Library_ExiledAPI.LogDebug("Helpers.IsInDarkRoom", $"Player {player.UserId} ({player.Nickname ?? "unknown"}): LightsEnabled={room.LightController.LightsEnabled}, IsDark={isDark}");
                return isDark;
            }
            catch (Exception ex)
            {
                Library_ExiledAPI.LogWarn("Helpers.IsInDarkRoom", $"Failed to check dark room for {player?.UserId ?? "null"} ({player?.Nickname ?? "unknown"}): {ex.Message}");
                return false;
            }
        }

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