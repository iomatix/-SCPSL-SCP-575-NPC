namespace SCP_575.Npc
{
    using Handlers;
    using MapGeneration;
    using MEC;
    using SCP_575.ConfigObjects;
    using SCP_575.Shared.Audio.Enums;
    using Shared;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
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
        private readonly HashSet<FacilityZone> _triggeredZones = new();
        private static readonly object BlackoutLock = new();
        private static int _blackoutStacks = 0;
        private CassieStatus _cassieState = CassieStatus.Idle;
        private CoroutineHandle _cassieCooldownCoroutine;
        private CoroutineHandle _keterActionCoroutine;

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
            _lightsourceHandler = _plugin.LightsourceHandler ?? throw new InvalidOperationException("LightsourceHandler is null.");
            _sanityHandler = _plugin.SanityEventHandler ?? throw new InvalidOperationException("SanityEventHandler is null.");
            Library_ExiledAPI.LogDebug("Methods.Constructor", $"Initialized Methods with PlayerSanityHandler instance ID={_sanityHandler.GetHashCode()}");
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

            // Kill CASSIE coroutine  
            if (_cassieCooldownCoroutine.IsRunning)
                Timing.KillCoroutines(_cassieCooldownCoroutine);

            if (_keterActionCoroutine.IsRunning)
                Timing.KillCoroutines(_keterActionCoroutine);

            _keterActionCoroutine = default;
            Plugin.Singleton.AudioManager.StopAmbience();
            _blackoutStacks = 0;
            _triggeredZones.Clear();
            Timing.KillCoroutines("SCP575keter");
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
        /// Runs the blackout timer, triggering blackout events at intervals.
        /// </summary>
        /// <returns>An enumerator for the coroutine.</returns>
        public IEnumerator<float> RunBlackoutTimer()
        {
            yield return Timing.WaitForSeconds(_config.BlackoutConfig.InitialDelay);
            Library_ExiledAPI.LogDebug("RunBlackoutTimer", "SCP-575 NPC blackout timer started.");

            while (true)
            {
                if (!_plugin.IsEventActive)
                {
                    Library_ExiledAPI.LogDebug("RunBlackoutTimer", "Event is inactive, waiting for reactivation.");
                    yield return Timing.WaitForSeconds(1f); // Poll for reactivation
                    continue;
                }

                float delay = _config.BlackoutConfig.RandomEvents
                    ? UnityEngine.Random.Range(_config.BlackoutConfig.DelayMin, _config.BlackoutConfig.DelayMax)
                    : _config.BlackoutConfig.InitialDelay;
                yield return Timing.WaitForSeconds(delay);

                _plugin.Npc.EventHandler.Coroutines.Add(Timing.RunCoroutine(ExecuteBlackoutEvent(), "575BlackoutExec"));
            }
        }

        private IEnumerator<float> ExecuteBlackoutEvent()
        {
            if (IsBlackoutActive) yield break;

            if (_config.CassieConfig.CassieMessageClearBeforeImportant)
                Library_ExiledAPI.Cassie_Clear();

            Library_ExiledAPI.LogDebug("ExecuteBlackoutEvent", "Starting blackout event...");
            TriggerCassieMessage(_config.CassieConfig.CassieMessageStart, true);

            if (_config.BlackoutConfig.FlickerLights)
                FlickerAffectedZones(_config.BlackoutConfig.FlickerDuration);

            yield return Timing.WaitForSeconds(_config.CassieConfig.TimeBetweenSentenceAndStart);
            TriggerCassieMessage(_config.CassieConfig.CassiePostMessage);

            float blackoutDuration = _config.BlackoutConfig.RandomEvents
                ? GetRandomBlackoutDuration()
                : _config.BlackoutConfig.DurationMax;

            bool blackoutOccurred = _config.BlackoutConfig.UsePerRoomChances
                ? HandleRoomSpecificBlackout(blackoutDuration)
                : HandleZoneSpecificBlackout(blackoutDuration);

            _plugin.Npc.EventHandler.Coroutines.Add(Timing.RunCoroutine(FinalizeBlackoutEvent(blackoutOccurred, blackoutDuration), "575BlackoutFinalize"));
        }


        private async Task FlickerAffectedZones(float blackoutDuration, CancellationToken cancellationToken = default)
        {
            if (!_plugin.IsEventActive) return;

            if (!_config.BlackoutConfig.FlickerLights) return;

            var zonesToFlicker = GetZonesToFlicker();
            var flickerTasks = zonesToFlicker.Select((zone, index) =>
                FlickerZoneLightsAsync(zone, cancellationToken)).ToList();

            try
            {
                await Task.WhenAll(flickerTasks);
            }
            catch (OperationCanceledException)
            {
                Library_ExiledAPI.LogDebug("FlickerAffectedZones", "Flickering cancelled.");
            }
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

        private async Task FlickerZoneLightsAsync(FacilityZone targetZone, CancellationToken cancellationToken = default)
        {
            var blackoutColor = new UnityEngine.Color(_config.BlackoutConfig.LightsColorR, _config.BlackoutConfig.LightsColorG, _config.BlackoutConfig.LightsColorB, 1f);
            float flickerInterval = 1f / _config.BlackoutConfig.FlickerFrequency;
            int totalFlickers = Mathf.RoundToInt(_config.BlackoutConfig.FlickerDuration / flickerInterval);

            try
            {
                LabApi.Features.Wrappers.Map.SetColorOfLights(blackoutColor, targetZone);

                for (int i = 0; i < totalFlickers; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    LabApi.Features.Wrappers.Map.TurnOffLights(flickerInterval * 0.5f, targetZone);
                    await Task.Delay(Mathf.RoundToInt(flickerInterval * 500), cancellationToken);

                    cancellationToken.ThrowIfCancellationRequested();

                    LabApi.Features.Wrappers.Map.TurnOnLights(targetZone);
                    LabApi.Features.Wrappers.Map.SetColorOfLights(blackoutColor, targetZone);
                    await Task.Delay(Mathf.RoundToInt(flickerInterval * 500), cancellationToken);
                }

                LabApi.Features.Wrappers.Map.TurnOffLights(targetZone);
            }
            catch (OperationCanceledException)
            {
                LabApi.Features.Wrappers.Map.TurnOffLights(targetZone);
                Library_ExiledAPI.LogDebug("FlickerZoneLightsAsync", $"Flickering cancelled for zone {targetZone}");
                throw;
            }
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
                Library_ExiledAPI.LogDebug("HandleZoneSpecificBlackout", "Facility-wide blackout triggered.");
                return true;
            }
            return isBlackoutTriggered;
        }

        private bool AttemptZoneBlackout(FacilityZone zone, float chance, string cassieMessage, float blackoutDuration, bool disableSystems = false)
        {
            if (!_plugin.IsEventActive) return false;

            if (UnityEngine.Random.Range(0f, 100f) >= chance) return false;

            if (_config.BlackoutConfig.FlickerLights) FlickerZoneLightsAsync(zone).GetAwaiter().GetResult();

            LabApi.Features.Wrappers.Map.TurnOffLights(blackoutDuration, zone);
            Library_ExiledAPI.LogDebug("AttemptZoneBlackout", $"Blackout triggered in zone {zone} for {blackoutDuration} seconds.");

            if (!IsBlackoutActive) TriggerCassieMessage(cassieMessage, true);
            if (disableSystems) DisableFacilitySystems(blackoutDuration);
            return true;
        }

        private void TriggerFacilityWideBlackout(float blackoutDuration)
        {
            if (!_plugin.IsEventActive) return;

            foreach (FacilityZone zone in Enum.GetValues(typeof(FacilityZone)))
            {
                LabApi.Features.Wrappers.Map.TurnOffLights(blackoutDuration, zone);
                Library_ExiledAPI.LogDebug("TriggerFacilityWideBlackout", $"Lights off in zone {zone} for {blackoutDuration} seconds.");
            }
            DisableFacilitySystems(blackoutDuration);
            if (!IsBlackoutActive) TriggerCassieMessage(_config.CassieConfig.CassieMessageFacility, true);
        }

        private bool HandleRoomSpecificBlackout(float blackoutDuration)
        {
            if (!_plugin.IsEventActive) return false;

            bool blackoutTriggered = false;
            foreach (LabApi.Features.Wrappers.Room room in Library_LabAPI.Rooms)
            {
                // Skip rooms without light controllers
                if (!room.AllLightControllers.Any()) continue;


                if (AttemptRoomBlackout(room, blackoutDuration))
                {
                    blackoutTriggered = true;
                    Library_ExiledAPI.LogDebug("HandleRoomSpecificBlackout", $"Blackout triggered in room {room.Name}.");
                }
            }

            if (!blackoutTriggered && _config.BlackoutConfig.EnableFacilityBlackout)
            {
                TriggerFacilityWideBlackout(blackoutDuration);
                Library_ExiledAPI.LogDebug("HandleRoomSpecificBlackout", "Facility-wide blackout triggered.");
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
        public bool AttemptRoomBlackout(LabApi.Features.Wrappers.Room room, float blackoutDuration, bool isForced = false, bool isCassieSilent = false)
        {
            if (!_plugin.IsEventActive) return false;

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

        private void HandleRoomBlackout(LabApi.Features.Wrappers.Room room, float blackoutDuration)
        {
            if (!Library_LabAPI.IsRoomAndNeighborsFreeOfEngagedGenerators(room)) return;

            if (_config.BlackoutConfig.DisableTeslas && room.Name == RoomName.HczTesla)
            {
                if (LabApi.Features.Wrappers.Tesla.TryGet(room, out LabApi.Features.Wrappers.Tesla tesla))
                {
                    tesla.InactiveTime = blackoutDuration + 0.5f;
                    tesla.Trigger();
                }
            }

            if (_config.BlackoutConfig.DisableNuke && room.Name == RoomName.HczWarhead && LabApi.Features.Wrappers.Warhead.IsDetonationInProgress && !LabApi.Features.Wrappers.Warhead.IsLocked)
            {
                LabApi.Features.Wrappers.Warhead.Stop();
                Library_ExiledAPI.LogDebug("HandleRoomBlackout", "Nuke detonation cancelled in HCZ Nuke room.");
            }

            Library_LabAPI.TurnOffRoomLights(room, blackoutDuration);
            Library_ExiledAPI.LogDebug("HandleRoomBlackout", $"Lights off in room {room.Name} for {blackoutDuration} seconds.");
        }

        private void DisableFacilitySystems(float blackoutDuration)
        {
            foreach (LabApi.Features.Wrappers.Room room in Library_LabAPI.Rooms.Where(Library_LabAPI.IsRoomAndNeighborsFreeOfEngagedGenerators))
            {
                Library_LabAPI.TurnOffRoomLights(room, blackoutDuration);
                Library_ExiledAPI.LogDebug("DisableFacilitySystems", $"Lights off in room {room.Name} for {blackoutDuration} seconds.");
            }

            ResetTeslaGates();

            if (_config.BlackoutConfig.DisableNuke && LabApi.Features.Wrappers.Warhead.IsDetonationInProgress && !LabApi.Features.Wrappers.Warhead.IsLocked)
            {
                LabApi.Features.Wrappers.Warhead.Stop();
                Library_ExiledAPI.LogDebug("DisableFacilitySystems", "Nuke detonation cancelled.");
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
            Library_ExiledAPI.LogDebug("FinalizeBlackoutEvent", $"Blackout triggered. Stacks: {_blackoutStacks}, Duration: {blackoutDuration}");
            TriggerCassieMessage(_config.CassieConfig.CassieKeter);
            Plugin.Singleton.AudioManager.PlayAmbience();

            yield return Timing.WaitForSeconds(blackoutDuration);
            DecrementBlackoutStack();
            Library_ExiledAPI.LogDebug("FinalizeBlackoutEvent", $"Blackout finalized. Stacks: {_blackoutStacks}");

            if (!IsBlackoutActive)
            {
                TriggerCassieMessage(_config.CassieConfig.CassieMessageEnd);
                yield return Timing.WaitForSeconds(_config.CassieConfig.TimeBetweenSentenceAndEnd);
                ResetTeslaGates();
                _triggeredZones.Clear();
                Plugin.Singleton.AudioManager.StopAmbience();
                Library_ExiledAPI.LogDebug("FinalizeBlackoutEvent", "Blackout completed. Systems reset.");
            }
        }

        private void ResetTeslaGates()
        {
            foreach (LabApi.Features.Wrappers.Tesla tesla in Library_LabAPI.Teslas)
            {
                tesla.Trigger();
                tesla.InactiveTime = 5f;
                Library_ExiledAPI.LogDebug("ResetTeslaGate", $"TeslaGate {tesla} reset. Cooldown: {tesla.InactiveTime}");
            }
        }

        #endregion

        #region CASSIE Management

        private void TriggerCassieMessage(string message, bool isGlitchy = false)
        {
            if (string.IsNullOrWhiteSpace(message) || _cassieState != CassieStatus.Idle)
            {
                Library_ExiledAPI.LogDebug("TriggerCassieMessage", $"Cassie busy ({_cassieState}), skipping: {message}");
                return;
            }

            _cassieState = CassieStatus.Playing;
            Library_ExiledAPI.LogDebug("TriggerCassieMessage", $"Triggering CASSIE: {message}");

            if (_config.CassieConfig.CassieMessageClearBeforeImportant)
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
            yield return Timing.WaitForSeconds(_config.CassieConfig.TimeBetweenSentenceAndStart + 0.5f);
            _cassieState = CassieStatus.Cooldown;
            yield return Timing.WaitForSeconds(1f);
            _cassieState = CassieStatus.Idle;
        }

        #endregion

        #region Damage Logic

        /// <summary>
        /// Executes the primary SCP-575 attack sequence, applying sanity-based damage and effects.
        /// </summary>
        /// <returns>An enumerator for the coroutine execution.</returns>
        public IEnumerator<float> KeterAction()
        {
            while (true)
            {
                if (!_plugin.IsEventActive)
                {
                    Library_ExiledAPI.LogDebug("KeterAction", "Event is inactive, waiting for reactivation.");
                    yield return Timing.WaitForSeconds(1f); // Poll for reactivation
                    continue;
                }

                yield return Timing.WaitForSeconds(_npcConfig.KeterActionDelay);
                if (!_plugin.IsEventActive) continue;

                var players = LabApi.Features.Wrappers.Player.ReadyList.ToList();
                foreach (LabApi.Features.Wrappers.Player player in players)
                {
                    try
                    {
                        if (player == null || player.UserId == null)
                        {
                            Library_ExiledAPI.LogWarn("Methods.KeterAction", "Player or UserId is null.");
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
                            Library_ExiledAPI.LogWarn("Methods.KeterAction", $"Failed to access player properties for {player.UserId}: {ex.Message}");
                            continue;
                        }
                        if (!isAlive || !isHuman || nickname == "null")
                        {
                            Library_ExiledAPI.LogWarn("Methods.KeterAction", $"Invalid player state: {player.UserId} ({nickname}), IsAlive={isAlive}, IsHuman={isHuman}, Nickname={(nickname != null ? "non-null" : "null")}");
                            continue;
                        }
                        if (!IsBlackoutActive)
                        {
                            Library_ExiledAPI.LogDebug("Methods.KeterAction", $"Skipping player {player.UserId} ({nickname}): Blackout not active");
                            continue;
                        }
                        bool isInDarkRoom = Library_LabAPI.IsPlayerInDarkRoom(player);
                        if (!isInDarkRoom)
                        {
                            Library_ExiledAPI.LogDebug("Methods.KeterAction", $"Skipping player {player.UserId} ({nickname}): Not in dark room");
                            continue;
                        }
                        if (!_sanityHandler.IsValidPlayer(player))
                        {
                            Library_ExiledAPI.LogWarn("Methods.KeterAction", $"Player {player.UserId} ({nickname}) became invalid before GetCurrentSanity");
                            continue;
                        }
                        float sanity;
                        try
                        {
                            sanity = _sanityHandler.GetCurrentSanity(player);
                            Library_ExiledAPI.LogDebug("Methods.KeterAction", $"Retrieved sanity {sanity} for {player.UserId} ({nickname})");
                        }
                        catch (Exception ex)
                        {
                            Library_ExiledAPI.LogWarn("Methods.KeterAction", $"Failed to get sanity for {player.UserId} ({nickname}): {ex.Message}");
                            continue;
                        }
                        try
                        {
                            Library_ExiledAPI.LogDebug("Methods.KeterAction", $"Processing player {player.UserId} ({nickname}), Sanity: {sanity}, IsAlive: {isAlive}, IsHuman: {isHuman}, PlayerSanityHandler instance ID: {_sanityHandler.GetHashCode()}");
                        }
                        catch (Exception ex)
                        {
                            Library_ExiledAPI.LogWarn("Methods.KeterAction", $"Failed to log processing for {player.UserId} ({nickname}): {ex.Message}");
                            continue;
                        }
                        if (!_sanityHandler.IsValidPlayer(player))
                        {
                            Library_ExiledAPI.LogWarn("Methods.KeterAction", $"Player {player.UserId} ({nickname}) became invalid before ApplyStageEffects");
                            continue;
                        }
                        Library_ExiledAPI.LogDebug("Methods.KeterAction", $"Calling ApplyStageEffects for {player.UserId} ({nickname})");
                        _sanityHandler.ApplyStageEffects(player);
                        PlayRandomAudioEffect(player);
                        _lightsourceHandler.ApplyLightsourceEffects(player);
                    }
                    catch (Exception ex)
                    {
                        Library_ExiledAPI.LogError("Methods.KeterAction", $"Failed to process player {player?.UserId ?? "null"} ({player?.Nickname ?? "null"}): {ex.Message}, StackTrace: {ex.StackTrace}");
                    }
                }
            }

        }


        /// <summary>
        /// Plays a random audio effect for a player during an SCP-575 attack.
        /// </summary>
        /// <param name="player">The player to play the audio for.</param>
        private void PlayRandomAudioEffect(LabApi.Features.Wrappers.Player player)
        {
            try
            {
                Library_ExiledAPI.LogDebug("Methods.PlayRandomAudioEffect", $"Playing audio for {player.UserId} ({player.Nickname ?? "null"})");
                var audioOptions = new[] { AudioKey.WhispersMixed, AudioKey.Scream, AudioKey.ScreamAngry, AudioKey.Whispers };
                var selectedClip = audioOptions[UnityEngine.Random.Range(0, audioOptions.Length)];
                _plugin.AudioManager.PlayAudioAutoManaged(player, selectedClip, hearableForAllPlayers: true, lifespan: 16f);
            }
            catch (Exception ex)
            {
                Library_ExiledAPI.LogError("Methods.PlayRandomAudioEffect", $"Failed to play audio for {player?.UserId ?? "null"} ({player?.Nickname ?? "null"}): {ex.Message}, StackTrace: {ex.StackTrace}");
            }
        }

        #endregion


        #region Utility Methods

        /// <summary>  
        /// Starts the damage coroutine if not already running  
        /// </summary>  
        public void StartKeterAction()
        {
            if (!_keterActionCoroutine.IsRunning)
            {
                _keterActionCoroutine = Timing.RunCoroutine(KeterAction(), "SCP575keter");
            }
        }

        /// <summary>  
        /// Stops the damage coroutine  
        /// </summary>  
        public void StopKeterAction()
        {
            if (_keterActionCoroutine.IsRunning)
            {
                Timing.KillCoroutines(_keterActionCoroutine);
            }
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
            foreach (var room in LabApi.Features.Wrappers.Room.List)
            {
                foreach (var lightController in room.AllLightControllers)
                {
                    lightController.LightsEnabled = true;
                    lightController.FlickerLights(_config.BlackoutConfig.FlickerDuration);
                }
            }
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
            Library_ExiledAPI.LogDebug("Kill575", "Killing SCP-575 NPC.");
            Clean();
        }


        #endregion
    }
}