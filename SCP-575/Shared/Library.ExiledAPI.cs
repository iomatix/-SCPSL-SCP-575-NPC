namespace SCP_575.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Exiled.API.Features;
    using Exiled.Loader;
    using MEC;
    using SCP_575.ConfigObjects;
    using SCP_575.Npc;
    using UnityEngine;

    /// <summary>
    /// A utility class providing encapsulated access to Exiled API functionality for SCP-575.
    /// Simplifies interactions with players, rooms, Tesla gates, Cassie messages, and logging
    /// while maintaining compatibility with LabAPI for gradual transition.
    /// </summary>
    public static class LibraryExiledAPI
    {
        private static readonly System.Random Random = Loader.Random;

        #region Configuration and Plugin Access

        /// <summary>
        /// Gets the singleton instance of the SCP-575 plugin.
        /// </summary>
        public static Plugin Plugin
        {
            get { return Plugin.Singleton; }
        }

        /// <summary>
        /// Gets the NPC Methods section of the plugin.
        /// </summary>
        public static Methods NpcMethods
        {
            get { return Plugin.Npc.Methods; }
        }

        /// <summary>
        /// Gets the root plugin configuration object.
        /// </summary>
        public static SCP_575.Config Config
        {
            get { return Plugin.Config; }
        }

        #endregion

        #region Collection Accessors

        /// <summary>
        /// Gets the list of currently connected players.
        /// </summary>
        public static IReadOnlyCollection<Player> Players
        {
            get { return Player.List; }
        }

        /// <summary>
        /// Gets the list of all rooms.
        /// </summary>
        public static IReadOnlyCollection<Room> Rooms
        {
            get { return Room.List; }
        }

        /// <summary>
        /// Gets the list of all Tesla gates.
        /// </summary>
        public static IReadOnlyCollection<TeslaGate> TeslaGates
        {
            get { return TeslaGate.List; }
        }

        /// <summary>
        /// Gets the room at the specified position.
        /// </summary>
        /// <param name="position">The world position to query.</param>
        /// <returns>The room at the specified position, or null if not found.</returns>
        public static Room GetRoomAtPosition(Vector3 position)
        {
            return Room.Get(position);
        }

        #endregion

        #region Random Number Generation

        /// <summary>
        /// Generates a random integer between 0 (inclusive) and the specified range (exclusive).
        /// </summary>
        /// <param name="range">The upper bound (exclusive) for the random number. Defaults to 100.</param>
        /// <returns>A random integer within the specified range.</returns>
        public static int GetRandomInt(int range = 100)
        {
            return Random.Next(range);
        }

        /// <summary>
        /// Generates a random integer between the specified minimum (inclusive) and maximum (exclusive).
        /// </summary>
        /// <param name="minValue">The minimum value (inclusive). Defaults to 0.</param>
        /// <param name="maxValue">The maximum value (exclusive). Defaults to 100.</param>
        /// <returns>A random integer within the specified range.</returns>
        public static int GetRandomInt(int minValue, int maxValue)
        {
            return Random.Next(minValue, maxValue);
        }

        /// <summary>
        /// Generates a random double between 0.0 (inclusive) and 1.0 (exclusive).
        /// </summary>
        /// <returns>A random double value.</returns>
        public static double GetRandomDouble()
        {
            return Random.NextDouble();
        }

        #endregion

        #region Cassie Messaging

        /// <summary>
        /// Clears all currently queued Cassie messages.
        /// </summary>
        public static void ClearCassieQueue()
        {
            Cassie.Clear();
        }

        /// <summary>
        /// Sends a glitched Cassie message with configured glitch and jam chances.
        /// </summary>
        /// <param name="message">The message to broadcast.</param>
        public static void SendGlitchyCassieMessage(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                LogWarn("SendGlitchyCassieMessage", "Message is null or empty");
                return;
            }

            Cassie.GlitchyMessage(
                "pitch_1.15 " + message,
                Config.CassieConfig.GlitchChance / 100f,
                Config.CassieConfig.JamChance / 100f);
        }

        /// <summary>
        /// Sends a clean Cassie message without noise or subtitles.
        /// </summary>
        /// <param name="message">The message to broadcast.</param>
        public static void SendCleanCassieMessage(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                LogWarn("SendCleanCassieMessage", "Message is null or empty");
                return;
            }

            Cassie.Message("pitch_0.95 " + message, isNoisy: false, isSubtitles: false, isHeld: false);
        }

        #endregion

        #region Room Utilities

        /// <summary>
        /// Checks if a room contains any engaged generators.
        /// </summary>
        /// <param name="room">The room to check.</param>
        /// <returns>True if no engaged generators are present, false otherwise.</returns>
        public static bool IsRoomFreeOfEngagedGenerators(Room room)
        {
            if (room == null)
            {
                LogWarn("IsRoomFreeOfEngagedGenerators", "Room instance is null");
                return false;
            }

            return !Generator.List.Any(gen => gen.Room == room && gen.IsEngaged);
        }

        /// <summary>
        /// Checks if a room and its neighbors are free of engaged generators.
        /// </summary>
        /// <param name="room">The room to check.</param>
        /// <returns>True if no engaged generators are present in the room or its neighbors, false otherwise.</returns>
        public static bool IsRoomAndNeighborsFreeOfEngagedGenerators(Room room)
        {
            if (room == null)
            {
                LogWarn("IsRoomAndNeighborsFreeOfEngagedGenerators", "Room instance is null");
                return false;
            }

            return !Generator.List.Any(gen => gen.IsEngaged && (gen.Room == room || room.NearestRooms.Contains(gen.Room)));
        }

        /// <summary>
        /// Enables and flickers lights in a room and its neighbors.
        /// </summary>
        /// <param name="room">The target room.</param>
        public static void EnableAndFlickerRoomAndNeighbors(Room room)
        {
            if (room == null)
            {
                LogWarn("EnableAndFlickerRoomAndNeighbors", "Room instance is null");
                return;
            }

            HashSet<Room> roomSet = new HashSet<Room>();
            roomSet.Add(room);
            foreach (var neighbor in room.NearestRooms)
            {
                roomSet.Add(neighbor);
            }

            foreach (var targetRoom in roomSet)
            {
                LogDebug("EnableAndFlickerRoomAndNeighbors", 
                    "Flickering lights in " + (targetRoom == room ? "the room" : "neighbor room") + ": " + targetRoom.Name);
                targetRoom.AreLightsOff = false;
                targetRoom.RoomLightController.ServerFlickerLights(Config.BlackoutConfig.FlickerDuration);
            }
        }

        /// <summary>
        /// Triggers a blackout event in a room and its neighbors, incrementing blackout stacks if successful.
        /// </summary>
        /// <param name="room">The target room.</param>
        /// <param name="blackoutDurationBase">The base duration for the blackout in seconds. Defaults to 13.</param>
        public static void TriggerBlackoutInRoomAndNeighbors(Room room, float blackoutDurationBase = 13f)
        {
            if (room == null)
            {
                LogWarn("TriggerBlackoutInRoomAndNeighbors", "Room instance is null");
                return;
            }

            HashSet<Room> roomSet = new HashSet<Room>();
            roomSet.Add(room);
            foreach (var neighbor in room.NearestRooms)
            {
                roomSet.Add(neighbor);
            }

            bool isFirstSuccess = false;
            float blackoutDuration = blackoutDurationBase + UnityEngine.Random.Range(Config.BlackoutConfig.DurationMin, Config.BlackoutConfig.DurationMax);

            foreach (var targetRoom in roomSet)
            {
                LogDebug("TriggerBlackoutInRoomAndNeighbors", 
                    "Attempting blackout in " + (targetRoom == room ? "the room" : "neighbor room") + ": " + targetRoom.Name);

                var labApiRoom = ToLabApiRoom(targetRoom);
                if (labApiRoom == null)
                {
                    LogWarn("TriggerBlackoutInRoomAndNeighbors", "Failed to convert room to LabAPI: " + targetRoom.Name);
                    continue;
                }

                bool attemptResult = NpcMethods.AttemptRoomBlackout(labApiRoom, blackoutDuration, isCassieSilent: true, isForced: true);
                if (attemptResult && !isFirstSuccess)
                {
                    NpcMethods.IncrementBlackoutStack();
                    Timing.CallDelayed(blackoutDuration, () => NpcMethods.DecrementBlackoutStack());
                    isFirstSuccess = true;
                }
            }
        }

        /// <summary>
        /// Checks if a player is in a darkened room.
        /// </summary>
        /// <param name="player">The player to check.</param>
        /// <returns>True if the player's current room has lights off, false otherwise.</returns>
        public static bool IsPlayerInDarkRoom(Player player)
        {
            if (player == null)
            {
                LogWarn("IsPlayerInDarkRoom", "Player instance is null");
                return false;
            }

            return player.CurrentRoom != null && player.CurrentRoom.AreLightsOff;
        }

        #endregion

        #region Logging

        /// <summary>
        /// Logs a debug message with a module identifier.
        /// </summary>
        /// <param name="moduleId">The module identifier.</param>
        /// <param name="message">The message to log.</param>
        public static void LogDebug(string moduleId, string message)
        {
            Log.Debug("[" + moduleId + "] " + message);
        }

        /// <summary>
        /// Logs a warning message with a module identifier.
        /// </summary>
        /// <param name="moduleId">The module identifier.</param>
        /// <param name="message">The message to log.</param>
        public static void LogWarn(string moduleId, string message)
        {
            Log.Warn("[" + moduleId + "] " + message);
        }

        /// <summary>
        /// Logs an info message with a module identifier.
        /// </summary>
        /// <param name="moduleId">The module identifier.</param>
        /// <param name="message">The message to log.</param>
        public static void LogInfo(string moduleId, string message)
        {
            Log.Info("[" + moduleId + "] " + message);
        }

        /// <summary>
        /// Logs an error message with a module identifier.
        /// </summary>
        /// <param name="moduleId">The module identifier.</param>
        /// <param name="message">The message to log.</param>
        public static void LogError(string moduleId, string message)
        {
            Log.Error("[" + moduleId + "] " + message);
        }

        #endregion

        #region API Conversions

        /// <summary>
        /// Converts a LabAPI player to an Exiled player.
        /// </summary>
        /// <param name="labApiPlayer">The LabAPI player to convert.</param>
        /// <returns>The corresponding Exiled player, or null if conversion fails.</returns>
        public static Player ToExiledPlayer(LabApi.Features.Wrappers.Player labApiPlayer)
        {
            if (labApiPlayer == null || labApiPlayer.ReferenceHub == null)
            {
                LogWarn("ToExiledPlayer", "LabAPI player or ReferenceHub is null");
                return null;
            }

            return Player.Get(labApiPlayer.ReferenceHub);
        }

        /// <summary>
        /// Converts a LabAPI ragdoll to an Exiled ragdoll.
        /// </summary>
        /// <param name="labApiRagdoll">The LabAPI ragdoll to convert.</param>
        /// <returns>The corresponding Exiled ragdoll, or null if conversion fails.</returns>
        public static Ragdoll ToExiledRagdoll(LabApi.Features.Wrappers.Ragdoll labApiRagdoll)
        {
            if (labApiRagdoll == null || labApiRagdoll.Base == null)
            {
                LogWarn("ToExiledRagdoll", "LabAPI ragdoll or Base is null");
                return null;
            }

            return Ragdoll.Get(labApiRagdoll.Base);
        }

        /// <summary>
        /// Converts a LabAPI room to an Exiled room by matching world position.
        /// </summary>
        /// <param name="labApiRoom">The LabAPI room to convert.</param>
        /// <returns>The corresponding Exiled room, or null if not found.</returns>
        public static Room ToExiledRoom(LabApi.Features.Wrappers.Room labApiRoom)
        {
            if (labApiRoom == null)
            {
                LogWarn("ToExiledRoom", "LabAPI room is null");
                return null;
            }

            foreach (var room in Rooms)
            {
                if (Helpers.Distance(room.Position, labApiRoom.Position) < 0.5f)
                {
                    return room;
                }
            }

            LogWarn("ToExiledRoom", "No Exiled room found for LabAPI room at position: " + labApiRoom.Position);
            return null;
        }

        #endregion

        #region Private Helpers

        private static HashSet<Room> GetRoomAndNeighbors(Room room)
        {
            HashSet<Room> roomSet = new HashSet<Room>();
            roomSet.Add(room);
            foreach (var neighbor in room.NearestRooms)
            {
                roomSet.Add(neighbor);
            }

            return roomSet;
        }

        private static LabApi.Features.Wrappers.Room ToLabApiRoom(Room room)
        {
            if (room == null)
            {
                LogWarn("ToLabApiRoom", "Room instance is null");
                return null;
            }

            return Plugin.Singleton.LibraryLabAPI.ToLabApiRoom(room);
        }

        #endregion
    }
}
