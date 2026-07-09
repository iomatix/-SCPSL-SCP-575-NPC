using InventorySystem.Items.Armor;
using LabApi.Extensions;
using LabApi.Extensions.Misc;
using LabApi.Features.Wrappers;
using MEC;
using PlayerRoles;
using PlayerRoles.PlayableScps.Scp3114;
using PlayerRoles.Ragdolls;
using PlayerStatsSystem;
using SCP_575.Shared.Audio.Enums;
using System;
using System.Collections.Generic;
using UnityEngine;
using Logger = LabApi.Extensions.Misc.iLogger;

namespace SCP_575.Shared
{
    /// <summary>
    /// High-performance combat system orchestrating damage propagation, armor mitigation, and custom ragdoll kinematics.
    /// </summary>
    public class Scp575DamageSystem
    {
        #region Constants & Registries
        public static string IdentifierName => nameof(Scp575DamageSystem);

        public static readonly IReadOnlyDictionary<HitboxType, float> HitboxDamageMultipliers = new Dictionary<HitboxType, float>
        {
            [HitboxType.Body] = 1.0f,
            [HitboxType.Headshot] = 1.85f,
            [HitboxType.Limb] = 0.75f
        };

        private readonly Plugin _plugin;
        private readonly NorthwoodLib.Pools.ListPool<Rigidbody> _rigidbodyPool = NorthwoodLib.Pools.ListPool<Rigidbody>.Shared;
        #endregion

        #region Properties
        public float DamagePenetration => _plugin.Npc.KeterDamagePenetration;
        public string DeathScreenText => _plugin.Hints.KilledByMessage;
        public string RagdollInspectText => _plugin.Hints.RagdollInspectText;
        #endregion

        #region Constructor
        public Scp575DamageSystem(Plugin plugin)
        {
            _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
        }
        #endregion

        #region Public Damage Channels
        /// <summary>
        /// Applies calculated structural damage to a target player with specialized death notifications.
        /// </summary>
        public bool DamagePlayer(Player target, float damage, HitboxType hitbox = HitboxType.Body)
        {
            if (target?.ReferenceHub is null || damage < 0f) return false;
            if (_plugin.SanityHandler is not null && _plugin.SanityHandler.IsProtectedByPainkillers(target)) return false;

            float processedDamage = DamageProcessor(target, damage, hitbox);
            if (processedDamage <= 0f) return false;

            _plugin.AudioDirector?.ProcessDamagedPlayerHitImpact(target);
            return target.Damage(processedDamage, DeathScreenText);
        }

        /// <summary>
        /// Inflicts non-lethal psychological trauma, reducing sanity metrics and applying rate-limited sound effects.
        /// </summary>
        public void ProcessAnomalousTrauma(Player player)
        {
            if (player is null || _plugin.SanityHandler is null) return;
            if (_plugin.SanityHandler.IsProtectedByPainkillers(player)) return;

            float dropAmount = _plugin.Sanity.ScpHitSanityDrop;
            if (dropAmount > 0f)
            {
                float newSanity = _plugin.SanityHandler.ChangeSanityValue(player, -dropAmount);
                Logger.Debug(IdentifierName, $"Anomalous trauma inflicted on {player.Nickname}. Sanity reduced by {dropAmount}. Current: {newSanity}", _plugin.Debug);
            }

            _plugin.SanityHandler.ApplyStageEffects(player, bypassBlackoutGate: true, forceIgnoreCooldown: true);
        }

        /// <summary>
        /// Commits post-mortem execution tracking, executing inventory drop physics and global stingers.
        /// </summary>
        public void ProcessLethalStrike(Player player)
        {
            if (player is null) return;

            Logger.Debug(IdentifierName, $"Lethal impact verified for {player.Nickname}. Dispatching kinetic pipeline sweeps.", _plugin.Debug);

            _plugin.AudioManager?.PlayAtPosition(AudioKey.ShadowStrike, player.Position);
            Timing.RunCoroutine(DropAndPushItems(player), CoroutineTags.ItemPhysics);
        }
        #endregion

