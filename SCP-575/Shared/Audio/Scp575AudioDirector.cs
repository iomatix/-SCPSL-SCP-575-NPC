using LabApi.Extensions;
using LabApi.Extensions.Misc;
using LabApi.Features.Wrappers;
using MEC;
using SCP_575.ConfigObjects;
using SCP_575.Handlers;
using SCP_575.Shared.Audio.Enums;
using SCP_575.Types;
using System;
using System.Collections.Generic;
using UnityEngine;

using Logger = LabApi.Extensions.Misc.iLogger;

namespace SCP_575.Shared.Audio
{
    /// <summary>
    /// Manages survival horror tension audio pacing mapping directly across 3 independent player psychological stress states.
    /// </summary>
    public class Scp575AudioDirector : IDisposable
    {
        #region Fields & Registries
        private readonly Plugin _plugin;
        private readonly Scp575AudioManager _audioManager;
        private readonly PlayerSanityHandler _sanityHandler;

        private readonly Dictionary<AudioKey, DateTime> _audioCooldowns = new();
        private readonly Dictionary<int, PlayerTensionProfile> _tensionCache = new();
        private readonly Dictionary<int, DateTime> _playerLastAttackAudioTime = new();
        private readonly Dictionary<int, DateTime> _acousticSuppressionCache = new();
        private readonly Dictionary<int, DateTime> _lastCombatAudioTime = new();
        private readonly Dictionary<int, DateTime> _transientInputNetworkGate = new();
        private readonly Dictionary<int, int> _activeClimaxWhisperSessions = new();
        private readonly Dictionary<int, int> _activePanicDroneSessions = new();
        private readonly Dictionary<int, int> _activeAmbientDroneSessions = new();

        private readonly object _directorLock = new();
        private bool _isDisposed;

        private const string DirectorCoroutineTag = CoroutineTags.AudioCoroutines;
        #endregion

        #region Constructor
        public Scp575AudioDirector(Plugin plugin, Scp575AudioManager audioManager, PlayerSanityHandler sanityHandler)
        {
            _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
            _audioManager = audioManager ?? throw new ArgumentNullException(nameof(audioManager));
            _sanityHandler = sanityHandler ?? throw new ArgumentNullException(nameof(sanityHandler));
        }
        #endregion

        #region Initialization & Main State Engine
        public void Initialize()
        {
            if (_isDisposed) return;

            CoroutineHandle handle = Timing.RunCoroutine(HandleTensionPacingLoop());
            handle.Tag = DirectorCoroutineTag;
        }

