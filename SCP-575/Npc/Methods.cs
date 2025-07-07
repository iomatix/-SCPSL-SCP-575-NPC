namespace SCP_575.Npc
{
    using System;
    using System.Collections.Generic;
    using Exiled.API.Enums;
    using Exiled.API.Features;
    using Exiled.API.Features.DamageHandlers;
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
        private static int blackoutStacks = 0;

        public void Init()
        {
            Log.Debug("Enabling SCP-575 Npc methods...");
            Server.RoundStarted += _plugin.Npc.EventHandlers.OnRoundStart;
            Server.RoundEnded += _plugin.Npc.EventHandlers.OnRoundEnd;
        }

        public void Disable()
        {
            Log.Debug("Disabling SCP-575 Npc methods...");
            Clean();
            Server.RoundStarted -= _plugin.Npc.EventHandlers.OnRoundStart;
            Server.RoundEnded -= _plugin.Npc.EventHandlers.OnRoundEnd;
        }

        public void Clean()
        {
            Log.Debug("Cleaning up SCP-575 Npc methods...");
            blackoutStacks = 0;
            triggeredZones.Clear();
            Timing.KillCoroutines("SCP575keter");
            ResetTeslaGates();
        }
        public IEnumerator<float> RunBlackoutTimer()
        {
            yield return Timing.WaitForSeconds(Config.InitialDelay);
            Log.Debug("Starting SCP-575 blackout event timer...");
            while (true)
            {
                yield return Timing.WaitForSeconds(Config.RandomEvents ? Loader.Random.Next(Config.DelayMin, Config.DelayMax) : Config.InitialDelay);
                _plugin.Npc.EventHandlers.Coroutines.Add(Timing.RunCoroutine(ExecuteBlackoutEvent(), "575BlackoutExec"));

            }
        }

        private bool IsBlackoutStacks() => blackoutStacks > 0;

        private IEnumerator<float> ExecuteBlackoutEvent()
        {
            if (!IsBlackoutStacks())
            {
                if (Config.CassieMessageClearBeforeImportant) Cassie.Clear();
                Log.Debug("Executing blackout event...");
                TriggerCassieMessage(Config.CassieMessageStart, true);

                if (Config.FlickerLights)
                {
                    FlickerAllZoneLights(Config.FlickerLightsDuration);
                }
                Log.Debug($"Waiting for {Config.TimeBetweenSentenceAndStart} seconds before starting blackout.");
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
            Log.Debug($"Flickering all lights for {duration} seconds.");
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

            if (!IsBlackoutStacks() && !isBlackoutTriggered && Config.EnableFacilityBlackout)
            {
                TriggerFacilityWideBlackout(blackoutDuration);
                Log.Debug("No specific zone blackout triggered, applying facility-wide blackout.");
                isBlackoutTriggered = true;
            }

            return isBlackoutTriggered;
        }

        private bool AttemptZoneBlackout(ZoneType zone, float chance, string cassieMessage, float blackoutDuration, bool disableSystems = false)
        {
            if (Loader.Random.NextDouble() * 100 < chance)
            {
                Map.TurnOffAllLights(blackoutDuration, zone);
                Log.Debug($"Turning off lights in zone {zone} for {blackoutDuration} seconds.");
                TriggerCassieMessage(cassieMessage, true);

                if (disableSystems)
                {
                    Log.Debug($"Blackout triggered in zone {zone} with blackout duration {blackoutDuration} seconds.");
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
                Log.Debug($"Turning off lights in zone {zone} for {blackoutDuration} seconds.");
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
                    Log.Debug($"Blackout triggered in room {room.Name} of type {room.Type} with blackout duration {blackoutDuration} seconds.");
                }
            }

            if (!blackoutTriggered && Config.EnableFacilityBlackout)
            {
                TriggerFacilityWideBlackout(blackoutDuration);
                Log.Debug("No specific room blackout triggered, applying facility-wide blackout.");
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
                            Log.Debug($"Blackout triggered in room {room.Name} of type {room.Type} with blackout duration {blackoutDuration} seconds.");
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
                            Log.Debug($"Blackout triggered in room {room.Name} of type {room.Type} with blackout duration {blackoutDuration} seconds.");
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
                            Log.Debug($"Blackout triggered in room {room.Name} of type {room.Type} with blackout duration {blackoutDuration} seconds.");
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
                            Log.Debug($"Blackout triggered in room {room.Name} of type {room.Type} with blackout duration {blackoutDuration} seconds.");
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
                            Log.Debug($"Blackout triggered in room {room.Name} of type {room.Type} with blackout duration {blackoutDuration} seconds.");
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
                Log.Debug("Nuke detonation cancelled due to blackout event in HCZ Nuke room.");
            }

            room.TurnOffLights(blackoutDuration);
            Log.Debug($"Turning off lights in room {room.Name} for {blackoutDuration} seconds. Room type: {room.Type}");
        }

        private void DisableFacilitySystems(float blackoutDuration)
        {
            foreach (Room room in Room.List)
            {
                room.TurnOffLights(blackoutDuration);
                Log.Debug($"Turning off lights in room {room.Name} for {blackoutDuration} seconds.");
            }

            ResetTeslaGates();


            if (Config.DisableNuke && Warhead.IsInProgress && !Warhead.IsLocked)
            {
                Warhead.Stop();
                Log.Debug("Nuke detonation cancelled due to blackout event.");
            }
        }

        private IEnumerator<float> FinalizeBlackoutEvent(bool blackoutOccurred, float blackoutDuration)
        {
            if (blackoutOccurred)
            {
                blackoutStacks++;
                Log.Debug($"Blackout event triggered. Current stacks: {blackoutStacks}, Duration: {blackoutDuration}");
                if (Config.Voice)
                {
                    TriggerCassieMessage(Config.CassieKeter);
                }

                yield return Timing.WaitForSeconds(blackoutDuration);
                blackoutStacks--;
                Log.Debug($"Blackout event ended. Current stacks: {blackoutStacks}");

                if (!IsBlackoutStacks()) TriggerCassieMessage(Config.CassieMessageEnd);
                yield return Timing.WaitForSeconds(Config.TimeBetweenSentenceAndEnd);

                if (!IsBlackoutStacks())
                {
                    ResetTeslaGates();
                    triggeredZones.Clear();
                    Log.Debug("Zones and Tesla Gates reset after blackout.");
                }

            }
            else
            {
                if (!IsBlackoutStacks()) TriggerCassieMessage(Config.CassieMessageWrong);
            }
        }

        private void ResetTeslaGates()
        {
            foreach (var teslaGate in TeslaGate.List)
            {
                ResetTeslaGate(teslaGate);
                Log.Debug($"Resetting TeslaGate {teslaGate.ToString()} after blackout.");
            }
        }

        private void ResetTeslaGate(TeslaGate gate)
        {
            gate.ForceTrigger();
            Log.Debug($"Resetting TeslaGate {gate.ToString()} after blackout.");
            gate.CooldownTime = 5f;
            Log.Debug($"TeslaGate {gate.ToString()} cooldown set to 5 seconds after blackout.");
        }

        private void TriggerCassieMessage(string message, bool isGlitchy = false)
        {
            if (string.IsNullOrEmpty(message)) return;
            Log.Debug($"Triggering CASSIE message: {message}");
            if (isGlitchy)
            {
                Cassie.GlitchyMessage(message, Config.GlitchChance / 100, Config.JamChance / 100);
            }
            else
            {
                Cassie.Message(message, false, false, false);
            }
        }

        public IEnumerator<float> KeterDamage()
        {
            while (true)
            {
                yield return Timing.WaitForSeconds(Config.KeterDamageDelay);
                if (blackoutStacks > 0)
                {
                    Log.Debug($"SCP-575 Keter damage handler active with {blackoutStacks} stacks.");
                    foreach (var player in Player.List)
                    {
                        Log.Debug($"Checking player {player.Nickname} for Keter damage during blackout.");
                        if (player.IsHuman && player.CurrentRoom.AreLightsOff && !player.HasFlashlightModuleEnabled && !(player.CurrentItem?.Base is InventorySystem.Items.ToggleableLights.ToggleableLightItemBase lightEmittingItem && lightEmittingItem.IsEmittingLight))
                        {
                            Log.Debug($"SCP-575 is attempting to deal damage to {player.Nickname} due to no light source in hand during blackout.");
                            float rawDamage = Config.KeterDamage * blackoutStacks;
                            float clampedDamage = Mathf.Max(rawDamage, 1f);
                            Scp575DamageHandler damageHandler = new Scp575DamageHandler(damage: clampedDamage, reason: Config.KilledBy);
                            player.Hurt(damageHandler);


                            Log.Debug($"SCP-575 is dealing {clampedDamage} damage to {player.Nickname} (raw: {rawDamage}) due to no light source during blackout.");
                            player.Broadcast(Config.KeterBroadcast);
                        }
                        else if (player.IsHuman)
                        {
                            Log.Debug($"SCP-575 did not deal damage to {player.Nickname} due to having a light source in hand or lights being on in their current room.");
                        }
                    }
                }
            }
        }
    }
}
