using LabApi.Events.Arguments.ServerEvents;
using LabApi.Events.CustomHandlers;
using LabApi.Extensions;
using LabApi.Extensions.Misc;
using LabApi.Features.Wrappers;
using MapGeneration;
using MEC;
using SCP_575.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Logger = LabApi.Extensions.Misc.iLogger;

namespace SCP_575.Handlers
{
    /// <summary>
    /// Handles physical facility infrastructure layout transformations and controls item injection algorithms.
    /// </summary>
    public class MapHandler : CustomEventsHandler
    {
        #region Fields
        private readonly Plugin _plugin;
        #endregion

        #region Constructor
        public MapHandler(Plugin plugin)
        {
            _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
        }
        #endregion

        #region Lifecycle Management
        public override void OnServerRoundEnded(RoundEndedEventArgs ev) => Clean();
        public override void OnServerWaitingForPlayers() => Clean();

        private void Clean()
        {
            // Fluent API Alignment: Direct string extension utilization to evict trailing routines
            CoroutineTags.MapCoroutines.KillCoroutine();
        }
        #endregion

        #region Public Dispatchers
        /// <summary>
        /// Public entrypoint invoked exclusively by the central NPC Lifecycle Orchestrator.
        /// </summary>
        public void ExecuteFlashlightDistribution()
        {
            if (_plugin.FlashlightSpawn is null || !_plugin.FlashlightSpawn.IsEnabled)
                return;

            Timing.RunCoroutine(SpawnFlashlightsPipeline(), CoroutineTags.MapCoroutines);
        }
        #endregion

        #region Distribution Engine
        /// <summary>
        /// Drives the item injection sequence across valid zone boundaries.
        /// </summary>
        private IEnumerator<float> SpawnFlashlightsPipeline()
        {
            yield return Timing.WaitForSeconds(2.5f);

            try
            {
                // Fluent API Alignment: Utilizing standard global Room collection registry directly
                var allRooms = Room.List;
                if (allRooms is null || !allRooms.Any())
                {
                    Logger.Error(nameof(MapHandler), "Asynchronous abort: Room registry data is blank at T+2.5s.");
                    yield break;
                }

                int totalSpawned = 0;

                foreach (Room room in allRooms)
                {
                    if (room is null || room.Name is RoomName.Pocket)
                        continue;

                    float spawnChance = GetZoneSpawnChance(room.Zone);
                    if (spawnChance <= 0f)
                        continue;

                    // Fluent API Upgrade: Seamless thread-safe probability roll success check directly from primitives
                    if (!spawnChance.RollSuccess())
                        continue;

                    Vector3 spawnPosition = room.Position + new Vector3(0f, 0.6f, 0f);

                    Pickup flashlightPickup = Pickup.Create(ItemType.Flashlight, spawnPosition, Quaternion.identity);
                    if (flashlightPickup is not null)
                    {
                        Timing.RunCoroutine(ApplyDelayedPhysicsPush(flashlightPickup), CoroutineTags.MapCoroutines);
                        totalSpawned++;

                        Logger.Debug(nameof(MapHandler), $"Injected flashlight into {room.Name} ({room.Zone}).", _plugin.Debug);
                    }
                }

                Logger.Info(nameof(MapHandler), $"Orchestrated flashlight injection complete. Total spawned: {totalSpawned}");
            }
            catch (Exception ex)
            {
                Logger.Error(nameof(MapHandler), $"Pipeline collapsed: {ex.Message}");
            }
        }

        private float GetZoneSpawnChance(FacilityZone zone)
        {
            var config = _plugin.FlashlightSpawn;
            return zone switch
            {
                FacilityZone.LightContainment => config.ChanceLight,
                FacilityZone.HeavyContainment => config.ChanceHeavy,
                FacilityZone.Entrance => config.ChanceEntrance,
                FacilityZone.Surface => config.ChanceSurface,
                _ => config.ChanceOther
            };
        }

        /// <summary>
        /// Forces a single-frame deferral to ensure the engine registers native transform weights before force application.
        /// </summary>
        private IEnumerator<float> ApplyDelayedPhysicsPush(Pickup pickup)
        {
            yield return Timing.WaitForOneFrame;

            if (pickup is null || pickup.IsDestroyed || !pickup.IsSpawned)
                yield break;

            Rigidbody rb = pickup.Rigidbody;
            if (rb is not null)
            {
                rb.isKinematic = false;
            }

            try
            {
                // Fluent API Upgrade: Leverage the single point of truth physics manipulation method securely
                float dynamicMagnitude = SafeRandom.Range(2.0f, 4.2f);
                pickup.ApplyKineticBlast(dynamicMagnitude, 3.5f);
            }
            catch (Exception ex)
            {
                Logger.Error(nameof(MapHandler), $"Failed to apply synchronized physics to pickup {pickup.Serial}: {ex.Message}");
            }
        }
        #endregion
    }
}