        /// <summary>
        /// Orchestrates real-time state machine transitions evaluating absolute spatial darkness matrices.
        /// </summary>
        private IEnumerator<float> HandleTensionPacingLoop()
        {
            while (true)
            {
                yield return Timing.WaitForSeconds(1.0f);

                if (!_plugin.IsEventActive)
                    continue;

                foreach (Player player in Player.ReadyList)
                {
                    if (player?.GameObject is null) continue;

                    int instanceId = player.GameObject.GetInstanceID();

                    if (!_sanityHandler.IsValidPlayer(player))
                    {
                        _audioManager.StopAmbienceForPlayer(player);
                        ClearHistoricalPlayerCaches(instanceId);
                        continue;
                    }

                    float currentSanity = _sanityHandler.GetCurrentSanity(player);

                    // MASTER-LEVEL ARCHITECTURE ALIGNMENT: 
                    // Querying both independent spatial hooks to separate State 2 (Gray) from State 3 (True Darkness)
                    bool isInTrueDarkness = player.IsInTrueDarkness();
                    bool isInDarkRoom = player.IsInDarkRoom();

                    // =================================================================================
                    // STATE 3: TRUE DARKNESS (Mrok — Sanity falling, full horror orchestration)
                    // =================================================================================
                    if (isInTrueDarkness)
                    {
                        _audioManager.PlayAmbienceForPlayer(player, fadeInDuration: 2.0f);

                        bool triggersPanicZone = currentSanity <= _plugin.Audio.PanicDroneSanityThreshold;
                        float lowSanityThreshold = _plugin.Audio.Tier2DisturbedWhispersThreshold;
                        bool shouldPlayLowSanityDrone = currentSanity <= lowSanityThreshold && !triggersPanicZone;

                        EvaluatePersistentPanicDrone(player, instanceId, currentSanity, true);
                        EvaluateLowSanityDrone(player, instanceId, shouldPlayLowSanityDrone);

                        if (!_acousticSuppressionCache.IsCooldownActive(instanceId))
                        {
                            ProcessPlayerStressTick(player, instanceId, currentSanity);
                        }
                    }
                    // =================================================================================
                    // STATE 2: DARK ROOM ONLY / ELEVATOR NEIGHBOR (Szarość — Safe Zone, creepy environment)
                    // =================================================================================
                    else if (isInDarkRoom)
                    {
                        // Play smoothed out environmental soundscapes, but entirely bypass stress gains
                        _audioManager.PlayAmbienceForPlayer(player, fadeInDuration: 4.0f);

                        // Safely evict and terminate high-intensity combat/panic background drones
                        EvaluatePersistentPanicDrone(player, instanceId, currentSanity, false);
                        EvaluateLowSanityDrone(player, instanceId, false);
                        DecayPlayerStressPassive(instanceId);

                        // Fire a completely harmless, non-lethal background structural audio creep effect
                        ExecuteSubtleEnvironmentCreep(player, instanceId);
                    }
                    // =================================================================================
                    // STATE 1: PURE ILLUMINATION (Jasność — Absolute safety and mental recovery)
                    // =================================================================================
                    else
                    {
                        _audioManager.StopAmbienceForPlayer(player);
                        EvaluatePersistentPanicDrone(player, instanceId, currentSanity, false);
                        EvaluateLowSanityDrone(player, instanceId, false);
                        DecayPlayerStressPassive(instanceId);

                        lock (_directorLock)
                        {
                            if (_activeClimaxWhisperSessions.TryGetValue(instanceId, out int whisperSessionId))
                            {
                                _audioManager.StopSession(whisperSessionId);
                                _activeClimaxWhisperSessions.Remove(instanceId);
                            }
                        }
                    }
                }
            }
        }

        public void OnPlayerLeft(Player player)
        {
            if (player?.GameObject is null) return;
            _audioManager.StopAmbienceForPlayer(player);
            ClearHistoricalPlayerCaches(player.GameObject.GetInstanceID());
        }
        #endregion

        #region Environmental Soundscape Injection (State 2 Primitives)
        /// <summary>
        /// Injects subtle audio feedback loops into the transitional gray zone without triggering mechanical gameplay harms.
        /// </summary>
        private void ExecuteSubtleEnvironmentCreep(Player player, int instanceId)
        {
            // Cooldown protection layer prevents tracking registry overhead or audio channel spamming inside elevators
            if (!_transientInputNetworkGate.TryAcquireLock(instanceId, TimeSpan.FromSeconds(15f))) return;

            // Fluent API Upgrade: Leveraged high-performance thread-isolated SafeRandom loops over procedural checks
            if (SafeRandom.RollSuccess(40f))
            {
                _audioManager.PlayAttached(player, AudioKey.WhispersSubtle, hearableForAll: false);
            }
            else if (SafeRandom.RollSuccess(20f))
            {
                _audioManager.PlayAttached(player, AudioKey.MonsterBreathLocal, hearableForAll: false);
            }
        }
        #endregion

        #region Stress Mechanics (State 3 Exclusives)
        private void ProcessPlayerStressTick(Player player, int instanceId, float currentSanity)
        {
            AudioConfig config = _plugin.Audio;

            lock (_directorLock)
            {
                if (!_tensionCache.TryGetValue(instanceId, out PlayerTensionProfile profile))
                {
                    profile = new PlayerTensionProfile();
                    _tensionCache[instanceId] = profile;
                }

                float sanityRiskFactor = 1.0f - currentSanity / 100f;
                float tensionGain = _plugin.Sanity.DecayRateBase * (1.0f + sanityRiskFactor * config.TensionSanityRiskMultiplier);

                profile.CurrentTension = (profile.CurrentTension + tensionGain).Clamp(0f, 100f);

                if (profile.CurrentTension >= profile.NextTriggerThreshold)
                {
                    ExecuteAuditoryClimax(player, currentSanity);
                    profile.ResetCurve(config);
                }
            }
        }

