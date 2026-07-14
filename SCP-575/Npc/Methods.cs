using LabApi.Extensions;
using LabApi.Extensions.Misc;
using LabApi.Features.Wrappers;
using MapGeneration;
using MEC;
using SCP_575.Handlers;
using SCP_575.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using Logger = LabApi.Extensions.Misc.iLogger;

namespace SCP_575.Npc
{
    /// <summary>
    /// Manages lifecycle hooks, predictive AI tracking loops, and illumination overrides for SCP-575 using Fluent API bindings.
    /// </summary>
    public class Methods
    {
        #region Fields
        private readonly Plugin _plugin;
        private readonly PlayerLightsourceHandler _lightsourceHandler;
        private readonly PlayerSanityHandler _sanityHandler;

        private bool _isInitialized;
        private int _blackoutStacks;
        private CassieStatus _cassieState = CassieStatus.Idle;

        private readonly HashSet<FacilityZone> _triggeredZones = new();
        private readonly Dictionary<int, NpcBehaviorState> _playerAiStates = new();
        private readonly object _blackoutLock = new();

        private const string TempCoroutineTag = CoroutineTags.Temp;

        // Static immutable cache for FacilityZone entries to eradicate Enum.GetValues heap allocations
        private static readonly FacilityZone[] AllZones = (FacilityZone[])Enum.GetValues(typeof(FacilityZone));
        #endregion

        #region Enums
        private enum CassieStatus
        {
            Idle,
            Playing,
            Cooldown
        }

        /// <summary>
        /// Specifies threat behavioral tiers for active targets.
        /// </summary>
        public enum NpcBehaviorState
        {
            /// <summary> Target is safe under functional lighting fields. </summary>
            Dormant,
            /// <summary> Target is in darkness but actively using a light source. </summary>
            Stalking,
            /// <summary> Target is fully exposed to darkness. </summary>
            Striking
        }
        #endregion

        #region Constructor
        /// <summary>
        /// Binds subsystem dependencies and establishes the execution context.
        /// </summary>
        public Methods(Plugin plugin)
        {
            _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
            _lightsourceHandler = _plugin.LightsourceHandler ?? throw new InvalidOperationException("LightsourceHandler missing.");
            _sanityHandler = _plugin.SanityHandler ?? throw new InvalidOperationException("SanityHandler missing.");

            Logger.Debug(nameof(Methods), "SCP-575 core execution registry bound to Fluent extension tracks.", _plugin.Debug);
        }
        #endregion

        #region Properties
        /// <summary>
        /// Checks if at least one active blackout event layer is processing.
        /// </summary>
        public bool IsBlackoutActive => Volatile.Read(ref _blackoutStacks) > 0;

        /// <summary>
        /// Returns the accumulation count of concurrent blackout event layers.
        /// </summary>
        public int GetCurrentBlackoutStacks => Volatile.Read(ref _blackoutStacks);

        /// <summary>
        /// Determines with zero heap allocations whether a specific facility zone is under an active SCP-575 blackout.
        /// </summary>
        public bool IsZoneUnderBlackout(FacilityZone zone)
        {
            lock (_blackoutLock)
            {
                return _blackoutStacks > 0 && _triggeredZones.Contains(zone);
            }
        }
        #endregion

        #region Lifecycle
        /// <summary>
        /// Runs probability weights and starts background event loops if successful.
        /// </summary>
        public void Init(float roll = -1f)
        {
            if (_isInitialized) return;

            float spawnRoll = roll < 0f ? SafeRandom.Range(0f, 100f) : roll;

            if (spawnRoll <= _plugin.Blackout.EventChance)
            {
                _isInitialized = true;
                _plugin.IsEventActive = true;

                Logger.Info(nameof(Methods), $"SCP-575 threat matrix active ({spawnRoll}% <= {_plugin.Blackout.EventChance}%). Loops starting.");

                StartBlackoutEventLoop();
                StartKeterActionLoop();
                StartSanityHandlerLoop();

                _plugin.MapHandler?.ExecuteFlashlightDistribution();
                _plugin.AudioDirector?.Initialize();

                foreach (Player player in Player.ReadyList)
                {
                    if (_sanityHandler.IsValidPlayer(player))
                        _sanityHandler.SetPlayerSanity(player, _plugin.Sanity.InitialSanity);
                }
            }
            else
            {
                Logger.Info(nameof(Methods), $"SCP-575 threat matrix skipped for this round cycle ({spawnRoll}%).");
            }
        }

