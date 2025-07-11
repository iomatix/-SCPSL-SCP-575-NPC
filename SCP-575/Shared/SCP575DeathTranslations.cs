namespace SCP_575.Shared
{
    using System.Collections.Generic;
    using SCP_575.ConfigObjects;

    public static class SCP575DeathTranslations
    {
        private static NpcConfig Config => Plugin.Singleton.Config.NpcConfig;

        public static readonly Dictionary<byte, SCP575DeathTranslation> TranslationsById = new Dictionary<byte, SCP575DeathTranslation>();


        public static readonly SCP575DeathTranslation DefaultDeathTranslation = new SCP575DeathTranslation(1, 2, 2, $"{Config.KilledByMessage}");
        public static readonly SCP575DeathTranslation CustomDeathTranslation_arg1 = new SCP575DeathTranslation(2, 3, 3, "{0}");
        public static readonly SCP575DeathTranslation CustomDeathTranslation_arg2 = new SCP575DeathTranslation(3, 4, 4, "{0} {1}");
        public static readonly SCP575DeathTranslation CustomDeathTranslation_arg3 = new SCP575DeathTranslation(4, 5, 5, "{0} {1} {2}");
    }
}