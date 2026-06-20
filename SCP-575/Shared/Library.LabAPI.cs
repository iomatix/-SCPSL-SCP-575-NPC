namespace SCP_575.Shared
{
    using Cassie;
    using Interactables.Interobjects.DoorUtils;
    using LabApi.Features.Console;
    using LabApi.Features.Wrappers;
    using MapGeneration;
    using MEC;
    using SCP_575.Npc;
    using SCP_575.Shared;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEngine;

    /// <summary>
    /// Provides highly optimized utilities and adapters for interacting with LabAPI in the SCP-575 context.
    /// </summary>
    public sealed class LibraryLabAPI
    {
        private readonly Plugin _plugin;

        public LibraryLabAPI(Plugin plugin)
        {
            _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
        }

        #region Properties

        public Methods Methods => _plugin.Npc.Methods;
        public Config Config => _plugin.Config;
        public IReadOnlyCollection<Player> Players => Player.List;
        public IReadOnlyCollection<Room> Rooms => Room.List;
        public IReadOnlyCollection<Elevator> Elevators => Elevator.List;
        public IReadOnlyCollection<Tesla> Teslas => Tesla.List;

        #endregion

        #region Logging

        public void LogDebug(string moduleId, string message)
        {
            if (_plugin?.Config?.Debug == false) return;
            LabApi.Features.Console.Logger.Debug($"[{moduleId}] {message}");
        }

        public static void LogWarn(string moduleId, string message) => LabApi.Features.Console.Logger.Warn($"[{moduleId}] {message}");
        public static void LogInfo(string moduleId, string message) => LabApi.Features.Console.Logger.Info($"[{moduleId}] {message}");
        public static void LogError(string moduleId, string message) => LabApi.Features.Console.Logger.Error($"[{moduleId}] {message}");

        #endregion

        #region Getters

        public Elevator GetRandomElevator() => Map.GetRandomElevator();
        public Room GetRoomAtPosition(Vector3 position) => Room.GetRoomAtPosition(position);
        public Player GetPlayer(ReferenceHub referenceHub) => Player.Get(referenceHub);
        public Ragdoll GetRagdoll(PlayerRoles.Ragdolls.BasicRagdoll ragdoll) => Ragdoll.Get(ragdoll);

        #endregion

        #region Room Utilities

        public void TurnOffRoomLights(Room room, float duration, float elevatorAffectChance = 0f)
        {
            if (room == null) return;

            // FIXED: Using foreach loop since AllLightControllers implements IEnumerable and does not support indexing.
            foreach (var controller in room.AllLightControllers)
            {
                controller.FlickerLights(duration);
            }

            HandleElevatorsForRoom(room, elevatorAffectChance, duration, elevator =>
            {
                elevator.LockAllDoors();
                var coroutine = Timing.CallDelayed(duration, () => elevator.UnlockAllDoors());
                coroutine.Tag = CoroutineTags.Temp;
            });

            if (Config.Debug) LogDebug(nameof(TurnOffRoomLights), $"Lights turned off in room {room.Name} for {duration} seconds.");
        }

        public bool IsRoomFreeOfEngagedGenerators(Room room)
        {
            // FIXED: Removed nullable reference type marker from out declaration to prevent syntax compilation crashes.
            if (!Generator.TryGetFromRoom(room, out List<Generator> generators) || generators == null)
                return true;

            foreach (var gen in generators)
            {
                if (gen.Engaged) return false;
            }
            return true;
        }

        public bool IsRoomAndNeighborsFreeOfEngagedGenerators(Room room)
        {
            if (room == null) return false;
            if (!IsRoomFreeOfEngagedGenerators(room)) return false;

            // FIXED: Reverted to foreach sequence to properly extract identifiers from HashSet without indexers.
            foreach (var neighborIdentifier in room.ConnectedRooms)
            {
                var neighborRoom = Room.Get(neighborIdentifier);
                if (neighborRoom != null && !IsRoomFreeOfEngagedGenerators(neighborRoom))
                    return false;
            }

            return true;
        }

        public void EnableAndFlickerRoomAndNeighborLights(Room room, float elevatorAffectChance = 0f)
        {
            if (room == null) return;

            foreach (LightsController controller in room.AllLightControllers)
            {
                controller.FlickerLights(Config.BlackoutConfig.FlickerDuration);
            }

            // FIXED: Utilizing explicit foreach iterations to resolve HashSet elements securely.
            foreach (var neighborIdentifier in room.ConnectedRooms)
            {
                var r = Room.Get(neighborIdentifier);
                if (r == null) continue;

                foreach (LightsController controller in r.AllLightControllers)
                {
                    controller.FlickerLights(Config.BlackoutConfig.FlickerDuration);
                }

                HandleElevatorsForRoom(r, elevatorAffectChance, Config.BlackoutConfig.FlickerDuration, elevator =>
                {
                    elevator.LockAllDoors();
                    var coroutine = Timing.CallDelayed(Config.BlackoutConfig.FlickerDuration, () => elevator.UnlockAllDoors());
                    coroutine.Tag = CoroutineTags.Temp;
                });
            }
        }

        public void DisableRoomAndNeighborLights(Room room, float blackoutDurationBase = 13f, bool forced = false)
        {
            if (room == null) return;

            bool attemptFirstSuccess = false;
            float blackoutDuration = blackoutDurationBase + UnityEngine.Random.Range(Config.BlackoutConfig.DurationMin, Config.BlackoutConfig.DurationMax);

            if (Methods.AttemptRoomBlackout(room, blackoutDuration, silent: true, forced: forced))
            {
                Methods.IncrementBlackoutStack();
                var coroutine = Timing.CallDelayed(blackoutDuration, () => Methods.DecrementBlackoutStack());
                coroutine.Tag = CoroutineTags.Temp;
                attemptFirstSuccess = true;
            }

            // FIXED: HashSet safe iteration tracking.
            foreach (var neighborIdentifier in room.ConnectedRooms)
            {
                var r = Room.Get(neighborIdentifier);
                if (r == null) continue;

                bool attemptResult = Methods.AttemptRoomBlackout(r, blackoutDuration, silent: true, forced: true);
                if (attemptResult && !attemptFirstSuccess)
                {
                    Methods.IncrementBlackoutStack();
                    var coroutine = Timing.CallDelayed(blackoutDuration, () => Methods.DecrementBlackoutStack());
                    coroutine.Tag = CoroutineTags.Temp;
                    attemptFirstSuccess = true;
                }
            }
        }

        public bool IsPlayerInDarkRoom(Player player)
        {
            var room = player?.Room;
            if (room == null) return false;

            var controllers = room.AllLightControllers;
            if (controllers == null) return false;

            bool hasControllers = false;
            foreach (var lc in controllers)
            {
                hasControllers = true;
                if (lc.LightsEnabled) return false;
            }

            return hasControllers;
        }

        #endregion

        #region Elevator Utilities

        public IEnumerable<Elevator> GetElevatorsInZone(FacilityZone zone)
        {
            foreach (var elevator in Elevator.List)
            {
                if (elevator.CurrentDestination.Rooms.Any(room => Room.Get(room.Base)?.Zone == zone))
                {
                    yield return elevator;
                }
            }
        }

        public bool IsElevatorActiveInRoom(Room room)
        {
            if (room == null) return false;

            foreach (var elevator in Elevator.List)
            {
                if (elevator.CurrentDestination.Rooms.Contains(room) &&
                    elevator.CurrentSequence != Interactables.Interobjects.ElevatorChamber.ElevatorSequence.Ready)
                {
                    return true;
                }
            }
            return false;
        }

        public IEnumerable<Elevator> GetElevatorsConnectedToRoom(Room room)
        {
            if (room == null) yield break;

            foreach (var elevator in Elevator.List)
            {
                if (elevator.CurrentDestination?.Rooms.Contains(room) == true)
                {
                    yield return elevator;
                }
            }
        }

        public void LockElevatorsInZone(FacilityZone zone, DoorLockReason lockReason = DoorLockReason.AdminCommand)
        {
            foreach (var elevator in GetElevatorsInZone(zone))
            {
                elevator.LockAllDoors();
            }
        }

        public void UnlockElevatorsInZone(FacilityZone zone)
        {
            foreach (var elevator in GetElevatorsInZone(zone))
            {
                elevator.UnlockAllDoors();
            }
        }

        public bool IsPlayerInExecutiveElevator(Player player)
        {
            var pRoom = player?.Room;
            if (pRoom == null) return false;

            foreach (var elevator in Elevator.List)
            {
                if (elevator.CurrentDestination.Rooms.Contains(pRoom))
                    return true;
            }
            return false;
        }

        #endregion

        #region Cassie Methods

        public void CassieClear() => Announcer.Clear();

        public void CassieGlitchyMessage(string message)
        {
            message = CassieGlitchifier.Glitchify(message, Config.CassieConfig.GlitchChance / 100, Config.CassieConfig.JamChance / 100);
            Announcer.Message($"pitch_1.15 {message}", string.Empty, playBackground: false);
        }

        public void CassieMessage(string message) =>
            Announcer.Message($"pitch_0.95 {message}", playBackground: false, priority: Plugin.Singleton.Config.CassieConfig.CassieMessagePriority);

        #endregion

        #region Adapters

        public Player? ToLabAPIPlayer(Exiled.API.Features.Player? exiledPlayer) =>
            exiledPlayer?.ReferenceHub == null ? null : Player.Get(exiledPlayer.ReferenceHub);

        public Ragdoll? ToLabAPIRagdoll(Exiled.API.Features.Ragdoll? exiledRagdoll) =>
            exiledRagdoll?.Base == null ? null : Ragdoll.Get(exiledRagdoll.Base);

        public Room? ToLabApiRoom(Exiled.API.Features.Room? exiledRoom)
        {
            // FIXED: Using highly optimized square distance delta scanning.
            // Bypasses missing .Base and netId definitions entirely, ensuring robust multi-framework cross-compatibility.
            if (exiledRoom == null) return null;
            Vector3 targetPos = exiledRoom.Position;

            foreach (var r in Rooms)
            {
                if (Vector3.SqrMagnitude(r.Position - targetPos) < 0.05f)
                    return r;
            }
            return null;
        }

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

        private void HandleElevatorsForRoom(Room room, float affectChance, float duration, Action<Elevator> elevatorAction)
        {
            if (affectChance <= 0f || affectChance > 100f) return;

            foreach (var elevator in GetElevatorsConnectedToRoom(room))
            {
                float roll = UnityEngine.Random.Range(0f, 100f);
                if (roll <= affectChance)
                {
                    elevatorAction(elevator);
                }
            }
        }

        #endregion
    }
}