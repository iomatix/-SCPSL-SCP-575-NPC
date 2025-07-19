namespace SCP_575.Shared
{
    using Exiled.API.Features;
    using Exiled.Loader;
    using MEC;
    using SCP_575.ConfigObjects;
    using SCP_575.Npc;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Unity.Profiling;
    using UnityEngine;

    /// <summary>
    /// Utility and adapter class for interacting with Exiled API within the SCP-575 context.
    /// Provides plugin config access, room/player helpers, Cassie messaging, and logging tools.
    /// </summary>
    public static class Library_ExiledAPI
    {
        #region Getters

        /// <summary>Gets the singleton instance of the SCP-575 plugin.</summary>
        public static Plugin Plugin => Plugin.Singleton;

        /// <summary>Gets the NPC Methods section of the plugin.</summary>
        public static Methods Methods => Plugin.Npc.Methods;

        /// <summary>Gets the NPC configuration section of the plugin.</summary>
        public static NpcConfig NpcConfig => Plugin.Config.NpcConfig;

        /// <summary>Gets the root plugin configuration object.</summary>
        public static SCP_575.Config Config => Plugin.Config;

        /// <summary>Gets the list of currently connected players.</summary>
        public static IReadOnlyCollection<Player> Players => Player.List;

        /// <summary>Gets the list of all rooms.</summary>
        public static IReadOnlyCollection<Room> Rooms => Room.List;

        /// <summary>Gets the list of all Tesla gates.</summary>
        public static IReadOnlyCollection<Exiled.API.Features.TeslaGate> TeslaGates => Exiled.API.Features.TeslaGate.List;

        /// <summary>Gets the room at the specified position.</summary>
        public static Room GetRoomAtPosition(Vector3 pos) => Room.Get(pos);

        #endregion

        #region Loader Methods

        /// <summary>Returns a random integer in range [0, range).</summary>
        public static int Loader_Random_Next(int range = 100) => Loader.Random.Next(range);

        /// <summary>Returns a random integer in range [minValue, maxValue).</summary>
        public static int Loader_Random_Next(int minValue = 0, int maxValue = 100) => Loader.Random.Next(minValue, maxValue);

        /// <summary>Returns a random double between 0.0 and 1.0.</summary>
        public static double Loader_Random_NextDouble() => Loader.Random.NextDouble();

        #endregion

        #region Cassie Methods

        /// <summary>Clears all currently queued Cassie messages.</summary>
        public static void Cassie_Clear() => Cassie.Clear();

        /// <summary>Sends a glitched Cassie message with configured glitch and jam chances.</summary>
        public static void Cassie_GlitchyMessage(string message) =>
            Cassie.GlitchyMessage(message, NpcConfig.GlitchChance / 100, NpcConfig.JamChance / 100);

        /// <summary>Sends a clean Cassie message with no noise or subtitles.</summary>
        public static void Cassie_Message(string message) =>
            Cassie.Message(message, isNoisy: false, isSubtitles: false, isHeld: false);

        #endregion

        #region Room Utilities

        /// <summary>Returns true if the given room contains an engaged generator.</summary>
        public static bool IsRoomFreeOfEngagedGenerators(Room room) =>
            Generator.List.Any(gen => gen.Room == room && gen.IsEngaged);

        /// <summary>
        /// Returns true if the specified room and all its neighbors are free of engaged generators.
        /// </summary>
        public static bool IsRoomAndNeighborsFreeOfEngagedGenerators(Room room) =>
            !Generator.List.Any(gen =>
                gen.IsEngaged &&
                (gen.Room == room || room.NearestRooms.Contains(gen.Room)));

        /// <summary>
        /// Enables and flickers lights in a room and all its neighboring rooms.
        /// </summary>
        /// <param name="room">The Exiled room to light up and flicker.</param>
        public static void EnableAndFlickerRoomAndNeighborLights(Room room)
        {
            if (room == null)
            {
                Library_ExiledAPI.LogWarn("EnableAndFlickerRoomAndNeighborLights", "Room instance is null");
                return;
            }

            foreach (Room neighbor in room.NearestRooms)
            {
                LogDebug("EnableAndFlickerRoomLights", $"Flickering lights in {(neighbor == room ? "the room" : "neighbor room")}: {neighbor.Name}");

                neighbor.AreLightsOff = false;
                neighbor.RoomLightController.ServerFlickerLights(NpcConfig.FlickerLightsDuration);
            }


        }

        /// <summary>
        /// Attempts a blackout event and increments blackout stacks by 1 if successful.
        /// </summary>
        /// <param name="room">The Exiled room to light up and flicker.</param>
        /// <param name="blackoutDurationBase"> Minimum time in seconds that the blackout occure.</param> 
        public static void DisableRoomAndNeighborLights(Room room, float blackoutDurationBase = 13f)
        {
            if (room == null)
            {
                Library_ExiledAPI.LogWarn("EnableAndFlickerRoomAndNeighborLights", "Room instance is null");
                return;
            }

            bool attemptFirstSucces = false;
            foreach (Room neighbor in room.NearestRooms)
            {
                LogDebug("DisableAndFlickerRoomAndNeighborLights", $"Flickering lights in {(neighbor == room ? "the room" : "neighbor room")}: {neighbor.Name}");

                float blackoutDuration = blackoutDurationBase + ((NpcConfig.DurationMin + NpcConfig.DurationMax) / 2f);

                bool attemptResult = Methods.AttemptRoomBlackout(neighbor, blackoutDuration, isCassieSilent: true, isForced: true);
                if (attemptResult)
                {
                    if (!attemptFirstSucces)
                    {
                        Methods.IncrementBlackoutStack();
                        AudioManager.PlayGlobalWhispersBang();
                        Timing.CallDelayed(blackoutDuration, () => Methods.DecrementBlackoutStack());
                        attemptFirstSucces = true;
                    }
                }

            }
        }

        public static bool IsInDarkRoom(Player player)
        {
            return player.CurrentRoom?.AreLightsOff ?? false;
        }

        #endregion

        #region Logging

        public static void LogDebug(string moduleId, string message) => Log.Debug($"[{moduleId}] {message}");
        public static void LogWarn(string moduleId, string message) => Log.Warn($"[{moduleId}] {message}");
        public static void LogInfo(string moduleId, string message) => Log.Info($"[{moduleId}] {message}");
        public static void LogError(string moduleId, string message) => Log.Error($"[{moduleId}] {message}");

        #endregion

        #region Adapters

        /// <summary>Converts a LabAPI player to Exiled player.</summary>
        public static Player? ToExiledPlayer(LabApi.Features.Wrappers.Player? labApiPlayer) =>
            labApiPlayer?.ReferenceHub == null ? null : Player.Get(labApiPlayer.ReferenceHub);

        /// <summary>Converts a LabAPI ragdoll to Exiled ragdoll.</summary>
        public static Ragdoll? ToExiledRagdoll(LabApi.Features.Wrappers.Ragdoll? labApiRagdoll) =>
            labApiRagdoll?.Base == null ? null : Ragdoll.Get(labApiRagdoll.Base);

        /// <summary>
        /// Converts a LabAPI Room to Exiled Room by matching world position.
        /// </summary>
        public static Room? ToExiledRoom(LabApi.Features.Wrappers.Room? labApiRoom)
        {
            if (labApiRoom == null) return null;

            return Room.List.FirstOrDefault(r => Scp575Helpers.Distance(r.Position, labApiRoom.Position) < 0.5f);
        }

        #endregion
    }
}
