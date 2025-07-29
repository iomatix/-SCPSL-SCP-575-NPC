namespace SCP_575.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using LabApi.Features.Wrappers;
    using MapGeneration;
    using MEC;
    using SCP_575.ConfigObjects;
    using SCP_575.Npc;
    using UnityEngine;

    /// <summary>
    /// Provides utilities and adapters for interacting with LabAPI in the SCP-575 context.
    /// Manages Cassie messaging, light control, elevator operations, and conversions between LabAPI and Exiled types.
    /// This class is sealed to prevent inheritance, ensuring it is used as a controlled wrapper library.
    /// </summary>
    public sealed class LibraryLabAPI
    {
        private readonly Plugin _plugin;

        /// <summary>
        /// Initializes a new instance of the <see cref="LibraryLabAPI"/> class.
        /// Typically instantiated once in the plugin lifecycle with the SCP-575 plugin instance.
        /// </summary>
        /// <param name="plugin">The SCP-575 plugin instance.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="plugin"/> is null.</exception>
        public LibraryLabAPI(Plugin plugin)
        {
            _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
        }

        #region Properties

        /// <summary>
        /// Gets the NPC methods section of the plugin.
        /// </summary>
        public Methods Methods => _plugin.Npc.Methods;

        /// <summary>
        /// Gets the root plugin configuration object.
        /// </summary>
        public Config Config => _plugin.Config;

        /// <summary>
        /// Gets a read-only collection of all players (LabAPI wrapped).
        /// </summary>
        public IReadOnlyCollection<Player> Players => Player.List;

        /// <summary>
        /// Gets a read-only collection of all rooms (LabAPI wrapped).
        /// </summary>
        public IReadOnlyCollection<Room> Rooms => Room.List;

        /// <summary>
        /// Gets a read-only collection of all elevators (LabAPI wrapped).
        /// </summary>
        public IReadOnlyCollection<Elevator> Elevators => Elevator.List;

        /// <summary>
        /// Gets a read-only collection of all Tesla gates.
        /// </summary>
        public IReadOnlyCollection<Tesla> Teslas => Tesla.List;

        #endregion

        #region Getters

        /// <summary>
        /// Gets a random elevator from the facility.
        /// </summary>
        /// <returns>A random <see cref="Elevator"/> instance.</returns>
        public Elevator GetRandomElevator() => Map.GetRandomElevator();

        /// <summary>
        /// Gets the room at the specified position.
        /// </summary>
        /// <param name="position">The world position to query.</param>
        /// <returns>The <see cref="Room"/> at the specified position, or null if none found.</returns>
        public Room GetRoomAtPosition(Vector3 position) => Room.GetRoomAtPosition(position);

        /// <summary>
        /// Gets a LabAPI player wrapper from a reference hub.
        /// </summary>
        /// <param name="referenceHub">The reference hub to convert.</param>
        /// <returns>The corresponding <see cref="Player"/> wrapper.</returns>
        public Player GetPlayer(ReferenceHub referenceHub) => Player.Get(referenceHub);

        /// <summary>
        /// Gets a LabAPI ragdoll wrapper from a native ragdoll object.
        /// </summary>
        /// <param name="ragdoll">The native ragdoll to convert.</param>
        /// <returns>The corresponding <see cref="Ragdoll"/> wrapper.</returns>
        public Ragdoll GetRagdoll(PlayerRoles.Ragdolls.BasicRagdoll ragdoll) => Ragdoll.Get(ragdoll);

        #endregion

        #region Room Utilities

        /// <summary>
        /// Turns off lights in a specific room for the specified duration.
        /// </summary>
        /// <param name="room">The room to turn lights off in.</param>
        /// <param name="duration">Duration in seconds to keep lights off.</param>
        /// <param name="elevatorAffectChance">Percentage chance (0-100) that connected elevators will be affected.</param>
        public void TurnOffRoomLights(Room room, float duration, float elevatorAffectChance = 0f)
        {
            if (room == null)
            {
                Library_ExiledAPI.LogWarn(nameof(TurnOffRoomLights), "Room instance is null");
                return;
            }

            Library_ExiledAPI.ToExiledRoom(room).TurnOffLights(duration);
            HandleElevatorsForRoom(room, elevatorAffectChance, duration, elevator =>
            {
                elevator.LockAllDoors();
                Library_ExiledAPI.LogDebug(nameof(TurnOffRoomLights), "Locked elevator doors due to room blackout");
                Timing.CallDelayed(duration, () => elevator.UnlockAllDoors());
            });

            Library_ExiledAPI.LogDebug(nameof(TurnOffRoomLights), $"Lights turned off in room {room.Name} for {duration} seconds.");
        }

        /// <summary>
        /// Checks if the given room contains no engaged generators.
        /// </summary>
        /// <param name="room">The room to check.</param>
        /// <returns>True if the room has no engaged generators; otherwise, false.</returns>
        public bool IsRoomFreeOfEngagedGenerators(Room room)
        {
            return !Generator.TryGetFromRoom(room, out List<Generator>? generators) || !generators.Any(gen => gen.Engaged);
        }

        /// <summary>
        /// Checks if the specified room and all its neighbors are free of engaged generators.
        /// </summary>
        /// <param name="room">The room to check.</param>
        /// <returns>True if the room and its neighbors have no engaged generators; otherwise, false.</returns>
        public bool IsRoomAndNeighborsFreeOfEngagedGenerators(Room room)
        {
            if (room == null) return false;

            var roomSet = GetRoomAndNeighbors(room);
            return roomSet.All(roomToCheck => IsRoomFreeOfEngagedGenerators(roomToCheck));
        }

        /// <summary>
        /// Enables and flickers lights in a room and all its neighboring rooms.
        /// </summary>
        /// <param name="room">The room to light up and flicker.</param>
        /// <param name="elevatorAffectChance">Percentage chance (0-100) that connected elevators will be affected.</param>
        public void EnableAndFlickerRoomAndNeighborLights(Room room, float elevatorAffectChance = 0f)
        {
            if (room == null)
            {
                Library_ExiledAPI.LogWarn(nameof(EnableAndFlickerRoomAndNeighborLights), "Room instance is null");
                return;
            }

            var roomSet = GetRoomAndNeighbors(room);
            foreach (var r in roomSet)
            {
                Library_ExiledAPI.LogDebug(nameof(EnableAndFlickerRoomAndNeighborLights), 
                    $"Flickering lights in {(r == room ? "the room" : "neighbor room")}: {r.Name}");

                Library_ExiledAPI.ToExiledRoom(r).TurnOffLights(Config.BlackoutConfig.FlickerDuration);
                HandleElevatorsForRoom(r, elevatorAffectChance, 0.5f, elevator =>
                {
                    elevator.LockAllDoors();
                    Timing.CallDelayed(0.5f, () => elevator.UnlockAllDoors());
                });
            }
        }

        /// <summary>
        /// Attempts a blackout event in a room and all its neighboring rooms, incrementing blackout stacks if successful.
        /// </summary>
        /// <param name="room">The room to blackout.</param>
        /// <param name="blackoutDurationBase">Minimum time in seconds for the blackout.</param>
        /// <param name="elevatorAffectChance">Percentage chance (0-100) that connected elevators will be affected.</param>
        public void DisableRoomAndNeighborLights(Room room, float blackoutDurationBase = 13f, float elevatorAffectChance = 0f)
        {
            if (room == null)
            {
                Library_ExiledAPI.LogWarn(nameof(DisableRoomAndNeighborLights), "Room instance is null");
                return;
            }

            var roomSet = GetRoomAndNeighbors(room);
            bool attemptFirstSuccess = false;
            float blackoutDuration = blackoutDurationBase + ((Config.BlackoutConfig.DurationMin + Config.BlackoutConfig.DurationMax) / 2f);

            foreach (var r in roomSet)
            {
                Library_ExiledAPI.LogDebug(nameof(DisableRoomAndNeighborLights), 
                    $"Flickering lights in {(r == room ? "the room" : "neighbor room")}: {r.Name}");

                bool attemptResult = Methods.AttemptRoomBlackout(r, blackoutDuration, isCassieSilent: true, isForced: true);
                if (attemptResult && !attemptFirstSuccess)
                {
                    Methods.IncrementBlackoutStack();
                    Timing.CallDelayed(blackoutDuration, () => Methods.DecrementBlackoutStack());
                    attemptFirstSuccess = true;
                }

                if (attemptResult)
                {
                    HandleElevatorsForRoom(r, elevatorAffectChance, blackoutDuration, elevator =>
                    {
                        elevator.LockAllDoors();
                        Library_ExiledAPI.LogDebug(nameof(DisableRoomAndNeighborLights), "Locked elevator due to blackout");
                        Timing.CallDelayed(blackoutDuration, () => elevator.UnlockAllDoors());
                    });
                }
            }
        }

        /// <summary>
        /// Determines if the given player is in a dark room (lights off).
        /// </summary>
        /// <param name="player">The player to check.</param>
        /// <returns>True if the player is in a room with all lights off; otherwise, false.</returns>
        public bool IsPlayerInDarkRoom(Player player)
        {
            return player?.Room?.AllLightControllers.Any() == true &&
                   player.Room.AllLightControllers.All(lc => !lc.LightsEnabled);
        }

        #endregion

        #region Elevator Utilities

        /// <summary>
        /// Gets all elevators in a specific facility zone.
        /// </summary>
        /// <param name="zone">The facility zone to search.</param>
        /// <returns>A collection of elevators in the specified zone.</returns>
        public IEnumerable<Elevator> GetElevatorsInZone(FacilityZone zone)
        {
            return Elevators.Where(elevator => elevator.Rooms.Any(room => Room.Get(room)?.Zone == zone));
        }

        /// <summary>
        /// Checks if any elevator is currently moving between the specified rooms.
        /// </summary>
        /// <param name="room">The room to check elevator activity for.</param>
        /// <returns>True if an elevator is active in or connected to the room; otherwise, false.</returns>
        public bool IsElevatorActiveInRoom(Room room)
        {
            return room != null && Elevators.Any(elevator => elevator.Rooms.Contains(room.Base) && elevator.IsMoving);
        }

        /// <summary>
        /// Gets all elevators connected to a specific room.
        /// </summary>
        /// <param name="room">The room to find connected elevators for.</param>
        /// <returns>A collection of elevators connected to the room.</returns>
        public IEnumerable<Elevator> GetElevatorsConnectedToRoom(Room room)
        {
            return room == null ? Enumerable.Empty<Elevator>() : Elevators.Where(elevator => elevator.Rooms.Contains(room.Base));
        }

        /// <summary>
        /// Locks all elevators in a specified zone for security purposes.
        /// </summary>
        /// <param name="zone">The zone to lock elevators in.</param>
        /// <param name="lockReason">The reason for locking.</param>
        public void LockElevatorsInZone(FacilityZone zone, DoorLockReason lockReason = DoorLockReason.AdminCommand)
        {
            foreach (var elevator in GetElevatorsInZone(zone))
            {
                foreach (var door in elevator.Doors)
                {
                    door.Lock(lockReason, true);
                }
                Library_ExiledAPI.LogDebug(nameof(LockElevatorsInZone), $"Locked elevator doors in zone {zone}");
            }
        }

        /// <summary>
        /// Unlocks all elevators in a specified zone.
        /// </summary>
        /// <param name="zone">The zone to unlock elevators in.</param>
        public void UnlockElevatorsInZone(FacilityZone zone)
        {
            foreach (var elevator in GetElevatorsInZone(zone))
            {
                foreach (var door in elevator.Doors)
                {
                    door.Unlock();
                }
                Library_ExiledAPI.LogDebug(nameof(UnlockElevatorsInZone), $"Unlocked elevator doors in zone {zone}");
            }
        }

        /// <summary>
        /// Determines if a player is currently in an elevator.
        /// </summary>
        /// <param name="player">The player to check.</param>
        /// <returns>True if the player is in an elevator room; otherwise, false.</returns>
        public bool IsPlayerInElevator(Player player)
        {
            return player?.Room != null && Elevators.Any(elevator => elevator.Rooms.Contains(player.Room.Base));
        }

        #endregion

        #region Cassie Methods

        /// <summary>
        /// Clears all currently queued Cassie messages.
        /// </summary>
        public void CassieClear() => Cassie.Clear();

        /// <summary>
        /// Sends a glitched Cassie message with configured glitch and jam chances.
        /// </summary>
        /// <param name="message">The message to send.</param>
        public void CassieGlitchyMessage(string message) =>
            Cassie.GlitchyMessage($"pitch_1.15 {message}", Config.CassieConfig.GlitchChance / 100, Config.CassieConfig.JamChance / 100);

        /// <summary>
        /// Sends a clean Cassie message with no noise or subtitles.
        /// </summary>
        /// <param name="message">The message to send.</param>
        public void CassieMessage(string message) =>
            Cassie.Message($"pitch_0.95 {message}", isNoisy: false, isSubtitles: false, isHeld: false);

        #endregion

        #region Adapters

        /// <summary>
        /// Converts an Exiled player to a LabAPI player wrapper.
        /// </summary>
        /// <param name="exiledPlayer">The Exiled player to convert.</param>
        /// <returns>The corresponding <see cref="Player"/> wrapper, or null if conversion fails.</returns>
        public Player? ToLabAPIPlayer(Exiled.API.Features.Player? exiledPlayer) =>
            exiledPlayer?.ReferenceHub == null ? null : Player.Get(exiledPlayer.ReferenceHub);

        /// <summary>
        /// Converts an Exiled ragdoll to a LabAPI ragdoll wrapper.
        /// </summary>
        /// <param name="exiledRagdoll">The Exiled ragdoll to convert.</param>
        /// <returns>The corresponding <see cref="Ragdoll"/> wrapper, or null if conversion fails.</returns>
        public Ragdoll? ToLabAPIRagdoll(Exiled.API.Features.Ragdoll? exiledRagdoll) =>
            exiledRagdoll?.Base == null ? null : Ragdoll.Get(exiledRagdoll.Base);

        /// <summary>
        /// Converts an Exiled room to a LabAPI room using world position.
        /// </summary>
        /// <param name="exiledRoom">The Exiled room to convert.</param>
        /// <returns>The corresponding <see cref="Room"/> wrapper, or null if conversion fails.</returns>
        public Room? ToLabApiRoom(Exiled.API.Features.Room? exiledRoom) =>
            exiledRoom == null ? null : Rooms.FirstOrDefault(r => Helpers.Distance(r.Position, exiledRoom.Position) < 0.5f);

        /// <summary>
        /// Converts an Exiled zone to a LabAPI facility zone.
        /// </summary>
        /// <param name="exiledZone">The Exiled zone to convert.</param>
        /// <returns>The corresponding <see cref="FacilityZone"/>, or null if conversion fails.</returns>
        public FacilityZone? ConvertToLabApiZone(Exiled.API.Enums.ZoneType exiledZone) =>
            exiledZone switch
            {
                Exiled.API.Enums.ZoneType.LightContainment => FacilityZone.LightContainment,
                Exiled.API.Enums.ZoneType.HeavyContainment => FacilityZone.HeavyContainment,
                Exiled.API.Enums.ZoneType.Entrance => FacilityZone.Entrance,
                Exiled.API.Enums.ZoneType.Surface => FacilityZone.Surface,
                _ => null
            };

        #endregion

        #region Private Helpers

        /// <summary>
        /// Gets a set of the specified room and its connected neighbors.
        /// </summary>
        /// <param name="room">The room to include along with its neighbors.</param>
        /// <returns>A set of rooms including the specified room and its neighbors.</returns>
        private HashSet<Room> GetRoomAndNeighbors(Room room)
        {
            var roomSet = new HashSet<Room> { room };
            foreach (var neighbor in room.ConnectedRooms.Select(Room.Get).Where(r => r != null))
            {
                roomSet.Add(neighbor);
            }
            return roomSet;
        }

        /// <summary>
        /// Handles connected elevators for a room based on a percentage chance.
        /// </summary>
        /// <param name="room">The room to check for connected elevators.</param>
        /// <param name="affectChance">Percentage chance (0-100) to affect each elevator.</param>
        /// <param name="duration">Duration for elevator actions.</param>
        /// <param name="elevatorAction">Action to perform on affected elevators.</param>
        private void HandleElevatorsForRoom(Room room, float affectChance, float duration, Action<Elevator> elevatorAction)
        {
            if (affectChance <= 0f || affectChance > 100f) return;

            foreach (var elevator in GetElevatorsConnectedToRoom(room))
            {
                float roll = UnityEngine.Random.Range(0f, 100f);
                if (roll <= affectChance)
                {
                    elevatorAction(elevator);
                    Library_ExiledAPI.LogDebug(nameof(HandleElevatorsForRoom), 
                        $"Affected elevator (roll: {roll:F1}% <= {affectChance}%)");
                }
                else
                {
                    Library_ExiledAPI.LogDebug(nameof(HandleElevatorsForRoom), 
                        $"Skipped elevator (roll: {roll:F1}% > {affectChance}%)");
                }
            }
        }

        #endregion
    }
}
