namespace SCP_575.Npc
{
    using CommandSystem.Commands;
    using MEC;
    using RemoteAdmin.Communication;
    using SCP_575.ConfigObjects;
    using Shared;
    using System;
    using System.Collections.Generic;
    using UnityEngine;

    // TODO: Crate MethodsExiledAPI and Move methods there, Last one ist LabAPI so should stay in default
    public class Methods
    {
        private readonly Plugin _plugin;
        private NpcConfig Config => _plugin.Config.NpcConfig;
        public Methods(Plugin plugin) => _plugin = plugin;
        private readonly HashSet<Exiled.API.Enums.ZoneType> triggeredZones = new HashSet<Exiled.API.Enums.ZoneType>();

        private static readonly object BlackoutLock = new();
        private static int blackoutStacks = 0;
        /// <summary>
        /// Public getter indicating whether the blackout effect is currently active.
        /// Returns true if blackoutStacks is greater than zero.
        /// </summary>
        public bool IsBlackoutActive => blackoutStacks > 0;

        public void Init()
        {
            Library_ExiledAPI.LogInfo("Init", "SCP-575 Npc methods initialized.");
            Exiled.Events.Handlers.Server.RoundStarted += _plugin.Npc.EventHandlers.OnRoundStart;
            Exiled.Events.Handlers.Server.RoundEnded += _plugin.Npc.EventHandlers.OnRoundEnd;
        }

        public void Disable()
        {
            Library_ExiledAPI.LogInfo("Disable", "SCP-575 Npc methods disabled.");
            Clean();
            Exiled.Events.Handlers.Server.RoundStarted -= _plugin.Npc.EventHandlers.OnRoundStart;
            Exiled.Events.Handlers.Server.RoundEnded -= _plugin.Npc.EventHandlers.OnRoundEnd;
        }

        public void Clean()
        {
            Library_ExiledAPI.LogInfo("Clean", "SCP-575 Npc methods cleaned.");
            blackoutStacks = 0;
            triggeredZones.Clear();
            Timing.KillCoroutines("SCP575keter");
            ResetTeslaGates();
        }
        public IEnumerator<float> RunBlackoutTimer()
        {
            yield return Timing.WaitForSeconds(Config.InitialDelay);
            Library_ExiledAPI.LogDebug("RunBlackoutTimer", "SCP-575 Npc methods started running blackout timer.");
            while (true)
            {
                yield return Timing.WaitForSeconds(Config.RandomEvents ? Library_ExiledAPI.Loader_Random_Next(Config.DelayMin, Config.DelayMax) : Config.InitialDelay);
                _plugin.Npc.EventHandlers.Coroutines.Add(Timing.RunCoroutine(ExecuteBlackoutEvent(), "575BlackoutExec"));

            }
        }

        private IEnumerator<float> ExecuteBlackoutEvent()
        {
            if (!IsBlackoutActive)
            {
                if (Config.CassieMessageClearBeforeImportant) Library_ExiledAPI.Cassie_Clear();
                Library_ExiledAPI.LogDebug("ExecuteBlackoutEvent", "Starting blackout event...");
                TriggerCassieMessage(Config.CassieMessageStart, true);

                if (Config.FlickerLights)
                {
                    FlickerAllZoneLights(Config.FlickerLightsDuration);
                }
                Library_ExiledAPI.LogDebug("ExecuteBlackoutEvent", $"Waiting for {Config.FlickerLightsDuration} seconds after flickering lights.");
                yield return Timing.WaitForSeconds(Config.TimeBetweenSentenceAndStart);
            }

            float blackoutDuration = Config.RandomEvents
                ? GetRandomBlackoutDuration()
                : Config.DurationMax;

            TriggerCassieMessage(Config.CassiePostMessage);

            bool blackoutOccurred = Config.UsePerRoomChances
                ? HandleRoomSpecificBlackout(blackoutDuration)
                : HandleZoneSpecificBlackout(blackoutDuration);

            _plugin.Npc.EventHandlers.Coroutines.Add(Timing.RunCoroutine(FinalizeBlackoutEvent(blackoutOccurred, blackoutDuration), "575BlackoutFinalize"));
        }

        private void FlickerAllZoneLights(float duration)
        {
            Library_ExiledAPI.LogDebug("FlickerAllZoneLights", $"Flickering all lights for {duration} seconds.");
            foreach (Exiled.API.Enums.ZoneType zone in Enum.GetValues(typeof(Exiled.API.Enums.ZoneType)))
            {
                Exiled.API.Features.Map.TurnOffAllLights(duration, zone);
            }
        }

