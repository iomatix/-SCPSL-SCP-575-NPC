namespace SCP_575.Shared
{
    using Footprinting;
    using InventorySystem.Items.Armor;
    using Mirror;
    using PlayerRoles;
    using PlayerRoles.PlayableScps.Scp3114;
    using PlayerRoles.Ragdolls;
    using PlayerStatsSystem;
    using RemoteAdmin.Communication;
    using System;
    using System.Collections.Generic;
    using UnityEngine;

    public static class Scp575DamageSystem
    {
        #region Constants and Static Properties  

        /// <summary>  
        /// Gets the unique identifier name for this class.
        /// </summary>  
        public static string IdentifierName => nameof(Scp575DamageSystem);

        /// <summary>  
        /// Defines force multipliers applied to different hitbox types during ragdoll processing.
        /// </summary>  
        /// <remarks>  
        /// These values are currently unused due to ragdoll position restoration but maintained for future use.
        /// </remarks>  
        public static readonly IReadOnlyDictionary<HitboxType, float> HitboxToForce = new Dictionary<HitboxType, float>
        {
            [HitboxType.Body] = 0.08f,
            [HitboxType.Headshot] = 0.085f,
            [HitboxType.Limb] = 0.016f
        };

        /// <summary>  
        /// Defines damage multipliers for different hitbox types to simulate realistic damage scaling.
        /// </summary>  
        public static readonly IReadOnlyDictionary<HitboxType, float> HitboxDamageMultipliers = new Dictionary<HitboxType, float>
        {
            [HitboxType.Body] = 1.0f,
            [HitboxType.Headshot] = 1.85f,
            [HitboxType.Limb] = 0.75f
        };

        #endregion

        #region Properties  

        /// <summary>  
        /// Gets the DamagePenetration factor for futher damage processing.
        /// </summary>  
        public static float DamagePenetration => Library_LabAPI.NpcConfig.KeterDamagePenetration;

        /// <summary>  
        /// Gets the Reason text displayed on the player's death screen.
        /// </summary>  
        public static string DeathScreenText => Library_LabAPI.NpcConfig.KilledByMessage;

        /// <summary>  
        /// Gets the text displayed when inspecting a ragdoll killed by this handler.
        /// </summary>  
        public static string RagdollInspectText => Library_LabAPI.NpcConfig.RagdollInspectText;

        /// <summary>  
        /// Gets the CASSIE announcement for deaths caused by this handler.
        /// SCP-575 deaths do not trigger CASSIE announcements.
        /// </summary>  
        public static string CassieDeathAnnouncement => "";

        #endregion

        #region Damage Processing 

        /// <summary>  
        /// Applies damage to the specified player with hitbox-based multipliers and armor penetration calculations.  
        /// </summary>  
        /// <param name="target">The player receiving damage.</param>  
        /// <param name="damage">The base damage amount to apply.</param>  
        /// <param name="hitbox">The hitbox type that determines damage multipliers.</param>  
        /// <returns><c>true</c> if damage was successfully applied; otherwise, <c>false</c>.</returns>  
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="target"/> is null.</exception>  
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="damage"/> is negative.</exception>  
        public static bool DamagePlayer(LabApi.Features.Wrappers.Player target, float damage, HitboxType hitbox = HitboxType.Body)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            if (damage < 0f)
                throw new ArgumentOutOfRangeException(nameof(damage), "Damage cannot be negative.");

            Library_ExiledAPI.LogDebug("DamagePlayer",
                $"Processing damage for {target.Nickname} with Hitbox: {hitbox} and Damage: {damage:F1}");

            float processedDamage = DamageProcessor(target, damage, hitbox);

            bool succeeded = target.Damage(processedDamage, DeathScreenText);

            if (succeeded)
            {
                if (target.IsAlive && Library_LabAPI.NpcConfig.EnableKeterOnDealDamageEffects)
                {
                    Scp575DamageSystem_LabAPI.ApplyDamageEffects(target);
                }

                Library_ExiledAPI.LogDebug("DamagePlayer",
                    target.IsAlive ? "Damage applied successfully - player survived" : "Damage applied successfully - player died");
            }
            else
            {
                Library_ExiledAPI.LogDebug("DamagePlayer", "Failed to apply damage");
            }

            return succeeded;
        }

        /// <summary>  
        /// Processes damage by applying hitbox multipliers and armor penetration calculations.  
        /// </summary>  
        /// <param name="target">The target player.</param>  
        /// <param name="damage">The base damage amount.</param>  
        /// <param name="hitbox">The hitbox type for multiplier calculations.</param>  
        /// <returns>The final processed damage amount after all calculations.</returns>  
        private static float DamageProcessor(LabApi.Features.Wrappers.Player target, float damage, HitboxType hitbox)
        {
            if (damage <= 0f)
                return damage;

            Library_ExiledAPI.LogDebug("DamageProcessor",
                $"Processing damage for {target.Nickname} with Hitbox: {hitbox} and Damage: {damage:F1}");

            float processedDamage = damage; // Initialize with base damage  

            // Apply hitbox-specific damage multipliers    
            if (HitboxDamageMultipliers.TryGetValue(hitbox, out var damageMul))
            {
                processedDamage *= damageMul;
                Library_ExiledAPI.LogDebug("DamageProcessor",
                    $"Applied hitbox multiplier {damageMul:F2} for {hitbox}. Damage: {damage:F1} -> {processedDamage:F1}");
            }

            // Handle armor interactions for armored roles - use the multiplied damage  
            processedDamage = ProcessArmorInteraction(target, processedDamage, hitbox);

            return processedDamage;
        }

        /// <summary>  
        /// Processes armor interactions by calculating penetration effects on damage.  
        /// Handles both Hume Shield and body armor calculations.  
        /// </summary>  
        /// <param name="target">The target player with potential armor.</param>  
        /// <param name="damage">The damage amount to process through armor.</param>  
        /// <param name="hitbox">The hitbox type for armor efficacy calculations.</param>  
        /// <returns>The final damage amount after armor processing.</returns>  
        private static float ProcessArmorInteraction(LabApi.Features.Wrappers.Player target, float damage, HitboxType hitbox)
        {
            if (damage <= 0f || target.RoleBase is not IArmoredRole armoredRole)
                return damage;

            Library_ExiledAPI.LogDebug("ProcessArmorInteraction",
                $"Player {target.Nickname} has armor role: {armoredRole.GetType().Name}");

            int armorEfficacy = armoredRole.GetArmorEfficacy(hitbox);
            int penetrationPercent = Mathf.RoundToInt(DamagePenetration * 100f);

            // Calculate Hume Shield absorption  
            float humeShield = target.ReferenceHub.playerStats.GetModule<HumeShieldStat>().CurValue;
            float shieldDamage = Mathf.Clamp(humeShield, 0f, damage);

            // Calculate damage that goes through to armor  
            float armorDamage = Mathf.Max(0f, damage - shieldDamage);

            // Apply armor penetration calculation  
            float postArmorDamage = BodyArmorUtils.ProcessDamage(armorEfficacy, armorDamage, penetrationPercent);

            // Final damage is shield damage (always full) plus penetrated armor damage  
            float finalDamage = shieldDamage + postArmorDamage;

            Library_ExiledAPI.LogDebug("ProcessArmorInteraction",
                $"Armor calculation for {target.Nickname}: " +
                $"Efficacy={armorEfficacy}, Penetration={penetrationPercent}%, " +
                $"Shield={shieldDamage:F1}, Armor={armorDamage:F1}->{postArmorDamage:F1}, " +
                $"Final={finalDamage:F1}");

            return finalDamage;
        }

        #endregion

        #region Ragdoll Processing  

        /// <summary>  
        /// Processes an SCP-575 ragdoll with visual effects and validation using LabAPI synchronization.  
        /// </summary>  
        /// <param name="ragdoll">The LabAPI ragdoll wrapper to process.</param>  
        /// <param name="handler">The SCP-575 damage handler that caused the death.</param>  
        /// <exception cref="ArgumentNullException">Thrown when ragdoll or handler is null.</exception>  
        public static void RagdollProcessor(LabApi.Features.Wrappers.Ragdoll ragdoll)
        {
            if (ragdoll == null)
                throw new ArgumentNullException(nameof(ragdoll));

            Library_ExiledAPI.LogDebug("RagdollProcess", $"Processing SCP-575 ragdoll at position: {ragdoll.Position}");

            // Set the damage handler to control ragdoll inspection text
            var customHandler = new CustomReasonDamageHandler(RagdollInspectText, 0.0f, "");
            ragdoll.DamageHandler = customHandler;

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

            Library_ExiledAPI.LogDebug("RagdollProcess", "SCP-575 ragdoll processing completed successfully");
        }

        /// <summary>
        /// Applies bone conversion to the ragdoll using LabAPI synchronization with forced network spawning.
        /// </summary>
        /// <param name="dynamicRagdoll">The dynamic ragdoll to convert.</param>
        /// <param name="ragdollWrapper">The LabAPI ragdoll wrapper for synchronization.</param>
        /// <returns>True if conversion succeeded, false otherwise.</returns>
        public static bool ApplyBoneConversion(DynamicRagdoll dynamicRagdoll, LabApi.Features.Wrappers.Ragdoll ragdollWrapper)
        {
            try
            {
                Library_ExiledAPI.LogDebug("ApplyBoneConversion",
                    $"Starting bone conversion - Child count: {dynamicRagdoll.transform.childCount}");


                // Apply the bone conversion
                Scp3114RagdollToBonesConverter.ConvertExisting(dynamicRagdoll);

                Library_ExiledAPI.LogDebug("ApplyBoneConversion",
                    $"Bone conversion completed - Child count: {dynamicRagdoll.transform.childCount}");

                return true;
            }
            catch (Exception ex)
            {
                Library_ExiledAPI.LogError("ApplyBoneConversion", $"Bone conversion failed: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region Utility Methods  

        /// <summary>
        /// Calculates a randomized force push multiplier within configured bounds.
        /// </summary>  
        /// <param name="baseValue">The base value to multiply by the random factor.</param>
        /// <returns>A randomized force multiplier value.</returns>
        /// <remarks>
        /// This method is currently unused due to ragdoll position restoration but maintained  
        /// for potential future use in item physics or other SCP-575 effects.
        /// </remarks>
        public static float CalculateForcePush(float baseValue = 1.0f)
        {
            float randomFactor = UnityEngine.Random.Range(
                Library_LabAPI.NpcConfig.KeterForceMinModifier,
                Library_LabAPI.NpcConfig.KeterForceMaxModifier);

            return baseValue * randomFactor;
        }

        /// <summary>
        /// Generates a random unit sphere velocity vector with damage-based scaling.
        /// </summary>  
        /// <param name="baseValue">The base velocity multiplier.</param>
        /// <returns>A scaled random velocity vector.</returns>
        /// <remarks>
        /// This method includes logic to prevent downward-pointing vectors that could cause  
        /// items to fall through the floor. The velocity is scaled logarithmically based on  
        /// damage amount to provide realistic physics effects.
        /// Currently unused for ragdolls due to position restoration.
        /// </remarks>
        public static Vector3 GetRandomUnitSphereVelocity(float baseValue = 1.0f)
        {
            Vector3 randomDirection = UnityEngine.Random.onUnitSphere;

            // Prevent downward vectors that could cause items to clip through floors  
            // If the vector points more than 45° downward, reflect it upward  
            if (Vector3.Dot(randomDirection, Vector3.down) > 0.707f) // cos(45°) ≈ 0.707  
            {
                Exiled.API.Features.Log.Debug(
                    "[GetRandomUnitSphereVelocity] Vector pointing downward, reflecting upward.");
                randomDirection = Vector3.Reflect(randomDirection, Vector3.up);
            }

            // Apply logarithmic scaling based on damage for realistic force distribution  
            float modifier = baseValue *
                           Mathf.Log((5 * Library_LabAPI.NpcConfig.KeterDamage) + 1) *
                           CalculateForcePush(Library_LabAPI.NpcConfig.KeterDamageVelocityModifier);

            return randomDirection * modifier;
        }

        public static bool IsScp575Damage(DamageHandlerBase handler)
        {
            return handler is CustomReasonDamageHandler customHandler &&
                   (customHandler.DeathScreenText == DeathScreenText || customHandler.RagdollInspectText == RagdollInspectText);
        }

        #endregion
    }

}