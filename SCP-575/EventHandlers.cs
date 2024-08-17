namespace SCP_575
{
    using Exiled.Events.EventArgs.Player;
    using Exiled.Loader;
    using MEC;
    using SCP_575.ConfigObjects;
    using System.Collections.Generic;

    public class EventHandlers
    {
        private readonly Plugin _plugin;

        public EventHandlers(Plugin plugin) => _plugin = plugin;

        public bool TeslasDisabled = false;
        public bool NukeDisabled = false;
        public List<CoroutineHandle> Coroutines = new List<CoroutineHandle>();

        public void OnSpawningRagdoll(SpawningRagdollEventArgs ev)
        {
            if (!_plugin.StopRagdollList.Contains(ev.Player))
            {
                return;
            }

            ev.IsAllowed = false;
            _plugin.StopRagdollList.Remove(ev.Player);
        }

        public void OnWaitingForPlayers()
        {
            if (_plugin.Config.SpawnType == InstanceType.Npc || (_plugin.Config.SpawnType == InstanceType.Random && Loader.Random.Next(100) > 55))
            {
                _plugin.Npc.Methods.Init();
            }
            else
            {
                // Exiled.CustomRoles.API is disabled
                //_plugin.Playable.Methods.Init();
            }
        }
    }
}
