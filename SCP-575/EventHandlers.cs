namespace SCP_575
{
    using System.Collections.Generic;
    using Exiled.API.Features;
    using Exiled.API.Features.Items;
    using Exiled.Events.EventArgs.Player;
    using Exiled.Loader;
    using InventorySystem;
    using MEC;
    using SCP_575.ConfigObjects;
    using SCP_575.Npc;


    public class EventHandlers
    {
        private readonly Plugin _plugin;

        

        public EventHandlers(Plugin plugin) => _plugin = plugin;
        private Methods _methods => _plugin.Npc.Methods;


        public bool TeslasDisabled = false;
        public bool NukeDisabled = false;
        public List<CoroutineHandle> Coroutines = new List<CoroutineHandle>();

        public void OnSpawningRagdoll(SpawningRagdollEventArgs ev)
        {
            Log.Debug($"[Catched Event] OnSpawningRagdoll: {ev.Info.Handler.RagdollInspectText}");
            if (ev.Info.Handler is Scp575DamageHandler scp575Handler)
            {
                Log.Debug($"[OnSpawningRagdoll] The event was caused by {Scp575DamageHandler.IdentifierName}");

                Player player = ev.Player;

                Log.Debug($"[OnSpawningRagdoll] Dropping all items from {player.Nickname}'s inventory called by Server.");
                List<Item> items = new List<Item>(player.Items);
                player.Inventory.ServerDropEverything();
                
                Timing.RunCoroutine(_methods.DropAndPushItems(player, items, scp575Handler));


            }
        }

        public void OnDyingEvent(DyingEventArgs ev)
        {
            Log.Debug($"[Catched Event] OnDyingEvent: {ev.Player.Nickname}");
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
