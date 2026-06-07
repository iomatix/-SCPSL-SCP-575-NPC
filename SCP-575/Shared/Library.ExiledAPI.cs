namespace SCP_575.Shared
{
    using Exiled.API.Features;
    using Exiled.Loader;
    using MEC;
    using SCP_575.ConfigObjects;
    using SCP_575.Npc;
    using SCP_575.Shared;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEngine;

    /// <summary>
    /// A highly optimized utility class providing encapsulated access to Exiled API functionality for SCP-575.
    /// </summary>
    public static class LibraryExiledAPI
    {
        private static readonly System.Random Random = Loader.Random;

        #region Configuration and Plugin Access

        public static Plugin Plugin => Plugin.Singleton;
        public static Methods NpcMethods => Plugin.Npc.Methods;
        public static SCP_575.Config Config => Plugin.Config;

        #endregion

        #region Collection Accessors

        public static IReadOnlyCollection<Player> Players => Player.List;
        public static IReadOnlyCollection<Room> Rooms => Room.List;
        public static IReadOnlyCollection<TeslaGate> TeslaGates => TeslaGate.List;

        public static Room GetRoomAtPosition(Vector3 position) => Room.Get(position);

        #endregion

        #region Random Number Generation

        public static int GetRandomInt(int range = 100) => Random.Next(range);
        public static int GetRandomInt(int minValue, int maxValue) => Random.Next(minValue, maxValue);
        public static double GetRandomDouble() => Random.NextDouble();

        #endregion

        #region Cassie Messaging

        public static void ClearCassieQueue() => Cassie.Clear();

        public static void SendGlitchyCassieMessage(string message)
        {
            if (string.IsNullOrEmpty(message)) return;

            Cassie.GlitchyMessage(
                "pitch_1.15 " + message,
                Config.CassieConfig.GlitchChance / 100f,
                Config.CassieConfig.JamChance / 100f);
        }

        public static void SendCleanCassieMessage(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            Cassie.Message("pitch_0.95 " + message, isNoisy: false, isSubtitles: false, isHeld: false);
        }

        #endregion

        #region Room Utilities

        public static bool IsRoomFreeOfEngagedGenerators(Room room)
        {
            if (room == null) return false;

            // FIXED: Avoided heavy LINQ allocation tracking; standard loop evaluation handles this instantly.
            var generators = Generator.List;
            foreach (var gen in generators)
            {
                if (gen.Room == room && gen.IsEngaged) return false;
            }
            return true;
        }

        public static bool IsRoomAndNeighborsFreeOfEngagedGenerators(Room room)
        {
            if (room == null) return false;

            // FIXED: Optimized checking chain by stripping out slow, multi-layered Nested Contains lookups.
            if (!IsRoomFreeOfEngagedGenerators(room)) return false;

            var nearest = room.NearestRooms;
            foreach (var neighbor in nearest)
            {
                if (neighbor != null && !IsRoomFreeOfEngagedGenerators(neighbor))
                    return false;
            }

            return true;
        }

        public static void EnableAndFlickerRoomAndNeighbors(Room room)
        {
            if (room == null) return;

            // FIXED: Completely removed HashSet allocation spikes to keep memory graph perfectly flat.
            // Executing the baseline room flicker routine safely.
            LibraryLabAPI.LogDebug("EnableAndFlickerRoomAndNeighbors", "Flickering lights in the room: " + room.Name);
            room.AreLightsOff = false;
            room.RoomLightController.ServerFlickerLights(Config.BlackoutConfig.FlickerDuration);

            // Flickering adjacent rooms securely.
            var nearest = room.NearestRooms;
            foreach (var neighbor in nearest)
            {
                if (neighbor == null || neighbor == room) continue;

                LibraryLabAPI.LogDebug("EnableAndFlickerRoomAndNeighbors", "Flickering lights in neighbor room: " + neighbor.Name);
                neighbor.AreLightsOff = false;
                neighbor.RoomLightController.ServerFlickerLights(Config.BlackoutConfig.FlickerDuration);
            }
        }

        public static void TriggerBlackoutInRoomAndNeighbors(Room room, float blackoutDurationBase = 13f)
        {
            if (room == null) return;

            bool isFirstSuccess = false;
            float blackoutDuration = blackoutDurationBase + UnityEngine.Random.Range(Config.BlackoutConfig.DurationMin, Config.BlackoutConfig.DurationMax);

            // Processing the center target room
            var centerLabApiRoom = ToLabApiRoom(room);
            if (centerLabApiRoom != null)
            {
                LibraryLabAPI.LogDebug("TriggerBlackoutInRoomAndNeighbors", "Attempting blackout in the room: " + room.Name);
                bool attemptResult = NpcMethods.AttemptRoomBlackout(centerLabApiRoom, blackoutDuration, silent: true, forced: true);
                if (attemptResult)
                {
                    NpcMethods.IncrementBlackoutStack();
                    // FIXED: Re-routed the hardcoded tag string via unified system static tags container.
                    var coroutine = Timing.CallDelayed(blackoutDuration, () => NpcMethods.DecrementBlackoutStack());
                    coroutine.Tag = CoroutineTags.Temp;
                    isFirstSuccess = true;
                }
            }

            // Processing neighbors directly from the raw array wrapper layout
            var nearest = room.NearestRooms;
            foreach (var neighbor in nearest)
            {
                if (neighbor == null || neighbor == room) continue;

                var labApiRoom = ToLabApiRoom(neighbor);
                if (labApiRoom == null) continue;

                LibraryLabAPI.LogDebug("TriggerBlackoutInRoomAndNeighbors", "Attempting blackout in neighbor room: " + neighbor.Name);
                bool attemptResult = NpcMethods.AttemptRoomBlackout(labApiRoom, blackoutDuration, silent: true, forced: true);
                if (attemptResult && !isFirstSuccess)
                {
                    NpcMethods.IncrementBlackoutStack();
                    var coroutine = Timing.CallDelayed(blackoutDuration, () => NpcMethods.DecrementBlackoutStack());
                    coroutine.Tag = CoroutineTags.Temp;
                    isFirstSuccess = true;
                }
            }
        }

        public static bool IsPlayerInDarkRoom(Player player)
        {
            return player?.CurrentRoom != null && player.CurrentRoom.AreLightsOff;
        }

        #endregion

        #region API Conversions

        public static Player ToExiledPlayer(LabApi.Features.Wrappers.Player labApiPlayer)
        {
            if (labApiPlayer?.ReferenceHub == null) return null;
            return Player.Get(labApiPlayer.ReferenceHub);
        }

        public static Ragdoll ToExiledRagdoll(LabApi.Features.Wrappers.Ragdoll labApiRagdoll)
        {
            if (labApiRagdoll?.Base == null) return null;
            return Ragdoll.Get(labApiRagdoll.Base);
        }

        public static Room ToExiledRoom(LabApi.Features.Wrappers.Room labApiRoom)
        {
            if (labApiRoom == null) return null;
            Vector3 targetPos = labApiRoom.Position;

            // FIXED: Replaced slow geometric distance square-root operations with blazing fast SqrMagnitude delta scans.
            foreach (var room in Rooms)
            {
                if (Vector3.SqrMagnitude(room.Position - targetPos) < 0.05f)
                {
                    return room;
                }
            }
            return null;
        }

        #endregion

        #region Private Helpers

        private static LabApi.Features.Wrappers.Room ToLabApiRoom(Room room)
        {
            if (room == null) return null;
            return Plugin.Singleton.LibraryLabAPI.ToLabApiRoom(room);
        }

        #endregion
    }
}