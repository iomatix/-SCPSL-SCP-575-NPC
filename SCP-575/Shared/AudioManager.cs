﻿namespace SCP_575.Shared
{

    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using LabApi.Features.Audio;
    using LabApi.Features.Wrappers;
    using MEC;
    using SCP_575.Npc;
    using UnityEngine;

    /// <summary>
    /// Manages audio playback for SCP: Secret Laboratory using LabAPI.
    /// Provides methods to play sounds locally and globally, manage speakers,
    /// and monitor performance.
    /// </summary>
    public static class AudioManager
    {
        /// <summary>
        /// Defines the available audio keys for playback.
        /// </summary>
        public enum AudioKey
        {
            Scream,
            ScreamAngry,
            ScreamDying,
            Whispers,
            WhispersBang,
            WhispersMixed,
            Ambience
        }

        /// <summary>
        /// Stores audio samples loaded from resources, keyed by <see cref="AudioKey"/>.
        /// </summary>
        private static readonly Dictionary<AudioKey, float[]> audioSamples = new();

        /// <summary>
        /// Tracks the load status of audio files, indicating whether each <see cref="AudioKey"/> was successfully loaded.
        /// </summary>
        private static readonly Dictionary<AudioKey, bool> audioLoadStatus = new();

        /// <summary>
        /// Manages active speakers, mapping controller IDs to their respective managed speaker objects.
        /// </summary>
        private static readonly Dictionary<byte, ManagedSpeaker> managedSpeakers = new();

        /// <summary>
        /// Stores available controller IDs for speaker allocation.
        /// </summary>
        private static readonly HashSet<byte> availableControllerIds = new();

        /// <summary>
        /// Synchronization object for thread-safe operations.
        /// </summary>
        private static readonly object lockObject = new();

        /// <summary>
        /// Stores performance metrics for each audio type, keyed by <see cref="AudioKey"/>.
        /// </summary>
        private static readonly Dictionary<AudioKey, PerformanceMetrics> performanceMetrics = new();

        /// <summary>
        /// Indicates whether the AudioManager has been disposed.
        /// </summary>
        private static volatile bool isDisposed = false;

        /// <summary>
        /// Coroutine handle for the periodic health check process.
        /// </summary>
        private static CoroutineHandle healthCheckCoroutine;

        /// <summary>
        /// Indicates whether the AudioManager is initialized.
        /// </summary>
        private static bool isInitialized;

        /// <summary>
        /// Maximum number of concurrent speakers allowed.
        /// </summary>
        private const int MAX_CONCURRENT_SPEAKERS = 50;

        /// <summary>
        /// Minimum controller ID for speaker allocation.
        /// </summary>
        private const byte MIN_CONTROLLER_ID = 101;

        /// <summary>
        /// Maximum controller ID for speaker allocation.
        /// </summary>
        private const byte MAX_CONTROLLER_ID = 199;

        /// <summary>
        /// Controller ID reserved for global ambience playback.
        /// </summary>
        private const byte GLOBAL_AMBIENCE_ID = 157;

        /// <summary>
        /// Maximum lifespan for custom audio playback in seconds (1 hour).
        /// </summary>
        private const float MAX_CUSTOM_LIFESPAN = 3600f;

        /// <summary>
        /// Minimum lifespan for custom audio playback in seconds (100ms).
        /// </summary>
        private const float MIN_CUSTOM_LIFESPAN = 0.1f;

        /// <summary>
        /// Maximum number of recent processing times to store for performance metrics.
        /// </summary>
        private const int MAX_PROCESSING_TIMES = 100;

        /// <summary>
        /// Cooldown for global screams in seconds.
        /// </summary>
        private const float GLOBAL_SCREAM_COOLDOWN = 35f;

        /// <summary>
        /// Gets or sets a value indicating whether the global ambience is currently looping.
        /// </summary>
        public static bool IsLoopingGlobalAmbience { get; set; }

        /// <summary>
        /// Tracks the last time a global scream was played to enforce cooldown.
        /// </summary>
        private static DateTime lastGlobalScreamTime = DateTime.MinValue;

        /// <summary>
        /// Defines the mapping of <see cref="AudioKey"/> values to their respective resource paths.
        /// </summary>
        private static readonly Dictionary<AudioKey, string> AudioFiles = new()
        {
            { AudioKey.Scream, "SCP-575.Shared.Audio.scream.wav" },
            { AudioKey.ScreamAngry, "SCP-575.Shared.Audio.scream-angry.wav" },
            { AudioKey.ScreamDying, "SCP-575.Shared.Audio.scream-dying.wav" },
            { AudioKey.Whispers, "SCP-575.Shared.Audio.whispers.wav" },
            { AudioKey.WhispersBang, "SCP-575.Shared.Audio.whispers-bang.wav" },
            { AudioKey.WhispersMixed, "SCP-575.Shared.Audio.whispers-mixed.wav" },
            { AudioKey.Ambience, "SCP-575.Shared.Audio.ambience.wav" }
        };

        /// <summary>
        /// Represents performance metrics for audio playback.
        /// </summary>
        public class PerformanceMetrics
        {
            /// <summary>
            /// Gets or sets the total number of play requests for the audio.
            /// </summary>
            public int TotalPlayRequests { get; set; }

            /// <summary>
            /// Gets or sets the number of successful play attempts.
            /// </summary>
            public int SuccessfulPlays { get; set; }

            /// <summary>
            /// Gets or sets the number of failed play attempts.
            /// </summary>
            public int FailedPlays { get; set; }

            /// <summary>
            /// Gets or sets the timestamp of the last play attempt.
            /// </summary>
            public DateTime LastUsed { get; set; }

            /// <summary>
            /// Gets or sets the average processing time for play attempts.
            /// </summary>
            public TimeSpan AverageProcessingTime { get; set; }

            /// <summary>
            /// Gets or sets the list of recent processing times for play attempts.
            /// </summary>
            public List<TimeSpan> RecentProcessingTimes { get; set; } = new();
        }

        /// <summary>
        /// Represents a managed speaker with additional tracking information.
        /// </summary>
        public class ManagedSpeaker
        {
            /// <summary>
            /// Gets or sets the speaker object used for audio playback.
            /// </summary>
            public SpeakerToy Speaker { get; set; }

            /// <summary>
            /// Gets or sets the creation timestamp of the speaker.
            /// </summary>
            public DateTime CreatedAt { get; set; }

            /// <summary>
            /// Gets or sets the custom lifespan of the speaker, if specified.
            /// </summary>
            public float? CustomLifespan { get; set; }

            /// <summary>
            /// Gets or sets a value indicating whether the speaker's audio is looped.
            /// </summary>
            public bool IsLooped { get; set; }

            /// <summary>
            /// Gets or sets the coroutine handle for the speaker's cleanup process.
            /// </summary>
            public CoroutineHandle CleanupCoroutine { get; set; }

            /// <summary>
            /// Gets or sets the number of times the speaker has played audio.
            /// </summary>
            public int PlayCount { get; set; }

            /// <summary>
            /// Gets or sets the timestamp of the speaker's last activity.
            /// </summary>
            public DateTime LastActivity { get; set; }

            /// <summary>
            /// Gets or sets a value indicating whether the speaker is corrupted.
            /// </summary>
            public bool IsCorrupted { get; set; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AudioManager"/> class.
        /// </summary>
        static AudioManager()
        {
            isInitialized = Initialize();
        }

        #region Initialization and Disposal

        /// <summary>
        /// Enables the AudioManager, initializing necessary resources.
        /// </summary>
        /// <remarks>
        /// This method should be called before using any other methods in this class.
        /// It is safe to call multiple times; subsequent calls are ignored if already initialized or disposed.
        /// </remarks>
        public static void Enable()
        {
            lock (lockObject)
            {
                if (isInitialized || isDisposed)
                {
                    Library_ExiledAPI.LogWarn("AudioManager.Enable", "AudioManager is already enabled or disposed");
                    return;
                }

                if (!isInitialized) isInitialized = Initialize();

                Library_ExiledAPI.LogInfo("AudioManager.Enable", "AudioManager enabled successfully");
            }
        }

        /// <summary>
        /// Disables the AudioManager, releasing all resources.
        /// </summary>
        /// <remarks>
        /// After calling this method, the AudioManager cannot be used until <see cref="Enable"/> is called again.
        /// It is safe to call multiple times; subsequent calls are ignored if not initialized or already disposed.
        /// </remarks>
        public static void Disable()
        {
            lock (lockObject)
            {
                if (!isInitialized || isDisposed)
                {
                    Library_ExiledAPI.LogWarn("AudioManager.Disable", "AudioManager is not enabled or already disposed");
                    return;
                }
                Dispose();
                isInitialized = false;
                Library_ExiledAPI.LogInfo("AudioManager.Disable", "AudioManager disabled successfully");
            }
        }

        /// <summary>
        /// Initializes the AudioManager's internal state and resources.
        /// </summary>
        /// <remarks>
        /// This method is called by <see cref="Enable"/> and the static constructor.
        /// It should not be called directly.
        /// </remarks>
        private static bool Initialize()
        {
            if (isDisposed) return false;

            for (byte i = MIN_CONTROLLER_ID; i <= MAX_CONTROLLER_ID; i++)
            {
                if (i != GLOBAL_AMBIENCE_ID) availableControllerIds.Add(i);
            }

            foreach (var audioKey in AudioFiles.Keys)
            {
                performanceMetrics[audioKey] = new PerformanceMetrics();
            }

            LoadAudioResources();
            healthCheckCoroutine = Timing.RunCoroutine(HealthCheckCoroutine(), Segment.Update);
            Library_ExiledAPI.LogInfo("AudioManager", "Initialized with automatic management and monitoring");

            return true;
        }

        /// <summary>
        /// Disposes of all resources held by the AudioManager.
        /// </summary>
        /// <remarks>
        /// This method is called by <see cref="Disable"/> and should not be called directly.
        /// After disposal, the AudioManager cannot be used until <see cref="Enable"/> is called.
        /// </remarks>
        public static void Dispose()
        {
            lock (lockObject)
            {
                if (isDisposed) return;
                isDisposed = true;
                Timing.KillCoroutines(healthCheckCoroutine);
                CleanupAllSpeakers();
                audioSamples.Clear();
                audioLoadStatus.Clear();
                performanceMetrics.Clear();
                availableControllerIds.Clear();
                Library_ExiledAPI.LogInfo("AudioManager", "Disposed and cleaned up all resources");
            }
        }

        #endregion

        #region Speaker Management

        /// <summary>
        /// Retrieves an existing speaker or creates a new one with the specified parameters.
        /// </summary>
        /// <param name="controllerId">The controller ID for the speaker.</param>
        /// <param name="position">The position to place the speaker.</param>
        /// <param name="isLooped">Whether the speaker's audio should loop.</param>
        /// <param name="customLifespan">Optional custom lifespan for the speaker in seconds.</param>
        /// <returns>The <see cref="SpeakerToy"/> instance for the specified controller ID.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the speaker cannot be created.</exception>
        private static SpeakerToy GetOrCreateSpeaker(byte controllerId, Vector3 position, bool isLooped, float? customLifespan = null)
        {
            lock (lockObject)
            {
                if (managedSpeakers.TryGetValue(controllerId, out var existingSpeaker) && existingSpeaker?.Speaker?.Base != null)
                {
                    return existingSpeaker.Speaker;
                }

                if (managedSpeakers.ContainsKey(controllerId))
                {
                    CleanupSpeaker(controllerId);
                }

                SpeakerToy speaker = SpeakerToy.Create(position, networkSpawn: true);
                if (speaker == null)
                {
                    Library_ExiledAPI.LogError("GetOrCreateSpeaker", $"Failed to create speaker with controller ID {controllerId}");
                    throw new InvalidOperationException($"Failed to create speaker with controller ID {controllerId}");
                }
                speaker.ControllerId = controllerId;

                var managedSpeaker = new ManagedSpeaker
                {
                    Speaker = speaker,
                    CreatedAt = DateTime.UtcNow,
                    CustomLifespan = customLifespan,
                    IsLooped = isLooped,
                    LastActivity = DateTime.UtcNow
                };

                managedSpeakers[controllerId] = managedSpeaker;
                Library_ExiledAPI.LogDebug("GetOrCreateSpeaker", $"Created speaker {controllerId} at {position}");
                return speaker;
            }
        }

        /// <summary>
        /// Schedules the cleanup of a speaker after a calculated delay.
        /// </summary>
        /// <param name="controllerId">The controller ID of the speaker to clean up.</param>
        /// <param name="audioKey">The <see cref="AudioKey"/> of the audio being played.</param>
        /// <param name="isLooped">Whether the audio is looped.</param>
        /// <param name="customLifespan">Optional custom lifespan for the speaker in seconds.</param>
        private static void ScheduleSpeakerCleanup(byte controllerId, AudioKey audioKey, bool isLooped, float? customLifespan)
        {
            lock (lockObject)
            {
                if (!managedSpeakers.TryGetValue(controllerId, out var managedSpeaker)) return;

                float cleanupDelay = CalculateCleanupDelay(audioKey, isLooped, customLifespan);
                if (cleanupDelay <= 0)
                {
                    Library_ExiledAPI.LogDebug("ScheduleSpeakerCleanup", $"No cleanup scheduled for controller {controllerId} (looped or invalid delay)");
                    return;
                }

                managedSpeaker.CleanupCoroutine = Timing.CallDelayed(cleanupDelay, () => CleanupSpeaker(controllerId));
                Library_ExiledAPI.LogDebug("ScheduleSpeakerCleanup", $"Scheduled cleanup for controller {controllerId} in {cleanupDelay} seconds");
            }
        }

        /// <summary>
        /// Calculates the cleanup delay for a speaker based on audio duration or custom lifespan.
        /// </summary>
        /// <param name="audioKey">The <see cref="AudioKey"/> of the audio being played.</param>
        /// <param name="isLooped">Whether the audio is looped.</param>
        /// <param name="customLifespan">Optional custom lifespan for the speaker in seconds.</param>
        /// <returns>The cleanup delay in seconds, or 0 if no cleanup is needed.</returns>
        private static float CalculateCleanupDelay(AudioKey audioKey, bool isLooped, float? customLifespan)
        {
            if (customLifespan.HasValue)
            {
                return Mathf.Clamp(customLifespan.Value, MIN_CUSTOM_LIFESPAN, MAX_CUSTOM_LIFESPAN);
            }

            if (isLooped) return 0f;

            if (audioSamples.TryGetValue(audioKey, out float[] samples))
            {
                int sampleRate = AudioTransmitter.SampleRate;
                if (sampleRate <= 0)
                {
                    Library_ExiledAPI.LogWarn("CalculateCleanupDelay", "Invalid sample rate, using fallback duration");
                    return 30f;
                }
                return samples.Length / (float)sampleRate + 1f;
            }

            Library_ExiledAPI.LogWarn("CalculateCleanupDelay", $"No samples found for {audioKey}, using fallback duration");
            return 30f;
        }

        /// <summary>
        /// Cleans up a specific speaker, stopping its audio and releasing resources.
        /// </summary>
        /// <param name="controllerId">The controller ID of the speaker to clean up.</param>
        public static void CleanupSpeaker(byte controllerId)
        {
            lock (lockObject)
            {
                if (!managedSpeakers.TryGetValue(controllerId, out var managedSpeaker)) return;

                try
                {
                    SpeakerToy.GetTransmitter(controllerId)?.Stop();
                    managedSpeaker.Speaker?.Destroy();
                    if (managedSpeaker.CleanupCoroutine.IsRunning)
                    {
                        Timing.KillCoroutines(managedSpeaker.CleanupCoroutine);
                    }
                    ReleaseControllerId(controllerId);
                    managedSpeakers.Remove(controllerId);
                    Library_ExiledAPI.LogDebug("CleanupSpeaker", $"Cleaned up speaker {controllerId}");
                }
                catch (Exception ex)
                {
                    Library_ExiledAPI.LogError("CleanupSpeaker", $"Error cleaning up speaker {controllerId}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Cleans up all managed speakers, stopping their audio and releasing resources.
        /// </summary>
        public static void CleanupAllSpeakers()
        {
            lock (lockObject)
            {
                foreach (var controllerId in managedSpeakers.Keys.ToList())
                {
                    CleanupSpeaker(controllerId);
                }
                IsLoopingGlobalAmbience = false;
                Library_ExiledAPI.LogDebug("CleanupAllSpeakers", "All speakers cleaned up");
            }
        }

        /// <summary>
        /// Stops audio playback for a specific speaker without destroying it.
        /// </summary>
        /// <param name="controllerId">The controller ID of the speaker to stop.</param>
        public static void StopSpeaker(byte controllerId)
        {
            lock (lockObject)
            {
                if (!managedSpeakers.TryGetValue(controllerId, out var managedSpeaker)) return;

                try
                {
                    SpeakerToy.GetTransmitter(controllerId)?.Stop();
                    Library_ExiledAPI.LogDebug("StopSpeaker", $"Stopped speaker {controllerId}");
                }
                catch (Exception ex)
                {
                    Library_ExiledAPI.LogError("StopSpeaker", $"Error stopping speaker {controllerId}: {ex.Message}");
                }
            }
        }

        #endregion

        #region Playback Methods

        /// <summary>
        /// Plays an audio clip for a specific player using a specified controller ID.
        /// </summary>
        /// <param name="audioKey">The <see cref="AudioKey"/> of the audio clip to play.</param>
        /// <param name="player">The player to play the audio for.</param>
        /// <param name="customVolume">The volume level (0.0 to 1.0). Defaults to 1.0f.</param>
        /// <param name="customMinDistance">The minimum distance for audio falloff. Defaults to 0.5f.</param>
        /// <param name="customMaxDistance">The maximum distance for audio falloff. Defaults to 15.5f.</param>
        /// <param name="position">The position to play the audio from. If null, uses the player's position.</param>
        /// <param name="controllerId">The controller ID to use for the speaker. Defaults to 1.</param>
        /// <param name="loop">Whether to loop the audio. Defaults to false.</param>
        /// <param name="hearableForAllPlayers">Whether the sound is audible to all players within range. Defaults to false.</param>
        /// <returns>True if the audio was successfully played; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="player"/> is null.</exception>
        public static bool PlayAudio(AudioKey audioKey, Player player, float customVolume = 1.0f, float customMinDistance = 0.5f, float customMaxDistance = 17.75f, Vector3? position = null, byte controllerId = 1, bool loop = false, bool hearableForAllPlayers = false)
        {
            if (player == null) throw new ArgumentNullException(nameof(player), "Player cannot be null.");

            return PlayAudioCore(
                audioKey,
                controllerId,
                position ?? player.Position,
                loop,
                null,
                p => p == player && IsPlayerValidForAudio(p),
                speaker => ConfigureSpeaker(speaker, true, customVolume, customMinDistance, customMaxDistance),
                hearableForAllPlayers
            );
        }

        /// <summary>
        /// Plays the angry scream audio for a specific player with automatic speaker management.
        /// </summary>
        /// <param name="player">The player to play the angry scream for.</param>
        /// <param name="customVolume">The volume level (0.0 to 1.0). Defaults to 1.0f.</param>
        /// <param name="customMinDistance">The minimum distance for audio falloff. Defaults to 0.5f.</param>
        /// <param name="customMaxDistance">The maximum distance for audio falloff. Defaults to 15.5f.</param>
        /// <param name="position">The position to play the audio from. If null, uses the player's position.</param>
        /// <param name="customLifespan">The custom lifespan for the speaker in seconds. Optional.</param>
        /// <param name="hearableForAllPlayers">Whether the sound is audible to all players within range. Defaults to false.</param>
        /// <returns>The controller ID of the speaker used, or null if playback failed.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="player"/> is null.</exception>
        public static byte? PlayAngryScreamAutoManaged(Player player, float customVolume = 1.0f, float customMinDistance = 0.5f, float customMaxDistance = 17.75f, Vector3? position = null, float? customLifespan = null, bool hearableForAllPlayers = false)
        {
            return PlayAudioAutoManaged(AudioKey.ScreamAngry, player, customVolume, customMinDistance, customMaxDistance, position, false, customLifespan, hearableForAllPlayers);
        }

        /// <summary>
        /// Plays the dying scream audio for a specific player with automatic speaker management.
        /// </summary>
        /// <param name="player">The player to play the dying scream for.</param>
        /// <param name="customVolume">The volume level (0.0 to 1.0). Defaults to 1.0f.</param>
        /// <param name="customMinDistance">The minimum distance for audio falloff. Defaults to 0.5f.</param>
        /// <param name="customMaxDistance">The maximum distance for audio falloff. Defaults to 15.5f.</param>
        /// <param name="position">The position to play the audio from. If null, uses the player's position.</param>
        /// <param name="customLifespan">The custom lifespan for the speaker in seconds. Optional.</param>
        /// <param name="hearableForAllPlayers">Whether the sound is audible to all players within range. Defaults to false.</param>
        /// <returns>The controller ID of the speaker used, or null if playback failed.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="player"/> is null.</exception>
        public static byte? PlayDyingScreamAutoManaged(Player player, float customVolume = 1.0f, float customMinDistance = 0.5f, float customMaxDistance = 17.75f, Vector3? position = null, float? customLifespan = null, bool hearableForAllPlayers = false)
        {
            return PlayAudioAutoManaged(AudioKey.ScreamDying, player, customVolume, customMinDistance, customMaxDistance, position, false, customLifespan, hearableForAllPlayers);
        }

        /// <summary>
        /// Plays the whispers audio for a specific player with automatic speaker management.
        /// </summary>
        /// <param name="player">The player to play the whispers for.</param>
        /// <param name="customVolume">The volume level (0.0 to 1.0). Defaults to 1.0f.</param>
        /// <param name="customMinDistance">The minimum distance for audio falloff. Defaults to 0.5f.</param>
        /// <param name="customMaxDistance">The maximum distance for audio falloff. Defaults to 15.5f.</param>
        /// <param name="position">The position to play the audio from. If null, uses the player's position.</param>
        /// <param name="customLifespan">The custom lifespan for the speaker in seconds. Optional.</param>
        /// <param name="hearableForAllPlayers">Whether the sound is audible to all players within range. Defaults to false.</param>
        /// <returns>The controller ID of the speaker used, or null if playback failed.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="player"/> is null.</exception>
        public static byte? PlayWhispersAutoManaged(Player player, float customVolume = 1.0f, float customMinDistance = 0.5f, float customMaxDistance = 17.75f, Vector3? position = null, float? customLifespan = null, bool hearableForAllPlayers = false)
        {
            return PlayAudioAutoManaged(AudioKey.Whispers, player, customVolume, customMinDistance, customMaxDistance, position, false, customLifespan, hearableForAllPlayers);
        }

        /// <summary>
        /// Plays the whispers-bang audio for a specific player with automatic speaker management.
        /// </summary>
        /// <param name="player">The player to play the whispers-bang for.</param>
        /// <param name="customVolume">The volume level (0.0 to 1.0). Defaults to 1.0f.</param>
        /// <param name="customMinDistance">The minimum distance for audio falloff. Defaults to 0.5f.</param>
        /// <param name="customMaxDistance">The maximum distance for audio falloff. Defaults to 15.5f.</param>
        /// <param name="position">The position to play the audio from. If null, uses the player's position.</param>
        /// <param name="customLifespan">The custom lifespan for the speaker in seconds. Optional.</param>
        /// <param name="hearableForAllPlayers">Whether the sound is audible to all players within range. Defaults to false.</param>
        /// <returns>The controller ID of the speaker used, or null if playback failed.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="player"/> is null.</exception>
        public static byte? PlayWhispersBangAutoManaged(Player player, float customVolume = 1.0f, float customMinDistance = 0.5f, float customMaxDistance = 17.75f, Vector3? position = null, float? customLifespan = null, bool hearableForAllPlayers = false)
        {
            return PlayAudioAutoManaged(AudioKey.WhispersBang, player, customVolume, customMinDistance, customMaxDistance, position, false, customLifespan, hearableForAllPlayers);
        }

        /// <summary>
        /// Plays the mixed whispers audio for a specific player with automatic speaker management.
        /// </summary>
        /// <param name="player">The player to play the mixed whispers for.</param>
        /// <param name="customVolume">The volume level (0.0 to 1.0). Defaults to 1.0f.</param>
        /// <param name="customMinDistance">The minimum distance for audio falloff. Defaults to 0.5f.</param>
        /// <param name="customMaxDistance">The maximum distance for audio falloff. Defaults to 15.5f.</param>
        /// <param name="position">The position to play the audio from. If null, uses the player's position.</param>
        /// <param name="customLifespan">The custom lifespan for the speaker in seconds. Optional.</param>
        /// <param name="hearableForAllPlayers">Whether the sound is audible to all players within range. Defaults to false.</param>
        /// <returns>The controller ID of the speaker used, or null if playback failed.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="player"/> is null.</exception>
        public static byte? PlayWhispersMixedAutoManaged(Player player, float customVolume = 1.0f, float customMinDistance = 0.5f, float customMaxDistance = 17.75f, Vector3? position = null, float? customLifespan = null, bool hearableForAllPlayers = false)
        {
            return PlayAudioAutoManaged(AudioKey.WhispersMixed, player, customVolume, customMinDistance, customMaxDistance, position, false, customLifespan, hearableForAllPlayers);
        }

        /// <summary>
        /// Plays a scream audio when an SCP damages or kills a player, either locally or globally based on the event type.
        /// </summary>
        /// <param name="targetPlayer">The player who was damaged or killed.</param>
        /// <param name="isKill">Whether the event is a kill (triggers global scream with cooldown).</param>
        /// <param name="customVolume">The volume level (0.0 to 1.0). Defaults to 1.0f.</param>
        /// <param name="customMinDistance">The minimum distance for audio falloff. Defaults to 0.5f.</param>
        /// <param name="customMaxDistance">The maximum distance for audio falloff. Defaults to 15.5f.</param>
        /// <param name="position">The position to play the scream from. If null, uses the player's position.</param>
        /// <param name="customLifespan">The custom lifespan for the speaker in seconds. Optional.</param>
        /// <returns>The controller ID of the speaker used, or null if playback failed.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="targetPlayer"/> is null.</exception>
        public static byte? PlayDamagedScream(Player targetPlayer, bool isKill, float customVolume = 1.0f, float customMinDistance = 0.5f, float customMaxDistance = 17.75f, Vector3? position = null, float? customLifespan = null)
        {
            if (isDisposed)
            {
                Library_ExiledAPI.LogError("PlayDamagedScream", "AudioManager is disposed");
                return null;
            }

            if (!ValidatePlaybackParameters(AudioKey.Scream, targetPlayer, customLifespan, "PlayDamagedScream"))
            {
                return null;
            }

            if (isKill)
            {
                if (DateTime.UtcNow - lastGlobalScreamTime < TimeSpan.FromSeconds(GLOBAL_SCREAM_COOLDOWN))
                {
                    Library_ExiledAPI.LogDebug("PlayDamagedScream", "Global scream on cooldown");
                    return null;
                }

                bool success = PlayGlobalSound(AudioKey.Scream, customVolume, customMinDistance, customMaxDistance, position ?? targetPlayer.Position, false, customLifespan);
                if (success)
                {
                    lastGlobalScreamTime = DateTime.UtcNow;
                    Library_ExiledAPI.LogDebug("PlayDamagedScream", "Played global scream for kill event");
                }
                return success ? GLOBAL_AMBIENCE_ID : null;
            }
            else
            {
                return PlayAudioAutoManaged(AudioKey.Scream, targetPlayer, customVolume, customMinDistance, customMaxDistance, position, false, customLifespan, hearableForAllPlayers: true);
            }
        }

        /// <summary>
        /// Core method for playing audio with customizable player filtering and speaker configuration.
        /// </summary>
        /// <param name="audioKey">The <see cref="AudioKey"/> of the audio clip to play.</param>
        /// <param name="controllerId">The controller ID for the speaker.</param>
        /// <param name="position">The position to play the audio from.</param>
        /// <param name="isLooped">Whether to loop the audio.</param>
        /// <param name="customLifespan">The custom lifespan for the speaker in seconds. Optional.</param>
        /// <param name="playerFilter">Function to filter valid players for playback.</param>
        /// <param name="configureSpeaker">Action to configure the speaker's properties.</param>
        /// <param name="hearableForAllPlayers">Whether the sound is audible to all players within range.</param>
        /// <returns>True if the audio was successfully played; otherwise, false.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the audio transmission fails.</exception>
        private static bool PlayAudioCore(AudioKey audioKey, byte controllerId, Vector3 position, bool isLooped, float? customLifespan,
            Func<Player, bool> playerFilter, Action<SpeakerToy> configureSpeaker, bool hearableForAllPlayers = false)
        {
            if (isDisposed)
            {
                Library_ExiledAPI.LogError("PlayAudioCore", "AudioManager is disposed");
                return false;
            }

            if (!ValidatePlaybackParameters(audioKey, null, customLifespan, "PlayAudioCore"))
            {
                return false;
            }

            if (controllerId == GLOBAL_AMBIENCE_ID && IsLoopingGlobalAmbience && audioKey != AudioKey.Ambience)
            {
                Library_ExiledAPI.LogWarn("PlayAudioCore", "Cannot override global ambience with a different sound");
                return false;
            }

            var startTime = DateTime.UtcNow;
            lock (lockObject)
            {
                try
                {
                    if (!IsAudioLoaded(audioKey))
                    {
                        return RecordFailure(audioKey, startTime, $"Audio '{audioKey}' not loaded");
                    }

                    SpeakerToy speaker = GetOrCreateSpeaker(controllerId, position, isLooped, customLifespan);
                    configureSpeaker(speaker);

                    var transmitter = SpeakerToy.GetTransmitter(controllerId);
                    if (transmitter == null)
                    {
                        Library_ExiledAPI.LogError("PlayAudioCore", $"Failed to get transmitter for controller {controllerId}");
                        throw new InvalidOperationException($"Failed to get transmitter for controller {controllerId}");
                    }
                    transmitter.ValidPlayers = hearableForAllPlayers
                        ? p => IsPlayerValidForAudio(p) && Vector3.Distance(p.Position, position) <= speaker.MaxDistance
                        : playerFilter;
                    transmitter.Play(audioSamples[audioKey], queue: false, loop: isLooped);

                    if (!isLooped || customLifespan.HasValue)
                    {
                        ScheduleSpeakerCleanup(controllerId, audioKey, isLooped, customLifespan);
                    }

                    Library_ExiledAPI.LogDebug("PlayAudioCore", $"Played {audioKey} on controller {controllerId} (hearableForAllPlayers={hearableForAllPlayers})");
                    RecordMetrics(audioKey, startTime, true);
                    return true;
                }
                catch (Exception ex)
                {
                    Library_ExiledAPI.LogError("PlayAudioCore", $"Failed to play {audioKey}: {ex.Message}");
                    RecordMetrics(audioKey, startTime, false);
                    return false;
                }
            }
        }

        /// <summary>
        /// Plays an audio clip for a specific player with automatic speaker management.
        /// </summary>
        /// <param name="audioKey">The <see cref="AudioKey"/> of the audio clip to play.</param>
        /// <param name="player">The player to play the audio for.</param>
        /// <param name="customVolume">The volume level (0.0 to 1.0). Defaults to 1.0f.</param>
        /// <param name="customMinDistance">The minimum distance for audio falloff. Defaults to 0.5f.</param>
        /// <param name="customMaxDistance">The maximum distance for audio falloff. Defaults to 15.5f.</param>
        /// <param name="position">The position to play the audio from. If null, uses the player's position.</param>
        /// <param name="loop">Whether to loop the audio. Defaults to false.</param>
        /// <param name="customLifespan">The custom lifespan for the speaker in seconds. Optional.</param>
        /// <param name="hearableForAllPlayers">Whether the sound is audible to all players within range. Defaults to false.</param>
        /// <returns>The controller ID of the speaker used, or null if playback failed.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="player"/> is null.</exception>
        public static byte? PlayAudioAutoManaged(AudioKey audioKey, Player player, float customVolume = 1.0f, float customMinDistance = 0.5f, float customMaxDistance = 17.75f, Vector3? position = null, bool loop = false, float? customLifespan = null, bool hearableForAllPlayers = false)
        {
            if (isDisposed)
            {
                Library_ExiledAPI.LogError("PlayAudioAutoManaged", "AudioManager is disposed");
                return null;
            }

            if (!ValidatePlaybackParameters(audioKey, player, customLifespan, "PlayAudioAutoManaged"))
            {
                return null;
            }

            byte? controllerId = AllocateControllerId();
            if (!controllerId.HasValue)
            {
                Library_ExiledAPI.LogError("PlayAudioAutoManaged", "Failed to allocate controller ID");
                return null;
            }

            bool success = PlayAudioCore(
                audioKey,
                controllerId.Value,
                position ?? player.Position,
                loop,
                customLifespan,
                p => p == player && IsPlayerValidForAudio(p),
                speaker => ConfigureSpeaker(speaker, true, customVolume, customMinDistance, customMaxDistance),
                hearableForAllPlayers
            );

            if (!success)
            {
                ReleaseControllerId(controllerId.Value);
                return null;
            }

            return controllerId.Value;
        }

        /// <summary>
        /// Plays the ambience audio for a specific player with automatic speaker management.
        /// </summary>
        /// <param name="player">The player to play the ambience for.</param>
        /// <param name="customVolume">The volume level (0.0 to 1.0). Defaults to 1.0f.</param>
        /// <param name="customMinDistance">The minimum distance for audio falloff. Defaults to 0.5f.</param>
        /// <param name="customMaxDistance">The maximum distance for audio falloff. Defaults to 15.5f.</param>
        /// <param name="position">The position to play the ambience from. If null, uses the player's position.</param>
        /// <param name="loop">Whether to loop the ambience. Defaults to true.</param>
        /// <param name="customLifespan">The custom lifespan for the speaker in seconds. Optional.</param>
        /// <param name="hearableForAllPlayers">Whether the sound is audible to all players within range. Defaults to false.</param>
        /// <returns>The controller ID of the speaker used, or null if playback failed.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="player"/> is null.</exception>
        public static byte? PlayAmbienceAutoManaged(Player player, float customVolume = 1.0f, float customMinDistance = 0.5f, float customMaxDistance = 17.75f, Vector3? position = null, bool loop = true, float? customLifespan = null, bool hearableForAllPlayers = false)
        {
            return PlayAudioAutoManaged(AudioKey.Ambience, player, customVolume, customMinDistance, customMaxDistance, position, loop, customLifespan, hearableForAllPlayers);
        }

        /// <summary>
        /// Plays the scream audio for a specific player with automatic speaker management.
        /// </summary>
        /// <param name="player">The player to play the scream for.</param>
        /// <param name="customVolume">The volume level (0.0 to 1.0). Defaults to 1.0f.</param>
        /// <param name="customMinDistance">The minimum distance for audio falloff. Defaults to 0.5f.</param>
        /// <param name="customMaxDistance">The maximum distance for audio falloff. Defaults to 15.5f.</param>
        /// <param name="position">The position to play the scream from. If null, uses the player's position.</param>
        /// <param name="customLifespan">The custom lifespan for the speaker in seconds. Optional.</param>
        /// <param name="hearableForAllPlayers">Whether the sound is audible to all players within range. Defaults to false.</param>
        /// <returns>The controller ID of the speaker used, or null if playback failed.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="player"/> is null.</exception>
        public static byte? PlayScreamAutoManaged(Player player, float customVolume = 1.0f, float customMinDistance = 0.5f, float customMaxDistance = 17.75f, Vector3? position = null, float? customLifespan = null, bool hearableForAllPlayers = false)
        {
            return PlayAudioAutoManaged(AudioKey.Scream, player, customVolume, customMinDistance, customMaxDistance, position, false, customLifespan, hearableForAllPlayers);
        }

        /// <summary>
        /// Plays an audio clip for a specific player using a specified controller ID (non-managed overload).
        /// </summary>
        /// <param name="audioKey">The <see cref="AudioKey"/> of the audio clip to play.</param>
        /// <param name="player">The player to play the audio for.</param>
        /// <param name="customVolume">The volume level (0.0 to 1.0). Defaults to 1.0f.</param>
        /// <param name="customMinDistance">The minimum distance for audio falloff. Defaults to 0.5f.</param>
        /// <param name="customMaxDistance">The maximum distance for audio falloff. Defaults to 15.5f.</param>
        /// <param name="position">The position to play the audio from. If null, uses the player's position.</param>
        /// <param name="controllerId">The controller ID to use for the speaker. Defaults to 1.</param>
        /// <param name="loop">Whether to loop the audio. Defaults to false.</param>
        /// <returns>True if the audio was successfully played; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="player"/> is null.</exception>
        public static bool PlayAudio(AudioKey audioKey, Player player, float customVolume = 1.0f, float customMinDistance = 0.5f, float customMaxDistance = 17.75f, Vector3? position = null, byte controllerId = 1, bool loop = false)
        {
            return PlayAudio(audioKey, player, customVolume, customMinDistance, customMaxDistance, position, controllerId, loop, false);
        }

        /// <summary>
        /// Plays the ambience audio to a collection of players using a specified controller ID.
        /// </summary>
        /// <param name="targetPlayers">The players to play the ambience for.</param>
        /// <param name="customVolume">The volume level (0.0 to 1.0). Defaults to 0.75f.</param>
        /// <param name="customMinDistance">The minimum distance for audio falloff. Defaults to 0.5f.</param>
        /// <param name="customMaxDistance">The maximum distance for audio falloff. Defaults to 50.0f.</param>
        /// <param name="position">The position to play the ambience from. If null, uses (0,0,0).</param>
        /// <param name="controllerId">The controller ID to use for the speaker. Defaults to 2.</param>
        /// <param name="loop">Whether to loop the ambience. Defaults to true.</param>
        /// <returns>True if the ambience was successfully played; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="targetPlayers"/> is null.</exception>
        public static bool PlayAmbienceToPlayers(IEnumerable<Player> targetPlayers, float customVolume = 0.75f, float customMinDistance = 0.5f, float customMaxDistance = 50.0f, Vector3? position = null, byte controllerId = 2, bool loop = true)
        {
            if (isDisposed)
            {
                Library_ExiledAPI.LogError("PlayAmbienceToPlayers", "AudioManager is disposed");
                return false;
            }

            if (targetPlayers == null)
            {
                Library_ExiledAPI.LogError("PlayAmbienceToPlayers", "Target players cannot be null");
                return false;
            }

            var validPlayers = targetPlayers.Where(IsPlayerValidForAudio).ToHashSet();
            if (!validPlayers.Any())
            {
                Library_ExiledAPI.LogWarn("PlayAmbienceToPlayers", "No valid players to play ambience to");
                return false;
            }

            return PlayAudioCore(
                AudioKey.Ambience,
                controllerId,
                position ?? Vector3.zero,
                loop,
                null,
                p => validPlayers.Contains(p) && IsPlayerValidForAudio(p),
                speaker => ConfigureSpeaker(speaker, position.HasValue, customVolume, customMinDistance, customMaxDistance)
            );
        }

        /// <summary>
        /// Plays a sound globally for all valid players.
        /// </summary>
        /// <param name="audioKey">The <see cref="AudioKey"/> of the audio clip to play.</param>
        /// <param name="customVolume">The volume level (0.0 to 1.0). Defaults to 0.9f.</param>
        /// <param name="customMinDistance">The minimum distance for audio falloff. Defaults to 0.85f.</param>
        /// <param name="customMaxDistance">The maximum distance for audio falloff. Defaults to 1500.0f.</param>
        /// <param name="centralPosition">The central position for the sound. If null, uses (0,0,0).</param>
        /// <param name="loop">Whether to loop the sound. Defaults to false.</param>
        /// <param name="customLifespan">The custom lifespan for the speaker in seconds. Optional.</param>
        /// <param name="controllerId">The specific controller ID to use. If null, allocates a new ID or uses <see cref="GLOBAL_AMBIENCE_ID"/>.</param>
        /// <returns>True if the sound was successfully played; otherwise, false.</returns>
        public static bool PlayGlobalSound(AudioKey audioKey, float customVolume = 0.9f, float customMinDistance = 0.85f, float customMaxDistance = 1500.0f, Vector3? centralPosition = null, bool loop = false, float? customLifespan = null, byte? controllerId = null)
        {
            byte actualControllerId;
            if (controllerId.HasValue)
            {
                actualControllerId = controllerId.Value;
            }
            else
            {
                byte? allocatedId = AllocateControllerId();
                if (allocatedId == null)
                {
                    Library_ExiledAPI.LogWarn("PlayGlobalSound", "No available controller IDs for global sound");
                    return false;
                }
                actualControllerId = allocatedId.Value;
            }
            bool success = PlayAudioCore(
                audioKey,
                actualControllerId,
                centralPosition ?? Vector3.zero,
                loop,
                customLifespan,
                p => IsPlayerValidForAudio(p),
                speaker => ConfigureSpeaker(speaker, false, customVolume, customMinDistance, customMaxDistance)
            );

            if (success && audioKey == AudioKey.Ambience && loop) IsLoopingGlobalAmbience = true;
            return success;
        }

        /// <summary>
        /// Plays the angry scream audio globally for all valid players.
        /// </summary>
        /// <param name="customVolume">The volume level (0.0 to 1.0). Defaults to 0.9f.</param>
        /// <param name="customMinDistance">The minimum distance for audio falloff. Defaults to 0.85f.</param>
        /// <param name="customMaxDistance">The maximum distance for audio falloff. Defaults to 1500.0f.</param>
        /// <param name="centralPosition">The central position for the sound. If null, uses (0,0,0).</param>
        /// <param name="loop">Whether to loop the sound. Defaults to false.</param>
        /// <param name="customLifespan">The custom lifespan for the speaker in seconds. Optional.</param>
        /// <param name="controllerId">The specific controller ID to use. If null, allocates a new ID.</param>
        /// <returns>True if the angry scream was successfully played; otherwise, false.</returns>
        public static bool PlayGlobalAngrySound(float customVolume = 0.9f, float customMinDistance = 0.85f, float customMaxDistance = 1500.0f, Vector3? centralPosition = null, bool loop = false, float? customLifespan = null, byte? controllerId = null)
        {
            Library_ExiledAPI.LogDebug("PlayGlobalAngrySound", "Playing global angry scream audio...");
            return PlayGlobalSound(AudioKey.ScreamAngry, customVolume, customMinDistance, customMaxDistance, centralPosition, loop, customLifespan, controllerId);
        }

        /// <summary>
        /// Plays the dying scream audio globally for all valid players.
        /// </summary>
        /// <param name="customVolume">The volume level (0.0 to 1.0). Defaults to 0.9f.</param>
        /// <param name="customMinDistance">The minimum distance for audio falloff. Defaults to 0.85f.</param>
        /// <param name="customMaxDistance">The maximum distance for audio falloff. Defaults to 1500.0f.</param>
        /// <param name="centralPosition">The central position for the sound. If null, uses (0,0,0).</param>
        /// <param name="loop">Whether to loop the sound. Defaults to false.</param>
        /// <param name="customLifespan">The custom lifespan for the speaker in seconds. Optional.</param>
        /// <param name="controllerId">The specific controller ID to use. If null, allocates a new ID.</param>
        /// <returns>True if the dying scream was successfully played; otherwise, false.</returns>
        public static bool PlayGlobalDyingSound(float customVolume = 0.9f, float customMinDistance = 0.85f, float customMaxDistance = 1500.0f, Vector3? centralPosition = null, bool loop = false, float? customLifespan = null, byte? controllerId = null)
        {
            Library_ExiledAPI.LogDebug("PlayGlobalDyingSound", "Playing global dying scream audio...");
            return PlayGlobalSound(AudioKey.ScreamDying, customVolume, customMinDistance, customMaxDistance, centralPosition, loop, customLifespan, controllerId);
        }

        /// <summary>
        /// Plays the whispers audio globally for all valid players.
        /// </summary>
        /// <param name="customVolume">The volume level (0.0 to 1.0). Defaults to 0.9f.</param>
        /// <param name="customMinDistance">The minimum distance for audio falloff. Defaults to 0.85f.</param>
        /// <param name="customMaxDistance">The maximum distance for audio falloff. Defaults to 1500.0f.</param>
        /// <param name="centralPosition">The central position for the sound. If null, uses (0,0,0).</param>
        /// <param name="loop">Whether to loop the sound. Defaults to false.</param>
        /// <param name="customLifespan">The custom lifespan for the speaker in seconds. Optional.</param>
        /// <param name="controllerId">The specific controller ID to use. If null, allocates a new ID.</param>
        /// <returns>True if the whispers were successfully played; otherwise, false.</returns>
        public static bool PlayGlobalWhispers(float customVolume = 0.9f, float customMinDistance = 0.85f, float customMaxDistance = 1500.0f, Vector3? centralPosition = null, bool loop = false, float? customLifespan = null, byte? controllerId = null)
        {
            Library_ExiledAPI.LogDebug("PlayGlobalWhispers", "Playing global whispers audio...");
            return PlayGlobalSound(AudioKey.Whispers, customVolume, customMinDistance, customMaxDistance, centralPosition, loop, customLifespan, controllerId);
        }

        /// <summary>
        /// Plays the whispers-bang audio globally for all valid players.
        /// </summary>
        /// <param name="customVolume">The volume level (0.0 to 1.0). Defaults to 0.9f.</param>
        /// <param name="customMinDistance">The minimum distance for audio falloff. Defaults to 0.85f.</param>
        /// <param name="customMaxDistance">The maximum distance for audio falloff. Defaults to 1500.0f.</param>
        /// <param name="centralPosition">The central position for the sound. If null, uses (0,0,0).</param>
        /// <param name="loop">Whether to loop the sound. Defaults to false.</param>
        /// <param name="customLifespan">The custom lifespan for the speaker in seconds. Optional.</param>
        /// <param name="controllerId">The specific controller ID to use. If null, allocates a new ID.</param>
        /// <returns>True if the whispers-bang was successfully played; otherwise, false.</returns>
        public static bool PlayGlobalWhispersBang(float customVolume = 0.9f, float customMinDistance = 0.85f, float customMaxDistance = 1500.0f, Vector3? centralPosition = null, bool loop = false, float? customLifespan = null, byte? controllerId = null)
        {
            Library_ExiledAPI.LogDebug("PlayGlobalWhispersBang", "Playing global whispers-bang audio...");
            return PlayGlobalSound(AudioKey.WhispersBang, customVolume, customMinDistance, customMaxDistance, centralPosition, loop, customLifespan, controllerId);
        }

        /// <summary>
        /// Plays the mixed whispers audio globally for all valid players.
        /// </summary>
        /// <param name="customVolume">The volume level (0.0 to 1.0). Defaults to 0.9f.</param>
        /// <param name="customMinDistance">The minimum distance for audio falloff. Defaults to 0.85f.</param>
        /// <param name="customMaxDistance">The maximum distance for audio falloff. Defaults to 1500.0f.</param>
        /// <param name="centralPosition">The central position for the sound. If null, uses (0,0,0).</param>
        /// <param name="loop">Whether to loop the sound. Defaults to false.</param>
        /// <param name="customLifespan">The custom lifespan for the speaker in seconds. Optional.</param>
        /// <param name="controllerId">The specific controller ID to use. If null, allocates a new ID.</param>
        /// <returns>True if the mixed whispers were successfully played; otherwise, false.</returns>
        public static bool PlayGlobalWhispersMixed(float customVolume = 0.9f, float customMinDistance = 0.85f, float customMaxDistance = 1500.0f, Vector3? centralPosition = null, bool loop = false, float? customLifespan = null, byte? controllerId = null)
        {
            Library_ExiledAPI.LogDebug("PlayGlobalWhispersMixed", "Playing global mixed whispers audio...");
            return PlayGlobalSound(AudioKey.WhispersMixed, customVolume, customMinDistance, customMaxDistance, centralPosition, loop, customLifespan, controllerId);
        }

        /// <summary>
        /// Plays the ambience sound globally for all valid players.
        /// </summary>
        /// <param name="customVolume">The volume level (0.0 to 1.0). Defaults to 0.65f.</param>
        /// <param name="customMinDistance">The minimum distance for audio falloff. Defaults to 1.0f.</param>
        /// <param name="customMaxDistance">The maximum distance for audio falloff. Defaults to 1500.5f.</param>
        /// <param name="centralPosition">The central position for the ambience. If null, uses (0,0,0).</param>
        /// <param name="loop">Whether to loop the ambience. Defaults to true.</param>
        /// <param name="customLifespan">The custom lifespan for the speaker in seconds. Optional.</param>
        /// <returns>True if the ambience was successfully played; otherwise, false.</returns>
        public static bool PlayGlobalAmbience(bool isBlackoutActive = true, float customVolume = 0.65f, float customMinDistance = 1.0f, float customMaxDistance = 1500.5f, Vector3? centralPosition = null, bool loop = true, float? customLifespan = null)
        {
            if (IsLoopingGlobalAmbience)
            {
                Library_ExiledAPI.LogWarn("PlayGlobalAmbience", "Global ambience already playing");
                return false;
            }

            bool success = PlayAudioCore(
                AudioKey.Ambience,
                GLOBAL_AMBIENCE_ID,
                centralPosition ?? Vector3.zero,
                loop,
                customLifespan,
                p => isBlackoutActive && IsPlayerValidForAudio(p) && Library_LabAPI.IsPlayerInDarkRoom(Library_LabAPI.GetPlayer(p.ReferenceHub)),
                speaker => ConfigureSpeaker(speaker, false, customVolume, customMinDistance, customMaxDistance)
            );

            if (success && loop) IsLoopingGlobalAmbience = true;
            return success;
        }

        /// <summary>
        /// Stops the global ambience if it is currently playing.
        /// </summary>
        public static void StopGlobalAmbience()
        {
            CleanupSpeaker(GLOBAL_AMBIENCE_ID);
            IsLoopingGlobalAmbience = false;
            Library_ExiledAPI.LogDebug("StopGlobalAmbience", "Global ambience stopped");
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Allocates a controller ID for a new speaker.
        /// </summary>
        /// <returns>The allocated controller ID, or null if none are available.</returns>
        private static byte? AllocateControllerId()
        {
            lock (lockObject)
            {
                if (managedSpeakers.Count >= MAX_CONCURRENT_SPEAKERS)
                {
                    Library_ExiledAPI.LogWarn("AllocateControllerId", $"Maximum speaker limit ({MAX_CONCURRENT_SPEAKERS}) reached");
                    return null;
                }
                if (availableControllerIds.Count == 0)
                {
                    Library_ExiledAPI.LogWarn("AllocateControllerId", "No available controller IDs");
                    return null;
                }
                byte controllerId = availableControllerIds.First();
                availableControllerIds.Remove(controllerId);
                Library_ExiledAPI.LogDebug("AllocateControllerId", $"Allocated controller ID {controllerId}");
                return controllerId;
            }
        }

        /// <summary>
        /// Releases a controller ID back to the pool of available IDs.
        /// </summary>
        /// <param name="controllerId">The controller ID to release.</param>
        private static void ReleaseControllerId(byte controllerId)
        {
            lock (lockObject)
            {
                if (controllerId != GLOBAL_AMBIENCE_ID && controllerId >= MIN_CONTROLLER_ID && controllerId <= MAX_CONTROLLER_ID)
                {
                    availableControllerIds.Add(controllerId);
                    Library_ExiledAPI.LogDebug("ReleaseControllerId", $"Released controller ID {controllerId}");
                }
            }
        }

        /// <summary>
        /// Validates playback parameters, including audio key, player, and custom lifespan.
        /// </summary>
        /// <param name="audioKey">The <see cref="AudioKey"/> to validate.</param>
        /// <param name="player">The player to validate, or null if not applicable.</param>
        /// <param name="customLifespan">The custom lifespan to validate, if specified.</param>
        /// <param name="methodName">The name of the calling method for logging purposes.</param>
        /// <returns>True if all parameters are valid; otherwise, false.</returns>
        private static bool ValidatePlaybackParameters(AudioKey audioKey, Player player, float? customLifespan, string methodName)
        {
            if (!Enum.IsDefined(typeof(AudioKey), audioKey))
            {
                Library_ExiledAPI.LogError(methodName, $"Unknown audio key: {audioKey}");
                return false;
            }
            if (player != null && !IsPlayerValidForAudio(player))
            {
                Library_ExiledAPI.LogWarn(methodName, $"Player {player.Nickname} is not valid for audio");
                return false;
            }
            if (customLifespan.HasValue && (customLifespan < MIN_CUSTOM_LIFESPAN || customLifespan > MAX_CUSTOM_LIFESPAN))
            {
                Library_ExiledAPI.LogError(methodName,
                    $"Custom lifespan {customLifespan.Value} is outside valid range ({MIN_CUSTOM_LIFESPAN}-{MAX_CUSTOM_LIFESPAN})");
                return false;
            }
            return true;
        }

        /// <summary>
        /// Checks if a player is valid for audio playback.
        /// </summary>
        /// <param name="player">The player to validate.</param>
        /// <returns>True if the player is valid; otherwise, false.</returns>
        private static bool IsPlayerValidForAudio(Player player)
        {
            return player != null && player.IsAlive && player.ReferenceHub?.connectionToClient?.isAuthenticated == true;
        }

        /// <summary>
        /// Configures a speaker with the specified properties.
        /// </summary>
        /// <param name="speaker">The speaker to configure.</param>
        /// <param name="isSpatial">Whether the audio should be spatialized.</param>
        /// <param name="volume">The volume level (0.0 to 1.0).</param>
        /// <param name="minDistance">The minimum distance for audio falloff.</param>
        /// <param name="maxDistance">The maximum distance for audio falloff.</param>
        private static void ConfigureSpeaker(SpeakerToy speaker, bool isSpatial, float volume, float minDistance, float maxDistance)
        {
            speaker.IsSpatial = isSpatial;
            speaker.Volume = volume;
            speaker.MinDistance = minDistance;
            speaker.MaxDistance = maxDistance;
            Library_ExiledAPI.LogDebug("ConfigureSpeaker", $"Configured speaker: spatial={isSpatial}, volume={volume}, minDistance={minDistance}, maxDistance={maxDistance}");
        }

        /// <summary>
        /// Checks if a specific audio clip has been loaded.
        /// </summary>
        /// <param name="audioKey">The <see cref="AudioKey"/> of the audio clip to check.</param>
        /// <returns>True if the audio is loaded; otherwise, false.</returns>
        public static bool IsAudioLoaded(AudioKey audioKey)
        {
            return audioLoadStatus.ContainsKey(audioKey) && audioLoadStatus[audioKey] && audioSamples[audioKey] != null;
        }

        /// <summary>
        /// Loads audio resources from embedded WAV files.
        /// </summary>
        private static void LoadAudioResources()
        {
            foreach (var audio in AudioFiles)
            {
                try
                {
                    using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(audio.Value))
                    {
                        if (stream == null)
                        {
                            Library_ExiledAPI.LogError("LoadAudioResources", $"Resource {audio.Value} not found");
                            audioLoadStatus[audio.Key] = false;
                            continue;
                        }
                        using (BinaryReader reader = new BinaryReader(stream))
                        {
                            reader.BaseStream.Seek(44, SeekOrigin.Begin);
                            byte[] rawData = reader.ReadBytes((int)(stream.Length - 44));
                            audioSamples[audio.Key] = ConvertToFloatArray(rawData);
                            audioLoadStatus[audio.Key] = true;
                            Library_ExiledAPI.LogDebug("LoadAudioResources", $"Loaded {audio.Key} with {audioSamples[audio.Key].Length} samples");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Library_ExiledAPI.LogError("LoadAudioResources", $"Failed to load {audio.Key}: {ex.Message}");
                    audioLoadStatus[audio.Key] = false;
                }
            }
        }

        /// <summary>
        /// Converts raw 16-bit PCM audio data to a float array normalized to [-1, 1].
        /// </summary>
        /// <param name="rawData">The raw audio data in 16-bit PCM format.</param>
        /// <returns>A float array representing the normalized audio samples.</returns>
        private static float[] ConvertToFloatArray(byte[] rawData)
        {
            float[] samples = new float[rawData.Length / 2];
            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] = BitConverter.ToInt16(rawData, i * 2) / 32768f;
            }
            return samples;
        }

        #endregion

        #region Monitoring and Metrics

        /// <summary>
        /// Periodically checks the health of managed speakers and cleans up corrupted or stale ones.
        /// </summary>
        /// <returns>An enumerator for the coroutine.</returns>
        private static IEnumerator<float> HealthCheckCoroutine()
        {
            while (!isDisposed)
            {
                yield return Timing.WaitForSeconds(30f);
                try
                {
                    PerformHealthCheck();
                    CleanupOrphanedSpeakers();
                }
                catch (Exception ex)
                {
                    Library_ExiledAPI.LogError("HealthCheckCoroutine", $"Health check failed: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Performs a health check on all managed speakers, marking corrupted ones for cleanup.
        /// </summary>
        private static void PerformHealthCheck()
        {
            lock (lockObject)
            {
                var corrupted = managedSpeakers
                    .Where(kvp => kvp.Value.Speaker?.Base == null || DateTime.UtcNow - kvp.Value.LastActivity > TimeSpan.FromMinutes(10))
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var id in corrupted)
                {
                    Library_ExiledAPI.LogWarn("PerformHealthCheck", $"Cleaning up corrupted or stale speaker {id}");
                    CleanupSpeaker(id);
                }
            }
        }

        /// <summary>
        /// Cleans up orphaned speakers and old performance metrics.
        /// </summary>
        private static void CleanupOrphanedSpeakers()
        {
            lock (lockObject)
            {
                var orphaned = managedSpeakers
                    .Where(kvp => DateTime.UtcNow - kvp.Value.LastActivity > TimeSpan.FromMinutes(30) && !SpeakerToy.GetTransmitter(kvp.Key).IsPlaying)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var id in orphaned)
                {
                    Library_ExiledAPI.LogInfo("CleanupOrphanedSpeakers", $"Cleaning up orphaned speaker {id}");
                    CleanupSpeaker(id);
                }
            }
        }

        /// <summary>
        /// Records a failed audio playback attempt with the specified reason.
        /// </summary>
        /// <param name="audioKey">The <see cref="AudioKey"/> of the audio clip.</param>
        /// <param name="startTime">The start time of the playback attempt.</param>
        /// <param name="reason">The reason for the failure.</param>
        /// <returns>False to indicate failure.</returns>
        private static bool RecordFailure(AudioKey audioKey, DateTime startTime, string reason)
        {
            Library_ExiledAPI.LogWarn("PlayAudioCore", reason);
            RecordMetrics(audioKey, startTime, false);
            return false;
        }

        /// <summary>
        /// Records performance metrics for an audio playback attempt.
        /// </summary>
        /// <param name="audioKey">The <see cref="AudioKey"/> of the audio clip.</param>
        /// <param name="startTime">The start time of the playback attempt.</param>
        /// <param name="success">Whether the playback was successful.</param>
        private static void RecordMetrics(AudioKey audioKey, DateTime startTime, bool success)
        {
            if (!performanceMetrics.TryGetValue(audioKey, out var metrics)) return;
            lock (lockObject)
            {
                metrics.TotalPlayRequests++;
                metrics.LastUsed = DateTime.UtcNow;
                if (success) metrics.SuccessfulPlays++; else metrics.FailedPlays++;

                var processingTime = DateTime.UtcNow - startTime;
                metrics.RecentProcessingTimes.Add(processingTime);
                if (metrics.RecentProcessingTimes.Count > MAX_PROCESSING_TIMES)
                {
                    metrics.RecentProcessingTimes.RemoveAt(0);
                }
                metrics.AverageProcessingTime = TimeSpan.FromMilliseconds(metrics.RecentProcessingTimes.Average(t => t.TotalMilliseconds));
            }
        }

        /// <summary>
        /// Gets the number of currently active speakers.
        /// </summary>
        /// <returns>The number of active speakers.</returns>
        public static int GetActiveSpeakerCount()
        {
            lock (lockObject)
            {
                return managedSpeakers.Count;
            }
        }

        /// <summary>
        /// Gets the number of available controller IDs.
        /// </summary>
        /// <returns>The number of available controller IDs.</returns>
        public static int GetAvailableControllerCount()
        {
            lock (lockObject)
            {
                return availableControllerIds.Count;
            }
        }

        /// <summary>
        /// Gets the status of all managed speakers.
        /// </summary>
        /// <returns>A dictionary containing speaker status information, keyed by controller ID.</returns>
        public static Dictionary<byte, (DateTime CreatedAt, bool IsLooped, float? CustomLifespan)> GetSpeakerStatus()
        {
            lock (lockObject)
            {
                return managedSpeakers.ToDictionary(
                    kvp => kvp.Key,
                    kvp => (kvp.Value.CreatedAt, kvp.Value.IsLooped, kvp.Value.CustomLifespan)
                );
            }
        }

        /// <summary>
        /// Retrieves the performance metrics for all audio types.
        /// </summary>
        /// <returns>A dictionary of performance metrics, keyed by <see cref="AudioKey"/>.</returns>
        public static Dictionary<AudioKey, PerformanceMetrics> GetPerformanceMetrics()
        {
            lock (lockObject)
            {
                return new Dictionary<AudioKey, PerformanceMetrics>(performanceMetrics);
            }
        }

        #endregion
    }
}