namespace SCP_575.Shared
{
    using System.Collections.Generic;
    using SCP_575.ConfigObjects;

    public static class Scp575DeathTranslations
    {
        private static NpcConfig Config => Plugin.Singleton.Config.NpcConfig;

        public static readonly Dictionary<byte, Scp575DeathTranslation> TranslationsById = new Dictionary<byte, Scp575DeathTranslation>();


        public static readonly Scp575DeathTranslation DefaultDeathTranslation = new Scp575DeathTranslation(1, 2, 2, $"{Config.KilledByMessage}");
        public static readonly Scp575DeathTranslation CustomDeathTranslation_arg1 = new Scp575DeathTranslation(2, 3, 3, "{0}");
        public static readonly Scp575DeathTranslation CustomDeathTranslation_arg2 = new Scp575DeathTranslation(3, 4, 4, "{0} {1}");
        public static readonly Scp575DeathTranslation CustomDeathTranslation_arg3 = new Scp575DeathTranslation(4, 5, 5, "{0} {1} {2}");
    }
}