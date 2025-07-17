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

        public static bool IsRoomFreeOfEngagedGenerators(Room room)
        {
            return Generator.List.Any(gen => gen.Room == room && gen.Engaged);
        }

        /// <summary>  
        /// Enables and flickers lights in the specified room and all its neighboring rooms.  
        /// </summary>  
        /// <param name="labRoom">The LabAPI room where the generator was activated</param>  
        public static void EnableAndFlickerRoomAndNeighborLights(LabApi.Features.Wrappers.Room labRoom)
        {
            // Convert LabAPI room to Exiled room to access neighbors  
            Exiled.API.Features.Room exiledRoom = Library_ExiledAPI.ToExiledRoom(labRoom);
            Library_ExiledAPI.LogDebug("EnableAndFlickerRoomLights", $"Processing room: {exiledRoom?.Name}");

            if (exiledRoom != null)
            {
                // Turn on & flicker lights in the main room  
                exiledRoom.RoomLightController.LightsEnabled = true;
                exiledRoom.RoomLightController.ServerFlickerLights(Library_LabAPI.NpcConfig.FlickerLightsDuration);

                // Process all neighboring rooms  
                foreach (var neighbor in exiledRoom.NearestRooms)
                {
                    Library_ExiledAPI.LogDebug(
                        "EnableAndFlickerRoomLights",
                        $"Also flickering lights in neighbor room: {neighbor.Name}"
                    );

                    neighbor.RoomLightController.LightsEnabled = true;
                    neighbor.RoomLightController.ServerFlickerLights(Library_LabAPI.NpcConfig.FlickerLightsDuration);
                }
            }
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
            if (exiledPlayer?.ReferenceHub == null) return null;

            return LabApi.Features.Wrappers.Player.Get(exiledPlayer.ReferenceHub);
        }


        /// <summary>  
        /// Converts an Exiled ragdoll to a LabAPI ragdoll wrapper.  
        /// </summary>  
        /// <param name="exiledRagdoll">The Exiled ragdoll to convert.</param>  
        /// <returns>The LabAPI ragdoll wrapper or null if input is null.</returns>  
        public static Ragdoll? ToLabAPIRagdoll(Exiled.API.Features.Ragdoll? exiledRagdoll)
        {
            if (exiledRagdoll?.Base == null) return null;

            return Ragdoll.Get(exiledRagdoll.Base);
        }

        /// <summary>
        /// Converts an Exiled Room back into the LabAPI wrapper,
        /// matching by world position using the static Distance(a,b).
        /// </summary>
        public static Room? ToLabApiRoom(this Exiled.API.Features.Room? exiledRoom)
        {
            if (exiledRoom == null) return null;

            return Room.List
                .FirstOrDefault(r => Scp575Helpers.Distance(r.Position, exiledRoom.Position) < 0.5f);
        }

        #endregion

    }
}
