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
            CoroutineTags.MapCoroutines.Kill();
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
            // Yield statements must exist strictly outside of try-catch blocks
            yield return Timing.WaitForSeconds(2.5f);

            List<Pickup> spawnedPickups = new List<Pickup>();
            int totalSpawned = 0;
            bool shouldAbort = false;

            // Stage 1: Synchronous item generation enclosed securely in try-catch
            try
            {
                var allRooms = Room.List;
                if (allRooms is null || allRooms.Count == 0)
                {
                    Logger.Error(nameof(MapHandler), "Asynchronous abort: Room registry data is blank at T+2.5s.");
                    shouldAbort = true;
                }
                else
                {
                    foreach (Room room in allRooms)
                    {
                        if (room is null || room.Name is RoomName.Pocket)
                            continue;

                        float spawnChance = GetZoneSpawnChance(room.Zone);
                        if (!spawnChance.RollChance())
                            continue;

                        Vector3 spawnPosition = room.Position + new Vector3(0f, 0.6f, 0f);

                        Pickup flashlightPickup = Pickup.Create(ItemType.Flashlight, spawnPosition, Quaternion.identity);
                        if (flashlightPickup is not null)
                        {
                            spawnedPickups.Add(flashlightPickup);
                            totalSpawned++;

                            Logger.Debug(nameof(MapHandler), $"Injected flashlight into {room.Name} ({room.Zone}).", _plugin.Debug);
                        }
                    }

                    Logger.Info(nameof(MapHandler), $"Orchestrated flashlight injection complete. Total spawned: {totalSpawned}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(nameof(MapHandler), $"Pipeline spawning stage collapsed: {ex.Message}");
            }

            // Conditional flow state controls evaluated outside protected blocks to allow yield execution
            if (shouldAbort || spawnedPickups.Count == 0)
                yield break;

            // Frame deferral execution executed safely outside try-catch boundaries
            yield return Timing.WaitForOneFrame;

            // Stage 2: Batch processing physics matrix updates enclosed in independent try-catch
            try
            {
                for (int i = 0; i < spawnedPickups.Count; i++)
                {
                    Pickup pickup = spawnedPickups[i];
                    if (pickup is null || pickup.IsDestroyed || !pickup.IsSpawned)
                        continue;

                    Rigidbody rb = pickup.Rigidbody;
                    if (rb is not null)
                    {
                        rb.isKinematic = false;
                    }

                    try
                    {
                        float dynamicMagnitude = SafeRandom.Range(2.0f, 4.2f);
                        pickup.ApplyKineticBlast(dynamicMagnitude, 3.5f);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(nameof(MapHandler), $"Failed to apply synchronized physics to pickup {pickup.Serial}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(nameof(MapHandler), $"Pipeline physics stage collapsed: {ex.Message}");
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
        #endregion
    }
}