        private void DecayPlayerStressPassive(int instanceId)
        {
            lock (_directorLock)
            {
                if (_tensionCache.TryGetValue(instanceId, out PlayerTensionProfile profile))
                {
                    profile.CurrentTension = (profile.CurrentTension - _plugin.Audio.TensionPassiveDecayRate).Clamp(0f, 100f);
                }
            }
        }

        private void ExecuteAuditoryClimax(Player player, float currentSanity)
        {
            AudioConfig config = _plugin.Audio;
            AudioKey scareKey = AudioKey.WhispersSubtle;

            if (currentSanity <= config.Tier4ShockStingerThreshold) scareKey = AudioKey.WhispersShockStinger;
            else if (currentSanity <= config.Tier3PsychoticWhispersThreshold) scareKey = AudioKey.WhispersPsychotic;
            else if (currentSanity <= config.Tier2DisturbedWhispersThreshold) scareKey = AudioKey.WhispersDisturbed;

            if (scareKey is AudioKey.WhispersShockStinger)
            {
                player.EnableEffect(FacilityEffectType.Deafened, intensity: 1, duration: config.ShockStingerDeafenDuration);
                _audioManager.PlayOrbitingAudio(player, scareKey, maxRadius: config.StingerMaxRadius, minRadius: config.StingerMinRadius, angularSpeed: config.StingerAngularSpeed);
            }
            else
            {
                int sessionId = _audioManager.PlayAttached(player, scareKey, hearableForAll: false);

                if (player?.GameObject is not null)
                {
                    int instanceId = player.GameObject.GetInstanceID();
                    lock (_directorLock)
                    {
                        if (sessionId != 0) _activeClimaxWhisperSessions[instanceId] = sessionId;
                    }
                }
            }
        }
        #endregion

        #region Drone Management Loops
        private void EvaluatePersistentPanicDrone(Player player, int instanceId, float currentSanity, bool activeInDarkness)
        {
            AudioConfig config = _plugin.Audio;
            bool triggersPanicZone = activeInDarkness && currentSanity <= config.PanicDroneSanityThreshold;

            lock (_directorLock)
            {
                if (triggersPanicZone)
                {
                    if (_activePanicDroneSessions.ContainsKey(instanceId)) return;

                    int sessionId = _audioManager.PlayAttached(player, AudioKey.WhispersPanicDrone, hearableForAll: false, fadeInDuration: config.PanicDroneFadeInDuration, loop: true);
                    if (sessionId != 0) _activePanicDroneSessions[instanceId] = sessionId;
                }
                else
                {
                    if (_activePanicDroneSessions.TryGetValue(instanceId, out int sessionId))
                    {
                        _audioManager.StopSession(sessionId);
                        _activePanicDroneSessions.Remove(instanceId);
                    }
                }
            }
        }

        public void EvaluateLowSanityDrone(Player player, int instanceId, bool shouldPlayDrone)
        {
            lock (_directorLock)
            {
                if (shouldPlayDrone)
                {
                    if (_activeAmbientDroneSessions.ContainsKey(instanceId)) return;

                    int sessionId = _audioManager.PlayAttached(player, AudioKey.SanityLowDrone, hearableForAll: false, fadeInDuration: 2.0f, loop: true);
                    if (sessionId != 0) _activeAmbientDroneSessions[instanceId] = sessionId;
                }
                else
                {
                    if (_activeAmbientDroneSessions.TryGetValue(instanceId, out int sessionId))
                    {
                        _audioManager.StopSession(sessionId);
                        _activeAmbientDroneSessions.Remove(instanceId);
                    }
                }
            }
        }
        #endregion

