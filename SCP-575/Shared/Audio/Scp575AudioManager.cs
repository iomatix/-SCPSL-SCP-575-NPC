namespace SCP_575.Shared.Audio
{
    using AudioManagerAPI.Defaults;
    using AudioManagerAPI.Features.Enums;
    using AudioManagerAPI.Features.Filters;
    using AudioManagerAPI.Features.Management;
    using LabApi.Features.Wrappers;
    using MEC;
    using SCP_575.Shared.Audio.Enums;
    using SCP_575.Shared;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using UnityEngine;
    using Log = LabApi.Features.Console.Logger;

    /// <summary>
    /// Highly optimized, enterprise-grade audio orchestration engine for SCP-575.
    /// Manages transient vocalizations, 3D isolated paranoia tracks, and procedural environmental soundscapes.
    /// Operates securely on AudioManagerAPI V2.0.0 Session Architecture.
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
            //{ AudioKey.StaticBuzz, new("scp575.static_buzz", 0.37f, 2.5f, 15f, true, AudioPriority.Medium, 0f) }, // TODO: Add static buzz

            { AudioKey.Ambience, new("scp575.ambience", 0.45f, 0f, 999.99f, false, AudioPriority.Low, 0f) },
            { AudioKey.SanityLowDrone, new("scp575.sanity_low_drone", 0.45f, 200.0f, 999.99f, false, AudioPriority.Medium, 0f) },
            { AudioKey.BlackoutImpactGlobal, new("scp575.blackout_impact_global", 0.95f, 0f, 999.99f, false, AudioPriority.High, 13f) },
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="Scp575AudioManager"/> class, anchoring it to the core plugin lifecycle and resolving native audio session bridges.
        /// </summary>
        /// <param name="plugin">The master plugin context used to reference configuration profiles and event states.</param>
        public Scp575AudioManager(Plugin plugin)
        {
            _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin), "Plugin instance cannot be null.");
            _audioEngine = DefaultAudioManager.Instance;
            RegisterAudioResources();
            _ambienceAudioSessionId = 0;
        }

        /// <summary>
        /// Performs emergency resource rehabilitation by tearing down active routine tracking threads and systematically fading out lingering audio sessions to prevent memory or channel allocation leaks.
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

            // FIXED: Clears tracking keys mapping to drone sessions. Since physical sessions 
            // are terminated above, maintaining these keys would block future ambient triggers in the same round.
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
        /// Serves as the central backend pipeline for processing, validating, sanitizing vectors, and executing all spatialized and non-spatialized raw audio tracks.
        /// </summary>
        /// <returns>A unique network audio session handle identifier used for runtime modification, tracking, or early eviction.</returns>
        public int PlayAudioAutoManaged(Player player, AudioKey audioKey, Vector3? position = null, float? lifespan = null, bool hearableForAllPlayers = false, bool queue = false, float fadeInDuration = 0f, bool isNonSpatial = false, bool isTransient = false)
        {
            if (!_audioRegistry.TryGetValue(audioKey, out var config))
                throw new ArgumentException($"Audio key {audioKey} not found in configuration.", nameof(audioKey));

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

            if (isNonSpatial && (audioKey == AudioKey.Scream_1 || audioKey == AudioKey.Scream_2 || audioKey == AudioKey.Scream_3 || audioKey == AudioKey.ScreamAngry || audioKey == AudioKey.ScreamDying))
            {
                double secondsSinceLastScream = (DateTime.UtcNow - _lastGlobalScreamTime).TotalSeconds;
                if (secondsSinceLastScream < _plugin.Config.AudioConfig.GlobalScreamCooldown) return 0;
                _lastGlobalScreamTime = DateTime.UtcNow;
            }

            if (!isNonSpatial && (float.IsNaN(playPosition.x) || float.IsNaN(playPosition.y) || float.IsNaN(playPosition.z) || float.IsInfinity(playPosition.x) || float.IsInfinity(playPosition.y) || float.IsInfinity(playPosition.z)))
            {
                playPosition = Vector3.zero;
            }

            if (audioKey == AudioKey.Ambience && (isNonSpatial || hearableForAllPlayers) && _ambienceAudioSessionId != 0)
            {
                StopAmbience();
            }

            // ===================================================================
            // AAA NETCODE OPTIMIZATION: AREA OF INTEREST (AoI) FILTRATION
            // ===================================================================
            Func<Player, bool> targetPlayerFilter;
            if (!hearableForAllPlayers && player != null)
            {
                targetPlayerFilter = p => p != null && p.UserId == player.UserId;
            }
            else if (hearableForAllPlayers && !isNonSpatial)
            {
                // Explicitly target only remote clients within the physical audio bubble sphere.
                // This drops network packet generation payload up to 90% in populated servers.
                float maxAudibleDistance = config.MaxDistance;
                targetPlayerFilter = p => p != null && p.IsReady && !p.IsHost && Vector3.Distance(p.Position, playPosition) <= maxAudibleDistance;
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

            // ===================================================================
            // FIX: RACE CONDITION SUPPRESSION FOR MICRO-TRANSIENTS
            // ===================================================================
            // If the audio track is marked as transient, we completely skip the manual LifespanCleanupCoroutine.
            // The engine's native 'autoCleanup: true' safely unloads the asset AFTER the track successfully ends.
            if (!isTransient)
            {
                float effectiveLifespan = lifespan ?? config.DefaultLifespan;
                if (effectiveLifespan > 0)
                {
                    Timing.RunCoroutine(LifespanCleanupCoroutine(sessionId, effectiveLifespan, audioKey, isNonSpatial, hearableForAllPlayers), AudioCoroutineTag);
                }
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
                listener: player, // Only the target player hears it
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
                validationCheck: () => true, // A static coordinate in space is always valid
                listener: null, // Null means it's a global spatial sound hearable by anyone nearby
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
        /// <param name="positionProvider">Delegate returning the current 3D vector of the target to orbit.</param>
        /// <param name="validationCheck">Delegate evaluating if the target is still valid (stops coroutine if false).</param>
        /// <param name="listener">The specific player who hears the audio, or null for a global broadcast.</param>
        public void PlayOrbitingAudioCore(Func<Vector3> positionProvider, Func<bool> validationCheck, Player listener, AudioKey audioKey, float? lifespan = null, float maxRadius = 3.2f, float minRadius = 0.6f, float angularSpeed = 1.1f, float approachSpeed = 1.5f, float heightOffset = 0.85f)
        {
            if (positionProvider == null || validationCheck == null || !validationCheck()) return;

            if (!_audioRegistry.TryGetValue(audioKey, out var config))
                throw new ArgumentException($"Audio key {audioKey} not found in configuration system registries.", nameof(audioKey));

            float effectiveLifespan = lifespan ?? config.DefaultLifespan;
            if (effectiveLifespan <= 0f) return;

            bool hearableForAll = listener == null;

            int sessionId = PlayAudioAutoManaged(
                player: listener,
                audioKey: audioKey,
                position: positionProvider(), // Fetches the initial position
                lifespan: effectiveLifespan,
                hearableForAllPlayers: hearableForAll,
                isTransient: false
            );

            if (sessionId == 0) return;

            Timing.RunCoroutine(
                TrackAndOrbitPositionCoroutine(positionProvider, validationCheck, sessionId, effectiveLifespan, maxRadius, minRadius, angularSpeed, approachSpeed, heightOffset),
                AudioCoroutineTag
            );
        }

        /// <summary>
        /// Generalized trigonometric vector update loop utilizing dynamic position providers.
        /// </summary>
        private IEnumerator<float> TrackAndOrbitPositionCoroutine(Func<Vector3> positionProvider, Func<bool> validationCheck, int sessionId, float duration, float maxRadius, float minRadius, float angularSpeed, float approachSpeed, float heightOffset = 0.85f)
        {
            float elapsed = 0f;
            float currentAngle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            float approachPhaseOffset = UnityEngine.Random.Range(0f, Mathf.PI * 2f);

            while (elapsed < duration)
            {
                if (!validationCheck())
                {
                    try
                    {
                        _audioEngine.FadeOutAudio(sessionId, _plugin.Config.AudioConfig.DefaultFadeDuration);
                        _pluginSessionIds.Remove(sessionId);
                    }
                    catch { }
                    yield break;
                }

                Vector3 corePosition = positionProvider();

                float normalizedSine = (Mathf.Sin((elapsed * approachSpeed) + approachPhaseOffset) + 1f) / 2f;
                float currentRadius = Mathf.Lerp(minRadius, maxRadius, normalizedSine);

                float xOffset = Mathf.Cos(currentAngle) * currentRadius;
                float zOffset = Mathf.Sin(currentAngle) * currentRadius;

                Vector3 projectedVector = new Vector3(
                    corePosition.x + xOffset,
                    corePosition.y + heightOffset, // Maintains ear-level projection
                    corePosition.z + zOffset
                );

                try
                {
                    _audioEngine.SetSessionPosition(sessionId, projectedVector);
                }
                catch (Exception)
                {
                    yield break;
                }

                currentAngle += angularSpeed * Timing.DeltaTime;
                elapsed += Timing.DeltaTime;

                yield return Timing.WaitForOneFrame;
            }
        }

        /// <summary>
        /// Broadcasts an environmental non-spatial audio track globally to all active network clients with automated lifetime garbage collection.
        /// </summary>
        /// <returns>A unique audio channel session identifier for global synchronization control.</returns>
        public int PlayGlobalAudioAutoManaged(AudioKey audioKey, float? lifespan = null, bool queue = false, float fadeInDuration = 0f)
        {
            return PlayAudioAutoManaged(null, audioKey, null, lifespan, hearableForAllPlayers: true, queue, fadeInDuration, isNonSpatial: true);
        }

        /// <summary>
        /// Establishes the continuous situational backdrop soundscape, dynamically filtering client audibility based on real-time facility blackout conditions and light levels.
        /// </summary>
        /// <returns>A unique session handle representing the running environmental background pipeline.</returns>
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
        /// Dispatches a spatialized audio cue fixed to a static coordinate vector in the 3D game world, 
        /// protected by a non-blocking debouncer gate tracking the source player.
        /// </summary>
        public void PlayAudioAtPosition(AudioKey key, Vector3 position, float? lifespan = null, bool isTransient = false, Player sourcePlayer = null)
        {
            PlayAudioAutoManaged(sourcePlayer, key, position, lifespan: lifespan, hearableForAllPlayers: true, isNonSpatial: false, isTransient: isTransient);
        }

        /// <summary>
        /// Injects a non-spatial auditory hallucination directly into a specific target player's personal headspace, completely isolating the perception from the rest of the server.
        /// </summary>
        /// <returns>The localized audio tracking ID.</returns>
        public int PlayLocalAudio(Player player, AudioKey audioKey, float? lifespan = null, float fadeInDuration = 0f, bool isTransient = false)
        {
            return PlayAudioAutoManaged(player, audioKey, position: null, lifespan: lifespan, hearableForAllPlayers: false, queue: false, fadeInDuration: fadeInDuration, isNonSpatial: true, isTransient: isTransient);
        }

        /// <summary>
        /// Projects a 3D spatialized audio effect that is strictly isolated to a single target player's perception network, creating a private positional auditory hallucination.
        /// </summary>
        /// <returns>The isolated audio tracking ID.</returns>
        public int PlayIsolatedSpatialAudio(Player player, AudioKey audioKey, Vector3 position, float? lifespan = null, float fadeInDuration = 0f, bool isTransient = false)
        {
            return PlayAudioAutoManaged(player, audioKey, position: position, lifespan: lifespan, hearableForAllPlayers: false, queue: false, fadeInDuration: fadeInDuration, isNonSpatial: false, isTransient: isTransient);
        }

        /// <summary>
        /// Evaluates a pool of auditory paranoia alternatives to select and manifest a random atmospheric psychological trigger for the target player.
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
        /// Orchestrates continuous 2D background ambience state transitions based on target sanity brackets, handling fade-in triggers or session caching per client.
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
        /// Explicit emergency cutoff tracking hook linked to network disconnection lifecycles to instantaneously strip active tracking audio from an exiting client.
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
        /// Scans internal assembly manifests via reflection to locate embedded audio binaries and maps them securely into the audio engine registration table.
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

        private IEnumerator<float> LifespanCleanupCoroutine(int sessionId, float lifespan, AudioKey audioKey, bool isNonSpatial, bool hearableForAllPlayers)
        {
            yield return Timing.WaitForSeconds(lifespan);

            if (audioKey == AudioKey.Ambience && (isNonSpatial || hearableForAllPlayers))
            {
                StopAmbience();
            }
            else
            {
                try
                {
                    // Using the newly discovered native validation check to prevent API exceptions
                    if (_audioEngine.IsValidSession(sessionId))
                    {
                        if (lifespan < 0.5f)
                        {
                            // HARD CUT: Instantly drops the network stream and releases memory.
                            // This stops micro-transients (like the 0.15s/0.05s clicks) from bleeding through.
                            _audioEngine.DestroySession(sessionId);
                        }
                        else
                        {
                            // Standard fade out for long environmental or narrative audio sequences
                            _audioEngine.FadeOutAudio(sessionId, _plugin.Config.AudioConfig.DefaultFadeDuration);
                        }
                    }
                }
                catch { /* Final safety net to protect server frame stability */ }
            }
            _pluginSessionIds.Remove(sessionId);
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