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
    /// Tracks individual player stress profiles, accumulates tension based on sanity decay,
    /// and triggers threshold-bound auditory scares to eliminate predictable pattern recognition.
    /// </summary>
    public class Scp575AudioDirector : IDisposable
    {
        private readonly Plugin _plugin;
        private readonly Scp575AudioManager _audioManager;
        private readonly PlayerSanityHandler _sanityHandler;

        private readonly Dictionary<string, PlayerTensionProfile> _tensionCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, DateTime> _acousticSuppressionCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, DateTime> _lastCombatAudioTime = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> _activePanicDroneSessions = new(StringComparer.OrdinalIgnoreCase);

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

                foreach (var player in Player.ReadyList)
                {
                    // --- PHASE 4: SESSION AUDITOR FAIL-SAFE ---
                    if (!_sanityHandler.IsValidPlayer(player))
                    {
                        ClearHistoricalPlayerCaches(player.UserId);
                        continue;
                    }

                    float currentSanity = _sanityHandler.GetCurrentSanity(player);
                    bool isInDarkness = _plugin.LibraryLabAPI.IsPlayerInDarkRoom(player);

                    EvaluatePersistentPanicDrone(player, currentSanity, isInDarkness);

                    if (IsAcousticBudgetSaturated(player, now))
                        continue;

                    if (isInDarkness)
                    {
                        ProcessPlayerStressTick(player, now);
                    }
                    else
                    {
                        DecayPlayerStressPassive(player);
                    }
                }
            }
        }

        public void OnPlayerLeft(string userId)
        {
            if (string.IsNullOrEmpty(userId)) return;
            ClearHistoricalPlayerCaches(userId);
        }

        private void ProcessPlayerStressTick(Player player, DateTime now)
        {
            string userId = player.UserId;
            float currentSanity = _sanityHandler.GetCurrentSanity(player);
            var config = _plugin.Config.AudioConfig;

            lock (_directorLock)
            {
                if (!_tensionCache.TryGetValue(userId, out var profile))
                {
                    profile = new PlayerTensionProfile();
                    _tensionCache[userId] = profile;
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

        /// <summary>
        /// Slowly drains tension when the player enters secure, illuminated zones to reward defensive playstyles.
        /// </summary>
        private void DecayPlayerStressPassive(Player player)
        {
            lock (_directorLock)
            {
                if (_tensionCache.TryGetValue(player.UserId, out var profile))
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
                _audioManager.PlayAttached(player, scareKey, hearableForAll: false, isTransient: true);
            }
        }

        private bool IsAcousticBudgetSaturated(Player player, DateTime now)
        {
            lock (_directorLock)
            {
                if (_acousticSuppressionCache.TryGetValue(player.UserId, out var expiryTime))
                {
                    return now < expiryTime;
                }
            }
            return false;
        }

        public void SuppressPsychologicalAudio(Player player, float durationSeconds)
        {
            if (player == null || string.IsNullOrEmpty(player.UserId)) return;

            lock (_directorLock)
            {
                _acousticSuppressionCache[player.UserId] = DateTime.Now.AddSeconds(durationSeconds);
            }

            LibraryLabAPI.LogDebug("AudioDirector.Budget", $"Acoustic suppression registered for {player.Nickname} for {durationSeconds}s.");
        }

        public void SuppressPsychologicalAudioInRadius(Vector3 position, float radiusMeter, float durationSeconds)
        {
            DateTime expiry = DateTime.Now.AddSeconds(durationSeconds);

            lock (_directorLock)
            {
                foreach (var player in Player.ReadyList)
                {
                    if (player == null || !player.IsReady || player.IsHost || !player.IsAlive)
                        continue;

                    if (Vector3.Distance(player.Position, position) <= radiusMeter)
                    {
                        _acousticSuppressionCache[player.UserId] = expiry;
                    }
                }
            }

            LibraryLabAPI.LogDebug("AudioDirector.Budget", $"Spatial acoustic suppression applied inside {radiusMeter}m radius for {durationSeconds}s.");
        }

        public void ProcessExplosionImpact(Vector3 position, ScpProjectileImpactType.ProjectileImpactType impactType, bool isBlackoutActive)
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
                _audioManager.PlayOrbitingAudio(
                    staticPosition: position,
                    audioKey: AudioKey.ScreamAngry,
                    maxRadius: config.GeneratorMaxRadius,
                    minRadius: config.GeneratorMinRadius,
                    angularSpeed: config.GeneratorAngularSpeed,
                    approachSpeed: config.GeneratorApproachSpeed
                );
            }
            else
            {
                _audioManager.PlayAtPosition(AudioKey.ScreamStandard, position);
            }
        }

        public void ProcessLightsourceFlicker(Player player)
        {
            if (player == null || !player.IsReady) return;

            if (IsAcousticBudgetSaturated(player, DateTime.Now))
                return;

            var config = _plugin.Config.AudioConfig;

            if (UnityEngine.Random.value <= 0.25f)
            {
                _audioManager.PlayOrbitingAudio(player, AudioKey.MonsterBreathLocal, isolated: true,
                    maxRadius: config.FlickerBreathMaxRadius, minRadius: config.FlickerBreathMinRadius, angularSpeed: config.FlickerBreathAngularSpeed, approachSpeed: config.FlickerBreathApproachSpeed);
            }

            if (UnityEngine.Random.value <= 0.15f)
            {
                _audioManager.PlayOrbitingAudio(player, AudioKey.ShadowClicking, isolated: true,
                    maxRadius: config.FlickerClickingMaxRadius, minRadius: config.FlickerClickingMinRadius, angularSpeed: config.FlickerClickingAngularSpeed, approachSpeed: config.FlickerClickingApproachSpeed);
            }
        }

        public void ProcessAnomalousCombatStinger(Player player, bool isVulnerable)
        {
            if (player == null || string.IsNullOrEmpty(player.UserId)) return;
            string userId = player.UserId;
            var config = _plugin.Config.AudioConfig;

            lock (_directorLock)
            {
                if (_lastCombatAudioTime.TryGetValue(userId, out var lastCombatAudio) && (DateTime.Now - lastCombatAudio).TotalSeconds < config.CombatStingerCooldown)
                {
                    return;
                }
                _lastCombatAudioTime[userId] = DateTime.Now;
            }

            if (isVulnerable)
            {
                _audioManager.PlayAggressiveAudio(player);
            }
            else
            {
                _audioManager.PlayDefensiveAudio(player);
            }
        }

        public void ProcessRagdollConsumption(Vector3 position)
        {
            var config = _plugin.Config.AudioConfig;
            _audioManager.PlayAtPosition(AudioKey.ShadowConsumingBody, position);

            _audioManager.PlayOrbitingAudio(
                staticPosition: position,
                audioKey: AudioKey.ShadowClicking,
                maxRadius: config.RagdollMaxRadius,
                minRadius: config.RagdollMinRadius,
                angularSpeed: config.RagdollAngularSpeed,
                approachSpeed: config.RagdollApproachSpeed,
                heightOffset: config.RagdollHeightOffset
            );
        }

        private void EvaluatePersistentPanicDrone(Player player, float currentSanity, bool isInDarkness)
        {
            string userId = player.UserId;
            var config = _plugin.Config.AudioConfig;
            bool triggersPanicZone = currentSanity <= config.PanicDroneSanityThreshold && isInDarkness;

            lock (_directorLock)
            {
                if (triggersPanicZone)
                {
                    if (_activePanicDroneSessions.ContainsKey(userId))
                        return;

                    int sessionId = _audioManager.PlayAttached(player, AudioKey.WhispersPanicDrone,
                        hearableForAll: false,
                        fadeInDuration: config.PanicDroneFadeInDuration,
                        loop: true);

                    if (sessionId != 0)
                    {
                        _activePanicDroneSessions[userId] = sessionId;
                        LibraryLabAPI.LogDebug("AudioDirector.Panic", $"Psychotic break checkpoint reached for {player.Nickname}. Persistent panic drone activated.");
                    }
                }
                else
                {
                    if (!_activePanicDroneSessions.TryGetValue(userId, out int sessionId))
                        return;

                    _audioManager.ForceStopAllPlayerAudio(player);
                    _activePanicDroneSessions.Remove(userId);

                    LibraryLabAPI.LogDebug("AudioDirector.Panic", $"Sanity stabilized or safe zone reached for {player.Nickname}. Dissolving panic drone.");
                }
            }
        }

        private void ClearHistoricalPlayerCaches(string userId)
        {
            lock (_directorLock)
            {
                _tensionCache.Remove(userId);
                _acousticSuppressionCache.Remove(userId);
                _lastCombatAudioTime.Remove(userId);

                if (_activePanicDroneSessions.TryGetValue(userId, out int sessionId))
                {
                    _activePanicDroneSessions.Remove(userId);
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

                foreach (int sessionId in _activePanicDroneSessions.Values)
                {
                    if (sessionId != 0)
                        _audioManager.ForceStopAllPlayerAudio(Player.Get(sessionId));
                }
                _activePanicDroneSessions.Clear();
            }
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            Clean();
            _isDisposed = true;
        }

        #region Internal Data Structures

        private sealed class PlayerTensionProfile
        {
            private static readonly System.Random _random = new();

            public float CurrentTension { get; set; }
            public float NextTriggerThreshold { get; private set; }

            public PlayerTensionProfile()
            {
                // Core bootstrap default fallback range
                CurrentTension = 0f;
                NextTriggerThreshold = (float)(_random.NextDouble() * (85.0 - 45.0) + 45.0);
            }

            public void ResetCurve(AudioConfig config)
            {
                CurrentTension = 0f;
                float min = config.TensionTriggerMinThreshold;
                float max = config.TensionTriggerMaxThreshold;

                NextTriggerThreshold = (float)(_random.NextDouble() * (max - min) + min);
            }
        }

        #endregion
    }
}