        #region External Game Telemetry Dispatches
        public void ProcessBlackoutAudioSequence(Player randomTarget)
        {
            if (!_audioCooldowns.TryAcquireLock(AudioKey.MonsterRoarGlobal, TimeSpan.FromSeconds(45f)))
            {
                Logger.Debug("AudioDirector", "Execution blocked: MonsterRoarGlobal channel active.", _plugin.Debug);
                return;
            }

            AudioConfig audioConfig = _plugin.Audio;

            _audioManager.PlayGlobal(AudioKey.BlackoutImpactGlobal);
            _audioManager.PlayGlobal(AudioKey.MonsterRoarGlobal);

            if (randomTarget is not null && randomTarget.IsReady)
            {
                _audioManager.PlayOrbitingAudio(randomTarget, AudioKey.ScreamStandard, isolated: true,
                    maxRadius: audioConfig.BlackoutScreamMaxRadius,
                    minRadius: audioConfig.BlackoutScreamMinRadius,
                    angularSpeed: audioConfig.BlackoutScreamAngularSpeed,
                    approachSpeed: audioConfig.BlackoutScreamApproachSpeed);
            }
        }

        public void SuppressPsychologicalAudio(Player player, float durationSeconds)
        {
            if (player?.GameObject is null) return;

            lock (_directorLock)
            {
                _acousticSuppressionCache.TryAcquireLock(player.GameObject.GetInstanceID(), TimeSpan.FromSeconds(durationSeconds));
            }
        }

        public void SuppressPsychologicalAudioInRadius(Vector3 position, float radiusMeter, float durationSeconds)
        {
            TimeSpan durationSpan = TimeSpan.FromSeconds(durationSeconds);

            lock (_directorLock)
            {
                foreach (Player player in Player.ReadyList)
                {
                    if (player?.GameObject is null || !player.IsReady || player.IsHost || !player.IsAlive) continue;

                    if (player.IsWithinDistance(position, radiusMeter))
                    {
                        _acousticSuppressionCache.TryAcquireLock(player.GameObject.GetInstanceID(), durationSpan);
                    }
                }
            }
        }

        public void ProcessDamagedPlayerHitImpact(Player target)
        {
            int id = target.GameObject.GetInstanceID();

            if (!_playerLastAttackAudioTime.TryGetValue(id, out var lastTime))
                lastTime = DateTime.MinValue;

            TimeSpan cooldown = TimeSpan.FromSeconds(_plugin.Sanity.AttackAudioCooldownSeconds);

            if (DateTime.UtcNow - lastTime >= cooldown)
            {
                _playerLastAttackAudioTime[id] = DateTime.UtcNow;
                _plugin.AudioManager?.PlayAtPosition(AudioKey.AnomalousImpact, target.Position);
            }
        }


        public void ProcessExplosionImpact(Vector3 position, ScpProjectileImpactType.ProjectileImpactType impactType, bool isBlackoutActive = false)
        {
            AudioConfig config = _plugin.Audio;
            SuppressPsychologicalAudioInRadius(position, radiusMeter: config.ExplosionSuppressionRadius, durationSeconds: config.ExplosionSuppressionDuration);
            _audioManager.PlayAtPosition(AudioKey.AnomalousImpact, position);

            switch (impactType)
            {
                case ScpProjectileImpactType.ProjectileImpactType.Helpful:
                    _audioManager.PlayOrbitingAudio(position, AudioKey.ScreamAngry, maxRadius: config.HelpfulExplosionMaxRadius, minRadius: config.HelpfulExplosionMinRadius, angularSpeed: config.HelpfulExplosionAngularSpeed, approachSpeed: config.HelpfulExplosionApproachSpeed);
                    _audioManager.PlayGlobal(AudioKey.WhispersDisturbed);
                    break;
                case ScpProjectileImpactType.ProjectileImpactType.Dangerous:
                    _audioManager.PlayOrbitingAudio(position, AudioKey.ScreamStandard, maxRadius: config.DangerousExplosionMaxRadius, minRadius: config.DangerousExplosionMinRadius, angularSpeed: config.DangerousExplosionAngularSpeed, approachSpeed: config.DangerousExplosionApproachSpeed);
                    break;
                default:
                    _audioManager.PlayAtPosition(AudioKey.WhispersSubtle, position);
                    break;
            }
        }

        public void ProcessExplosionImpactBoostFeedback() => _audioManager.PlayGlobal(AudioKey.MonsterBreathLocal);

