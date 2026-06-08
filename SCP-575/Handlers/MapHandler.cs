namespace SCP_575.Handlers
{
    using LabApi.Events.CustomHandlers;
    using LabApi.Features.Wrappers;
    using MapGeneration;
    using MEC;
    using SCP_575.Shared;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEngine;

    public class MapHandler : CustomEventsHandler
    {
        private readonly Plugin _plugin;
        private readonly System.Random _random;

        public MapHandler(Plugin plugin)
        {
            _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
            _random = new System.Random();
        }

        /// <summary>
        /// Public entrypoint invoked exclusively by the central NPC Lifecycle Orchestrator.
        /// </summary>
        public void ExecuteFlashlightDistribution()
        {
            if (_plugin.Config?.FlashlightSpawnConfig == null || !_plugin.Config.FlashlightSpawnConfig.IsEnabled)
                return;

            // We still force an asynchronous delay because physical map generation (Unity structures)
            // occurs downstream from the framework logical initialization routines.
            Timing.RunCoroutine(SpawnFlashlightsPipeline(), CoroutineTags.MapCoroutines);
        }

        private IEnumerator<float> SpawnFlashlightsPipeline()
        {
            // Safeguard delay allowing Unity scene graph and network transforms to fully compile.
            yield return Timing.WaitForSeconds(2.5f);

            try
            {
                var allRooms = _plugin.LibraryLabAPI?.Rooms;
                if (allRooms == null || !allRooms.Any())
                {
                    LibraryLabAPI.LogError("MapHandler", "Asynchronous abort: Room registry data is blank at T+2.5s.");
                    yield break;
                }

                int totalSpawned = 0;

                foreach (Room room in allRooms)
                {
                    if (room == null || room.Name == RoomName.Pocket)
                        continue;

                    float spawnChance = GetZoneSpawnChance(room.Zone);
                    if (spawnChance <= 0f)
                        continue;

                    double roll = _random.NextDouble() * 100.0;
                    if (roll > spawnChance)
                        continue;

                    Vector3 spawnPosition = room.Position + new Vector3(0f, 0.5f, 0f);

                    var flashlightPickup = Pickup.Create(ItemType.Flashlight, spawnPosition, Quaternion.identity);
                    if (flashlightPickup != null)
                    {
                        ApplyRandomThrowForce(flashlightPickup);
                        totalSpawned++;

                        LibraryLabAPI.LogDebug("MapHandler", $"Injected flashlight into {room.Name} ({room.Zone}).");
                    }
                }

                LibraryLabAPI.LogInfo("MapHandler", $"Orchestrated flashlight injection complete. Total spawned: {totalSpawned}");
            }
            catch (Exception ex)
            {
                LibraryLabAPI.LogError("MapHandler.Spawn", $"Pipeline collapsed: {ex}");
            }
        }

        private float GetZoneSpawnChance(FacilityZone zone)
        {
            var config = _plugin.Config.FlashlightSpawnConfig;
            switch (zone)
            {
                case FacilityZone.LightContainment: return config.ChanceLight;
                case FacilityZone.HeavyContainment: return config.ChanceHeavy;
                case FacilityZone.Entrance: return config.ChanceEntrance;
                case FacilityZone.Surface: return config.ChanceSurface;
                default: return config.ChanceOther;
            }
        }

        private void ApplyRandomThrowForce(Pickup pickup)
        {
            if (pickup == null || pickup.GameObject == null) return;

            Rigidbody rb = pickup.GameObject.GetComponent<Rigidbody>();
            if (rb == null) return;

            float randomAngle = (float)(_random.NextDouble() * Math.PI * 2.0);
            Vector3 throwDirection = new Vector3((float)Math.Cos(randomAngle), 0.3f, (float)Math.Sin(randomAngle)).normalized;

            rb.velocity = throwDirection * UnityEngine.Random.Range(1.5f, 3.5f);
            rb.angularVelocity = new Vector3(UnityEngine.Random.Range(-5f, 5f), UnityEngine.Random.Range(-5f, 5f), UnityEngine.Random.Range(-5f, 5f));
        }
    }
}