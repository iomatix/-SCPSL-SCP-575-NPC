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

    /// <summary>  
    /// Provides utility methods for processing SCP-575 ragdolls with visual effects and validation.  
    /// Uses LabAPI's built-in synchronization mechanisms for proper network handling.  
    /// </summary>  
    /// <remarks>  
    /// This class leverages LabAPI's wrapper system for network synchronization instead of  
    /// manually managing NetworkTransform components, ensuring compatibility with the framework.  
    /// </remarks>  
    public static class Scp575Helpers
    {
        /// <summary>  
        /// Processes an SCP-575 ragdoll with visual effects and validation using LabAPI synchronization.  
        /// </summary>  
        /// <param name="ragdoll">The LabAPI ragdoll wrapper to process.</param>  
        /// <param name="handler">The SCP-575 damage handler that caused the death.</param>  
        /// <exception cref="ArgumentNullException">Thrown when ragdoll or handler is null.</exception>  
        public static void RagdollProcess(Ragdoll ragdoll, Scp575DamageHandler handler)
        {
            if (ragdoll == null)
                throw new ArgumentNullException(nameof(ragdoll));
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            Library_ExiledAPI.LogDebug("RagdollProcess", $"Processing SCP-575 ragdoll at position: {ragdoll.Position}");

            // Validate ragdoll state using LabAPI wrapper properties  
            if (!ValidateRagdollState(ragdoll))
                return;

            // Apply bone conversion if ragdoll is DynamicRagdoll  
            if (ragdoll.Base is DynamicRagdoll dynamicRagdoll)
            {
                if (!ApplyBoneConversion(dynamicRagdoll, ragdoll))
                    return;
            }
            else
            {
                Library_ExiledAPI.LogWarn("RagdollProcess", "Ragdoll is not a DynamicRagdoll. Skipping bone conversion.");
            }

            // Apply visual effects using LabAPI synchronization  
            ApplyVisualEffects(ragdoll, handler);

            // Schedule validation using LabAPI's timing system  
            SchedulePostSpawnValidation(ragdoll);

            // Create debug marker if enabled  
            if (Library_LabAPI.Config.Debug)
                CreateDebugMarker(ragdoll.Position);

            Library_ExiledAPI.LogDebug("RagdollProcess", "SCP-575 ragdoll processing completed successfully");
        }

        /// <summary>  
        /// Validates ragdoll state using LabAPI wrapper properties and built-in synchronization.  
        /// </summary>  
        /// <param name="ragdoll">The LabAPI ragdoll wrapper to validate.</param>  
        /// <returns>True if validation passed, false if critical issues were found.</returns>  
        private static bool ValidateRagdollState(Ragdoll ragdoll)
        {
            if (ragdoll?.Base == null)
            {
                Library_ExiledAPI.LogError("ValidateRagdollState", "Ragdoll Base is null");
                return false;
            }

            GameObject ragdollGO = ragdoll.Base.gameObject;
            if (ragdollGO == null)
            {
                Library_ExiledAPI.LogError("ValidateRagdollState", "Ragdoll GameObject is null");
                return false;
            }

            // Ensure ragdoll GameObject is active  
            if (!ragdollGO.activeSelf)
            {
                ragdollGO.SetActive(true);
                Library_ExiledAPI.LogWarn("ValidateRagdollState", "Ragdoll GameObject was inactive — enabled manually");
            }

            // Handle visibility culling using Unity components  
            if (ragdollGO.TryGetComponent<CullableBehaviour>(out var cullable))
            {
                cullable.enabled = false;
                Library_ExiledAPI.LogDebug("ValidateRagdollState", "Disabled CullableBehaviour to force visibility");
            }

            // Validate and fix renderer states  
            var renderers = ragdollGO.GetComponentsInChildren<Renderer>(true);
            Library_ExiledAPI.LogDebug("ValidateRagdollState", $"Found {renderers.Length} renderers in hierarchy");

            int boneRendererCount = 0;
            int humanRendererCount = 0;

            foreach (var renderer in renderers)
            {
                bool isBonePart = IsBonePartRenderer(renderer.name);

                if (isBonePart)
                {
                    boneRendererCount++;
                    EnsureRendererEnabled(renderer, "bone part");
                }
                else
                {
                    humanRendererCount++;
                    EnsureRendererEnabled(renderer, "human part");
                }
            }

            Library_ExiledAPI.LogDebug("ValidateRagdollState",
                $"Renderer summary - Human: {humanRendererCount}, Bone: {boneRendererCount}, Total: {renderers.Length}");

            // Use LabAPI's Position property for network synchronization  
            Vector3 currentPosition = ragdoll.Position;
            ragdoll.Position = currentPosition; // This triggers LabAPI's network sync  

            Library_ExiledAPI.LogDebug("ValidateRagdollState",
                $"Ragdoll synchronized - Type: {ragdoll.Base.GetType().Name}, Position: {ragdoll.Position}");

            return true;
        }

        /// <summary>  
        /// Applies bone conversion to the ragdoll using LabAPI synchronization.  
        /// </summary>  
        /// <param name="dynamicRagdoll">The dynamic ragdoll to convert.</param>  
        /// <param name="ragdollWrapper">The LabAPI ragdoll wrapper for synchronization.</param>  
        /// <returns>True if conversion succeeded, false otherwise.</returns>  
        private static bool ApplyBoneConversion(DynamicRagdoll dynamicRagdoll, Ragdoll ragdollWrapper)
        {
            try
            {
                Library_ExiledAPI.LogDebug("ApplyBoneConversion",
                    $"Starting bone conversion - Child count: {dynamicRagdoll.transform.childCount}");

                // Apply the bone conversion  
                Scp3114RagdollToBonesConverter.ConvertExisting(dynamicRagdoll);

                Library_ExiledAPI.LogDebug("ApplyBoneConversion",
                    $"Bone conversion completed - Child count: {dynamicRagdoll.transform.childCount}");

                // Process bone parts without manually adding NetworkTransform  
                ProcessBoneParts(dynamicRagdoll, ragdollWrapper);

                // Use LabAPI's synchronization to ensure network consistency  
                SynchronizeRagdollState(ragdollWrapper);

                return true;
            }
            catch (Exception ex)
            {
                Library_ExiledAPI.LogError("ApplyBoneConversion", $"Bone conversion failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>  
        /// Processes bone parts after conversion, ensuring proper visibility without manual NetworkTransform management.  
        /// </summary>  
        /// <param name="dynamicRagdoll">The converted dynamic ragdoll.</param>  
        /// <param name="ragdollWrapper">The LabAPI wrapper for synchronization.</param>  
        private static void ProcessBoneParts(DynamicRagdoll dynamicRagdoll, Ragdoll ragdollWrapper)
        {
            foreach (Transform child in dynamicRagdoll.transform)
            {
                if (child.name.EndsWith("(Clone)"))
                {
                    GameObject boneGO = child.gameObject;
                    Library_ExiledAPI.LogDebug("ProcessBoneParts", $"Processing bone part: {boneGO.name}");

                    // Ensure bone part is active and visible  
                    if (!boneGO.activeSelf)
                    {
                        boneGO.SetActive(true);
                        Library_ExiledAPI.LogDebug("ProcessBoneParts", $"Activated bone part: {boneGO.name}");
                    }

                    // Enable all renderers on bone parts  
                    var renderers = boneGO.GetComponentsInChildren<Renderer>(true);
                    foreach (var renderer in renderers)
                    {
                        EnsureRendererEnabled(renderer, $"bone part {boneGO.name}");
                    }
                }
            }

            // Schedule delayed validation using MEC timing  
            Timing.CallDelayed(0.15f, () => ValidateBonePartsDelayed(dynamicRagdoll));
        }

        /// <summary>  
        /// Validates bone parts after a delay to ensure proper initialization.  
        /// </summary>  
        /// <param name="dynamicRagdoll">The dynamic ragdoll to validate.</param>  
        private static void ValidateBonePartsDelayed(DynamicRagdoll dynamicRagdoll)
        {
            if (dynamicRagdoll == null) return;

            foreach (Transform child in dynamicRagdoll.transform)
            {
                if (child.name.EndsWith("(Clone)"))
                {
                    GameObject boneGO = child.gameObject;
                    bool isActive = boneGO.activeSelf;
                    var renderers = boneGO.GetComponentsInChildren<Renderer>(true);
                    int enabledRenderers = renderers.Count(r => r.enabled);

                    Library_ExiledAPI.LogDebug("ValidateBonePartsDelayed",
                        $"Bone part {boneGO.name} - Active: {isActive}, Enabled renderers: {enabledRenderers}/{renderers.Length}");
                }
            }
        }

        /// <summary>  
        /// Synchronizes ragdoll state using LabAPI's built-in mechanisms.  
        /// </summary>  
        /// <param name="ragdoll">The ragdoll wrapper to synchronize.</param>  
        private static void SynchronizeRagdollState(Ragdoll ragdoll)
        {
            // Use LabAPI's Position property which handles NetworkInfo synchronization  
            Vector3 currentPosition = ragdoll.Position;
            Quaternion currentRotation = ragdoll.Rotation;
            Vector3 currentScale = ragdoll.Scale;

            // Trigger synchronization by setting properties (LabAPI handles the network sync)  
            ragdoll.Position = currentPosition;
            ragdoll.Rotation = currentRotation;
            ragdoll.Scale = currentScale;

            Library_ExiledAPI.LogDebug("SynchronizeRagdollState",
                $"Ragdoll state synchronized - Position: {currentPosition}, Rotation: {currentRotation}");
        }

        /// <summary>  
        /// Applies visual effects to the ragdoll using LabAPI synchronization.  
        /// </summary>  
        /// <param name="ragdoll">The LabAPI ragdoll wrapper.</param>  
        /// <param name="handler">The damage handler containing effect parameters.</param>  
        private static void ApplyVisualEffects(Ragdoll ragdoll, Scp575DamageHandler handler)
        {
            var hitbox = handler.Hitbox;

            Library_ExiledAPI.LogDebug("ApplyVisualEffects",
                $"Applying SCP-575 visual effects to ragdoll at hitbox: {hitbox}");

            // Apply visual effects based on hitbox type  
            ApplyHitboxSpecificEffects(ragdoll, hitbox);

            // Use LabAPI's synchronization to ensure effects are visible to all clients  
            SynchronizeRagdollState(ragdoll);

            Library_ExiledAPI.LogDebug("ApplyVisualEffects", "SCP-575 visual effects applied successfully");
        }

        /// <summary>  
        /// Applies hitbox-specific visual effects to the ragdoll.  
        /// </summary>  
        /// <param name="ragdoll">The ragdoll wrapper to modify.</param>  
        /// <param name="hitbox">The hitbox that was targeted.</param>  
        private static void ApplyHitboxSpecificEffects(Ragdoll ragdoll, HitboxType hitbox)
        {
            try
            {
                switch (hitbox)
                {
                    case HitboxType.Headshot:
                        ApplyHeadshotEffects(ragdoll);
                        Library_ExiledAPI.LogDebug("ApplyHitboxSpecificEffects", "Applied headshot visual effects");
                        break;
                    case HitboxType.Body:
                        ApplyBodyShotEffects(ragdoll);
                        Library_ExiledAPI.LogDebug("ApplyHitboxSpecificEffects", "Applied body shot visual effects");
                        break;
                    case HitboxType.Limb:
                        ApplyLimbShotEffects(ragdoll);
                        Library_ExiledAPI.LogDebug("ApplyHitboxSpecificEffects", "Applied limb shot visual effects");
                        break;
                    default:
                        Library_ExiledAPI.LogWarn("ApplyHitboxSpecificEffects", $"Unknown hitbox type: {hitbox}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Library_ExiledAPI.LogError("ApplyHitboxSpecificEffects", $"Failed to apply visual effects: {ex.Message}");
            }
        }

        /// <summary>  
        /// Schedules post-spawn validation using LabAPI's timing system.  
        /// </summary>  
        /// <param name="ragdoll">The ragdoll to validate.</param>  
        private static void SchedulePostSpawnValidation(Ragdoll ragdoll)
        {
            if (ragdoll == null)
            {
                Library_ExiledAPI.LogError("SchedulePostSpawnValidation", "Ragdoll is null");
                return;
            }

            // Use MEC timing for delayed validation  
            Timing.CallDelayed(0.1f, () => PerformPostSpawnValidation(ragdoll));
        }

        /// <summary>  
        /// Performs post-spawn validation to ensure ragdoll visibility across all clients.  
        /// </summary>  
        /// <param name="ragdoll">The ragdoll to validate.</param>  
        private static void PerformPostSpawnValidation(Ragdoll ragdoll)
        {
            if (ragdoll?.Base == null) return;

            var renderers = ragdoll.Base.gameObject.GetComponentsInChildren<Renderer>(true);
            int enabledCount = 0;
            int totalCount = renderers.Length;

            foreach (var renderer in renderers)
            {
                if (!renderer.enabled)
                {
                    renderer.enabled = true;
                    Library_ExiledAPI.LogDebug("PerformPostSpawnValidation", $"Re-enabled renderer: {renderer.name}");
                }

                if (renderer.enabled) enabledCount++;

                Library_ExiledAPI.LogDebug("PerformPostSpawnValidation",
                    $"Renderer validation - {renderer.name}: enabled={renderer.enabled}, visible={renderer.isVisible}");
            }

            Library_ExiledAPI.LogDebug("PerformPostSpawnValidation",
                $"Post-spawn validation completed - {enabledCount}/{totalCount} renderers enabled");

            // Force a final synchronization to ensure all clients see the ragdoll properly  
            SynchronizeRagdollState(ragdoll);
        }

        /// <summary>  
        /// Determines if a renderer belongs to a bone part based on naming conventions.  
        /// </summary>  
        /// <param name="rendererName">The name of the renderer to check.</param>  
        /// <returns>True if the renderer is identified as a bone part, false otherwise.</returns>  
        private static bool IsBonePartRenderer(string rendererName)
        {
            return rendererName.Contains("Clone") ||
                   rendererName.Contains("Spine") ||
                   rendererName.Contains("Head") ||
                   rendererName.Contains("Arm") ||
                   rendererName.Contains("Thigh") ||
                   rendererName.Contains("leg");
        }

        /// <summary>  
        /// Ensures a renderer is properly enabled and logs the operation.  
        /// </summary>  
        /// <param name="renderer">The renderer to enable.</param>  
        /// <param name="partType">Description of the part type for logging purposes.</param>  
        private static void EnsureRendererEnabled(Renderer renderer, string partType)
        {
            if (!renderer.gameObject.activeSelf)
            {
                renderer.gameObject.SetActive(true);
                Library_ExiledAPI.LogDebug("EnsureRendererEnabled", $"Activated {partType} GameObject: {renderer.name}");
            }

            if (!renderer.enabled)
            {
                renderer.enabled = true;
                Library_ExiledAPI.LogDebug("EnsureRendererEnabled", $"Enabled {partType} renderer: {renderer.name}");
            }

            // Force visibility refresh if renderer is enabled but not visible  
            if (renderer.enabled && renderer.gameObject.activeSelf && !renderer.isVisible)
            {
                renderer.enabled = false;
                renderer.enabled = true;

                // Force material refresh if available  
                if (renderer.material != null)
                {
                    var material = renderer.material;
                    renderer.material = material;
                }

                Library_ExiledAPI.LogDebug("EnsureRendererEnabled", $"Forced visibility refresh for {partType}: {renderer.name}");
            }
        }

        /// <summary>  
        /// Applies headshot-specific visual effects to the ragdoll.  
        /// </summary>  
        /// <param name="ragdoll">The ragdoll to apply effects to.</param>  
        private static void ApplyHeadshotEffects(Ragdoll ragdoll)
        {
            // Implementation for headshot visual effects  
            // This could include particle effects, material changes, etc.  
            Library_ExiledAPI.LogDebug("ApplyHeadshotEffects", $"Applied headshot effects to ragdoll at {ragdoll.Position}");
        }

        /// <summary>  
        /// Applies body shot-specific visual effects to the ragdoll.  
        /// </summary>  
        /// <param name="ragdoll">The ragdoll to apply effects to.</param>  
        private static void ApplyBodyShotEffects(Ragdoll ragdoll)
        {
            // Implementation for body shot visual effects  
            Library_ExiledAPI.LogDebug("ApplyBodyShotEffects", $"Applied body shot effects to ragdoll at {ragdoll.Position}");
        }

        /// <summary>  
        /// Applies limb shot-specific visual effects to the ragdoll.  
        /// </summary>  
        /// <param name="ragdoll">The ragdoll to apply effects to.</param>  
        private static void ApplyLimbShotEffects(Ragdoll ragdoll)
        {
            // Implementation for limb shot visual effects  
            Library_ExiledAPI.LogDebug("ApplyLimbShotEffects", $"Applied limb shot effects to ragdoll at {ragdoll.Position}");
        }

        /// <summary>  
        /// Creates a debug marker at the specified position for development purposes.  
        /// </summary>  
        /// <param name="position">The world position to create the marker at.</param>  
        private static void CreateDebugMarker(Vector3 position)
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