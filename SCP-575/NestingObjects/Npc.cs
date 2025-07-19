namespace SCP_575.NestingObjects
{
    using SCP_575.Npc;

    public class Npc
    {
        public Methods Methods { get; set; }
        public EventHandler EventHandler { get; set; }

        public Npc(Plugin plugin)
        {
            Methods = new Methods(plugin);
            EventHandler = new EventHandler(plugin);
        }
    }
}