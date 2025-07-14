namespace SCP_575.Shared
{
    using SCP_575;
    using SCP_575.ConfigObjects;

    public struct Scp575DeathTranslation
    {
        public readonly int _ragdollTranId;

        public readonly int _deathTranId;

        public readonly byte Id;

        public readonly string LogLabel;

        public readonly string RagdollTranslation;

        public readonly string DeathscreenTranslation;

        public Scp575DeathTranslation(byte id, int ragdoll, int deathscreen, string backup)
        {
            _ragdollTranId = ragdoll - 1;
            _deathTranId = deathscreen - 1;
            Id = id;
            LogLabel = backup;

            NpcConfig Config = Plugin.Singleton.Config.NpcConfig;

            RagdollTranslation = Config.RagdollInspectText;
            DeathscreenTranslation = Config.KilledByMessage;

            Scp575DeathTranslations.TranslationsById[id] = this;
        }
    }
}
