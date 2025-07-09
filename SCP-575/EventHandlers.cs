namespace SCP_575
{
    using Exiled.API.Features;
    using Exiled.API.Features.Items;
    using Exiled.API.Features.Pickups;
    using Exiled.Events.EventArgs.Player;
    using Exiled.Loader;
    using InventorySystem;
    using MEC;
    using PlayerRoles.PlayableScps.Scp3114;
    using PlayerRoles.Ragdolls;
    using SCP_575.ConfigObjects;
    using System;
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
            Log.Debug($"[Catched Event] OnSpawningRagdoll: {ev.Info.Handler.RagdollInspectText}");
            if (ev.Info.Handler is Scp575DamageHandler scp575Handler)
            {
                Log.Debug($"[OnSpawningRagdoll] The event was caused by {Scp575DamageHandler.IdentifierName}");

                Player player = ev.Player;

                List<Item> itemsDropped = new List<Item>();
                foreach (Item item in player.Items)
                {

                    Log.Debug($"[OnSpawningRagdoll] Dropped item added to the list: {item.Serial} from player: {player.Nickname}");
                    itemsDropped.Add(item);
                }

                Log.Debug($"[OnSpawningRagdoll] Dropping all items from {player.Nickname}'s inventory called by Server.");
                player.Inventory.ServerDropEverything();

                Timing.CallDelayed(0.15f, () =>
                {
                    foreach (Item item in itemsDropped)
                    {
                        Pickup droppedPickup = Pickup.Get(item.Serial);

                        if (droppedPickup != null)
                        {

                            Vector3 randomDirection = scp575Handler.GetRandomUnitSphereVelocity();
                            float forceMagnitude = scp575Handler.calculateForcePush();
                            Log.Debug($"[OnSpawningRagdoll] Applying force to dropped item: {droppedPickup.Serial} with direction: {randomDirection} and magnitude: {forceMagnitude}");
                            droppedPickup.Base.transform.GetComponent<Rigidbody>()?.AddForce(randomDirection * forceMagnitude, ForceMode.Impulse);
                        }
                    }

                });
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
