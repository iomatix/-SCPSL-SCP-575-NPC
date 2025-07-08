namespace SCP_575
{
    using Exiled.API.Features;
    using Mirror;
    using PlayerRoles.Ragdolls;
    using PlayerStatsSystem;
    using UnityEngine;
    using SCP_575.ConfigObjects;
    using Footprinting;
    using CustomPlayerEffects;
    using PlayerRoles.Spectating;
    using PlayerRoles;

    public class Scp575DamageHandler : StandardDamageHandler, IRagdollInspectOverride
    {
        private Vector3 _velocity;
        private string _ragdollInspectText;
        private string _deathScreenText;
        private string _serverLogsText;
        public override CassieAnnouncement CassieDeathAnnouncement => null;
        public override float Damage { get; set; }
        public bool AllowSelfDamage => false;

        // From IRagdollInspectOverride
        public override string RagdollInspectText => _ragdollInspectText;
        public override string DeathScreenText => _deathScreenText;
        public override string ServerLogsText => _serverLogsText;

        public Footprint Attacker { get; set; }

        public Scp575DamageHandler(float damage, NpcConfig pluginNpcConfig, Player attacker = null)
            : base()
        {
            Damage = damage;
            if (attacker != null) Attacker = new Footprint(attacker.ReferenceHub);

            _ragdollInspectText = pluginNpcConfig.RagdollInspectText;
            _deathScreenText = pluginNpcConfig.KilledByMessage;
            _serverLogsText = "Died to " + pluginNpcConfig.KilledBy;
            Log.Info($"[Scp575] InspectText='{_ragdollInspectText}', DeathText='{_deathScreenText}', LogText='{_serverLogsText}'");

            Vector3 randomDirectionVelocity = Random.onUnitSphere;
            float normalizedDamageModifier = Mathf.Log(3 * Damage + 1) * pluginNpcConfig.KeterDamageVelocityModifier;
            _velocity = randomDirectionVelocity * normalizedDamageModifier;

        }

        public override HandlerOutput ApplyDamage(ReferenceHub ply)
        {
            HandlerOutput handlerOutput = base.ApplyDamage(ply);
            try
            {
                if (handlerOutput != HandlerOutput.Death)
                {
                    Player player = Player.Get(ply);
                    player.EnableEffect(new Blindness(), 0.75f);
                    player.EnableEffect(new Slowness(), 1.25f);
                    player.PlaceBlood(_velocity);
                }
            }
            catch (System.Exception e)
            {
                Log.Error($"[Scp575DamageHandler] Error during handling ApplyDamage: {e}");
            }

            Log.Debug($"[Scp575DamageHandler] ApplyDamage invoked for player {ply.characterClassManager.name}, result = {handlerOutput}");

            return handlerOutput;
        }

        public override void ProcessRagdoll(BasicRagdoll ragdoll)
        {
            // ALWAYS call base first to wire up texts, network state, etc.
            base.ProcessRagdoll(ragdoll);
            // ONLY DynamicRagdoll can be remapped by the converter:
            if (ragdoll is DynamicRagdoll dyn)
            {
                Log.Info($"[Scp575DamageHandler] Converting ragdoll to SCP-3114 skeleton");

                // This will:
                //  - remove the old human mesh
                //  - remap all bones
                try
                {
                    //Scp3114RagdollToBonesConverter.ConvertExisting(dyn);
                }
                catch (System.Exception e)
                {
                    Log.Error($"[Scp575DamageHandler] Error converting ragdoll: {e}");
                    return; // If conversion fails, we don't want to proceed with applying velocity.
                }
                // Re-apply velocity to all rigidbodies:
                foreach (var rb in dyn.LinkedRigidbodies)
                {
                    //rb.linearVelocity += _velocity;
                }
            }
            else
            {
                // Non-dynamic ragdolls fall back to vanilla behavior
                Log.Warn($"[Scp575DamageHandler] Ragdoll is not DynamicRagdoll, using default mesh: {ragdoll.name}");
            }
        }


        public override void WriteDeathScreen(NetworkWriter writer)
        {
            writer.WriteSpawnReason(SpectatorSpawnReason.Other);
            writer.WriteUInt(Attacker.NetId);
            writer.WriteString(_deathScreenText);
            writer.WriteRoleType(RoleTypeId.None);
        }

        public override void WriteAdditionalData(NetworkWriter writer)
        {
            StartVelocity = _velocity;
            base.WriteAdditionalData(writer);
            writer.WriteVector3(_velocity);
            writer.WriteString(_ragdollInspectText);
            writer.WriteString(_deathScreenText);
            writer.WriteString(_serverLogsText);
        }

        public override void ReadAdditionalData(NetworkReader reader)
        {
            base.ReadAdditionalData(reader);
            _velocity = reader.ReadVector3();
            _ragdollInspectText = reader.ReadString();
            _deathScreenText = reader.ReadString();
            _serverLogsText = reader.ReadString();
        }

    }
}
