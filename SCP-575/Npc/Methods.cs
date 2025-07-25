namespace SCP_575.Npc
{
    using Handlers;
    using LabApi.Events.CustomHandlers;
    using MEC;
    using SCP_575.ConfigObjects;
    using SCP_575.Shared.Audio.Enums;
    using Shared;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Systems;
    using UnityEngine;
    using static UnityEngine.GraphicsBuffer;

    /// <summary>
    /// Manages SCP-575 NPC behaviors including blackout events, CASSIE announcements, and damage mechanics.
    /// </summary>
    public class Methods
    {
        private readonly Plugin _plugin;
        private readonly Config _config;
        private readonly NpcConfig _npcConfig;

        private readonly HashSet<Exiled.API.Enums.ZoneType> _triggeredZones = new HashSet<Exiled.API.Enums.ZoneType>();
        private static readonly object BlackoutLock = new();
        private static int _blackoutStacks = 0;

        private readonly PlayerLightsourceHandler _lightCooldownHandler;
        private readonly PlayerSanityHandler _sanityHandler;

        /// <summary>
        /// Initializes a new instance of the <see cref="Methods"/> class.
        /// </summary>
        /// <param name="plugin">The plugin instance providing configuration and utilities.</param>
        /// <exception cref="ArgumentNullException">Thrown if plugin is null.</exception>
        public Methods(Plugin plugin)
        {
            _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin), "Plugin instance cannot be null.");

            _config = Plugin.Singleton.Config;
            _npcConfig = _config.NpcConfig;

            _lightCooldownHandler = new PlayerLightsourceHandler(plugin);
            _sanityHandler = new PlayerSanityHandler(plugin);
        }

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
            RegisterEventHandler();
        }

        /// <summary>
        /// Disables event handlers and cleans up resources.
        /// </summary>
        public void Disable()
        {
            Library_ExiledAPI.LogInfo("Disable", "SCP-575 NPC methods disabled.");
            Clean();
            UnregisterEventHandler();

        }

        /// <summary>
        /// Cleans up active coroutines, resets blackout states, and stops global ambience.
        /// </summary>
        public void Clean()
        {
            Library_ExiledAPI.LogInfo("Clean", "SCP-575 NPC methods cleaned.");
            Plugin.Singleton.AudioManager.StopAmbience();
            _blackoutStacks = 0;
            _triggeredZones.Clear();
            Timing.KillCoroutines("SCP575keter");
            ResetTeslaGates();
        }

        private void RegisterEventHandler()
        {
            LabApi.Events.Handlers.ServerEvents.RoundStarted += _plugin.Npc.EventHandler.OnRoundStart;
            LabApi.Events.Handlers.ServerEvents.RoundEnded += _plugin.Npc.EventHandler.OnRoundEnd;
            LabApi.Events.Handlers.ServerEvents.GeneratorActivated += _plugin.Npc.EventHandler.OnGeneratorActivated;
            LabApi.Events.Handlers.ServerEvents.ExplosionSpawned += _plugin.Npc.EventHandler.OnExplosionSpawned;
            LabApi.Events.Handlers.ServerEvents.ProjectileExploded += _plugin.Npc.EventHandler.OnProjectileExploded;

        }

        private void UnregisterEventHandler()
        {
            LabApi.Events.Handlers.ServerEvents.RoundStarted -= _plugin.Npc.EventHandler.OnRoundStart;
            LabApi.Events.Handlers.ServerEvents.RoundEnded -= _plugin.Npc.EventHandler.OnRoundEnd;
            LabApi.Events.Handlers.ServerEvents.GeneratorActivated -= _plugin.Npc.EventHandler.OnGeneratorActivated;
            LabApi.Events.Handlers.ServerEvents.ExplosionSpawned -= _plugin.Npc.EventHandler.OnExplosionSpawned;
            LabApi.Events.Handlers.ServerEvents.ProjectileExploded -= _plugin.Npc.EventHandler.OnProjectileExploded;

        }

        #endregion

        #region Blackout Logic

        /// <summary>
        /// Runs the blackout timer, triggering blackout events at intervals.
        /// </summary>
        /// <returns>An enumerator for the coroutine.</returns>
        public IEnumerator<float> RunBlackoutTimer()
        {
            yield return Timing.WaitForSeconds(_npcConfig.InitialDelay);
            Library_ExiledAPI.LogDebug("RunBlackoutTimer", "SCP-575 NPC blackout timer started.");
            while (true)
            {
                float delay = _npcConfig.RandomEvents
                    ? Library_ExiledAPI.Loader_Random_Next(_npcConfig.DelayMin, _npcConfig.DelayMax)
                    : _npcConfig.InitialDelay;
                yield return Timing.WaitForSeconds(delay);
                _plugin.Npc.EventHandler.Coroutines.Add(Timing.RunCoroutine(ExecuteBlackoutEvent(), "575BlackoutExec"));
            }
        }

        private IEnumerator<float> ExecuteBlackoutEvent()
        {
            if (!IsBlackoutActive)
            {
                if (_npcConfig.CassieMessageClearBeforeImportant) Library_ExiledAPI.Cassie_Clear();
                Library_ExiledAPI.LogDebug("ExecuteBlackoutEvent", "Starting blackout event...");
                TriggerCassieMessage(_npcConfig.CassieMessageStart, true);

                if (_config.FlickerLights)
                {
                    FlickerAllZoneLights(_config.FlickerDuration);
                }
                yield return Timing.WaitForSeconds(_npcConfig.TimeBetweenSentenceAndStart);

                TriggerCassieMessage(_npcConfig.CassiePostMessage);
            }

            float blackoutDuration = _npcConfig.RandomEvents
                ? GetRandomBlackoutDuration()
                : _npcConfig.DurationMax;

            bool blackoutOccurred = _npcConfig.UsePerRoomChances
                ? HandleRoomSpecificBlackout(blackoutDuration)
                : HandleZoneSpecificBlackout(blackoutDuration);

            _plugin.Npc.EventHandler.Coroutines.Add(Timing.RunCoroutine(FinalizeBlackoutEvent(blackoutOccurred, blackoutDuration), "575BlackoutFinalize"));
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
            return (float)Library_ExiledAPI.Loader_Random_NextDouble() * (_npcConfig.DurationMax - _npcConfig.DurationMin) + _npcConfig.DurationMin;
        }

        private bool HandleZoneSpecificBlackout(float blackoutDuration)
        {
            bool isBlackoutTriggered = false;

            isBlackoutTriggered |= AttemptZoneBlackout(Exiled.API.Enums.ZoneType.LightContainment, _npcConfig.ChanceLight, _npcConfig.CassieMessageLight, blackoutDuration);
            isBlackoutTriggered |= AttemptZoneBlackout(Exiled.API.Enums.ZoneType.HeavyContainment, _npcConfig.ChanceHeavy, _npcConfig.CassieMessageHeavy, blackoutDuration);
            isBlackoutTriggered |= AttemptZoneBlackout(Exiled.API.Enums.ZoneType.Entrance, _npcConfig.ChanceEntrance, _npcConfig.CassieMessageEntrance, blackoutDuration);
            isBlackoutTriggered |= AttemptZoneBlackout(Exiled.API.Enums.ZoneType.Surface, _npcConfig.ChanceSurface, _npcConfig.CassieMessageSurface, blackoutDuration);

            if (!IsBlackoutActive && !isBlackoutTriggered && _npcConfig.EnableFacilityBlackout)
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
            if (!IsBlackoutActive) TriggerCassieMessage(_npcConfig.CassieMessageFacility, true);
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

            if (!blackoutTriggered && _npcConfig.EnableFacilityBlackout)
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
                    chance = _npcConfig.ChanceHeavy;
                    cassieMessage = _npcConfig.CassieMessageHeavy;
                    break;
                case Exiled.API.Enums.ZoneType.LightContainment:
                    chance = _npcConfig.ChanceLight;
                    cassieMessage = _npcConfig.CassieMessageLight;
                    break;
                case Exiled.API.Enums.ZoneType.Entrance:
                    chance = _npcConfig.ChanceEntrance;
                    cassieMessage = _npcConfig.CassieMessageEntrance;
                    break;
                case Exiled.API.Enums.ZoneType.Surface:
                    chance = _npcConfig.ChanceSurface;
                    cassieMessage = _npcConfig.CassieMessageSurface;
                    break;
                default:
                    chance = _npcConfig.ChanceOther;
                    cassieMessage = _npcConfig.CassieMessageOther;
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

            if (_npcConfig.DisableNuke && LabApi.Features.Wrappers.Warhead.IsDetonationInProgress && !LabApi.Features.Wrappers.Warhead.IsLocked)
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
                TriggerCassieMessage(_npcConfig.CassieKeter);


                if (_npcConfig.KeterAmbient)
                {
                    Plugin.Singleton.AudioManager.PlayAmbience();
                }

                yield return Timing.WaitForSeconds(blackoutDuration);
                DecrementBlackoutStack();
                Library_ExiledAPI.LogDebug("FinalizeBlackoutEvent", $"Blackout finalized. Stacks: {_blackoutStacks}");

                if (!IsBlackoutActive)
                {
                    TriggerCassieMessage(_npcConfig.CassieMessageEnd);
                    yield return Timing.WaitForSeconds(_npcConfig.TimeBetweenSentenceAndEnd);
                    ResetTeslaGates();
                    _triggeredZones.Clear();
                    Plugin.Singleton.AudioManager.StopAmbience();
                    Library_ExiledAPI.LogDebug("FinalizeBlackoutEvent", "Blackout completed. Systems reset.");
                }
            }
            else if (!IsBlackoutActive)
            {
                TriggerCassieMessage(_npcConfig.CassieMessageWrong);
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

            if (_npcConfig.CassieMessageClearBeforeImportant)
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
            yield return Timing.WaitForSeconds(_npcConfig.TimeBetweenSentenceAndStart + 0.5f);
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

        // TODO: KeterDamage -> Sanity System, checks if in the dark room -> sanity goes down
        // -> less sanity eq more negative effects and damage while SCP 575 strikes
        // Sanity regens only with pills (configurable random e.g. 2-10%) and SCP-500 (configrable default 100%)
        // E.g.
        public IEnumerator<float> KeterAction()
        {
            while (true)
            {
                yield return Timing.WaitForSeconds(_npcConfig.KeterActionDelay);

                foreach (LabApi.Features.Wrappers.Player player in LabApi.Features.Wrappers.Player.ReadyList.Where(p => (p.IsAlive && p.IsHuman)))
                {
                    PlayerSanityStageConfig stage = _sanityHandler.GetCurrentSanityStage(player); // Find matching config
                    if (stage == null) continue;
                    if (IsBlackoutActive && stage.DamageOnStrike > 0)
                    {
                        if (player.IsAlive)
                        {
                            // TODO better audio immersion
                            Timing.CallDelayed(3.75f, () =>
                            {
                                var audioOptions = new AudioKey[]
                                {
                                    AudioKey.WhispersMixed,
                                    AudioKey.Scream,
                                    AudioKey.ScreamAngry,
                                    AudioKey.WhispersBang
                                };

                                var randomIndex = UnityEngine.Random.Range(0, audioOptions.Length);
                                var selectedClip = audioOptions[randomIndex];

                                Plugin.Singleton.AudioManager.PlayAudioAutoManaged(player, selectedClip, hearableForAllPlayers: true, lifespan: 25f);

                            });

                            _sanityHandler.ApplyStageEffects(player);
                            float rawDamage = stage.DamageOnStrike * _blackoutStacks;
                            float clampedDamage = Mathf.Max(rawDamage, 1f);
                            Scp575DamageSystem.DamagePlayer(player, clampedDamage);
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

            if (!IsBlackoutActive) Reset575();
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
                room.LightController.FlickerLights(_npcConfig.FlickerLightsDuration);
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