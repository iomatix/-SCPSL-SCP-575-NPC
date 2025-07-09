namespace SCP_575
{
    using System.Collections.Generic;
    using Exiled.API.Features;
    using Exiled.Events.EventArgs.Player;
    using Exiled.Loader;
    using MEC;
    using SCP_575.ConfigObjects;

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