        private float GetRandomBlackoutDuration()
        {
            return (float)Library_ExiledAPI.Loader_Random_NextDouble() * (Config.DurationMax - Config.DurationMin) + Config.DurationMin;
        }

        private bool HandleZoneSpecificBlackout(float blackoutDuration)
        {
            bool isBlackoutTriggered = false;

            // Use OR-assignment to combine results from all attempts to prevent overriding.
            isBlackoutTriggered |= AttemptZoneBlackout(Exiled.API.Enums.ZoneType.LightContainment, Config.ChanceLight, Config.CassieMessageLight, blackoutDuration);
            isBlackoutTriggered |= AttemptZoneBlackout(Exiled.API.Enums.ZoneType.HeavyContainment, Config.ChanceHeavy, Config.CassieMessageHeavy, blackoutDuration);
            isBlackoutTriggered |= AttemptZoneBlackout(Exiled.API.Enums.ZoneType.Entrance, Config.ChanceEntrance, Config.CassieMessageEntrance, blackoutDuration);
            isBlackoutTriggered |= AttemptZoneBlackout(Exiled.API.Enums.ZoneType.Surface, Config.ChanceSurface, Config.CassieMessageSurface, blackoutDuration);

            if (!IsBlackoutActive && !isBlackoutTriggered && Config.EnableFacilityBlackout)
            {
                TriggerFacilityWideBlackout(blackoutDuration);
                Library_ExiledAPI.LogDebug("HandleZoneSpecificBlackout", "Facility-wide blackout triggered due to no specific zone blackout.");
                isBlackoutTriggered = true;
            }

            return isBlackoutTriggered;
        }

        private bool AttemptZoneBlackout(Exiled.API.Enums.ZoneType zone, float chance, string cassieMessage, float blackoutDuration, bool disableSystems = false)
        {
            if (Library_ExiledAPI.Loader_Random_NextDouble() * 100 < chance)
            {
                Exiled.API.Features.Map.TurnOffAllLights(blackoutDuration, zone);
                Library_ExiledAPI.LogDebug("AttemptZoneBlackout", $"Attempting to trigger blackout in zone {zone} with chance {chance}% and duration {blackoutDuration} seconds.");
                TriggerCassieMessage(cassieMessage, true);

                if (disableSystems)
                {
                    Library_ExiledAPI.LogDebug("AttemptZoneBlackout", $"Blackout triggered in zone {zone} with blackout duration {blackoutDuration} seconds.");
                    DisableFacilitySystems(blackoutDuration);
                }

                return true;
            }

            return false;
        }

        private void TriggerFacilityWideBlackout(float blackoutDuration)
        {
            foreach (Exiled.API.Enums.ZoneType zone in Enum.GetValues(typeof(Exiled.API.Enums.ZoneType)))
            {
                Exiled.API.Features.Map.TurnOffAllLights(blackoutDuration, zone);
                Library_ExiledAPI.LogDebug("TriggerFacilityWideBlackout", $"Turning off lights in zone {zone} for {blackoutDuration} seconds.");
            }

            DisableFacilitySystems(blackoutDuration);
            TriggerCassieMessage(Config.CassieMessageFacility, true);
        }

        private bool HandleRoomSpecificBlackout(float blackoutDuration)
        {
            bool blackoutTriggered = false;

            foreach (Exiled.API.Features.Room room in Library_ExiledAPI.Rooms)
            {
                if (AttemptRoomBlackout(room, blackoutDuration))
                {
                    blackoutTriggered = true;
                    Library_ExiledAPI.LogDebug("HandleRoomSpecificBlackout", $"Blackout triggered in room {room.Name} of type {room.Type} with blackout duration {blackoutDuration} seconds.");
                }
            }

            if (!blackoutTriggered && Config.EnableFacilityBlackout)
            {
                TriggerFacilityWideBlackout(blackoutDuration);
                Library_ExiledAPI.LogDebug("HandleRoomSpecificBlackout", "Facility-wide blackout triggered due to no specific room blackout.");
                return true;
            }

            return blackoutTriggered;
        }

