namespace SCP_575.Npc
{
    using Handlers;
    using LabApi.Features.Wrappers;
    using MapGeneration;
    using MEC;
    using SCP_575.ConfigObjects;
    using SCP_575.Shared.Audio.Enums;
    using Shared;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEngine;

    /// <summary>
    /// Manages SCP-575 NPC behaviors, including blackout events, CASSIE announcements, and damage mechanics.
    /// </summary>
    public class Methods
    {
        private readonly Plugin _plugin;
        private readonly Config _config;
        private readonly NpcConfig _npcConfig;
        private readonly PlayerLightsourceHandler _lightsourceHandler;
        private readonly PlayerSanityHandler _sanityHandler;
        private readonly LibraryLabAPI _libraryLabAPI;
        private readonly HashSet<FacilityZone> _triggeredZones = new();
        private static readonly object BlackoutLock = new();
        private static int _blackoutStacks = 0;
        private CassieStatus _cassieState = CassieStatus.Idle;

        private enum CassieStatus
        {
            Idle,
            Playing,
            Cooldown
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Methods"/> class.
        /// </summary>
        /// <param name="plugin">The plugin instance providing configuration and utilities.</param>
        /// <exception cref="ArgumentNullException">Thrown if plugin is null.</exception>
        public Methods(Plugin plugin)
        {
            _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin), "Plugin instance cannot be null.");
            _config = _plugin.Config;
            _npcConfig = _config.NpcConfig;

            // Initialize all critical dependencies with proper null checks
            _lightsourceHandler = _plugin.LightsourceHandler ?? throw new InvalidOperationException("LightsourceHandler is null. Plugin cannot function without it.");
            _sanityHandler = _plugin.SanityEventHandler ?? throw new InvalidOperationException("SanityEventHandler is null. Plugin cannot function without it.");

            // Initialize the LabAPI with dependency injection
            _libraryLabAPI = _plugin.LibraryLabAPI ?? throw new InvalidOperationException("LibraryLabAPI is null. Plugin cannot function without it.");

            LibraryExiledAPI.LogDebug(nameof(Methods), $"Dependencies initialized:\n" +
                $"• LightsourceHandler: {_lightsourceHandler.GetHashCode()}\n" +
                $"• SanityHandler: {_sanityHandler.GetHashCode()}\n" +
                $"• LibraryLabAPI: {_libraryLabAPI?.GetHashCode()}\n" +
                $"• Config v{_plugin.Version}");
        }

        /// <summary>
        /// Gets a value indicating whether a blackout is currently active.
        /// </summary>
        public bool IsBlackoutActive => _blackoutStacks > 0;

        /// <summary>
        /// Gets a value indicating how many stacks are currently active.
        /// </summary>
        public int GetCurrentBlackoutStacks => _blackoutStacks;

        #region Lifecycle Management

        /// <summary>
        /// Initializes event handlers and prepares the NPC for operation.
        /// </summary>
        public void Init(float roll = -1f)
        {
            LibraryExiledAPI.LogInfo("Init", "SCP-575 NPC methods initialized.");
            RegisterEventHandlers();
            if (roll <= _config.BlackoutConfig.EventChance)
            {
                _plugin.IsEventActive = true;
                LibraryExiledAPI.LogInfo(nameof(Init), "SCP-575 NPC spawning due to roll being within spawn chance.");

                _plugin.Npc.Methods.StartBlackoutEventLoop();
                _plugin.Npc.Methods.StartKeterActionLoop();
                _plugin.Npc.Methods.StartSanityHandlerLoop();

                foreach (var player in LabApi.Features.Wrappers.Player.ReadyList)
                {
                    if (_plugin.SanityEventHandler.IsValidPlayer(player))
                    {
                        _plugin.SanityEventHandler.SetSanity(player, _plugin.Config.SanityConfig.InitialSanity);
                    }
                }
            }
            else
            {
                LibraryExiledAPI.LogDebug(nameof(Init), "Skipping SCP-575 NPC spawn — roll yielded no chance.");
            }
        }

        /// <summary>
        /// Disables event handlers and cleans up resources.
        /// </summary>
        public void Disable()
        {
            LibraryExiledAPI.LogInfo("Disable", "SCP-575 NPC methods disabled.");
            Clean();
            _plugin.IsEventActive = false;
            Timing.KillCoroutines("SCP575-BlackoutLoop");
            Timing.KillCoroutines("SCP575-ActionLoop");
            _plugin.SanityEventHandler.Clean();
            UnregisterEventHandlers();
        }

