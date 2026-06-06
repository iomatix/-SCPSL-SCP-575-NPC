namespace SCP_575.Shared.Audio
{
    using AudioManagerAPI.Defaults;
    using AudioManagerAPI.Features.Enums;
    using AudioManagerAPI.Features.Filters;
    using AudioManagerAPI.Features.Management;
    using LabApi.Features.Wrappers;
    using MEC;
    using SCP_575.ConfigObjects;
    using SCP_575.Shared.Audio.Enums;
    using SCP575.Shared;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using UnityEngine;
    using Log = LabApi.Features.Console.Logger;

    /// <summary>
    /// Manages audio playback for SCP-575, including screams, whispers, and ambience, with support for spatial and non-spatial audio.
    /// Operates on AudioManagerAPI V2.0.0 Session Architecture.
    /// </summary>
    public class Scp575AudioManager
    {
        private readonly Plugin _plugin;
        private static IAudioManager sharedAudioManager;
        private DateTime lastGlobalScreamTime = DateTime.MinValue;

        private int _ambienceAudioSessionId;
        private readonly HashSet<int> _pluginSessionIds = new();

        // MEC Tag for all coroutines started by this audio manager, allowing for easy cleanup on round end or plugin disable.
        private const string AudioCoroutineTag = CoroutineTags.AudioCoroutines;

        private readonly Dictionary<AudioKey, (string key, float volume, float minDistance, float maxDistance, bool isSpatial, AudioPriority priority, float defaultLifespan)> audioConfig = new()
        {
            { AudioKey.Scream, ("scp575.scream", 0.85f, 5f, 50f, true, AudioPriority.High, 15f) },
            { AudioKey.ScreamAngry, ("scp575.scream-angry", 0.9f, 5f, 50f, true, AudioPriority.High, 15f) },
            { AudioKey.ScreamDying, ("scp575.scream-dying", 1.0f, 60f, 60f, true, AudioPriority.High, 15f) },
            { AudioKey.Whispers, ("scp575.whispers", 0.95f, 3f, 30f, true, AudioPriority.Medium, 15f) },
            { AudioKey.WhispersBang, ("scp575.whispers-bang", 0.65f, 3f, 30f, true, AudioPriority.Medium, 15f) },
            { AudioKey.WhispersMixed, ("scp575.whispers-mixed", 0.85f, 3f, 30f, true, AudioPriority.Medium, 15f) },
            { AudioKey.Ambience, ("scp575.ambience", 0.5f, 0f, 999.99f, false, AudioPriority.Low, 0f) },
        };

        public Scp575AudioManager(Plugin plugin)
        {
            _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin), "Plugin instance cannot be null.");
            if (_plugin.Config?.AudioConfig == null)
                throw new ArgumentNullException(nameof(_plugin.Config.AudioConfig), "Audio configuration cannot be null.");
            if (_plugin.Config.AudioConfig.GlobalScreamCooldown <= 0)
                throw new ArgumentException("GlobalScreamCooldown must be positive.", nameof(_plugin.Config.AudioConfig));
            if (_plugin.Config.AudioConfig.DefaultFadeDuration < 0)
                throw new ArgumentException("DefaultFadeDuration must be non-negative.", nameof(_plugin.Config.AudioConfig));

            sharedAudioManager = DefaultAudioManager.Instance;
            RegisterAudioResources();

            _ambienceAudioSessionId = 0;
        }

        /// <summary>
        /// Cleans up all active audio coroutines and sessions.
        /// Call this when the round ends or the plugin is disabled.
        /// </summary>
        public void Clean()
        {
            Timing.KillCoroutines(AudioCoroutineTag);
            StopAmbience();
            _pluginSessionIds.Clear();
            Log.Debug("[Scp575AudioManager] Audio manager cleaned up.");
        }

        public int PlayAudioAutoManaged(Player player, AudioKey audioKey, Vector3? position = null, float? lifespan = null, bool hearableForAllPlayers = false, bool queue = false, float fadeInDuration = 0f, bool isNonSpatial = false)
        {
            if (!audioConfig.TryGetValue(audioKey, out var config))
                throw new ArgumentException($"Audio key {audioKey} not found in configuration.", nameof(audioKey));

            if (!hearableForAllPlayers && player == null && !isNonSpatial)
                throw new ArgumentNullException(nameof(player), "Player cannot be null when hearableForAllPlayers is false and audio is spatial.");

            Vector3 playPosition = isNonSpatial ? Vector3.zero : (position ?? player.Position);

            if (isNonSpatial && (audioKey == AudioKey.Scream || audioKey == AudioKey.ScreamAngry || audioKey == AudioKey.ScreamDying))
            {
                double secondsSinceLastScream = (DateTime.UtcNow - lastGlobalScreamTime).TotalSeconds;
                if (secondsSinceLastScream < _plugin.Config.AudioConfig.GlobalScreamCooldown)
                {
                    return 0; // Silently block due to cooldown to avoid log spam
                }
                lastGlobalScreamTime = DateTime.UtcNow;
            }

            if (!isNonSpatial && (float.IsNaN(playPosition.x) || float.IsNaN(playPosition.y) || float.IsNaN(playPosition.z) ||
                float.IsInfinity(playPosition.x) || float.IsInfinity(playPosition.y) || float.IsInfinity(playPosition.z)))
            {
                Log.Warn($"[Scp575AudioManager] Invalid position for audio {audioKey}: {playPosition}. Falling back to Vector3.zero.");
                playPosition = Vector3.zero;
            }

            if (!isNonSpatial && config.minDistance > config.maxDistance)
            {
                Log.Warn($"[Scp575AudioManager] Invalid distances for audio {audioKey}: minDistance ({config.minDistance}) > maxDistance ({config.maxDistance}).");
                return 0;
            }

            if (audioKey == AudioKey.Ambience && (isNonSpatial || hearableForAllPlayers) && _ambienceAudioSessionId != 0)
            {
                StopAmbience();
            }

            int sessionId;

            Func<Player, bool> targetPlayerFilter = (!hearableForAllPlayers && player != null && !isNonSpatial)
                ? (p => p != null && p.UserId == player.UserId)
                : null;

            if (isNonSpatial)
            {
                sessionId = sharedAudioManager.PlayGlobalAudio(
                    config.key, loop: false, volume: config.volume, priority: config.priority,
                    validPlayersFilter: targetPlayerFilter, queue: queue, fadeInDuration: fadeInDuration,
                    lifespan: lifespan, autoCleanup: true);
            }
            else
            {
                sessionId = sharedAudioManager.PlayAudio(
                    config.key, playPosition, loop: false, volume: config.volume,
                    minDistance: config.minDistance, maxDistance: config.maxDistance,
                    isSpatial: config.isSpatial, priority: config.priority,
                    validPlayersFilter: targetPlayerFilter, queue: queue,
                    fadeInDuration: fadeInDuration, lifespan: lifespan, autoCleanup: true);
            }

            if (sessionId == 0)
            {
                Log.Warn($"[Scp575AudioManager] Failed to play audio {audioKey}. Possible reasons: audio file missing or session allocation failed.");
                return 0;
            }

            if (audioKey == AudioKey.Ambience && (isNonSpatial || hearableForAllPlayers))
            {
                _ambienceAudioSessionId = sessionId;
            }

            _pluginSessionIds.Add(sessionId);

            float effectiveLifespan = lifespan ?? config.defaultLifespan;
            if (effectiveLifespan > 0)
            {
                var coroutine = Timing.CallDelayed(effectiveLifespan, () =>
                 {
                     if (audioKey == AudioKey.Ambience && (isNonSpatial || hearableForAllPlayers))
                     {
                         StopAmbience();
                     }
                     else
                     {
                         sharedAudioManager.FadeOutAudio(sessionId, _plugin.Config.AudioConfig.DefaultFadeDuration);
                     }
                     _pluginSessionIds.Remove(sessionId);
                 });
                coroutine.Tag = AudioCoroutineTag;
            }

            return sessionId;
        }

        public int PlayGlobalAudioAutoManaged(AudioKey audioKey, float? lifespan = null, bool queue = false, float fadeInDuration = 0f)
        {
            return PlayAudioAutoManaged(null, audioKey, null, lifespan, hearableForAllPlayers: true, queue, fadeInDuration, isNonSpatial: true);
        }

        public int PlayAmbience(bool loop = true, float? lifespan = null, float fadeInDuration = 0f, bool queue = false)
        {
            var config = audioConfig[AudioKey.Ambience];

            if (!loop && config.defaultLifespan <= 0 && !lifespan.HasValue)
            {
                Log.Warn($"[Scp575AudioManager][PlayAmbience] Invalid default lifespan for ambience: {config.defaultLifespan}. Must be positive for non-looping audio.");
                return 0;
            }

            if (_ambienceAudioSessionId != 0)
            {
                StopAmbience();
            }

            var isDarkRoomFilter = AudioFilters.IsInRoomWhereLightsAre(false);
            Func<Player, bool> blackoutFilter = p => isDarkRoomFilter(p) && _plugin.Npc.Methods.IsBlackoutActive;

            int sessionId = sharedAudioManager.PlayGlobalAudio(
                key: config.key, loop: loop, volume: config.volume, priority: config.priority,
                validPlayersFilter: blackoutFilter, queue: queue, fadeInDuration: fadeInDuration,
                persistent: true, lifespan: null, autoCleanup: true);

            if (sessionId == 0) return 0;

            _ambienceAudioSessionId = sessionId;

            if (!loop && lifespan.HasValue)
            {
                if (lifespan.Value <= 0)
                {
                    StopAmbience();
                    return 0;
                }

                var coroutine = Timing.CallDelayed(lifespan.Value, StopAmbience);
                coroutine.Tag = AudioCoroutineTag;
            }

            return sessionId;
        }

        public void StopAmbience()
        {
            if (_ambienceAudioSessionId != 0)
            {
                sharedAudioManager.FadeOutAudio(_ambienceAudioSessionId, _plugin.Config.AudioConfig.DefaultFadeDuration);
                _ambienceAudioSessionId = 0;
            }
        }

        public void SkipAudio(int sessionId, int count)
        {
            if (sessionId == 0) return;
            sharedAudioManager.SkipAudio(sessionId, count);
        }

        private void RegisterAudioResources()
        {
            var assembly = Assembly.GetExecutingAssembly();
            string[] allResourceNames = assembly.GetManifestResourceNames();

            foreach (var pair in audioConfig)
            {
                string key = pair.Value.key;
                string resourceName = allResourceNames.FirstOrDefault(r =>
                    r.EndsWith($"{key}.wav", StringComparison.OrdinalIgnoreCase) ||
                    r.EndsWith($"{key.Replace(".", "_")}.wav", StringComparison.OrdinalIgnoreCase));

                if (string.IsNullOrEmpty(resourceName))
                {
                    Log.Error($"[Scp575AudioManager] CRITICAL: Could not find any embedded resource matching key '{key}'.");
                    continue;
                }

                sharedAudioManager.RegisterAudio(key, () =>
                {
                    var stream = assembly.GetManifestResourceStream(resourceName);
                    if (stream == null || stream.Length == 0)
                    {
                        Log.Error($"[Scp575AudioManager] Failed to load stream for: {resourceName}");
                    }
                    return stream;
                });
            }
        }

        /// <summary>
        /// Helper method to standardize audio playback parameters.
        /// </summary>
        public void PlayAudioAtPosition(AudioKey key, Vector3 position)
        {
            _plugin.AudioManager.PlayAudioAutoManaged(
                null,
                key,
                position: position,
                hearableForAllPlayers: true,
                lifespan: 25f);
        }

        /// <summary>
        /// Plays random audio effect for player. If no arguments are provided, selects from pool of AudioKey.WhispersMixed, AudioKey.Scream, AudioKey.ScreamAngry, AudioKey.Whispers.
        /// </summary>
        public void PlayRandomAudioEffect(Player player, params AudioKey[] options)
        {
            if (options == null || options.Length == 0)
            {
                options = new[] { AudioKey.WhispersMixed, AudioKey.Scream, AudioKey.ScreamAngry, AudioKey.Whispers };
            }

            var selected = options[UnityEngine.Random.Range(0, options.Length)];
            _plugin.AudioManager.PlayAudioAutoManaged(player, selected, hearableForAllPlayers: true, lifespan: 16f);
        }
    }
}