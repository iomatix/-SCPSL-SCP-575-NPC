namespace SCP_575.Npc
{
    using Shared;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using LabApi.Events.CustomHandlers;
    using MEC;
    using SCP_575.ConfigObjects;
    using UnityEngine;

    /// <summary>
    /// Manages SCP-575 NPC behaviors including blackout events, CASSIE announcements, and damage mechanics.
    /// </summary>
    public class Methods
    {
        private readonly Plugin _plugin;
        private readonly NpcConfig _config;

        /// <summary>
        /// Initializes a new instance of the <see cref="Methods"/> class.
        /// </summary>
        /// <param name="plugin">The plugin instance providing configuration and utilities.</param>
        /// <exception cref="ArgumentNullException">Thrown if plugin is null.</exception>
        public Methods(Plugin plugin)
        {
            _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin), "Plugin instance cannot be null.");
            _config = plugin.Config.NpcConfig;
        }

        private readonly HashSet<Exiled.API.Enums.ZoneType> _triggeredZones = new HashSet<Exiled.API.Enums.ZoneType>();
        private static readonly object BlackoutLock = new();
        private static int _blackoutStacks = 0;

        private readonly LightCooldownHandler _lightCooldownHandler = new LightCooldownHandler();

        /// <summary>
        /// Gets a value indicating whether a blackout is currently active.
        /// </summary>
        public bool IsBlackoutActive => _blackoutStacks > 0;

        private enum CassieStatus
        {
            Idle,
            Playing,
            Cooldown
        }

        private CassieStatus _cassieState = CassieStatus.Idle;
        private CoroutineHandle _cassieCooldownCoroutine;

        #region Initialization and Cleanup

        /// <summary>
        /// Initializes event handlers and prepares the NPC for operation.
        /// </summary>
        public void Init()
        {
            Library_ExiledAPI.LogInfo("Init", "SCP-575 NPC methods initialized.");
            RegisterEventHandlers();
        }

        /// <summary>
        /// Disables event handlers and cleans up resources.
        /// </summary>
        public void Disable()
        {
            Library_ExiledAPI.LogInfo("Disable", "SCP-575 NPC methods disabled.");
            Clean();
            UnregisterEventHandlers();

        }

        /// <summary>
        /// Cleans up active coroutines, resets blackout states, and stops global ambience.
        /// </summary>
        public void Clean()
        {
            Library_ExiledAPI.LogInfo("Clean", "SCP-575 NPC methods cleaned.");
            AudioManager.StopGlobalAmbience();
            _blackoutStacks = 0;
            _triggeredZones.Clear();
            Timing.KillCoroutines("SCP575keter");
            ResetTeslaGates();
        }

        private void RegisterEventHandlers()
        {
            Exiled.Events.Handlers.Server.RoundStarted += _plugin.Npc.EventHandlers.OnRoundStart;
            Exiled.Events.Handlers.Server.RoundEnded += _plugin.Npc.EventHandlers.OnRoundEnd;
            LabApi.Events.Handlers.ServerEvents.GeneratorActivated += _plugin.Npc.EventHandlers.OnGeneratorActivated;
            LabApi.Events.Handlers.ServerEvents.ExplosionSpawned += _plugin.Npc.EventHandlers.OnExplosionSpawned;

            if (_config.EnableKeterLightsourceCooldown)
            {
                CustomHandlersManager.RegisterEventsHandler(_lightCooldownHandler);
            }
        }

        private void UnregisterEventHandlers()
        {
            Exiled.Events.Handlers.Server.RoundStarted -= _plugin.Npc.EventHandlers.OnRoundStart;
            Exiled.Events.Handlers.Server.RoundEnded -= _plugin.Npc.EventHandlers.OnRoundEnd;
            LabApi.Events.Handlers.ServerEvents.GeneratorActivated -= _plugin.Npc.EventHandlers.OnGeneratorActivated;
            LabApi.Events.Handlers.ServerEvents.ExplosionSpawned -= _plugin.Npc.EventHandlers.OnExplosionSpawned;

            if (_config.EnableKeterLightsourceCooldown)
            {
                CustomHandlersManager.UnregisterEventsHandler(_lightCooldownHandler);
            }
        }

        #endregion

        #region Blackout Logic

        /// <summary>
        /// Runs the blackout timer, triggering blackout events at intervals.
        /// </summary>
        /// <returns>An enumerator for the coroutine.</returns>
        public IEnumerator<float> RunBlackoutTimer()
        {
            yield return Timing.WaitForSeconds(_config.InitialDelay);
            Library_ExiledAPI.LogDebug("RunBlackoutTimer", "SCP-575 NPC blackout timer started.");
            while (true)
            {
                float delay = _config.RandomEvents
                    ? Library_ExiledAPI.Loader_Random_Next(_config.DelayMin, _config.DelayMax)
                    : _config.InitialDelay;
                yield return Timing.WaitForSeconds(delay);
                _plugin.Npc.EventHandlers.Coroutines.Add(Timing.RunCoroutine(ExecuteBlackoutEvent(), "575BlackoutExec"));
            }
        }

        private IEnumerator<float> ExecuteBlackoutEvent()
        {
            if (!IsBlackoutActive)
            {
                if (_config.CassieMessageClearBeforeImportant) Library_ExiledAPI.Cassie_Clear();
                Library_ExiledAPI.LogDebug("ExecuteBlackoutEvent", "Starting blackout event...");
                TriggerCassieMessage(_config.CassieMessageStart, true);

                if (_config.FlickerLights)
                {
                    FlickerAllZoneLights(_config.FlickerLightsDuration);
                }
                yield return Timing.WaitForSeconds(_config.TimeBetweenSentenceAndStart);

                TriggerCassieMessage(_config.CassiePostMessage);
            }

            float blackoutDuration = _config.RandomEvents
                ? GetRandomBlackoutDuration()
                : _config.DurationMax;

            bool blackoutOccurred = _config.UsePerRoomChances
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
            return (float)Library_ExiledAPI.Loader_Random_NextDouble() * (_config.DurationMax - _config.DurationMin) + _config.DurationMin;
        }

        private bool HandleZoneSpecificBlackout(float blackoutDuration)
        {
            bool isBlackoutTriggered = false;

            isBlackoutTriggered |= AttemptZoneBlackout(Exiled.API.Enums.ZoneType.LightContainment, _config.ChanceLight, _config.CassieMessageLight, blackoutDuration);
            isBlackoutTriggered |= AttemptZoneBlackout(Exiled.API.Enums.ZoneType.HeavyContainment, _config.ChanceHeavy, _config.CassieMessageHeavy, blackoutDuration);
            isBlackoutTriggered |= AttemptZoneBlackout(Exiled.API.Enums.ZoneType.Entrance, _config.ChanceEntrance, _config.CassieMessageEntrance, blackoutDuration);
            isBlackoutTriggered |= AttemptZoneBlackout(Exiled.API.Enums.ZoneType.Surface, _config.ChanceSurface, _config.CassieMessageSurface, blackoutDuration);

            if (!IsBlackoutActive && !isBlackoutTriggered && _config.EnableFacilityBlackout)
            {
                TriggerFacilityWideBlackout(blackoutDuration);
                Library_ExiledAPI.LogDebug("HandleZoneSpecificBlackout", "Facility-wide blackout triggered.");
                isBlackoutTriggered = true;
            }

            return isBlackoutTriggered;
        }

        private bool AttemptZoneBlackout(Exiled.API.Enums.ZoneType zone, float chance, string cassieMessage, float blackoutDuration, bool disableSystems = false)
        {
            if (Library_ExiledAPI.Loader_Random_NextDouble() * 100 < chance)
            {
                Exiled.API.Features.Map.TurnOffAllLights(blackoutDuration, zone);
                Library_ExiledAPI.LogDebug("AttemptZoneBlackout", $"Blackout triggered in zone {zone} for {blackoutDuration} seconds.");
                if (!IsBlackoutActive) TriggerCassieMessage(cassieMessage, true);

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
            foreach (Exiled.API.Enums.ZoneType zone in Enum.GetValues(typeof(Exiled.API.Enums.ZoneType)))
            {
                Exiled.API.Features.Map.TurnOffAllLights(blackoutDuration, zone);
                Library_ExiledAPI.LogDebug("TriggerFacilityWideBlackout", $"Lights off in zone {zone} for {blackoutDuration} seconds.");
            }
            DisableFacilitySystems(blackoutDuration);
            if (!IsBlackoutActive) TriggerCassieMessage(_config.CassieMessageFacility, true);
        }

        private bool HandleRoomSpecificBlackout(float blackoutDuration)
        {
            bool blackoutTriggered = false;

            foreach (Exiled.API.Features.Room room in Library_ExiledAPI.Rooms)
            {
                if (AttemptRoomBlackout(room, blackoutDuration))
                {
                    blackoutTriggered = true;
                    Library_ExiledAPI.LogDebug("HandleRoomSpecificBlackout", $"Blackout triggered in room {room.Name}.");
                }
            }

            if (!blackoutTriggered && _config.EnableFacilityBlackout)
            {
                TriggerFacilityWideBlackout(blackoutDuration);
                Library_ExiledAPI.LogDebug("HandleRoomSpecificBlackout", "Facility-wide blackout triggered.");
                return true;
            }
            return blackoutTriggered;
        }

        public bool AttemptRoomBlackout(Exiled.API.Features.Room room, float blackoutDuration, bool isForced = false, bool isCassieSilent = false)
        {

            float chance;
            string cassieMessage;
            Exiled.API.Enums.ZoneType zone = room.Zone;

            switch (zone)
            {
                case Exiled.API.Enums.ZoneType.HeavyContainment:
                    chance = _config.ChanceHeavy;
                    cassieMessage = _config.CassieMessageHeavy;
                    break;
                case Exiled.API.Enums.ZoneType.LightContainment:
                    chance = _config.ChanceLight;
                    cassieMessage = _config.CassieMessageLight;
                    break;
                case Exiled.API.Enums.ZoneType.Entrance:
                    chance = _config.ChanceEntrance;
                    cassieMessage = _config.CassieMessageEntrance;
                    break;
                case Exiled.API.Enums.ZoneType.Surface:
                    chance = _config.ChanceSurface;
                    cassieMessage = _config.CassieMessageSurface;
                    break;
                default:
                    chance = _config.ChanceOther;
                    cassieMessage = _config.CassieMessageOther;
                    break;
            }

            if (isForced || (Library_ExiledAPI.Loader_Random_NextDouble() * 100) < chance)
            {
                HandleRoomBlackout(room, blackoutDuration);
                if (!_triggeredZones.Contains(zone))
                {
                    if (!IsBlackoutActive && !isCassieSilent) TriggerCassieMessage(cassieMessage);
                    _triggeredZones.Add(zone);
                }
                return true;
            }
            return false;
        }

        private void HandleRoomBlackout(Exiled.API.Features.Room room, float blackoutDuration)
        {
            if (!Library_ExiledAPI.IsRoomAndNeighborsFreeOfEngagedGenerators(room)) return;

            if (_config.DisableTeslas && room.Type == Exiled.API.Enums.RoomType.HczTesla)
            {
                room.TeslaGate.CooldownTime = blackoutDuration + 0.5f;
                room.TeslaGate.ForceTrigger();
            }

            if (_config.DisableNuke && room.Type == Exiled.API.Enums.RoomType.HczNuke && LabApi.Features.Wrappers.Warhead.IsDetonationInProgress && !LabApi.Features.Wrappers.Warhead.IsLocked)
            {
                LabApi.Features.Wrappers.Warhead.Stop();
                Library_ExiledAPI.LogDebug("HandleRoomBlackout", "Nuke detonation cancelled in HCZ Nuke room.");
            }

            room.TurnOffLights(blackoutDuration);
            Library_ExiledAPI.LogDebug("HandleRoomBlackout", $"Lights off in room {room.Name} for {blackoutDuration} seconds.");
        }

        private void DisableFacilitySystems(float blackoutDuration)
        {
            foreach (Exiled.API.Features.Room room in Library_ExiledAPI.Rooms)
            {
                if (Library_ExiledAPI.IsRoomAndNeighborsFreeOfEngagedGenerators(room))
                {
                    room.TurnOffLights(blackoutDuration);
                    Library_ExiledAPI.LogDebug("DisableFacilitySystems", $"Lights off in room {room.Name} for {blackoutDuration} seconds.");
                }
            }

            ResetTeslaGates();

            if (_config.DisableNuke && LabApi.Features.Wrappers.Warhead.IsDetonationInProgress && !LabApi.Features.Wrappers.Warhead.IsLocked)
            {
                LabApi.Features.Wrappers.Warhead.Stop();
                Library_ExiledAPI.LogDebug("DisableFacilitySystems", "Nuke detonation cancelled.");
            }
        }

        private IEnumerator<float> FinalizeBlackoutEvent(bool blackoutOccurred, float blackoutDuration)
        {
            if (blackoutOccurred)
            {
                IncrementBlackoutStack();
                Library_ExiledAPI.LogDebug("FinalizeBlackoutEvent", $"Blackout triggered. Stacks: {_blackoutStacks}, Duration: {blackoutDuration}");
                TriggerCassieMessage(_config.CassieKeter);


                if (_config.KeterAmbient)
                {
                    AudioManager.PlayGlobalAmbience();
                }

                yield return Timing.WaitForSeconds(blackoutDuration);
                DecrementBlackoutStack();
                Library_ExiledAPI.LogDebug("FinalizeBlackoutEvent", $"Blackout finalized. Stacks: {_blackoutStacks}");

                if (!IsBlackoutActive)
                {
                    TriggerCassieMessage(_config.CassieMessageEnd);
                    yield return Timing.WaitForSeconds(_config.TimeBetweenSentenceAndEnd);
                    ResetTeslaGates();
                    _triggeredZones.Clear();
                    AudioManager.StopGlobalAmbience();
                    Library_ExiledAPI.LogDebug("FinalizeBlackoutEvent", "Blackout completed. Systems reset.");
                }
            }
            else if (!IsBlackoutActive)
            {
                TriggerCassieMessage(_config.CassieMessageWrong);
            }
        }

        private void ResetTeslaGates()
        {
            foreach (Exiled.API.Features.TeslaGate teslaGate in Library_ExiledAPI.TeslaGates)
            {
                ResetTeslaGate(teslaGate);
                Library_ExiledAPI.LogDebug("ResetTeslaGates", $"Reset TeslaGate {teslaGate}.");
            }
        }

        private void ResetTeslaGate(Exiled.API.Features.TeslaGate gate)
        {
            gate.ForceTrigger();
            gate.CooldownTime = 5f;
            Library_ExiledAPI.LogDebug("ResetTeslaGate", $"TeslaGate {gate} reset. Cooldown: {gate.CooldownTime}");
        }

        #endregion

        #region CASSIE Management

        private void TriggerCassieMessage(string message, bool isGlitchy = false)
        {
            if (string.IsNullOrWhiteSpace(message)) return;

            if (_cassieState != CassieStatus.Idle)
            {
                Library_ExiledAPI.LogDebug("TriggerCassieMessage", $"Cassie busy ({_cassieState}), skipping: {message}");
                return;
            }

            _cassieState = CassieStatus.Playing;
            Library_ExiledAPI.LogDebug("TriggerCassieMessage", $"Triggering CASSIE: {message}");

            if (_config.CassieMessageClearBeforeImportant)
                Library_ExiledAPI.Cassie_Clear();

            if (isGlitchy)
                Library_ExiledAPI.Cassie_GlitchyMessage(message);
            else
                Library_ExiledAPI.Cassie_Message(message);

            if (_cassieCooldownCoroutine.IsRunning)
                Timing.KillCoroutines(_cassieCooldownCoroutine);

            _cassieCooldownCoroutine = Timing.RunCoroutine(CassieCooldownRoutine());
        }

        private IEnumerator<float> CassieCooldownRoutine()
        {
            yield return Timing.WaitForSeconds(_config.TimeBetweenSentenceAndStart + 0.5f);
            _cassieState = CassieStatus.Cooldown;
            yield return Timing.WaitForSeconds(1f);
            _cassieState = CassieStatus.Idle;
        }

        #endregion

        #region Damage Logic

        private bool ShouldApplyBlackoutDamage(Exiled.API.Features.Player player)
        {
            if (!player?.IsAlive ?? true)
            {
                Library_ExiledAPI.LogDebug("ShouldApplyBlackoutDamage", $"Player {player?.Nickname ?? "null"} is not alive or null.");
                return false;
            }

            Library_ExiledAPI.LogDebug("ShouldApplyBlackoutDamage", $"Checking damage for {player.Nickname}.");
            return IsHumanWithoutLight(player) && IsInDarkRoom(player);
        }

        private bool IsHumanWithoutLight(Exiled.API.Features.Player player)
        {
            if (!player.IsHuman || player.HasFlashlightModuleEnabled) return false;

            if (player.CurrentItem?.Base is InventorySystem.Items.ToggleableLights.ToggleableLightItemBase lightItem)
            {
                return !lightItem.IsEmittingLight;
            }
            return true;
        }

        private bool IsInDarkRoom(Exiled.API.Features.Player player)
        {
            return player.CurrentRoom?.AreLightsOff ?? false;
        }

        /// <summary>
        /// Applies damage to players during blackouts if conditions are met.
        /// </summary>
        /// <returns>An enumerator for the coroutine.</returns>
        public IEnumerator<float> KeterDamage()
        {
            Library_ExiledAPI.LogDebug("KeterDamage", "SCP-575 damage handler started.");
            while (true)
            {
                yield return Timing.WaitForSeconds(_config.KeterDamageDelay);
                if (IsBlackoutActive)
                {
                    Library_ExiledAPI.LogDebug("KeterDamage", $"Damage handler active. Stacks: {_blackoutStacks}");
                    foreach (LabApi.Features.Wrappers.Player player in Library_LabAPI.Players)
                    {
                        Exiled.API.Features.Player exiledPlayer = Library_ExiledAPI.ToExiledPlayer(player);
                        if (exiledPlayer == null) continue;

                        if (ShouldApplyBlackoutDamage(exiledPlayer))
                        {
                            float rawDamage = _config.KeterDamage * _blackoutStacks;
                            float clampedDamage = Mathf.Max(rawDamage, 1f);

                            yield return Timing.WaitForOneFrame;
                            Scp575DamageSystem.DamagePlayer(player, clampedDamage);

                            Timing.CallDelayed(3.75f, () =>
                            {
                                AudioManager.PlayWhispersMixedAutoManaged(player, hearableForAllPlayers: true);
                            });
                            _lightCooldownHandler.OnScp575AttacksPlayer(player);

                            if (_config.EnableKeterHint)
                            {
                                player.SendHint(_config.KeterHint);
                            }
                        }
                        else if (player.IsHuman && IsInDarkRoom(player))
                        {
                            AudioManager.PlayWhispersAutoManaged(player, hearableForAllPlayers: true);
                            _lightCooldownHandler.OnScp575AttacksPlayer(player);
                        }
                    }
                }
            }
        }
        #endregion

        #region Utility Methods

        public void IncrementBlackoutStack()
        {
            lock (BlackoutLock)
                _blackoutStacks++;
        }

        public void DecrementBlackoutStack()
        {
            lock (BlackoutLock)
                _blackoutStacks = Math.Max(0, _blackoutStacks - 1);
        }

        /// <summary>
        /// Checks if all generators are engaged.
        /// </summary>
        /// <param name="reqCountEngagedGens">Required number of engaged generators. Defaults to 3.</param>
        /// <returns>True if all generators are engaged; otherwise, false.</returns>
        public bool AreAllGeneratorsEngaged(int reqCountEngagedGens = 3)
        {
            var generators = LabApi.Features.Wrappers.Generator.List;
            return generators.Count >= reqCountEngagedGens && generators.All(gen => gen.Engaged);
        }

        /// <summary>
        /// Resets SCP-575 state, turning on all lights and resetting systems.
        /// </summary>
        public void Reset575()
        {
            Library_ExiledAPI.LogDebug("Reset575", "Resetting SCP-575 state.");
            _blackoutStacks = 0;
            foreach (LabApi.Features.Wrappers.Room room in LabApi.Features.Wrappers.Room.List)
            {
                room.LightController.LightsEnabled = true;
                room.LightController.FlickerLights(_config.FlickerLightsDuration);
            }
            _triggeredZones.Clear();
            ResetTeslaGates();
        }

        /// <summary>
        /// Determines if a explosion is dangerous to SCP-575. This method works for damagable explosions like those done by disruptor, he granades, and SCP-018.
        /// </summary>
        /// <param name="explosionType">The explosion to check.</param>
        /// <returns>True if dangerous; otherwise, false.</returns>
        public bool IsDangerousToScp575(ExplosionType explosionType)
        {
            if (explosionType == null) return false;
            return explosionType switch
            {
                ExplosionType.Grenade => true,
                ExplosionType.SCP018 => true,
                ExplosionType.Jailbird => true,
                ExplosionType.Disruptor => true,
                _ => false
            };
        }

        /// <summary>
        /// Terminates SCP-575 by cleaning up its state.
        /// </summary>
        public void Kill575()
        {
            Library_ExiledAPI.LogDebug("Kill575", "Killing SCP-575 NPC.");
            Clean();
        }

        #endregion
    }
}