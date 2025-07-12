namespace SCP_575.Shared
{
    using System.Collections.Generic;
    using Exiled.API.Features;
    using Exiled.Loader;
    using SCP_575.ConfigObjects;

    public static class Library_ExiledAPI
    {



        // Getters 

        public static  Plugin Plugin => Plugin.Singleton;
        public static NpcConfig NpcConfig => Plugin.Config.NpcConfig;
        public static SCP_575.Config Config => Plugin.Config;

        public static IReadOnlyCollection<Player> Players => Player.List;

        public static IReadOnlyCollection<Room> Rooms => Room.List;

        public static IReadOnlyCollection<Exiled.API.Features.TeslaGate> TeslaGates => Exiled.API.Features.TeslaGate.List;


        // Loader methods for the plugin

        public static int Loader_Random_Next(int range = 100) => Loader.Random.Next(range);

        public static int Loader_Random_Next(int minValue = 0, int maxValue = 100) => Loader.Random.Next(minValue, maxValue);

        public static double Loader_Random_NextDouble() => Loader.Random.NextDouble();

        // Cassie methods for the plugin
        public static void Cassie_Clear() => Cassie.Clear();
        public static void Cassie_GlitchyMessage(string message) => Cassie.GlitchyMessage(message, NpcConfig.GlitchChance / 100, NpcConfig.JamChance / 100);

        public static void Cassie_Message(string message) => Cassie.Message(message, false, false, false);



        // Logging methods for the plugin
        public static void LogDebug(string moduleId, string message)
        {
            Log.Debug($"[{moduleId}] {message}");
        }

        public static void LogWarn(string moduleId, string message)
        {
            Log.Warn($"[{moduleId}] {message}");
        }

        public static void LogInfo(string moduleId, string message)
        {
            Log.Info($"[{moduleId}] {message}");
        }

        public static void LogError(string moduleId, string message)
        {
            Log.Error($"[{moduleId}] {message}");
        }

        // Adapters
        public static Player ToExiledPlayer(LabApi.Features.Wrappers.Player labPlayer)
        {
            return Player.Get(labPlayer.ReferenceHub);
        }

    }
}
