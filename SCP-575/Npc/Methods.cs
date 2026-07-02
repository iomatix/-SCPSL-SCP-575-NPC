namespace SCP_575.Npc
{
    using Handlers;
    using LabApi.Features.Wrappers;
    using MapGeneration;
    using MEC;
    using SCP_575.ConfigObjects;
    using SCP_575.Shared;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEngine;

    /// <summary>
    /// Executes core tactical algorithms, facility blackout events, and structural combat state logic 
    /// for the SCP-575 entity framework. Handles synchronized timing loops for environmental progression.
    /// </summary>
    public class Methods
    {
        private readonly Plugin _plugin;
        private readonly PlayerLightsourceHandler _lightsourceHandler;
        private readonly PlayerSanityHandler _sanityHandler;
        private readonly LibraryLabAPI _libraryLabAPI;

        private bool _isInitialized = false;
        private readonly HashSet<FacilityZone> _triggeredZones = new();
        private readonly object BlackoutLock = new();

        private int _blackoutStacks = 0;
        private CassieStatus _cassieState = CassieStatus.Idle;

        private readonly Dictionary<int, NpcBehaviorState> _playerAiStates = new();

        private const string TempCoroutineTag = CoroutineTags.Temp;

        private enum CassieStatus
        {
            Idle,
            Playing,
            Cooldown
        }

        /// <summary>
        /// Defines operational predatory intervals for the shadow anomaly, modulating
        /// tactical pressure and mechanical trauma delivery based on subject vulnerability.
        /// </summary>
        public enum NpcBehaviorState
        {
            /// <summary> The entity is passive or the subject is fully protected by localized room lighting. </summary>
            Dormant,
            /// <summary> The entity is actively stalking and closing in on a subject holding an active light source. </summary>
            Stalking,
            /// <summary> The entity undergoes breakthrough, executing targets left completely vulnerable in darkness. </summary>
            Striking
        }

        public Methods(Plugin plugin)
        {
            _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));

            _lightsourceHandler = _plugin.LightsourceHandler ?? throw new InvalidOperationException("LightsourceHandler missing.");
            _sanityHandler = _plugin.SanityEventHandler ?? throw new InvalidOperationException("SanityEventHandler missing.");
            _libraryLabAPI = _plugin.LibraryLabAPI ?? throw new InvalidOperationException("LibraryLabAPI missing.");

            LibraryLabAPI.LogDebug(nameof(Methods), "NPC Methods system linked with Handlers and LibraryAPI.");
        }

        public bool IsBlackoutActive { get { lock (BlackoutLock) return _blackoutStacks > 0; } }
        public int GetCurrentBlackoutStacks { get { lock (BlackoutLock) return _blackoutStacks; } }

        #region Lifecycle Management

        public void Init(float roll = -1f)
        {
            if (_isInitialized) return;

            if (roll <= _plugin.Blackout.EventChance)
            {
                _isInitialized = true;
                _plugin.IsEventActive = true;

                LibraryLabAPI.LogInfo(nameof(Init), $"SCP-575 spawn roll successful. Initializing loops.");

                StartBlackoutEventLoop();
                StartKeterActionLoop();
                StartSanityHandlerLoop();
                _plugin.MapHandler?.ExecuteFlashlightDistribution();

                _plugin.AudioDirector?.Initialize();

                foreach (var player in Player.ReadyList)
                {
                    if (_sanityHandler.IsValidPlayer(player))
                        _sanityHandler.SetSanity(player, _plugin.Sanity.InitialSanity);
                }
            }
            else
            {
                LibraryLabAPI.LogDebug(nameof(Init), $"SCP-575 will not spawn this round.");
            }
        }

        public void Disable()
        {
            _isInitialized = false;
            _plugin.IsEventActive = false;

            // Kill all static lifecycle threads
            foreach (string tag in CoroutineTags.AllStaticTags)
            {
                Timing.KillCoroutines(tag);
            }

            Timing.KillCoroutines(TempCoroutineTag);
            LibraryLabAPI.LogDebug(nameof(Disable), "Killed all static and dynamic SCP-575 coroutines via system tags.");

            _plugin.AudioDirector?.Clean();
            _plugin.AudioManager?.Clean(fullShutdown: true);
            _sanityHandler?.Clean();
            _plugin.LightsourceHandler?.Clean();

            _playerAiStates.Clear();
            _triggeredZones.Clear();

            LibraryLabAPI.LogInfo(nameof(Disable), "SCP-575 NPC logic and all coroutines terminated safely.");
        }

        public void Clean()
        {
            Timing.KillCoroutines(CoroutineTags.CassieCooldown);
            Timing.KillCoroutines(CoroutineTags.GridTearDown);
            Timing.KillCoroutines(CoroutineTags.GeneratorSurge);
            Timing.KillCoroutines(TempCoroutineTag);
            Reset575();
        }

        #endregion

        #region Blackout Management

        public IEnumerator<float> RunBlackoutLoop()
        {
            yield return Timing.WaitForSeconds(_plugin.Blackout.InitialDelay);

            while (_isInitialized)
            {
                if (!_plugin.IsEventActive)
                {
                    yield return Timing.WaitForSeconds(1f);
                    continue;
                }

                float delay = _plugin.Blackout.RandomEvents
                    ? UnityEngine.Random.Range(_plugin.Blackout.DelayMin, _plugin.Blackout.DelayMax)
                    : _plugin.Blackout.InitialDelay;

                yield return Timing.WaitForSeconds(delay);
                Timing.RunCoroutine(ExecuteBlackoutEvent(), TempCoroutineTag);
            }
        }

        private IEnumerator<float> ExecuteBlackoutEvent()
        {
            var audioConfig = _plugin.Audio;

            if (!IsBlackoutActive)
            {
                if (_plugin.Cassie.CassieMessageClearBeforeImportant)
                    Announcer.Clear();

                TriggerCassieMessage(_plugin.Cassie.CassieMessageStart, true);
                if (_plugin.Blackout.FlickerLights)
                    FlickerAffectedZones(_plugin.Blackout.FlickerDuration);

                yield return Timing.WaitForSeconds(_plugin.Cassie.TimeBetweenSentenceAndStart);
                TriggerCassieMessage(_plugin.Cassie.CassiePostMessage);
            }

            Player targetPlayer = null;
            if (UnityEngine.Random.Range(0f, 100f) < 70f)
            {
                var validTargets = Player.ReadyList.Where(p => p.IsAlive && p.IsHuman && _libraryLabAPI.IsPlayerInDarkRoom(p)).ToList();
                if (validTargets.Count > 0)
                {
                    targetPlayer = validTargets[UnityEngine.Random.Range(0, validTargets.Count)];
                }
            }
            _plugin.AudioDirector?.ProcessBlackoutAudioSequence(targetPlayer);

            float duration = _plugin.Blackout.RandomEvents
                ? UnityEngine.Random.Range(_plugin.Blackout.DurationMin, _plugin.Blackout.DurationMax)
                : _plugin.Blackout.DurationMax;

            bool occurred = _plugin.Blackout.UsePerRoomChances
                ? HandleRoomSpecificBlackout(duration)
                : HandleZoneSpecificBlackout(duration);

            Timing.RunCoroutine(FinalizeBlackoutEvent(occurred, duration), TempCoroutineTag);
        }

        private void FlickerAffectedZones(float duration)
        {
            if (!_plugin.IsEventActive || !_plugin.Blackout.FlickerLights) return;

            var zones = GetZonesToFlicker();
            foreach (var zone in zones)
                Timing.RunCoroutine(FlickerZoneLightsCoroutine(zone), TempCoroutineTag);
        }

        private List<FacilityZone> GetZonesToFlicker()
        {
            if (_plugin.Blackout.UsePerRoomChances || _plugin.Blackout.EnableFacilityBlackout)
                return Enum.GetValues(typeof(FacilityZone)).Cast<FacilityZone>().ToList();

            var zones = new List<FacilityZone>();
            if (_plugin.Blackout.ChanceLight > 0) zones.Add(FacilityZone.LightContainment);
            if (_plugin.Blackout.ChanceHeavy > 0) zones.Add(FacilityZone.HeavyContainment);
            if (_plugin.Blackout.ChanceEntrance > 0) zones.Add(FacilityZone.Entrance);
            if (_plugin.Blackout.ChanceSurface > 0) zones.Add(FacilityZone.Surface);
            return zones;
        }

        public IEnumerator<float> FlickerZoneLightsCoroutine(FacilityZone targetZone)
        {
            var color = new Color(_plugin.Blackout.LightsColorR, _plugin.Blackout.LightsColorG, _plugin.Blackout.LightsColorB);
            float interval = 1f / _plugin.Blackout.FlickerFrequency;
            int flickers = Mathf.RoundToInt(_plugin.Blackout.FlickerDuration / interval);

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
            triggered |= AttemptZoneBlackout(FacilityZone.LightContainment, _plugin.Blackout.ChanceLight, _plugin.Cassie.CassieMessageLight, duration);
            triggered |= AttemptZoneBlackout(FacilityZone.HeavyContainment, _plugin.Blackout.ChanceHeavy, _plugin.Cassie.CassieMessageHeavy, duration);
            triggered |= AttemptZoneBlackout(FacilityZone.Entrance, _plugin.Blackout.ChanceEntrance, _plugin.Cassie.CassieMessageEntrance, duration);
            triggered |= AttemptZoneBlackout(FacilityZone.Surface, _plugin.Blackout.ChanceSurface, _plugin.Cassie.CassieMessageSurface, duration);

            if (!triggered && _plugin.Blackout.EnableFacilityBlackout)
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
            if (!IsBlackoutActive) TriggerCassieMessage(_plugin.Cassie.CassieMessageFacility, true);
        }

        private bool HandleRoomSpecificBlackout(float duration)
        {
            bool triggered = false;
            foreach (Room room in _libraryLabAPI.Rooms.Where(r => r.AllLightControllers.Any()))
            {
                if (AttemptRoomBlackout(room, duration)) triggered = true;
            }

            if (!triggered && _plugin.Blackout.EnableFacilityBlackout)
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

            HandleRoomBlackout(room, duration, forced);

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
                FacilityZone.HeavyContainment => (_plugin.Blackout.ChanceHeavy, _plugin.Cassie.CassieMessageHeavy),
                FacilityZone.LightContainment => (_plugin.Blackout.ChanceLight, _plugin.Cassie.CassieMessageLight),
                FacilityZone.Entrance => (_plugin.Blackout.ChanceEntrance, _plugin.Cassie.CassieMessageEntrance),
                _ => (_plugin.Blackout.ChanceOther, _plugin.Cassie.CassieMessageOther)
            };
        }

        private void HandleRoomBlackout(Room room, float duration, bool forced = false)
        {
            if (!forced && !_libraryLabAPI.IsRoomAndNeighborsFreeOfEngagedGenerators(room)) return;

            HandleTeslaBlackout(room, duration);
            HandleWarheadBlackout();

            _libraryLabAPI.TurnOffRoomLights(room, duration, _plugin.Blackout.ElevatorLockdownProbability);
        }

        private void HandleTeslaBlackout(Room room, float duration)
        {
            if (_plugin.Blackout.DisableTeslas && room.Name == RoomName.HczTesla && Tesla.TryGet(room, out Tesla tesla))
            {
                tesla.InactiveTime = duration + 0.5f;
                tesla.Trigger();
            }
        }

        private void HandleWarheadBlackout()
        {
            if (_plugin.Blackout.DisableNuke && Warhead.IsDetonationInProgress && !Warhead.IsLocked)
                Warhead.Stop();
        }

        private void DisableFacilitySystems(float duration)
        {
            foreach (Room room in _libraryLabAPI.Rooms.Where(_libraryLabAPI.IsRoomAndNeighborsFreeOfEngagedGenerators))
                _libraryLabAPI.TurnOffRoomLights(room, duration, _plugin.Blackout.ElevatorLockdownProbability);

            ResetTeslaGates();
            HandleWarheadBlackout();
        }

        private IEnumerator<float> FinalizeBlackoutEvent(bool occurred, float duration)
        {
            if (!occurred)
            {
                if (!IsBlackoutActive) TriggerCassieMessage(_plugin.Cassie.CassieMessageWrong);
                yield break;
            }

            IncrementBlackoutStack();
            TriggerCassieMessage(_plugin.Cassie.CassieKeter);

            yield return Timing.WaitForSeconds(duration);

            DecrementBlackoutStack();

            if (!IsBlackoutActive)
            {
                TriggerCassieMessage(_plugin.Cassie.CassieMessageEnd);
                yield return Timing.WaitForSeconds(_plugin.Cassie.TimeBetweenSentenceAndEnd);
                ResetTeslaGates();
                _triggeredZones.Clear();
                _plugin.AudioManager.Clean();
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

        public void StartTimedBlackoutBoost(float duration, string logContext, string startLog, string endLog, Action startAction = null)
        {
            Timing.RunCoroutine(TimedBlackoutBoostCoroutine(duration, logContext, startLog, endLog, startAction), CoroutineTags.BlackoutStacks);
        }

        private IEnumerator<float> TimedBlackoutBoostCoroutine(float duration, string logContext, string startLog, string endLog, Action startAction = null)
        {
            IncrementBlackoutStack();
            try
            {
                startAction?.Invoke();

                if (!string.IsNullOrEmpty(startLog))
                    LibraryLabAPI.LogInfo(logContext, startLog);

                yield return Timing.WaitForSeconds(duration);
            }
            finally
            {
                DecrementBlackoutStack();

                if (!string.IsNullOrEmpty(endLog))
                    LibraryLabAPI.LogInfo(logContext, endLog);
            }
        }

        #endregion

        #region CASSIE Management

        private void TriggerCassieMessage(string message, bool isGlitchy = false)
        {
            if (string.IsNullOrWhiteSpace(message) || _cassieState != CassieStatus.Idle) return;

            _cassieState = CassieStatus.Playing;

            if (_plugin.Cassie.CassieMessageClearBeforeImportant)
                Announcer.Clear();

            if (isGlitchy) Announcer.GlitchyMessage(message, _plugin.Cassie.GlitchChance, _plugin.Cassie.JamChance);
            else Announcer.Message(message, message, playBackground: false, priority: 0.65f, glitchScale: 0f);
            {

                StartCassieCooldown();
            }
        }

        private IEnumerator<float> CassieCooldownRoutine()
        {
            yield return Timing.WaitForSeconds(_plugin.Cassie.TimeBetweenSentenceAndStart + 0.5f);
            _cassieState = CassieStatus.Cooldown;
            yield return Timing.WaitForSeconds(1f);
            _cassieState = CassieStatus.Idle;
        }

        #endregion

        #region Attack Logic & AI State Machine

        /// <summary>
        /// Main operational decision thread for the SCP-575 entity. Evaluates subject parameters 
        /// inside unlit sectors and routes behavioral state executions strictly via architectural handler abstractions.
        /// </summary>
        public IEnumerator<float> KeterActionLoop()
        {
            while (_isInitialized)
            {
                float offset = UnityEngine.Random.Range(-_plugin.Npc.KeterActionDelayRandomizerValue, _plugin.Npc.KeterActionDelayRandomizerValue);
                float finalDelay = _plugin.Npc.KeterActionDelay + offset;

                if (finalDelay < 0.25f) finalDelay = 0.25f;

                yield return Timing.WaitForSeconds(finalDelay);

                if (!_plugin.IsEventActive || !IsBlackoutActive) continue;

                foreach (Player player in Player.ReadyList)
                {
                    if (player?.GameObject == null || !player.IsAlive || !player.IsHuman || player.Room.Name == RoomName.Pocket)
                        continue;

                    try
                    {
                        // FIX: Zero-allocation lookup targeting native GameObject instance identifier integers
                        int playerInstanceId = player.GameObject.GetInstanceID();
                        bool isInDarkness = _libraryLabAPI.IsPlayerInDarkRoom(player);

                        if (!isInDarkness || !_sanityHandler.IsValidPlayer(player))
                        {
                            _playerAiStates[playerInstanceId] = NpcBehaviorState.Dormant;
                            continue;
                        }

                        bool hasActiveLight = !Helpers.IsHumanWithoutLight(player);

                        NpcBehaviorState newState = hasActiveLight ? NpcBehaviorState.Stalking : NpcBehaviorState.Striking;
                        _playerAiStates[playerInstanceId] = newState;

                        // Decoupled transaction dispatch execution execution pipelines
                        _sanityHandler.ApplyDamageToPlayer(player);

                        if (newState == NpcBehaviorState.Stalking)
                        {
                            _lightsourceHandler.ApplyLightsourceEffects(player);
                        }
                    }
                    catch (Exception ex)
                    {
                        LibraryLabAPI.LogError("Methods.KeterAction", $"Error executing macro strike processing for target {player.Nickname}: {ex.Message}");
                    }
                }
            }
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

        /// <summary>
        /// Initiates a synchronized delayed state mutation sequence once all facility power generators are fully engaged.
        /// </summary>
        public void ProcessFullGridRestorationTeardown()
        {
            Timing.RunCoroutine(ExecuteGridRestorationRoutine(), CoroutineTags.GridTearDown);
        }

        private IEnumerator<float> ExecuteGridRestorationRoutine()
        {
            yield return Timing.WaitForSeconds(3.75f);
            try
            {
                if (_plugin.Npc.IsNpcKillable)
                {
                    Kill575();
                    LibraryLabAPI.LogInfo("GeneratorHandler", "SCP-575 permanently terminated via core power grid restoration.");
                }
                else
                {
                    Reset575();
                    LibraryLabAPI.LogInfo("GeneratorHandler", "Facility grid operational. SCP-575 suppressed, background loops preserved.");
                }
            }
            catch (Exception ex)
            {
                LibraryLabAPI.LogError("GeneratorHandler.Teardown", $"Failed to execute post-mortem state change: {ex.Message}");
            }
        }

        /// <summary>
        /// Triggers a localized stabilization window. The anomaly forces a temporary blackout surge, 
        /// but the grid permanently locks into a full safe-zone once the stabilization timer expires.
        /// </summary>
        public void ExecuteLocalizedRetaliationSurge(Room generatorRoom)
        {
            if (generatorRoom == null) return;

            // Fire the dual-phase stabilization pipeline using MEC
            Timing.RunCoroutine(ExecuteStabilizationPipelineCoroutine(generatorRoom), CoroutineTags.GeneratorSurge);
        }


        private IEnumerator<float> ExecuteStabilizationPipelineCoroutine(Room generatorRoom)
        {
            // Wait 110ms to let the native base-game generator scripts finish forcing the lights ON, 
            // before our anomaly clamps the grid back into tactical darkness.
            yield return Timing.WaitForSeconds(0.11f);

            if (generatorRoom == null || !_plugin.IsEventActive) yield break;

            // Phase 1: Localized grid overload (The monster's desperate counter-attack)
            float stabilizationWindow = _plugin.Blackout.GeneratorStabilizationDuration;

            _libraryLabAPI.DisableRoomAndNeighborLights(generatorRoom, stabilizationWindow, forced: true);

            // Hand off the overload scare design entirely to the director layer
            _plugin.AudioDirector?.ProcessGeneratorOverloadRetaliation(generatorRoom.Position);

            // Wait out the high-tension stabilization window
            yield return Timing.WaitForSeconds(stabilizationWindow);

            // Phase 2: Grid Lock (The generator fully stabilizes and locks out the anomaly)
            if (_plugin.IsEventActive && generatorRoom != null)
            {
                _libraryLabAPI.EnableAndFlickerRoomAndNeighborLights(generatorRoom, 0f);
                _plugin.AudioDirector?.ProcessGeneratorStabilizedFeedback(generatorRoom.Position);

                LibraryLabAPI.LogInfo("Methods.GeneratorStabilize", $"Generator room {generatorRoom.Name} fully stabilized. Safe-zone locked.");
            }
        }

        public void Reset575()
        {
            _plugin.AudioManager.Clean(fullShutdown: false);

            lock (BlackoutLock)
            {
                _blackoutStacks = 0;
            }

            Map.ResetColorOfLights();
            Map.TurnOnLights();
            _triggeredZones.Clear();
            ResetTeslaGates();

            // Evict structural tracking coordinates safely to enforce cross-round data isolation models
            _playerAiStates.Clear();
        }

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

        public void ForceGlobalBlackoutEvent()
        {
            if (!_isInitialized || !_plugin.IsEventActive) return;

            Timing.RunCoroutine(ExecuteBlackoutEvent(), CoroutineTags.Temp);
        }

        public void Kill575() => Disable();

        #endregion
    }
}