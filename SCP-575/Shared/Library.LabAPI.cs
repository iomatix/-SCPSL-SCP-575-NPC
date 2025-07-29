namespace SCP_575.Shared
{
    using LabApi.Features.Wrappers;
    using MapGeneration;
    using MEC;
    using SCP_575.ConfigObjects;
    using SCP_575.Npc;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEngine;

    /// <summary>
    /// Utility and adapter class for interacting with LabAPI within the SCP-575 context.
    /// Provides methods for Cassie messaging, light control, and conversions between LabAPI and Exiled types.
    /// </summary>
    public static class Library_LabAPI
    {
        #region Getters

        /// <summary>Gets the singleton instance of the SCP-575 plugin.</summary>
        public static Plugin Plugin => Plugin.Singleton;

        /// <summary>Gets the NPC Methods section of the plugin.</summary>
        public static Methods Methods => Plugin.Npc.Methods;

        /// <summary>Gets the root plugin configuration object.</summary>
        public static Config Config => Plugin.Config;

        /// <summary>Gets a list of all players (LabAPI wrapped).</summary>
        public static IReadOnlyCollection<Player> Players => Player.List;

        /// <summary>Gets a list of all rooms (LabAPI wrapped).</summary>
        public static IReadOnlyCollection<Room> Rooms => Room.List;

        /// <summary>Gets a list of all elevators (LabAPI wrapped).</summary>  
        public static IReadOnlyCollection<Elevator> Elevators => Elevator.List;  
          
        /// <summary>Gets a random elevator from the facility.</summary>  
        public static Elevator GetRandomElevator() => Map.GetRandomElevator();

        /// <summary>Gets the list of all Tesla gates.</summary>
        public static IReadOnlyCollection<Tesla> Teslas => Tesla.List;

        /// <summary>Gets the room at the specified position.</summary>
        public static Room GetRoomAtPosition(Vector3 pos) => Room.GetRoomAtPosition(pos);

        /// <summary>Gets a LabAPI player wrapper from a reference hub.</summary>
        public static Player GetPlayer(ReferenceHub ply) => Player.Get(ply);

        /// <summary>Gets a LabAPI ragdoll wrapper from a native ragdoll object.</summary>
        public static Ragdoll GetRagdoll(PlayerRoles.Ragdolls.BasicRagdoll ragdoll) => Ragdoll.Get(ragdoll);

        #endregion

        #region Room Utilities

        /// <summary>  
        /// Turns off lights in a specific room for the specified duration.  
        /// </summary>  
        /// <param name="room">The room to turn lights off in.</param>  
        /// <param name="duration">Duration in seconds to keep lights off.</param>  
        public static void TurnOffRoomLights(Room room, float duration)
        {
            if (room == null)
            {
                Library_ExiledAPI.LogWarn("TurnOffRoomLights", "Room instance is null");
                return;
            }

            Library_ExiledAPI.ToExiledRoom(room).TurnOffLights(duration);
            // This foreach loop makes global blackout.
            //foreach (var lightController in room.AllLightControllers)
            //{
            //    lightController.FlickerLights(duration);
            //}

            Library_ExiledAPI.LogDebug("TurnOffRoomLights", $"Lights turned off in room {room.Name} for {duration} seconds.");
        }

        /// <summary>Returns true if the given room contains an engaged generator.</summary>  
        public static bool IsRoomFreeOfEngagedGenerators(Room room)
        {
            if (!Generator.TryGetFromRoom(room, out List<Generator>? generators))
                return true; // No generators in room  

            return !generators.Any(gen => gen.Engaged);
        }

        /// <summary>  
        /// Returns true if the specified room and all its neighbors are free of engaged generators.  
        /// </summary>  
        public static bool IsRoomAndNeighborsFreeOfEngagedGenerators(Room room)
        {
            if (room == null) return false;

            // Get connected rooms  
            var connectedRooms = room.ConnectedRooms.Select(Room.Get).Where(r => r != null);
            var allRooms = new HashSet<Room> { room };
            foreach (var neighbor in connectedRooms)
                allRooms.Add(neighbor);

            // Check each room using optimized lookup  
            foreach (var roomToCheck in allRooms)
            {
                if (Generator.TryGetFromRoom(roomToCheck, out List<Generator>? generators))
                {
                    if (generators.Any(gen => gen.Engaged))
                        return false;
                }
            }

            return true;
        }
        /// <summary>  
        /// Enables and flickers lights in a room and all its neighboring rooms.  
        /// </summary>  
        /// <param name="room">The LabAPI room to light up and flicker.</param>  
        public static void EnableAndFlickerRoomAndNeighborLights(Room room)
        {
            if (room == null)
            {
                Library_ExiledAPI.LogWarn("EnableAndFlickerRoomAndNeighborLights", "Room instance is null");
                return;
            }

            // Get neighbor rooms using LabAPI  
            var neighborRooms = room.ConnectedRooms.Select(Room.Get).Where(r => r != null);
            var roomSet = new HashSet<Room> { room };
            foreach (var neighbor in neighborRooms)
                roomSet.Add(neighbor);

            foreach (var r in roomSet)
            {
                Library_ExiledAPI.LogDebug("EnableAndFlickerRoomLights", $"Flickering lights in {(r == room ? "the room" : "neighbor room")}: {r.Name}");

                Library_ExiledAPI.ToExiledRoom(r).TurnOffLights(Config.BlackoutConfig.FlickerDuration); // Turn them On after Flicker Duration via Exiled adapter.
                // Enable lights and flicker using LabAPI  
                //foreach (var lightController in r.AllLightControllers)
                //{
                //    lightController.LightsEnabled = true;
                //    lightController.FlickerLights(Config.BlackoutConfig.FlickerDuration);
                //}
            }
        }

        /// <summary>  
        /// Attempts a blackout event in a room and all its neighboring rooms. Increments blackout stacks by 1 if successful.  
        /// </summary>  
        /// <param name="room">The LabAPI room to blackout.</param>  
        /// <param name="blackoutDurationBase">Minimum time in seconds that the blackout occurs.</param>   
        public static void DisableRoomAndNeighborLights(Room room, float blackoutDurationBase = 13f)
        {
            if (room == null)
            {
                Library_ExiledAPI.LogWarn("DisableRoomAndNeighborLights", "Room instance is null");
                return;
            }

            // Get neighbor rooms using LabAPI  
            var neighborRooms = room.ConnectedRooms.Select(Room.Get).Where(r => r != null);
            var roomSet = new HashSet<Room> { room };
            foreach (var neighbor in neighborRooms)
                roomSet.Add(neighbor);

            bool attemptFirstSuccess = false;
            foreach (var r in roomSet)
            {
                Library_ExiledAPI.LogDebug("DisableAndFlickerRoomAndNeighborLights", $"Flickering lights in {(r == room ? "the room" : "neighbor room")}: {r.Name}");

                float blackoutDuration = blackoutDurationBase + ((Config.BlackoutConfig.DurationMin + Config.BlackoutConfig.DurationMax) / 2f);

                bool attemptResult = Methods.AttemptRoomBlackout(r, blackoutDuration, isCassieSilent: true, isForced: true);
                if (attemptResult)
                {
                    if (!attemptFirstSuccess)
                    {
                        Methods.IncrementBlackoutStack();
                        Timing.CallDelayed(blackoutDuration, () => Methods.DecrementBlackoutStack());
                        attemptFirstSuccess = true;
                    }
                }
            }
        }

        /// <summary>
        /// Determines if the given player is in a dark room (lights off).
        /// </summary>
        public static bool IsPlayerInDarkRoom(Player player)
        {
            var room = player.Room;
            return room?.AllLightControllers.Any() == true &&
                   room.AllLightControllers.All(lc => !lc.LightsEnabled);
        }

        #endregion

        #region Elevator Utilities  
          
        /// <summary>  
        /// Gets all elevators in a specific zone.  
        /// </summary>  
        /// <param name="zone">The facility zone to search.</param>  
        /// <returns>Collection of elevators in the specified zone.</returns>  
        public static IEnumerable<Elevator> GetElevatorsInZone(FacilityZone zone)  
        {  
            return Elevators.Where(elevator =>   
                elevator.Rooms.Any(room => Room.Get(room)?.Zone == zone));  
        }  

        /// <summary>  
        /// Handles connected elevators based on percentage chance.  
        /// </summary>  
        /// <param name="room">The room to check for connected elevators.</param>  
        /// <param name="affectChance">Percentage chance (0-100) to affect each elevator.</param>  
        /// <param name="elevatorAction">Action to perform on affected elevators.</param>  
        private static void HandleConnectedElevators(Room room, float affectChance, Action<Elevator> elevatorAction)  
        {  
            if (affectChance <= 0f || affectChance > 100f) return;  
          
            var connectedElevators = GetElevatorsConnectedToRoom(room);  
              
            foreach (var elevator in connectedElevators)  
            {  
                // Roll percentage chance for each elevator  
                float roll = UnityEngine.Random.Range(0f, 100f);  
                if (roll <= affectChance)  
                {  
                    elevatorAction(elevator);  
                    Library_ExiledAPI.LogDebug("HandleConnectedElevators",   
                        $"Affected elevator (roll: {roll:F1}% <= {affectChance}%)");  
                }  
                else  
                {  
                    Library_ExiledAPI.LogDebug("HandleConnectedElevators",   
                        $"Skipped elevator (roll: {roll:F1}% > {affectChance}%)");  
                }  
            }  
        }  
          
        /// <summary>  
        /// Checks if any elevator is currently moving between the specified rooms.  
        /// </summary>  
        /// <param name="room">The room to check elevator activity for.</param>  
        /// <returns>True if an elevator is active in or connected to the room.</returns>  
        public static bool IsElevatorActiveInRoom(Room room)  
        {  
            if (room == null) return false;  
              
            return Elevators.Any(elevator =>   
                elevator.Rooms.Contains(room.Base) && elevator.IsMoving);  
        }  
          
        /// <summary>  
        /// Gets all elevators connected to a specific room.  
        /// </summary>  
        /// <param name="room">The room to find connected elevators for.</param>  
        /// <returns>Collection of elevators connected to the room.</returns>  
        public static IEnumerable<Elevator> GetElevatorsConnectedToRoom(Room room)  
        {  
            if (room == null) return Enumerable.Empty<Elevator>();  
              
            return Elevators.Where(elevator => elevator.Rooms.Contains(room.Base));  
        }  
          
        /// <summary>  
        /// Attempts to lock all elevators in a zone for security purposes.  
        /// </summary>  
        /// <param name="zone">The zone to lock elevators in.</param>  
        /// <param name="lockReason">The reason for locking.</param>  
        public static void LockElevatorsInZone(FacilityZone zone, DoorLockReason lockReason = DoorLockReason.AdminCommand)  
        {  
            var elevatorsInZone = GetElevatorsInZone(zone);  
              
            foreach (var elevator in elevatorsInZone)  
            {  
                foreach (var door in elevator.Doors)  
                {  
                    door.Lock(lockReason, true);  
                }  
                  
                Library_ExiledAPI.LogDebug("LockElevatorsInZone",   
                    $"Locked elevator doors in zone {zone}");  
            }  
        }  
          
        /// <summary>  
        /// Unlocks all elevators in a zone.  
        /// </summary>  
        /// <param name="zone">The zone to unlock elevators in.</param>  
        public static void UnlockElevatorsInZone(FacilityZone zone)  
        {  
            var elevatorsInZone = GetElevatorsInZone(zone);  
              
            foreach (var elevator in elevatorsInZone)  
            {  
                foreach (var door in elevator.Doors)  
                {  
                    door.Unlock();  
                }  
                  
                Library_ExiledAPI.LogDebug("UnlockElevatorsInZone",   
                    $"Unlocked elevator doors in zone {zone}");  
            }  
        }  
          
        /// <summary>  
        /// Determines if a player is currently in an elevator.  
        /// </summary>  
        /// <param name="player">The player to check.</param>  
        /// <returns>True if the player is in an elevator room.</returns>  
        public static bool IsPlayerInElevator(Player player)  
        {  
            if (player?.Room == null) return false;  
              
            return Elevators.Any(elevator => elevator.Rooms.Contains(player.Room.Base));  
        }  
          
        #endregion

        #region Utilities

        /// <summary>  
        /// Checks if a player is human and not holding an active light source.  
        /// </summary>  
        /// <param name="player">The player to check.</param>  
        /// <returns>True if the player is human without an active light source, false otherwise.</returns>  
        //public static bool IsHumanWithoutLight(Player player)
        //{
        //    try
        //    {
        //        if (!player.IsHuman) return false;

        //        // Check if player has weapon flashlight enabled  
        //        // The specific limitation is weapon flashlight detection. While LabAPI provides events for when weapon flashlights are toggled, it doesn't expose a direct property on FirearmItem to check the current flashlight state.
        //        if (player.CurrentItem is FirearmItem firearm && firearm.FlashlightEnabled)
        //            return false;

        //        // Check if player is holding an active light item  
        //        if (player.CurrentItem is LightItem lightItem && lightItem.IsEmitting)
        //            return false;

        //        return true;
        //    }
        //    catch (Exception ex)
        //    {
        //        Library_ExiledAPI.LogError("Helpers.IsHumanWithoutLight", $"Failed to check player {player?.UserId ?? "null"} ({player?.Nickname ?? "unknown"}): {ex.Message}");
        //        return false;
        //    }
        //}



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

        public static FacilityZone? ConvertToLabApiZone(Exiled.API.Enums.ZoneType exiledZone)
        {
            return exiledZone switch
            {
                Exiled.API.Enums.ZoneType.LightContainment => FacilityZone.LightContainment,
                Exiled.API.Enums.ZoneType.HeavyContainment => FacilityZone.HeavyContainment,
                Exiled.API.Enums.ZoneType.Entrance => FacilityZone.Entrance,
                Exiled.API.Enums.ZoneType.Surface => FacilityZone.Surface,
                _ => null
            };
        }

        #endregion
    }
}
