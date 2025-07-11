namespace SCP_575
{
    using System;
    using System.Collections.Generic;
    //using Exiled.CustomRoles.API;
    //using Exiled.CustomRoles.API.Features;
    using MEC;
    using Server = Exiled.Events.Handlers.Server;

    public class Plugin : Exiled.API.Features.Plugin<Config>
    {
        public static Plugin Singleton;

        public override string Author { get; } = "iomatix & Joker119";
        public override string Name { get; } = "SCP-575 NPC";
        public override string Prefix { get; } = "SCP575";


        public override Version Version { get; } = new(6, 8, 1);
        public override Version RequiredExiledVersion { get; } = new(9, 6, 0);

        public EventHandlers EventHandlers { get; private set; }


        public NestingObjects.Npc Npc { get; private set; }
        //public NestingObjects.Playable Playable { get; private set; }
        //public List<Player> StopRagdollList { get; } = new List<Player>();

        public override void OnEnabled()
        {

            Singleton = this;
            //Config.PlayableConfig.Scp575.Register();
            EventHandlers = new EventHandlers(this);
            Npc = new NestingObjects.Npc(this);
            //Playable = new NestingObjects.Playable(this);

            Server.WaitingForPlayers += EventHandlers.OnWaitingForPlayers;

            LabApi.Events.Handlers.PlayerEvents.Hurting += EventHandlers.OnPlayerHurting;
            LabApi.Events.Handlers.PlayerEvents.Hurt += EventHandlers.OnPlayerHurt;
            LabApi.Events.Handlers.PlayerEvents.Dying += EventHandlers.OnPlayerDying;
            LabApi.Events.Handlers.PlayerEvents.Death += EventHandlers.OnPlayerDeath;
            LabApi.Events.Handlers.PlayerEvents.SpawningRagdoll += EventHandlers.OnSpawningRagdoll;
            LabApi.Events.Handlers.PlayerEvents.SpawnedRagdoll += EventHandlers.OnSpawnedRagdoll;


            base.OnEnabled();
        }

        public override void OnDisabled()
        {
            //CustomRole.UnregisterRoles();
            foreach (CoroutineHandle handle in EventHandlers.Coroutines) Timing.KillCoroutines(handle);
            EventHandlers.Coroutines.Clear();
            Server.WaitingForPlayers -= EventHandlers.OnWaitingForPlayers;

            LabApi.Events.Handlers.PlayerEvents.Hurting -= EventHandlers.OnPlayerHurting;
            LabApi.Events.Handlers.PlayerEvents.Hurt -= EventHandlers.OnPlayerHurt;
            LabApi.Events.Handlers.PlayerEvents.Dying -= EventHandlers.OnPlayerDying;
            LabApi.Events.Handlers.PlayerEvents.Death -= EventHandlers.OnPlayerDeath;
            LabApi.Events.Handlers.PlayerEvents.SpawningRagdoll -= EventHandlers.OnSpawningRagdoll;
            LabApi.Events.Handlers.PlayerEvents.SpawnedRagdoll -= EventHandlers.OnSpawnedRagdoll;

            EventHandlers = null;
            Npc = null;
            //Playable = null;

            base.OnDisabled();
        }
    }
}