        /// <summary>
        /// Cleans up active coroutines, resets blackout states, and stops global ambience.
        /// </summary>
        public void Clean()
        {
            LibraryExiledAPI.LogInfo("Clean", "SCP-575 NPC methods cleaned.");

            Timing.KillCoroutines("SCP575-CassieCD");

            // Restart Coroutines Loops to break any active actions
            StartBlackoutEventLoop();
            StartKeterActionLoop();
            StartSanityHandlerLoop();

            Plugin.Singleton.AudioManager.StopAmbience();
            _blackoutStacks = 0;
            _triggeredZones.Clear();
            LabApi.Features.Wrappers.Map.ResetColorOfLights();
            LabApi.Features.Wrappers.Map.TurnOnLights();
            ResetTeslaGates();
        }

        private void RegisterEventHandlers()
        {
            LabApi.Events.Handlers.ServerEvents.RoundStarted += _plugin.Npc.EventHandler.OnRoundStart;
            LabApi.Events.Handlers.ServerEvents.RoundEnded += _plugin.Npc.EventHandler.OnRoundEnd;
            LabApi.Events.Handlers.ServerEvents.GeneratorActivated += _plugin.Npc.EventHandler.OnGeneratorActivated;
            LabApi.Events.Handlers.ServerEvents.ExplosionSpawned += _plugin.Npc.EventHandler.OnExplosionSpawned;
            LabApi.Events.Handlers.ServerEvents.ProjectileExploded += _plugin.Npc.EventHandler.OnProjectileExploded;
        }

        private void UnregisterEventHandlers()
        {
            LabApi.Events.Handlers.ServerEvents.RoundStarted -= _plugin.Npc.EventHandler.OnRoundStart;
            LabApi.Events.Handlers.ServerEvents.RoundEnded -= _plugin.Npc.EventHandler.OnRoundEnd;
            LabApi.Events.Handlers.ServerEvents.GeneratorActivated -= _plugin.Npc.EventHandler.OnGeneratorActivated;
            LabApi.Events.Handlers.ServerEvents.ExplosionSpawned -= _plugin.Npc.EventHandler.OnExplosionSpawned;
            LabApi.Events.Handlers.ServerEvents.ProjectileExploded -= _plugin.Npc.EventHandler.OnProjectileExploded;
        }

        #endregion

        #region Blackout Management

        /// <summary>
        /// Runs the Blackout loop, triggering blackout events at intervals.
        /// </summary>
        /// <returns>An enumerator for the coroutine.</returns>
        public IEnumerator<float> RunBlackoutLoop()
        {
            yield return Timing.WaitForSeconds(_config.BlackoutConfig.InitialDelay);
            LibraryExiledAPI.LogDebug("RunBlackoutTimer", "SCP-575 NPC blackout timer started.");

            while (true)
            {
                if (!_plugin.IsEventActive)
                {
                    LibraryExiledAPI.LogDebug("RunBlackoutTimer", "Event is inactive, waiting for reactivation.");
                    yield return Timing.WaitForSeconds(1f); // Poll for reactivation
                    continue;
                }

                float delay = _config.BlackoutConfig.RandomEvents
                    ? UnityEngine.Random.Range(_config.BlackoutConfig.DelayMin, _config.BlackoutConfig.DelayMax)
                    : _config.BlackoutConfig.InitialDelay;
                yield return Timing.WaitForSeconds(delay);

                _plugin.Npc.EventHandler.Coroutines.Add(Timing.RunCoroutine(ExecuteBlackoutEvent(), "SCP575-BlackoutExec"));
            }
        }