        private bool AttemptRoomBlackout(Exiled.API.Features.Room room, float blackoutDuration)
        {

            switch (room.Zone)
            {
                case Exiled.API.Enums.ZoneType.HeavyContainment:
                    if (Library_ExiledAPI.Loader_Random_NextDouble() * 100 < Config.ChanceHeavy)
                    {
                        HandleRoomBlackout(room, blackoutDuration);
                        if (!triggeredZones.Contains(Exiled.API.Enums.ZoneType.HeavyContainment))
                        {
                            TriggerCassieMessage(Config.CassieMessageHeavy);
                            triggeredZones.Add(Exiled.API.Enums.ZoneType.HeavyContainment);
                            Library_ExiledAPI.LogDebug("AttemptRoomBlackout", $"Blackout triggered in room {room.Name} of type {room.Type} with blackout duration {blackoutDuration} seconds.");
                        }
                        return true;
                    }
                    break;
                case Exiled.API.Enums.ZoneType.LightContainment:
                    if (Library_ExiledAPI.Loader_Random_NextDouble() * 100 < Config.ChanceLight)
                    {
                        HandleRoomBlackout(room, blackoutDuration);
                        if (!triggeredZones.Contains(Exiled.API.Enums.ZoneType.LightContainment))
                        {
                            TriggerCassieMessage(Config.CassieMessageLight);
                            triggeredZones.Add(Exiled.API.Enums.ZoneType.LightContainment);
                            Library_ExiledAPI.LogDebug("AttemptRoomBlackout", $"Blackout triggered in room {room.Name} of type {room.Type} with blackout duration {blackoutDuration} seconds.");
                        }
                        return true;
                    }
                    break;
                case Exiled.API.Enums.ZoneType.Entrance:
                    if (Library_ExiledAPI.Loader_Random_NextDouble() * 100 < Config.ChanceEntrance)
                    {
                        HandleRoomBlackout(room, blackoutDuration);
                        if (!triggeredZones.Contains(Exiled.API.Enums.ZoneType.Entrance))
                        {
                            TriggerCassieMessage(Config.CassieMessageEntrance);
                            triggeredZones.Add(Exiled.API.Enums.ZoneType.Entrance);
                            Library_ExiledAPI.LogDebug("AttemptRoomBlackout", $"Blackout triggered in room {room.Name} of type {room.Type} with blackout duration {blackoutDuration} seconds.");
                        }
                        return true;
                    }
                    break;
                case Exiled.API.Enums.ZoneType.Surface:
                    if (Library_ExiledAPI.Loader_Random_NextDouble() * 100 < Config.ChanceSurface)
                    {
                        HandleRoomBlackout(room, blackoutDuration);
                        if (!triggeredZones.Contains(Exiled.API.Enums.ZoneType.Surface))
                        {
                            TriggerCassieMessage(Config.CassieMessageSurface);
                            triggeredZones.Add(Exiled.API.Enums.ZoneType.Surface);
                            Library_ExiledAPI.LogDebug("AttemptRoomBlackout", $"Blackout triggered in room {room.Name} of type {room.Type} with blackout duration {blackoutDuration} seconds.");
                        }
                        return true;
                    }
                    break;
                default:
                    if (Library_ExiledAPI.Loader_Random_NextDouble() * 100 < Config.ChanceOther)
                    {
                        HandleRoomBlackout(room, blackoutDuration);
                        if (!triggeredZones.Contains(Exiled.API.Enums.ZoneType.Other))
                        {
                            TriggerCassieMessage(Config.CassieMessageOther);
                            triggeredZones.Add(Exiled.API.Enums.ZoneType.Other);
                            Library_ExiledAPI.LogDebug("AttemptRoomBlackout", $"Blackout triggered in room {room.Name} of type {room.Type} with blackout duration {blackoutDuration} seconds.");
                        }
                        return true;
                    }
                    break;
            }

            return false;
        }

        private void HandleRoomBlackout(Exiled.API.Features.Room room, float blackoutDuration)
        {

            if (Config.DisableTeslas && room.Type.Equals(Exiled.API.Enums.RoomType.HczTesla))
            {
                room.TeslaGate.CooldownTime = blackoutDuration + 0.5f;
                room.TeslaGate.ForceTrigger();
            }

            if (Config.DisableNuke && room.Type.Equals(Exiled.API.Enums.RoomType.HczNuke) && LabApi.Features.Wrappers.Warhead.IsDetonationInProgress && !LabApi.Features.Wrappers.Warhead.IsLocked)
            {
                LabApi.Features.Wrappers.Warhead.Stop();
                Library_ExiledAPI.LogDebug("HandleRoomBlackout", "Nuke detonation cancelled due to blackout event in HCZ Nuke room.");
            }

            room.TurnOffLights(blackoutDuration);
            Library_ExiledAPI.LogDebug("HandleRoomBlackout", $"Lights turned off in room {room.Name} of type {room.Type} for {blackoutDuration} seconds.");
        }

