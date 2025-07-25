namespace SCP_575.Systems
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using InventorySystem.Items.Armor;
    using MEC;
    using PlayerRoles;
    using PlayerRoles.PlayableScps.Scp3114;
    using PlayerRoles.Ragdolls;
    using PlayerStatsSystem;
    using SCP_575.Shared;

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
        /// <exception cref="ArgumentNullException">Thrown when ragdoll or handler is null.</exception>
        public static void RagdollProcessor(Exiled.API.Features.Player player, Exiled.API.Features.Ragdoll ragdoll)
        {
            if (ragdoll == null)
                throw new ArgumentNullException(nameof(ragdoll));

            Library_ExiledAPI.LogDebug("RagdollProcess", $"Processing SCP-575 ragdoll at position: {ragdoll.Position}");
            try
            {
                Exiled.API.Features.Ragdoll newRagdoll = ReplaceRagdoll(player, ragdoll);
                ApplyStandardRagdollPhysics(newRagdoll);
                ConvertToBones(newRagdoll);
                Library_ExiledAPI.LogDebug("RagdollProcess", $"SCP-575 ragdoll processing completed successfully");
            }
            catch (Exception ex)
            {
                Library_ExiledAPI.LogError("RagdollProcess", $"Failed to process exiled ragdoll: {ex.Message}");
            }
        }

        public static Exiled.API.Features.Ragdoll ReplaceRagdoll(Exiled.API.Features.Player player, Exiled.API.Features.Ragdoll originalRagdoll)
        {
            if (player == null || originalRagdoll == null)
                return null;

            Library_ExiledAPI.LogDebug("ReplaceRagdollWithPhysics",
                $"Replacing ragdoll for {player.Nickname} at position: {originalRagdoll.Position}");

            // Prepare RagdollData
            var customHandler = new CustomReasonDamageHandler(RagdollInspectText, 0.0f, "");
            var ragdollData = new RagdollData(
                player.ReferenceHub,
                customHandler,
                RoleTypeId.Scp3114, // Temporary replace body to skeleton, for player.Role,
                originalRagdoll.Position,
                originalRagdoll.Rotation,
                player.DisplayNickname,
                Time.time);

            originalRagdoll.Destroy();

            // Spawn Exiled ragdoll
            return Exiled.API.Features.Ragdoll.CreateAndSpawn(ragdollData);
        }


        /// <summary>    
        /// Applies physics forces to standard ragdoll for dynamic movement effects.    
        /// Used with Exiled ragdoll wrappers for reliable physics application.  
        /// </summary>    
        /// <param name="ragdoll">The Exiled ragdoll wrapper to apply physics to.</param>    
        public static void ApplyStandardRagdollPhysics(Exiled.API.Features.Ragdoll ragdoll)
        {

            Library_ExiledAPI.LogDebug("ApplyStandardRagdollPhysics", $"Attempting physics on Exiled ragdoll at {ragdoll.Position}");

            Vector3 upwardForce = Vector3.up * CalculateForcePush(3.8f);
            Vector3 randomForce = GetRandomUnitSphereVelocity(2.8f);
            foreach (Rigidbody rb in ragdoll.SpecialRigidbodies)
            {
                if (ragdoll.SpecialRigidbodies == null || ragdoll.SpecialRigidbodies.Count() == 0)
                {
                    Library_ExiledAPI.LogWarn("ApplyStandardRagdollPhysics", "No rigidbodies found on ragdoll.");
                    return;
                }

                try
                {
                    rb.linearVelocity = upwardForce + randomForce;
                    rb.angularVelocity = UnityEngine.Random.insideUnitSphere * 6f;
                    Library_ExiledAPI.LogDebug("ApplyStandardRagdollPhysics", $"Applying forces to {rb.name} - Upward: {upwardForce}, Random: {randomForce}");
                }
                catch (Exception ex)
                {
                    Library_ExiledAPI.LogError("ApplyStandardRagdollPhysics", $"Failed to apply Exiled ragdoll physics: {ex.Message}");
                }
            }

        }

        public static void ConvertToBones(Exiled.API.Features.Ragdoll ragdoll)
        {
            try
            {
                // Convert to bones
                if (ragdoll.Base.TryGetComponent<DynamicRagdoll>(out var dr))
                {
                    Scp3114RagdollToBonesConverter.ConvertExisting(dr);
                }
            }
            catch (Exception ex)
            {
                Library_ExiledAPI.LogError("ConvertToBones", $"Failed to convert Exiled ragdoll to bones: {ex.Message}");
            }
        }

        #endregion

        #region Utility Methods  

        public static IEnumerator<float> DropAndPushItems(
            LabApi.Features.Wrappers.Player player
        )
        {
            Library_ExiledAPI.LogDebug("OnPlayerDying", $"Dropping all items from {player.Nickname}'s inventory called by Server.");
            List<LabApi.Features.Wrappers.Pickup> droppedPickups = player.DropAllItems();

            yield return Timing.WaitForOneFrame;  // let engine spawn pickups

            foreach (var pickup in droppedPickups)
            {
                if (pickup?.Rigidbody == null)
                {
                    Library_ExiledAPI.LogWarn("DropAndPushItems", $"Invalid pickup or missing Rigidbody - skipping.");
                    continue;
                }

                var rb = pickup.Rigidbody;
                var dir = GetRandomUnitSphereVelocity();
                var mag = CalculateForcePush();

                yield return Timing.WaitForOneFrame; // ensure physics engine is ready

                try
                {
                    rb.linearVelocity = dir * mag;
                    rb.angularVelocity = UnityEngine.Random.insideUnitSphere * Library_LabAPI.NpcConfig.KeterDamageVelocityModifier;
                    Library_ExiledAPI.LogDebug("DropAndPushItems", $"Pushed item {pickup.Serial} with velocity {dir * mag}.");
                }
                catch (Exception ex)
                {
                    Library_ExiledAPI.LogError("DropAndPushItems", $"Error pushing item {pickup.Serial}:{pickup.Base.name}: {ex}");
                }

                yield return Timing.WaitForOneFrame;  // stagger pushes
            }
        }

        public static bool IsScp575Damage(DamageHandlerBase handler)
        {
            return handler is CustomReasonDamageHandler customHandler &&
                   customHandler.DeathScreenText == DeathScreenText;
        }

        public static bool IsScp575BodyRagdoll(DamageHandlerBase handler)
        {
            return handler is CustomReasonDamageHandler customHandler &&
                   customHandler.RagdollInspectText == RagdollInspectText;
        }

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
                           Mathf.Log(Library_LabAPI.NpcConfig.KeterDamageVelocityModifier * Library_LabAPI.NpcConfig.KeterDamage + 1) *
                           CalculateForcePush(Library_LabAPI.NpcConfig.KeterDamageVelocityModifier);

            return randomDirection * modifier;
        }

        #endregion
    }

}
