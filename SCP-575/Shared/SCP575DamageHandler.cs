namespace SCP_575.Shared
{
    using System.Collections.Generic;
    using Footprinting;
    using InventorySystem.Items.Armor;
    using Mirror;
    using PlayerRoles;
    using PlayerRoles.Ragdolls;
    using PlayerStatsSystem;
    using SCP_575;
    using SCP_575.ConfigObjects;
    using UnityEngine;
    using Utils.Networking;
    public class Scp575DamageHandler : AttackerDamageHandler
    {
        public static string IdentifierName => nameof(Scp575DamageHandler);

        public static byte IdentifierByte => 175;

        private static NpcConfig Config = Plugin.Singleton.Config.NpcConfig;


        // Config & State
        public override float Damage { get; set; }
        public override bool AllowSelfDamage => false;
        public Footprint Target { get; set; }
        public override Footprint Attacker { get; set; }

        // Direction & Physics
        private sbyte _hitDirectionX, _hitDirectionZ;
        private Vector3 _velocity;
        private readonly float _penetration;
        private readonly string _deathReasonFormat;
        private readonly bool _useHumanHitboxes;

        // Multipliers
        public static readonly Dictionary<HitboxType, float> HitboxToForce = new()
        {
            [HitboxType.Body] = 0.08f,
            [HitboxType.Headshot] = 0.085f,
            [HitboxType.Limb] = 0.016f
        };
        public static readonly Dictionary<HitboxType, float> HitboxDamageMultipliers = new()
        {
            [HitboxType.Headshot] = 1.85f,
            [HitboxType.Limb] = 0.75f
        };


        public Scp575DamageHandler()
        {
            _deathReasonFormat = SCP575DeathTranslations.CustomDeathTranslation_arg1.RagdollTranslation;
        }


        public override CassieAnnouncement CassieDeathAnnouncement => null;

        public override string RagdollInspectText => string.Format(_deathReasonFormat, Config.KilledByMessage);

        public override string DeathScreenText => string.Empty;

        public override string ServerLogsText => $"Killed by {Config.KilledBy}, Attacker: {Attacker.Hub?.playerStats.name ?? "SCP-575 NPC"}, Hitbox: {Hitbox}";

        public override string ServerMetricsText => base.ServerMetricsText + "," + Config.KilledByMessage;

        public Scp575DamageHandler(LabApi.Features.Wrappers.Player target, float damage, LabApi.Features.Wrappers.Player attacker = null, bool useHumanMultipliers = true
        ) : this()
        {
            Library_ExiledAPI.LogDebug("Scp575DamageHandler", $"Handler initialized with damage: {damage}, Target: {target.Nickname}, Attacker: {attacker?.Nickname ?? "SCP-575 NPC"}");
            Damage = damage;
            Attacker = attacker?.ReferenceHub is var hub ? new Footprint(hub) : default;

            Target = new Footprint(target.ReferenceHub);

            _velocity = GetRandomUnitSphereVelocity(Config.KeterDamageVelocityModifier);
            _penetration = Config.KeterDamagePenetration;
            _useHumanHitboxes = useHumanMultipliers;
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
            Exiled.API.Features.Log.Debug($"[ApplyDamage] Applying damage to {ply.nicknameSync.MyNick} with Hitbox: {Hitbox} and Damage: {Damage:F1}");
            var labPlayer = LabApi.Features.Wrappers.Player.Get(ply);
            Scp575DamageHandler_LabAPI.ApplyDamageEffects(labPlayer);

            HandlerOutput handlerOutput = base.ApplyDamage(ply);

            Scp575DamageHandler_ExiledAPI.HandleApplyDamageFeedback(ply, Damage, handlerOutput);

            return handlerOutput;
        }

        public override void ProcessDamage(ReferenceHub ply)
        {
            Library_ExiledAPI.LogDebug("ProcessDamage", $"Processing damage for {ply.nicknameSync.MyNick} with Hitbox: {Hitbox} and Damage: {Damage:F1}");
            if (!_useHumanHitboxes && ply.IsHuman())
            {
                Library_ExiledAPI.LogDebug("ProcessDamage", $"Using human hitboxes is disabled, setting Hitbox to Body for {ply.nicknameSync.MyNick}");
                Hitbox = HitboxType.Body;
            }

            if (_useHumanHitboxes && HitboxDamageMultipliers.TryGetValue(Hitbox, out var damageMul))
            {
                Damage *= damageMul;
                Library_ExiledAPI.LogDebug("ProcessDamage", $"Hitbox {Hitbox} found in HitboxDamageMultipliers, applying multiplier: {damageMul} to Damage: {Damage:F1}");
            }

            Library_ExiledAPI.LogDebug("ProcessDamage", $"Processing base for ProcessDamage(ply) after multipliers: {Damage:F1} for player: {ply.nicknameSync.MyNick}");
            base.ProcessDamage(ply);
            if (Damage != 0f && ply.roleManager.CurrentRole is IArmoredRole armoredRole)
            {
                Exiled.API.Features.Log.Debug($"[ProcessDamage] Player {ply.nicknameSync.MyNick} is an armored role: {armoredRole.ToString()}");
                int armorEfficacy = armoredRole.GetArmorEfficacy(Hitbox);
                int penetrationPercent = Mathf.RoundToInt(_penetration * 100f);
                float shieldNum = Mathf.Clamp(ply.playerStats.GetModule<HumeShieldStat>().CurValue, 0f, Damage);
                float baseDamage = Mathf.Max(0f, Damage - shieldNum);
                float postArmorNum = BodyArmorUtils.ProcessDamage(armorEfficacy, baseDamage, penetrationPercent);
                Damage = postArmorNum + shieldNum;
                Library_ExiledAPI.LogDebug("ProcessDamage", $"Player {ply.nicknameSync.MyNick} armor efficacy: {armorEfficacy}, penetration percent: {penetrationPercent}, base damage: {baseDamage:F1}, processed damage: {postArmorNum:F1}, final Damage: {Damage:F1}");
            }
        }

        public override void ProcessRagdoll(BasicRagdoll ragdoll)
        {
            Library_ExiledAPI.LogDebug("ProcessRagdoll", $"Processing ragdoll for {ragdoll.name} with Hitbox: {Hitbox}, Damage: {Damage:F1}, Velocity: {_velocity}, Direction: ({_hitDirectionX}, {_hitDirectionZ})");
            base.ProcessRagdoll(ragdoll);
        }

        public float calculateForcePush(float baseValue = 1.0f)
        {
            float r = Random.Range(Config.KeterForceMinModifier, Config.KeterForceMaxModifier);
            return baseValue * r;
        }

        public Vector3 GetRandomUnitSphereVelocity(float baseValue = 1.0f)
        {
            Vector3 rDir = Random.onUnitSphere;

            // Potential fix for items falling through the floor
            // If it's mostly pointing downward (e.g. more than 45° down), flip it!
            if (Vector3.Dot(rDir, Vector3.down) > 0.707f) // cos(45°) ≈ 0.707
            {
                Exiled.API.Features.Log.Debug($"[GetRandomUnitSphereVelocity] Vector3 is pointing downward, reflecting.");
                rDir = Vector3.Reflect(rDir, Vector3.up);
            }

            float modifier = baseValue * Mathf.Log((3 * Damage) + 1) * calculateForcePush(Config.KeterDamageVelocityModifier);
            return rDir * modifier;
        }

    }

}
