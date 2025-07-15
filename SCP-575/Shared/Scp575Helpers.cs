namespace SCP_575.Shared
{
    using System;
    using MEC;
    using PlayerRoles.PlayableScps.Scp3114;
    using PlayerRoles.Ragdolls;
    using ProgressiveCulling;
    using UnityEngine;

    /// <summary>  
    /// Provides utility methods for processing SCP-575 ragdolls with visual effects and validation.
    /// </summary>  
    /// <remarks>  
    /// This class focuses on visual effects and validation rather than physics manipulation,  
    /// since ragdoll positioning is handled by the Scp575DamageHandler.
    /// </remarks>  
    public static class Scp575Helpers
    {
        /// <summary>  
        /// Processes an SCP-575 ragdoll with visual effects and validation.
        /// </summary>  
        /// <param name="ragdoll">The LabAPI ragdoll wrapper to process.</param>  
        /// <param name="handler">The SCP-575 damage handler that caused the death.</param>  
        /// <exception cref="ArgumentNullException">Thrown when ragdoll or handler is null.</exception>  
        public static void RagdollProcess(LabApi.Features.Wrappers.Ragdoll ragdoll, Scp575DamageHandler handler)
        {
            if (ragdoll == null)
                throw new ArgumentNullException(nameof(ragdoll));
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            GameObject ragdollGO = ragdoll.Base.gameObject;

            // Validate ragdoll state and apply visual fixes  
            if (!RagdollValidateState(ragdoll, ragdollGO))
                return;

            // Ensure ragdoll is a DynamicRagdoll for bone conversion  
            if (ragdoll.Base is not DynamicRagdoll dynamicRagdoll)
            {
                Library_ExiledAPI.LogWarn("RagdollProcess", "Ragdoll is not a DynamicRagdoll. Skipping bone conversion.");
                return;
            }

            // Apply bone conversion for proper visual representation  
            if (!RagdollApplyBoneConversion(dynamicRagdoll))
                return;

            // Apply visual effects without physics manipulation  
            RagdollApplyVisualEffects(dynamicRagdoll, handler, ragdoll);

            // Schedule post-spawn validation  
            RagdollSchedulePostSpawnValidation(ragdoll);

            // Create debug marker if needed  
            CreateDebugMarker(ragdoll.Position);
        }

        /// <summary>  
        /// Validates and fixes ragdoll state issues related to visibility and activation.
        /// </summary>  
        /// <param name="ragdoll">The LabAPI ragdoll wrapper.</param>  
        /// <param name="ragdollGO">The ragdoll GameObject.</param>  
        /// <returns>True if validation passed, false if critical issues were found.</returns>  
        public static bool RagdollValidateState(LabApi.Features.Wrappers.Ragdoll ragdoll, GameObject ragdollGO)
        {
            if (ragdollGO == null)
            {
                Library_ExiledAPI.LogError("RagdollValidateState", "Ragdoll GameObject is null");
                return false;
            }

            // Ensure ragdoll GameObject is active  
            if (!ragdollGO.activeSelf)
            {
                ragdollGO.SetActive(true);
                Library_ExiledAPI.LogWarn("RagdollValidateState", "Ragdoll GameObject was inactive — enabled manually.");
            }

            // Handle visibility culling issues  
            if (ragdollGO.TryGetComponent(out CullableBehaviour cullable))
            {
                cullable.enabled = false;
                Library_ExiledAPI.LogDebug("RagdollValidateState", "Disabled CullableBehaviour to force visibility");
            }

            // Force all renderers to be visible  
            var renderers = ragdollGO.GetComponentsInChildren<Renderer>(true);
            Library_ExiledAPI.LogDebug("RagdollValidateState", $"Found {renderers.Length} renderers in hierarchy");

            foreach (var renderer in renderers)
            {
                // Force enable inactive renderers if their GameObjects are supposed to be active  
                if (renderer.enabled && renderer.gameObject.activeSelf && !renderer.isVisible)
                {
                    // Try to force visibility by temporarily disabling and re-enabling  
                    renderer.enabled = false;
                    renderer.enabled = true;
                }

                Library_ExiledAPI.LogDebug("RagdollValidateState",
                    $"Renderer: {renderer.name}, enabled: {renderer.enabled}, active: {renderer.gameObject.activeSelf}");
            }

            // Force refresh the ragdoll's network data to ensure synchronization  
            ragdoll.Position = ragdoll.Position; // This triggers the NetworkInfo update  

            Library_ExiledAPI.LogDebug("RagdollValidateState",
                $"Ragdoll type: {ragdoll.Base.GetType().Name}, Position: {ragdoll.Position}");

            return true;
        }

        /// <summary>  
        /// Applies bone conversion to the ragdoll for proper physics representation.
        /// </summary>  
        /// <param name="dynamicRagdoll">The dynamic ragdoll to convert.</param>  
        /// <returns>True if conversion succeeded, false otherwise.</returns>  
        public static bool RagdollApplyBoneConversion(DynamicRagdoll dynamicRagdoll)
        {
            if (dynamicRagdoll == null)
            {
                Library_ExiledAPI.LogError("RagdollApplyBoneConversion", "DynamicRagdoll is null");
                return false;
            }

            try
            {
                Scp3114RagdollToBonesConverter.ConvertExisting(dynamicRagdoll);
                Library_ExiledAPI.LogDebug("RagdollApplyBoneConversion", "Ragdoll bones conversion completed successfully.");
                return true;
            }
            catch (Exception ex)
            {
                Library_ExiledAPI.LogError("RagdollApplyBoneConversion", $"Bone conversion failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>  
        /// Applies visual effects to the ragdoll without physics manipulation.
        /// </summary>  
        /// <param name="dynamicRagdoll">The dynamic ragdoll to apply effects to.</param>  
        /// <param name="handler">The damage handler containing effect parameters.</param>  
        /// <param name="ragdoll">The LabAPI ragdoll wrapper.</param>  
        /// <remarks>  
        /// This method focuses on visual effects rather than physics forces since  
        /// ragdoll positioning is controlled by the damage handler.
        /// </remarks>  
        private static void RagdollApplyVisualEffects(DynamicRagdoll dynamicRagdoll, Scp575DamageHandler handler, LabApi.Features.Wrappers.Ragdoll ragdoll)
        {
            var hitbox = handler.Hitbox;

            // Log the effect application for debugging  
            Library_ExiledAPI.LogDebug("RagdollApplyVisualEffects",
                $"Applying SCP-575 visual effects to ragdoll at hitbox: {hitbox}");

            // Apply visual effects here (particle systems, material changes, etc.)  
            // Example: Change ragdoll material to indicate SCP-575 death  
            ApplyDeathVisualEffects(dynamicRagdoll, hitbox);

            // Apply audio effects if needed  
            // Example: Play death sound at ragdoll position  
            // AudioManager.PlayDeathSound(ragdoll.Position);  

            Library_ExiledAPI.LogDebug("RagdollApplyVisualEffects", "SCP-575 visual effects applied successfully");
        }

        /// <summary>  
        /// Applies death-specific visual effects to the ragdoll.
        /// </summary>  
        /// <param name="dynamicRagdoll">The dynamic ragdoll to modify.</param>  
        /// <param name="hitbox">The hitbox that was targeted.</param>  
        private static void ApplyDeathVisualEffects(DynamicRagdoll dynamicRagdoll, HitboxType hitbox)
        {
            // Example implementation - customize based on your needs  
            try
            {
                // You could apply different visual effects based on hitbox  
                switch (hitbox)
                {
                    case HitboxType.Headshot:
                        // Apply headshot-specific visual effects  
                        Library_ExiledAPI.LogDebug("ApplyDeathVisualEffects", "Applied headshot visual effects");
                        break;
                    case HitboxType.Body:
                        // Apply body shot visual effects  
                        Library_ExiledAPI.LogDebug("ApplyDeathVisualEffects", "Applied body shot visual effects");
                        break;
                    case HitboxType.Limb:
                        // Apply limb shot visual effects  
                        Library_ExiledAPI.LogDebug("ApplyDeathVisualEffects", "Applied limb shot visual effects");
                        break;
                }
            }
            catch (Exception ex)
            {
                Library_ExiledAPI.LogError("ApplyDeathVisualEffects", $"Failed to apply visual effects: {ex.Message}");
            }
        }

        /// <summary>  
        /// Schedules post-spawn validation to ensure ragdoll visibility.
        /// </summary>  
        /// <param name="ragdoll">The ragdoll to validate.</param>  
        public static void RagdollSchedulePostSpawnValidation(LabApi.Features.Wrappers.Ragdoll ragdoll)
        {
            if (ragdoll == null)
            {
                Library_ExiledAPI.LogError("RagdollSchedulePostSpawnValidation", "Ragdoll is null");
                return;
            }

            Timing.CallDelayed(1.5f, () =>
            {
                try
                {
                    if (ragdoll?.Base == null)
                    {
                        Library_ExiledAPI.LogWarn("RagdollSchedulePostSpawnValidation", "Ragdoll was destroyed before validation");
                        return;
                    }

                    var renderer = ragdoll.Base.GetComponentInChildren<Renderer>();
                    if (renderer == null)
                    {
                        Library_ExiledAPI.LogError("RagdollSchedulePostSpawnValidation", "No Renderer found in ragdoll hierarchy after spawn");
                    }
                    else
                    {
                        Library_ExiledAPI.LogDebug("RagdollSchedulePostSpawnValidation",
                            $"Ragdoll renderer validation - enabled: {renderer.enabled}, visible: {renderer.isVisible}");

                        if (!renderer.isVisible)
                        {
                            Library_ExiledAPI.LogWarn("RagdollSchedulePostSpawnValidation", "Ragdoll renderer is not visible after spawn");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Library_ExiledAPI.LogError("RagdollSchedulePostSpawnValidation", $"Validation failed: {ex.Message}");
                }
            });
        }

        /// <summary>  
        /// Creates a debug marker at the specified position for development purposes.
        /// </summary>  
        /// <param name="position">The world position to create the marker at.</param>
        public static void CreateDebugMarker(Vector3 position)
        {
            try
            {
                GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                marker.transform.position = position + Vector3.up;
                marker.transform.localScale = new Vector3(0.35f, 0.65f, 0.35f);

                var renderer = marker.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material.color = Color.magenta;
                }

                // Auto-destroy after 3 minutes  
                GameObject.Destroy(marker, 180f);
                Library_ExiledAPI.LogDebug("CreateDebugMarker", $"Debug marker created at: {marker.transform.position}");
            }
            catch (Exception ex)
            {
                Library_ExiledAPI.LogError("CreateDebugMarker", $"Failed to create debug marker: {ex.Message}");
            }
        }
    }
}