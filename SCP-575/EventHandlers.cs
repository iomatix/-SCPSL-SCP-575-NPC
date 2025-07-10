namespace SCP_575
{
    using Exiled.API.Features;
    using Exiled.API.Features.Items;
    using Exiled.Events.EventArgs.Player;
    using Exiled.Loader;
    using InventorySystem;
    using LabApi.Events.Arguments.PlayerEvents;
    using MEC;
    using SCP_575.ConfigObjects;
    using SCP_575.Npc;
    using System.Collections.Generic;


    public class EventHandlers
    {
        private readonly Plugin _plugin;


        public EventHandlers(Plugin plugin) => _plugin = plugin;

        private Methods _methods => _plugin.Npc.Methods;

        public bool TeslasDisabled = false;
        public bool NukeDisabled = false;
        public List<CoroutineHandle> Coroutines = new List<CoroutineHandle>();

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



        public void OnSpawningRagdoll(PlayerSpawningRagdollEventArgs ev)
        {

            Log.Debug($"[Catched Event] OnSpawningRagdoll: {ev.Ragdoll.DamageHandler.RagdollInspectText}");
            if (ev.Ragdoll.DamageHandler is Scp575DamageHandler scp575Handler)
            {
                Log.Debug($"[OnSpawningRagdoll] The event was caused by {Scp575DamageHandler.IdentifierName}");

                Player player = ev.Player;

                Log.Debug($"[OnSpawningRagdoll] Dropping all items from {player.Nickname}'s inventory called by Server.");
                List<Item> items = new List<Item>(player.Items);
                player.Inventory.ServerDropEverything();

                Timing.RunCoroutine(_methods.DropAndPushItems(player, items, scp575Handler));
            }
        }

        public void OnSpawnedRagdoll(PlayerSpawnedRagdollEventArgs ev)
        {
            Log.Debug($"[Catched Event] OnSpawnedRagdoll: {ev.Player.Nickname}");
            if (ev.DamageHandler is Scp575DamageHandler scp575Handler)
            {
                Log.Debug($"[OnSpawnedRagdoll] The event was caused by {Scp575DamageHandler.IdentifierName}");

            }
        }

        public void OnPlayerDying(PlayerDyingEventArgs ev)
        {
            Log.Debug($"[Catched Event] OnPlayerDying: {ev.Player.Nickname}");
            if (ev.DamageHandler is Scp575DamageHandler scp575Handler)
            {
                Log.Debug($"[OnPlayerDying] The event was caused by {Scp575DamageHandler.IdentifierName}");

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
    }
}