        private IEnumerator<float> ExecuteBlackoutEvent()
        {
            if (!IsBlackoutActive)
            {
                if (_config.CassieConfig.CassieMessageClearBeforeImportant)
                    LibraryExiledAPI.ClearCassieQueue();

                LibraryExiledAPI.LogDebug("ExecuteBlackoutEvent", "Starting blackout event...");
                TriggerCassieMessage(_config.CassieConfig.CassieMessageStart, true);
                if (_config.BlackoutConfig.FlickerLights)
                    FlickerAffectedZones(_config.BlackoutConfig.FlickerDuration);

                yield return Timing.WaitForSeconds(_config.CassieConfig.TimeBetweenSentenceAndStart);
                TriggerCassieMessage(_config.CassieConfig.CassiePostMessage);
            }

            float blackoutDuration = _config.BlackoutConfig.RandomEvents
                ? GetRandomBlackoutDuration()
                : _config.BlackoutConfig.DurationMax;

            bool blackoutOccurred = _config.BlackoutConfig.UsePerRoomChances
                ? HandleRoomSpecificBlackout(blackoutDuration)
                : HandleZoneSpecificBlackout(blackoutDuration);

            _plugin.Npc.EventHandler.Coroutines.Add(Timing.RunCoroutine(FinalizeBlackoutEvent(blackoutOccurred, blackoutDuration), "SCP575-BlackoutFin"));
        }


        private void FlickerAffectedZones(float blackoutDuration)
        {
            if (!_plugin.IsEventActive) return;

            if (!_config.BlackoutConfig.FlickerLights) return;

            var zonesToFlicker = GetZonesToFlicker();
            zonesToFlicker.ForEach(zone =>
                _plugin.Npc.EventHandler.Coroutines.Add(Timing.RunCoroutine(FlickerZoneLightsCoroutine(zone), "SCP575-FlickerZoneTask"))
            );
        }

        private List<FacilityZone> GetZonesToFlicker()
        {
            var zones = new List<FacilityZone>();
            if (_config.BlackoutConfig.UsePerRoomChances || _config.BlackoutConfig.EnableFacilityBlackout)
            {
                zones.AddRange(Enum.GetValues(typeof(FacilityZone)).Cast<FacilityZone>());
            }
            else
            {
                if (_config.BlackoutConfig.ChanceLight > 0) zones.Add(FacilityZone.LightContainment);
                if (_config.BlackoutConfig.ChanceHeavy > 0) zones.Add(FacilityZone.HeavyContainment);
                if (_config.BlackoutConfig.ChanceEntrance > 0) zones.Add(FacilityZone.Entrance);
                if (_config.BlackoutConfig.ChanceSurface > 0) zones.Add(FacilityZone.Surface);
            }
            return zones;
        }

        public IEnumerator<float> FlickerZoneLightsCoroutine(FacilityZone targetZone)
        {
            var blackoutColor = new UnityEngine.Color(
                _config.BlackoutConfig.LightsColorR,
                _config.BlackoutConfig.LightsColorG,
                _config.BlackoutConfig.LightsColorB,
                1f);

            float flickerInterval = 1f / _config.BlackoutConfig.FlickerFrequency;
            int totalFlickers = Mathf.RoundToInt(_config.BlackoutConfig.FlickerDuration / flickerInterval);
            float halfInterval = flickerInterval * 0.5f;

            Map.SetColorOfLights(blackoutColor, targetZone);

            for (int i = 0; i < totalFlickers; i++)
            {
                Map.TurnOffLights(halfInterval, targetZone);
                yield return Timing.WaitForSeconds(halfInterval);

                Map.TurnOnLights(targetZone);
                yield return Timing.WaitForSeconds(halfInterval);
            }

            Map.ResetColorOfLights(targetZone);
            Map.TurnOnLights(targetZone);
            LibraryExiledAPI.LogDebug("FlickerZoneLightsCoroutine", $"Flickering completed for zone {targetZone}");
        }

        private float GetRandomBlackoutDuration()
        {
            return UnityEngine.Random.Range(_config.BlackoutConfig.DurationMin, _config.BlackoutConfig.DurationMax);
        }

        private bool HandleZoneSpecificBlackout(float blackoutDuration)
        {
            if (!_plugin.IsEventActive) return false;

            bool isBlackoutTriggered = false;
            isBlackoutTriggered |= AttemptZoneBlackout(FacilityZone.LightContainment, _config.BlackoutConfig.ChanceLight, _config.CassieConfig.CassieMessageLight, blackoutDuration);
            isBlackoutTriggered |= AttemptZoneBlackout(FacilityZone.HeavyContainment, _config.BlackoutConfig.ChanceHeavy, _config.CassieConfig.CassieMessageHeavy, blackoutDuration);
            isBlackoutTriggered |= AttemptZoneBlackout(FacilityZone.Entrance, _config.BlackoutConfig.ChanceEntrance, _config.CassieConfig.CassieMessageEntrance, blackoutDuration);
            isBlackoutTriggered |= AttemptZoneBlackout(FacilityZone.Surface, _config.BlackoutConfig.ChanceSurface, _config.CassieConfig.CassieMessageSurface, blackoutDuration);

            if (!isBlackoutTriggered && _config.BlackoutConfig.EnableFacilityBlackout)
            {
                TriggerFacilityWideBlackout(blackoutDuration);
                LibraryExiledAPI.LogDebug("HandleZoneSpecificBlackout", "Facility-wide blackout triggered.");
                return true;
            }
            return isBlackoutTriggered;
        }

