namespace SCP_575
{

    using Exiled.API.Features;
    using Exiled.Loader;
    using InventorySystem;
    using LabApi.Events.Arguments.PlayerEvents;
    using LabApi.Features.Wrappers;
    using MEC;
    using PlayerRoles.PlayableScps.Scp3114;
    using PlayerRoles.Ragdolls;
    using ProgressiveCulling;
    using SCP_575.ConfigObjects;
    using SCP_575.Npc;
    using System;
    using System.Collections.Generic;
    using System.Reflection.Emit;
    using UnityEngine;
    using static PlayerRoles.FirstPersonControl.Thirdperson.Subcontrollers.FeetStabilizerSubcontroller;

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
            if (_plugin.Config.SpawnType == InstanceType.Npc || (_plugin.Config.SpawnType == InstanceType.Random && Loader.Random.Next(100) > 55))
            {
                _plugin.Npc.Methods.Init();
            }
            else
            {
                //_plugin.Playable.Methods.Init();
            }
        }

        public void OnPlayerHurting(PlayerHurtingEventArgs ev)
        {
            Log.Debug($"[Catched Event] OnPlayerHurting: {ev.Attacker?.Nickname ?? "No Attacker"} -> {ev.Player.Nickname}");
            if (ev.DamageHandler is Scp575DamageHandler scp575Handler)
            {
                Log.Debug($"[OnPlayerHurting] The event was caused by {Scp575DamageHandler.IdentifierName}");

            }

        }

        public void OnPlayerHurt(PlayerHurtEventArgs ev)
        {

            Log.Debug($"[Catched Event] OnPlayerHurt: {ev.Attacker?.Nickname ?? "No Attacker"} -> {ev.Player.Nickname}");
            if (ev.DamageHandler is Scp575DamageHandler scp575Handler)
            {
                Log.Debug($"[OnPlayerHurt] The event was caused by {Scp575DamageHandler.IdentifierName}");

            }

        }

        public void OnPlayerDying(PlayerDyingEventArgs ev)
        {
            Log.Debug($"[Catched Event] OnPlayerDying: {ev.Player.Nickname}");
            if (ev.DamageHandler is Scp575DamageHandler scp575Handler)
            {
                Log.Debug($"[OnPlayerDying] The event was caused by {Scp575DamageHandler.IdentifierName}");

                LabApi.Features.Wrappers.Player player = ev.Player;

                Log.Debug($"[OnPlayerDying] Dropping all items from {player.Nickname}'s inventory called by Server.");
                List<Item> items = new List<Item>(player.Items);
                player.Inventory.ServerDropEverything();

                Timing.RunCoroutine(_methods.DropAndPushItems(player, items, scp575Handler));

            }
        }

        public void OnSpawningRagdoll(PlayerSpawningRagdollEventArgs ev)
        {

            Log.Debug($"[Catched Event] OnSpawningRagdoll: {ev.DamageHandler.RagdollInspectText}");
            if (ev.Ragdoll.DamageHandler is Scp575DamageHandler scp575Handler)
            {
                Log.Debug($"[OnSpawningRagdoll] The event was caused by {Scp575DamageHandler.IdentifierName}");

            }
        }

        public void OnSpawnedRagdoll(PlayerSpawnedRagdollEventArgs ev)
        {
            Log.Debug($"[Catched Event] OnSpawnedRagdoll: {ev.Player.Nickname}");

            // ✅ Only proceed if the damage handler matches SCP-575
            if (ev.DamageHandler is Scp575DamageHandler scp575Handler)
            {
                Log.Debug($"[OnSpawnedRagdoll] The event was caused by {Scp575DamageHandler.IdentifierName}");

                LabApi.Features.Wrappers.Ragdoll ragdoll = ev.Ragdoll;
                GameObject ragdollGO = ragdoll.Base.gameObject;

                // 🟢 Ensure ragdoll is active in hierarchy
                if (!ragdollGO.activeSelf)
                {
                    ragdollGO.SetActive(true);
                    Log.Warn("[OnSpawnedRagdoll] Ragdoll GameObject was inactive — enabled manually.");
                }

                // 🟢 Force CullableBehaviour visibility if present
                if (ragdollGO.TryGetComponent(out CullableBehaviour cullable))
                {
                    cullable.enabled = true;
                    Log.Debug($"[OnSpawnedRagdoll] CullableBehaviour enabled. ShouldBeVisible = {cullable.ShouldBeVisible}");
                }
                else
                {
                    Log.Debug("[OnSpawnedRagdoll] No CullableBehaviour attached.");
                }

                // 🟢 Offset Y-position if ragdoll appears underground
                if (ragdoll.Position.y < 0.1f)
                {
                    ragdollGO.transform.position += Vector3.up * 0.5f;
                    Log.Warn($"[OnSpawnedRagdoll] Adjusted ragdoll Y position to avoid clipping: {ragdollGO.transform.position}");
                }

                Log.Debug($"[OnSpawnedRagdoll] Ragdoll transform: Position = {ragdoll.Position}, Rotation = {ragdoll.Rotation.eulerAngles}");

                // 🛑 Ensure ragdoll is a DynamicRagdoll
                if (ragdoll.Base is not DynamicRagdoll dynamicRagdoll)
                {
                    Log.Warn("[OnSpawnedRagdoll] Ragdoll is not DynamicRagdoll. Skipping.");
                    return;
                }

                // 🎯 Inspect renderer status pre-conversion
                var renderer = ragdoll.Base.GetComponentInChildren<Renderer>();
                if (renderer == null)
                    Log.Warn("[OnSpawnedRagdoll] No Renderer found in ragdoll hierarchy.");
                else
                    Log.Debug($"[OnSpawnedRagdoll] Renderer enabled = {renderer.enabled}, isVisible = {renderer.isVisible}, bounds = {renderer.bounds.size}");

                // ⏱️ Run delayed renderer visibility audit after bone conversion
                Timing.CallDelayed(0.5f, () =>
                {
                    var postConvertRenderer = ragdoll.Base.GetComponentInChildren<Renderer>();
                    if (postConvertRenderer == null)
                    {
                        Log.Warn("[SCP-575] Post-conversion: No renderer found.");
                    }
                    else
                    {
                        Log.Debug($"[SCP-575] Renderer status: enabled = {postConvertRenderer.enabled}, isVisible = {postConvertRenderer.isVisible}");
                        if (!postConvertRenderer.isVisible)
                        {
                            Log.Warn($"[SCP-575] Ragdoll still not visible. Position = {ragdoll.Position}, Role = {ev.Player.Role}");
                        }
                    }
                });

                // 💥 Look up hitbox-specific force scaling
                var hitbox = scp575Handler.Hitbox;
                if (!Scp575DamageHandler.HitboxToForce.TryGetValue(hitbox, out float baseForce))
                {
                    Log.Warn($"[OnSpawnedRagdoll] Unknown hitbox: {hitbox}. No force applied.");
                    return;
                }

                // 💨 Generate randomized, upward-safe force vector
                float finalForce = scp575Handler.calculateForcePush(baseForce);
                Log.Debug($"[OnSpawnedRagdoll] Final push force: {finalForce}");

                Vector3 safeVelocity = scp575Handler.GetRandomUnitSphereVelocity(baseForce);

                // 💣 Apply force to the targeted hitbox
                foreach (var _hitbox in dynamicRagdoll.Hitboxes)
                {
                    if (_hitbox.RelatedHitbox != hitbox) continue;

                    Log.Debug($"[OnSpawnedRagdoll] Applying force to hitbox: {_hitbox.RelatedHitbox}");
                    _hitbox.Target.AddForce(safeVelocity, ForceMode.VelocityChange);
                }

                foreach (Transform child in ragdollGO.transform)
                    Log.Debug($"[OnSpawnedRagdoll] Child: {child.name}, active={child.gameObject.activeSelf}");

                // 🧍 Convert ragdoll visual mesh to bones
                try
                {
                    Scp3114RagdollToBonesConverter.ConvertExisting(dynamicRagdoll);
                    Log.Debug("[OnSpawnedRagdoll] Converted ragdoll to bones.");
                }
                catch (Exception ex)
                {
                    Log.Error($"[OnSpawnedRagdoll] Bone conversion error: {ex}");
                    return;
                }

                foreach (Transform child in ragdollGO.transform)
                    Log.Debug($"[OnSpawnedRagdoll] Child: {child.name}, active={child.gameObject.activeSelf}");


                // 💥 Scatter additional force across ragdoll limbs
                foreach (var rb in dynamicRagdoll.LinkedRigidbodies)
                {
                    rb.AddForce(safeVelocity, ForceMode.VelocityChange);
                }

                /// DEBUG
                GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                marker.transform.position = ragdoll.Position + Vector3.up * 1.3f;
                marker.GetComponent<Renderer>().material.color = Color.yellow;
                GameObject.Destroy(marker, 120f);
            }
        }

        // Todo Turn On Lights in the room/whole heavy on generator completed, Play creepy sound via Cassie

        // Todo turn On ALL lights in the facility on three generators, Play creepy sound via Cassie

        // Todo kill SCP 575 on kill switch for computer SCP, Play creepy sound via Cassie

        // ToDo turn On lights for 5 seconds On FLASHNADE explosion in the SCP575 dark room, Play creepy sound via Cassie

        public void OnPlayerDeath(PlayerDeathEventArgs ev)
        {
            Log.Debug($"[Catched Event] OnPlayerDeath: {ev.Player.Nickname}");
            if (ev.DamageHandler is Scp575DamageHandler scp575Handler)
            {
                Log.Debug($"[OnPlayerDeath] The event was caused by {Scp575DamageHandler.IdentifierName}");
            }
        }

    }
}
