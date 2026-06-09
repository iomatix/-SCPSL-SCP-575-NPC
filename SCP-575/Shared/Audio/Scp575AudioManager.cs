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
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using UnityEngine;
    using Log = LabApi.Features.Console.Logger;

    /// <summary>
    /// Highly optimized acoustic orchestration engine for SCP-575.
    /// Acts as a lightweight profile registry that delegates structural space transformations 
    /// and tracking loops directly to the underlying AudioManagerAPI V2.3.2 subsystem.
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

        /// <summary>
        /// Initializes a new instance of the <see cref="Scp575AudioManager"/> class, anchoring it to the core plugin lifecycle.
        /// </summary>
        /// <param name="plugin">The master plugin context used to reference configuration profiles.</param>
        public Scp575AudioManager(Plugin plugin)
        {
            _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin), "Plugin instance cannot be null.");
            _audioEngine = DefaultAudioManager.Instance;
            RegisterAudioResources();
            _ambienceAudioSessionId = 0;
        }

        /// <summary>
        /// Performs emergency resource rehabilitation by tearing down active routine tracking threads and systematically fading out lingering audio sessions.
        /// </summary>
        /// <param name="fullShutdown">If set to <c>true</c>, cleans up infrastructure audio segments like functional generator hums alongside standard effects.</param>
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

        /// <summary>
        /// Serves as the fallback managed backend pipeline for processing, validating, and executing raw audio tracks.
        /// </summary>
        /// <returns>A unique network audio session handle identifier used for runtime modification.</returns>
        public int PlayAudioAutoManaged(Player player, AudioKey audioKey, Vector3? position = null, float? lifespan = null, bool hearableForAllPlayers = false, bool queue = false, float fadeInDuration = 0f, bool isNonSpatial = false, bool isTransient = false, Player sourcePlayer = null)
        {
            if (!_audioRegistry.TryGetValue(audioKey, out var config))
                throw new ArgumentException($"Audio key {audioKey} not found in configuration.", nameof(audioKey));

            // Guard against virtual audio pipeline channel starvation if asynchronous events are spammed.
            if (isTransient && player != null)
            {
                string debounceKey = player.UserId + "_" + (int)audioKey;
                double currentTime = Timing.LocalTime;

                if (_transientCooldowns.TryGetValue(debounceKey, out double nextAllowedTime) && currentTime < nextAllowedTime)
                {
                    return 0;
                }

                _transientCooldowns[debounceKey] = currentTime + 0.090;
            }

            if (!hearableForAllPlayers && player == null && !isNonSpatial)
                throw new ArgumentNullException(nameof(player), "Player cannot be null when hearableForAllPlayers is false and audio is spatial.");

            Vector3 playPosition = isNonSpatial ? Vector3.zero : (position ?? player.Position);

            // Enforce strategic pacing limitations on apocalyptic spatial global vocalizations.
            if (isNonSpatial && (audioKey == AudioKey.Scream_1 || audioKey == AudioKey.Scream_2 || audioKey == AudioKey.Scream_3 || audioKey == AudioKey.ScreamAngry || audioKey == AudioKey.ScreamDying))
            {
                double secondsSinceLastScream = (DateTime.UtcNow - _lastGlobalScreamTime).TotalSeconds;
                if (secondsSinceLastScream < _plugin.Config.AudioConfig.GlobalScreamCooldown) return 0;
                _lastGlobalScreamTime = DateTime.UtcNow;
            }

            // Sanitize coordinate primitives against floating point instabilities before spatial layout attachment.
            if (!isNonSpatial && (float.IsNaN(playPosition.x) || float.IsNaN(playPosition.y) || float.IsNaN(playPosition.z) || float.IsInfinity(playPosition.x) || float.IsInfinity(playPosition.y) || float.IsInfinity(playPosition.z)))
            {
                playPosition = Vector3.zero;
            }

            if (audioKey == AudioKey.Ambience && (isNonSpatial || hearableForAllPlayers) && _ambienceAudioSessionId != 0)
            {
                StopAmbience();
            }

            // Isolate network packet visibility arrays depending on subjective hallucination vs global auditory broadcast states.
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
                    config.Key, loop: false, volume: config.Volume, priority: config.Priority,
                    validPlayersFilter: targetPlayerFilter, queue: queue, fadeInDuration: fadeInDuration,
                    lifespan: lifespan, autoCleanup: true);
            }
            else
            {
                sessionId = _audioEngine.PlayAudio(
                    config.Key, playPosition, loop: isGeneratorHum, volume: config.Volume,
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
        /// Localized hallucination target tracking. Orbits exclusively around a specific player's perception.
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
        /// Static environmental manifestation. Orbits around a fixed geographical coordinate, audible to all nearby entities.
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
        /// Universal acoustic orbit engine. Tracks any arbitrary spatial coordinate provider in real-time.
        /// </summary>
        public void PlayOrbitingAudioCore(Func<Vector3> positionProvider, Func<bool> validationCheck, Player listener, AudioKey audioKey, float? lifespan = null, float maxRadius = 3.2f, float minRadius = 0.6f, float angularSpeed = 1.1f, float approachSpeed = 1.5f, float heightOffset = 0.85f)
        {
            if (positionProvider == null || validationCheck == null || !validationCheck()) return;

            if (!_audioRegistry.TryGetValue(audioKey, out var profile))
                throw new ArgumentException($"Audio key {audioKey} not found in configuration system registries.", nameof(audioKey));

            float effectiveLifespan = lifespan ?? profile.DefaultLifespan;
            if (effectiveLifespan <= 0f) return;

            // Consolidate trigonometric velocity variables into a parameter object to insulate the call stack from API parameter modifications.
            OrbitSettings dynamicMovementConfiguration = new OrbitSettings(
                maxRadius: maxRadius,
                minRadius: minRadius,
                angularSpeed: angularSpeed,
                approachSpeed: approachSpeed,
                heightOffset: heightOffset
            );

            // Shift coordinate wave computations away from the local assembly directly onto the low-level framework driver matrix.
            _audioEngine.PlayOrbitingAudio(
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
        }

        /// <summary>
        /// Attaches a dynamic sound source to a moving target silhouette with frame-perfect tracking vectors.
        /// </summary>
        public void PlayTrackingAudio(Player player, AudioKey audioKey, float? lifespan = null, bool hearableForAllPlayers = true, Vector3? customOffset = null)
        {
            if (player == null || !player.IsReady) return;

            if (!_audioRegistry.TryGetValue(audioKey, out var profile))
                throw new ArgumentException($"Audio key {audioKey} not found in configuration registries.", nameof(audioKey));

            float effectiveLifespan = lifespan ?? profile.DefaultLifespan;
            if (effectiveLifespan <= 0f) return;

            // Maintain spatial tracking within the target's anatomical perception grid height maps without generating heap allocations.
            Func<Vector3> locationProvider = () =>
            {
                if (player == null || player.GameObject == null) return Vector3.zero;
                if (customOffset.HasValue) return player.Position + customOffset.Value;

                Transform transformTarget = player.GameObject.transform;
                return player.Position + (transformTarget.up * 1.65f) + (transformTarget.forward * 0.001f);
            };

            // Offload runtime synchronous coordinate loops entirely to the network virtualization framework.
            _audioEngine.PlayTrackingAudio(
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
        }

        /// <summary>
        /// Broadcasts an environmental non-spatial audio track globally to all active network clients with automated lifetime garbage collection.
        /// </summary>
        public int PlayGlobalAudioAutoManaged(AudioKey audioKey, float? lifespan = null, bool queue = false, float fadeInDuration = 0f)
        {
            return PlayAudioAutoManaged(null, audioKey, null, lifespan, hearableForAllPlayers: true, queue, fadeInDuration, isNonSpatial: true);
        }

        /// <summary>
        /// Establishes the continuous situational backdrop soundscape, dynamically filtering client audibility based on real-time facility blackout conditions.
        /// </summary>
        public int PlayAmbience(bool loop = true, float? lifespan = null, float fadeInDuration = 0f, bool queue = false)
        {
            var config = _audioRegistry[AudioKey.Ambience];
            if (_ambienceAudioSessionId != 0) StopAmbience();

            var isDarkRoomFilter = AudioFilters.IsInRoomWhereLightsAre(false);
            Func<Player, bool> blackoutFilter = p => isDarkRoomFilter(p) && _plugin.Npc.Methods.IsBlackoutActive;

            int sessionId = _audioEngine.PlayGlobalAudio(
                key: config.Key, loop: loop, volume: config.Volume, priority: config.Priority,
                validPlayersFilter: blackoutFilter, queue: queue, fadeInDuration: fadeInDuration,
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

        /// <summary>
        /// Gracefully de-escalates the environmental atmosphere by fading out the global facility ambience engine channel.
        /// </summary>
        public void StopAmbience()
        {
            if (_ambienceAudioSessionId != 0)
            {
                _audioEngine.FadeOutAudio(_ambienceAudioSessionId, _plugin.Config.AudioConfig.DefaultFadeDuration);
                _ambienceAudioSessionId = 0;
            }
        }

        /// <summary>
        /// Forwards the frame buffer playback index of a designated active audio session to skip specific segments of a track.
        /// </summary>
        public void SkipAudio(int sessionId, int count) => _audioEngine.SkipAudio(sessionId, count);

        /// <summary>
        /// Dispatches a high-fidelity spatialized acoustic event with automatic dual-channel configuration 
        /// to ensure latency-free simulation for the actor and immersive propagation for the environment.
        /// </summary>
        public void PlayAudioAtPosition(AudioKey key, Vector3 position, float? lifespan = null, bool isTransient = false, Player sourcePlayer = null)
        {
            if (!_audioRegistry.TryGetValue(key, out var profile))
            {
                Log.Error($"[Scp575AudioManager] Failed to route smart spatial asset. Key '{key}' is unassigned.");
                return;
            }

            // Suppress wave interference mechanics and jitter variations for the local execution agent 
            // while preserving authentic multi-channel attenuation profiles for remote peripheral observers.
            _audioEngine.PlaySpatialSmart(
                key: profile.Key,
                position: position,
                sourcePlayer: sourcePlayer,
                priority: profile.Priority,
                lifespan: lifespan ?? profile.DefaultLifespan,
                volume: profile.Volume,
                minDistance: profile.MinDistance,
                maxDistance: profile.MaxDistance
            );
        }

        /// <summary>
        /// Injects a non-spatial auditory hallucination directly into a specific target player's personal headspace.
        /// </summary>
        public int PlayLocalAudio(Player player, AudioKey audioKey, float? lifespan = null, float fadeInDuration = 0f, bool isTransient = false)
        {
            return PlayAudioAutoManaged(player, audioKey, position: null, lifespan: lifespan, hearableForAllPlayers: false, queue: false, fadeInDuration: fadeInDuration, isNonSpatial: true, isTransient: isTransient);
        }

        /// <summary>
        /// Projects a 3D spatialized audio effect that is strictly isolated to a single target player's perception network.
        /// </summary>
        public int PlayIsolatedSpatialAudio(Player player, AudioKey audioKey, Vector3 position, float? lifespan = null, float fadeInDuration = 0f, bool isTransient = false)
        {
            return PlayAudioAutoManaged(player, audioKey, position: position, lifespan: lifespan, hearableForAllPlayers: false, queue: false, fadeInDuration: fadeInDuration, isNonSpatial: false, isTransient: isTransient);
        }

        /// <summary>
        /// Evaluates a pool of auditory paranoia alternatives to select and manifest a random atmospheric psychological trigger.
        /// </summary>
        public void PlayRandomAudioEffect(Player player, params AudioKey[] options)
        {
            if (options == null || options.Length == 0)
            {
                options = new[] { AudioKey.WhispersMixed, AudioKey.Whispers_1, AudioKey.Whispers_2, AudioKey.ShadowClicking };
            }
            var selected = options[UnityEngine.Random.Range(0, options.Length)];
            PlayAudioAutoManaged(player, selected, hearableForAllPlayers: false, lifespan: null);
        }

        /// <summary>
        /// Orchestrates continuous 2D background ambience state transitions based on target sanity brackets.
        /// </summary>
        public void UpdatePlayerBackgroundAmbient(Player player, bool shouldPlayDrone)
        {
            if (player == null || string.IsNullOrEmpty(player.UserId)) return;

            string userId = player.UserId.ToLowerInvariant();

            if (shouldPlayDrone)
            {
                if (_activeDroneSessions.ContainsKey(userId)) return;

                int sessionId = PlayLocalAudio(player, AudioKey.SanityLowDrone, lifespan: null, fadeInDuration: 2.0f);
                if (sessionId != 0)
                {
                    _activeDroneSessions[userId] = sessionId;
                }
            }
            else
            {
                if (!_activeDroneSessions.TryGetValue(userId, out int sessionId)) return;

                if (sessionId != 0)
                {
                    _audioEngine.FadeOutAudio(sessionId, _plugin.Config.AudioConfig.DefaultFadeDuration);
                    _pluginSessionIds.Remove(sessionId);
                }
                _activeDroneSessions.Remove(userId);
            }
        }

        /// <summary>
        /// Explicit emergency cutoff tracking hook linked to network disconnection lifecycles.
        /// </summary>
        public void ForceStopAllPlayerAudio(Player player)
        {
            if (player == null || string.IsNullOrEmpty(player.UserId)) return;

            string userId = player.UserId.ToLowerInvariant();
            if (_activeDroneSessions.TryGetValue(userId, out int sessionId))
            {
                if (sessionId != 0) _audioEngine.FadeOutAudio(sessionId, _plugin.Config.AudioConfig.DefaultFadeDuration);
                _activeDroneSessions.Remove(userId);
            }
        }

        /// <summary>
        /// Scans internal assembly manifests via reflection to locate embedded audio binaries and maps them securely.
        /// </summary>
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