        private bool AttemptZoneBlackout(FacilityZone zone, float chance, string cassieMessage, float blackoutDuration, bool disableSystems = false)
        {
            if (!_plugin.IsEventActive) return false;

            if (UnityEngine.Random.Range(0f, 100f) >= chance) return false;

            Map.TurnOffLights(blackoutDuration, zone);
            LibraryExiledAPI.LogDebug("AttemptZoneBlackout", $"Blackout triggered in zone {zone} for {blackoutDuration} seconds.");

            if (!IsBlackoutActive) TriggerCassieMessage(cassieMessage, true);
            if (disableSystems) DisableFacilitySystems(blackoutDuration);
            return true;
        }

        private void TriggerFacilityWideBlackout(float blackoutDuration)
        {
            if (!_plugin.IsEventActive) return;

            Map.TurnOffLights(blackoutDuration);
            LibraryExiledAPI.LogDebug("TriggerFacilityWideBlackout", $"Lights off in the Facility for {blackoutDuration} seconds.");
            DisableFacilitySystems(blackoutDuration);
            if (!IsBlackoutActive) TriggerCassieMessage(_config.CassieConfig.CassieMessageFacility, true);
        }

        private bool HandleRoomSpecificBlackout(float blackoutDuration)
        {
            if (!_plugin.IsEventActive) return false;

            bool blackoutTriggered = false;
            foreach (Room room in _libraryLabAPI.Rooms)
            {
                // Skip rooms without light controllers
                if (!room.AllLightControllers.Any()) continue;


                if (AttemptRoomBlackout(room, blackoutDuration))
                {
                    blackoutTriggered = true;
                    LibraryExiledAPI.LogDebug("HandleRoomSpecificBlackout", $"Blackout triggered in room {room.Name}.");
                }
            }

            if (!blackoutTriggered && _config.BlackoutConfig.EnableFacilityBlackout)
            {
                TriggerFacilityWideBlackout(blackoutDuration);
                LibraryExiledAPI.LogDebug("HandleRoomSpecificBlackout", "Facility-wide blackout triggered.");
                return true;
            }
            return blackoutTriggered;
        }

        /// <summary>
        /// Attempts to trigger a blackout in a specific room based on zone-specific chances.
        /// </summary>
        /// <param name="room">The room to attempt blackout in.</param>
        /// <param name="blackoutDuration">Duration of the blackout.</param>
        /// <param name="isForced">If true, forces the blackout regardless of chance.</param>
        /// <param name="isCassieSilent">If true, suppresses CASSIE messages.</param>
        /// <returns>True if blackout was triggered; otherwise, false.</returns>
        public bool AttemptRoomBlackout(Room room, float blackoutDuration, bool isForced = false, bool isCassieSilent = false)
        {
            if (!_plugin.IsEventActive) return false;
            if (room == null) return false;

            var (chance, cassieMessage) = GetRoomBlackoutParams(room.Zone);
            if (!isForced && UnityEngine.Random.Range(0f, 100f) >= chance) return false;

            HandleRoomBlackout(room, blackoutDuration);
            if (!_triggeredZones.Contains(room.Zone) && !isCassieSilent)
            {
                if (!IsBlackoutActive) TriggerCassieMessage(cassieMessage);
                _triggeredZones.Add(room.Zone);
            }
            return true;
        }

