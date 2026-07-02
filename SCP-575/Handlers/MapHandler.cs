namespace SCP_575.Handlers
{
    using LabApi.Events.Arguments.ServerEvents;
    using LabApi.Events.CustomHandlers;
    using LabApi.Features.Wrappers;
    using MEC;
    using SCP_575.Shared;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEngine;

    /// <summary>
    /// Handles physical facility infrastructure layout transformations. Sets up resource grids 
    /// and controls item injection algorithms post-generation while preserving network synchronization stability.
    /// </summary>
    public class MapHandler : CustomEventsHandler
    {
        private readonly Plugin _plugin;
        private readonly System.Random _random;

        public MapHandler(Plugin plugin)
        {
            _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
            _random = new System.Random();
        }

        #region Lifecycle Management

        /// <summary>
        /// Instantly terminates trailing spatial item pipelines during macro round boundary transitions 
        /// to safeguard the next round context against asynchronous heap pollution.
        /// </summary>
        public override void OnServerRoundEnded(RoundEndedEventArgs ev) => Clean();
        public override void OnServerWaitingForPlayers() => Clean();

        private void Clean()
        {
            Timing.KillCoroutines(CoroutineTags.MapCoroutines);
        }

        #endregion

        /// <summary>
        /// Public entrypoint invoked exclusively by the central NPC Lifecycle Orchestrator.
        /// </summary>
        public void ExecuteFlashlightDistribution()
        {
            if (_plugin.Config?.FlashlightSpawnConfig == null || !_plugin.Config.FlashlightSpawnConfig.IsEnabled)
                return;

            Timing.RunCoroutine(SpawnFlashlightsPipeline(), CoroutineTags.MapCoroutines);
        }

        /// <summary>
        /// Drives the item injection sequence across valid zone boundaries.
        /// Evaluates spatial distribution limits dynamically after scene layout compilation settles.
        /// </summary>
        private IEnumerator<float> SpawnFlashlightsPipeline()
        {
            // Introduces a strict temporal buffer allowing the native scene graph and network mirror transforms to initialize completely.
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

                    // Elevating the injection vector anchors the item in safe spatial air coordinates,
                    // preventing premature mesh tracking failures or immediate physics intersection with floor primitives.
                    Vector3 spawnPosition = room.Position + new Vector3(0f, 0.6f, 0f);

                    var flashlightPickup = Pickup.Create(ItemType.Flashlight, spawnPosition, Quaternion.identity);
                    if (flashlightPickup != null)
                    {
                        // Physics tasks must run out-of-frame asynchronously to bypass native inventory serialization blocks.
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
        /// Forces a single-frame deferral over the rigid-body state machine.
        /// This ensures the engine registers native transform weights before we apply localized kinetic forces.
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
                // Disabling the kinematic gate wakes up the PhysX solver pipeline on this active networked entity.
                rb.isKinematic = false;

                Vector3 randomDirection = UnityEngine.Random.onUnitSphere;

                // Restricts the drop angle projection plane to guarantee props scatter outward rather than sinking into ground mesh layers.
                if (Vector3.Dot(randomDirection, Vector3.down) > 0.707f)
                {
                    randomDirection = Vector3.Reflect(randomDirection, Vector3.up);
                }

                // Generates organic dispersion variations to give items a natural fallen look across environment grids.
                float dynamicMagnitude = UnityEngine.Random.Range(2.0f, 4.2f);

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