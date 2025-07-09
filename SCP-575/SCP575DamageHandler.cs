namespace SCP_575
{
    using System.Collections.Generic;
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
    using UnityEngine;
    using Utils.Networking;

    public class Scp575DamageHandler : AttackerDamageHandler
    {
        public static string IdentifierName => nameof(Scp575DamageHandler);

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
        public static readonly DeathTranslation scp575Translation = new DeathTranslation(scp575Id, 26, 26, "{0}");

        public override CassieAnnouncement CassieDeathAnnouncement => null;

        public override float Damage { get; set; }

        public override bool AllowSelfDamage => false;

        // From IRagdollInspectOverride
        public override string RagdollInspectText => string.Format(_deathReasonFormat, Config.RagdollInspectText);

        public override string DeathScreenText => Config.KilledByMessage;

        public override string ServerLogsText => "Obliterated by " + Config.KilledBy;

        public override string ServerMetricsText => base.ServerMetricsText;

        public override Footprint Attacker { get; set; }

        public Footprint Target { get; set; }


        public Scp575DamageHandler()
        {
            _deathReasonFormat = scp575Translation.RagdollTranslation;
        }

        public Scp575DamageHandler(Player target, float damage, Player attacker = null, bool useHumanMutltipliers = true)
            : this()
        {
            Log.Debug($"[Scp575DamageHandler] Handler initialized with damage: {damage}, Target: {target.Nickname}, Attacker: {attacker?.Nickname ?? "null"}");
            Damage = damage;
            Attacker = attacker?.ReferenceHub is var hub ? new Footprint(hub) : default;

            Target = new Footprint(target.ReferenceHub);


            _velocity = GetRandomUnitSphereVelocity(Config.KeterDamageVelocityModifier);
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
        }

        public override void ReadAdditionalData(NetworkReader reader)
        {
            base.ReadAdditionalData(reader);
            Hitbox = (HitboxType)reader.ReadByte();
            _hitDirectionX = reader.ReadSByte();
            _hitDirectionZ = reader.ReadSByte();
            _velocity = reader.ReadVector3();
            Target = new Footprint(reader.ReadReferenceHub());
        }

        public override HandlerOutput ApplyDamage(ReferenceHub ply)
        {
            Player player = Player.Get(ply);
            // Apply some effects
            player.EnableEffect<Ensnared>(0.35f);
            player.EnableEffect<Flashed>(0.1f);
            player.EnableEffect<Blurred>(0.25f);

            player.EnableEffect<Deafened>(3.85f);
            player.EnableEffect<AmnesiaVision>(3.65f);
            player.EnableEffect<Sinkhole>(3.25f);
            player.EnableEffect<Concussed>(3.15f);
            player.EnableEffect<Blindness>(2.65f);
            player.EnableEffect<Burned>(2.5f);

            player.EnableEffect<AmnesiaItems>(1.65f);
            player.EnableEffect<Stained>(0.75f);
            player.EnableEffect<Asphyxiated>(1.25f);

            player.EnableEffect<Disabled>(3.75f);
            player.EnableEffect<Exhausted>(5.75f);
            player.EnableEffect<Traumatized>(15.5f);

            HandlerOutput handlerOutput = base.ApplyDamage(ply);

            player.PlaceBlood(_velocity);

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



        public override void ProcessDamage(ReferenceHub ply)
        {
            Log.Debug($"[Scp575DamageHandler] Processing damage for {ply.nicknameSync.MyNick} with Hitbox: {Hitbox} and Damage: {Damage:F1}");
            if (!_useHumanHitboxes && ply.IsHuman())
            {
                Log.Debug($"[Scp575DamageHandler] Using human hitboxes is disabled, setting Hitbox to Body for {ply.nicknameSync.MyNick}");
                Hitbox = HitboxType.Body;
            }

            if (_useHumanHitboxes && HitboxDamageMultipliers.TryGetValue(Hitbox, out var value))
            {
                Damage *= value;
                Log.Debug($"[Scp575DamageHandler] Hitbox {Hitbox} found in HitboxDamageMultipliers, applying multiplier: {value} to Damage: {Damage:F1}");
            }

            Log.Debug($"[Scp575DamageHandler] Processing base() for ProcessDamage(ply) after multipliers: {Damage:F1} for player: {ply.nicknameSync.MyNick}");
            base.ProcessDamage(ply);
            if (Damage != 0f && ply.roleManager.CurrentRole is IArmoredRole armoredRole)
            {
                Log.Debug($"[Scp575DamageHandler] Player {ply.nicknameSync.MyNick} is an armored role: {armoredRole.ToString()}");
                int armorEfficacy = armoredRole.GetArmorEfficacy(Hitbox);
                int penetrationPercent = Mathf.RoundToInt(_penetration * 100f);
                float num = Mathf.Clamp(ply.playerStats.GetModule<HumeShieldStat>().CurValue, 0f, Damage);
                float baseDamage = Mathf.Max(0f, Damage - num);
                float num2 = BodyArmorUtils.ProcessDamage(armorEfficacy, baseDamage, penetrationPercent);
                Damage = num2 + num;
                Log.Debug($"[Scp575DamageHandler] Player {ply.nicknameSync.MyNick} armor efficacy: {armorEfficacy}, penetration percent: {penetrationPercent}, base damage: {baseDamage:F1}, processed damage: {num2:F1}, final Damage: {Damage:F1}");
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

            float num = calculateForcePush(value);
            Log.Debug($"[Scp575DamageHandler] Applying force: {num} to ragdoll: {ragdoll.name} with hitbox: {Hitbox}");

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

        public float calculateForcePush(float baseValue = 1.0f)
        {
            float value2 = Random.Range(Config.KeterForceMinModifier, Config.KeterForceMaxModifier);
            return baseValue * value2;
        }

        public Vector3 GetRandomUnitSphereVelocity(float baseValue = 1.0f)
        {
            Vector3 randomDirection = Random.onUnitSphere;
            float modifier = baseValue * Mathf.Log(3 * Damage + 1) * calculateForcePush(Config.KeterDamageVelocityModifier);
            return randomDirection * modifier;
        }

    }

}