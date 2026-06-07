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

        private const string AudioCoroutineTag = CoroutineTags.AudioCoroutines;

        private readonly Dictionary<AudioKey, (string key, float volume, float minDistance, float maxDistance, bool isSpatial, AudioPriority priority, float defaultLifespan)> audioConfig = new()
        {
            // ===================================================================
            // TRANSIENT VOCALIZATIONS & VOCAL ATTACKS
            // ===================================================================
            { AudioKey.Scream_1, ("scp575.scream_1", 0.85f, 5f, 50f, true, AudioPriority.High, 15f) },
            { AudioKey.Scream_2, ("scp575.scream_2", 0.85f, 5f, 50f, true, AudioPriority.High, 15f) },
            { AudioKey.Scream_3, ("scp575.scream_3", 0.85f, 5f, 50f, true, AudioPriority.High, 15f) },
            { AudioKey.ScreamAngry, ("scp575.scream_angry", 0.9f, 5f, 50f, true, AudioPriority.High, 15f) },
            { AudioKey.ScreamHurt, ("scp575.scream_hurt", 0.9f, 5f, 45f, true, AudioPriority.High, 10f) },
            { AudioKey.ScreamDying, ("scp575.scream_dying", 1.0f, 15f, 80f, true, AudioPriority.High, 20f) },
            { AudioKey.MonsterRoarGlobal, ("scp575.monster_roar_global", 0.95f, 0f, 999.99f, false, AudioPriority.High, 35f) },

            // ===================================================================
            // SPATIALIZED PSYCHOLOGICAL FEEDBACK & PARANOIA
            // ===================================================================
            { AudioKey.Whispers_1, ("scp575.whispers_1", 0.5f, 2f, 20f, true, AudioPriority.Medium, 10f) },
            { AudioKey.Whispers_2, ("scp575.whispers_2", 0.65f, 3f, 25f, true, AudioPriority.Medium, 15f) },
            { AudioKey.WhispersBang, ("scp575.whispers_bang", 0.75f, 2f, 15f, true, AudioPriority.High, 5f) },
            { AudioKey.WhispersMixed, ("scp575.whispers_mixed", 0.8f, 2f, 25f, true, AudioPriority.Medium, 20f) },
            { AudioKey.MonsterBreathLocal, ("scp575.monster_breathe_local", 0.85f, 1f, 8f, true, AudioPriority.High, 8f) },
            { AudioKey.ShadowClicking, ("scp575.shadow_clicking", 0.7f, 1f, 12f, true, AudioPriority.Medium, 5f) },

            // ===================================================================
            // KINETIC TRAUMA & TACTICAL INTERACTION FEEDBACK
            // ===================================================================
            { AudioKey.ShadowStrike, ("scp575.shadow_strike", 0.9f, 3f, 35f, true, AudioPriority.High, 8f) },
            { AudioKey.GeneratorHumDefense, ("scp575.generator_hum_defense", 0.75f, 5f, 40f, true, AudioPriority.Medium, 0f) },

            // ===================================================================
            // ENVIRONMENTAL ACOUSTIC BACKGROUNDS & ZONE STATE TRANSITIONS
            // ===================================================================
            { AudioKey.Ambience, ("scp575.ambience", 0.45f, 0f, 999.99f, false, AudioPriority.Low, 0f) },
            { AudioKey.SanityLowDrone, ("scp575.sanity_low_drone", 0.6f, 0f, 999.99f, false, AudioPriority.Medium, 0f) },
            { AudioKey.BlackoutImpactGlobal, ("scp575.blackout_impact_global", 1.0f, 0f, 999.99f, false, AudioPriority.High, 10f) },
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

            // FIXED: Expanded anti-spam check to accurately account for split scream keys.
            if (isNonSpatial && (audioKey == AudioKey.Scream_1 || audioKey == AudioKey.Scream_2 || audioKey == AudioKey.Scream_3 || audioKey == AudioKey.ScreamAngry || audioKey == AudioKey.ScreamDying))
            {
                double secondsSinceLastScream = (DateTime.UtcNow - lastGlobalScreamTime).TotalSeconds;
                if (secondsSinceLastScream < _plugin.Config.AudioConfig.GlobalScreamCooldown)
                {
                    return 0;
                }
                lastGlobalScreamTime = DateTime.UtcNow;
            }

            if (!isNonSpatial && (float.IsNaN(playPosition.x) || float.IsNaN(playPosition.y) || float.IsNaN(playPosition.z) ||
                float.IsInfinity(playPosition.x) || float.IsInfinity(playPosition.y) || float.IsInfinity(playPosition.z)))
            {
                Log.Warn($"[Scp575AudioManager] Invalid position for audio {audioKey}. Falling back to Vector3.zero.");
                playPosition = Vector3.zero;
            }

            if (!isNonSpatial && config.minDistance > config.maxDistance)
            {
                Log.Warn($"[Scp575AudioManager] Invalid distances for audio {audioKey}: minDistance > maxDistance.");
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

            if (sessionId == 0) return 0;

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

            if (_ambienceAudioSessionId != 0) StopAmbience();

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
                sharedAudioManager.FadeOutAudio(_ambienceAudioSessionId, _plugin.Config.AudioConfig.DefaultFadeDuration);
                _ambienceAudioSessionId = 0;
            }
        }

        public void SkipAudio(int sessionId, int count)
        {
            if (sessionId == 0) return;
            sharedAudioManager.SkipAudio(sessionId, count);
        }

        /// <summary>
        /// Deploys a clean, spatialized environmental 3D sound component without breaking dictionary constraints.
        /// </summary>
        public void PlayAudioAtPosition(AudioKey key, Vector3 position, float? lifespan = null)
        {
            _plugin.AudioManager.PlayAudioAutoManaged(
                player: null,
                audioKey: key,
                position: position,
                lifespan: lifespan, // FIXED: Now cleanly falls back to config matrix if null.
                hearableForAllPlayers: true,
                isNonSpatial: false);
        }

        /// <summary>
        /// Plays random audio effect for player. If no options are provided, defaults to baseline paranoia pool.
        /// </summary>
        public void PlayRandomAudioEffect(Player player, params AudioKey[] options)
        {
            if (options == null || options.Length == 0)
            {
                options = new[] { AudioKey.WhispersMixed, AudioKey.Scream_1, AudioKey.Scream_2, AudioKey.Scream_3, AudioKey.ScreamAngry, AudioKey.ScreamHurt, AudioKey.Whispers_1, AudioKey.Whispers_2 };
            }

            var selected = options[UnityEngine.Random.Range(0, options.Length)];
            _plugin.AudioManager.PlayAudioAutoManaged(player, selected, hearableForAllPlayers: true, lifespan: null);
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

                if (string.IsNullOrEmpty(resourceName)) continue;

                sharedAudioManager.RegisterAudio(key, () => {
                    var stream = assembly.GetManifestResourceStream(resourceName);
                    return stream;
                });
            }
        }
    }
}