        private void DisableFacilitySystems(float blackoutDuration)
        {
            foreach (Exiled.API.Features.Room room in Library_ExiledAPI.Rooms)
            {
                room.TurnOffLights(blackoutDuration);
                Library_ExiledAPI.LogDebug("DisableFacilitySystems", $"Turning off lights in room {room.Name} for {blackoutDuration} seconds.");
            }

            ResetTeslaGates();


            if (Config.DisableNuke && LabApi.Features.Wrappers.Warhead.IsDetonationInProgress && !LabApi.Features.Wrappers.Warhead.IsLocked)
            {
                LabApi.Features.Wrappers.Warhead.Stop();
                Library_ExiledAPI.LogDebug("DisableFacilitySystems", "Nuke detonation cancelled due to blackout event.");
            }
        }

        private IEnumerator<float> FinalizeBlackoutEvent(bool blackoutOccurred, float blackoutDuration)
        {
            if (blackoutOccurred)
            {
                IncrementBlackoutStack();
                Library_ExiledAPI.LogDebug("FinalizeBlackoutEvent", $"Blackout event triggered. Current stacks: {blackoutStacks}, Duration: {blackoutDuration}");
                if (Config.Voice)
                {
                    TriggerCassieMessage(Config.CassieKeter);
                }

                yield return Timing.WaitForSeconds(blackoutDuration);
                DecrementBlackoutStack();
                Library_ExiledAPI.LogDebug("FinalizeBlackoutEvent", $"Blackout event finalized, stacks decremented to {blackoutStacks}.");

                if (!IsBlackoutActive) TriggerCassieMessage(Config.CassieMessageEnd);
                yield return Timing.WaitForSeconds(Config.TimeBetweenSentenceAndEnd);

                if (!IsBlackoutActive)
                {
                    ResetTeslaGates();
                    triggeredZones.Clear();
                    Library_ExiledAPI.LogDebug("FinalizeBlackoutEvent", "Blackout event completed. All systems reset.");
                }

            }
            else
            {
                if (!IsBlackoutActive) TriggerCassieMessage(Config.CassieMessageWrong);
            }
        }

        private void ResetTeslaGates()
        {
            foreach (Exiled.API.Features.TeslaGate teslaGate in Library_ExiledAPI.TeslaGates)
            {
                ResetTeslaGate(teslaGate);
                Library_ExiledAPI.LogDebug("ResetTeslaGates", $"Resetting TeslaGate {teslaGate.ToString()} after blackout.");
            }
        }

        private void ResetTeslaGate(Exiled.API.Features.TeslaGate gate)
        {
            gate.ForceTrigger();
            Library_ExiledAPI.LogDebug("ResetTeslaGate", $"Resetting TeslaGate {gate.ToString()} after blackout.");
            gate.CooldownTime = 5f;
            Library_ExiledAPI.LogDebug("ResetTeslaGate", $"TeslaGate {gate.ToString()} has been reset. Cooldown = {gate.CooldownTime}");
        }

        private void TriggerCassieMessage(string message, bool isGlitchy = false)
        {
            if (string.IsNullOrEmpty(message)) return;
            Library_ExiledAPI.LogDebug("TriggerCassieMessage", $"Triggering CASSIE message: {message}");
            if (isGlitchy)
            {
                Library_ExiledAPI.Cassie_GlitchyMessage(message);
            }
            else
            {
                Library_ExiledAPI.Cassie_Message(message);
            }
        }

        private void IncrementBlackoutStack()
        {
            lock (BlackoutLock)
                blackoutStacks++;
        }

        private void DecrementBlackoutStack()
        {
            lock (BlackoutLock)
                blackoutStacks = Math.Max(0, blackoutStacks - 1);
        }

        private bool ShouldApplyBlackoutDamage(Exiled.API.Features.Player player)
        {
            Library_ExiledAPI.LogDebug("ShouldApplyBlackoutDamage", $"Checking if player {player.Nickname} should receive damage during blackout.");
            return IsHumanWithoutLight(player) && IsInDarkRoom(player);
        }