        private (float Chance, string CassieMessage) GetRoomBlackoutParams(FacilityZone zone)
        {
            return zone switch
            {
                FacilityZone.HeavyContainment => (_config.BlackoutConfig.ChanceHeavy, _config.CassieConfig.CassieMessageHeavy),
                FacilityZone.LightContainment => (_config.BlackoutConfig.ChanceLight, _config.CassieConfig.CassieMessageLight),
                FacilityZone.Entrance => (_config.BlackoutConfig.ChanceEntrance, _config.CassieConfig.CassieMessageEntrance),
                FacilityZone.Surface => (_config.BlackoutConfig.ChanceSurface, _config.CassieConfig.CassieMessageSurface),
                _ => (_config.BlackoutConfig.ChanceOther, _config.CassieConfig.CassieMessageOther)
            };
        }

        private void HandleRoomBlackout(Room room, float blackoutDuration)
        {
            if (room == null)
            {
                LibraryExiledAPI.LogError(nameof(HandleRoomBlackout), "Blackout aborted: Room reference is null");
                return;
            }

            try
            {
                if (!_libraryLabAPI.IsRoomAndNeighborsFreeOfEngagedGenerators(room))
                {
                    LibraryExiledAPI.LogDebug(nameof(HandleRoomBlackout),
                        $"Skipping blackout in {room.Name}: Engaged generators present");
                    return;
                }

                HandleTeslaBlackout(room, blackoutDuration);
                HandleWarheadBlackout();
                ExecuteRoomBlackout(room, blackoutDuration);
            }
            catch (Exception ex)
            {
                LibraryExiledAPI.LogError(nameof(HandleRoomBlackout),
                    $"CRITICAL FAILURE during blackout: {ex}\nRoom: {room.Name} ({room?.GetType()})");
            }
        }

        private void HandleTeslaBlackout(Room room, float blackoutDuration)
        {
            if (!_config.BlackoutConfig.DisableTeslas || room.Name != RoomName.HczTesla)
                return;

            try
            {
                if (Tesla.TryGet(room, out Tesla tesla))
                {
                    tesla.InactiveTime = blackoutDuration + 0.5f;
                    tesla.Trigger();
                    LibraryExiledAPI.LogDebug(nameof(HandleRoomBlackout),
                        $"Disabled Tesla in {room.Name} for {blackoutDuration + 0.5f}s");
                }
            }
            catch (Exception ex)
            {
                LibraryExiledAPI.LogError(nameof(HandleRoomBlackout),
                    $"Tesla blackout failed: {ex}\nRoom: {room.Name}");
            }
        }

        private void HandleWarheadBlackout()
        {
            if (!_config.BlackoutConfig.DisableNuke)
                return;

            try
            {
                if (Warhead.IsDetonationInProgress && !Warhead.IsLocked)
                {
                    Warhead.Stop();
                    LibraryExiledAPI.LogDebug(nameof(HandleRoomBlackout),
                        "Nuke detonation cancelled in HCZ Nuke room");
                }
            }
            catch (Exception ex)
            {
                LibraryExiledAPI.LogError(nameof(HandleRoomBlackout),
                    $"Warhead cancellation failed: {ex}");
            }
        }

        private void ExecuteRoomBlackout(Room room, float blackoutDuration)
        {
            try
            {
                _libraryLabAPI.TurnOffRoomLights(
                    room,
                    blackoutDuration,
                    _config.BlackoutConfig.ElevatorLockdownProbability
                );

                LibraryExiledAPI.LogDebug(nameof(HandleRoomBlackout),
                    $"Lights disabled in {room.Name} for {blackoutDuration}s");
            }
            catch (Exception ex)
            {
                LibraryExiledAPI.LogError(nameof(HandleRoomBlackout),
                    $"Light blackout failed: {ex}\nRoom: {room.Name}");
            }
        }

        private void DisableFacilitySystems(float blackoutDuration)
        {
            foreach (Room room in _libraryLabAPI.Rooms.Where(_libraryLabAPI.IsRoomAndNeighborsFreeOfEngagedGenerators))
            {
                _libraryLabAPI.TurnOffRoomLights(room, blackoutDuration, _config.BlackoutConfig.ElevatorLockdownProbability);
                LibraryExiledAPI.LogDebug("DisableFacilitySystems", $"Lights off in room {room.Name} for {blackoutDuration} seconds.");
            }

            ResetTeslaGates();

            if (_config.BlackoutConfig.DisableNuke && Warhead.IsDetonationInProgress && !Warhead.IsLocked)
            {
                Warhead.Stop();
                LibraryExiledAPI.LogDebug("DisableFacilitySystems", "Nuke detonation cancelled.");
            }
        }

