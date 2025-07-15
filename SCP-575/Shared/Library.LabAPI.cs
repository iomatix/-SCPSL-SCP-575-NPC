namespace SCP_575.Shared
{
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using LabApi.Features.Wrappers;
    using SCP_575.ConfigObjects;

    public static class Library_LabAPI
    {

        // Getters 

        public static Plugin Plugin => Plugin.Singleton;
        public static NpcConfig NpcConfig => Plugin.Config.NpcConfig;
        public static Config Config => Plugin.Config;


        public static IReadOnlyCollection<Player> Players => Player.List;


        public static IReadOnlyCollection<Room> Rooms => Room.List;

        public static Player GetPlayer(ReferenceHub ply)
        {
            return Player.Get(ply);
        }

        public static Ragdoll GetRagdoll(PlayerRoles.Ragdolls.BasicRagdoll ragdoll)
        {
            return Ragdoll.Get(ragdoll);
        }

        // Cassie methods for the plugin
        public static void Cassie_Clear() => Cassie.Clear();
        public static void Cassie_GlitchyMessage(string message) => Cassie.GlitchyMessage(message, NpcConfig.GlitchChance / 100, NpcConfig.JamChance / 100);

        public static void Cassie_Message(string message) => Cassie.Message(message, isNoisy: false, isSubtitles: false);

        // Adapters

        public static Player ToLabApiPlayer(Exiled.API.Features.Player exPlayer)
        {
            return Player.Get(exPlayer.ReferenceHub);
        }

    }
}
