namespace SCP_575.Shared.Audio
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using UnityEngine;
    using AudioManagerAPI.Defaults;
    using AudioManagerAPI.Features.Enums;
    using AudioManagerAPI.Features.Filters;
    using AudioManagerAPI.Features.Management;
    using AudioManagerAPI.Features.Speakers;
    using AudioManagerAPI.Features.Static;
    using LabApi.Features.Wrappers;
    using MEC;
    using SCP_575.Shared.Audio.Enums;
    using SCP_575.ConfigObjects;
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

        // byte -> int (Session ID update)
        private int _ambienceAudioSessionId;
        private readonly HashSet<int> _pluginSessionIds = new();

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

        public int PlayAudioAutoManaged(Player player, AudioKey audioKey, Vector3? position = null, float? lifespan = null, bool hearableForAllPlayers = false, bool queue = false, float fadeInDuration = 0f, bool isNonSpatial = false)
        {
            if (!audioConfig.ContainsKey(audioKey))
                throw new ArgumentException($"Audio key {audioKey} not found in configuration.", nameof(audioKey));

            if (!hearableForAllPlayers && player == null && !isNonSpatial)
                throw new ArgumentNullException(nameof(player), "Player cannot be null when hearableForAllPlayers is false and audio is spatial.");

            var config = audioConfig[audioKey];
            Vector3 playPosition = isNonSpatial ? Vector3.zero : (position ?? player.Position);

            if (!isNonSpatial && player != null)
            {
                float distance = Vector3.Distance(player.Position, playPosition);
                Log.Debug($"[Scp575AudioManager] Player {player.Nickname} position: {player.Position}, audio position: {playPosition}, distance: {distance:F2}, maxDistance: {config.maxDistance}");
            }

            if (isNonSpatial && (audioKey == AudioKey.Scream || audioKey == AudioKey.ScreamAngry || audioKey == AudioKey.ScreamDying))
            {
                double secondsSinceLastScream = (DateTime.UtcNow - lastGlobalScreamTime).TotalSeconds;
                if (secondsSinceLastScream < _plugin.Config.AudioConfig.GlobalScreamCooldown)
                {
                    Log.Warn($"[Scp575AudioManager] Scream audio {audioKey} blocked due to global cooldown. Time since last scream: {secondsSinceLastScream:F2}s.");
                    return 0;
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


            // zero-allocation player filter - only create if needed, otherwise pass null to play for all players
            Func<Player, bool> targetPlayerFilter = (!hearableForAllPlayers && player != null && !isNonSpatial)
                ? (p => p != null && p.UserId == player.UserId)
                : null;

            if (isNonSpatial)
            {
                sessionId = sharedAudioManager.PlayGlobalAudio(
                    config.key,
                    loop: false,
                    volume: config.volume,
                    priority: config.priority,
                    validPlayersFilter: targetPlayerFilter,
                    queue: queue,
                    fadeInDuration: fadeInDuration,
                    lifespan: lifespan,
                    autoCleanup: true);
            }
            else
            {
                sessionId = sharedAudioManager.PlayAudio(
                    config.key,
                    playPosition,
                    loop: false,
                    volume: config.volume,
                    minDistance: config.minDistance,
                    maxDistance: config.maxDistance,
                    isSpatial: config.isSpatial,
                    priority: config.priority,
                    validPlayersFilter: targetPlayerFilter,
                    queue: queue,
                    fadeInDuration: fadeInDuration,
                    lifespan: lifespan,
                    autoCleanup: true);
            }

            if (sessionId == 0)
            {
                Log.Warn($"[Scp575AudioManager] Failed to play audio {audioKey} {(isNonSpatial ? "globally (non-spatial)" : hearableForAllPlayers ? "for all nearby players" : $"for player {player?.Nickname ?? "unknown"}")} at {playPosition}. Possible reasons: audio file missing or session allocation failed.");
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
                if (effectiveLifespan <= 0)
                {
                    Log.Warn($"[Scp575AudioManager] Invalid lifespan for audio {audioKey}: {effectiveLifespan}. Must be positive.");
                    if (audioKey == AudioKey.Ambience && (isNonSpatial || hearableForAllPlayers))
                    {
                        StopAmbience();
                    }
                    else
                    {
                        sharedAudioManager.FadeOutAudio(sessionId, _plugin.Config.AudioConfig.DefaultFadeDuration);
                    }
                    return 0;
                }

                Timing.CallDelayed(effectiveLifespan, () =>
                {
                    if (audioKey == AudioKey.Ambience && (isNonSpatial || hearableForAllPlayers))
                    {
                        StopAmbience();
                    }
                    else
                    {
                        sharedAudioManager.FadeOutAudio(sessionId, _plugin.Config.AudioConfig.DefaultFadeDuration);
                        Log.Debug($"[Scp575AudioManager] Stopped audio {audioKey} with session ID {sessionId} after lifespan of {effectiveLifespan} seconds.");
                    }
                    _pluginSessionIds.Remove(sessionId);
                });
            }

            Log.Debug($"[Scp575AudioManager] Played audio {audioKey} {(isNonSpatial ? "globally (non-spatial)" : hearableForAllPlayers ? "for all nearby players" : $"for player {player?.Nickname ?? "unknown"}")} at {playPosition} with Session ID {sessionId}{(queue ? " (queued)" : "")}.");
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

            // Const filter to avoid allocations in loop - checks if player is in a dark room and if blackout is active
            var isDarkRoomFilter = AudioFilters.IsInRoomWhereLightsAre(false);
            Func<Player, bool> blackoutFilter = p => isDarkRoomFilter(p) && _plugin.Npc.Methods.IsBlackoutActive;

            int sessionId = sharedAudioManager.PlayGlobalAudio(
                key: config.key,
                loop: loop,
                volume: config.volume,
                priority: config.priority,
                validPlayersFilter: blackoutFilter,
                queue: queue,
                fadeInDuration: fadeInDuration,
                persistent: true,
                lifespan: null,
                autoCleanup: true);

            if (sessionId == 0)
            {
                Log.Warn($"[Scp575AudioManager][PlayAmbience] Failed to play ambience. Possible reasons: audio file missing or session allocation failed.");
                return 0;
            }

            _ambienceAudioSessionId = sessionId;

            if (!loop && lifespan.HasValue)
            {
                if (lifespan.Value <= 0)
                {
                    Log.Warn($"[Scp575AudioManager][PlayAmbience] Invalid lifespan: {lifespan.Value}. Must be positive.");
                    StopAmbience();
                    return 0;
                }

                Timing.CallDelayed(lifespan.Value, StopAmbience);
            }

            Log.Debug($"[Scp575AudioManager][PlayAmbience] Started ambience with Session ID {sessionId} (loop: {loop}, queue: {queue}).");
            return sessionId;
        }

        public void StopAmbience()
        {
            if (_ambienceAudioSessionId != 0)
            {
                sharedAudioManager.FadeOutAudio(_ambienceAudioSessionId, _plugin.Config.AudioConfig.DefaultFadeDuration);
                Log.Debug($"[Scp575AudioManager] Stopped ambience audio with Session ID {_ambienceAudioSessionId}.");
                _ambienceAudioSessionId = 0;
            }
        }

        public void SkipAudio(int sessionId, int count)
        {
            if (sessionId == 0)
            {
                Log.Warn($"[Scp575AudioManager][SkipAudio] Invalid session ID 0 for skipping audio.");
                return;
            }

            sharedAudioManager.SkipAudio(sessionId, count);
            Log.Debug($"[Scp575AudioManager][SkipAudio] Skipped {count} audio clips for Session ID {sessionId}.");
        }

        private void RegisterAudioResources()
        {
            var assembly = Assembly.GetExecutingAssembly();
            foreach (var pair in audioConfig)
            {
                string resourceName = $"SCP_575.Shared.Audio.Files.{pair.Value.key}.wav";
                sharedAudioManager.RegisterAudio(pair.Value.key, () =>
                {
                    var stream = assembly.GetManifestResourceStream(resourceName);
                    if (stream == null || stream.Length == 0)
                    {
                        Log.Error($"[Scp575AudioManager][RegisterAudioResources] Failed to load audio resource: {resourceName}. Stream is null or empty.");
                    }
                    else
                    {
                        Log.Debug($"[Scp575AudioManager][RegisterAudioResources] Loaded audio resource: {resourceName}, size: {stream.Length} bytes");
                    }
                    return stream;
                });
                Log.Debug($"[Scp575AudioManager][RegisterAudioResources] Registered audio resource: {pair.Value.key}");
            }
        }
    }
}