        #region Internal Processing Pipelines
        private float DamageProcessor(Player target, float damage, HitboxType hitbox)
        {
            if (damage <= 0f) return damage;

            float processedDamage = damage;
            if (HitboxDamageMultipliers.TryGetValue(hitbox, out float damageMul))
            {
                processedDamage *= damageMul;
            }

            return ProcessArmorInteraction(target, processedDamage, hitbox);
        }

        private float ProcessArmorInteraction(Player target, float damage, HitboxType hitbox)
        {
            if (damage <= 0f || target.ReferenceHub is null) return damage;
            if (target.RoleBase is not IArmoredRole armoredRole) return damage;

            int armorEfficacy = armoredRole.GetArmorEfficacy(hitbox);
            int penetrationPercent = (DamagePenetration * 100f).Clamp(0f, 100f).RoundToInt();

            float humeShield = target.GetHumeShieldValue();

            float shieldDamage = humeShield.Clamp(0f, damage);
            float armorDamage = (damage - shieldDamage).LimitMin(0f);
            float postArmorDamage = BodyArmorUtils.ProcessDamage(armorEfficacy, armorDamage, penetrationPercent);

            return shieldDamage + postArmorDamage;
        }
        #endregion

        #region Ragdoll Physics & Conversions
        /// <summary>
        /// Processes skeletal physics propulsion and schedules post-mortem model replacements.
        /// </summary>
        public void RagdollProcessor(Player player, Ragdoll ragdoll)
        {
            if (ragdoll is null || player is null || !player.IsReady) return;

            try
            {
                Timing.RunCoroutine(ProcessRagdollPhysics(ragdoll, player, player.Role), CoroutineTags.RagdollPhysics);
            }
            catch (Exception ex)
            {
                Logger.Error(nameof(RagdollProcessor), $"Critical ragdoll simulation pipeline failure: {ex.Message}");
            }
        }

        private IEnumerator<float> ProcessRagdollPhysics(Ragdoll ragdoll, Player player, RoleTypeId oldRole)
        {
            if (ragdoll?.Base?.gameObject is null) yield break;

            yield return Timing.WaitForSeconds(0.11f);

            List<Rigidbody> rigidbodies = _rigidbodyPool.Rent();
            bool hasComponents = false;

            try
            {
                // Zero-allocation: Populating rented list context directly via Unity native API internal routing
                ragdoll.Base.GetComponentsInChildren<Rigidbody>(rigidbodies);
                hasComponents = rigidbodies.Count > 0;

                if (hasComponents)
                {
                    Vector3 upwardForce = Vector3.up * CalculateForcePush(7.45f);
                    ApplyStandardRagdollPhysics(rigidbodies, upwardForce, 12.75f);
                }
            }
            finally
            {
                // Guarantees pool recycling integrity before coroutine execution sequence jumps boundaries
                _rigidbodyPool.Return(rigidbodies);
            }

            if (!hasComponents) yield break;

            yield return Timing.WaitForSeconds(0.175f);

            if (player is not null && player.IsReady)
            {
                ReplaceRagdoll(player, ragdoll, oldRole);
            }
        }

