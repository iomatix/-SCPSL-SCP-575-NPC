namespace SCP_575.Shared
{
    using LabApi.Features.Wrappers;
    using SCP_575.ConfigObjects;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Utility and adapter class for interacting with LabAPI within the SCP-575 context.
    /// Provides methods for Cassie messaging, light control, and conversions between LabAPI and Exiled types.
    /// </summary>
    public static class Library_LabAPI
    {
        #region Getters

        /// <summary>Gets the singleton instance of the SCP-575 plugin.</summary>
        public static Plugin Plugin => Plugin.Singleton;

        /// <summary>Gets the full configuration object.</summary>
        public static Config Config => Plugin.Config;

        /// <summary>Gets a list of all players (LabAPI wrapped).</summary>
        public static IReadOnlyCollection<Player> Players => Player.List;

        /// <summary>Gets a list of all rooms (LabAPI wrapped).</summary>
        public static IReadOnlyCollection<Room> Rooms => Room.List;

        /// <summary>Gets a LabAPI player wrapper from a reference hub.</summary>
        public static Player GetPlayer(ReferenceHub ply) => Player.Get(ply);

        /// <summary>Gets a LabAPI ragdoll wrapper from a native ragdoll object.</summary>
        public static Ragdoll GetRagdoll(PlayerRoles.Ragdolls.BasicRagdoll ragdoll) => Ragdoll.Get(ragdoll);

        #endregion

        #region Utilities

        /// <summary>
        /// Determines if the given player is in a dark room (lights off).
        /// </summary>
        public static bool IsPlayerInDarkRoom(Player player)
        {
            var room = player.Room;
            return room?.LightController != null && !room.LightController.LightsEnabled;
        }

        /// <summary>
        /// Returns true if the given room contains an engaged generator.
        /// </summary>
        public static bool IsRoomFreeOfEngagedGenerators(Room room) =>
            Generator.List.Any(gen => gen.Room == room && gen.Engaged);

        /// <summary>
        /// Enables and flickers lights in a LabAPI room and its neighboring Exiled rooms.
        /// </summary>
        public static void EnableAndFlickerRoomAndNeighborLights(Room labRoom)
        {
            var exiledRoom = Library_ExiledAPI.ToExiledRoom(labRoom);
            Library_ExiledAPI.LogDebug("EnableAndFlickerRoomLights", $"Processing room: {exiledRoom?.Name}");

            if (exiledRoom != null)
            {
                exiledRoom.RoomLightController.LightsEnabled = true;
                exiledRoom.RoomLightController.ServerFlickerLights(Config.BlackoutConfig.FlickerDuration);

                foreach (var neighbor in exiledRoom.NearestRooms)
                {
                    Library_ExiledAPI.LogDebug("EnableAndFlickerRoomLights", $"Also flickering lights in neighbor room: {neighbor.Name}");
                    neighbor.RoomLightController.LightsEnabled = true;
                    neighbor.RoomLightController.ServerFlickerLights(Config.BlackoutConfig.FlickerDuration);
                }
            }
        }

        #endregion

        #region Cassie Methods

        /// <summary>Clears all currently queued Cassie messages.</summary>
        public static void Cassie_Clear() => Cassie.Clear();

        /// <summary>Sends a glitched Cassie message with configured glitch and jam chances.</summary>
        public static void Cassie_GlitchyMessage(string message) =>
            Cassie.GlitchyMessage("pitch_1.15 " + message, Config.CassieConfig.GlitchChance / 100, Config.CassieConfig.JamChance / 100);

        /// <summary>Sends a clean Cassie message with no noise or subtitles.</summary>
        public static void Cassie_Message(string message) =>
            Cassie.Message("pitch_0.95 " + message, isNoisy: false, isSubtitles: false, isHeld: false);

        #endregion

        #region Adapters

        /// <summary>Converts an Exiled player into a LabAPI player wrapper.</summary>
        public static Player? ToLabAPIPlayer(Exiled.API.Features.Player? exiledPlayer) =>
            exiledPlayer?.ReferenceHub == null ? null : Player.Get(exiledPlayer.ReferenceHub);

        /// <summary>Converts an Exiled ragdoll to a LabAPI ragdoll wrapper.</summary>
        public static Ragdoll? ToLabAPIRagdoll(Exiled.API.Features.Ragdoll? exiledRagdoll) =>
            exiledRagdoll?.Base == null ? null : Ragdoll.Get(exiledRagdoll.Base);

        /// <summary>
        /// Converts an Exiled Room to LabAPI Room using world position.
        /// </summary>
        public static Room? ToLabApiRoom(this Exiled.API.Features.Room? exiledRoom)
        {
            if (exiledRoom == null) return null;

            return Room.List.FirstOrDefault(r => Helpers.Distance(r.Position, exiledRoom.Position) < 0.5f);
        }

        #endregion
    }
}
