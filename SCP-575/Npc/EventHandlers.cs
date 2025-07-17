namespace SCP_575.Npc
{
    using System.Collections.Generic;
    using Exiled.API.Enums;
    using MEC;
    using SCP_575.ConfigObjects;
    using SCP_575.Shared;
    using UnityEngine;

    public class EventHandlers
    {
        private readonly Plugin _plugin;

        public EventHandlers(Plugin plugin) => _plugin = plugin;

        private NpcConfig Config => _plugin.Config.NpcConfig;

        public List<CoroutineHandle> Coroutines = new List<CoroutineHandle>();

        public void OnRoundStart()
        {
            var roll = Library_ExiledAPI.Loader_Random_Next(100);
            Library_ExiledAPI.LogDebug("SCP-575.Npc.EventHandlers", $"OnRoundStart: SpawnChance Roll = {roll}, EnableKeter = {Config.EnableKeter}");

            if (roll <= _plugin.Config.NpcConfig.SpawnChance)
            {
                Library_ExiledAPI.LogDebug("SCP-575.Npc.EventHandlers", "OnRoundStart: SCP-575 NPC spawning due to roll being within spawn chance.");
                Coroutines.Add(Timing.RunCoroutine(_plugin.Npc.Methods.RunBlackoutTimer()));

                if (Config.EnableKeter)
                {
                    Library_ExiledAPI.LogDebug("SCP-575.Npc.EventHandlers", "OnRoundStart: Keter mode enabled, starting Keter damage coroutine.");
                    Coroutines.Add(Timing.RunCoroutine(_plugin.Npc.Methods.KeterDamage(), tag: "SCP575keter"));
                }
            }

        }

        public void OnRoundEnd(Exiled.Events.EventArgs.Server.RoundEndedEventArgs ev)
        {
            _plugin.Npc.Methods.Disable();
            foreach (CoroutineHandle handle in Coroutines) Timing.KillCoroutines(handle);
            Coroutines.Clear();
        }

        public void OnWaitingPlayers()
        {
            _plugin.Npc.Methods.Disable();
            foreach (CoroutineHandle handle in Coroutines) Timing.KillCoroutines(handle);
            Coroutines.Clear();
        }


        /// <summary>
        /// Called when a LabAPI generator is flipped on.  
        /// Enables & flickers lights in that room and all its neighbors, plays a global sound,
        /// then checks whether all generators are active to drive your SCP-575 logic.
        /// </summary>
        public void OnGeneratorActivated(LabApi.Events.Arguments.ServerEvents.GeneratorActivatedEventArgs ev)
        {
            // 1) Grab the LabAPI room where the generator was activated as Exiled room.
            Exiled.API.Features.Room exiledRoom = Library_ExiledAPI.ToExiledRoom(ev.Generator.Room);
            Library_ExiledAPI.LogDebug("OnGeneratorActivated", $"Generator activated in room: {exiledRoom.Name}");
            Library_ExiledAPI.EnableAndFlickerRoomAndNeighborLights(exiledRoom);

            // 4) Creepy audio cue
            AudioManager.PlayGlobalSound("scream-angry");

            // 5) If all generators are up, trigger your SCP-575 behavior
            if (_plugin.Npc.Methods.AreAllGeneratorsEngaged())
            {
                if (Library_LabAPI.NpcConfig.IsNpcKillable)
                {
                    _plugin.Npc.Methods.Kill575();
                }
                else
                {
                    _plugin.Npc.Methods.DyingGlobalSound();
                    _plugin.Npc.Methods.Reset575();
                }
            }
        }

        public void OnProjectileExploded(LabApi.Events.Arguments.ServerEvents.ProjectileExplodedEventArgs ev)
        {
            if (!_plugin.Npc.Methods.IsDangerousToScp575(ev.TimedGrenade)) return;

            Exiled.API.Features.Room room = Exiled.API.Features.Room.Get(ev.Position);
            if (room == null || !room.AreLightsOff || !_plugin.Npc.Methods.IsBlackoutActive) return;

            Library_ExiledAPI.LogDebug("OnGrenadeExploded", $"Grenade or disruptor used in dark SCP-575 room: {room.Name}");
            Library_ExiledAPI.EnableAndFlickerRoomAndNeighborLights(room);

            _plugin.Npc.Methods.AngryGlobalSound();

        }
    }
}
