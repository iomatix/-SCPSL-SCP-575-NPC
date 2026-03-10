namespace SCP_575.Npc
{
    using Handlers;
    using LabApi.Features.Wrappers;
    using MapGeneration;
    using MEC;
    using SCP_575.ConfigObjects;
    using SCP_575.Shared.Audio.Enums;
    using SCP575.Shared;
    using Shared;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEngine;

    /// <summary>
    /// Manages core SCP-575 NPC behaviors, including blackout orchestration, 
    /// CASSIE announcements, and the Keter-class attack logic.
    /// </summary>
    public class Methods
    {
        private readonly Plugin _plugin;
        private readonly Config _config;
        private readonly NpcConfig _npcConfig;
        private readonly PlayerLightsourceHandler _lightsourceHandler;
        private readonly PlayerSanityHandler _sanityHandler;
        private readonly LibraryLabAPI _libraryLabAPI;

        private bool _isInitialized = false;
        private readonly HashSet<FacilityZone> _triggeredZones = new();
        private static readonly object BlackoutLock = new();
        private static int _blackoutStacks = 0;
        private CassieStatus _cassieState = CassieStatus.Idle;

        // Note: Removed _activeCoroutines list to prevent memory leaks from short-lived coroutines.
        // We now rely strictly on MEC's native tagging system for memory safety and cleanup.
        private const string TempCoroutineTag = CoroutineTags.Temp;

        private enum CassieStatus
        {
            Idle,
            Playing,
            Cooldown
        }

        public Methods(Plugin plugin)
        {
            _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
            _config = _plugin.Config;
            _npcConfig = _config.NpcConfig;

            _lightsourceHandler = _plugin.LightsourceHandler ?? throw new InvalidOperationException("LightsourceHandler missing.");
            _sanityHandler = _plugin.SanityEventHandler ?? throw new InvalidOperationException("SanityEventHandler missing.");
            _libraryLabAPI = _plugin.LibraryLabAPI ?? throw new InvalidOperationException("LibraryLabAPI missing.");

            LibraryLabAPI.LogDebug(nameof(Methods), "NPC Methods system linked with Handlers and LibraryAPI.");
        }

        public bool IsBlackoutActive => _blackoutStacks > 0;
        public int GetCurrentBlackoutStacks => _blackoutStacks;

        #region Lifecycle Management

        /// <summary>
        /// Prepares the SCP-575 NPC for the current round, rolling for spawn chance
        /// and initializing all relevant loops and sanity values for players.
        /// </summary>
        public void Init(float roll = -1f)
        {
            if (_isInitialized) return;

            if (roll <= _config.BlackoutConfig.EventChance)
            {
                _isInitialized = true;
                _plugin.IsEventActive = true;

                LibraryLabAPI.LogInfo(nameof(Init), $"SCP-575 spawn roll successful. Initializing loops.");

                StartBlackoutEventLoop();
                StartKeterActionLoop();
                StartSanityHandlerLoop();

                foreach (var player in Player.ReadyList)
                {
                    if (_sanityHandler.IsValidPlayer(player))
                        _sanityHandler.SetSanity(player, _config.SanityConfig.InitialSanity);
                }
            }
            else
            {
                LibraryLabAPI.LogDebug(nameof(Init), $"SCP-575 will not spawn this round.");
            }
        }

        /// <summary>
        /// Disables all SCP-575 logic, stops coroutines, and cleans up active event states.
        /// </summary>
        public void Disable()
        {
            _isInitialized = false;
            _plugin.IsEventActive = false;

            // Clean up all static coroutines via predefined array
            foreach (string tag in CoroutineTags.AllStaticTags)
            {
                Timing.KillCoroutines(tag);
            }

            _plugin.AudioManager?.Clean();
            _sanityHandler?.Clean();
            _plugin.LightsourceHandler?.Clean();

            LibraryLabAPI.LogInfo(nameof(Disable), "SCP-575 NPC logic and all coroutines terminated.");
        }

        public void Clean()
        {
            Timing.KillCoroutines(CoroutineTags.CassieCooldown);
            Reset575();
        }

        #endregion

        #region Blackout Management

        public IEnumerator<float> RunBlackoutLoop()
        {
            yield return Timing.WaitForSeconds(_config.BlackoutConfig.InitialDelay);

            while (_isInitialized)
            {
                if (!_plugin.IsEventActive)
                {
                    yield return Timing.WaitForSeconds(1f);
                    continue;
                }

                float delay = _config.BlackoutConfig.RandomEvents
                    ? UnityEngine.Random.Range(_config.BlackoutConfig.DelayMin, _config.BlackoutConfig.DelayMax)
                    : _config.BlackoutConfig.InitialDelay;

                yield return Timing.WaitForSeconds(delay);
                Timing.RunCoroutine(ExecuteBlackoutEvent(), TempCoroutineTag);
            }
        }

        private IEnumerator<float> ExecuteBlackoutEvent()
        {
            if (!IsBlackoutActive)
            {
                if (_config.CassieConfig.CassieMessageClearBeforeImportant)
                    LibraryExiledAPI.ClearCassieQueue();

                TriggerCassieMessage(_config.CassieConfig.CassieMessageStart, true);
                if (_config.BlackoutConfig.FlickerLights)
                    FlickerAffectedZones(_config.BlackoutConfig.FlickerDuration);

                yield return Timing.WaitForSeconds(_config.CassieConfig.TimeBetweenSentenceAndStart);
                TriggerCassieMessage(_config.CassieConfig.CassiePostMessage);
            }

            float duration = _config.BlackoutConfig.RandomEvents
                ? UnityEngine.Random.Range(_config.BlackoutConfig.DurationMin, _config.BlackoutConfig.DurationMax)
                : _config.BlackoutConfig.DurationMax;

            bool occurred = _config.BlackoutConfig.UsePerRoomChances
                ? HandleRoomSpecificBlackout(duration)
                : HandleZoneSpecificBlackout(duration);

            Timing.RunCoroutine(FinalizeBlackoutEvent(occurred, duration), TempCoroutineTag);
        }

        private void FlickerAffectedZones(float duration)
        {
            if (!_plugin.IsEventActive || !_config.BlackoutConfig.FlickerLights) return;

            var zones = GetZonesToFlicker();
            foreach (var zone in zones)
                Timing.RunCoroutine(FlickerZoneLightsCoroutine(zone), TempCoroutineTag);
        }

        private List<FacilityZone> GetZonesToFlicker()
        {
            if (_config.BlackoutConfig.UsePerRoomChances || _config.BlackoutConfig.EnableFacilityBlackout)
                return Enum.GetValues(typeof(FacilityZone)).Cast<FacilityZone>().ToList();

            var zones = new List<FacilityZone>();
            if (_config.BlackoutConfig.ChanceLight > 0) zones.Add(FacilityZone.LightContainment);
            if (_config.BlackoutConfig.ChanceHeavy > 0) zones.Add(FacilityZone.HeavyContainment);
            if (_config.BlackoutConfig.ChanceEntrance > 0) zones.Add(FacilityZone.Entrance);
            if (_config.BlackoutConfig.ChanceSurface > 0) zones.Add(FacilityZone.Surface);
            return zones;
        }

        public IEnumerator<float> FlickerZoneLightsCoroutine(FacilityZone targetZone)
        {
            var color = new Color(_config.BlackoutConfig.LightsColorR, _config.BlackoutConfig.LightsColorG, _config.BlackoutConfig.LightsColorB);
            float interval = 1f / _config.BlackoutConfig.FlickerFrequency;
            int flickers = Mathf.RoundToInt(_config.BlackoutConfig.FlickerDuration / interval);

            Map.SetColorOfLights(color, targetZone);
            for (int i = 0; i < flickers; i++)
            {
                Map.TurnOffLights(interval * 0.5f, targetZone);
                yield return Timing.WaitForSeconds(interval * 0.5f);
                Map.TurnOnLights(targetZone);
                yield return Timing.WaitForSeconds(interval * 0.5f);
            }
            Map.ResetColorOfLights(targetZone);
        }

        private bool HandleZoneSpecificBlackout(float duration)
        {
            bool triggered = false;
            triggered |= AttemptZoneBlackout(FacilityZone.LightContainment, _config.BlackoutConfig.ChanceLight, _config.CassieConfig.CassieMessageLight, duration);
            triggered |= AttemptZoneBlackout(FacilityZone.HeavyContainment, _config.BlackoutConfig.ChanceHeavy, _config.CassieConfig.CassieMessageHeavy, duration);
            triggered |= AttemptZoneBlackout(FacilityZone.Entrance, _config.BlackoutConfig.ChanceEntrance, _config.CassieConfig.CassieMessageEntrance, duration);
            triggered |= AttemptZoneBlackout(FacilityZone.Surface, _config.BlackoutConfig.ChanceSurface, _config.CassieConfig.CassieMessageSurface, duration);

            if (!triggered && _config.BlackoutConfig.EnableFacilityBlackout)
            {
                TriggerFacilityWideBlackout(duration);
                return true;
            }
            return triggered;
        }

        private bool AttemptZoneBlackout(FacilityZone zone, float chance, string cassie, float duration)
        {
            if (!_plugin.IsEventActive || UnityEngine.Random.Range(0f, 100f) >= chance) return false;

            Map.TurnOffLights(duration, zone);
            if (!IsBlackoutActive) TriggerCassieMessage(cassie, true);
            return true;
        }

        private void TriggerFacilityWideBlackout(float duration)
        {
            Map.TurnOffLights(duration);
            DisableFacilitySystems(duration);
            if (!IsBlackoutActive) TriggerCassieMessage(_config.CassieConfig.CassieMessageFacility, true);
        }

        private bool HandleRoomSpecificBlackout(float duration)
        {
            bool triggered = false;
            foreach (Room room in _libraryLabAPI.Rooms.Where(r => r.AllLightControllers.Any()))
            {
                if (AttemptRoomBlackout(room, duration)) triggered = true;
            }

            if (!triggered && _config.BlackoutConfig.EnableFacilityBlackout)
            {
                TriggerFacilityWideBlackout(duration);
                return true;
            }
            return triggered;
        }

        public bool AttemptRoomBlackout(Room room, float duration, bool forced = false, bool silent = false)
        {
            if (!_plugin.IsEventActive || room == null) return false;

            var (chance, cassie) = GetRoomBlackoutParams(room.Zone);
            if (!forced && UnityEngine.Random.Range(0f, 100f) >= chance) return false;

            HandleRoomBlackout(room, duration);
            if (!_triggeredZones.Contains(room.Zone) && !silent)
            {
                if (!IsBlackoutActive) TriggerCassieMessage(cassie);
                _triggeredZones.Add(room.Zone);
            }
            return true;
        }

        private (float, string) GetRoomBlackoutParams(FacilityZone zone)
        {
            return zone switch
            {
                FacilityZone.HeavyContainment => (_config.BlackoutConfig.ChanceHeavy, _config.CassieConfig.CassieMessageHeavy),
                FacilityZone.LightContainment => (_config.BlackoutConfig.ChanceLight, _config.CassieConfig.CassieMessageLight),
                FacilityZone.Entrance => (_config.BlackoutConfig.ChanceEntrance, _config.CassieConfig.CassieMessageEntrance),
                _ => (_config.BlackoutConfig.ChanceOther, _config.CassieConfig.CassieMessageOther)
            };
        }

        private void HandleRoomBlackout(Room room, float duration)
        {
            if (!_libraryLabAPI.IsRoomAndNeighborsFreeOfEngagedGenerators(room)) return;

            HandleTeslaBlackout(room, duration);
            HandleWarheadBlackout();

            _libraryLabAPI.TurnOffRoomLights(room, duration, _config.BlackoutConfig.ElevatorLockdownProbability);
        }

        private void HandleTeslaBlackout(Room room, float duration)
        {
            if (_config.BlackoutConfig.DisableTeslas && room.Name == RoomName.HczTesla && Tesla.TryGet(room, out Tesla tesla))
            {
                tesla.InactiveTime = duration + 0.5f;
                tesla.Trigger();
            }
        }

        private void HandleWarheadBlackout()
        {
            if (_config.BlackoutConfig.DisableNuke && Warhead.IsDetonationInProgress && !Warhead.IsLocked)
                Warhead.Stop();
        }

        private void DisableFacilitySystems(float duration)
        {
            foreach (Room room in _libraryLabAPI.Rooms.Where(_libraryLabAPI.IsRoomAndNeighborsFreeOfEngagedGenerators))
                _libraryLabAPI.TurnOffRoomLights(room, duration, _config.BlackoutConfig.ElevatorLockdownProbability);

            ResetTeslaGates();
            HandleWarheadBlackout();
        }

        private IEnumerator<float> FinalizeBlackoutEvent(bool occurred, float duration)
        {
            if (!occurred)
            {
                if (!IsBlackoutActive) TriggerCassieMessage(_config.CassieConfig.CassieMessageWrong);
                yield break;
            }

            IncrementBlackoutStack();
            TriggerCassieMessage(_config.CassieConfig.CassieKeter);
            _plugin.AudioManager.PlayAmbience();

            yield return Timing.WaitForSeconds(duration);

            DecrementBlackoutStack();

            if (!IsBlackoutActive)
            {
                TriggerCassieMessage(_config.CassieConfig.CassieMessageEnd);
                yield return Timing.WaitForSeconds(_config.CassieConfig.TimeBetweenSentenceAndEnd);
                ResetTeslaGates();
                _triggeredZones.Clear();
                _plugin.AudioManager.StopAmbience();
            }
        }

        private void ResetTeslaGates()
        {
            foreach (Tesla tesla in _libraryLabAPI.Teslas)
            {
                tesla.Trigger();
                tesla.InactiveTime = 5f;
            }
        }

        #endregion

        #region CASSIE Management

        private void TriggerCassieMessage(string message, bool isGlitchy = false)
        {
            if (string.IsNullOrWhiteSpace(message) || _cassieState != CassieStatus.Idle) return;

            _cassieState = CassieStatus.Playing;

            if (_config.CassieConfig.CassieMessageClearBeforeImportant)
                LibraryExiledAPI.ClearCassieQueue();

            if (isGlitchy) LibraryExiledAPI.SendGlitchyCassieMessage(message);
            else LibraryExiledAPI.SendCleanCassieMessage(message);

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

        #region Attack Logic

        public IEnumerator<float> KeterActionLoop()
        {
            while (_isInitialized)
            {
                yield return Timing.WaitForSeconds(_npcConfig.KeterActionDelay);

                if (!_plugin.IsEventActive || !IsBlackoutActive) continue;

                foreach (Player player in Player.ReadyList)
                {
                    try
                    {
                        if (player == null || !player.IsAlive || !player.IsHuman || player.Room.Name == RoomName.Pocket) continue;
                        if (!_libraryLabAPI.IsPlayerInDarkRoom(player)) continue;
                        if (!_sanityHandler.IsValidPlayer(player)) continue;

                        _sanityHandler.ApplyStageEffects(player);
                        PlayRandomAudioEffect(player);
                        _lightsourceHandler.ApplyLightsourceEffects(player);
                    }
                    catch (Exception ex)
                    {
                        LibraryLabAPI.LogError("Methods.KeterAction", $"Error processing {player?.Nickname}: {ex.Message}");
                    }
                }
            }
        }

        private void PlayRandomAudioEffect(Player player)
        {
            var options = new[] { AudioKey.WhispersMixed, AudioKey.Scream, AudioKey.ScreamAngry, AudioKey.Whispers };
            var selected = options[UnityEngine.Random.Range(0, options.Length)];
            _plugin.AudioManager.PlayAudioAutoManaged(player, selected, hearableForAllPlayers: true, lifespan: 16f);
        }

        #endregion

        #region Utility Methods

        public void StartBlackoutEventLoop()
        {
            Timing.KillCoroutines(CoroutineTags.BlackoutLoop);
            Timing.RunCoroutine(RunBlackoutLoop(), CoroutineTags.BlackoutLoop);
        }

        public void StartKeterActionLoop()
        {
            Timing.KillCoroutines(CoroutineTags.ActionLoop);
            Timing.RunCoroutine(KeterActionLoop(), CoroutineTags.ActionLoop);
        }

        public void StartSanityHandlerLoop()
        {
            Timing.KillCoroutines(CoroutineTags.SanityHandler);

            // Assigning to _sanityHandler allows external access if needed, but MEC handles execution
            Timing.RunCoroutine(_sanityHandler.HandleSanityDecay(), CoroutineTags.SanityHandler);
        }

        public void StartCassieCooldown()
        {
            Timing.KillCoroutines(CoroutineTags.CassieCooldown);
            Timing.RunCoroutine(CassieCooldownRoutine(), CoroutineTags.CassieCooldown);
        }

        public void IncrementBlackoutStack() { lock (BlackoutLock) _blackoutStacks++; }

        public void DecrementBlackoutStack()
        {
            lock (BlackoutLock)
            {
                _blackoutStacks = Math.Max(0, _blackoutStacks - 1);
                if (!IsBlackoutActive) Reset575();
            }
        }

        public bool AreAllGeneratorsEngaged(int req = 3) => Generator.List.Count >= req && Generator.List.All(gen => gen.Engaged);

        public void Reset575()
        {
            _plugin.AudioManager.StopAmbience();
            _blackoutStacks = 0;
            Map.ResetColorOfLights();
            Map.TurnOnLights();
            _triggeredZones.Clear();
            ResetTeslaGates();
        }

        /// <summary>
        /// Determines if an explosion is dangerous to SCP-575.
        /// Based on a strict whitelist to avoid issues with non-standard explosion types.
        /// </summary>
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

        public void Kill575() => Disable();

        #endregion
    }
}