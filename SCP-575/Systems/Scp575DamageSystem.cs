namespace SCP_575.Systems
{
    using InventorySystem.Items.Armor;
    using LabApi.Features.Wrappers;
    using MEC;
    using PlayerRoles;
    using PlayerRoles.PlayableScps.Scp3114;
    using PlayerRoles.Ragdolls;
    using PlayerStatsSystem;
    using SCP_575.Shared;
    using System;
    using System.Collections.Generic;
    using System.Linq;
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
        /// Gets the DamagePenetration factor for further damage processing.
        /// </summary>  
        public static float DamagePenetration => Plugin.Singleton.Config.NpcConfig.KeterDamagePenetration;

        /// <summary>  
        /// Gets the Reason text displayed on the player's death screen.
        /// </summary>  
        public static string DeathScreenText => Plugin.Singleton.Config.HintsConfig.KilledByMessage;

        /// <summary>  
        /// Gets the text displayed when inspecting a ragdoll killed by this handler.
        /// </summary>  
        public static string RagdollInspectText => Plugin.Singleton.Config.HintsConfig.RagdollInspectText;

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

            try
            {
                if (target.ReferenceHub == null)
                {
                    LibraryExiledAPI.LogWarn(nameof(DamagePlayer), $"ReferenceHub is null for player {target.UserId} ({target.Nickname ?? "null"})");
                    return false;
                }

                LibraryExiledAPI.LogDebug(nameof(DamagePlayer),
                    $"Processing damage for {target.Nickname ?? "null"} with Hitbox: {hitbox} and Damage: {damage:F1}");

                float processedDamage = DamageProcessor(target, damage, hitbox);

                bool succeeded = target.Damage(processedDamage, DeathScreenText);

                if (succeeded)
                {
                    LibraryExiledAPI.LogDebug(nameof(DamagePlayer),
                        target.IsAlive ? "Damage applied successfully - player survived" : "Damage applied successfully - player died");
                }
                else
                {
                    LibraryExiledAPI.LogDebug(nameof(DamagePlayer), "Failed to apply damage");
                }

                return succeeded;
            }
            catch (Exception ex)
            {
                LibraryExiledAPI.LogError(nameof(DamagePlayer),
                    $"Failed to process damage for {target.UserId} ({target.Nickname ?? "null"}): {ex.Message}, StackTrace: {ex.StackTrace}");
                return false;
            }
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

            try
            {
                LibraryExiledAPI.LogDebug(nameof(DamageProcessor),
                    $"Processing damage for {target.Nickname ?? "null"} with Hitbox: {hitbox} and Damage: {damage:F1}");

                float processedDamage = damage; // Initialize with base damage  

                // Apply hitbox-specific damage multipliers    
                if (HitboxDamageMultipliers.TryGetValue(hitbox, out var damageMul))
                {
                    processedDamage *= damageMul;
                    LibraryExiledAPI.LogDebug(nameof(DamageProcessor),
                        $"Applied hitbox multiplier {damageMul:F2} for {hitbox}. Damage: {damage:F1} -> {processedDamage:F1}");
                }

                // Handle armor interactions for armored roles - use the multiplied damage  
                processedDamage = ProcessArmorInteraction(target, processedDamage, hitbox);

                return processedDamage;
            }
            catch (Exception ex)
            {
                LibraryExiledAPI.LogError(nameof(DamageProcessor),
                    $"Failed to process damage for {target.UserId} ({target.Nickname ?? "null"}): {ex.Message}, StackTrace: {ex.StackTrace}");
                return damage;
            }
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
            if (damage <= 0f)
                return damage;

            try
            {
                if (target.RoleBase is not IArmoredRole armoredRole)
                {
                    LibraryExiledAPI.LogDebug(nameof(ProcessArmorInteraction),
                        $"Player {target.Nickname ?? "null"} has no armored role, returning damage: {damage:F1}");
                    return damage;
                }

                if (target.ReferenceHub == null)
                {
                    LibraryExiledAPI.LogWarn(nameof(ProcessArmorInteraction),
                        $"ReferenceHub is null for player {target.UserId} ({target.Nickname ?? "null"})");
                    return damage;
                }

                LibraryExiledAPI.LogDebug(nameof(ProcessArmorInteraction),
                    $"Player {target.Nickname ?? "null"} has armor role: {armoredRole.GetType().Name}");

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

                LibraryExiledAPI.LogDebug(nameof(ProcessArmorInteraction),
                    $"Armor calculation for {target.Nickname ?? "null"}: " +
                    $"Efficacy={armorEfficacy}, Penetration={penetrationPercent}%, " +
                    $"Shield={shieldDamage:F1}, Armor={armorDamage:F1}->{postArmorDamage:F1}, " +
                    $"Final={finalDamage:F1}");

                return finalDamage;
            }
            catch (Exception ex)
            {
                LibraryExiledAPI.LogError("ProcessArmorInteraction",
                    $"Failed to process armor for {target.UserId} ({target.Nickname ?? "null"}): {ex.Message}, StackTrace: {ex.StackTrace}");
                return damage;
            }
        }

        #endregion

        #region Ragdoll Processing  

        #region Pooling Infrastructure  
        /// <summary>  
        /// Shared pool for rigidbody arrays to reduce garbage collection during ragdoll physics processing.  
        /// </summary>  
        private static readonly NorthwoodLib.Pools.ListPool<Rigidbody> RigidbodyPool = NorthwoodLib.Pools.ListPool<Rigidbody>.Shared;
        #endregion

        /// <summary>
        /// Processes an SCP-575 ragdoll with visual effects and validation using LabAPI synchronization.
        /// </summary>
        /// <param name="ragdoll">The LabAPI ragdoll wrapper to process.</param>
        /// <exception cref="ArgumentNullException">Thrown when ragdoll or handler is null.</exception>
        public static void RagdollProcessor(Player player, Ragdoll ragdoll)
        {
            if (ragdoll == null)
            {
                LibraryExiledAPI.LogWarn(nameof(RagdollProcessor), "Aborted: Null ragdoll received");
                return;
            }

            if (player == null || !player.IsReady)
            {
                LibraryExiledAPI.LogWarn(nameof(RagdollProcessor),
                    $"Invalid player state for ragdoll at {ragdoll.Position}");
                return;
            }

            LibraryExiledAPI.LogDebug(nameof(RagdollProcessor), $"Processing SCP-575 ragdoll for {player.Nickname} at position: {ragdoll.Position}");
            try
            {
                Ragdoll newRagdoll = ReplaceRagdoll(player, ragdoll);
                if (newRagdoll == null)
                {
                    LibraryExiledAPI.LogError(nameof(RagdollProcessor), "Failed to create replacement ragdoll");
                    return;
                }

                // Apply physics in a coroutine to ensure proper frame timing
                Timing.RunCoroutine(ProcessRagdollPhysics(newRagdoll));
                LibraryExiledAPI.LogDebug(nameof(RagdollProcessor), $"SCP-575 ragdoll processing completed");
            }
            catch (Exception ex)
            {
                LibraryExiledAPI.LogError(nameof(RagdollProcessor), $"Critical ragdoll error: {ex.GetType().Name} - {ex.Message}\n{ex.StackTrace}");
            }
        }
        private static IEnumerator<float> ProcessRagdollPhysics(Ragdoll ragdoll)
        {
            if (ragdoll?.Base?.gameObject == null)
            {
                LibraryExiledAPI.LogWarn(nameof(ProcessRagdollPhysics), "Ragdoll destroyed before physics could be applied");
                yield break;
            }

            // Wait for ragdoll to be fully initialized    
            yield return Timing.WaitForOneFrame;
            yield return Timing.WaitForOneFrame;

            // Rent a list from the pool instead of creating array  
            List<Rigidbody> rigidbodies = RigidbodyPool.Rent();

            try
            {
                // Get all rigidbodies and add to pooled list  
                Rigidbody[] ragdollRigidbodies = ragdoll.Base.GetComponentsInChildren<Rigidbody>();
                if (ragdollRigidbodies == null || ragdollRigidbodies.Length == 0)
                {
                    LibraryExiledAPI.LogWarn(nameof(ProcessRagdollPhysics), "No rigidbodies found on ragdoll");
                    yield break;
                }

                rigidbodies.AddRange(ragdollRigidbodies);

                Vector3 upwardForce = Vector3.up * CalculateForcePush(6.69f);
                Vector3 randomForce = GetRandomUnitSphereVelocity(4.75f);

                ApplyStandardRagdollPhysics(rigidbodies, upwardForce, randomForce);
            }
            finally
            {
                // Always return the list to the pool
                RigidbodyPool.Return(rigidbodies);
            }
        }

        private static Ragdoll ReplaceRagdoll(Player player, Ragdoll originalRagdoll)
        {
            if (player == null || originalRagdoll == null)
                return null;

            try
            {
                LibraryExiledAPI.LogDebug(nameof(ReplaceRagdoll),
                    $"Replacing ragdoll for {player.Nickname ?? "null"} at position: {originalRagdoll.Position}");

                // Check ragdoll validity
                if (originalRagdoll.Base == null || !originalRagdoll.Base.isActiveAndEnabled)
                {
                    LibraryExiledAPI.LogWarn(nameof(ReplaceRagdoll), "Original ragdoll is invalid or not spawned");
                    return null;
                }

                // Store position with vertical offset to prevent floor clipping
                Vector3 spawnPosition = originalRagdoll.Position + Vector3.up * 0.15f;
                Quaternion spawnRotation = originalRagdoll.Rotation;

                // Prepare RagdollData
                var customHandler = new CustomReasonDamageHandler(RagdollInspectText, 0.0f, "");
                Ragdoll newRagdoll = Ragdoll.SpawnRagdoll(
                    RoleTypeId.Scp3114,
                    spawnPosition,
                    spawnRotation,
                    customHandler,
                    player.Nickname);

                // Only destroy original if new ragdoll was successfully created
                if (newRagdoll == null)
                {
                    LibraryExiledAPI.LogError(nameof(ReplaceRagdoll), "Ragdoll.SpawnRagdoll returned null");
                    return null;
                }

                originalRagdoll.Destroy();
                return newRagdoll;
            }
            catch (Exception ex)
            {
                LibraryExiledAPI.LogError(nameof(ReplaceRagdoll),
                    $"Failed to replace ragdoll for {player.UserId} ({player.Nickname ?? "null"}): {ex.Message}, StackTrace: {ex.StackTrace}");
                return null;
            }
        }

        private static void ApplyStandardRagdollPhysics(List<Rigidbody> rigidbodies, Vector3 upwardForce, Vector3 randomForce)
        {
            if (rigidbodies == null || rigidbodies.Count == 0)
            {
                LibraryExiledAPI.LogWarn(nameof(ApplyStandardRagdollPhysics), $"No rigidbodies found on ragdoll");
                return;
            }
            foreach (Rigidbody rb in rigidbodies)
            {
                if (rb == null) continue;

                try
                {
                    // Scale force based on rigidbody mass  
                    float massMultiplier = Mathf.Max(1f, rb.mass / 1f);
                    Vector3 scaledUpwardForce = upwardForce * massMultiplier;
                    Vector3 scaledRandomForce = randomForce * massMultiplier;

                    rb.linearVelocity = scaledUpwardForce + scaledRandomForce;
                    rb.angularVelocity = UnityEngine.Random.insideUnitSphere * Plugin.Singleton.Config.NpcConfig.KeterDamageVelocityModifier;
                }
                catch (Exception ex)
                {
                    LibraryExiledAPI.LogError(nameof(ApplyStandardRagdollPhysics),
                        $"Failed to apply physics to rigidbody {rb.name}: {ex.Message}");
                }
            }
        }

        private static void ConvertToBones(Ragdoll ragdoll)
        {
            if (ragdoll?.Base == null)
            {
                LibraryExiledAPI.LogWarn(nameof(ConvertToBones), "Ragdoll is null or destroyed - bone conversion aborted");
                return;
            }


            try
            {
                if (IsDynamicRagdoll(ragdoll) && ragdoll.Base.TryGetComponent<DynamicRagdoll>(out var dr))
                {
                    Scp3114RagdollToBonesConverter.ConvertExisting(dr);
                    LibraryExiledAPI.LogDebug(nameof(ConvertToBones), "Ragdoll successfully converted to bones");
                }
            }
            catch (Exception ex)
            {
                LibraryExiledAPI.LogError("ConvertToBones", $"Failed to convert ragdoll to bones: {ex.Message}, StackTrace: {ex.StackTrace}");
            }
        }

        #endregion

        #region Utility Methods  

        private static bool IsDynamicRagdoll(Ragdoll ragdoll)
        {
            return ragdoll?.Base?.TryGetComponent<DynamicRagdoll>(out _) == true;
        }

        public static IEnumerator<float> DropAndPushItems(Player player)
        {
            if (player == null || !player.IsReady || player.IsHost)
            {
                LibraryExiledAPI.LogDebug(nameof(DropAndPushItems), "Aborted: Invalid player state");
                yield break;
            }

            const int maxWaitFrames = 6;
            int waitFrames = 0;
            List<Pickup> droppedPickups;

            try
            {
                LibraryExiledAPI.LogDebug(nameof(DropAndPushItems), $"Dropping all items from {player.Nickname ?? "null"}'s inventory");
                droppedPickups = player.DropAllItems();
            }
            catch (Exception ex)
            {
                LibraryExiledAPI.LogError(nameof(DropAndPushItems), $"Failed to drop items: {ex.Message}");
                yield break;
            }

            // Wait until items are properly spawned  
            while (player.Inventory.UserInventory.Items.Count > 0 && waitFrames++ < maxWaitFrames)
            {
                yield return Timing.WaitForOneFrame;
            }

            // Apply physics to all valid pickups  
            foreach (Pickup pickup in droppedPickups)
            {
                if (pickup?.Rigidbody == null || pickup.IsDestroyed || !pickup.IsSpawned)
                {
                    LibraryExiledAPI.LogWarn(nameof(DropAndPushItems), $"Invalid pickup {pickup?.Serial ?? 0} - skipping");
                    continue;
                }

                try
                {
                    // Ensure rigidbody can receive physics  
                    pickup.Rigidbody.isKinematic = false;

                    var direction = GetRandomUnitSphereVelocity(5.75f);
                    var magnitude = CalculateForcePush(7.35f);

                    // Apply physics directly  
                    pickup.Rigidbody.linearVelocity = direction * magnitude;
                    pickup.Rigidbody.angularVelocity = UnityEngine.Random.insideUnitSphere * Plugin.Singleton.Config.NpcConfig.KeterDamageVelocityModifier;

                    LibraryExiledAPI.LogDebug(nameof(DropAndPushItems),
                        $"Applied physics to item {pickup.Serial} ({pickup.Type}) with velocity {direction * magnitude}");
                }
                catch (Exception ex)
                {
                    LibraryExiledAPI.LogError(nameof(DropAndPushItems),
                        $"Failed to apply physics to item {pickup.Serial} ({pickup.Type}): {ex.Message}");
                }

                // Stagger physics application  
                yield return Timing.WaitForOneFrame;
            }

            // Return the pooled list  
            NorthwoodLib.Pools.ListPool<Pickup>.Shared.Return(droppedPickups);
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
        private static float CalculateForcePush(float baseValue = 1.0f)
        {
            float randomFactor = UnityEngine.Random.Range(
                Plugin.Singleton.Config.NpcConfig.KeterForceMinModifier,
                Plugin.Singleton.Config.NpcConfig.KeterForceMaxModifier);

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
        private static Vector3 GetRandomUnitSphereVelocity(float baseVelocityValue = 1.0f)
        {
            Vector3 randomDirection = UnityEngine.Random.onUnitSphere;

            // Prevent downward vectors that could cause items to clip through floors  
            // If the vector points more than 45° downward, reflect it upward  
            if (Vector3.Dot(randomDirection, Vector3.down) > 0.707f) // cos(45°) ≈ 0.707  
            {
                LibraryExiledAPI.LogDebug("GetRandomUnitSphereVelocity", "Vector pointing downward, reflecting upward.");
                randomDirection = Vector3.Reflect(randomDirection, Vector3.up);
            }

            // Apply logarithmic scaling based on damage for realistic force distribution  
            float modifier = baseVelocityValue *
                           Mathf.Log(Plugin.Singleton.Config.NpcConfig.KeterDamageVelocityModifier) *
                           CalculateForcePush(Plugin.Singleton.Config.NpcConfig.KeterDamageVelocityModifier);

            return randomDirection * modifier;
        }

        #endregion
    }
}