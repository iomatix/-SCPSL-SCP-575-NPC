namespace SCP_575.Shared.Audio
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using UnityEngine;
    using AudioManagerAPI.Defaults;
    using AudioManagerAPI.Features.Enums;
    using AudioManagerAPI.Features.Management;
    using AudioManagerAPI.Features.Speakers;
    using AudioManagerAPI.Features.Static;
    using LabApi.Features.Wrappers;
    using MEC;

    using SCP_575.Shared.Audio.Enums;
    using SCP_575.Shared.Audio.Filters;

    using Log = LabApi.Features.Console.Logger;

    /// <summary>
    /// Manages audio playback for SCP-575, including screams, whispers, and ambience.
    /// </summary>
    public class Scp575AudioManager
    {
        private static IAudioManager sharedAudioManager;
        private const float GLOBAL_SCREAM_COOLDOWN = 35f;
        private DateTime lastGlobalScreamTime = DateTime.MinValue;
        private byte _ambienceAudioControllerId;
        private const float DEFAULT_FADE_DURATION = 1f;

        private readonly Dictionary<AudioKey, (string key, float volume, float minDistance, float maxDistance, bool isSpatial, AudioPriority priority, float defaultLifespan)> audioConfig = new()
        {
            { AudioKey.Scream, ("scp575.scream", 0.8f, 5f, 50f, true, AudioPriority.High, 15f) },
            { AudioKey.ScreamAngry, ("scp575.scream-angry", 0.9f, 5f, 50f, true, AudioPriority.High, 15f) },
            { AudioKey.ScreamDying, ("scp575.scream-dying", 1.0f, 60f, 60f, true, AudioPriority.High, 15f) },
            { AudioKey.Whispers, ("scp575.whispers", 0.5f, 3f, 30f, true, AudioPriority.Medium, 15f) },
            { AudioKey.WhispersBang, ("scp575.whispers-bang", 0.6f, 3f, 30f, true, AudioPriority.Medium, 15f) },
            { AudioKey.WhispersMixed, ("scp575.whispers-mixed", 0.5f, 3f, 30f, true, AudioPriority.Medium, 15f) },
            { AudioKey.Ambience, ("scp575.ambience", 0.4f, 0f, 999.99f, false, AudioPriority.Low, 0f) },
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="Scp575AudioManager"/> class.
        /// </summary>
        public Scp575AudioManager()
        {
            if (sharedAudioManager == null)
            {
                DefaultAudioManager.RegisterDefaults(cacheSize: 20);
                sharedAudioManager = DefaultAudioManager.Instance;
                RegisterAudioResources();
            }
            _ambienceAudioControllerId = 0;
        }

        /// <summary>
        /// Plays an audio effect for a specific player or globally, with optional auto-stop after a lifespan.
        /// </summary>
        /// <param name="player">The player to hear the audio, or null for global playback.</param>
        /// <param name="audioKey">The audio effect to play.</param>
        /// <param name="position">The optional 3D position for playback; defaults to player's position or Vector3.zero for global.</param>
        /// <param name="lifespan">The optional duration before auto-stopping the audio; defaults to config value.</param>
        /// <param name="hearableForAllPlayers">If true, plays audio globally for all ready players.</param>
        /// <param name="queue">If true, queues the audio instead of playing immediately.</param>
        /// <param name="fadeInDuration">The duration of the fade-in effect in seconds (0 for no fade).</param>
        /// <returns>The controller ID of the speaker, or 0 if playback fails.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="player"/> is null when <paramref name="hearableForAllPlayers"/> is false.</exception>
        public byte PlayAudioAutoManaged(Player player, AudioKey audioKey, Vector3? position = null, float? lifespan = null, bool hearableForAllPlayers = false, bool queue = false, float fadeInDuration = 0f)
        {
            if (!hearableForAllPlayers && player == null)
                throw new ArgumentNullException(nameof(player), "Player cannot be null when hearableForAllPlayers is false.");

            var config = audioConfig[audioKey];
            Vector3 playPosition = hearableForAllPlayers ? (position ?? Vector3.zero) : (position ?? player.Position);

            // Enforce global scream cooldown
            if (hearableForAllPlayers && (audioKey == AudioKey.Scream || audioKey == AudioKey.ScreamAngry || audioKey == AudioKey.ScreamDying))
            {
                if ((DateTime.UtcNow - lastGlobalScreamTime).TotalSeconds < GLOBAL_SCREAM_COOLDOWN)
                {
                    Log.Warn($"[AudioManagerAPI] Global scream {audioKey} blocked due to cooldown. Time since last scream: {(DateTime.UtcNow - lastGlobalScreamTime).TotalSeconds:F2}s.");
                    return 0;
                }
                lastGlobalScreamTime = DateTime.UtcNow;
            }

            // Validate position
            if (float.IsNaN(playPosition.x) || float.IsNaN(playPosition.y) || float.IsNaN(playPosition.z) ||
                float.IsInfinity(playPosition.x) || float.IsInfinity(playPosition.y) || float.IsInfinity(playPosition.z))
            {
                Log.Warn($"[AudioManagerAPI] Invalid position for audio {audioKey}: {playPosition}.");
                return 0;
            }

            // Validate distances
            if (config.minDistance > config.maxDistance)
            {
                Log.Warn($"[AudioManagerAPI] Invalid distances for audio {audioKey}: minDistance ({config.minDistance}) > maxDistance ({config.maxDistance}).");
                return 0;
            }

            // Stop existing ambience if playing ambience globally
            if (hearableForAllPlayers && audioKey == AudioKey.Ambience && _ambienceAudioControllerId != 0)
            {
                StopAmbience();
            }

            byte controllerId;
            if (hearableForAllPlayers)
            {
                controllerId = sharedAudioManager.PlayGlobalAudio(config.key, false, config.volume, config.priority, queue: queue, fadeInDuration: fadeInDuration, lifespan: lifespan, autoCleanup: true);
            }
            else
            {
                controllerId = sharedAudioManager.PlayAudio(
                    config.key, playPosition, false, config.volume, config.minDistance, config.maxDistance, config.isSpatial, config.priority, queue: queue, lifespan: lifespan, autoCleanup: true, configureSpeaker: speaker =>
                    {
                        if (speaker is ISpeakerWithPlayerFilter playerFilterSpeaker)
                        {
                            playerFilterSpeaker.SetValidPlayers(p => p == player);
                        }
                    });
            }

            if (controllerId != 0)
            {
                // Track ambience controller ID for global ambience
                if (audioKey == AudioKey.Ambience && hearableForAllPlayers)
                {
                    _ambienceAudioControllerId = controllerId;
                }

                // Apply default or provided lifespan
                float effectiveLifespan = lifespan ?? config.defaultLifespan;
                if (effectiveLifespan > 0)
                {
                    if (effectiveLifespan <= 0)
                    {
                        Log.Warn($"[AudioManagerAPI] Invalid lifespan for audio {audioKey}: {effectiveLifespan}. Must be positive.");
                        if (audioKey == AudioKey.Ambience && hearableForAllPlayers)
                        {
                            StopAmbience();
                        }
                        else
                        {
                            sharedAudioManager.FadeOutAudio(controllerId, DEFAULT_FADE_DURATION);
                        }
                        return 0;
                    }

                    Timing.CallDelayed(effectiveLifespan, () =>
                    {
                        if (audioKey == AudioKey.Ambience && hearableForAllPlayers)
                        {
                            StopAmbience();
                        }
                        else
                        {
                            sharedAudioManager.FadeOutAudio(controllerId, DEFAULT_FADE_DURATION);
                            Log.Debug($"[AudioManagerAPI] Destroyed speaker for audio {audioKey} with controller ID {controllerId} after lifespan of {effectiveLifespan} seconds.");
                        }
                    });
                }

                Log.Debug($"[AudioManagerAPI] Played audio {audioKey} {(hearableForAllPlayers ? "globally" : $"for player {player?.Nickname ?? "unknown"}")} at {playPosition} with ID {controllerId}{(queue ? " (queued)" : "")}.");
            }
            else
            {
                Log.Warn($"[AudioManagerAPI] Failed to play audio {audioKey} {(hearableForAllPlayers ? "globally" : $"for player {player?.Nickname ?? "unknown"}")}. Possible reasons: audio file missing, speaker creation failed, or ID allocation queued.");
            }

            return controllerId;
        }

        /// <summary>
        /// Plays an audio effect globally for all ready players with optional auto-stop.
        /// </summary>
        /// <param name="audioKey">The audio effect to play.</param>
        /// <param name="lifespan">The optional duration before auto-stopping the audio; defaults to config value.</param>
        /// <param name="queue">If true, queues the audio instead of playing immediately.</param>
        /// <param name="fadeInDuration">The duration of the fade-in effect in seconds (0 for no fade).</param>
        /// <returns>The controller ID of the speaker, or 0 if playback fails.</returns>
        public byte PlayGlobalAudioAutoManaged(AudioKey audioKey, float? lifespan = null, bool queue = false, float fadeInDuration = 0f)
        {
            return PlayAudioAutoManaged(null, audioKey, Vector3.zero, lifespan, hearableForAllPlayers: true, queue, fadeInDuration);
        }

        /// <summary>
        /// Plays ambient audio globally, with optional looping and auto-stop.
        /// </summary>
        /// <param name="loop">Whether the ambience should loop.</param>
        /// <param name="lifespan">The optional duration before auto-stopping the audio; ignored if looping.</param>
        /// <param name="fadeInDuration">The duration of the fade-in effect in seconds (0 for no fade).</param>
        /// <param name="queue">If true, queues the ambience instead of playing immediately.</param>
        /// <returns>The controller ID of the speaker, or 0 if playback fails.</returns>
        public byte PlayAmbience(bool loop = true, float? lifespan = null, float fadeInDuration = 0f, bool queue = false)
        {
            var config = audioConfig[AudioKey.Ambience];

            // Stop currently playing ambience (if any)
            if (_ambienceAudioControllerId != 0)
            {
                sharedAudioManager.FadeOutAudio(_ambienceAudioControllerId, DEFAULT_FADE_DURATION);
                StopAmbience();
            }

            // Play new ambience
            byte controllerId = sharedAudioManager.PlayGlobalAudio(
                config.key, loop, config.volume, config.priority, queue: queue, fadeInDuration: fadeInDuration, persistent: true);

            // Try get speaker
            var speaker = StaticSpeakerFactory.GetSpeaker(controllerId);
            if (speaker == null) {
                Log.Warn($"[PlayAmbience] Speaker was not created correctly, received null.");
                return 0;
            }

            // Play Ambience only for players covered by darkness and if SCP-575 is active.
            if (speaker is ISpeakerWithPlayerFilter filterSpeaker)
            {
                filterSpeaker.ValidPlayers = AudioFilters.InDarkRoomAliveAndCondition(Plugin.Singleton.Npc.Methods.IsBlackoutActive);
            }
            else
            {
                Log.Warn($"[PlayAmbience] Speaker does not implement ISpeakerWithPlayerFilter. Filterring will not apply.");
            }

            if (controllerId != 0)
            {
                _ambienceAudioControllerId = controllerId;

                if (lifespan.HasValue && !loop)
                {
                    if (lifespan.Value <= 0)
                    {
                        Log.Warn($"[PlayAmbience] Invalid lifespan: {lifespan.Value}. Must be positive.");
                        StopAmbience();
                        return 0;
                    }

                    Timing.CallDelayed(lifespan.Value, () =>
                    {
                        sharedAudioManager.FadeOutAudio(controllerId, DEFAULT_FADE_DURATION);
                        StopAmbience();
                    });
                }

                Log.Debug($"[PlayAmbience] Started ambience with ID {controllerId} (loop: {loop}, queue: {queue}).");
            }
            else
            {
                Log.Warn($"[PlayAmbience] Failed to play ambience. Possible reasons: audio file missing, speaker creation failed, or ID allocation queued.");
            }

            return controllerId;
        }

        /// <summary>
        /// Stops and destroys the current global ambience audio.
        /// </summary>
        /// <returns>True if ambience was stopped; false if no ambience was playing.</returns>
        public bool StopAmbience()
        {
            if (_ambienceAudioControllerId == 0)
            {
                Log.Debug($"[StopAmbience] No ambience is currently playing.");
                return false;
            }

            var speaker = sharedAudioManager.GetSpeaker(_ambienceAudioControllerId);
            if (speaker == null)
            {
                Log.Warn($"[StopAmbience] No speaker found for ambience controller ID {_ambienceAudioControllerId}. Clearing invalid ID.");
                _ambienceAudioControllerId = 0;
                return false;
            }

            sharedAudioManager.FadeOutAudio(_ambienceAudioControllerId, DEFAULT_FADE_DURATION);
            sharedAudioManager.DestroySpeaker(_ambienceAudioControllerId);
            Log.Debug($"[StopAmbience] Stopped and destroyed ambience with controller ID {_ambienceAudioControllerId}.");
            _ambienceAudioControllerId = 0;

            return true;
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
                Log.Warn($"[SkipAudio] Invalid controller ID 0 for skipping audio.");
                return;
            }

            sharedAudioManager.SkipAudio(controllerId, count);
            Log.Debug($"[SkipAudio] Skipped {count} audio clips for controller ID {controllerId}.");
        }

        /// <summary>
        /// Cleans up all active SCP-575 audio speakers and resets the ambience controller ID.
        /// </summary>
        public void CleanupAllSpeakers()
        {
            sharedAudioManager.CleanupAllSpeakers();
            _ambienceAudioControllerId = 0;
            Log.Debug($"[CleanupAllSpeakers] Cleaned up all SCP-575 audio speakers.");
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
                    if (stream == null)
                    {
                        Log.Error($"[RegisterAudioResources] Failed to load audio resource: {resourceName}");
                    }
                    return stream;
                });
                Log.Debug($"[RegisterAudioResources] Registered audio resource: {pair.Value.key}");
            }
        }
    }
}