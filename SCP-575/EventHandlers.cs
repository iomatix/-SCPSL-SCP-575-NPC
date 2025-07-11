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

     // Todo: This class shouldnt be 'using' any of APIs, instead import methods from different modules from this repo
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

                Library_ExiledAPI.LogDebug("OnPlayerDying", $"Dropping all items from {player.Nickname}'s inventory called by Server.");

                List<LabApi.Features.Wrappers.Item> items = new List<LabApi.Features.Wrappers.Item>(player.Items);
                player.Inventory.ServerDropEverything();

                Timing.RunCoroutine(_methods.DropAndPushItems(player, items, scp575Handler));

            }
        }

        public void OnSpawningRagdoll(LabApi.Events.Arguments.PlayerEvents.PlayerSpawningRagdollEventArgs ev)
        {

            Library_ExiledAPI.LogDebug("Catched Event", $"OnSpawningRagdoll: {ev.Player.Nickname}");

            if (ev.Ragdoll.DamageHandler is Scp575DamageHandler scp575Handler)
            {
                Library_ExiledAPI.LogDebug("OnSpawningRagdoll", $"The event was caused by {Scp575DamageHandler.IdentifierName}");

            }
        }

        public void OnSpawnedRagdoll(LabApi.Events.Arguments.PlayerEvents.PlayerSpawnedRagdollEventArgs ev)
        {
            Library_ExiledAPI.LogDebug("Catched Event", $"OnSpawnedRagdoll: {ev.Player.Nickname}");

            // ✅ Only proceed if the damage handler matches SCP-575
            if (ev.DamageHandler is Scp575DamageHandler scp575Handler)
            {
                Library_ExiledAPI.LogDebug("OnSpawnedRagdoll", $"The event was caused by {Scp575DamageHandler.IdentifierName}");

                LabApi.Features.Wrappers.Ragdoll ragdoll = ev.Ragdoll;
                GameObject ragdollGO = ragdoll.Base.gameObject;

                // 🟢 Ensure ragdoll is active in hierarchy
                if (!ragdollGO.activeSelf)
                {
                    ragdollGO.SetActive(true);
                    Library_ExiledAPI.LogWarn("OnSpawnedRagdoll", "Ragdoll GameObject was inactive — enabled manually.");
                }

                // 🟢 Force CullableBehaviour visibility if present
                if (ragdollGO.TryGetComponent(out CullableBehaviour cullable))
                {
                    cullable.enabled = true;
                    Library_ExiledAPI.LogWarn("OnSpawnedRagdoll", $"CullableBehaviour enabled. ShouldBeVisible = {cullable.ShouldBeVisible}");
                }
                else
                {
                    Library_ExiledAPI.LogDebug("OnSpawnedRagdoll", "No CullableBehaviour attached to the ragdoll GameObject.");
                }

                // 🟢 Offset Y-position if ragdoll appears underground
                if (ragdoll.Position.y < 0.1f)
                {
                    ragdollGO.transform.position += Vector3.up * 0.5f;
                    Library_ExiledAPI.LogWarn("OnSpawnedRagdoll", $"Adjusted ragdoll Y position to avoid clipping: {ragdollGO.transform.position}");
                }

                Library_ExiledAPI.LogDebug("OnSpawnedRagdoll", $"Ragdoll transform: Position = {ragdoll.Position}, Rotation = {ragdoll.Rotation.eulerAngles}");

                // 🛑 Ensure ragdoll is a DynamicRagdoll
                if (ragdoll.Base is not DynamicRagdoll dynamicRagdoll)
                {
                    Library_ExiledAPI.LogWarn("OnSpawnedRagdoll", "Ragdoll is not a DynamicRagdoll. Skipping force application and conversion.");
                    return;
                }

                // 🎯 Inspect renderer status pre-conversion
                var renderer = ragdoll.Base.GetComponentInChildren<Renderer>();
                if (renderer == null) Library_ExiledAPI.LogWarn("OnSpawnedRagdoll", "No Renderer found in ragdoll hierarchy.");
                else Library_ExiledAPI.LogDebug("OnSpawnedRagdoll", $"Renderer found: enabled = {renderer.enabled}, isVisible = {renderer.isVisible}, bounds = {renderer.bounds.size}");

                // ⏱️ Run delayed renderer visibility audit after bone conversion
                Timing.CallDelayed(0.5f, () =>
                {
                    var postConvertRenderer = ragdoll.Base.GetComponentInChildren<Renderer>();
                    if (postConvertRenderer == null)
                    {
                        Library_ExiledAPI.LogWarn("OnSpawnedRagdoll", "Post-conversion: No Renderer found in ragdoll hierarchy.");
                    }
                    else
                    {
                        Library_ExiledAPI.LogDebug("OnSpawnedRagdoll", $"Renderer status: enabled = {postConvertRenderer.enabled}, isVisible = {postConvertRenderer.isVisible}");
                        Library_ExiledAPI.LogDebug("OnSpawnedRagdoll", $"Post-conversion Renderer bounds: {postConvertRenderer.bounds.size}");
                        if (!postConvertRenderer.isVisible)
                        {
                            Library_ExiledAPI.LogWarn("OnSpawnedRagdoll", "Ragdoll is not visible after conversion. This may indicate an issue with the ragdoll's visibility settings or the conversion process.");
                        }
                    }
                });

                // 💥 Look up hitbox-specific force scaling
                var hitbox = scp575Handler.Hitbox;
                if (!Scp575DamageHandler.HitboxToForce.TryGetValue(hitbox, out float baseForce))
                {
                    Library_ExiledAPI.LogWarn("OnSpawnedRagdoll", $"Unknown hitbox: {hitbox}. No force applied.");
                    return;
                }

                // 💨 Generate randomized, upward-safe force vector
                float finalForce = scp575Handler.calculateForcePush(baseForce);
                Library_ExiledAPI.LogDebug("OnSpawnedRagdoll", $"Applying force to hitbox: {hitbox} with base force: {baseForce}");

                Vector3 safeVelocity = scp575Handler.GetRandomUnitSphereVelocity(baseForce);

                // 💣 Apply force to the targeted hitbox
                foreach (var _hitbox in dynamicRagdoll.Hitboxes)
                {
                    if (_hitbox.RelatedHitbox != hitbox) continue;

                    Library_ExiledAPI.LogDebug("OnSpawnedRagdoll", $"Applying force to hitbox: {_hitbox.RelatedHitbox} with velocity {safeVelocity}");
                    _hitbox.Target.AddForce(safeVelocity, ForceMode.VelocityChange);
                }

                foreach (Transform child in ragdollGO.transform) Library_ExiledAPI.LogDebug("OnSpawnedRagdoll", $"Child: {child.name}, active={child.gameObject.activeSelf}");

                // 🧍 Convert ragdoll visual mesh to bones
                try
                {
                    Scp3114RagdollToBonesConverter.ConvertExisting(dynamicRagdoll);
                    Library_ExiledAPI.LogDebug("OnSpawnedRagdoll", "Ragdoll bones conversion completed successfully.");
                }
                catch (Exception ex)
                {
                    Library_ExiledAPI.LogError("OnSpawnedRagdoll", $"Bone conversion error: {ex}");
                    return;
                }

                foreach (Transform child in ragdollGO.transform) Library_ExiledAPI.LogDebug("OnSpawnedRagdoll", $"Child: {child.name}, active={child.gameObject.activeSelf}");


                // 💥 Scatter additional force across ragdoll limbs
                foreach (var rb in dynamicRagdoll.LinkedRigidbodies)
                {
                    rb.AddForce(safeVelocity, ForceMode.VelocityChange);
                }

                /// DEBUG
                GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                marker.transform.position = ragdoll.Position + Vector3.up * 1f;
                marker.transform.localScale = new Vector3(0.35f, 0.65f, 0.35f);
                marker.GetComponent<Renderer>().material.color = Color.magenta;
                GameObject.Destroy(marker, 180f);
                Library_ExiledAPI.LogDebug("OnSpawnedRagdoll - Marker", $"Spawned {Color.magenta} debug marker at: {marker.transform.position}, Duration: 180 seconds");

            }
        }

        // Todo Turn On Lights in the room/whole heavy on generator completed, Play creepy sound via Cassie

        // Todo turn On ALL lights in the facility on three generators, Play creepy sound via Cassie

        // Todo kill SCP 575 on kill switch for computer SCP, Play creepy sound via Cassie

        // ToDo turn On lights for 5 seconds On FLASHNADE explosion in the SCP575 dark room, Play creepy sound via Cassie

        public void OnPlayerDeath(LabApi.Events.Arguments.PlayerEvents.PlayerDeathEventArgs ev)
        {
            Library_ExiledAPI.LogDebug("Catched Event", $"OnPlayerDeath: {ev.Player.Nickname}");
            if (ev.DamageHandler is Scp575DamageHandler scp575Handler)
            {
                Library_ExiledAPI.LogDebug("OnSpawningRagdoll", $"The event was caused by {Scp575DamageHandler.IdentifierName}");

            }
        }

    }
}