        private Ragdoll ReplaceRagdoll(Player player, Ragdoll originalRagdoll, RoleTypeId oldRole)
        {
            if (player is null || originalRagdoll?.Base is null) return null;

            try
            {
                Vector3 spawnPosition = originalRagdoll.Position;
                Quaternion spawnRotation = originalRagdoll.Rotation;

                CustomReasonDamageHandler customHandler = new(RagdollInspectText, 0.0f, "");

                Ragdoll newRagdoll = Ragdoll.SpawnRagdoll(
                    RoleTypeId.Scp3114,
                    spawnPosition,
                    spawnRotation,
                    customHandler,
                    player.Nickname);

                if (newRagdoll?.Base is not null)
                {
                    BasicRagdoll basicRagdoll = newRagdoll.Base;
                    RagdollData oldInfo = basicRagdoll.NetworkInfo;

                    RagdollData newInfo = new(
                        oldInfo.OwnerHub,
                        oldInfo.Handler,
                        oldRole,
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
                Logger.Error(nameof(ReplaceRagdoll), $"Skeletal entity identity override structure collapsed: {ex.Message}");
                return null;
            }
        }

        private void ApplyStandardRagdollPhysics(List<Rigidbody> rigidbodies, Vector3 upwardForce, float randomForceMagnitude)
        {
            if (rigidbodies is null || rigidbodies.Count == 0) return;

            float torqueModifier = _plugin.Npc.KeterDamageVelocityModifier;

            for (int i = 0; i < rigidbodies.Count; i++)
            {
                Rigidbody rb = rigidbodies[i];
                if (rb is null) continue;
                rb.isKinematic = false;

                Vector3 uniqueRandomForce = GetRandomUnitSphereVelocity(randomForceMagnitude);
                float randomUpweight = SafeRandom.Range(0.8f, 1.3f);
                Vector3 combinedForce = (upwardForce * randomUpweight) + uniqueRandomForce;

                rb.AddForce(combinedForce, ForceMode.Impulse);
                rb.AddTorque(UnityEngine.Random.insideUnitSphere * torqueModifier, ForceMode.Impulse);
            }
        }

        private void ConvertToBones(Ragdoll ragdoll)
        {
            if (ragdoll?.Base is null) return;

            try
            {
                if (IsDynamicRagdoll(ragdoll) && ragdoll.Base.TryGetComponent(out DynamicRagdoll dr))
                {
                    Scp3114RagdollToBonesConverter.ConvertExisting(dr);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(nameof(ConvertToBones), $"Skeletal destruction script conversion exception: {ex.Message}");
            }
        }
        #endregion

        #region Kinetic Drops & Utilities
        /// <summary>
        /// Drops all inventory assets from the target player, applying batch vector forces.
        /// </summary>
        public IEnumerator<float> DropAndPushItems(Player player)
        {
            if (player is null || !player.IsReady || player.IsHost) yield break;

            int waitFrames = 0;
            List<Pickup> droppedPickups;

            try
            {
                droppedPickups = player.DropAllItems();
            }
            catch (Exception ex)
            {
                Logger.Error(nameof(DropAndPushItems), $"Inventory eviction routine execution failure: {ex.Message}");
                yield break;
            }

            if (droppedPickups is null || droppedPickups.Count == 0) yield break;

            while (player.Inventory.UserInventory.Items.Count > 0 && waitFrames++ < 6)
            {
                yield return Timing.WaitForOneFrame;
            }

            float configModifier = _plugin.Npc.KeterDamageVelocityModifier;
            float internalSharedModifier = 1.45f * Mathf.Log(configModifier) * CalculateForcePush(configModifier);
            float forcePushMagnitude = CalculateForcePush(1.35f);
            float finalLinearMagnitude = internalSharedModifier * forcePushMagnitude;

            droppedPickups.ApplyKineticBlast(finalLinearMagnitude, configModifier);

            yield return Timing.WaitForOneFrame;
        }

        public bool IsScp575Damage(DamageHandlerBase handler) =>
            handler is CustomReasonDamageHandler customHandler && customHandler.DeathScreenText == DeathScreenText;

        public bool IsScp575BodyRagdoll(DamageHandlerBase handler) =>
            handler is CustomReasonDamageHandler customHandler && customHandler.RagdollInspectText == RagdollInspectText;

        private bool IsDynamicRagdoll(Ragdoll ragdoll) =>
            ragdoll?.Base?.TryGetComponent<DynamicRagdoll>(out _) == true;

        private float CalculateForcePush(float baseValue = 1.0f) =>
            baseValue * SafeRandom.Range(_plugin.Npc.KeterForceMinModifier, _plugin.Npc.KeterForceMaxModifier);

        private Vector3 GetRandomUnitSphereVelocity(float baseVelocityValue = 1.0f)
        {
            float modifier = baseVelocityValue *
                             Mathf.Log(_plugin.Npc.KeterDamageVelocityModifier) *
                             CalculateForcePush(_plugin.Npc.KeterDamageVelocityModifier);

            return VectorExtensions.GetRandomUpwardSphereVelocity(modifier);
        }
        #endregion
    }
}