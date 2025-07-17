namespace SCP_575.Shared
{
    using Exiled.API.Features;
    using Exiled.Loader;
    using SCP_575.ConfigObjects;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEngine;

    public static class Library_ExiledAPI
    {

        #region Getters

        public static Plugin Plugin => Plugin.Singleton;
        public static NpcConfig NpcConfig => Plugin.Config.NpcConfig;
        public static SCP_575.Config Config => Plugin.Config;

        public static IReadOnlyCollection<Player> Players => Player.List;

        public static IReadOnlyCollection<Room> Rooms => Room.List;

        public static IReadOnlyCollection<Exiled.API.Features.TeslaGate> TeslaGates => Exiled.API.Features.TeslaGate.List;

        #endregion

        #region Loader Methods

        public static int Loader_Random_Next(int range = 100) => Loader.Random.Next(range);

        public static int Loader_Random_Next(int minValue = 0, int maxValue = 100) => Loader.Random.Next(minValue, maxValue);

        public static double Loader_Random_NextDouble() => Loader.Random.NextDouble();

        #endregion

        #region Cassie methods
        public static void Cassie_Clear() => Cassie.Clear();
        public static void Cassie_GlitchyMessage(string message) => Cassie.GlitchyMessage(message, NpcConfig.GlitchChance / 100, NpcConfig.JamChance / 100);

        public static void Cassie_Message(string message) => Cassie.Message(message, false, false, false);
        #endregion

        #region Utilities

        public static bool IsRoomFreeOfEngagedGenerators(Room room)
        {
            return Generator.List.Any(gen => gen.Room == room && gen.IsEngaged);
        }

        public static bool IsRoomAndNeighborsFreeOfEngagedGenerators(Room room)
        {
            return !Generator.List.Any(gen =>
                gen.IsEngaged
                && (gen.Room == room || room.NearestRooms.Contains(gen.Room))
            );
        }

        /// <summary>  
        /// Enables and flickers lights in the specified room and all its neighboring rooms.  
        /// </summary>  
        /// <param name="labRoom">The LabAPI room where the generator was activated</param>  
        public static void EnableAndFlickerRoomAndNeighborLights(Room room)
        {

            if (room != null)
            {
                // Turn on & flicker lights in the main room  
                room.RoomLightController.LightsEnabled = true;
                room.RoomLightController.ServerFlickerLights(Library_LabAPI.NpcConfig.FlickerLightsDuration);

                // Process all neighboring rooms  
                foreach (var neighbor in room.NearestRooms)
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

        #region Logging methods
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

        #endregion

        #region Adapters
        /// <summary>  
        /// Converts a LabAPI player wrapper to an Exiled player.  
        /// </summary>  
        /// <param name="labApiPlayer">The LabAPI player wrapper to convert.</param>  
        /// <returns>The Exiled player or null if input is null.</returns>  
        public static Player? ToExiledPlayer(LabApi.Features.Wrappers.Player? labApiPlayer)
        {
            if (labApiPlayer?.ReferenceHub == null) return null;

            return Player.Get(labApiPlayer.ReferenceHub);
        }

        /// <summary>  
        /// Converts a LabAPI ragdoll wrapper to an Exiled ragdoll.  
        /// </summary>  
        /// <param name="labApiRagdoll">The LabAPI ragdoll wrapper to convert.</param>  
        /// <returns>The Exiled ragdoll or null if input is null.</returns>  
        public static Ragdoll? ToExiledRagdoll(LabApi.Features.Wrappers.Ragdoll? labApiRagdoll)
        {
            if (labApiRagdoll?.Base == null) return null;

            return Ragdoll.Get(labApiRagdoll.Base);
        }

        /// <summary>
        /// Converts a LabAPI Room wrapper into its corresponding Exiled Room,
        /// matching by world position using the static Distance(a,b).
        /// </summary>
        public static Room? ToExiledRoom(LabApi.Features.Wrappers.Room? labApiRoom)
        {
            if (labApiRoom == null) return null;

            // Use your static Distance method to compare positions:
            return Room.List
                .FirstOrDefault(r => Scp575Helpers.Distance(r.Position, labApiRoom.Position) < 0.5f);
        }


        #endregion

    }
}
