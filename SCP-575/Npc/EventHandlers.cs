namespace SCP_575.Npc
{
    using System.Collections.Generic;
    using Exiled.API.Features;
    using Exiled.Events.EventArgs.Server;
    using Exiled.Loader;
    using MEC;
    using SCP_575.ConfigObjects;


    public class EventHandlers
    {
        private readonly Plugin _plugin;
        public EventHandlers(Plugin plugin) => _plugin = plugin;

        private NpcConfig Config => _plugin.Config.NpcConfig;

        public List<CoroutineHandle> Coroutines = new List<CoroutineHandle>();
        public void OnRoundStart()
        {
            if (Loader.Random.Next(100) <= _plugin.Config.NpcConfig.SpawnChance)
            {
                Coroutines.Add(Timing.RunCoroutine(_plugin.Npc.Methods.RunBlackoutTimer()));

                if (Config.EnableKeter)
                {
                    Coroutines.Add(Timing.RunCoroutine(_plugin.Npc.Methods.KeterDamage(), tag: "SCP575keter"));
                }
            }

        }

        public void OnRoundEnd(RoundEndedEventArgs ev)
        {
            _plugin.Npc.Methods.Disable();
            foreach (CoroutineHandle handle in Coroutines) Timing.KillCoroutines(handle);
            Coroutines.Clear();
        }

        public void OnWaitingPlayers(RoundEndedEventArgs ev)
        {
            _plugin.Npc.Methods.Disable();
            foreach (CoroutineHandle handle in Coroutines) Timing.KillCoroutines(handle);
            Coroutines.Clear();
        }

    }
}
