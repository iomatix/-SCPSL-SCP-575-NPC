namespace SCP_575
{
    using CustomPlayerEffects;
    using Exiled.API.Features;
    using Footprinting;
    using InventorySystem.Items.Armor;
    using Mirror;
    using PlayerRoles;
    using PlayerRoles.PlayableScps.Scp3114;
    using PlayerRoles.Ragdolls;
    using PlayerStatsSystem;
    using SCP_575.ConfigObjects;
    using System.Collections.Generic;
    using UnityEngine;
    using Utils.Networking;


    public class Scp575DamageHandler : StandardDamageHandler, IRagdollInspectOverride
    {
        private static Plugin Instance => Plugin.Singleton;
        private NpcConfig Config => Instance.Config.NpcConfig;

        public sbyte _hitDirectionX, _hitDirectionZ;

        public Vector3 _velocity;

        public readonly float _penetration;

        public readonly string _deathReasonFormat;

        public readonly bool _useHumanHitboxes;


        public static readonly Dictionary<HitboxType, float> HitboxToForce = new Dictionary<HitboxType, float>
        {
            [HitboxType.Body] = 0.08f,
            [HitboxType.Headshot] = 0.08f,
            [HitboxType.Limb] = 0.016f
        };

        public static readonly Dictionary<HitboxType, float> HitboxDamageMultipliers = new Dictionary<HitboxType, float>
        {
            [HitboxType.Headshot] = 2f,
            [HitboxType.Limb] = 0.7f
        };

        // Death Translation
        // Unique byte that doesn't conflict with others
        private static readonly byte scp575Id = 31;
        public static readonly DeathTranslation scp575Translation = new DeathTranslation(scp575Id, 36, 36, "{0}");

        public override CassieAnnouncement CassieDeathAnnouncement => null;

        public override float Damage { get; set; }

        public bool AllowSelfDamage => false;

        // From IRagdollInspectOverride
        public override string RagdollInspectText => string.Format(_deathReasonFormat, Config.RagdollInspectText);

        public override string DeathScreenText => Config.KilledByMessage;

        public override string ServerLogsText => "Obliterated by " + Config.KilledBy;

        public override string ServerMetricsText => base.ServerMetricsText;

        public Footprint Attacker { get; set; }

        public Footprint Target { get; set; }

        public Scp575DamageHandler()
        {
            _deathReasonFormat = scp575Translation.RagdollTranslation;
        }

        public Scp575DamageHandler(Player target, float damage, Player attacker = null, bool useHumanMutltipliers = true)
            : this()
        {
            Log.Debug($"[Scp575DamageHandler] Handler initialized with damage: {damage}, Attacker: {attacker?.Nickname ?? "null"}");
            Damage = damage;
            if (attacker != null) Attacker = new Footprint(attacker.ReferenceHub);
            Target = new Footprint(target.ReferenceHub);

            Vector3 randomDirectionVelocity = Random.onUnitSphere;
            float normalizedDamageModifier = Mathf.Log(3 * Damage + 1) * Config.KeterDamageVelocityModifier;

            _velocity = randomDirectionVelocity * normalizedDamageModifier;
            _penetration = Config.KeterDamagePenetration;
            _useHumanHitboxes = useHumanMutltipliers;
            Vector3 forward = target.ReferenceHub.PlayerCameraReference.forward;
            _hitDirectionX = (sbyte)Mathf.RoundToInt(forward.x * 127f);
            _hitDirectionZ = (sbyte)Mathf.RoundToInt(forward.z * 127f);

        }

        public override void WriteAdditionalData(NetworkWriter writer)
        {
            base.WriteAdditionalData(writer);
            writer.WriteByte((byte)Hitbox);
            writer.WriteSByte(_hitDirectionX);
            writer.WriteSByte(_hitDirectionZ);
            writer.WriteVector3(_velocity);
            writer.WriteReferenceHub(Target.Hub);
            try
            {
                Log.Debug("[Scp575DamageHandler] Trying to write Attacker ReferenceHub: " + Attacker.Hub);
                writer.WriteReferenceHub(Attacker.Hub);
            }
            catch (System.Exception e)
            {
                writer.WriteReferenceHub(null); // Write null if there's an error
                Log.Debug($"[Scp575DamageHandler] writing Attacker ReferenceHub aborted: {e}");
            }

        }

        public override void ReadAdditionalData(NetworkReader reader)
        {
            base.ReadAdditionalData(reader);
            Hitbox = (HitboxType)reader.ReadByte();
            _hitDirectionX = reader.ReadSByte();
            _hitDirectionZ = reader.ReadSByte();
            _velocity = reader.ReadVector3();
            Target = new Footprint(reader.ReadReferenceHub());
            var attackerHub = reader.ReadReferenceHub();
            Attacker = attackerHub != null ? new Footprint(attackerHub) : default;
            Log.Debug("[Scp575DamageHandler] Read Attacker ReferenceHub: " + attackerHub);


        }

        public override HandlerOutput ApplyDamage(ReferenceHub ply)
        {
            try
            {
                Player player = Player.Get(ply);

                // Apply some effects
                player.EnableEffect<Flashed>(0.25f);
                player.EnableEffect<Blindness>(0.75f);
                player.EnableEffect<Slowness>(3.25f);
                player.EnableEffect<Deafened>(2f);
                player.EnableEffect<Corroding>(1.5f);
                player.EnableEffect<Sinkhole>(1.5f);
                player.EnableEffect<Asphyxiated>(1.5f);
                player.EnableEffect<Bleeding>(3.25f);
                player.EnableEffect<Ensnared>(0.35f);
                player.PlaceBlood(_velocity);

                HandlerOutput handlerOutput = base.ApplyDamage(ply);

                switch (handlerOutput)
                {
                    case HandlerOutput.Death:
                        Log.Debug($"[Scp575DamageHandler] {player.Nickname} was killed by {Config.KilledBy} | Damage: {Damage:F1} | HP before death: {player.Health + Damage:F1}");
                        break;

                    case HandlerOutput.Damaged:
                        Log.Debug($"[Scp575DamageHandler] {player.Nickname} took {Damage:F1} damage from {Config.KilledBy} | Remaining HP: {player.Health:F1} | Raw HP damage dealt: {DealtHealthDamage:F1}");
                        break;

                    default:
                        Log.Debug($"[Scp575DamageHandler] {player.Nickname} received non-damaging interaction by {Config.KilledBy} | Damage: {Damage:F1} | HandlerOutput: {handlerOutput}");
                        break;
                }

                return handlerOutput;
            }
            catch (System.Exception e)
            {
                Log.Error($"[Scp575DamageHandler] Error in ApplyDamage: {e}");
                return HandlerOutput.Nothing;
            }
        }
        public override void ProcessDamage(ReferenceHub ply)
        {
            if (!_useHumanHitboxes && ply.IsHuman())
            {
                Hitbox = HitboxType.Body;
            }

            if (_useHumanHitboxes && HitboxDamageMultipliers.TryGetValue(Hitbox, out var value))
            {
                Damage *= value;
            }

            base.ProcessDamage(ply);
            if (Damage != 0f && ply.roleManager.CurrentRole is IArmoredRole armoredRole)
            {
                int armorEfficacy = armoredRole.GetArmorEfficacy(Hitbox);
                int penetrationPercent = Mathf.RoundToInt(_penetration * 100f);
                float num = Mathf.Clamp(ply.playerStats.GetModule<HumeShieldStat>().CurValue, 0f, Damage);
                float baseDamage = Mathf.Max(0f, Damage - num);
                float num2 = BodyArmorUtils.ProcessDamage(armorEfficacy, baseDamage, penetrationPercent);
                Damage = num2 + num;
            }
        }

        public override void ProcessRagdoll(BasicRagdoll ragdoll)
        {
            // ALWAYS call base first to wire up texts, network state, etc.
            base.ProcessRagdoll(ragdoll);
            Log.Debug($"[Scp575DamageHandler] Processing ragdoll: {ragdoll.name}");
            if (!HitboxToForce.TryGetValue(Hitbox, out var value) || !(ragdoll is DynamicRagdoll dynamicRagdoll))
            {
                Log.Warn($"[Scp575DamageHandler] Hitbox {Hitbox} not found in HitboxToForce or ragdoll is not DynamicRagdoll. Skipping velocity application.");
                return;
            }

            float value2 = Random.Range(1f, 5f);
            float num = value * value2;

            HitboxData[] hitboxes = dynamicRagdoll.Hitboxes;
            for (int i = 0; i < hitboxes.Length; i++)
            {
                Log.Debug($"[Scp575DamageHandler] Hitbox {i}: {hitboxes[i].RelatedHitbox} on {hitboxes[i].Target.name}");
                HitboxData hitboxData = hitboxes[i];
                if (hitboxData.RelatedHitbox == Hitbox)
                {
                    Log.Debug($"[Scp575DamageHandler] Applying velocity to hitbox {i} on {hitboxData.Target.name}");
                    hitboxData.Target.AddForce(_velocity, ForceMode.VelocityChange);
                }
            }

            // This will:
            //  - remove the old human mesh
            //  - remap all bones
            try
            {
                Log.Debug($"[Scp575DamageHandler] Converting ragdoll to bones by Scp3114RagdollToBonesConverter");
                Scp3114RagdollToBonesConverter.ConvertExisting(dynamicRagdoll);
            }
            catch (System.Exception e)
            {
                Log.Error($"[Scp575DamageHandler] Error converting ragdoll: {e}");
                return; // If conversion fails, we don't want to proceed with applying velocity.
            }
            // Re-apply velocity to all rigidbodies:
            foreach (var rb in dynamicRagdoll.LinkedRigidbodies)
            {
                //rb.linearVelocity = _velocity;
            }
        }

    }

}