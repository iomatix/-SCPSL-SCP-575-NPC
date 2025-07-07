namespace SCP_575
{
    using CustomPlayerEffects;
    using Exiled.API.Features;
    using Mirror;
    using PlayerRoles;
    using PlayerRoles.Ragdolls;
    using PlayerStatsSystem;
    using UnityEngine;

    public class Scp575DamageHandler : StandardDamageHandler
    {


        private float _damage;

        private readonly string _killedBy;

        public readonly string Reason;


        public override string RagdollInspectText => Reason;

        public override string DeathScreenText => _killedBy;
        public override string ServerLogsText => _killedBy;

        public override string ServerMetricsText => base.ServerMetricsText;

        public override CassieAnnouncement CassieDeathAnnouncement => null;

        public string AttackerNick => "SCP-575";

        public Scp575DamageHandler(float damage, string killedByName = "SCP-575", string reason = "Killed by SCP-575")
        {
            _damage = damage;
            _killedBy = killedByName;
            Reason = reason;
        }

        public override float Damage
        {
            get => _damage;
            set => _damage = value;
        }




        /// <summary>
        /// Apply any active status effect modifiers to the base damage.
        /// </summary>
        public override void ProcessDamage(ReferenceHub ply)
        {
            if (ply == null)
            {
                Log.Error("Null ReferenceHub in ProcessDamage.");
                return;
            }

            Player player = Player.Get(ply);
            if (player is null)
            {
                Log.Error($"Failed to resolve player from ReferenceHub: {ply.nicknameSync.Network_myNickSync}");
                return;
            }

            float modified = _damage;

            foreach (var effect in player.ActiveEffects)
            {
                if (effect is IDamageModifierEffect modifier)
                {
                    modified *= modifier.GetDamageModifier(_damage, this, HitboxType.Body);
                }
            }

            _damage = modified;
        }

        /// <summary>
        /// Replace the player's ragdoll with a skeleton-style version.
        /// </summary>
        public override void ProcessRagdoll(BasicRagdoll ragdoll)
        {
            if (ragdoll == null || ragdoll.Info == null)
            {
                Log.Warn("Ragdoll or its Info is null, skipping custom ragdoll processing.");
                return;
            }

            var newData = new RagdollData(
                hub: ragdoll.Info.OwnerHub,
                handler: this,
                roleType: RoleTypeId.Destroyed, // OR RoleTypeId.Scp3114
                position: ragdoll.Info.StartPosition,
                rotation: ragdoll.Info.StartRotation,
                nick: ragdoll.Info.Nickname,
                creationTime: ragdoll.Info.CreationTime,
                serial: ragdoll.Info.Serial
            );

            ragdoll.NetworkInfo = newData;

            Log.Debug($"Transformed ragdoll for {ragdoll.Info.Nickname} to black skeleton (RoleTypeId.Destroyed).");

            base.ProcessRagdoll(ragdoll);
        }


    }
}
