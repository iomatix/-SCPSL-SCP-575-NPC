namespace SCP_575
{
    using Exiled.API.Features;
    using Exiled.API.Features.Items;
    using Exiled.API.Features.Pickups;
    using Exiled.Events.EventArgs.Player;
    using Exiled.Loader;
    using InventorySystem;
    using MEC;
    using SCP_575.ConfigObjects;
    using System.Collections.Generic;
    using UnityEngine;

    public class EventHandlers
    {
        private readonly Plugin _plugin;

        public EventHandlers(Plugin plugin) => _plugin = plugin;

        public bool TeslasDisabled = false;
        public bool NukeDisabled = false;
        public List<CoroutineHandle> Coroutines = new List<CoroutineHandle>();

        public void OnSpawningRagdoll(SpawningRagdollEventArgs ev)
        {
            Log.Debug($"[Catched Event] On Spawning Ragdoll: {ev.Info.Handler.RagdollInspectText}");
            Scp575DamageHandler tempHandler = new Scp575DamageHandler();
            if (ev.Info.Handler.RagdollInspectText == tempHandler.RagdollInspectText)
            {
                Log.Debug($"[Event On Spawning Ragdoll] The event was caused by Scp575DamageHandler");
            }
        }
        public void OnDyingEvent(DyingEventArgs ev)
        {
            Log.Debug($"[Catched Event] On Dying: {ev.Player.Nickname}");
            Log.Debug($"Damage handler value: {ev.DamageHandler.ToString() ?? "null"}");
            Scp575DamageHandler tempHandler = new Scp575DamageHandler();
            Log.Debug($"[OnDyingEvent] Checking if the damage handler is {Scp575DamageHandler.IdentifierName} for player: {ev.Player.Nickname}");
            Log.Debug($"[OnDyingEvent] Damage handler name: {ev.DamageHandler}");
            if (ev.DamageHandler.ToString() == Scp575DamageHandler.IdentifierName)
            {
                Log.Debug($"[OnDyingEvent] Matched SCP575 handler! Now pushing items…");

                Player player = ev.Player;

                List<Item> itemsDropped = new List<Item>();
                foreach (Item item in player.Items)
                {

                    Log.Debug($"[OnDyingEvent] Dropped item added to the list: {item.Serial} from player: {player.Nickname}");
                    itemsDropped.Add(item);
                }

                Log.Debug($"[OnDyingEvent] Dropping all items from {player.Nickname}'s inventory called by Server.");
                player.Inventory.ServerDropEverything();

                Timing.CallDelayed(0.15f, () =>
                {
                    foreach (Item item in itemsDropped)
                    {
                        Pickup droppedPickup = Pickup.Get(item.Serial);

                        if (droppedPickup != null)
                        {

                            Vector3 randomDirection = tempHandler.GetRandomUnitSphereVelocity();
                            float forceMagnitude = tempHandler.calculateForcePush();
                            Log.Debug($"[Scp575DamageHandler] Applying force to dropped item: {droppedPickup.Serial} with direction: {randomDirection} and magnitude: {forceMagnitude}");
                            droppedPickup.Base.transform.GetComponent<Rigidbody>()?.AddForce(randomDirection * forceMagnitude, ForceMode.Force);
                        }
                    }

                });
            }
            else
            {
                Log.Debug($"[OnDyingEvent] Different handler: {ev.DamageHandler}");
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
