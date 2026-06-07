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
        private readonly Dictionary<string, int> _activeDroneSessions = new(); // FIXED: Tracks UserID -> Audio SessionID
        private readonly HashSet<int> _generatorSessionIds = new();
        private readonly HashSet<int> _pluginSessionIds = new();

        private const string AudioCoroutineTag = CoroutineTags.AudioCoroutines;

        private readonly Dictionary<AudioKey, AudioTrackProfile> _audioRegistry = new()
        {
            { AudioKey.Scream_1, new("scp575.scream_1", 0.91f, 65f, 150f, true, AudioPriority.High, 9f) },
            { AudioKey.Scream_2, new("scp575.scream_2", 0.91f, 65f, 150f, true, AudioPriority.High, 9f) },
            { AudioKey.Scream_3, new("scp575.scream_3", 0.91f, 65f, 150f, true, AudioPriority.High, 9f) },
            { AudioKey.ScreamAngry, new("scp575.scream_angry", 0.99f, 175f, 450f, true, AudioPriority.High, 9f) },
            { AudioKey.ScreamHurt, new("scp575.scream_hurt", 0.99f, 125f, 345f, true, AudioPriority.High, 7f) },
            { AudioKey.ScreamDying, new("scp575.scream_dying", 1.0f, 255f, 480f, true, AudioPriority.High, 20f) },
            { AudioKey.MonsterRoarGlobal, new("scp575.monster_roar_global", 0.95f, 45f, 999.99f, false, AudioPriority.High, 40f) },

            { AudioKey.Whispers_1, new("scp575.whispers_1", 0.47f, 5f, 35f, true, AudioPriority.Medium, 11f) },
            { AudioKey.Whispers_2, new("scp575.whispers_2", 0.55f, 7f, 45f, true, AudioPriority.Medium, 19f) },
            { AudioKey.Whispers_3, new("scp575.whispers_3", 0.65f, 9f, 52f, true, AudioPriority.Medium, 14f) },
            { AudioKey.WhispersBang, new("scp575.whispers_bang", 0.78f, 12f, 65f, true, AudioPriority.High, 20f) },
            { AudioKey.WhispersMixed, new("scp575.whispers_mixed", 0.75f, 10f, 55f, true, AudioPriority.Medium, 25f) },
            { AudioKey.MonsterBreathLocal, new("scp575.monster_breath_local", 0.75f, 5f, 24f, true, AudioPriority.High, 11f) },
            { AudioKey.ShadowClicking, new("scp575.shadow_clicking", 0.67f, 4.75f, 33f, true, AudioPriority.High, 9f) },

            { AudioKey.ShadowStrike, new("scp575.shadow_strike", 0.88f, 5.5f, 37f, true, AudioPriority.High, 5f) },
            { AudioKey.GeneratorHumDefense, new("scp575.generator_hum_defense", 0.67f, 6.5f, 45f, true, AudioPriority.Medium, 0f) },

            { AudioKey.Ambience, new("scp575.ambience", 0.45f, 0f, 999.99f, false, AudioPriority.Low, 0f) },
            { AudioKey.SanityLowDrone, new("scp575.sanity_low_drone", 0.55f, 200.0f, 999.99f, false, AudioPriority.Medium, 0f) },
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

            if (fullShutdown && _generatorSessionIds.Count > 0)
            {
                foreach (int sessionId in _generatorSessionIds.ToList())
                {
                    if (sessionId == 0) continue;
                    try { _audioEngine.FadeOutAudio(sessionId, _plugin.Config.AudioConfig.DefaultFadeDuration); } catch { }
                }
                _generatorSessionIds.Clear();
            }

            // FIXED: Clean iteration loop utilizing the stored session IDs natively
            if (fullShutdown && _activeDroneSessions.Count > 0)
            {
                foreach (int sessionId in _activeDroneSessions.Values.ToList())
                {
                    if (sessionId == 0) continue;
                    try { _audioEngine.FadeOutAudio(sessionId, _plugin.Config.AudioConfig.DefaultFadeDuration); } catch { }
                }
                _activeDroneSessions.Clear();
            }

            Log.Debug($"[Scp575AudioManager] Clean executed. (FullShutdown: {fullShutdown})");
        }

        public int PlayAudioAutoManaged(Player player, AudioKey audioKey, Vector3? position = null, float? lifespan = null, bool hearableForAllPlayers = false, bool queue = false, float fadeInDuration = 0f, bool isNonSpatial = false, bool isTransient = false)
        {
            if (!_audioRegistry.TryGetValue(audioKey, out var config))
                throw new ArgumentException($"Audio key {audioKey} not found in configuration.", nameof(audioKey));

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

            Func<Player, bool> targetPlayerFilter = (!hearableForAllPlayers && player != null)
                ? (p => p != null && p.UserId == player.UserId)
                : null;

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

            float effectiveLifespan = lifespan ?? config.DefaultLifespan;
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
                        _audioEngine.FadeOutAudio(sessionId, _plugin.Config.AudioConfig.DefaultFadeDuration);
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

        public void StopAmbience()
        {
            if (_ambienceAudioSessionId != 0)
            {
                _audioEngine.FadeOutAudio(_ambienceAudioSessionId, _plugin.Config.AudioConfig.DefaultFadeDuration);
                _ambienceAudioSessionId = 0;
            }
        }

        public void SkipAudio(int sessionId, int count) => _audioEngine.SkipAudio(sessionId, count);

        public void PlayAudioAtPosition(AudioKey key, Vector3 position, float? lifespan = null, bool isTransient = false)
        {
            PlayAudioAutoManaged(null, key, position, lifespan: lifespan, hearableForAllPlayers: true, isNonSpatial: false, isTransient: isTransient);
        }

        public int PlayLocalAudio(Player player, AudioKey audioKey, float? lifespan = null, float fadeInDuration = 0f, bool isTransient = false)
        {
            return PlayAudioAutoManaged(player, audioKey, position: null, lifespan: lifespan, hearableForAllPlayers: false, queue: false, fadeInDuration: fadeInDuration, isNonSpatial: true, isTransient: isTransient);
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

                // Invokes your pre-existing PlayLocalAudio pipeline using a 2.0s immersive fade-in
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