        private bool IsHumanWithoutLight(Exiled.API.Features.Player player)
        {
            Library_ExiledAPI.LogDebug("IsHumanWithoutLight", $"Checking if player {player.Nickname} is human and has no light source in hand.");

            if (!player.IsHuman || player.HasFlashlightModuleEnabled) return false;

            Library_ExiledAPI.LogDebug("IsHumanWithoutLight", $"Current item in hand: {player.CurrentItem?.Base?.name ?? "None"}");
            if (player.CurrentItem?.Base is InventorySystem.Items.ToggleableLights.ToggleableLightItemBase lightItem)
            {
                Library_ExiledAPI.LogDebug("IsHumanWithoutLight", $"Player {player.Nickname} has a light source in hand: {lightItem.name}, and IsEmittingLight: {lightItem.IsEmittingLight}");
                return !lightItem.IsEmittingLight;
            }

            Library_ExiledAPI.LogDebug("IsHumanWithoutLight", $"Player {player.Nickname} has no light source in hand.");
            return true;
        }

        private bool IsInDarkRoom(Exiled.API.Features.Player player)
        {
            Library_ExiledAPI.LogDebug("IsInDarkRoom", $"Checking if player {player.Nickname} is in a dark room. Current room: {player.CurrentRoom?.Name ?? "None"}");
            return player.CurrentRoom?.AreLightsOff ?? false;
        }

        public IEnumerator<float> KeterDamage()
        {
            Library_ExiledAPI.LogDebug("KeterDamage", "SCP-575 Keter damage handler started.");
            while (true)
            {
                yield return Timing.WaitForSeconds(Config.KeterDamageDelay);
                if (IsBlackoutActive)
                {
                    Library_ExiledAPI.LogDebug("KeterDamage", $"SCP-575 Keter damage handler active with {blackoutStacks} stacks.");
                    
                    foreach (LabApi.Features.Wrappers.Player player in Library_LabAPI.Players)
                    {
                        Library_ExiledAPI.LogDebug("KeterDamage", $"Checking player {player.Nickname} for Keter damage during blackout.");

                        if (ShouldApplyBlackoutDamage(Library_ExiledAPI.ToExiledPlayer(player)))
                        {
                            Library_ExiledAPI.LogDebug("KeterDamage", $"Applying damage to player {player.Nickname} due to no light source in hand during blackout.");

                            float rawDamage = Config.KeterDamage * blackoutStacks;
                            float clampedDamage = Mathf.Max(rawDamage, 1f);
                            Scp575DamageHandler damageHandler = new Scp575DamageHandler(player, damage: clampedDamage);

                            yield return Timing.WaitForOneFrame; // Ensure engine is ready before applying damage

                            player.Damage(damageHandler);

                            Library_ExiledAPI.LogDebug("KeterDamage", $"Player {player.Nickname} has been damaged by SCP-575. Damage: {clampedDamage}, Raw Damage: {rawDamage}");
                            player.SendBroadcast(Config.KeterBroadcast, 3, type: Broadcast.BroadcastFlags.Normal, shouldClearPrevious: true);

                        }
                        else if (player.IsHuman)
                        {
                            Library_ExiledAPI.LogDebug("KeterDamage", $"Player {player.Nickname} has a light source in hand or lights are on in their current room, no damage applied.");
                        }
                    }
                }
            }
        }

        public IEnumerator<float> DropAndPushItems(
            LabApi.Features.Wrappers.Player player,
            List<LabApi.Features.Wrappers.Pickup> droppedPickups,
            Scp575DamageHandler scp575Handler
        )
        {
            yield return Timing.WaitForOneFrame;  // let engine spawn pickups


            foreach (var pickup in droppedPickups)
            {

                if (pickup == null)
                {
                    Library_ExiledAPI.LogWarn("DropAndPushItems", $"Pickup {pickup.Serial}:{pickup.Base.name} not found - skipping.");
                    continue;
                }

                var rb = pickup.Base.GetComponent<Rigidbody>();
                if (rb == null)
                {
                    Library_ExiledAPI.LogWarn("DropAndPushItems", $"Rigidbody missing on pickup {pickup.Serial}:{pickup.Base.name} - skipping.");
                    continue;
                }

                var dir = scp575Handler.GetRandomUnitSphereVelocity();
                var mag = scp575Handler.calculateForcePush();

                yield return Timing.WaitForOneFrame; // ensure physics engine is ready

                try
                {
                    rb.AddForce(dir * mag, ForceMode.Force);
                    Library_ExiledAPI.LogDebug("DropAndPushItems", $"Pushed item {pickup.Serial}:{pickup.Base.name} with direction {dir} and magnitude {mag}.");


                }
                catch (Exception ex)
                {
                    Library_ExiledAPI.LogError("DropAndPushItems", $"Error pushing item {pickup.Serial}:{pickup.Base.name}: {ex}");
                }

                yield return Timing.WaitForOneFrame;  // stagger pushes
            }
        }

    }
}
