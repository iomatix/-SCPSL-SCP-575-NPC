namespace SCP_575.Npc
{
    using System;
    using System.Collections.Generic;
    using Exiled.API.Enums;
    using Exiled.API.Features;
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

        private int blackoutStacks = 0;

        public void Init()
        {
            Server.RoundStarted += _plugin.Npc.EventHandlers.OnRoundStart;
            Server.RoundEnded += _plugin.Npc.EventHandlers.OnRoundEnd;
        }

        public void Disable()
        {
            Clean();
            Server.RoundStarted -= _plugin.Npc.EventHandlers.OnRoundStart;
            Server.RoundEnded -= _plugin.Npc.EventHandlers.OnRoundEnd;
        }

        public void Clean()
        {
            blackoutStacks = 0;
            ResetTeslaGates();
        }
        public IEnumerator<float> RunBlackoutTimer()
        {
            yield return Timing.WaitForSeconds(Config.InitialDelay);

            while (true)
            {
                yield return Timing.WaitForSeconds(Config.RandomEvents ? Loader.Random.Next(Config.DelayMin, Config.DelayMax) : Config.InitialDelay);
                _plugin.Npc.EventHandlers.Coroutines.Add(Timing.RunCoroutine(ExecuteBlackoutEvent()));
                
            }
        }

        private IEnumerator<float> ExecuteBlackoutEvent()
        {
            if (blackoutStacks == 0)
            {
                TriggerCassieMessage(Config.CassieMessageStart, true);

                if (Config.FlickerLights)
                {
                    FlickerAllZoneLights(Config.FlickerLightsDuration);
                }
                yield return Timing.WaitForSeconds(Config.TimeBetweenSentenceAndStart);
            }

            float blackoutDuration = Config.RandomEvents
                ? GetRandomBlackoutDuration()
                : Config.DurationMax;

            if (Config.EnableKeter)
            {
                _plugin.EventHandlers.Coroutines.Add(Timing.RunCoroutine(KeterDamage(blackoutDuration), tag: "SCP575keter"));
            }

            TriggerCassieMessage(Config.CassiePostMessage);

            bool blackoutOccurred = Config.UsePerRoomChances
                ? HandleRoomSpecificBlackout(blackoutDuration)
                : HandleZoneSpecificBlackout(blackoutDuration);

            _plugin.Npc.EventHandlers.Coroutines.Add(Timing.RunCoroutine(FinalizeBlackoutEvent(blackoutOccurred, blackoutDuration)));
        }

        private void FlickerAllZoneLights(float duration)
        {
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

            isBlackoutTriggered |= AttemptZoneBlackout(ZoneType.LightContainment, Config.ChanceLight, Config.CassieMessageLight, blackoutDuration);
            isBlackoutTriggered |= AttemptZoneBlackout(ZoneType.HeavyContainment, Config.ChanceHeavy, Config.CassieMessageHeavy, blackoutDuration);
            isBlackoutTriggered |= AttemptZoneBlackout(ZoneType.Entrance, Config.ChanceEntrance, Config.CassieMessageEntrance, blackoutDuration);
            isBlackoutTriggered |= AttemptZoneBlackout(ZoneType.Surface, Config.ChanceSurface, Config.CassieMessageSurface, blackoutDuration);

            if (blackoutStacks == 0 && !isBlackoutTriggered && Config.EnableFacilityBlackout)
            {
                TriggerFacilityWideBlackout(blackoutDuration);
                isBlackoutTriggered = true;
            }

            return isBlackoutTriggered;
        }

        private bool AttemptZoneBlackout(ZoneType zone, float chance, string cassieMessage, float blackoutDuration, bool disableSystems = false)
        {
            if (Loader.Random.NextDouble() * 100 < chance)
            {
                Map.TurnOffAllLights(blackoutDuration, zone);
                TriggerCassieMessage(cassieMessage, true);

                if (disableSystems)
                {
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
                }
            }

            if (!blackoutTriggered && Config.EnableFacilityBlackout)
            {
                TriggerFacilityWideBlackout(blackoutDuration);
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
                        return true;
                    }
                    break;
                case ZoneType.LightContainment:
                    if (Loader.Random.NextDouble() * 100 < Config.ChanceLight)
                    {
                        HandleRoomBlackout(room, blackoutDuration);
                        return true;
                    }
                    break;
                case ZoneType.Entrance:
                    if (Loader.Random.NextDouble() * 100 < Config.ChanceEntrance)
                    {
                        HandleRoomBlackout(room, blackoutDuration);
                        return true;
                    }
                    break;
                case ZoneType.Surface:
                    if (Loader.Random.NextDouble() * 100 < Config.ChanceSurface)
                    {
                        HandleRoomBlackout(room, blackoutDuration);
                        return true;
                    }
                    break;
                default:
                    if (Loader.Random.NextDouble() * 100 < Config.ChanceOther)
                    {
                        HandleRoomBlackout(room, blackoutDuration);
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
            }

            room.TurnOffLights(blackoutDuration);
        }

        private void DisableFacilitySystems(float blackoutDuration)
        {
            foreach (Room room in Room.List)
            {
                room.TurnOffLights(blackoutDuration);
            }

            ResetTeslaGates();


            if (Config.DisableNuke && Warhead.IsInProgress && !Warhead.IsLocked)
            {
                Warhead.Stop();
            }
        }

        private IEnumerator<float> FinalizeBlackoutEvent(bool blackoutOccurred, float blackoutDuration)
        {
            if (blackoutOccurred)
            {
                blackoutStacks++;
                if (Config.Voice)
                {
                    TriggerCassieMessage(Config.CassieKeter);
                }

                yield return Timing.WaitForSeconds(blackoutDuration);
                blackoutStacks--;
                if(blackoutStacks == 0) TriggerCassieMessage(Config.CassieMessageEnd);
                yield return Timing.WaitForSeconds(Config.TimeBetweenSentenceAndEnd);

                if (blackoutStacks == 0)
                {
                    Timing.KillCoroutines("SCP575keter");
                    ResetTeslaGates();
                }
            }
            else
            {
                TriggerCassieMessage(Config.CassieMessageWrong);
            }
        }

        private void ResetTeslaGates()
        {
            foreach (var teslaGate in TeslaGate.List)
            {
                ResetTeslaGate(teslaGate);
            }
        }

        private void ResetTeslaGate(TeslaGate gate)
        {
            gate.ForceTrigger();
            gate.CooldownTime = 5f;
        }

        private void TriggerCassieMessage(string message, bool isGlitchy = false)
        {
            if (isGlitchy)
            {
                Cassie.GlitchyMessage(message, Config.GlitchChance / 100, Config.JamChance / 100);
            }
            else
            {
                Cassie.Message(message, false, false, false);
            }
        }

        public IEnumerator<float> KeterDamage(float duration)
        {
            do
            {
                foreach (var player in Player.List)
                {
                    if (player.IsHuman && player.CurrentRoom.AreLightsOff && !player.HasFlashlightModuleEnabled && !(player.CurrentItem?.IsEmittingLight ?? false))
                    {
                        player.Hurt(Config.KeterDamage * blackoutStacks, Config.KilledBy);
                        player.Broadcast(Config.KeterBroadcast);
                    }

                    yield return Timing.WaitForSeconds(Config.KeterDamageDelay);
                }
            } while ((duration -= Config.KeterDamageDelay) > Config.KeterDamageDelay);
        }
    }
}