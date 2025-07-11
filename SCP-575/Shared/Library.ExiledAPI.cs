using Exiled.API.Features;

namespace SCP_575.Shared
{
    public static class Library_ExiledAPI
    {

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
    }
}