        /// <summary>
        /// Restores facility lighting maps, halts background coroutines, and flushes runtime trackers.
        /// </summary>
        public void Disable()
        {
            _isInitialized = false;
            _plugin.IsEventActive = false;

            CoroutineTags.AllStaticTags.Kill();
            TempCoroutineTag.Kill();

            _plugin.AudioDirector?.Clean();
            _plugin.AudioManager?.Clean(fullShutdown: true);
            _sanityHandler?.Clean();
            _plugin.LightsourceHandler?.Clean();

            _playerAiStates.Clear();
            _triggeredZones.Clear();

            Logger.Info(nameof(Methods), "SCP-575 core logic modules and timeline loops terminated safely.");
        }

        /// <summary>
        /// Resets background tracking parameters and purges active memory registries.
        /// </summary>
        public void Clean()
        {
            CoroutineTags.CassieCooldown.Kill();
            CoroutineTags.GridTearDown.Kill();
            CoroutineTags.GeneratorSurge.Kill();
            TempCoroutineTag.Kill();
            Reset575();
        }
        #endregion

        #region Loops
        /// <summary>
        /// Continuously evaluates timelines to schedule and deploy automated blackout events.
        /// </summary>
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
                    ? SafeRandom.Range((float)_plugin.Blackout.DelayMin, (float)_plugin.Blackout.DelayMax)
                    : _plugin.Blackout.InitialDelay;

