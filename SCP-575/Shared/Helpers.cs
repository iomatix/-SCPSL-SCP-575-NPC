namespace SCP_575.Shared
{
    using System;
    using System.Linq;
    using InventorySystem.Items.Firearms.Attachments;
    using InventorySystem.Items.ToggleableLights;
    using LabApi.Features.Wrappers;
    using UnityEngine;

    public static class Helpers
    {
        /// <summary>
        /// Checks if a player is human and completely vulnerable to darkness (not holding any active mobile photon emitters).
        /// </summary>
        /// <param name="player">The player loop target to evaluate.</param>
        /// <returns>True if the player is a vulnerable human caught in absolute mroku, false if protected by emission fields.</returns>
        public static bool IsHumanWithoutLight(Player player)
        {
            if (player == null || !player.IsHuman) return false;

            try
            {

                var currentItem = player.CurrentItem;
                if (currentItem == null) return true;

                // 2. Checking dedicated mobile light items (Flashlights, lanterns etc.)
                if (currentItem.Base is ToggleableLightItemBase lightItem)
                {
                    return !lightItem.IsEmittingLight;
                }

                // FIXED: Embedded critical validation for active tactical weapon-mounted flashlights
                // to prevent unfair trauma distribution while human forces have weapons drawn.
                if (currentItem is FirearmItem firearm)
                {
                    if (firearm.FlashlightEnabled && firearm.Attachments != null &&
                        firearm.Attachments.Any(a => a.Name == AttachmentName.Flashlight))
                    {
                        return false; // Player is protected by their weapon attachment flashlight
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                LibraryLabAPI.LogError("Helpers.IsHumanWithoutLight", $"Critical state validation failure for {player.Nickname}: {ex.Message}");
                return true; // FIXED: Defaulting to true guarantees threat engagement instead of granting unintended invulnerability.
            }
        }

        /// <summary>
        /// Calculates the exact Euclidean distance between two spatial points.
        /// </summary>
        public static float Distance(Vector3 a, Vector3 b)
        {
            // FIXED: Removed structural null-checks since Vector3 is a value type struct and can never be null.
            return Vector3.Distance(a, b);
        }

        /// <summary>
        /// Calculates the fast squared distance magnitude between two spatial points.
        /// Use this for performance-heavy distance throttling to entirely avoid costly square-root calculations.
        /// </summary>
        public static float DistanceSquared(Vector3 a, Vector3 b)
        {
            return Vector3.SqrMagnitude(a - b);
        }
    }
}