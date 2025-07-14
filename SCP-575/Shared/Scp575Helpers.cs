
namespace SCP_575.Shared
{
    using System;
    using MEC;
    using PlayerRoles.PlayableScps.Scp3114;
    using PlayerRoles.Ragdolls;
    using ProgressiveCulling;
    using UnityEngine;
    public static class Scp575Helpers
{
        public static void RagdollProcess(LabApi.Features.Wrappers.Ragdoll ragdoll, Scp575DamageHandler handler)
        {
            GameObject ragdollGO = ragdoll.Base.gameObject;

            // Validate ragdoll state  
            if (!RagdollValidateState(ragdoll, ragdollGO))
                return;

            // Ensure ragdoll is a DynamicRagdoll for physics manipulation  
            if (ragdoll.Base is not DynamicRagdoll dynamicRagdoll)
            {
                Library_ExiledAPI.LogWarn("ProcessScp575Ragdoll", "Ragdoll is not a DynamicRagdoll. Skipping physics effects.");
                return;
            }

            // Apply bone conversion for proper physics  
            if (!RagdollApplyBoneConversion(dynamicRagdoll))
                return;

            // Apply SCP-575 specific effects  
            RagdollApplyScp575Effects(dynamicRagdoll, handler, ragdoll);

            // Schedule post-spawn validation  
            RagdollSchedulePostSpawnValidation(ragdoll);

            // Create debug marker if needed  
            CreateDebugMarker(ragdoll.Position);
        }

        public static bool RagdollValidateState(LabApi.Features.Wrappers.Ragdoll ragdoll, GameObject ragdollGO)
        {
            // Check if ragdoll GameObject is active  
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

            // Log renderer information for debugging  
            var renderers = ragdollGO.GetComponentsInChildren<Renderer>(true);
            Library_ExiledAPI.LogDebug("RagdollValidateState", $"Found {renderers.Length} renderers in hierarchy");

            foreach (var renderer in renderers)
            {
                Library_ExiledAPI.LogDebug("RagdollValidateState",
                    $"Renderer: {renderer.name}, enabled: {renderer.enabled}, active: {renderer.gameObject.activeSelf}");
            }

            Library_ExiledAPI.LogDebug("RagdollValidateState",
                $"Ragdoll type: {ragdoll.Base.GetType().Name}, Position: {ragdoll.Position}");

            return true;
        }

        public static bool RagdollApplyBoneConversion(DynamicRagdoll dynamicRagdoll)
        {
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

        public static void RagdollApplyScp575Effects(DynamicRagdoll dynamicRagdoll, Scp575DamageHandler handler, LabApi.Features.Wrappers.Ragdoll ragdoll)
        {
            var hitbox = handler.Hitbox;

            if (!Scp575DamageHandler.HitboxToForce.TryGetValue(hitbox, out float baseForce))
            {
                Library_ExiledAPI.LogWarn("RagdollApplyScp575Effects", $"Unknown hitbox: {hitbox}. Using default force.");
                baseForce = 0.1f; // Default force value  
            }

            // Generate dramatic force vector  
            Vector3 forceVector = handler.GetRandomUnitSphereVelocity(baseForce);
            Library_ExiledAPI.LogDebug("RagdollApplyScp575Effects", $"Applying force to hitbox: {hitbox} with base force: {baseForce}, velocity: {forceVector}");

            // Apply force to specific hitbox with validation  
            bool forceApplied = false;
            foreach (var hitboxComponent in dynamicRagdoll.Hitboxes)
            {
                if (hitboxComponent.RelatedHitbox != hitbox)
                    continue;

                if (hitboxComponent.Target != null)
                {
                    hitboxComponent.Target.AddForce(forceVector, ForceMode.VelocityChange);
                    Library_ExiledAPI.LogDebug("RagdollApplyScp575Effects", $"Applied force to hitbox: {hitboxComponent.RelatedHitbox}");
                    forceApplied = true;
                }
            }

            if (!forceApplied)
            {
                Library_ExiledAPI.LogWarn("RagdollApplyScp575Effects", $"No valid hitbox found for {hitbox}, applying to all limbs");
            }

            // Apply secondary force to all limbs for dramatic effect  
            foreach (var rigidbody in dynamicRagdoll.LinkedRigidbodies)
            {
                if (rigidbody != null)
                {
                    rigidbody.AddForce(forceVector * 0.3f, ForceMode.VelocityChange);
                }
            }

            Library_ExiledAPI.LogDebug("RagdollApplyScp575Effects", "SCP-575 ragdoll effects applied successfully");
        }

        public static void RagdollSchedulePostSpawnValidation(LabApi.Features.Wrappers.Ragdoll ragdoll)
        {
            Timing.CallDelayed(0.5f, () =>
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
                        Library_ExiledAPI.LogDebug("RagdollSchedulePostSpawnValidation", $"Ragdoll renderer validation - enabled: {renderer.enabled}, visible: {renderer.isVisible}");

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