        private IEnumerator<float> FinalizeBlackoutEvent(bool blackoutOccurred, float blackoutDuration)
        {
            if (!blackoutOccurred)
            {
                if (!IsBlackoutActive) TriggerCassieMessage(_config.CassieConfig.CassieMessageWrong);
                yield break;
            }

            IncrementBlackoutStack();
            LibraryExiledAPI.LogDebug("FinalizeBlackoutEvent", $"Blackout triggered. Stacks: {_blackoutStacks}, Duration: {blackoutDuration}");
            TriggerCassieMessage(_config.CassieConfig.CassieKeter);
            Plugin.Singleton.AudioManager.PlayAmbience();

            yield return Timing.WaitForSeconds(blackoutDuration);
            DecrementBlackoutStack();
            LibraryExiledAPI.LogDebug("FinalizeBlackoutEvent", $"Blackout finalized. Stacks: {_blackoutStacks}");

            if (!IsBlackoutActive)
            {
                TriggerCassieMessage(_config.CassieConfig.CassieMessageEnd);
                yield return Timing.WaitForSeconds(_config.CassieConfig.TimeBetweenSentenceAndEnd);
                ResetTeslaGates();
                _triggeredZones.Clear();
                Plugin.Singleton.AudioManager.StopAmbience();
                LibraryExiledAPI.LogDebug("FinalizeBlackoutEvent", "Blackout completed. Systems reset.");
            }
        }

        private void ResetTeslaGates()
        {
            foreach (Tesla tesla in _libraryLabAPI.Teslas)
            {
                tesla.Trigger();
                tesla.InactiveTime = 5f;
                LibraryExiledAPI.LogDebug("ResetTeslaGate", $"TeslaGate {tesla} reset. Cooldown: {tesla.InactiveTime}");
            }
        }

        #endregion

        #region CASSIE Management

        private void TriggerCassieMessage(string message, bool isGlitchy = false)
        {
            if (string.IsNullOrWhiteSpace(message) || _cassieState != CassieStatus.Idle)
            {
                LibraryExiledAPI.LogDebug("TriggerCassieMessage", $"Cassie busy ({_cassieState}), skipping: {message}");
                return;
            }

            _cassieState = CassieStatus.Playing;
            LibraryExiledAPI.LogDebug("TriggerCassieMessage", $"Triggering CASSIE: {message}");

            if (_config.CassieConfig.CassieMessageClearBeforeImportant)
                LibraryExiledAPI.ClearCassieQueue();

            if (isGlitchy)
                LibraryExiledAPI.SendGlitchyCassieMessage(message);
            else
                LibraryExiledAPI.SendCleanCassieMessage(message);

            StartCassieCooldown();
        }

        private IEnumerator<float> CassieCooldownRoutine()
        {
            yield return Timing.WaitForSeconds(_config.CassieConfig.TimeBetweenSentenceAndStart + 0.5f);
            _cassieState = CassieStatus.Cooldown;
            yield return Timing.WaitForSeconds(1f);
            _cassieState = CassieStatus.Idle;
        }

        #endregion

        #region Damage Logic

