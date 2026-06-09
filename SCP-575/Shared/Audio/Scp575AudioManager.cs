namespace SCP_575.Shared.Audio
{
    using AudioManagerAPI.Defaults;
    using AudioManagerAPI.Features.Enums;
    using AudioManagerAPI.Features.Filters;
    using AudioManagerAPI.Features.Management;
    using AudioManagerAPI.Features.Management.Settings;
    using LabApi.Features.Wrappers;
    using MEC;
    using SCP_575.Shared;
    using SCP_575.Shared.Audio.Enums;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using UnityEngine;
    using UnityEngine.Profiling;
    using Log = LabApi.Features.Console.Logger;

    /// <summary>
    /// Manages audio asset registries and playback tracking sessions for the SCP-575 subsystem.
    /// Delegates spatial routing and global audio processing to the underlying AudioManagerAPI.
    /// </summary>
    public class Scp575AudioManager
    {
        private readonly Plugin _plugin;
        private readonly IAudioManager _audioEngine;
        private DateTime _lastGlobalScreamTime = DateTime.MinValue;

        private int _ambienceAudioSessionId;
        private readonly Dictionary<string, int> _activeDroneSessions = new();
        private readonly Dictionary<string, double> _transientCooldowns = new Dictionary<string, double>();
        private readonly HashSet<int> _generatorSessionIds = new();
        private readonly HashSet<int> _pluginSessionIds = new();

        private const string AudioCoroutineTag = CoroutineTags.AudioCoroutines;

        private readonly Dictionary<AudioKey, AudioTrackProfile> _audioRegistry = new()
        {
            { AudioKey.Scream_1, new("scp575.scream_1", 0.95f, 65f, 150f, true, AudioPriority.High, 9f) },
            { AudioKey.Scream_2, new("scp575.scream_2", 0.95f, 65f, 150f, true, AudioPriority.High, 9f) },
            { AudioKey.Scream_3, new("scp575.scream_3", 0.95f, 65f, 150f, true, AudioPriority.High, 9f) },
            { AudioKey.ScreamAngry, new("scp575.scream_angry", 0.95f, 175f, 450f, true, AudioPriority.High, 9f) },
            { AudioKey.ScreamHurt, new("scp575.scream_hurt", 0.95f, 125f, 345f, true, AudioPriority.High, 7f) },
            { AudioKey.ScreamDying, new("scp575.scream_dying", 0.95f, 255f, 480f, true, AudioPriority.High, 20f) },
            { AudioKey.MonsterRoarGlobal, new("scp575.monster_roar_global", 0.85f, 45f, 999.99f, false, AudioPriority.High, 40f) },

            { AudioKey.Whispers_1, new("scp575.whispers_1", 0.45f, 5f, 35f, true, AudioPriority.Medium, 11f) },
            { AudioKey.Whispers_2, new("scp575.whispers_2", 0.55f, 7f, 45f, true, AudioPriority.Medium, 19f) },
            { AudioKey.Whispers_3, new("scp575.whispers_3", 0.65f, 9f, 52f, true, AudioPriority.Medium, 14f) },
            { AudioKey.WhispersBang, new("scp575.whispers_bang", 0.75f, 12f, 65f, true, AudioPriority.High, 20f) },
            { AudioKey.WhispersMixed, new("scp575.whispers_mixed", 0.75f, 10f, 55f, true, AudioPriority.Medium, 25f) },
            { AudioKey.MonsterBreathLocal, new("scp575.monster_breath_local", 0.8f, 5f, 24f, true, AudioPriority.High, 11f) },
            { AudioKey.ShadowClicking, new("scp575.shadow_clicking", 0.65f, 4.75f, 33f, true, AudioPriority.High, 9f) },

            { AudioKey.ShadowStrike, new("scp575.shadow_strike", 0.95f, 5.5f, 30f, true, AudioPriority.High, 5f) },
            { AudioKey.ShadowConsumingBody, new("scp575.shadow_consuming_body", 0.95f, 7.5f, 45f, true, AudioPriority.High, 5f) },
            { AudioKey.AnomalousImpact, new("scp575.anomalous_impact", 0.95f, 3.5f, 25f, true, AudioPriority.High, 5f) },
            { AudioKey.GeneratorHumDefense, new("scp575.generator_hum_defense", 0.75f, 6.5f, 45f, true, AudioPriority.Medium, 0f) },
            { AudioKey.LightShortCircuit, new("scp575.light_short_circuit", 0.9f, 2.5f, 18f, true, AudioPriority.Max, 1.5f) },

            { AudioKey.Ambience, new("scp575.ambience", 0.45f, 0f, 999.99f, false, AudioPriority.Low, 0f) },
            { AudioKey.SanityLowDrone, new("scp575.sanity_low_drone", 0.45f, 200.0f, 999.99f, false, AudioPriority.Medium, 0f) },
            { AudioKey.BlackoutImpactGlobal, new("scp575.blackout_impact_global", 0.95f, 0f, 999.99f, false, AudioPriority.High, 13f) },
        };

        public Scp575AudioManager(Plugin plugin)
        {
            _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin), "Plugin instance cannot be null.");
            _audioEngine = DefaultAudioManager.Instance;
            RegisterAudioResources();
            _ambienceAudioSessionId = 0;
        }

        public void Clean(bool fullShutdown = false)
        {
            Timing.KillCoroutines(AudioCoroutineTag);
            StopAmbience();

            if (_pluginSessionIds.Count > 0)
            {
                foreach (int sessionId in _pluginSessionIds.ToList())
                {
                    if (sessionId == 0) continue;
                    try { _audioEngine.FadeOutAudio(sessionId, _plugin.Config.AudioConfig.DefaultFadeDuration); } catch { }
                }
                _pluginSessionIds.Clear();
            }

            _activeDroneSessions.Clear();

            if (fullShutdown && _generatorSessionIds.Count > 0)
            {
                foreach (int sessionId in _generatorSessionIds.ToList())
                {
                    if (sessionId == 0) continue;
                    try { _audioEngine.FadeOutAudio(sessionId, _plugin.Config.AudioConfig.DefaultFadeDuration); } catch { }
                }
                _generatorSessionIds.Clear();
            }

            Log.Debug($"[Scp575AudioManager] Clean executed. (FullShutdown: {fullShutdown})");
        }

        public int PlayAudioAutoManaged(Player player, AudioKey audioKey, Vector3? position = null, float? lifespan = null, bool hearableForAllPlayers = false, bool queue = false, float fadeInDuration = 0f, bool isNonSpatial = false, bool isTransient = false, Player sourcePlayer = null, bool loop = false)
        {
            if (!_audioRegistry.TryGetValue(audioKey, out var config))
                throw new ArgumentException($"Audio key {audioKey} not found in configuration.", nameof(audioKey));

            if (isTransient && player != null && !TryAcquireTransientLock(player.UserId, audioKey))
            {
                return 0;
            }

            if (!hearableForAllPlayers && player == null && !isNonSpatial)
                throw new ArgumentNullException(nameof(player), "Player cannot be null when hearableForAllPlayers is false and audio is spatial.");

            Vector3 playPosition = isNonSpatial ? Vector3.zero : SanitizePosition(position ?? player.Position);

            if (isNonSpatial && (audioKey == AudioKey.Scream_1 || audioKey == AudioKey.Scream_2 || audioKey == AudioKey.Scream_3 || audioKey == AudioKey.ScreamAngry || audioKey == AudioKey.ScreamDying))
            {
                double secondsSinceLastScream = (DateTime.UtcNow - _lastGlobalScreamTime).TotalSeconds;
                if (secondsSinceLastScream < _plugin.Config.AudioConfig.GlobalScreamCooldown) return 0;
                _lastGlobalScreamTime = DateTime.UtcNow;
            }

            if (audioKey == AudioKey.Ambience && (isNonSpatial || hearableForAllPlayers) && _ambienceAudioSessionId != 0)
            {
                StopAmbience();
            }

            Func<Player, bool> targetPlayerFilter;
            if (!hearableForAllPlayers && player != null)
            {
                targetPlayerFilter = p => p != null && p.UserId == player.UserId;
            }
            else if (hearableForAllPlayers && !isNonSpatial)
            {
                float maxAudibleDistance = config.MaxDistance;
                string sourceUserId = sourcePlayer?.UserId;

                targetPlayerFilter = p => p != null && p.IsReady && !p.IsHost
                    && (sourceUserId == null || p.UserId != sourceUserId)
                    && Vector3.Distance(p.Position, playPosition) <= maxAudibleDistance;
            }
            else
            {
                targetPlayerFilter = null;
            }

            bool isGeneratorHum = audioKey == AudioKey.GeneratorHumDefense;
            int sessionId;

            if (isNonSpatial)
            {
                sessionId = _audioEngine.PlayGlobalAudio(
                    config.Key, loop: loop, volume: config.Volume, priority: config.Priority,
                    validPlayersFilter: targetPlayerFilter, queue: queue, fadeInDuration: fadeInDuration,
                    lifespan: lifespan, autoCleanup: true);
            }
            else
            {
                bool dynamicLoop = isGeneratorHum || loop;
                sessionId = _audioEngine.PlayAudio(
                    config.Key, playPosition, loop: dynamicLoop, volume: config.Volume,
                    minDistance: config.MinDistance, maxDistance: config.MaxDistance,
                    isSpatial: config.IsSpatial, priority: config.Priority,
                    validPlayersFilter: targetPlayerFilter, queue: queue,
                    fadeInDuration: fadeInDuration, lifespan: lifespan, autoCleanup: true);
            }

            if (sessionId == 0) return 0;

            if (audioKey == AudioKey.Ambience && (isNonSpatial || hearableForAllPlayers))
            {
                _ambienceAudioSessionId = sessionId;
            }

            if (isGeneratorHum) _generatorSessionIds.Add(sessionId);
            else if (!isTransient)
            {
                _pluginSessionIds.Add(sessionId);
            }

            return sessionId;
        }

        /// <summary>
        /// Registers a spatialized audio orbit relative to a moving player target.
        /// </summary>
        public void PlayOrbitingAudio(Player player, AudioKey audioKey, float? lifespan = null, float maxRadius = 3.2f, float minRadius = 0.6f, float angularSpeed = 1.1f, float approachSpeed = 1.5f)
        {
            PlayOrbitingAudioCore(
                positionProvider: () => player.Position,
                validationCheck: () => player != null && player.IsAlive,
                listener: player,
                audioKey: audioKey,
                lifespan: lifespan,
                maxRadius: maxRadius,
                minRadius: minRadius,
                angularSpeed: angularSpeed,
                approachSpeed: approachSpeed
            );
        }

        /// <summary>
        /// Registers a spatialized audio orbit around fixed static world coordinates.
        /// </summary>
        public void PlayOrbitingAudio(Vector3 staticPosition, AudioKey audioKey, float? lifespan = null, float maxRadius = 3.2f, float minRadius = 0.6f, float angularSpeed = 1.1f, float approachSpeed = 1.5f, float heightOffset = 0.85f)
        {
            PlayOrbitingAudioCore(
                positionProvider: () => staticPosition,
                validationCheck: () => true,
                listener: null,
                audioKey: audioKey,
                lifespan: lifespan,
                maxRadius: maxRadius,
                minRadius: minRadius,
                angularSpeed: angularSpeed,
                approachSpeed: approachSpeed,
                heightOffset: heightOffset
            );
        }

        /// <summary>
        /// Core operational routine for injecting real-time orbiting vector calculations into the audio engine.
        /// </summary>
        public void PlayOrbitingAudioCore(Func<Vector3> positionProvider, Func<bool> validationCheck, Player listener, AudioKey audioKey, float? lifespan = null, float maxRadius = 3.2f, float minRadius = 0.6f, float angularSpeed = 1.1f, float approachSpeed = 1.5f, float heightOffset = 0.85f)
        {
            if (positionProvider == null || validationCheck == null || !validationCheck()) return;

            if (!_audioRegistry.TryGetValue(audioKey, out var profile))
                throw new ArgumentException($"Audio key {audioKey} not found in configuration system registries.", nameof(audioKey));

            float effectiveLifespan = lifespan ?? profile.DefaultLifespan;
            if (effectiveLifespan <= 0f) return;

            OrbitSettings dynamicMovementConfiguration = new OrbitSettings(
                maxRadius: maxRadius,
                minRadius: minRadius,
                angularSpeed: angularSpeed,
                approachSpeed: approachSpeed,
                heightOffset: heightOffset
            );

            int sessionId = _audioEngine.PlayOrbitingAudio(
                key: profile.Key,
                positionProvider: positionProvider,
                validationCheck: validationCheck,
                volume: profile.Volume,
                minDistance: profile.MinDistance,
                maxDistance: profile.MaxDistance,
                orbitSettings: dynamicMovementConfiguration,
                priority: profile.Priority,
                lifespan: effectiveLifespan,
                targetPlayerFilter: listener == null ? null : p => p != null && p.UserId == listener.UserId
            );

            if (sessionId != 0)
            {
                _pluginSessionIds.Add(sessionId);
            }
        }

        /// <summary>
        /// Binds a dynamic sound instance to trace player transform vectors.
        /// </summary>
        public void PlayTrackingAudio(Player player, AudioKey audioKey, float? lifespan = null, bool hearableForAllPlayers = true, Vector3? customOffset = null)
        {
            if (player == null || !player.IsReady) return;

            if (!_audioRegistry.TryGetValue(audioKey, out var profile))
                throw new ArgumentException($"Audio key {audioKey} not found in configuration registries.", nameof(audioKey));

            float effectiveLifespan = lifespan ?? profile.DefaultLifespan;
            if (effectiveLifespan <= 0f) return;

            Func<Vector3> locationProvider = () =>
            {
                if (player == null || player.GameObject == null) return Vector3.zero;
                if (customOffset.HasValue) return player.Position + customOffset.Value;

                Transform transformTarget = player.GameObject.transform;
                return player.Position + (transformTarget.up * 1.65f) + (transformTarget.forward * 0.001f);
            };

            int sessionId = _audioEngine.PlayTrackingAudio(
                key: profile.Key,
                positionProvider: locationProvider,
                validationCheck: () => player != null && player.IsReady && player.IsAlive,
                priority: profile.Priority,
                lifespan: effectiveLifespan,
                targetPlayerFilter: hearableForAllPlayers ? null : p => p != null && p.UserId == player.UserId,
                volume: profile.Volume,
                minDistance: profile.MinDistance,
                maxDistance: profile.MaxDistance
            );

            if (sessionId != 0)
            {
                _pluginSessionIds.Add(sessionId);
            }
        }

        public int PlayGlobalAudioAutoManaged(AudioKey audioKey, float? lifespan = null, bool queue = false, float fadeInDuration = 0f)
        {
            return PlayAudioAutoManaged(null, audioKey, null, lifespan, hearableForAllPlayers: true, queue, fadeInDuration, isNonSpatial: true);
        }

        /// <summary>
        /// Registers a persistent global ambient track that dynamically streams only to
        /// players currently fully submerged in dark rooms while the event lifecycle is active.
        /// </summary>
        public int PlayAmbience(bool loop = true, float? lifespan = null, float fadeInDuration = 0f, bool queue = false)
        {
            var config = _audioRegistry[AudioKey.Ambience];
            if (_ambienceAudioSessionId != 0) StopAmbience();

            // Real-time predicate filter evaluated by the underlying audio engine per-frame/per-player
            Func<Player, bool> darkroomFilter = p => p != null
                && p.IsReady
                && !p.IsHost
                && _plugin.IsEventActive
                && _libraryLabAPI.IsPlayerInDarkRoom(p);

            int sessionId = _audioEngine.PlayGlobalAudio(
                key: config.Key, loop: loop, volume: config.Volume, priority: config.Priority,
                validPlayersFilter: darkroomFilter, queue: queue, fadeInDuration: fadeInDuration,
                persistent: true, lifespan: null, autoCleanup: true);

            if (sessionId == 0) return 0;
            _ambienceAudioSessionId = sessionId;

            if (!loop && lifespan.HasValue)
            {
                if (lifespan.Value <= 0) { StopAmbience(); return 0; }
                var coroutine = Timing.CallDelayed(lifespan.Value, StopAmbience);
                coroutine.Tag = AudioCoroutineTag;
            }

            return sessionId;
        }

        public void StopAmbience()
        {
            if (_ambienceAudioSessionId != 0)
            {
                _audioEngine.FadeOutAudio(_ambienceAudioSessionId, _plugin.Config.AudioConfig.DefaultFadeDuration);
                _ambienceAudioSessionId = 0;
            }
        }

        public void SkipAudio(int sessionId, int count) => _audioEngine.SkipAudio(sessionId, count);

        public void PlayAudioAtPosition(AudioKey key, Vector3 position, float? lifespan = null, bool isTransient = false, Player sourcePlayer = null)
        {
            if (!_audioRegistry.TryGetValue(key, out var profile))
            {
                Log.Error($"[Scp575AudioManager] Failed to route smart spatial asset. Key '{key}' is unassigned.");
                return;
            }

            if (isTransient && sourcePlayer != null && !TryAcquireTransientLock(sourcePlayer.UserId, key))
            {
                return;
            }

            Vector3 targetPosition = SanitizePosition(position);

            var sessions = _audioEngine.PlaySpatialSmart(
                key: profile.Key,
                position: targetPosition,
                sourcePlayer: sourcePlayer,
                priority: profile.Priority,
                lifespan: lifespan ?? profile.DefaultLifespan,
                volume: profile.Volume,
                minDistance: profile.MinDistance,
                maxDistance: profile.MaxDistance
            );

            if (sessions.worldSessionId != 0) _pluginSessionIds.Add(sessions.worldSessionId);
            if (sessions.sourceSessionId != 0) _pluginSessionIds.Add(sessions.sourceSessionId);
        }

        public int PlayLocalAudio(Player player, AudioKey audioKey, float? lifespan = null, float fadeInDuration = 0f, bool isTransient = false, bool loop = false)
        {
            return PlayAudioAutoManaged(player, audioKey, position: null, lifespan: lifespan, hearableForAllPlayers: false, queue: false, fadeInDuration: fadeInDuration, isNonSpatial: true, isTransient: isTransient, loop: loop);
        }

        public int PlayIsolatedSpatialAudio(Player player, AudioKey audioKey, Vector3 position, float? lifespan = null, float fadeInDuration = 0f, bool isTransient = false)
        {
            return PlayAudioAutoManaged(player, audioKey, position: position, lifespan: lifespan, hearableForAllPlayers: false, queue: false, fadeInDuration: fadeInDuration, isNonSpatial: false, isTransient: isTransient);
        }

        public void PlayRandomAudioEffect(Player player, params AudioKey[] options)
        {
            if (options == null || options.Length == 0)
            {
                options = new[] { AudioKey.WhispersMixed, AudioKey.Whispers_1, AudioKey.Whispers_2, AudioKey.ShadowClicking };
            }
            var selected = options[UnityEngine.Random.Range(0, options.Length)];
            PlayAudioAutoManaged(player, selected, hearableForAllPlayers: false, lifespan: null);
        }

        public void PlayAggressiveAudio(Player player)
        {
            PlayCommonAudio(player, new[] { AudioKey.AnomalousImpact }, 0.15f);
            PlayCommonAudio(player, new[] { AudioKey.ShadowStrike }, 0.10f);

            AudioKey[] aggressivePool = { AudioKey.Scream_1, AudioKey.Scream_2, AudioKey.Scream_3, AudioKey.ScreamAngry };
            PlayCommonAudio(player, aggressivePool, 0.10f, orbit: true);
            PlayCommonAudio(player, new[] { AudioKey.ShadowClicking }, 0.05f);
        }

        public void PlayDefensiveAudio(Player player)
        {
            PlayCommonAudio(player, new[] { AudioKey.AnomalousImpact }, 0.10f);
            PlayCommonAudio(player, new[] { AudioKey.ShadowStrike }, 0.05f);

            AudioKey[] defensivePool = { AudioKey.Whispers_1, AudioKey.Whispers_2, AudioKey.WhispersBang };
            PlayCommonAudio(player, defensivePool, 0.10f, orbit: true);
            PlayCommonAudio(player, new[] { AudioKey.ShadowClicking }, 0.10f);
        }

        public void PlayCommonAudio(Player player, AudioKey[] pool, float chance, bool orbit = false)
        {
            if (UnityEngine.Random.value > chance)
                return;

            AudioKey selected = pool.Length == 1
                ? pool[0]
                : pool[UnityEngine.Random.Range(0, pool.Length)];

            if (orbit)
            {
                PlayOrbitingAudio(player, selected);
            }
            else
            {
                PlayAudioAtPosition(selected, player.Position, isTransient: true);
            }
        }

        public bool UpdatePlayerBackgroundAmbient(Player player, bool shouldPlayDrone)
        {
            if (player == null || string.IsNullOrEmpty(player.UserId)) return false;

            string userId = player.UserId.ToLowerInvariant();

            if (shouldPlayDrone)
            {
                if (_activeDroneSessions.ContainsKey(userId)) return true;

                int sessionId = PlayLocalAudio(player, AudioKey.SanityLowDrone, lifespan: null, fadeInDuration: 2.0f, isTransient: false, loop: true);
                if (sessionId != 0)
                {
                    _activeDroneSessions[userId] = sessionId;
                    _pluginSessionIds.Add(sessionId);
                    return true;
                }
                return false;
            }
            else
            {
                if (!_activeDroneSessions.TryGetValue(userId, out int sessionId)) return true;

                if (sessionId != 0)
                {
                    _audioEngine.FadeOutAudio(sessionId, _plugin.Config.AudioConfig.DefaultFadeDuration);
                    _pluginSessionIds.Remove(sessionId);
                }
                _activeDroneSessions.Remove(userId);
                return true;
            }
        }

        public void ForceStopAllPlayerAudio(Player player)
        {
            if (player == null || string.IsNullOrEmpty(player.UserId)) return;

            string userId = player.UserId.ToLowerInvariant();
            if (_activeDroneSessions.TryGetValue(userId, out int sessionId))
            {
                if (sessionId != 0)
                {
                    _audioEngine.FadeOutAudio(sessionId, _plugin.Config.AudioConfig.DefaultFadeDuration);
                    _pluginSessionIds.Remove(sessionId);
                }
                _activeDroneSessions.Remove(userId);
            }
        }

        #region Helper Private Methods (DRY & Robustness)

        /// <summary>
        /// Validates and evaluates transient audio key cooldown tracking constraints.
        /// </summary>
        private bool TryAcquireTransientLock(string userId, AudioKey key)
        {
            string debounceKey = userId + "_" + (int)key;
            double currentTime = Timing.LocalTime;

            if (_transientCooldowns.TryGetValue(debounceKey, out double nextAllowedTime) && currentTime < nextAllowedTime)
            {
                return false;
            }

            _transientCooldowns[debounceKey] = currentTime + 0.090;
            return true;
        }

        /// <summary>
        /// Validates coordinate boundaries to block invalid vector mutations within Unity's audio layers.
        /// </summary>
        private Vector3 SanitizePosition(Vector3 position)
        {
            if (float.IsNaN(position.x) || float.IsNaN(position.y) || float.IsNaN(position.z) ||
                float.IsInfinity(position.x) || float.IsInfinity(position.y) || float.IsInfinity(position.z))
            {
                return Vector3.zero;
            }
            return position;
        }

        private void RegisterAudioResources()
        {
            var assembly = Assembly.GetExecutingAssembly();
            string[] allResourceNames = assembly.GetManifestResourceNames();

            foreach (var pair in _audioRegistry)
            {
                string key = pair.Value.Key;
                string resourceName = allResourceNames.FirstOrDefault(r =>
                    r.EndsWith($"{key}.wav", StringComparison.OrdinalIgnoreCase) ||
                    r.EndsWith($"{key.Replace(".", "_")}.wav", StringComparison.OrdinalIgnoreCase));

                if (string.IsNullOrEmpty(resourceName)) continue;

                _audioEngine.RegisterAudio(key, () => assembly.GetManifestResourceStream(resourceName));
            }
        }

        #endregion

        private sealed class AudioTrackProfile
        {
            public string Key { get; }
            public float Volume { get; }
            public float MinDistance { get; }
            public float MaxDistance { get; }
            public bool IsSpatial { get; }
            public AudioPriority Priority { get; }
            public float DefaultLifespan { get; }

            public AudioTrackProfile(string key, float volume, float minDistance, float maxDistance, bool isSpatial, AudioPriority priority, float defaultLifespan)
            {
                Key = key; Volume = volume; MinDistance = minDistance; MaxDistance = maxDistance; IsSpatial = isSpatial; Priority = priority; DefaultLifespan = defaultLifespan;
            }
        }
    }
}