        public void ProcessGeneratorActivation(Vector3 position, bool allGeneratorsEngaged, bool retaliationConfigured)
        {
            AudioConfig config = _plugin.Audio;
            _audioManager.PlayAtPosition(AudioKey.GeneratorHumDefense, position, loop: true);

            if (allGeneratorsEngaged)
            {
                _audioManager.PlayAtPosition(AudioKey.ScreamDying, position);
                return;
            }

            if (retaliationConfigured)
            {
                _audioManager.PlayOrbitingAudio(position, AudioKey.ScreamAngry, maxRadius: config.GeneratorMaxRadius, minRadius: config.GeneratorMinRadius, angularSpeed: config.GeneratorAngularSpeed, approachSpeed: config.GeneratorApproachSpeed);
            }
            else
            {
                _audioManager.PlayAtPosition(AudioKey.ScreamStandard, position);
            }
        }

        public void ProcessGeneratorOverloadRetaliation(Vector3 position)
        {
            _audioManager.PlayAtPosition(AudioKey.LightShortCircuit, position);
            _audioManager.PlayAtPosition(AudioKey.ScreamAngry, position);
        }

        public void ProcessGeneratorStabilizedFeedback(Vector3 position) => _audioManager.PlayAtPosition(AudioKey.LightSwitch, position);
        public void ProcessLightSwitchClick(Vector3 position) => _audioManager.PlayAtPosition(AudioKey.LightSwitch, position);
        public void ProcessLightsourceErrorFeedback(Player player) => _audioManager.PlayTrackingAudio(player: player, audioKey: AudioKey.LightShortCircuitFinal, hearableForAllPlayers: true);

        public void ProcessLightsourceFlicker(Player player)
        {
            if (player?.GameObject is null || !player.IsReady) return;
            int instanceId = player.GameObject.GetInstanceID();

            if (_acousticSuppressionCache.IsCooldownActive(instanceId) || !_transientInputNetworkGate.TryAcquireLock(instanceId, TimeSpan.FromMilliseconds(90))) return;

            AudioConfig config = _plugin.Audio;

            if (SafeRandom.Range(0f, 1f) <= 0.25f)
            {
                _audioManager.PlayOrbitingAudio(player, AudioKey.MonsterBreathLocal, maxRadius: config.FlickerBreathMaxRadius, minRadius: config.FlickerBreathMinRadius, angularSpeed: config.FlickerBreathAngularSpeed, approachSpeed: config.FlickerBreathApproachSpeed, isolated: true);
            }

            if (SafeRandom.Range(0f, 1f) <= 0.15f)
            {
                _audioManager.PlayOrbitingAudio(player, AudioKey.ShadowClicking, maxRadius: config.FlickerClickingMaxRadius, minRadius: config.FlickerClickingMinRadius, angularSpeed: config.FlickerClickingAngularSpeed, approachSpeed: config.FlickerClickingApproachSpeed, isolated: true);
            }
        }

        public void ProcessLightsourceSparkFeedback(Player player, bool isFinalBlow)
        {
            if (player?.GameObject is null || !player.IsReady) return;
            int instanceId = player.GameObject.GetInstanceID();

            if (_acousticSuppressionCache.IsCooldownActive(instanceId)) return;

            if (isFinalBlow)
            {
                _audioManager.PlayAtPosition(AudioKey.LightShortCircuitFinal, player.Position, lifespan: 0.35f, isTransient: true, sourcePlayer: player);
            }
            else
            {
                _audioManager.PlayAtPosition(AudioKey.LightShortCircuit, player.Position, lifespan: 0.15f, isTransient: true, sourcePlayer: player);
            }
        }

        public void ProcessAnomalousCombatStinger(Player player, bool isVulnerable)
        {
            if (player?.GameObject is null) return;
            int instanceId = player.GameObject.GetInstanceID();
            AudioConfig config = _plugin.Audio;

            lock (_directorLock)
            {
                if (!_lastCombatAudioTime.TryAcquireLock(instanceId, TimeSpan.FromSeconds(config.CombatStingerCooldown))) return;
            }

            if (isVulnerable) PlayAggressiveAudio(player);
            else PlayDefensiveAudio(player);
        }