        /// <summary>
        /// Executes the primary SCP-575 attack sequence loop, applying sanity-based damage and effects.
        /// </summary>
        /// <returns>An enumerator for the coroutine execution.</returns>
        public IEnumerator<float> KeterActionLoop()
        {
            while (true)
            {
                if (!_plugin.IsEventActive)
                {
                    LibraryExiledAPI.LogDebug("KeterAction", "Event is inactive, waiting for reactivation.");
                    yield return Timing.WaitForSeconds(1f); // Poll for reactivation
                    continue;
                }

                yield return Timing.WaitForSeconds(_npcConfig.KeterActionDelay);
                if (!_plugin.IsEventActive) continue;

                var players = Player.ReadyList.ToList();
                foreach (Player player in players)
                {
                    try
                    {
                        if (player == null || player.UserId == null)
                        {
                            LibraryExiledAPI.LogWarn("Methods.KeterAction", "Player or UserId is null.");
                            continue;
                        }
                        string nickname = "Unknown";
                        bool isAlive = false;
                        bool isHuman = false;
                        try
                        {
                            nickname = player.Nickname ?? "null";
                            isAlive = player.IsAlive;
                            isHuman = player.IsHuman;
                        }
                        catch (Exception ex)
                        {
                            LibraryExiledAPI.LogWarn("Methods.KeterAction", $"Failed to access player properties for {player.UserId}: {ex.Message}");
                            continue;
                        }
                        if (!isAlive || !isHuman || nickname == "null")
                        {
                            LibraryExiledAPI.LogWarn("Methods.KeterAction", $"Invalid player state: {player.UserId} ({nickname}), IsAlive={isAlive}, IsHuman={isHuman}, Nickname={(nickname != null ? "non-null" : "null")}");
                            continue;
                        }
                        if (!IsBlackoutActive)
                        {
                            LibraryExiledAPI.LogDebug("Methods.KeterAction", $"Skipping player {player.UserId} ({nickname}): Blackout not active");
                            continue;
                        }
                        bool isInDarkRoom = _libraryLabAPI.IsPlayerInDarkRoom(player);
                        if (!isInDarkRoom)
                        {
                            LibraryExiledAPI.LogDebug("Methods.KeterAction", $"Skipping player {player.UserId} ({nickname}): Not in dark room");
                            continue;
                        }
                        if (!_sanityHandler.IsValidPlayer(player))
                        {
                            LibraryExiledAPI.LogWarn("Methods.KeterAction", $"Player {player.UserId} ({nickname}) became invalid before GetCurrentSanity");
                            continue;
                        }
                        float sanity;
                        try
                        {
                            sanity = _sanityHandler.GetCurrentSanity(player);
                            LibraryExiledAPI.LogDebug("Methods.KeterAction", $"Retrieved sanity {sanity} for {player.UserId} ({nickname})");
                        }
                        catch (Exception ex)
                        {
                            LibraryExiledAPI.LogWarn("Methods.KeterAction", $"Failed to get sanity for {player.UserId} ({nickname}): {ex.Message}");
                            continue;
                        }
                        try
                        {
                            LibraryExiledAPI.LogDebug("Methods.KeterAction", $"Processing player {player.UserId} ({nickname}), Sanity: {sanity}, IsAlive: {isAlive}, IsHuman: {isHuman}, PlayerSanityHandler instance ID: {_sanityHandler.GetHashCode()}");
                        }
                        catch (Exception ex)
                        {
                            LibraryExiledAPI.LogWarn("Methods.KeterAction", $"Failed to log processing for {player.UserId} ({nickname}): {ex.Message}");
                            continue;
                        }
                        if (!_sanityHandler.IsValidPlayer(player))
                        {
                            LibraryExiledAPI.LogWarn("Methods.KeterAction", $"Player {player.UserId} ({nickname}) became invalid before ApplyStageEffects");
                            continue;
                        }
                        LibraryExiledAPI.LogDebug("Methods.KeterAction", $"Calling ApplyStageEffects for {player.UserId} ({nickname})");
                        _sanityHandler.ApplyStageEffects(player);
                        PlayRandomAudioEffect(player);
                        _lightsourceHandler.ApplyLightsourceEffects(player);
                    }
                    catch (Exception ex)
                    {
                        LibraryExiledAPI.LogError("Methods.KeterAction", $"Failed to process player {player?.UserId ?? "null"} ({player?.Nickname ?? "null"}): {ex.Message}, StackTrace: {ex.StackTrace}");
                    }
                }
            }

        }


        /// <summary>
        /// Plays a random audio effect for a player during an SCP-575 attack.
        /// </summary>
        /// <param name="player">The player to play the audio for.</param>
        private void PlayRandomAudioEffect(Player player)
        {
            try
            {
                LibraryExiledAPI.LogDebug("Methods.PlayRandomAudioEffect", $"Playing audio for {player.UserId} ({player.Nickname ?? "null"})");
                var audioOptions = new[] { AudioKey.WhispersMixed, AudioKey.Scream, AudioKey.ScreamAngry, AudioKey.Whispers };
                var selectedClip = audioOptions[UnityEngine.Random.Range(0, audioOptions.Length)];
                _plugin.AudioManager.PlayAudioAutoManaged(player, selectedClip, hearableForAllPlayers: true, lifespan: 16f);
            }
            catch (Exception ex)
            {
                LibraryExiledAPI.LogError("Methods.PlayRandomAudioEffect", $"Failed to play audio for {player?.UserId ?? "null"} ({player?.Nickname ?? "null"}): {ex.Message}, StackTrace: {ex.StackTrace}");
            }
        }

