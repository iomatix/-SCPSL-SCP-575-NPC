namespace SCP_575
{
    using System.ComponentModel;
    using SCP_575.ConfigObjects;

    public class Config : Exiled.API.Interfaces.IConfig
    {
        [Description("Whether or not the plugin is enabled.")]
        public bool IsEnabled { get; set; } = true;

        [Description("The type of SCP-575 that will be used. Valid options: Npc")]
        public InstanceType SpawnType { get; set; } = InstanceType.Npc;

        [Description("The configs for NPC instances of SCP-575.")]
        public NpcConfig NpcConfig { get; set; } = new NpcConfig();

        [Description("Whether of not debug messages are displayed in the console.")]
        public bool Debug { get; set; } = false;
    }
}