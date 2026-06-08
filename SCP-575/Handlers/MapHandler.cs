namespace SCP_575.Handlers
{
    using LabApi.Events.Arguments.ServerEvents;
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

        public override void OnServerRoundStarted()
        {
            if (!_plugin.IsEventActive || _plugin.Config?.FlashlightSpawnConfig == null || !_plugin.Config.FlashlightSpawnConfig.IsEnabled)
                return;

            Timing.RunCoroutine(SpawnFlashlightsPipeline(), CoroutineTags.MapCoroutines);
        }

        private IEnumerator<float> SpawnFlashlightsPipeline()
        {
            // Deferring execution to ensure that Room positions and NavMesh generation are completely baked.
            yield return Timing.WaitForSeconds(1.5f);

            try
            {
                var allRooms = _plugin.LibraryLabAPI?.Rooms;
                if (allRooms == null || !allRooms.Any())
                {
                    LibraryLabAPI.LogError("MapHandler", "Aborting distribution: Custom room registry is unavailable or uninitialized.");
                    yield break;
                }

                int totalSpawned = 0;

                foreach (Room room in allRooms)
                {
                    if (room == null)
                        continue;

                    // Absolute mechanical constraint: Pocket dimension must maintain strict item isolation.
                    if (room.Name == RoomName.Pocket)
                        continue;

                    float spawnChance = GetZoneSpawnChance(room.Zone);
                    if (spawnChance <= 0f)
                        continue;

                    double roll = _random.NextDouble() * 100.0;
                    if (roll > spawnChance)
                        continue;

                    // Hovering the item exactly 0.5m above center prevents it from initializing embedded inside the floor collider.
                    Vector3 spawnPosition = room.Position + new Vector3(0f, 0.5f, 0f);

                    var flashlightPickup = Pickup.Create(ItemType.Flashlight, spawnPosition, Quaternion.identity);
                    if (flashlightPickup != null)
                    {
                        ApplyRandomThrowForce(flashlightPickup);
                        totalSpawned++;

                        LibraryLabAPI.LogDebug("MapHandler", $"Injected flashlight into {room.Name} ({room.Zone}). Roll: {roll:F1}/{spawnChance}%");
                    }
                }

                LibraryLabAPI.LogInfo("MapHandler", $"Dynamic flashlight injection sequence complete. Total items spawned: {totalSpawned}");
            }
            catch (Exception ex)
            {
                LibraryLabAPI.LogError("MapHandler.Spawn", $"Pipeline collapsed due to an unhandled exception: {ex}");
            }
        }

        /// <summary>
        /// Resolves the configured zone probability using classic C# 7.4 syntax for runtime compatibility.
        /// </summary>
        private float GetZoneSpawnChance(FacilityZone zone)
        {
            var config = _plugin.Config.FlashlightSpawnConfig;

            switch (zone)
            {
                case FacilityZone.LightContainment:
                    return config.ChanceLight;
                case FacilityZone.HeavyContainment:
                    return config.ChanceHeavy;
                case FacilityZone.Entrance:
                    return config.ChanceEntrance;
                case FacilityZone.Surface:
                    return config.ChanceSurface;
                default:
                    return config.ChanceOther;
            }
        }

        private void ApplyRandomThrowForce(Pickup pickup)
        {
            if (pickup == null)
                return;

            GameObject visualObject = pickup.GameObject;
            if (visualObject == null)
                return;

            Rigidbody rb = visualObject.GetComponent<Rigidbody>();
            if (rb == null)
                return;

            // Calculating flat horizontal circle vector to distribute items naturally without clipping into ceilings.
            float randomAngle = (float)(_random.NextDouble() * Math.PI * 2.0);
            Vector3 throwDirection = new Vector3((float)Math.Cos(randomAngle), 0.3f, (float)Math.Sin(randomAngle)).normalized;

            // Randomized velocity scalar creates asymmetric drop patterns across multiple rooms.
            float throwForce = UnityEngine.Random.Range(1.5f, 3.5f);
            rb.velocity = throwDirection * throwForce;

            // Injecting chaotic three-axis angular velocity to mimic realistic physics tumbling upon instantiation.
            rb.angularVelocity = new Vector3(
                UnityEngine.Random.Range(-5f, 5f),
                UnityEngine.Random.Range(-5f, 5f),
                UnityEngine.Random.Range(-5f, 5f)
            );
        }
    }
}