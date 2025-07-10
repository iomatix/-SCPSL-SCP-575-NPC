namespace SCP_575.Npc
{
    using System;
    using System.Collections.Generic;
    using Exiled.API.Enums;
    using Exiled.API.Features;
    using Exiled.API.Features.Items;
    using Exiled.API.Features.Pickups;
    using Exiled.Loader;
    using MEC;
    using SCP_575.ConfigObjects;
    using UnityEngine;
    using Map = Exiled.API.Features.Map;
    using Server = Exiled.Events.Handlers.Server;

    public class Methods
    {
        private readonly Plugin _plugin;
        private NpcConfig Config => _plugin.Config.NpcConfig;
        public Methods(Plugin plugin) => _plugin = plugin;

        private readonly HashSet<ZoneType> triggeredZones = new HashSet<ZoneType>();

        private static readonly object BlackoutLock = new();
        private static int blackoutStacks = 0;
        /// <summary>
        /// Public getter indicating whether the blackout effect is currently active.
        /// Returns true if blackoutStacks is greater than zero.
        /// </summary>
        public bool IsBlackoutActive => blackoutStacks > 0;

        public void Init()
        {
            Log.Debug("[Init] Enabling SCP-575 Npc methods...");
            Server.RoundStarted += _plugin.Npc.EventHandlers.OnRoundStart;
            Server.RoundEnded += _plugin.Npc.EventHandlers.OnRoundEnd;
        }

        public void Disable()
        {
            Log.Debug("[Disable] Disabling SCP-575 Npc methods...");
            Clean();
            Server.RoundStarted -= _plugin.Npc.EventHandlers.OnRoundStart;
            Server.RoundEnded -= _plugin.Npc.EventHandlers.OnRoundEnd;
        }

        public void Clean()
        {
            Log.Debug("[Clean] Cleaning up SCP-575 Npc methods...");
            blackoutStacks = 0;
            triggeredZones.Clear();
            Timing.KillCoroutines("SCP575keter");
            ResetTeslaGates();
        }
        public IEnumerator<float> RunBlackoutTimer()
        {
            yield return Timing.WaitForSeconds(Config.InitialDelay);
            Log.Debug("[RunBlackoutTimer] Starting SCP-575 blackout event timer...");
            while (true)
            {
                yield return Timing.WaitForSeconds(Config.RandomEvents ? Loader.Random.Next(Config.DelayMin, Config.DelayMax) : Config.InitialDelay);
                _plugin.Npc.EventHandlers.Coroutines.Add(Timing.RunCoroutine(ExecuteBlackoutEvent(), "575BlackoutExec"));

            }
        }

        private IEnumerator<float> ExecuteBlackoutEvent()
        {
            if (!IsBlackoutActive)
            {
                if (Config.CassieMessageClearBeforeImportant) Cassie.Clear();
                Log.Debug("[ExecuteBlackoutEvent] Executing blackout event...");
                TriggerCassieMessage(Config.CassieMessageStart, true);

                if (Config.FlickerLights)
                {
                    FlickerAllZoneLights(Config.FlickerLightsDuration);
                }
                Log.Debug($"[ExecuteBlackoutEvent] Waiting for {Config.TimeBetweenSentenceAndStart} seconds before starting blackout.");
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
            Log.Debug($"[FlickerAllZoneLights] Flickering all lights for {duration} seconds.");
            foreach (ZoneType zone in Enum.GetValues(typeof(ZoneType)))
            {
                Map.TurnOffAllLights(duration, zone);
            }
        }

        private float GetRandomBlackoutDuration()
        {
            return (float)Loader.Random.NextDouble() * (Config.DurationMax - Config.DurationMin) + Config.DurationMin;
        }

        private bool HandleZoneSpecificBlackout(float blackoutDuration)
        {
            bool isBlackoutTriggered = false;

            // Use OR-assignment to combine results from all attempts to prevent overriding.
            isBlackoutTriggered |= AttemptZoneBlackout(ZoneType.LightContainment, Config.ChanceLight, Config.CassieMessageLight, blackoutDuration);
            isBlackoutTriggered |= AttemptZoneBlackout(ZoneType.HeavyContainment, Config.ChanceHeavy, Config.CassieMessageHeavy, blackoutDuration);
            isBlackoutTriggered |= AttemptZoneBlackout(ZoneType.Entrance, Config.ChanceEntrance, Config.CassieMessageEntrance, blackoutDuration);
            isBlackoutTriggered |= AttemptZoneBlackout(ZoneType.Surface, Config.ChanceSurface, Config.CassieMessageSurface, blackoutDuration);

            if (!IsBlackoutActive && !isBlackoutTriggered && Config.EnableFacilityBlackout)
            {
                TriggerFacilityWideBlackout(blackoutDuration);
                Log.Debug("[HandleZoneSpecificBlackout] No specific zone blackout triggered, applying facility-wide blackout.");
                isBlackoutTriggered = true;
            }

            return isBlackoutTriggered;
        }

        private bool AttemptZoneBlackout(ZoneType zone, float chance, string cassieMessage, float blackoutDuration, bool disableSystems = false)
        {
            if (Loader.Random.NextDouble() * 100 < chance)
            {
                Map.TurnOffAllLights(blackoutDuration, zone);
                Log.Debug($"[AttemptZoneBlackout] Turning off lights in zone {zone} for {blackoutDuration} seconds.");
                TriggerCassieMessage(cassieMessage, true);

                if (disableSystems)
                {
                    Log.Debug($"[AttemptZoneBlackout] Blackout triggered in zone {zone} with blackout duration {blackoutDuration} seconds.");
                    DisableFacilitySystems(blackoutDuration);
                }

                return true;
            }

            return false;
        }

        private void TriggerFacilityWideBlackout(float blackoutDuration)
        {
            foreach (ZoneType zone in Enum.GetValues(typeof(ZoneType)))
            {
                Map.TurnOffAllLights(blackoutDuration, zone);
                Log.Debug($"[TriggerFacilityWideBlackout] Turning off lights in zone {zone} for {blackoutDuration} seconds.");
            }

            DisableFacilitySystems(blackoutDuration);
            TriggerCassieMessage(Config.CassieMessageFacility, true);
        }

        private bool HandleRoomSpecificBlackout(float blackoutDuration)
        {
            bool blackoutTriggered = false;

            foreach (var room in Room.List)
            {
                if (AttemptRoomBlackout(room, blackoutDuration))
                {
                    blackoutTriggered = true;
                    Log.Debug($"[HandleRoomSpecificBlackout] Blackout triggered in room {room.Name} of type {room.Type} with blackout duration {blackoutDuration} seconds.");
                }
            }

            if (!blackoutTriggered && Config.EnableFacilityBlackout)
            {
                TriggerFacilityWideBlackout(blackoutDuration);
                Log.Debug("[HandleRoomSpecificBlackout] No specific room blackout triggered, applying facility-wide blackout.");
                return true;
            }

            return blackoutTriggered;
        }

        private bool AttemptRoomBlackout(Room room, float blackoutDuration)
        {

            switch (room.Zone)
            {
                case ZoneType.HeavyContainment:
                    if (Loader.Random.NextDouble() * 100 < Config.ChanceHeavy)
                    {
                        HandleRoomBlackout(room, blackoutDuration);
                        if (!triggeredZones.Contains(ZoneType.HeavyContainment))
                        {
                            TriggerCassieMessage(Config.CassieMessageHeavy);
                            triggeredZones.Add(ZoneType.HeavyContainment);
                            Log.Debug($"[AttemptRoomBlackout] Blackout triggered in room {room.Name} of type {room.Type} with blackout duration {blackoutDuration} seconds.");
                        }
                        return true;
                    }
                    break;
                case ZoneType.LightContainment:
                    if (Loader.Random.NextDouble() * 100 < Config.ChanceLight)
                    {
                        HandleRoomBlackout(room, blackoutDuration);
                        if (!triggeredZones.Contains(ZoneType.LightContainment))
                        {
                            TriggerCassieMessage(Config.CassieMessageLight);
                            triggeredZones.Add(ZoneType.LightContainment);
                            Log.Debug($"[AttemptRoomBlackout] Blackout triggered in room {room.Name} of type {room.Type} with blackout duration {blackoutDuration} seconds.");
                        }
                        return true;
                    }
                    break;
                case ZoneType.Entrance:
                    if (Loader.Random.NextDouble() * 100 < Config.ChanceEntrance)
                    {
                        HandleRoomBlackout(room, blackoutDuration);
                        if (!triggeredZones.Contains(ZoneType.Entrance))
                        {
                            TriggerCassieMessage(Config.CassieMessageEntrance);
                            triggeredZones.Add(ZoneType.Entrance);
                            Log.Debug($"[AttemptRoomBlackout] Blackout triggered in room {room.Name} of type {room.Type} with blackout duration {blackoutDuration} seconds.");
                        }
                        return true;
                    }
                    break;
                case ZoneType.Surface:
                    if (Loader.Random.NextDouble() * 100 < Config.ChanceSurface)
                    {
                        HandleRoomBlackout(room, blackoutDuration);
                        if (!triggeredZones.Contains(ZoneType.Surface))
                        {
                            TriggerCassieMessage(Config.CassieMessageSurface);
                            triggeredZones.Add(ZoneType.Surface);
                            Log.Debug($"[AttemptRoomBlackout] Blackout triggered in room {room.Name} of type {room.Type} with blackout duration {blackoutDuration} seconds.");
                        }
                        return true;
                    }
                    break;
                default:
                    if (Loader.Random.NextDouble() * 100 < Config.ChanceOther)
                    {
                        HandleRoomBlackout(room, blackoutDuration);
                        if (!triggeredZones.Contains(ZoneType.Other))
                        {
                            TriggerCassieMessage(Config.CassieMessageOther);
                            triggeredZones.Add(ZoneType.Other);
                            Log.Debug($"[AttemptRoomBlackout] Blackout triggered in room {room.Name} of type {room.Type} with blackout duration {blackoutDuration} seconds.");
                        }
                        return true;
                    }
                    break;
            }

            return false;
        }

        private void HandleRoomBlackout(Room room, float blackoutDuration)
        {
            if (Config.DisableTeslas && room.Type.Equals(RoomType.HczTesla))
            {
                room.TeslaGate.CooldownTime = blackoutDuration + 0.5f;
                room.TeslaGate.ForceTrigger();
            }

            if (Config.DisableNuke && room.Type.Equals(RoomType.HczNuke) && Warhead.IsInProgress && !Warhead.IsLocked)
            {
                Warhead.Stop();
                Log.Debug("[HandleRoomBlackout] Nuke detonation cancelled due to blackout event in HCZ Nuke room.");
            }

            room.TurnOffLights(blackoutDuration);
            Log.Debug($"[HandleRoomBlackout] Turning off lights in room {room.Name} for {blackoutDuration} seconds. Room type: {room.Type}");
        }

        private void DisableFacilitySystems(float blackoutDuration)
        {
            foreach (Room room in Room.List)
            {
                room.TurnOffLights(blackoutDuration);
                Log.Debug($"[DisableFacilitySystems] Turning off lights in room {room.Name} for {blackoutDuration} seconds.");
            }

            ResetTeslaGates();


            if (Config.DisableNuke && Warhead.IsInProgress && !Warhead.IsLocked)
            {
                Warhead.Stop();
                Log.Debug("[DisableFacilitySystems] Nuke detonation cancelled due to blackout event.");
            }
        }

        private IEnumerator<float> FinalizeBlackoutEvent(bool blackoutOccurred, float blackoutDuration)
        {
            if (blackoutOccurred)
            {
                IncrementBlackoutStack();
                Log.Debug($"[FinalizeBlackoutEvent] Increased blackoutStacks to {blackoutStacks}");
                Log.Debug($"[FinalizeBlackoutEvent] Blackout event triggered. Current stacks: {blackoutStacks}, Duration: {blackoutDuration}");
                if (Config.Voice)
                {
                    TriggerCassieMessage(Config.CassieKeter);
                }

                yield return Timing.WaitForSeconds(blackoutDuration);
                DecrementBlackoutStack();
                Log.Debug($"[FinalizeBlackoutEvent] Blackout event ended. Current stacks: {blackoutStacks}");

                if (!IsBlackoutActive) TriggerCassieMessage(Config.CassieMessageEnd);
                yield return Timing.WaitForSeconds(Config.TimeBetweenSentenceAndEnd);

                if (!IsBlackoutActive)
                {
                    ResetTeslaGates();
                    triggeredZones.Clear();
                    Log.Debug("[FinalizeBlackoutEvent] Zones and Tesla Gates reset after blackout.");
                }

            }
            else
            {
                if (!IsBlackoutActive) TriggerCassieMessage(Config.CassieMessageWrong);
            }
        }

        private void ResetTeslaGates()
        {
            foreach (var teslaGate in TeslaGate.List)
            {
                ResetTeslaGate(teslaGate);
                Log.Debug($"[ResetTeslaGates] Resetting TeslaGate {teslaGate.ToString()} after blackout.");
            }
        }

        private void ResetTeslaGate(TeslaGate gate)
        {
            gate.ForceTrigger();
            Log.Debug($"[ResetTeslaGate] Resetting TeslaGate {gate.ToString()} after blackout.");
            gate.CooldownTime = 5f;
            Log.Debug($"[ResetTeslaGate] TeslaGate {gate.ToString()} cooldown set to 5 seconds after blackout.");
        }

        private void TriggerCassieMessage(string message, bool isGlitchy = false)
        {
            if (string.IsNullOrEmpty(message)) return;
            Log.Debug($"[TriggerCassieMessage] Triggering CASSIE message: {message}");
            if (isGlitchy)
            {
                Cassie.GlitchyMessage(message, Config.GlitchChance / 100, Config.JamChance / 100);
            }
            else
            {
                Cassie.Message(message, false, false, false);
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

        private bool ShouldApplyBlackoutDamage(Player player)
        {
            Log.Debug($"Checking if SCP-575 should apply damage to {player.Nickname}");
            return IsHumanWithoutLight(player) && IsInDarkRoom(player);
        }

        private bool IsHumanWithoutLight(Player player)
        {
            Log.Debug($"Checking if player {player.Nickname} is human and has no light source in hand.");
            if (!player.IsHuman || player.HasFlashlightModuleEnabled)
                return false;

            Log.Debug($"[IsHumanWithoutLight] Player {player.Nickname} is human and does not have flashlight module enabled.");
            Log.Debug($"[IsHumanWithoutLight] Current item in hand: {player.CurrentItem?.Base?.name ?? "None"}");
            // Check if current item is a light-emitting item and if it's on
            Log.Debug($"[IsHumanWithoutLight] Current item is toggleable light: {player.CurrentItem?.Base is InventorySystem.Items.ToggleableLights.ToggleableLightItemBase}");
            if (player.CurrentItem?.Base is InventorySystem.Items.ToggleableLights.ToggleableLightItemBase lightItem)
            {
                return !lightItem.IsEmittingLight;
            }
            Log.Debug($"[IsHumanWithoutLight] Player {player.Nickname} has no light source in hand.");
            return true;
        }

        private bool IsInDarkRoom(Player player)
        {
            Log.Debug($"Checking if player {player.Nickname} is in a dark room.");
            return player.CurrentRoom?.AreLightsOff ?? false;
        }

        public IEnumerator<float> KeterDamage()
        {
            Log.Debug("[KeterDamage] KeterDamage() Called: Starting SCP-575 Keter damage coroutine...");
            while (true)
            {
                yield return Timing.WaitForSeconds(Config.KeterDamageDelay);
                Log.Debug($"[KeterDamage] SCP-575 Keter damage handler check with {blackoutStacks} stacks.");
                if (IsBlackoutActive)
                {
                    Log.Debug($"SCP-575 Keter damage handler active with {blackoutStacks} stacks.");
                    foreach (Player player in Player.List)
                    {
                        Log.Debug($"[KeterDamage] Checking player {player.Nickname} for Keter damage during blackout.");

                        if (ShouldApplyBlackoutDamage(player))
                        {
                            Log.Debug($"SCP-575 is attempting to deal damage to {player.Nickname} due to no light source in hand during blackout.");
                            float rawDamage = Config.KeterDamage * blackoutStacks;
                            float clampedDamage = Mathf.Max(rawDamage, 1f);
                            Scp575DamageHandler damageHandler = new Scp575DamageHandler(player, damage: clampedDamage);

                            yield return Timing.WaitForOneFrame; // Ensure engine is ready before applying damage

                            player.Hurt(damageHandler);

                            Log.Debug($"[KeterDamage] SCP-575 has dealt {clampedDamage} damage to {player.Nickname} (raw: {rawDamage}) due to no light source during blackout.");
                            player.Broadcast(Config.KeterBroadcast);
                        }
                        else if (player.IsHuman)
                        {
                            Log.Debug($"[KeterDamage] SCP-575 did not deal damage to {player.Nickname} due to having a light source in hand or lights being on in their current room.");
                        }
                    }
                }
            }
        }

        public IEnumerator<float> DropAndPushItems(
            Player player,
            List<LabApi.Features.Wrappers.Item> itemsToDrop,
            Scp575DamageHandler scp575Handler
        )
        {
            yield return Timing.WaitForOneFrame;  // let engine spawn pickups

            foreach (var item in itemsToDrop)
            {
                var pickup = Pickup.Get(item.Serial);
                if (pickup == null)
                {
                    Log.Warn($"[DropAndPush] Pickup {item.Serial} not found - skipping.");
                    continue;
                }

                var rb = pickup.Base.GetComponent<Rigidbody>();
                if (rb == null)
                {
                    Log.Warn($"[DropAndPush] Rigidbody missing on {item.Serial}.");
                    continue;
                }

                var dir = scp575Handler.GetRandomUnitSphereVelocity();
                var mag = scp575Handler.calculateForcePush();

                yield return Timing.WaitForOneFrame; // ensure physics engine is ready

                try
                {
                    rb.AddForce(dir * mag, ForceMode.Impulse);
                    Log.Debug($"[DropAndPush] Pushed {pickup.Info.ItemId} [id:{item.Serial}]: dir={dir}, mag={mag}");
                }
                catch (Exception ex)
                {
                    Log.Error($"[DropAndPush] Error pushing {item.Serial}: {ex}");
                }

                yield return Timing.WaitForOneFrame;  // stagger pushes
            }
        }

    }
}
