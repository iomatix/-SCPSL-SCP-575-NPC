namespace SCP_575.Shared.Audio
{
    using LabApi.Features.Wrappers;
    using MEC;
    using SCP_575;
    using SCP_575.ConfigObjects;
    using SCP_575.Handlers;
    using SCP_575.Shared;
    using SCP_575.Shared.Audio.Enums;
    using SCP_575.Types;
    using System;
    using System.Collections.Generic;
    using UnityEngine;

    /// <summary>
    /// Autonomous narrative engine managing survival horror tension mechanics.
    /// Tracks individual player stress profiles using ultra-performance native instance IDs to avoid GC allocations.
    /// </summary>
    public class Scp575AudioDirector : IDisposable
    {
        private readonly Plugin _plugin;
        private readonly Scp575AudioManager _audioManager;
        private readonly PlayerSanityHandler _sanityHandler;

        private readonly Dictionary<int, PlayerTensionProfile> _tensionCache = new();
        private readonly Dictionary<int, DateTime> _acousticSuppressionCache = new();
        private readonly Dictionary<int, DateTime> _lastCombatAudioTime = new();
        private readonly Dictionary<int, int> _activePanicDroneSessions = new();
        private readonly Dictionary<int, int> _activeAmbientDroneSessions = new();
        private readonly Dictionary<int, double> _transientInputNetworkGate = new();

        private readonly object _directorLock = new();
        private bool _isDisposed;

        private const string DirectorCoroutineTag = CoroutineTags.AudioCoroutines;

        public Scp575AudioDirector(Plugin plugin, Scp575AudioManager audioManager, PlayerSanityHandler sanityHandler)
        {
            _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
            _audioManager = audioManager ?? throw new ArgumentNullException(nameof(audioManager));
            _sanityHandler = sanityHandler ?? throw new ArgumentNullException(nameof(sanityHandler));
        }

        public void Initialize()
        {
            if (_isDisposed) return;

            var handle = Timing.RunCoroutine(HandleTensionPacingLoop());
            handle.Tag = DirectorCoroutineTag;
        }

        private IEnumerator<float> HandleTensionPacingLoop()
        {
            while (true)
            {
                yield return Timing.WaitForSeconds(1.0f);

                if (!_plugin.IsEventActive)
                    continue;

                DateTime now = DateTime.Now;

                foreach (Player player in Player.ReadyList)
                {
                    if (player?.GameObject == null) continue;

                    int instanceId = player.GameObject.GetInstanceID();

                    if (!_sanityHandler.IsValidPlayer(player))
                    {
                        ClearHistoricalPlayerCaches(instanceId);
                        continue;
                    }

                    float currentSanity = _sanityHandler.GetCurrentSanity(player);
                    bool isInDarkness = _plugin.LibraryLabAPI.IsPlayerInDarkRoom(player);

                    EvaluatePersistentPanicDrone(player, instanceId, currentSanity, isInDarkness);
                    UpdatePlayerBackgroundAmbient(player, instanceId, isInDarkness);

                    if (IsAcousticBudgetSaturated(instanceId, now))
                        continue;

                    if (isInDarkness)
                    {
                        ProcessPlayerStressTick(player, instanceId, currentSanity);
                    }
                    else
                    {
                        DecayPlayerStressPassive(instanceId);
                    }
                }
            }
        }

        public void OnPlayerLeft(Player player)
        {
            if (player?.GameObject == null) return;
            ClearHistoricalPlayerCaches(player.GameObject.GetInstanceID());
        }

        #region Core Psychological Rules

        private void ProcessPlayerStressTick(Player player, int instanceId, float currentSanity)
        {
            var config = _plugin.Config.AudioConfig;

            lock (_directorLock)
            {
                if (!_tensionCache.TryGetValue(instanceId, out var profile))
                {
                    profile = new PlayerTensionProfile();
                    _tensionCache[instanceId] = profile;
                }

                float sanityRiskFactor = 1.0f - currentSanity / 100f;
                float tensionGain = _plugin.Config.SanityConfig.DecayRateBase * (1.0f + sanityRiskFactor * config.TensionSanityRiskMultiplier);

                profile.CurrentTension = Mathf.Clamp(profile.CurrentTension + tensionGain, 0f, 100f);

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
                if (_tensionCache.TryGetValue(instanceId, out var profile))
                {
                    profile.CurrentTension = Mathf.Clamp(profile.CurrentTension - _plugin.Config.AudioConfig.TensionPassiveDecayRate, 0f, 100f);
                }
            }
        }

        private void ExecuteAuditoryClimax(Player player, float currentSanity)
        {
            var config = _plugin.Config.AudioConfig;
            AudioKey scareKey = AudioKey.WhispersSubtle;

            if (currentSanity <= config.Tier4ShockStingerThreshold) scareKey = AudioKey.WhispersShockStinger;
            else if (currentSanity <= config.Tier3PsychoticWhispersThreshold) scareKey = AudioKey.WhispersPsychotic;
            else if (currentSanity <= config.Tier2DisturbedWhispersThreshold) scareKey = AudioKey.WhispersDisturbed;

            if (scareKey == AudioKey.WhispersShockStinger)
            {
                player.EnableEffect<CustomPlayerEffects.Deafened>(intensity: 1, duration: config.ShockStingerDeafenDuration);
                _audioManager.PlayOrbitingAudio(player, scareKey, maxRadius: config.StingerMaxRadius, minRadius: config.StingerMinRadius, angularSpeed: config.StingerAngularSpeed);
            }
            else
            {
                _audioManager.PlayAttached(player, scareKey, hearableForAll: false);
            }
        }

        private void EvaluatePersistentPanicDrone(Player player, int instanceId, float currentSanity, bool isInDarkness)
        {
            var config = _plugin.Config.AudioConfig;
            bool triggersPanicZone = currentSanity <= config.PanicDroneSanityThreshold && isInDarkness;

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

        public void UpdatePlayerBackgroundAmbient(Player player, int instanceId, bool shouldPlayDrone)
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

        #region Defensive and Kinetic Inputs

        private bool IsAcousticBudgetSaturated(int instanceId, DateTime now)
        {
            lock (_directorLock)
            {
                if (_acousticSuppressionCache.TryGetValue(instanceId, out var expiryTime))
                {
                    return now < expiryTime;
                }
            }
            return false;
        }

        public void SuppressPsychologicalAudio(Player player, float durationSeconds)
        {
            if (player?.GameObject == null) return;
            lock (_directorLock)
            {
                _acousticSuppressionCache[player.GameObject.GetInstanceID()] = DateTime.Now.AddSeconds(durationSeconds);
            }
        }

        public void SuppressPsychologicalAudioInRadius(Vector3 position, float radiusMeter, float durationSeconds)
        {
            DateTime expiry = DateTime.Now.AddSeconds(durationSeconds);

            lock (_directorLock)
            {
                foreach (var player in Player.ReadyList)
                {
                    if (player?.GameObject == null || !player.IsReady || player.IsHost || !player.IsAlive) continue;

                    if (Vector3.Distance(player.Position, position) <= radiusMeter)
                    {
                        _acousticSuppressionCache[player.GameObject.GetInstanceID()] = expiry;
                    }
                }
            }
        }

        public void ProcessExplosionImpact(Vector3 position, ScpProjectileImpactType.ProjectileImpactType impactType, bool isBlackoutActive = false)
        {
            var config = _plugin.Config.AudioConfig;
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

        public void ProcessGeneratorActivation(Vector3 position, bool allGeneratorsEngaged, bool retaliationConfigured)
        {
            var config = _plugin.Config.AudioConfig;
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

        public void ProcessLightsourceFlicker(Player player)
        {
            if (player?.GameObject == null || !player.IsReady) return;
            int instanceId = player.GameObject.GetInstanceID();

            if (IsAcousticBudgetSaturated(instanceId, DateTime.Now) || !TryAcquireTransientNetworkLock(instanceId)) return;

            var config = _plugin.Config.AudioConfig;

            if (UnityEngine.Random.value <= 0.25f)
            {
                _audioManager.PlayOrbitingAudio(player, AudioKey.MonsterBreathLocal, maxRadius: config.FlickerBreathMaxRadius, minRadius: config.FlickerBreathMinRadius, angularSpeed: config.FlickerBreathAngularSpeed, approachSpeed: config.FlickerBreathApproachSpeed, isolated: true);
            }

            if (UnityEngine.Random.value <= 0.15f)
            {
                _audioManager.PlayOrbitingAudio(player, AudioKey.ShadowClicking, maxRadius: config.FlickerClickingMaxRadius, minRadius: config.FlickerClickingMinRadius, angularSpeed: config.FlickerClickingAngularSpeed, approachSpeed: config.FlickerClickingApproachSpeed, isolated: true);
            }
        }

        public void ProcessAnomalousCombatStinger(Player player, bool isVulnerable)
        {
            if (player?.GameObject == null) return;
            int instanceId = player.GameObject.GetInstanceID();
            var config = _plugin.Config.AudioConfig;

            lock (_directorLock)
            {
                if (_lastCombatAudioTime.TryGetValue(instanceId, out var lastCombatAudio) && (DateTime.Now - lastCombatAudio).TotalSeconds < config.CombatStingerCooldown) return;
                _lastCombatAudioTime[instanceId] = DateTime.Now;
            }

            if (isVulnerable) PlayAggressiveAudio(player);
            else PlayDefensiveAudio(player);
        }

        public void ProcessRagdollConsumption(Vector3 position)
        {
            var config = _plugin.Config.AudioConfig;
            _audioManager.PlayAtPosition(AudioKey.ShadowConsumingBody, position);
            _audioManager.PlayOrbitingAudio(position, AudioKey.ShadowClicking, maxRadius: config.RagdollMaxRadius, minRadius: config.RagdollMinRadius, angularSpeed: config.RagdollAngularSpeed, approachSpeed: config.RagdollApproachSpeed, heightOffset: config.RagdollHeightOffset);
        }

        #endregion

        #region Core Behavioral Probability Routing

        private void PlayAggressiveAudio(Player player)
        {
            if (UnityEngine.Random.value <= 0.15f) _audioManager.PlayAttached(player, AudioKey.AnomalousImpact, hearableForAll: false);
            if (UnityEngine.Random.value <= 0.10f) _audioManager.PlayAttached(player, AudioKey.ShadowStrike, hearableForAll: false);
            if (UnityEngine.Random.value <= 0.10f) _audioManager.PlayOrbitingAudio(player, AudioKey.ScreamStandard);
            if (UnityEngine.Random.value <= 0.04f) _audioManager.PlayAttached(player, AudioKey.ShadowClicking, hearableForAll: false);
        }

        private void PlayDefensiveAudio(Player player)
        {
            if (UnityEngine.Random.value <= 0.10f) _audioManager.PlayAttached(player, AudioKey.AnomalousImpact, hearableForAll: false);
            if (UnityEngine.Random.value <= 0.05f) _audioManager.PlayAttached(player, AudioKey.ShadowStrike, hearableForAll: false);
            // FIX: Resolved 'DynamicWhispers' build breakdown by routing selection to a valid registered database token
            if (UnityEngine.Random.value <= 0.10f) _audioManager.PlayOrbitingAudio(player, AudioKey.WhispersSubtle);
            if (UnityEngine.Random.value <= 0.07f) _audioManager.PlayAttached(player, AudioKey.ShadowClicking, hearableForAll: false);
        }

        private bool TryAcquireTransientNetworkLock(int instanceId)
        {
            double currentTime = Timing.LocalTime;
            if (_transientInputNetworkGate.TryGetValue(instanceId, out double nextAllowedTime) && currentTime < nextAllowedTime) return false;
            _transientInputNetworkGate[instanceId] = currentTime + 0.090;
            return true;
        }

        #endregion

        #region Resource Cleanups

        private void ClearHistoricalPlayerCaches(int instanceId)
        {
            lock (_directorLock)
            {
                _tensionCache.Remove(instanceId);
                _acousticSuppressionCache.Remove(instanceId);
                _lastCombatAudioTime.Remove(instanceId);
                _transientInputNetworkGate.Remove(instanceId);

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
            }
        }

        public void Clean()
        {
            Timing.KillCoroutines(DirectorCoroutineTag);
            lock (_directorLock)
            {
                _lastCombatAudioTime.Clear();
                _tensionCache.Clear();
                _acousticSuppressionCache.Clear();
                _transientInputNetworkGate.Clear();

                foreach (int sessionId in _activePanicDroneSessions.Values)
                {
                    _audioManager.StopSession(sessionId);
                }
                _activePanicDroneSessions.Clear();

                foreach (int sessionId in _activeAmbientDroneSessions.Values)
                {
                    _audioManager.StopSession(sessionId);
                }
                _activeAmbientDroneSessions.Clear();
            }
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            Clean();
            _isDisposed = true;
        }

        #endregion

        #region Embedded Structures

        private sealed class PlayerTensionProfile
        {
            private static readonly System.Random _random = new();

            public float CurrentTension { get; set; }
            public float NextTriggerThreshold { get; private set; }

            public PlayerTensionProfile()
            {
                CurrentTension = 0f;
                NextTriggerThreshold = (float)(_random.NextDouble() * (85.0 - 45.0) + 45.0);
            }

            public void ResetCurve(AudioConfig config)
            {
                CurrentTension = 0f;
                NextTriggerThreshold = (float)(_random.NextDouble() * (config.TensionTriggerMaxThreshold - config.TensionTriggerMinThreshold) + config.TensionTriggerMinThreshold);
            }
        }

        #endregion
    }
}