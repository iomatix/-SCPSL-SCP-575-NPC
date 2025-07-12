namespace SCP_575.Npc
{
    using System.Collections.Generic;
    using MEC;
    using SCP_575.ConfigObjects;
    using SCP_575.Shared;

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
    }
}
