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
    /// </summary>
    public class Scp575AudioManager
    {
        private readonly Plugin _plugin;
        private static IAudioManager sharedAudioManager;
        private DateTime lastGlobalScreamTime = DateTime.MinValue;
        private byte _ambienceAudioControllerId;

        private readonly HashSet<byte> _pluginControllerIds = new();

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

        /// <summary>
        /// Initializes a new instance of the <see cref="Scp575AudioManager"/> class.
        /// </summary>
        /// <param name="plugin">Reference to the main <see cref="Plugin"/> instance.</param>
        /// <exception cref="ArgumentNullException">Thrown if the plugin instance or its AudioConfig is null.</exception>
        /// <exception cref="ArgumentException">Thrown if the AudioConfig values are invalid.</exception>
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


            _ambienceAudioControllerId = 0;
        }

        /// <summary>
        /// Plays an audio effect at a 3D position for a specific player, all nearby players, or globally (non-spatial) for all players, with optional auto-stop after a lifespan.
        /// </summary>
        /// <param name="player">The player to hear the audio, or null for playback audible to all nearby players or non-spatial global playback.</param>
        /// <param name="audioKey">The audio effect to play.</param>
        /// <param name="position">The optional 3D position for spatial audio; defaults to player's position if hearableForAllPlayers is false, or Vector3.zero if hearableForAllPlayers is true.</param>
        /// <param name="lifespan">The optional duration before auto-stopping the audio; defaults to config value.</param>
        /// <param name="hearableForAllPlayers">If true, plays spatial audio at the specified position for all nearby players; if false, plays only for the specified player. Ignored if isNonSpatial is true.</param>
        /// <param name="queue">If true, queues the audio instead of playing immediately.</param>
        /// <param name="fadeInDuration">The duration of the fade-in effect in seconds (0 for no fade).</param>
        /// <param name="isNonSpatial">If true, plays audio globally without spatial positioning, audible to all ready players.</param>
        /// <returns>The controller ID of the speaker, or 0 if playback fails.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="player"/> is null when <paramref name="hearableForAllPlayers"/> is false and <paramref name="isNonSpatial"/> is false.</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="audioKey"/> is not found in the audio configuration.</exception>
        public byte PlayAudioAutoManaged(Player player, AudioKey audioKey, Vector3? position = null, float? lifespan = null, bool hearableForAllPlayers = false, bool queue = false, float fadeInDuration = 0f, bool isNonSpatial = false)
        {
            if (!audioConfig.ContainsKey(audioKey))
                throw new ArgumentException($"Audio key {audioKey} not found in configuration.", nameof(audioKey));

            if (!hearableForAllPlayers && player == null && !isNonSpatial)
                throw new ArgumentNullException(nameof(player), "Player cannot be null when hearableForAllPlayers is false and audio is spatial.");

            var config = audioConfig[audioKey];
            Vector3 playPosition = isNonSpatial ? Vector3.zero : (position ?? player.Position);
            // Log player position and distance to audio position
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

            // Validate position for spatial audio
            if (!isNonSpatial && (float.IsNaN(playPosition.x) || float.IsNaN(playPosition.y) || float.IsNaN(playPosition.z) ||
                float.IsInfinity(playPosition.x) || float.IsInfinity(playPosition.y) || float.IsInfinity(playPosition.z)))
            {
                Log.Warn($"[Scp575AudioManager] Invalid position for audio {audioKey}: {playPosition}. Falling back to Vector3.zero.");
                playPosition = Vector3.zero;
            }

            // Validate distances for spatial audio
            if (!isNonSpatial && config.minDistance > config.maxDistance)
            {
                Log.Warn($"[Scp575AudioManager] Invalid distances for audio {audioKey}: minDistance ({config.minDistance}) > maxDistance ({config.maxDistance}).");
                return 0;
            }

            // Stop existing ambience if playing new ambience
            if (audioKey == AudioKey.Ambience && (isNonSpatial || hearableForAllPlayers) && _ambienceAudioControllerId != 0)
            {
                StopAmbience();
            }

            byte controllerId;
            if (isNonSpatial)
            {
                controllerId = sharedAudioManager.PlayGlobalAudio(
                    config.key,
                    loop: false,
                    volume: config.volume,
                    priority: config.priority,
                    queue: queue,
                    fadeInDuration: fadeInDuration,
                    lifespan: lifespan,
                    autoCleanup: true);
            }
            else
            {
                controllerId = sharedAudioManager.PlayAudio(
                    config.key,
                    playPosition,
                    loop: false,
                    volume: config.volume,
                    minDistance: config.minDistance,
                    maxDistance: config.maxDistance,
                    isSpatial: config.isSpatial,
                    priority: config.priority,
                    queue: queue,
                    lifespan: lifespan,
                    autoCleanup: true,
                    configureSpeaker: speaker =>
                    {
                        if (!hearableForAllPlayers && speaker is ISpeakerWithPlayerFilter playerFilterSpeaker)
                        {
                            playerFilterSpeaker.SetValidPlayers(p => p == player);
                        }
                    });
            }

            if (controllerId == 0)
            {
                Log.Warn($"[Scp575AudioManager] Failed to play audio {audioKey} {(isNonSpatial ? "globally (non-spatial)" : hearableForAllPlayers ? "for all nearby players" : $"for player {player?.Nickname ?? "unknown"}")} at {playPosition}. Possible reasons: audio file missing, speaker creation failed, or ID allocation queued.");
                return 0;
            }

            // Track ambience controller ID
            if (audioKey == AudioKey.Ambience && (isNonSpatial || hearableForAllPlayers))
            {
                _ambienceAudioControllerId = controllerId;
            }

            // Apply lifespan for auto-stop
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
                        sharedAudioManager.FadeOutAudio(controllerId, _plugin.Config.AudioConfig.DefaultFadeDuration);
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
                        sharedAudioManager.FadeOutAudio(controllerId, _plugin.Config.AudioConfig.DefaultFadeDuration);
                        Log.Debug($"[Scp575AudioManager] Stopped audio {audioKey} with controller ID {controllerId} after lifespan of {effectiveLifespan} seconds.");
                    }
                });
            }

            Log.Debug($"[Scp575AudioManager] Played audio {audioKey} {(isNonSpatial ? "globally (non-spatial)" : hearableForAllPlayers ? "for all nearby players" : $"for player {player?.Nickname ?? "unknown"}")} at {playPosition} with ID {controllerId}{(queue ? " (queued)" : "")}.");
            return controllerId;
        }

        /// <summary>
        /// Plays an audio effect globally (non-spatial) for all ready players with optional auto-stop.
        /// </summary>
        /// <param name="audioKey">The audio effect to play.</param>
        /// <param name="lifespan">The optional duration before auto-stopping the audio; defaults to config value.</param>
        /// <param name="queue">If true, queues the audio instead of playing immediately.</param>
        /// <param name="fadeInDuration">The duration of the fade-in effect in seconds (0 for no fade).</param>
        /// <returns>The controller ID of the speaker, or 0 if playback fails.</returns>
        public byte PlayGlobalAudioAutoManaged(AudioKey audioKey, float? lifespan = null, bool queue = false, float fadeInDuration = 0f)
        {
            return PlayAudioAutoManaged(null, audioKey, null, lifespan, hearableForAllPlayers: true, queue, fadeInDuration, isNonSpatial: true);
        }

        /// <summary>
        /// Plays ambient audio globally, with optional looping and auto-stop, audible only to players in dark rooms during SCP-575 blackout.
        /// </summary>
        /// <param name="loop">Whether the ambience should loop.</param>
        /// <param name="lifespan">The optional duration before auto-stopping the audio; ignored if looping.</param>
        /// <param name="fadeInDuration">The duration of the fade-in effect in seconds (0 for no fade).</param>
        /// <param name="queue">If true, queues the ambience instead of playing immediately.</param>
        /// <returns>The controller ID of the speaker, or 0 if playback fails.</returns>
        public byte PlayAmbience(bool loop = true, float? lifespan = null, float fadeInDuration = 0f, bool queue = false)
        {
            var config = audioConfig[AudioKey.Ambience];

            // Validate default lifespan
            if (!loop && config.defaultLifespan <= 0 && !lifespan.HasValue)
            {
                Log.Warn($"[Scp575AudioManager][PlayAmbience] Invalid default lifespan for ambience: {config.defaultLifespan}. Must be positive for non-looping audio.");
                return 0;
            }

            // Stop existing ambience if any
            if (_ambienceAudioControllerId != 0)
            {
                StopAmbience();
            }

            // Play new ambience
            byte controllerId = sharedAudioManager.PlayGlobalAudioWithFilter(
                key: config.key,
                loop: loop,
                volume: config.volume,
                priority: config.priority,
                configureSpeaker: speaker =>
                {
                    if (speaker is ISpeakerWithPlayerFilter filterSpeaker)
                    {
                        filterSpeaker.SetValidPlayers(player => AudioFilters.IsInRoomWhereLightsAre(false)(player) && _plugin.Npc.Methods.IsBlackoutActive);
                    }
                    else
                    {
                        Log.Warn($"[Scp575AudioManager][PlayAmbience] Speaker does not implement ISpeakerWithPlayerFilter. Filtering will not apply.");
                    }
                },
                queue: queue,
                fadeInDuration: fadeInDuration,
                persistent: true,
                lifespan: null, // Managed manually below
                autoCleanup: true);

            if (controllerId == 0)
            {
                Log.Warn($"[Scp575AudioManager][PlayAmbience] Failed to play ambience. Possible reasons: audio file missing, speaker creation failed, or ID allocation queued.");
                return 0;
            }

            _ambienceAudioControllerId = controllerId;

            // Apply lifespan for non-looping ambience
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

            Log.Debug($"[Scp575AudioManager][PlayAmbience] Started ambience with ID {controllerId} (loop: {loop}, queue: {queue}).");
            return controllerId;
        }

        /// <summary>
        /// Stops the currently playing ambience audio, if any.
        /// </summary>
        public void StopAmbience()
        {
            if (!sharedAudioManager.IsValidController(_ambienceAudioControllerId)) return;
            if (_ambienceAudioControllerId != 0)
            {
                sharedAudioManager.FadeOutAudio(_ambienceAudioControllerId, _plugin.Config.AudioConfig.DefaultFadeDuration);
                Log.Debug($"[Scp575AudioManager] Stopped ambience audio with controller ID {_ambienceAudioControllerId}.");
                _ambienceAudioControllerId = 0;
            }
        }

        /// <summary>
        /// Skips the specified number of audio clips for a given controller ID.
        /// </summary>
        /// <param name="controllerId">The controller ID of the speaker to skip audio for.</param>
        /// <param name="count">The number of clips to skip, including the current one.</param>
        public void SkipAudio(byte controllerId, int count)
        {
            if (controllerId == 0)
            {
                Log.Warn($"[Scp575AudioManager][SkipAudio] Invalid controller ID 0 for skipping audio.");
                return;
            }

            sharedAudioManager.SkipAudio(controllerId, count);
            Log.Debug($"[Scp575AudioManager][SkipAudio] Skipped {count} audio clips for controller ID {controllerId}.");
        }

        /// <summary>
        /// Registers SCP-575 audio resources from the assembly's embedded resources.
        /// </summary>
        private void RegisterAudioResources()
        {
            var assembly = Assembly.GetExecutingAssembly();
            foreach (var pair in audioConfig)
            {
                string resourceName = $"SCP-575.Shared.Audio.Files.{pair.Value.key}.wav";
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