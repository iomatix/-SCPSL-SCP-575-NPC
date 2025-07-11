namespace SCP_575
{
    using System.Collections.Generic;
    using UnityEngine;
    using Utils.Networking;
    using CustomPlayerEffects;
    using Footprinting;
    using InventorySystem.Items.Armor;
    using LabApi.Features.Wrappers;
    using Mirror;
    using PlayerRoles;
    using PlayerRoles.Ragdolls;
    using PlayerStatsSystem;
    using SCP_575.ConfigObjects;

     // TODO: Separate Logic from LabAPI vs Exiled API to different files
    public class Scp575DamageHandler : AttackerDamageHandler
    {
        public static string IdentifierName => nameof(Scp575DamageHandler);
        public static byte IdentifierByte => 175;

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
        public static readonly DeathTranslation scp575Translation = new DeathTranslation(IdentifierByte, IdentifierByte, IdentifierByte, "{0}");

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
            Exiled.API.Features.Log.Debug($"[Scp575DamageHandler] Handler initialized with damage: {damage}, Target: {target.Nickname}, Attacker: {attacker?.Nickname ?? "null"}");
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
            Exiled.API.Features.Log.Debug($"[ApplyDamage] Applying damage to {ply.nicknameSync.MyNick} with Hitbox: {Hitbox} and Damage: {Damage:F1}");
            Player player = Player.Get(ply);
            // Apply some effects
            player.EnableEffect<Ensnared>(duration: 0.35f);
            player.EnableEffect<Flashed>(duration: 0.075f);
            player.EnableEffect<Blurred>(duration: 0.25f);

            player.EnableEffect<Deafened>(duration: 3.75f);
            player.EnableEffect<AmnesiaVision>(duration: 3.65f);
            player.EnableEffect<Sinkhole>(duration: 3.25f);
            player.EnableEffect<Concussed>(duration: 3.15f);
            player.EnableEffect<Blindness>(duration: 2.65f);
            player.EnableEffect<Burned>(duration: 2.5f, intensity: 3); // Intensity of three: Damage is increased by 8.75%.

            player.EnableEffect<AmnesiaItems>(duration: 1.65f);
            player.EnableEffect<Stained>(duration: 0.75f);
            player.EnableEffect<Asphyxiated>(duration: 1.25f, intensity: 3); // Intensity of three: Stamina drains at 1.75% per second. HP drains at 0.7 per second.

            player.EnableEffect<Bleeding>(duration: 3.65f, intensity: 3); // Intensity of three: Damage values are 7, 3.5, 1.75, 0.875 and 0.7.
            player.EnableEffect<Disabled>(duration: 4.75f, intensity: 1); // Intensity of one: Movement is slowed down by 12%.
            player.EnableEffect<Exhausted>(duration: 6.75f);
            player.EnableEffect<Traumatized>(duration: 9.5f);

            HandlerOutput handlerOutput = base.ApplyDamage(ply);

            Exiled.API.Features.Player.Get(ply).PlaceBlood(new Vector3(0f, 0f, -1f));

            switch (handlerOutput)
            {
                case HandlerOutput.Death:
                    Exiled.API.Features.Log.Debug($"[ApplyDamage] {player.Nickname} was killed by {Config.KilledBy} | Damage: {Damage:F1} | HP before death: {player.Health + Damage:F1}");
                    break;

                case HandlerOutput.Damaged:
                    Exiled.API.Features.Log.Debug($"[ApplyDamage] {player.Nickname} took {Damage:F1} damage from {Config.KilledBy} | Remaining HP: {player.Health:F1} | Raw HP damage dealt: {DealtHealthDamage:F1}");
                    break;

                default:
                    Exiled.API.Features.Log.Debug($"[ApplyDamage] {player.Nickname} received non-damaging interaction by {Config.KilledBy} | Damage: {Damage:F1} | HandlerOutput: {handlerOutput}");
                    break;
            }

            return handlerOutput;
        }



        public override void ProcessDamage(ReferenceHub ply)
        {
            Exiled.API.Features.Log.Debug($"[ProcessDamage] Processing damage for {ply.nicknameSync.MyNick} with Hitbox: {Hitbox} and Damage: {Damage:F1}");
            if (!_useHumanHitboxes && ply.IsHuman())
            {
                Exiled.API.Features.Log.Debug($"[ProcessDamage] Using human hitboxes is disabled, setting Hitbox to Body for {ply.nicknameSync.MyNick}");
                Hitbox = HitboxType.Body;
            }

            if (_useHumanHitboxes && HitboxDamageMultipliers.TryGetValue(Hitbox, out var value))
            {
                Damage *= value;
                Exiled.API.Features.Log.Debug($"[ProcessDamage] Hitbox {Hitbox} found in HitboxDamageMultipliers, applying multiplier: {value} to Damage: {Damage:F1}");
            }

            Exiled.API.Features.Log.Debug($"[ProcessDamage] Processing base() for ProcessDamage(ply) after multipliers: {Damage:F1} for player: {ply.nicknameSync.MyNick}");
            base.ProcessDamage(ply);
            if (Damage != 0f && ply.roleManager.CurrentRole is IArmoredRole armoredRole)
            {
                Exiled.API.Features.Log.Debug($"[ProcessDamage] Player {ply.nicknameSync.MyNick} is an armored role: {armoredRole.ToString()}");
                int armorEfficacy = armoredRole.GetArmorEfficacy(Hitbox);
                int penetrationPercent = Mathf.RoundToInt(_penetration * 100f);
                float num = Mathf.Clamp(ply.playerStats.GetModule<HumeShieldStat>().CurValue, 0f, Damage);
                float baseDamage = Mathf.Max(0f, Damage - num);
                float num2 = BodyArmorUtils.ProcessDamage(armorEfficacy, baseDamage, penetrationPercent);
                Damage = num2 + num;
                Exiled.API.Features.Log.Debug($"[ProcessDamage] Player {ply.nicknameSync.MyNick} armor efficacy: {armorEfficacy}, penetration percent: {penetrationPercent}, base damage: {baseDamage:F1}, processed damage: {num2:F1}, final Damage: {Damage:F1}");
            }
        }

        public override void ProcessRagdoll(BasicRagdoll ragdoll)
        {
            Exiled.API.Features.Log.Debug($"[ProcessRagdoll] Processing ragdoll: {ragdoll.name}");
            base.ProcessRagdoll(ragdoll);
        }

        public float calculateForcePush(float baseValue = 1.0f)
        {
            float value2 = UnityEngine.Random.Range(Config.KeterForceMinModifier, Config.KeterForceMaxModifier);
            return baseValue * value2;
        }

        public Vector3 GetRandomUnitSphereVelocity(float baseValue = 1.0f)
        {
            Vector3 randomDirection = UnityEngine.Random.onUnitSphere;

            // Potential fix for items falling through the floor
            // If it's mostly pointing downward (e.g. more than 45° down), flip it!
            if (Vector3.Dot(randomDirection, Vector3.down) > 0.707f) // cos(45°) ≈ 0.707
            {
                Exiled.API.Features.Log.Debug($"[GetRandomUnitSphereVelocity] Vector3 is pointing downward, reflecting.");
                randomDirection = Vector3.Reflect(randomDirection, Vector3.up);
            }

            float modifier = baseValue * Mathf.Log(3 * Damage + 1) * calculateForcePush(Config.KeterDamageVelocityModifier);
            return randomDirection * modifier;
        }

    }

}
