namespace SCP_575
{
    using CustomPlayerEffects;
    using Exiled.API.Features;
    using Exiled.API.Features.DamageHandlers;
    using InventorySystem.Items.Pickups;
    using InventorySystem.Items.Usables.Scp330;
    using InventorySystem;
    using Mirror;
    using PlayerRoles;
    using PlayerRoles.Ragdolls;
    using PlayerStatsSystem;
    using UnityEngine;
    using SCP_575.ConfigObjects;
    using System.Linq;
    using Exiled.API.Features.Items;
    using InventorySystem.Items;
    using Discord;
    using Exiled.API.Enums;

    public class Scp575DamageHandler : StandardDamageHandler
    {
        public readonly Vector3 _velocity;

        public override CassieAnnouncement CassieDeathAnnouncement => null;

        public override float Damage { get; set; }

        public override string RagdollInspectText => _ragdollInspectText;
        public override string DeathScreenText => _deathScreenText;
        public override string ServerLogsText => _serverLogsText;

        private protected string _ragdollInspectText;
        private protected string _deathScreenText;
        private protected string _serverLogsText;


        public Scp575DamageHandler(float damage, NpcConfig pluginNpcConfig)
        {
            Damage = damage;
            _ragdollInspectText = pluginNpcConfig.RagdollInspectText;
            _deathScreenText = pluginNpcConfig.KilledByMessage;
            _serverLogsText = "Died to " + pluginNpcConfig.KilledBy;

            Vector3 randomDirectionVelocity = Random.onUnitSphere;
            float normalizedDamage = Mathf.Log(Damage + 1);
            randomDirectionVelocity *= normalizedDamage * pluginNpcConfig.KeterDamageVelocityModifier;
            this.StartVelocity = randomDirectionVelocity;

            _velocity = randomDirectionVelocity;
        }

        public override HandlerOutput ApplyDamage(ReferenceHub ply)
        {
            HandlerOutput handlerOutput = base.ApplyDamage(ply);

            Player.Get(ply).PlayShieldBreakSound();
            Log.Debug($"[Scp575DamageHandler] ApplyDamage invoked for player {ply.characterClassManager.name}, result = {handlerOutput}");

            if (handlerOutput != HandlerOutput.Death)
            {
                Log.Debug($"[Scp575DamageHandler] Player did not die. Exiting ApplyDamage.");
                return handlerOutput;
            }
            Log.Debug($"[Scp575DamageHandler] Death confirmed. Checking held item…");

            ItemIdentifier heldItem = ply.inventory.CurItem;
            if (heldItem.TypeId == ItemType.None)
            {
                Log.Debug($"[Scp575DamageHandler] No held item (ItemType.None). Nothing to drop.");
                return handlerOutput;
            }
            Log.Debug($"[Scp575DamageHandler] Player is holding {heldItem.TypeId} (Serial #{heldItem.SerialNumber}).");


            if (ply.inventory.UserInventory.Items.TryGetValue(heldItem.SerialNumber, out ItemBase entry))
            {
                Log.Debug($"[Scp575DamageHandler] Found entry: Type={heldItem.TypeId}, Weight={entry.Weight}, Name={entry.name}");
                var psi = new PickupSyncInfo(heldItem.TypeId, entry.Weight);
                Log.Debug($"[Scp575DamageHandler] Spawning pickup via ServerCreatePickup( Type={heldItem.TypeId}, Weight={entry.Weight})");
                var pickupGo = InventoryExtensions.ServerCreatePickup(
                    psi: psi,
                    inv: ply.inventory,
                    item: entry
                );

                if (pickupGo == null)
                {
                    Log.Warn($"[Scp575DamageHandler] ServerCreatePickup returned null for {heldItem.TypeId}.");
                    return handlerOutput;
                }

                if (pickupGo.TryGetComponent<Rigidbody>(out var rb))
                {
                    rb.linearVelocity = _velocity;
                    Log.Debug($"[Scp575DamageHandler] Applied velocity {_velocity} to pickup rigidbody.");
                }
                else
                {
                    Log.Warn($"[Scp575DamageHandler] Rigidbody component missing on spawned pickup {pickupGo.name}.");
                }

            }

            return handlerOutput;
        }

        public override void ProcessRagdoll(BasicRagdoll ragdoll)
        {
            base.ProcessRagdoll(ragdoll);

            Log.Info($"[Scp575DamageHandler] Processed ragdoll for {ragdoll.name}");
        }

        public override void WriteAdditionalData(NetworkWriter writer)
        {
            StartVelocity = _velocity;
            base.WriteAdditionalData(writer);
            writer.WriteString(RagdollInspectText);
            writer.WriteString(DeathScreenText);
            writer.WriteString(ServerLogsText);

        }

        public override void ReadAdditionalData(NetworkReader reader)
        {
            base.ReadAdditionalData(reader);
            _ragdollInspectText = reader.ReadString();
            _deathScreenText = reader.ReadString();
            _serverLogsText = reader.ReadString();
        }

    }
}