                yield return Timing.WaitForSeconds(delay);
                Timing.RunCoroutine(ExecuteBlackoutEvent(), TempCoroutineTag);
            }
        }

        private IEnumerator<float> ExecuteBlackoutEvent()
        {
            if (!IsBlackoutActive)
            {
                if (_plugin.Cassie.CassieMessageClearBeforeImportant)
                    CassieExtensions.CassieClear();

                double startMessageDuration = TriggerCassieMessage(_plugin.Cassie.CassieMessageStart, isGlitchy: true);

                if (_plugin.Blackout.FlickerLights)
                    FlickerAffectedZones();

                yield return Timing.WaitForSeconds((float)startMessageDuration + _plugin.Blackout.BlackoutBufferTime);
                TriggerCassieMessage(_plugin.Cassie.CassiePostMessage, isGlitchy: true);
            }

            Player targetPlayer = null;
            if (SafeRandom.Range(0f, 100f) < 70f)
            {
                int validCount = 0;
                foreach (Player p in Player.ReadyList)
                {
                    if (p != null && p.IsAlive && p.IsHuman && p.IsInDarkRoom())
                    {
                        validCount++;
                    }
                }

                if (validCount > 0)
                {
                    int randomIndex = SafeRandom.Next(0, validCount);
                    int current = 0;
                    foreach (Player p in Player.ReadyList)
                    {
                        if (p != null && p.IsAlive && p.IsHuman && p.IsInDarkRoom())
                        {
                            if (current == randomIndex)
                            {
                                targetPlayer = p;
                                break;
                            }
                            current++;
                        }
                    }
                }
            }
            _plugin.AudioDirector?.ProcessBlackoutAudioSequence(targetPlayer);

            float duration = _plugin.Blackout.RandomEvents
                ? SafeRandom.Range(_plugin.Blackout.DurationMin, _plugin.Blackout.DurationMax)
                : _plugin.Blackout.DurationMax;

            bool occurred = _plugin.Blackout.UsePerRoomChances
                ? HandleRoomSpecificBlackout(duration)
                : HandleZoneSpecificBlackout(duration);

            Timing.RunCoroutine(FinalizeBlackoutEvent(occurred, duration), TempCoroutineTag);
        }
        #endregion

        #region Grid Overrides
        private void FlickerAffectedZones()
        {
            if (!_plugin.IsEventActive || !_plugin.Blackout.FlickerLights) return;

            Color color = new Color(_plugin.Blackout.LightsColorR, _plugin.Blackout.LightsColorG, _plugin.Blackout.LightsColorB);

            // Inlined structural decision matrix to prevent allocating temporary list buffers entirely
            if (_plugin.Blackout.UsePerRoomChances || _plugin.Blackout.EnableFacilityBlackout)
            {
                for (int i = 0; i < AllZones.Length; i++)
                {
                    Timing.RunCoroutine(AllZones[i].FlickerLightsCoroutine(color, _plugin.Blackout.FlickerDuration, _plugin.Blackout.FlickerFrequency), TempCoroutineTag);
                }
                return;
            }

            if (_plugin.Blackout.ChanceLight > 0f)
                Timing.RunCoroutine(FacilityZone.LightContainment.FlickerLightsCoroutine(color, _plugin.Blackout.FlickerDuration, _plugin.Blackout.FlickerFrequency), TempCoroutineTag);
            if (_plugin.Blackout.ChanceHeavy > 0f)
                Timing.RunCoroutine(FacilityZone.HeavyContainment.FlickerLightsCoroutine(color, _plugin.Blackout.FlickerDuration, _plugin.Blackout.FlickerFrequency), TempCoroutineTag);
            if (_plugin.Blackout.ChanceEntrance > 0f)
                Timing.RunCoroutine(FacilityZone.Entrance.FlickerLightsCoroutine(color, _plugin.Blackout.FlickerDuration, _plugin.Blackout.FlickerFrequency), TempCoroutineTag);
            if (_plugin.Blackout.ChanceSurface > 0f)
                Timing.RunCoroutine(FacilityZone.Surface.FlickerLightsCoroutine(color, _plugin.Blackout.FlickerDuration, _plugin.Blackout.FlickerFrequency), TempCoroutineTag);
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
            if (!_plugin.IsEventActive || !chance.RollChance()) return false;

            zone.TurnOffLights(duration);

            if (!IsBlackoutActive) TriggerCassieMessage(cassie, isGlitchy: true);
            return true;
        }

        private void TriggerFacilityWideBlackout(float duration)
        {
            foreach (var zone in ZoneExtensions.All)
            {
                zone.TurnOffLights(duration);
                lock (_blackoutLock)
                {
                    _triggeredZones.Add(zone);
                }
            }

            if (!IsBlackoutActive) TriggerCassieMessage(_plugin.Cassie.CassieMessageFacility, isGlitchy: true);
        }

        private bool HandleRoomSpecificBlackout(float duration)
        {
            bool triggered = false;

            foreach (Room room in Room.List)
            {
                if (room?.AllLightControllers != null && room.AllLightControllers.Any())
                {
                    if (AttemptRoomBlackout(room, duration)) triggered = true;
                }
            }

            if (!triggered && _plugin.Blackout.EnableFacilityBlackout)
            {
                TriggerFacilityWideBlackout(duration);
                return true;
            }
            return triggered;
        }

        /// <summary>
        /// Executes an illumination power drop on a single room node if probability criteria are met.
        /// </summary>
        public bool AttemptRoomBlackout(Room room, float duration, bool forced = false, bool silent = false)
        {
            if (!_plugin.IsEventActive || room is null) return false;

            var (chance, cassie) = GetRoomBlackoutParams(room.Zone);
            if (!forced && !chance.RollChance()) return false;

            HandleRoomBlackout(room, duration, forced);

            if (!_triggeredZones.Contains(room.Zone) && !silent)
            {
                if (!IsBlackoutActive) TriggerCassieMessage(cassie);
                _triggeredZones.Add(room.Zone);
            }
            return true;
        }

        private (float Chance, string Cassie) GetRoomBlackoutParams(FacilityZone zone) => zone switch
        {
            FacilityZone.HeavyContainment => (_plugin.Blackout.ChanceHeavy, _plugin.Cassie.CassieMessageHeavy),
            FacilityZone.LightContainment => (_plugin.Blackout.ChanceLight, _plugin.Cassie.CassieMessageLight),
            FacilityZone.Entrance => (_plugin.Blackout.ChanceEntrance, _plugin.Cassie.CassieMessageEntrance),
            _ => (_plugin.Blackout.ChanceOther, _plugin.Cassie.CassieMessageOther)
        };

        private void HandleRoomBlackout(Room room, float duration, bool forced = false)
        {
            if (!forced && !room.IsFreeOfEngagedGenerators()) return;

            HandleTeslaBlackout(room, duration);
            HandleWarheadBlackout();

            room.TurnOffLights(duration);

            foreach (Elevator elevator in room.GetElevatorsConnectedToRoom())
            {
                elevator.TurnOffLights(duration);
            }
        }

        private void HandleTeslaBlackout(Room room, float duration)
        {
            if (_plugin.Blackout.DisableTeslas && room.Name is RoomName.HczTesla && Tesla.TryGet(room, out Tesla tesla))
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
            foreach (Room room in Room.List)
            {
                if (room != null && room.IsFreeOfEngagedGenerators())
                {
                    room.TurnOffLights(duration);
                    foreach (Elevator elevator in room.GetElevatorsConnectedToRoom())
                    {
                        elevator.TurnOffLights(duration);
                    }
                }
            }

            ResetTeslaGates();
            HandleWarheadBlackout();
        }

        private IEnumerator<float> FinalizeBlackoutEvent(bool occurred, float duration)
        {
            if (!occurred)
            {
                if (!IsBlackoutActive) TriggerCassieMessage(_plugin.Cassie.CassieMessageWrong, isGlitchy: true);
                yield break;
            }

            IncrementBlackoutStack();
            TriggerCassieMessage(_plugin.Cassie.CassieKeter);

            yield return Timing.WaitForSeconds(duration);

            DecrementBlackoutStack();

            if (!IsBlackoutActive)
            {
                double endMessageDuration = TriggerCassieMessage(_plugin.Cassie.CassieMessageEnd, isGlitchy: true);
                yield return Timing.WaitForSeconds((float)endMessageDuration + 0.5f);
                ResetTeslaGates();
                ZoneExtensions.All.TurnOnLights();
                _triggeredZones.Clear();
                _plugin.AudioManager.Clean(fullShutdown: false);
            }
        }

        private void ResetTeslaGates()
        {
            foreach (Tesla tesla in Tesla.List)
            {
                tesla.Trigger();
                tesla.InactiveTime = 5f;
            }
        }
        #endregion

        #region Commands
        /// <summary>
        /// Forces an immediate facility blackout sequence via administration command parameters.
        /// </summary>
        public void ForceGlobalBlackoutEvent()
        {
            if (!_isInitialized || !_plugin.IsEventActive) return;
            Timing.RunCoroutine(ExecuteBlackoutEvent(), TempCoroutineTag);
        }

        /// <summary>
        /// Forces an immediate blackout sequence isolated to a single facility zone.
        /// </summary>
        public void ForceZoneBlackoutEvent(FacilityZone zone)
        {
            if (!_isInitialized || !_plugin.IsEventActive) return;
            Timing.RunCoroutine(ExecuteZoneBlackoutEventRoutine(zone), TempCoroutineTag);
        }

        private IEnumerator<float> ExecuteZoneBlackoutEventRoutine(FacilityZone zone)
        {
            float duration = _plugin.Blackout.DurationMax;

            if (!IsBlackoutActive)
            {
                if (_plugin.Cassie.CassieMessageClearBeforeImportant)
                    CassieExtensions.CassieClear();

                double startMessageDuration = TriggerCassieMessage(_plugin.Cassie.CassieMessageStart, isGlitchy: true);

                if (_plugin.Blackout.FlickerLights)
                {
                    Color color = new Color(_plugin.Blackout.LightsColorR, _plugin.Blackout.LightsColorG, _plugin.Blackout.LightsColorB);
                    Timing.RunCoroutine(zone.FlickerLightsCoroutine(color, _plugin.Blackout.FlickerDuration, _plugin.Blackout.FlickerFrequency), TempCoroutineTag);
                }

                yield return Timing.WaitForSeconds((float)startMessageDuration + _plugin.Blackout.BlackoutBufferTime);

                string zoneCassie = zone switch
                {
                    FacilityZone.HeavyContainment => _plugin.Cassie.CassieMessageHeavy,
                    FacilityZone.LightContainment => _plugin.Cassie.CassieMessageLight,
                    FacilityZone.Entrance => _plugin.Cassie.CassieMessageEntrance,
                    FacilityZone.Surface => _plugin.Cassie.CassieMessageSurface,
                    _ => _plugin.Cassie.CassieMessageOther
                };

                TriggerCassieMessage(zoneCassie, isGlitchy: true);
            }

            zone.TurnOffLights(duration);
            Timing.RunCoroutine(FinalizeBlackoutEvent(occurred: true, duration), TempCoroutineTag);
        }
        #endregion

        #region Registration & Stacks
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
                    Logger.Info(logContext, startLog);

                yield return Timing.WaitForSeconds(duration);
            }
            finally
            {
                DecrementBlackoutStack();

                if (!string.IsNullOrEmpty(endLog))
                    Logger.Info(logContext, endLog);
            }
        }

        public void IncrementBlackoutStack()
        {
            Interlocked.Increment(ref _blackoutStacks);
        }

        public void DecrementBlackoutStack()
        {
            if (Interlocked.Decrement(ref _blackoutStacks) == 0)
            {
                Reset575();
            }
        }
        #endregion

        #region Broadcasts
        private double TriggerCassieMessage(string message, bool isGlitchy = false, bool force = false)
        {
            string sanitizedMessage = message.SanitizeCassieString();
            if (string.IsNullOrEmpty(sanitizedMessage)) return 0.0;

            if (!force && Announcer.IsSpeaking)
            {
                Logger.Debug(nameof(Methods), $"Announcer busy. Dropping: {sanitizedMessage}", _plugin.Debug);
                return 0.0;
            }

            if (!force && _cassieState != CassieStatus.Idle)
            {
                Logger.Debug(nameof(Methods), $"Pipeline locked. Status: [{_cassieState}].", _plugin.Debug);
                return 0.0;
            }

            _cassieState = CassieStatus.Playing;

            if (_plugin.Cassie.CassieMessageClearBeforeImportant)
                CassieExtensions.CassieClear();

            double duration = isGlitchy
                ? CassieExtensions.DispatchGlitchyMessage(sanitizedMessage, _plugin.Cassie.GlitchChance, _plugin.Cassie.JamChance)
                : CassieExtensions.DispatchMessage(sanitizedMessage);

            StartCassieCooldown(duration);
            return duration;
        }

        private void StartCassieCooldown(double duration)
        {
            CoroutineTags.CassieCooldown.Kill();
            Timing.RunCoroutine(CassieCooldownRoutine(duration), CoroutineTags.CassieCooldown);
        }

        private IEnumerator<float> CassieCooldownRoutine(double duration)
        {
            yield return Timing.WaitForSeconds((float)duration + 0.5f);
            _cassieState = CassieStatus.Cooldown;
            yield return Timing.WaitForSeconds(1f);
            _cassieState = CassieStatus.Idle;
        }
        #endregion

        #region AI Predatory Decisions
        /// <summary>
        /// Analyzes unlit facility segments to route tactical trauma delivery matrices onto human targets.
        /// </summary>
        public IEnumerator<float> KeterActionLoop()
        {
            while (_isInitialized)
            {
                float offset = SafeRandom.Range(-_plugin.Npc.KeterActionDelayRandomizerValue, _plugin.Npc.KeterActionDelayRandomizerValue);
                float finalDelay = (_plugin.Npc.KeterActionDelay + offset).LimitMin(0.25f);

                yield return Timing.WaitForSeconds(finalDelay);

                if (!_plugin.IsEventActive || !IsBlackoutActive) continue;

                foreach (Player player in Player.ReadyList)
                {
                    if (player?.GameObject is null || !player.IsAlive || !player.IsHuman || player.Room.Name is RoomName.Pocket)
                        continue;

                    try
                    {
                        int playerInstanceId = player.GameObject.GetInstanceID();
                        bool isInDarkness = player.IsInTrueDarkness();

                        if (!isInDarkness || !_sanityHandler.IsValidPlayer(player))
                        {
                            _playerAiStates[playerInstanceId] = NpcBehaviorState.Dormant;
                            continue;
                        }

                        bool hasActiveLight = player.HasActiveLightSource();
                        NpcBehaviorState newState = hasActiveLight ? NpcBehaviorState.Stalking : NpcBehaviorState.Striking;
                        _playerAiStates[playerInstanceId] = newState;

                        _sanityHandler.ApplyDamageToPlayer(player);

                        if (newState is NpcBehaviorState.Stalking)
                        {
                            _lightsourceHandler.ApplyLightsourceEffects(player);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("Methods.KeterAction", $"Trauma pipeline processing failure for target {player.Nickname}: {ex.Message}");
                    }
                }
            }
        }

        public void StartSanityHandlerLoop()
        {
            CoroutineTags.SanityHandler.Kill();
            Timing.RunCoroutine(_sanityHandler.HandleSanityDecay(), CoroutineTags.SanityHandler);
        }

        public void StartBlackoutEventLoop()
        {
            CoroutineTags.BlackoutLoop.Kill();
            Timing.RunCoroutine(RunBlackoutLoop(), CoroutineTags.BlackoutLoop);
        }

        public void StartKeterActionLoop()
        {
            CoroutineTags.ActionLoop.Kill();
            Timing.RunCoroutine(KeterActionLoop(), CoroutineTags.ActionLoop);
        }
        #endregion

        #region Technical Infrastructure
        public bool AreAllGeneratorsEngaged(int req = 3)
        {
            if (Generator.List.Count < req) return false;

            foreach (Generator gen in Generator.List)
            {
                if (!gen.Engaged) return false;
            }
            return true;
        }

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
                    Logger.Info("GeneratorHandler", "SCP-575 permanently terminated via power restoration.");
                }
                else
                {
                    Reset575();
                    Logger.Info("GeneratorHandler", "Grid operational. Anomaly suppressed, tracking preserved.");
                }
            }
            catch (Exception ex)
            {
                Logger.Error("GeneratorHandler.Teardown", $"Post-mortem state transition critical failure handled: {ex.Message}");
            }
        }

        public void ExecuteLocalizedRetaliationSurge(Room generatorRoom)
        {
            if (generatorRoom is null) return;
            Timing.RunCoroutine(ExecuteStabilizationPipelineCoroutine(generatorRoom), CoroutineTags.GeneratorSurge);
        }

        private IEnumerator<float> ExecuteStabilizationPipelineCoroutine(Room generatorRoom)
        {
            yield return Timing.WaitForSeconds(0.11f);

            if (generatorRoom is null || !_plugin.IsEventActive) yield break;

            float stabilizationWindow = _plugin.Blackout.GeneratorStabilizationDuration;

            generatorRoom.TurnOffRoomAndNeighborLights(stabilizationWindow);
            _plugin.AudioDirector?.ProcessGeneratorOverloadRetaliation(generatorRoom.Position);

            yield return Timing.WaitForSeconds(stabilizationWindow);

            if (_plugin.IsEventActive && generatorRoom is not null)
            {
                generatorRoom.TurnOnRoomAndNeighborLights(0.33f);
                _plugin.AudioDirector?.ProcessGeneratorStabilizedFeedback(generatorRoom.Position);

                Logger.Info("Methods.GeneratorStabilize", $"Generator room node [{generatorRoom.Name}] fully stabilized.");
            }
        }

        /// <summary>
        /// Resets structural registries, flushes blackout stacks, and restores lighting zones globally.
        /// </summary>
        public void Reset575()
        {
            _plugin.AudioManager.Clean(fullShutdown: false);

            Interlocked.Exchange(ref _blackoutStacks, 0);

            _plugin.ElevatorHandler?.ClearAllFlickers();
            for (int i = 0; i < AllZones.Length; i++)
            {
                AllZones[i].TurnOnLights();
            }

            lock (_blackoutLock)
            {
                _triggeredZones.Clear();
            }

            ResetTeslaGates();
            _playerAiStates.Clear();
        }

        /// <summary>
        /// Validates if an incoming explosion vector contains specific attributes dangerous to the anomaly.
        /// </summary>
        public bool IsDangerousToScp575(ExplosionType explosionType) => explosionType switch
        {
            ExplosionType.Grenade or ExplosionType.SCP018 or ExplosionType.Jailbird or ExplosionType.Disruptor => true,
            _ => false
        };

        /// <summary>
        /// Shuts down the active instance runtime thread loop maps.
        /// </summary>
        public void Kill575() => Disable();
        #endregion
    }
}