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

                    // Elevated center point prevents early collisions with floor primitives during spawn frames.
                    Vector3 spawnPosition = room.Position + new Vector3(0f, 0.6f, 0f);

                    var flashlightPickup = Pickup.Create(ItemType.Flashlight, spawnPosition, Quaternion.identity);
                    if (flashlightPickup != null)
                    {
                        // Enqueuing the physics push to run asynchronously to ensure the engine registers the item's spawn lifecycle state.
                        Timing.RunCoroutine(ApplyDelayedPhysicsPush(flashlightPickup), CoroutineTags.MapCoroutines);
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

        /// <summary>
        /// Asynchronously delays physics execution by exactly one frame to ensure 
        /// that the native base-game spawning logic doesn't overwrite our custom velocities.
        /// </summary>
        private IEnumerator<float> ApplyDelayedPhysicsPush(Pickup pickup)
        {
            yield return Timing.WaitForOneFrame;

            if (pickup == null || pickup.IsDestroyed || !pickup.IsSpawned)
                yield break;

            Rigidbody rb = pickup.Rigidbody;
            if (rb == null)
                yield break;

            try
            {
                // Force kinematic state to false to wake up the Unity physics processing pipeline on this network object.
                rb.isKinematic = false;

                // Generating a clean 3D force vector.
                Vector3 randomDirection = UnityEngine.Random.onUnitSphere;

                // Reflection gate: prevents items from being driven straight down into floor mesh colliders.
                if (Vector3.Dot(randomDirection, Vector3.down) > 0.707f)
                {
                    randomDirection = Vector3.Reflect(randomDirection, Vector3.up);
                }

                // Standard natural drop force magnitude for environment props layout.
                float dynamicMagnitude = UnityEngine.Random.Range(2.0f, 4.2f);

                // Utilizing the modern Unity linearVelocity property discovered in your functional runtime code.
                rb.linearVelocity = randomDirection * dynamicMagnitude;
                rb.angularVelocity = UnityEngine.Random.insideUnitSphere * 3.5f;
            }
            catch (Exception ex)
            {
                LibraryLabAPI.LogError("MapHandler.Physics", $"Failed to apply synchronized physics to pickup {pickup.Serial}: {ex.Message}");
            }
        }
    }
}