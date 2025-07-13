namespace SCP_575
{
    using System;
    using System.Collections.Generic;
    using InventorySystem;
    using MEC;
    using PlayerRoles.PlayableScps.Scp3114;
    using PlayerRoles.Ragdolls;
    using ProgressiveCulling;
    using SCP_575.ConfigObjects;
    using SCP_575.Npc;
    using Shared;
    using UnityEngine;
    public class EventHandlers
    {
        private readonly Plugin _plugin;


        public EventHandlers(Plugin plugin) => _plugin = plugin;

        private Methods _methods => _plugin.Npc.Methods;

        public bool TeslasDisabled = false;
        public bool NukeDisabled = false;
        public List<CoroutineHandle> Coroutines = new List<CoroutineHandle>();


        public void OnWaitingForPlayers()
        {
            if (_plugin.Config.SpawnType == InstanceType.Npc || (_plugin.Config.SpawnType == InstanceType.Random && Library_ExiledAPI.Loader_Random_Next(100) > 55))
            {
                _plugin.Npc.Methods.Init();
            }
            else
            {
                //_plugin.Playable.Methods.Init();
            }
        }

        public void OnPlayerHurting(LabApi.Events.Arguments.PlayerEvents.PlayerHurtingEventArgs ev)
        {
            Library_ExiledAPI.LogDebug("Catched Event", $"OnPlayerHurting: {ev.Attacker?.Nickname ?? "SCP-575 NPC"} -> {ev.Player.Nickname}");
            if (ev.DamageHandler is Scp575DamageHandler scp575Handler)
            {
                Library_ExiledAPI.LogDebug("OnPlayerHurting", $"The event was caused by {Scp575DamageHandler.IdentifierName}");

            }

        }

        public void OnPlayerHurt(LabApi.Events.Arguments.PlayerEvents.PlayerHurtEventArgs ev)
        {
            Library_ExiledAPI.LogDebug("Catched Event", $"OnPlayerHurt: {ev.Attacker?.Nickname ?? "SCP-575 NPC"} -> {ev.Player.Nickname}");

            if (ev.DamageHandler is Scp575DamageHandler scp575Handler)
            {
                Library_ExiledAPI.LogDebug("OnPlayerHurt", $"The event was caused by {Scp575DamageHandler.IdentifierName}");

            }

        }

        public void OnPlayerDying(LabApi.Events.Arguments.PlayerEvents.PlayerDyingEventArgs ev)
        {
            Library_ExiledAPI.LogDebug("Catched Event", $"OnPlayerDying: {ev.Player.Nickname}");

            if (ev.DamageHandler is Scp575DamageHandler scp575Handler)
            {
                Library_ExiledAPI.LogDebug("OnPlayerDying", $"The event was caused by {Scp575DamageHandler.IdentifierName}");

                LabApi.Features.Wrappers.Player player = ev.Player;

                Timing.RunCoroutine(_methods.DropAndPushItems(player, scp575Handler));

            }
        }

        public void OnSpawningRagdoll(LabApi.Events.Arguments.PlayerEvents.PlayerSpawningRagdollEventArgs ev)
        {
            Library_ExiledAPI.LogDebug("Catched Event", $"OnSpawningRagdoll: {ev.Player.Nickname}");

            if (ev.DamageHandler is Scp575DamageHandler scp575Handler)
            {
                Library_ExiledAPI.LogDebug("OnSpawningRagdoll", $"The event was caused by {Scp575DamageHandler.IdentifierName}");

                Library_ExiledAPI.LogDebug("OnSpawningRagdoll", $"Setting ragdoll spawn position to: {ev.Ragdoll.Position} (death position: {ev.Player.Position})");
            }
        }

        public void OnSpawnedRagdoll(LabApi.Events.Arguments.PlayerEvents.PlayerSpawnedRagdollEventArgs ev)
        {
            Library_ExiledAPI.LogDebug("Catched Event", $"OnSpawnedRagdoll: {ev.Player.Nickname}");

            if (ev.DamageHandler is Scp575DamageHandler scp575Handler)
            {
                Library_ExiledAPI.LogDebug("OnSpawnedRagdoll", $"The event was caused by {Scp575DamageHandler.IdentifierName}");

                LabApi.Features.Wrappers.Ragdoll ragdoll = ev.Ragdoll;
                GameObject ragdollGO = ragdoll.Base.gameObject;

                // SANITY DEBUG
                var allRenderers = ragdollGO.GetComponentsInChildren<Renderer>(true); // Include inactive
                Library_ExiledAPI.LogDebug("OnSpawnedRagdoll - SANITY", $"Found {allRenderers.Length} renderers in hierarchy (including inactive)");

                foreach (var r in allRenderers)
                {
                    Library_ExiledAPI.LogDebug("OnSpawnedRagdoll - SANITY", $"Renderer: {r.name}, enabled: {r.enabled}, gameObject active: {r.gameObject.activeSelf}");
                }
                Library_ExiledAPI.LogDebug("OnSpawnedRagdoll - SANITY", $"Ragdoll type: {ragdoll.Base.GetType().Name}");
                Library_ExiledAPI.LogDebug("OnSpawnedRagdoll - SANITY", $"Ragdoll GameObject active: {ragdollGO.activeSelf}");
                Library_ExiledAPI.LogDebug("OnSpawnedRagdoll - SANITY", $"Ragdoll transform parent: {ragdollGO.transform.parent?.name ?? "None"}");
                //

                // Ensure ragdoll is active in hierarchy  
                if (!ragdollGO.activeSelf)
                {
                    ragdollGO.SetActive(true);
                    Library_ExiledAPI.LogWarn("OnSpawnedRagdoll", "Ragdoll GameObject was inactive — enabled manually.");
                }

                // Handle CullableBehaviour visibility issues  
                if (ragdollGO.TryGetComponent(out CullableBehaviour cullable))
                {
                    cullable.enabled = false; // Disable culling entirely to force visibility  
                    Library_ExiledAPI.LogDebug("OnSpawnedRagdoll", "Disabled CullableBehaviour to force visibility");
                }

                Library_ExiledAPI.LogDebug("OnSpawnedRagdoll", $"Ragdoll initial position: {ragdoll.Position}, Rotation: {ragdoll.Rotation.eulerAngles}");

                // Ensure ragdoll is a DynamicRagdoll  
                if (ragdoll.Base is not DynamicRagdoll dynamicRagdoll)
                {
                    Library_ExiledAPI.LogWarn("OnSpawnedRagdoll", "Ragdoll is not a DynamicRagdoll. Skipping force application and conversion.");
                    return;
                }

                // 1. FIRST: Handle bone conversion before any position/force manipulation  
                try
                {
                    Scp3114RagdollToBonesConverter.ConvertExisting(dynamicRagdoll);
                    Library_ExiledAPI.LogDebug("OnSpawnedRagdoll", "Ragdoll bones conversion completed successfully.");
                    //Library_ExiledAPI.LogDebug("OnSpawnedRagdoll", "Ragdoll bones conversion disabled for now, dumb method called successfully.");
                }
                catch (Exception ex)
                {
                    Library_ExiledAPI.LogError("OnSpawnedRagdoll", $"Bone conversion error: {ex}");
                    return;
                }

                // 2. SECOND: Apply forces after conversion and position fixes
                var hitbox = scp575Handler.Hitbox;
                if (!Scp575DamageHandler.HitboxToForce.TryGetValue(hitbox, out float baseForce))
                {
                    Library_ExiledAPI.LogWarn("OnSpawnedRagdoll", $"Unknown hitbox: {hitbox}. No force applied.");
                    return;
                }

                // Generate force vector  
                Vector3 safeVelocity = scp575Handler.GetRandomUnitSphereVelocity(baseForce);
                Library_ExiledAPI.LogDebug("OnSpawnedRagdoll", $"Applying force to hitbox: {hitbox} with base force: {baseForce}, velocity: {safeVelocity}");

                // Apply force to specific hitbox  
                foreach (var _hitbox in dynamicRagdoll.Hitboxes)
                {
                    if (_hitbox.RelatedHitbox != hitbox) continue;

                    Library_ExiledAPI.LogDebug("OnSpawnedRagdoll", $"Applying force to hitbox: {_hitbox.RelatedHitbox}");
                    _hitbox.Target.AddForce(safeVelocity, ForceMode.VelocityChange);
                }

                // Apply additional force to all ragdoll limbs for dramatic effect  
                foreach (var rb in dynamicRagdoll.LinkedRigidbodies)
                {
                    rb.AddForce(safeVelocity * 0.5f, ForceMode.VelocityChange); // Reduced force for limbs  
                }

                // Post-conversion renderer check  
                Timing.CallDelayed(0.3f, () =>
                {
                    var renderer = ragdoll.Base.GetComponentInChildren<Renderer>();
                    if (renderer == null)
                    {
                        Library_ExiledAPI.LogWarn("OnSpawnedRagdoll", "Post-conversion: No Renderer found in ragdoll hierarchy.");
                    }
                    else
                    {
                        Library_ExiledAPI.LogDebug("OnSpawnedRagdoll", $"Post-conversion renderer: enabled={renderer.enabled}, visible={renderer.isVisible}");

                        if (!renderer.isVisible)
                        {
                            Library_ExiledAPI.LogWarn("OnSpawnedRagdoll", "Ragdoll renderer not visible after conversion.");
                        }
                    }
                });

                // DEBUG: Create marker at final ragdoll position  
                GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                marker.transform.position = ragdoll.Position + (Vector3.up * 1f);
                marker.transform.localScale = new Vector3(0.35f, 0.65f, 0.35f);
                marker.GetComponent<Renderer>().material.color = Color.magenta;
                GameObject.Destroy(marker, 180f);
                Library_ExiledAPI.LogDebug("OnSpawnedRagdoll - Marker", $"Spawned debug marker at: {marker.transform.position}");
            }
        }

        // Todo Turn On Lights in the room/whole heavy on generator completed, Play creepy sound via Cassie

        // Todo turn On ALL lights in the facility on three generators, Play creepy sound via Cassie

        // Todo kill SCP 575 on kill switch for computer SCP, Play creepy sound via Cassie

        // ToDo turn On lights for 5 seconds On FLASHNADE explosion in the SCP575 dark room, Play creepy sound via Cassie

        // TODO 2176 triggers SCP-575 in the room

        public void OnPlayerDeath(LabApi.Events.Arguments.PlayerEvents.PlayerDeathEventArgs ev)
        {
            Library_ExiledAPI.LogDebug("Catched Event", $"OnPlayerDeath: {ev.Player.Nickname}");
            if (ev.DamageHandler is Scp575DamageHandler scp575Handler)
            {
                Library_ExiledAPI.LogDebug("OnPlayerDeath", $"The event was caused by {Scp575DamageHandler.IdentifierName}");

            }
        }

    }
}
