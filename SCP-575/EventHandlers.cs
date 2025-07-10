namespace SCP_575
{
    using Exiled.API.Features;
    using Exiled.Loader;
    using InventorySystem;
    using LabApi.Events.Arguments.PlayerEvents;
    using LabApi.Features.Wrappers;
    using MEC;
    using PlayerRoles.PlayableScps.Scp3114;
    using PlayerRoles.Ragdolls;
    using SCP_575.ConfigObjects;
    using SCP_575.Npc;
    using System;
    using System.Collections.Generic;


    public class EventHandlers
    {
        private readonly Plugin _plugin;


        public EventHandlers(Plugin plugin) => _plugin = plugin;

        private Methods _methods => _plugin.Npc.Methods;

        public bool TeslasDisabled = false;
        public bool NukeDisabled = false;
        public List<CoroutineHandle> Coroutines = new List<CoroutineHandle>();


        public void OnWaitingForPlayers()
        {
            if (_plugin.Config.SpawnType == InstanceType.Npc || (_plugin.Config.SpawnType == InstanceType.Random && Loader.Random.Next(100) > 55))
            {
                _plugin.Npc.Methods.Init();
            }
            else
            {
                //_plugin.Playable.Methods.Init();
            }
        }

        public void OnPlayerHurting(PlayerHurtingEventArgs ev)
        {
            Log.Debug($"[Catched Event] OnPlayerHurting: {ev.Attacker?.Nickname ?? "No Attacker"} -> {ev.Player.Nickname}");
            if (ev.DamageHandler is Scp575DamageHandler scp575Handler)
            {
                Log.Debug($"[OnPlayerHurting] The event was caused by {Scp575DamageHandler.IdentifierName}");

            }

        }

        public void OnPlayerHurt(PlayerHurtEventArgs ev)
        {

            Log.Debug($"[Catched Event] OnPlayerHurt: {ev.Attacker?.Nickname ?? "No Attacker"} -> {ev.Player.Nickname}");
            if (ev.DamageHandler is Scp575DamageHandler scp575Handler)
            {
                Log.Debug($"[OnPlayerHurt] The event was caused by {Scp575DamageHandler.IdentifierName}");

            }

        }

        public void OnPlayerDying(PlayerDyingEventArgs ev)
        {
            Log.Debug($"[Catched Event] OnPlayerDying: {ev.Player.Nickname}");
            if (ev.DamageHandler is Scp575DamageHandler scp575Handler)
            {
                Log.Debug($"[OnPlayerDying] The event was caused by {Scp575DamageHandler.IdentifierName}");

                LabApi.Features.Wrappers.Player player = ev.Player;

                Log.Debug($"[OnPlayerDying] Dropping all items from {player.Nickname}'s inventory called by Server.");
                List<Item> items = new List<Item>(player.Items);
                player.Inventory.ServerDropEverything();

                Timing.RunCoroutine(_methods.DropAndPushItems(player, items, scp575Handler));

            }
        }

        public void OnSpawningRagdoll(PlayerSpawningRagdollEventArgs ev)
        {

            Log.Debug($"[Catched Event] OnSpawningRagdoll: {ev.Ragdoll.DamageHandler.RagdollInspectText}");
            if (ev.Ragdoll.DamageHandler is Scp575DamageHandler scp575Handler)
            {
                Log.Debug($"[OnSpawningRagdoll] The event was caused by {Scp575DamageHandler.IdentifierName}");

            }
        }

        public void OnSpawnedRagdoll(PlayerSpawnedRagdollEventArgs ev)
        {
            Log.Debug($"[Catched Event] OnSpawnedRagdoll: {ev.Player.Nickname}");
            if (ev.DamageHandler is Scp575DamageHandler scp575Handler)
            {
                Log.Debug($"[OnSpawnedRagdoll] The event was caused by {Scp575DamageHandler.IdentifierName}");
                LabApi.Features.Wrappers.Ragdoll ragdoll = ev.Ragdoll;
                if (ragdoll.Base is not DynamicRagdoll dynamicRagdoll)
                {
                    Log.Warn($"[OnSpawnedRagdoll] Ragdoll is not DynamicRagdoll. Skipping.");
                    return;
                }

                var hitbox = scp575Handler.Hitbox;
                if (!Scp575DamageHandler.HitboxToForce.TryGetValue(hitbox, out float baseForce))
                {
                    Log.Warn($"[OnSpawnedRagdoll] Unknown hitbox: {hitbox}. No force applied.");
                    return;
                }

                float finalForce = scp575Handler.calculateForcePush(baseForce);
                Log.Debug($"[OnSpawnedRagdoll] Final push force: {finalForce}");

                foreach (var _hitbox in dynamicRagdoll.Hitboxes)
                {
                    if (_hitbox.RelatedHitbox != hitbox) continue;

                    Log.Debug($"[OnSpawnedRagdoll] Applying force to hitbox: {_hitbox.RelatedHitbox}");
                    _hitbox.Target.AddForce(scp575Handler._velocity * finalForce, UnityEngine.ForceMode.VelocityChange);
                }

                try
                {
                    Scp3114RagdollToBonesConverter.ConvertExisting(dynamicRagdoll);
                    Log.Debug($"[OnSpawnedRagdoll] Converted ragdoll to bones.");
                }
                catch (Exception ex)
                {
                    Log.Error($"[OnSpawnedRagdoll] Bone conversion error: {ex}");
                    return;
                }

                foreach (var rb in dynamicRagdoll.LinkedRigidbodies)
                {
                    rb.AddForce(scp575Handler._velocity * finalForce, UnityEngine.ForceMode.VelocityChange);
                }

            }
        }

        public void OnPlayerDeath(PlayerDeathEventArgs ev)
        {
            Log.Debug($"[Catched Event] OnPlayerDeath: {ev.Player.Nickname}");
            if (ev.DamageHandler is Scp575DamageHandler scp575Handler)
            {
                Log.Debug($"[OnPlayerDeath] The event was caused by {Scp575DamageHandler.IdentifierName}");

            }
        }

    }
}
