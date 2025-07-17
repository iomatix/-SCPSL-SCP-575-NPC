namespace SCP_575.Shared
{
    using LabApi.Features.Wrappers;
    using SCP_575.ConfigObjects;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.InteropServices;
    using UnityEngine;

    public static class Library_LabAPI
    {

        #region Getters
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

        #endregion

        #region Utilities
        public static bool IsPlayerInDarkRoom(Player player)
        {
            // Remove debug logging for performance  
            var room = player.Room;
            if (room?.LightController == null) return false;

            return !room.LightController.LightsEnabled;
        }

        #endregion

        #region Cassie methods
        public static void Cassie_Clear() => Cassie.Clear();

        public static void Cassie_GlitchyMessage(string message) => Cassie.GlitchyMessage(message, NpcConfig.GlitchChance / 100, NpcConfig.JamChance / 100);

        public static void Cassie_Message(string message) => Cassie.Message(message, isNoisy: false, isSubtitles: false);
        #endregion

        #region Adapters

        /// <summary>  
        /// Converts an Exiled player to a LabAPI player wrapper.  
        /// </summary>  
        /// <param name="exiledPlayer">The Exiled player to convert.</param>  
        /// <returns>The LabAPI player wrapper or null if input is null.</returns>  
        public static LabApi.Features.Wrappers.Player? ToLabAPIPlayer(Exiled.API.Features.Player? exiledPlayer)
        {
            if (exiledPlayer?.ReferenceHub == null)
                return null;

            return LabApi.Features.Wrappers.Player.Get(exiledPlayer.ReferenceHub);
        }


        /// <summary>  
        /// Converts an Exiled ragdoll to a LabAPI ragdoll wrapper.  
        /// </summary>  
        /// <param name="exiledRagdoll">The Exiled ragdoll to convert.</param>  
        /// <returns>The LabAPI ragdoll wrapper or null if input is null.</returns>  
        public static Ragdoll? ToLabAPIRagdoll(Exiled.API.Features.Ragdoll? exiledRagdoll)
        {
            if (exiledRagdoll?.Base == null)
                return null;

            return Ragdoll.Get(exiledRagdoll.Base);
        }


        #endregion

    }
}
