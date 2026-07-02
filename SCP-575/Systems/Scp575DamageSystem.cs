namespace SCP_575.Shared
{
    using InventorySystem.Items.Armor;
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

    public class Scp575DamageSystem
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

        private readonly Plugin _plugin;

        public Scp575DamageSystem(Plugin plugin)
        {
            _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
        }

        public float DamagePenetration => _plugin.Npc.KeterDamagePenetration;
        public string DeathScreenText => _plugin.Hints.KilledByMessage;
        public string RagdollInspectText => _plugin.Hints.RagdollInspectText;

        #endregion

        #region Damage Processing 

        /// <summary>
        /// Inflicts processed damage onto a target player with custom death screen indicators.
        /// </summary>
        public bool DamagePlayer(LabApi.Features.Wrappers.Player target, float damage, HitboxType hitbox = HitboxType.Body)
        {
            if (target?.ReferenceHub == null) return false;
            if (_plugin?.SanityEventHandler != null && _plugin.SanityEventHandler.IsProtectedByPainkillers(target)) return false;
            if (damage < 0f) return false;


            float processedDamage = DamageProcessor(target, damage, hitbox);
            return target.Damage(processedDamage, DeathScreenText);
        }

        private float DamageProcessor(LabApi.Features.Wrappers.Player target, float damage, HitboxType hitbox)
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

        private float ProcessArmorInteraction(LabApi.Features.Wrappers.Player target, float damage, HitboxType hitbox)
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

        /// <summary>
        /// Processes non-lethal anomalous attacks by decreasing sanity and executing anti-spam soundscapes.
        /// </summary>
        public void ProcessAnomalousTrauma(LabApi.Features.Wrappers.Player player, ref DateTime lastAttackAudioTime, TimeSpan cooldown)
        {
            if (player == null || _plugin == null) return;
            if (_plugin.SanityEventHandler != null && _plugin.SanityEventHandler.IsProtectedByPainkillers(player)) return;

            // 1. Process Sanity Reduction & Consequences
            float dropAmount = _plugin.Sanity.ScpHitSanityDrop;
            if (dropAmount > 0f)
            {
                float newSanity = _plugin.SanityEventHandler.ChangeSanityValue(player, -dropAmount);
                LibraryLabAPI.LogDebug(IdentifierName, $"Anomalous trauma inflicted on {player.Nickname}. Sanity slashed by {dropAmount}. New sanity: {newSanity}");
            }
            _plugin.SanityEventHandler.ApplyStageEffects(player, bypassBlackoutGate: true, forceIgnoreCooldown: true);

            // 2. Play Mild/Lighter Hurt Audio Cues (With Rate Limiting)
            if (DateTime.UtcNow - lastAttackAudioTime >= cooldown)
            {
                lastAttackAudioTime = DateTime.UtcNow;
                _plugin.AudioManager.PlayAtPosition(AudioKey.AnomalousImpact, player.Position);
            }
        }

        /// <summary>
        /// Handles the definitive post-mortem execution logic, managing audio stingers and item scatters.
        /// </summary>
        public void ProcessLethalStrike(LabApi.Features.Wrappers.Player player)
        {
            if (player == null || _plugin == null) return;

            LibraryLabAPI.LogDebug(IdentifierName, $"Death confirmed from {IdentifierName} for {player.Nickname}. Triggering item physics and lethal soundscape.");

            // ShadowStrike is strictly reserved for lethal impact synchronization
            _plugin.AudioManager.PlayAtPosition(AudioKey.ShadowStrike, player.Position);

            // Offload item kinetic scatter calculations to an isolated coroutine to prevent main-thread choking
            Timing.RunCoroutine(DropAndPushItems(player), CoroutineTags.ItemPhysics);
        }

        #endregion

        #region Ragdoll Processing  

        private readonly NorthwoodLib.Pools.ListPool<Rigidbody> RigidbodyPool = NorthwoodLib.Pools.ListPool<Rigidbody>.Shared;

        public void RagdollProcessor(Player player, Ragdoll ragdoll)
        {
            if (ragdoll == null || player == null || !player.IsReady) return;

            try
            {
                Timing.RunCoroutine(ProcessRagdollPhysics(ragdoll, player, player.Role), CoroutineTags.RagdollPhysics);

            }
            catch (Exception ex)
            {
                LibraryLabAPI.LogError(nameof(RagdollProcessor), $"Critical ragdoll error: {ex.Message}");
            }
        }

        private IEnumerator<float> ProcessRagdollPhysics(Ragdoll ragdoll, Player player, RoleTypeId oldRole)
        {
            if (ragdoll?.Base?.gameObject == null) yield break;

            yield return Timing.WaitForSeconds(0.11f);

            Rigidbody[] ragdollRigidbodies = ragdoll.Base.GetComponentsInChildren<Rigidbody>();
            if (ragdollRigidbodies == null || ragdollRigidbodies.Length == 0) yield break;

            List<Rigidbody> rigidbodies = RigidbodyPool.Rent();
            try
            {
                rigidbodies.AddRange(ragdollRigidbodies);

                Vector3 upwardForce = Vector3.up * CalculateForcePush(7.45f);
                ApplyStandardRagdollPhysics(rigidbodies, upwardForce, 12.75f);
            }
            finally
            {
                RigidbodyPool.Return(rigidbodies);
            }

            yield return Timing.WaitForSeconds(0.175f);

            if (player != null && player.IsReady)
            {
                Ragdoll newRagdoll = ReplaceRagdoll(player, ragdoll, oldRole);
            }
        }

        private Ragdoll ReplaceRagdoll(Player player, Ragdoll originalRagdoll, RoleTypeId oldRole)
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
                LibraryLabAPI.LogError(nameof(ReplaceRagdoll), $"Failed to replace ragdoll: {ex.Message}");
                return null;
            }
        }

        private void ApplyStandardRagdollPhysics(List<Rigidbody> rigidbodies, Vector3 upwardForce, float randomForceMagnitude)
        {
            if (rigidbodies == null || rigidbodies.Count == 0) return;

            foreach (Rigidbody rb in rigidbodies)
            {
                if (rb == null) continue;
                rb.isKinematic = false;

                Vector3 uniqueRandomForce = GetRandomUnitSphereVelocity(randomForceMagnitude);

                float randomUpweight = UnityEngine.Random.Range(0.8f, 1.3f);
                Vector3 randomizedUpward = upwardForce * randomUpweight;

                Vector3 combinedForce = randomizedUpward + uniqueRandomForce;

                rb.AddForce(combinedForce, ForceMode.Impulse);

                float torqueModifier = _plugin.Npc.KeterDamageVelocityModifier;
                rb.AddTorque(UnityEngine.Random.insideUnitSphere * torqueModifier, ForceMode.Impulse);
            }
        }

        private void ConvertToBones(Ragdoll ragdoll)
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

        private bool IsDynamicRagdoll(Ragdoll ragdoll)
        {
            return ragdoll?.Base?.TryGetComponent<DynamicRagdoll>(out _) == true;
        }

        public IEnumerator<float> DropAndPushItems(Player player)
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

            if (droppedPickups == null || droppedPickups.Count == 0) yield break;

            while (player.Inventory.UserInventory.Items.Count > 0 && waitFrames++ < maxWaitFrames)
            {
                yield return Timing.WaitForOneFrame;
            }

            float configModifier = _plugin.Npc.KeterDamageVelocityModifier;

            float internalSharedModifier = 1.45f * Mathf.Log(configModifier) * CalculateForcePush(configModifier);
            float forcePushMagnitude = CalculateForcePush(1.35f);

            float finalLinearMagnitude = internalSharedModifier * forcePushMagnitude;

            int pickupCount = droppedPickups.Count;

            for (int i = 0; i < pickupCount; i++)
            {
                Pickup pickup = droppedPickups[i];

                if (pickup == null || pickup.IsDestroyed || !pickup.IsSpawned) continue;

                Rigidbody rb = pickup.Rigidbody;
                if (rb == null) continue;

                try
                {
                    rb.isKinematic = false;

                    Vector3 randomDirection = UnityEngine.Random.onUnitSphere;
                    if (Vector3.Dot(randomDirection, Vector3.down) > 0.707f)
                    {
                        randomDirection = Vector3.Reflect(randomDirection, Vector3.up);
                    }

                    rb.linearVelocity = randomDirection * finalLinearMagnitude;
                    rb.angularVelocity = UnityEngine.Random.insideUnitSphere * configModifier;
                }
                catch (Exception ex)
                {
                    LibraryLabAPI.LogError(nameof(DropAndPushItems), $"Failed to apply physics to item {pickup.Serial}: {ex.Message}");
                }
            }

            yield return Timing.WaitForOneFrame;
        }

        public bool IsScp575Damage(DamageHandlerBase handler)
        {
            return handler is CustomReasonDamageHandler customHandler &&
                   customHandler.DeathScreenText == DeathScreenText;
        }

        public bool IsScp575BodyRagdoll(DamageHandlerBase handler)
        {
            return handler is CustomReasonDamageHandler customHandler &&
                   customHandler.RagdollInspectText == RagdollInspectText;
        }

        private float CalculateForcePush(float baseValue = 1.0f)
        {
            float randomFactor = UnityEngine.Random.Range(
                _plugin.Npc.KeterForceMinModifier,
                _plugin.Npc.KeterForceMaxModifier);

            return baseValue * randomFactor;
        }

        private Vector3 GetRandomUnitSphereVelocity(float baseVelocityValue = 1.0f)
        {
            Vector3 randomDirection = UnityEngine.Random.onUnitSphere;

            if (Vector3.Dot(randomDirection, Vector3.down) > 0.707f)
            {
                randomDirection = Vector3.Reflect(randomDirection, Vector3.up);
            }

            float modifier = baseVelocityValue *
                           Mathf.Log(_plugin.Npc.KeterDamageVelocityModifier) *
                           CalculateForcePush(_plugin.Npc.KeterDamageVelocityModifier);

            return randomDirection * modifier;
        }

        #endregion
    }
}