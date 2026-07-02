namespace Company.PluginProject
{
    using System;
    using LabApi.Loader.Features.Plugins;
    using LabApi.Features.Console;

    public class Plugin : LabApi.Loader.Features.Plugins.Plugin<Config>
    {
        public static Plugin Singleton { get; private set; }

        public override string Author => "iomatix";
        public override string Name => "Company.PluginProject";
        public override string Description => "Production-grade automated plugin layer.";
        public override Version Version => new(1, 0, 0);
        public override Version RequiredApiVersion => new(1, 0, 0);

        public override void LoadConfigs()
        {
            base.LoadConfigs();
            Config.Validate();
        }

        public override void Enable()
        {
            Singleton = this;
            Logger.Info($"{Name} has been initialized successfully under LabAPI.");
        }

        public override void Disable()
        {
            Singleton = null;
            Logger.Info($"{Name} has been disabled.");
        }
    }
}