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

        public static string IdentifierName => nameof(Scp575DamageSystem);

        public static readonly IReadOnlyDictionary<HitboxType, float> HitboxDamageMultipliers = new Dictionary<HitboxType, float>
        {
            [HitboxType.Body] = 1.0f,
            [HitboxType.Headshot] = 1.85f,
            [HitboxType.Limb] = 0.75f
        };

        #endregion

        #region Properties  

        public static float DamagePenetration => Plugin.Singleton.Config.NpcConfig.KeterDamagePenetration;
        public static string DeathScreenText => Plugin.Singleton.Config.HintsConfig.KilledByMessage;
        public static string RagdollInspectText => Plugin.Singleton.Config.HintsConfig.RagdollInspectText;

        #endregion

        #region Damage Processing 

        public static bool DamagePlayer(LabApi.Features.Wrappers.Player target, float damage, HitboxType hitbox = HitboxType.Body)
        {
            if (target?.ReferenceHub == null) return false;
            if (damage < 0f) return false;

            float processedDamage = DamageProcessor(target, damage, hitbox);
            return target.Damage(processedDamage, DeathScreenText);
        }

        private static float DamageProcessor(LabApi.Features.Wrappers.Player target, float damage, HitboxType hitbox)
        {
            if (damage <= 0f) return damage;

            float processedDamage = damage;
            if (HitboxDamageMultipliers.TryGetValue(hitbox, out var damageMul))
            {
                processedDamage *= damageMul;
            }

            processedDamage = ProcessArmorInteraction(target, processedDamage, hitbox);
            return processedDamage;
        }

        private static float ProcessArmorInteraction(LabApi.Features.Wrappers.Player target, float damage, HitboxType hitbox)
        {
            if (damage <= 0f || target.ReferenceHub == null) return damage;

            if (target.RoleBase is not IArmoredRole armoredRole)
                return damage;

            int armorEfficacy = armoredRole.GetArmorEfficacy(hitbox);
            int penetrationPercent = Mathf.RoundToInt(DamagePenetration * 100f);

            // FIXED: Added safe null-check for HumeShieldStat module to prevent critical NREs on human roles.
            float humeShield = 0f;
            if (target.ReferenceHub.playerStats.TryGetModule<HumeShieldStat>(out var shieldStat))
            {
                humeShield = shieldStat.CurValue;
            }

            float shieldDamage = Mathf.Clamp(humeShield, 0f, damage);
            float armorDamage = Mathf.Max(0f, damage - shieldDamage);
            float postArmorDamage = BodyArmorUtils.ProcessDamage(armorEfficacy, armorDamage, penetrationPercent);

            return shieldDamage + postArmorDamage;
        }

        #endregion

        #region Ragdoll Processing  

        private static readonly NorthwoodLib.Pools.ListPool<Rigidbody> RigidbodyPool = NorthwoodLib.Pools.ListPool<Rigidbody>.Shared;

        public static void RagdollProcessor(Player player, Ragdoll ragdoll)
        {
            if (ragdoll == null || player == null || !player.IsReady) return;

            try
            {
                Ragdoll newRagdoll = ReplaceRagdoll(player, ragdoll);
                if (newRagdoll == null) return;

                Timing.RunCoroutine(ProcessRagdollPhysics(newRagdoll), "SCP575-RagdollPhys");
            }
            catch (Exception ex)
            {
                LibraryLabAPI.LogError(nameof(RagdollProcessor), $"Critical ragdoll error: {ex.Message}");
            }
        }

        private static IEnumerator<float> ProcessRagdollPhysics(Ragdoll ragdoll)
        {
            if (ragdoll?.Base?.gameObject == null) yield break;

            // Wait for frames to allow Unity to fully initialize the physics bones layout.
            yield return Timing.WaitForOneFrame;
            yield return Timing.WaitForOneFrame;

            List<Rigidbody> rigidbodies = RigidbodyPool.Rent();
            try
            {
                Rigidbody[] ragdollRigidbodies = ragdoll.Base.GetComponentsInChildren<Rigidbody>();
                if (ragdollRigidbodies == null || ragdollRigidbodies.Length == 0) yield break;

                rigidbodies.AddRange(ragdollRigidbodies);

                Vector3 upwardForce = Vector3.up * CalculateForcePush(6.69f);
                Vector3 randomForce = GetRandomUnitSphereVelocity(4.75f);

                ApplyStandardRagdollPhysics(rigidbodies, upwardForce, randomForce);

                // FIXED: Embedded the dormant ConvertToBones method directly into the logic lifecycle here.
                ConvertToBones(ragdoll);
            }
            finally
            {
                RigidbodyPool.Return(rigidbodies);
            }
        }

        private static Ragdoll ReplaceRagdoll(Player player, Ragdoll originalRagdoll)
        {
            if (player == null || originalRagdoll?.Base == null) return null;

            try
            {
                Vector3 spawnPosition = originalRagdoll.Position;
                Quaternion spawnRotation = originalRagdoll.Rotation;

                var customHandler = new CustomReasonDamageHandler(RagdollInspectText, 0.0f, "");

                Ragdoll newRagdoll = Ragdoll.SpawnRagdoll(
                    RoleTypeId.Scp3114,
                    spawnPosition,
                    spawnRotation,
                    customHandler,
                    player.Nickname);

                if (newRagdoll?.Base != null)
                {
                    var basicRagdoll = newRagdoll.Base;
                    var oldInfo = basicRagdoll.NetworkInfo;

                    var newInfo = new PlayerRoles.Ragdolls.RagdollData(
                        oldInfo.OwnerHub,
                        oldInfo.Handler,
                        oldInfo.RoleType,
                        oldInfo.StartRelativePosition,
                        oldInfo.StartRelativeRotation,
                        oldInfo.Scale,
                        oldInfo.Nickname,
                        oldInfo.CreationTime,
                        oldInfo.Serial
                    );

                    basicRagdoll.NetworkInfo = newInfo;
                }

                originalRagdoll.Destroy();
                return newRagdoll;
            }
            catch (Exception ex)
            {
                LibraryLabAPI.LogError(nameof(ReplaceRagdoll), $"Failed to replace ragdoll: {ex.Message}");
                return null;
            }
        }

        private static void ApplyStandardRagdollPhysics(List<Rigidbody> rigidbodies, Vector3 upwardForce, Vector3 randomForce)
        {
            if (rigidbodies == null || rigidbodies.Count == 0) return;

            Vector3 combinedForce = upwardForce + randomForce;

            foreach (Rigidbody rb in rigidbodies)
            {
                if (rb == null) continue;

                rb.isKinematic = false;

                // FIXED: Removed manual mass multiplier to avoid extreme explosive launches caused by double-mass scaling in Impulse mode.
                rb.AddForce(combinedForce, ForceMode.Impulse);
                rb.angularVelocity = UnityEngine.Random.insideUnitSphere * Plugin.Singleton.Config.NpcConfig.KeterDamageVelocityModifier;
            }
        }

        private static void ConvertToBones(Ragdoll ragdoll)
        {
            if (ragdoll?.Base == null) return;

            try
            {
                if (IsDynamicRagdoll(ragdoll) && ragdoll.Base.TryGetComponent<DynamicRagdoll>(out var dr))
                {
                    Scp3114RagdollToBonesConverter.ConvertExisting(dr);
                }
            }
            catch (Exception ex)
            {
                LibraryLabAPI.LogError("ConvertToBones", $"Failed to convert ragdoll to bones: {ex.Message}");
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
            if (player == null || !player.IsReady || player.IsHost) yield break;

            const int maxWaitFrames = 6;
            int waitFrames = 0;
            List<Pickup> droppedPickups;

            try
            {
                droppedPickups = player.DropAllItems();
            }
            catch (Exception ex)
            {
                LibraryLabAPI.LogError(nameof(DropAndPushItems), $"Failed to drop items: {ex.Message}");
                yield break;
            }

            while (player.Inventory.UserInventory.Items.Count > 0 && waitFrames++ < maxWaitFrames)
            {
                yield return Timing.WaitForOneFrame;
            }

            // FIXED: Moved the WaitForOneFrame outside the loop to execute the physics impulse for all items instantly in a single frame.
            foreach (Pickup pickup in droppedPickups)
            {
                if (pickup?.Rigidbody == null || pickup.IsDestroyed || !pickup.IsSpawned) continue;

                try
                {
                    pickup.Rigidbody.isKinematic = false;

                    var direction = GetRandomUnitSphereVelocity(5.75f);
                    var magnitude = CalculateForcePush(7.35f);

                    pickup.Rigidbody.linearVelocity = direction * magnitude;
                    pickup.Rigidbody.angularVelocity = UnityEngine.Random.insideUnitSphere * Plugin.Singleton.Config.NpcConfig.KeterDamageVelocityModifier;
                }
                catch (Exception ex)
                {
                    LibraryLabAPI.LogError(nameof(DropAndPushItems), $"Failed to apply physics to item {pickup.Serial}: {ex.Message}");
                }
            }

            yield return Timing.WaitForOneFrame;
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

        private static float CalculateForcePush(float baseValue = 1.0f)
        {
            float randomFactor = UnityEngine.Random.Range(
                Plugin.Singleton.Config.NpcConfig.KeterForceMinModifier,
                Plugin.Singleton.Config.NpcConfig.KeterForceMaxModifier);

            return baseValue * randomFactor;
        }

        private static Vector3 GetRandomUnitSphereVelocity(float baseVelocityValue = 1.0f)
        {
            Vector3 randomDirection = UnityEngine.Random.onUnitSphere;

            if (Vector3.Dot(randomDirection, Vector3.down) > 0.707f)
            {
                randomDirection = Vector3.Reflect(randomDirection, Vector3.up);
            }

            float modifier = baseVelocityValue *
                           Mathf.Log(Plugin.Singleton.Config.NpcConfig.KeterDamageVelocityModifier) *
                           CalculateForcePush(Plugin.Singleton.Config.NpcConfig.KeterDamageVelocityModifier);

            return randomDirection * modifier;
        }

        #endregion
    }
}