        public void ProcessRagdollConsumption(Vector3 position)
        {
            AudioConfig config = _plugin.Audio;
            _audioManager.PlayAtPosition(AudioKey.ShadowConsumingBody, position);
            _audioManager.PlayOrbitingAudio(position, AudioKey.ShadowClicking, maxRadius: config.RagdollMaxRadius, minRadius: config.RagdollMinRadius, angularSpeed: config.RagdollAngularSpeed, approachSpeed: config.RagdollApproachSpeed, heightOffset: config.RagdollHeightOffset);
        }
        #endregion

        #region Probability Profiles Maps
        private void PlayAggressiveAudio(Player player)
        {
            float rand = SafeRandom.Range(0f, 1f);
            if (rand <= 0.10f) _audioManager.PlayAttached(player, AudioKey.ShadowStrike, hearableForAll: false);
            if (rand <= 0.10f) _audioManager.PlayOrbitingAudio(player, AudioKey.ScreamStandard);
            if (rand <= 0.04f) _audioManager.PlayAttached(player, AudioKey.ShadowClicking, hearableForAll: false);
        }

        private void PlayDefensiveAudio(Player player)
        {
            float rand = SafeRandom.Range(0f, 1f);
            if (rand <= 0.05f) _audioManager.PlayAttached(player, AudioKey.ShadowStrike, hearableForAll: false);
            if (rand <= 0.10f) _audioManager.PlayOrbitingAudio(player, AudioKey.WhispersSubtle);
            if (rand <= 0.07f) _audioManager.PlayAttached(player, AudioKey.ShadowClicking, hearableForAll: false);
        }
        #endregion

        #region Cleanup Routines
        private void ClearHistoricalPlayerCaches(int instanceId)
        {
            lock (_directorLock)
            {
                _tensionCache.Remove(instanceId);
                _acousticSuppressionCache.Remove(instanceId);
                _lastCombatAudioTime.Remove(instanceId);
                _transientInputNetworkGate.Remove(instanceId);
                _playerLastAttackAudioTime.Remove(instanceId);

                if (_activePanicDroneSessions.TryGetValue(instanceId, out int panicId))
                {
                    _audioManager.StopSession(panicId);
                    _activePanicDroneSessions.Remove(instanceId);
                }

                if (_activeAmbientDroneSessions.TryGetValue(instanceId, out int ambientId))
                {
                    _audioManager.StopSession(ambientId);
                    _activeAmbientDroneSessions.Remove(instanceId);
                }

                if (_activeClimaxWhisperSessions.TryGetValue(instanceId, out int whisperId))
                {
                    _audioManager.StopSession(whisperId);
                    _activeClimaxWhisperSessions.Remove(instanceId);
                }
            }
        }

        public void Clean()
        {
            DirectorCoroutineTag.KillCoroutine();
            lock (_directorLock)
            {
                _lastCombatAudioTime.Clear();
                _tensionCache.Clear();
                _acousticSuppressionCache.Clear();
                _transientInputNetworkGate.Clear();
                _playerLastAttackAudioTime.Clear();

                foreach (int sessionId in _activePanicDroneSessions.Values) _audioManager.StopSession(sessionId);
                _activePanicDroneSessions.Clear();

                foreach (int sessionId in _activeAmbientDroneSessions.Values) _audioManager.StopSession(sessionId);
                _activeAmbientDroneSessions.Clear();

                foreach (int sessionId in _activeClimaxWhisperSessions.Values) _audioManager.StopSession(sessionId);
                _activeClimaxWhisperSessions.Clear();
            }
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            Clean();
            _isDisposed = true;
        }
        #endregion

        #region Nested Profile Structures
        private sealed class PlayerTensionProfile
        {
            public float CurrentTension { get; set; }
            public float NextTriggerThreshold { get; private set; }

            public PlayerTensionProfile()
            {
                CurrentTension = 0f;
                NextTriggerThreshold = SafeRandom.Range(45.0f, 85.0f);
            }

            public void ResetCurve(AudioConfig config)
            {
                CurrentTension = 0f;
                NextTriggerThreshold = SafeRandom.Range(config.TensionTriggerMinThreshold, config.TensionTriggerMaxThreshold);
            }
        }
        #endregion
    }
}