        #endregion


        #region Utility Methods

        /// <summary>  
        /// Starts the main Blackout event loop coroutine, or restarts if it is already running.
        /// </summary>  
        public void StartBlackoutEventLoop()
        {
            Timing.KillCoroutines("SCP575-BlackoutLoop");
            _plugin.Npc.EventHandler.Coroutines.Add(Timing.RunCoroutine(RunBlackoutLoop(), "SCP575-BlackoutLoop"));
        }

        /// <summary>  
        /// Starts the Sanity handler coroutine, or restarts if it is already running.
        /// </summary>  
        public void StartSanityHandlerLoop()
        {
            if (_plugin.SanityEventHandler.SanityDecayCoroutine.IsRunning)
                Timing.KillCoroutines(_plugin.SanityEventHandler.SanityDecayCoroutine);
            _plugin.SanityEventHandler.SanityDecayCoroutine = Timing.RunCoroutine(_plugin.SanityEventHandler.HandleSanityDecay(), "SCP575-SanityHandler");
            _plugin.Npc.EventHandler.Coroutines.Add(_plugin.SanityEventHandler.SanityDecayCoroutine);
        }

        /// <summary>  
        /// Starts the SCP-575 Action loop coroutine, or restarts if it is already running.
        /// </summary>
        public void StartKeterActionLoop()
        {
            Timing.KillCoroutines("SCP575-ActionLoop");
            _plugin.Npc.EventHandler.Coroutines.Add(Timing.RunCoroutine(KeterActionLoop(), "SCP575-ActionLoop"));
        }

        /// <summary>  
        /// Starts the Cassie Cooldown coroutine, or restarts if it is already running.
        /// </summary>
        public void StartCassieCooldown()
        {
            Timing.KillCoroutines("SCP575-CassieCd");
            _plugin.Npc.EventHandler.Coroutines.Add(Timing.RunCoroutine(CassieCooldownRoutine(), "SCP575-CassieCd"));
        }

        /// <summary>
        /// Increments the blackout stack count in a thread-safe manner.
        /// </summary>
        public void IncrementBlackoutStack()
        {
            lock (BlackoutLock) _blackoutStacks++;
        }

        /// <summary>
        /// Decrements the blackout stack count in a thread-safe manner and resets if no stacks remain.
        /// </summary>
        public void DecrementBlackoutStack()
        {
            lock (BlackoutLock) _blackoutStacks = Math.Max(0, _blackoutStacks - 1);
            if (!IsBlackoutActive) Reset575();
        }

        /// <summary>
        /// Checks if all generators are engaged.
        /// </summary>
        /// <param name="reqCountEngagedGens">Required number of engaged generators. Defaults to 3.</param>
        /// <returns>True if all generators are engaged; otherwise, false.</returns>
        public bool AreAllGeneratorsEngaged(int reqCountEngagedGens = 3)
        {
            var generators = Generator.List;
            return generators.Count >= reqCountEngagedGens && generators.All(gen => gen.Engaged);
        }

        /// <summary>
        /// Resets SCP-575 state, turning on all lights and resetting systems.
        /// </summary>
        public void Reset575()
        {
            LibraryExiledAPI.LogDebug("Reset575", "Resetting SCP-575 state.");
            _blackoutStacks = 0;
            foreach (var room in Room.List)
            {
                foreach (var lightController in room.AllLightControllers)
                {
                    lightController.LightsEnabled = true;
                    lightController.FlickerLights(_config.BlackoutConfig.FlickerDuration);
                }
            }
            LabApi.Features.Wrappers.Map.ResetColorOfLights();
            LabApi.Features.Wrappers.Map.TurnOnLights();
            _triggeredZones.Clear();
            ResetTeslaGates();
        }

        /// <summary>
        /// Determines if an explosion is dangerous to SCP-575.
        /// </summary>
        /// <param name="explosionType">The explosion to check.</param>
        /// <returns>True if dangerous; otherwise, false.</returns>
        public bool IsDangerousToScp575(ExplosionType explosionType)
        {
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
            LibraryExiledAPI.LogDebug("Kill575", "Killing SCP-575 NPC.");
            Disable();
        }

        #endregion
    }
}
