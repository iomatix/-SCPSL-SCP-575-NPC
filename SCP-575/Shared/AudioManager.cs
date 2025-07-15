namespace SCP_575.Shared
{
    using LabApi.Features.Audio;
    using LabApi.Features.Wrappers;
    using MEC;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using UnityEngine;

    /// <summary>
    /// Manages audio playback for SCP: Secret Laboratory using LabAPI.
    /// Provides methods to play sounds locally and globally, manage speakers,
    /// and monitor performance.
    /// </summary>
    public static class AudioManager
    {
        private static readonly Dictionary<string, float[]> audioSamples = new();
        private static readonly Dictionary<string, bool> audioLoadStatus = new();
        private static readonly Dictionary<byte, ManagedSpeaker> managedSpeakers = new();
        private static readonly HashSet<byte> availableControllerIds = new();
        private static readonly object lockObject = new();
        private static readonly Dictionary<string, PerformanceMetrics> performanceMetrics = new();
        private static volatile bool isDisposed = false;
        private static CoroutineHandle healthCheckCoroutine;
        private static bool isInitialized;

        private const int MAX_CONCURRENT_SPEAKERS = 50;
        private const byte MIN_CONTROLLER_ID = 101;
        private const byte MAX_CONTROLLER_ID = 199;
        private const byte GLOBAL_AMBIENCE_ID = 157;
        private const float MAX_CUSTOM_LIFESPAN = 3600f; // 1 hour max
        private const float MIN_CUSTOM_LIFESPAN = 0.1f; // 100ms min
        private const int MAX_PROCESSING_TIMES = 100; // Limit for RecentProcessingTimes

        public static bool IsLoopingGlobalAmbience = false;

        private static readonly Dictionary<string, string> AudioFiles = new()
        {
            { "scream", "SCP-575.Shared.Audio.scream.wav" },
            { "scream-angry", "SCP-575.Shared.Audio.scream-angry.wav" },
            { "ambience", "SCP-575.Shared.Audio.ambience.wav" },
        };

        /// <summary>
        /// Represents performance metrics for audio playback.
        /// </summary>
        public class PerformanceMetrics
        {
            public int TotalPlayRequests { get; set; }
            public int SuccessfulPlays { get; set; }
            public int FailedPlays { get; set; }
            public DateTime LastUsed { get; set; }
            public TimeSpan AverageProcessingTime { get; set; }
            public List<TimeSpan> RecentProcessingTimes { get; set; } = new();
        }

        /// <summary>
        /// Represents a managed speaker with additional tracking information.
        /// </summary>
        public class ManagedSpeaker
        {
            public SpeakerToy Speaker { get; set; }
            public DateTime CreatedAt { get; set; }
            public float? CustomLifespan { get; set; }
            public bool IsLooped { get; set; }
            public CoroutineHandle CleanupCoroutine { get; set; }
            public int PlayCount { get; set; }
            public DateTime LastActivity { get; set; }
            public bool IsCorrupted { get; set; }
        }

        static AudioManager()
        {
            Initialize();
        }

        #region Initialization and Disposal

        /// <summary>
        /// Enables the AudioManager, initializing necessary resources.
        /// </summary>
        /// <remarks>
        /// This method should be called before using any other methods in this class.
        /// It is safe to call this method multiple times; subsequent calls will be ignored.
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

                Initialize();
                isInitialized = true;
                Library_ExiledAPI.LogInfo("AudioManager.Enable", "AudioManager enabled successfully");
            }
        }

        /// <summary>
        /// Disables the AudioManager, releasing all resources.
        /// </summary>
        /// <remarks>
        /// After calling this method, the AudioManager cannot be used until Enable is called again.
        /// It is safe to call this method multiple times; subsequent calls will be ignored.
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
        /// This method is called by Enable and should not be called directly.
        /// </remarks>
        private static void Initialize()
        {
            if (isDisposed) return;

            for (byte i = MIN_CONTROLLER_ID; i <= MAX_CONTROLLER_ID; i++)
            {
                if (i != GLOBAL_AMBIENCE_ID)
                    availableControllerIds.Add(i);
            }

            foreach (var audioKey in AudioFiles.Keys)
            {
                performanceMetrics[audioKey] = new PerformanceMetrics();
            }

            LoadAudioResources();
            healthCheckCoroutine = Timing.RunCoroutine(HealthCheckCoroutine(), Segment.Update);
            Library_ExiledAPI.LogInfo("AudioManager", "Initialized with automatic management and monitoring");
        }

        /// <summary>
        /// Disposes of all resources held by the AudioManager.
        /// </summary>
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
        /// Creates a new managed speaker with the specified parameters.
        /// </summary>
        /// <exceptions>
        /// <exception cref="Exception">Thrown if speaker creation fails.</exception>
        /// </exceptions>
        private static SpeakerToy CreateManagedSpeaker(byte controllerId, Vector3 position, bool isLooped, float? customLifespan = null)
        {
            if (managedSpeakers.ContainsKey(controllerId))
            {
                CleanupSpeaker(controllerId);
            }

            try
            {
                SpeakerToy speaker = SpeakerToy.Create(position, networkSpawn: true);
                speaker.ControllerId = controllerId;
                speaker.Volume = 1.0f;
                speaker.IsSpatial = true;
                speaker.MinDistance = 1.0f;
                speaker.MaxDistance = 15.0f;

                var managedSpeaker = new ManagedSpeaker
                {
                    Speaker = speaker,
                    CreatedAt = DateTime.UtcNow,
                    CustomLifespan = customLifespan,
                    IsLooped = isLooped,
                    CleanupCoroutine = default
                };

                managedSpeakers[controllerId] = managedSpeaker;
                Library_ExiledAPI.LogDebug("CreateManagedSpeaker", $"Created managed speaker {controllerId} at {position}");
                return speaker;
            }
            catch (Exception ex)
            {
                Library_ExiledAPI.LogError("CreateManagedSpeaker", $"Failed to create speaker {controllerId}: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Ensures that a speaker with the given controller ID exists at the specified position.
        /// </summary>
        /// <param name="controllerId">The controller ID of the speaker.</param>
        /// <param name="position">The position to place the speaker if it needs to be created.</param>
        /// <returns>The existing or newly created SpeakerToy instance.</returns>
        private static SpeakerToy EnsureSpeakerExists(byte controllerId, Vector3 position)
        {
            lock (lockObject)
            {
                if (managedSpeakers.TryGetValue(controllerId, out ManagedSpeaker existingSpeaker) &&
                    existingSpeaker?.Speaker?.Base != null)
                {
                    return existingSpeaker.Speaker;
                }

                if (managedSpeakers.ContainsKey(controllerId))
                {
                    CleanupSpeaker(controllerId);
                }

                return CreateManagedSpeaker(controllerId, position, false);
            }
        }

        /// <summary>
        /// Schedules the cleanup of a speaker after a certain delay.
        /// </summary>
        /// <param name="controllerId">The controller ID of the speaker to clean up.</param>
        /// <param name="audioKey">The key of the audio being played.</param>
        /// <param name="isLooped">Whether the audio is looped.</param>
        /// <param name="customLifespan">Optional custom lifespan for the speaker.</param>
        private static void ScheduleCleanup(byte controllerId, string audioKey, bool isLooped, float? customLifespan)
        {
            lock (lockObject)
            {
                if (!managedSpeakers.TryGetValue(controllerId, out var managedSpeaker))
                    return;

                if (customLifespan.HasValue && customLifespan.Value < 0)
                {
                    Library_ExiledAPI.LogWarn("ScheduleCleanup", $"Invalid negative lifespan {customLifespan.Value} for controller {controllerId}");
                    return;
                }

                float cleanupDelay;

                if (customLifespan.HasValue)
                {
                    cleanupDelay = customLifespan.Value;
                }
                else if (isLooped)
                {
                    Library_ExiledAPI.LogDebug("ScheduleCleanup", $"Looped audio on controller {controllerId} - no auto-cleanup scheduled");
                    return;
                }
                else
                {
                    if (audioSamples.TryGetValue(audioKey, out float[] samples))
                    {
                        int sampleRate = AudioTransmitter.SampleRate;
                        if (sampleRate <= 0)
                        {
                            Library_ExiledAPI.LogError("ScheduleCleanup", "Invalid sample rate, using fallback duration");
                            cleanupDelay = 30.0f;
                        }
                        else
                        {
                            cleanupDelay = samples.Length / (float)sampleRate + 1.0f;
                        }
                    }
                    else
                    {
                        cleanupDelay = 30.0f;
                    }
                }

                managedSpeaker.CleanupCoroutine = Timing.CallDelayed(cleanupDelay, () => CleanupSpeaker(controllerId));
                Library_ExiledAPI.LogDebug("ScheduleCleanup", $"Scheduled cleanup for controller {controllerId} in {cleanupDelay} seconds");
            }
        }

        /// <summary>
        /// Cleans up a specific speaker, stopping its audio and releasing resources.
        /// </summary>
        /// <param name="controllerId">The controller ID of the speaker to clean up.</param>
        public static void CleanupSpeaker(byte controllerId)
        {
            lock (lockObject)
            {
                if (!managedSpeakers.TryGetValue(controllerId, out var managedSpeaker))
                    return;

                try
                {
                    var transmitter = SpeakerToy.GetTransmitter(controllerId);
                    transmitter.Stop();

                    if (managedSpeaker.Speaker?.Base != null)
                    {
                        managedSpeaker.Speaker.Destroy();
                    }

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
                var controllerIds = managedSpeakers.Keys.ToList();
                foreach (var controllerId in controllerIds)
                {
                    CleanupSpeaker(controllerId);
                }

                IsLoopingGlobalAmbience = false;
            }
        }

        /// <summary>
        /// Stops the audio transmission for a specific speaker without destroying it.
        /// </summary>
        /// <param name="controllerId">The controller ID of the speaker to stop.</param>
        public static void StopSpeaker(byte controllerId)
        {
            lock (lockObject)
            {
                if (managedSpeakers.TryGetValue(controllerId, out var managedSpeaker))
                {
                    try
                    {
                        var transmitter = SpeakerToy.GetTransmitter(controllerId);
                        transmitter.Stop();
                        Library_ExiledAPI.LogDebug("StopSpeaker", $"Stopped speaker {controllerId}");
                    }
                    catch (Exception ex)
                    {
                        Library_ExiledAPI.LogError("StopSpeaker", $"Error stopping speaker {controllerId}: {ex.Message}");
                    }
                }
            }
        }

        #endregion

        #region Playback Methods

        /// <summary>
        /// Plays an audio clip for a specific player with automatic speaker management.
        /// </summary>
        /// <param name="audioKey">The key of the audio clip to play.</param>
        /// <param name="player">The player to play the audio for.</param>
        /// <param name="position">Optional position to play the audio from. If null, uses the player's position.</param>
        /// <param name="loop">Whether to loop the audio.</param>
        /// <param name="customLifespan">Optional custom lifespan for the speaker in seconds.</param>
        /// <returns>The controller ID of the speaker used, or null if playback failed.</returns>
        public static byte? PlayAudioAutoManaged(string audioKey, Player player, Vector3? position = null, bool loop = false, float? customLifespan = null)
        {
            if (isDisposed)
            {
                Library_ExiledAPI.LogError("PlayAudioAutoManaged", "AudioManager is disposed");
                return null;
            }

            var startTime = DateTime.UtcNow;

            try
            {
                if (!ValidateAudioKey(audioKey, "PlayAudioAutoManaged") ||
                    !ValidatePlayer(player, "PlayAudioAutoManaged") ||
                    !ValidateCustomLifespan(customLifespan, "PlayAudioAutoManaged"))
                {
                    RecordMetrics(audioKey, startTime, false);
                    return null;
                }

                byte? controllerId = AllocateControllerId();
                if (!controllerId.HasValue)
                {
                    Library_ExiledAPI.LogError("PlayAudioAutoManaged", "Failed to allocate controller ID");
                    RecordMetrics(audioKey, startTime, false);
                    return null;
                }

                if (PlayAudioInternal(audioKey, player, position, controllerId.Value, loop))
                {
                    ScheduleCleanup(controllerId.Value, audioKey, loop, customLifespan);
                    RecordMetrics(audioKey, startTime, true);
                    return controllerId.Value;
                }
                else
                {
                    ReleaseControllerId(controllerId.Value);
                    RecordMetrics(audioKey, startTime, false);
                    return null;
                }
            }
            catch (Exception ex)
            {
                Library_ExiledAPI.LogError("PlayAudioAutoManaged", $"Unexpected error: {ex.Message}");
                RecordMetrics(audioKey, startTime, false);
                return null;
            }
        }

        /// <summary>
        /// Plays the ambience audio for a specific player with automatic speaker management.
        /// </summary>
        /// <param name="player">The player to play the ambience for.</param>
        /// <param name="position">Optional position to play the ambience from.</param>
        /// <param name="loop">Whether to loop the ambience. Default is true.</param>
        /// <param name="customLifespan">Optional custom lifespan for the speaker.</param>
        /// <returns>The controller ID of the speaker used, or null if playback failed.</returns>
        public static byte? PlayAmbienceAutoManaged(Player player, Vector3? position = null, bool loop = true, float? customLifespan = null)
        {
            return PlayAudioAutoManaged("ambience", player, position, loop, customLifespan);
        }

        /// <summary>
        /// Plays the scream audio for a specific player with automatic speaker management.
        /// </summary>
        /// <param name="player">The player to play the scream for.</param>
        /// <param name="position">Optional position to play the scream from.</param>
        /// <param name="customLifespan">Optional custom lifespan for the speaker.</param>
        /// <returns>The controller ID of the speaker used, or null if playback failed.</returns>
        public static byte? PlayScreamAutoManaged(Player player, Vector3? position = null, float? customLifespan = null)
        {
            return PlayAudioAutoManaged("scream", player, position, false, customLifespan);
        }

        /// <summary>
        /// Plays an audio clip for a specific player using a specified controller ID.
        /// </summary>
        /// <param name="audioKey">The key of the audio clip to play.</param>
        /// <param name="player">The player to play the audio for.</param>
        /// <param name="position">Optional position to play the audio from.</param>
        /// <param name="controllerId">The controller ID to use for the speaker.</param>
        /// <param name="loop">Whether to loop the audio.</param>
        /// <returns>True if the audio was successfully played, false otherwise.</returns>
        public static bool PlayAudio(string audioKey, Player player, Vector3? position = null, byte controllerId = 1, bool loop = false)
        {
            if (isDisposed)
            {
                Library_ExiledAPI.LogError("PlayAudio", "AudioManager is disposed");
                return false;
            }

            var startTime = DateTime.UtcNow;

            lock (lockObject)
            {
                try
                {
                    if (!ValidateAudioKey(audioKey, "PlayAudio") ||
                        !ValidatePlayer(player, "PlayAudio") ||
                        !ValidateControllerId(controllerId, "PlayAudio"))
                    {
                        RecordMetrics(audioKey, startTime, false);
                        return false;
                    }

                    if (!IsAudioLoaded(audioKey))
                    {
                        Library_ExiledAPI.LogWarn("PlayAudio", $"Audio '{audioKey}' not loaded");
                        RecordMetrics(audioKey, startTime, false);
                        return false;
                    }

                    SpeakerToy speaker = EnsureSpeakerExists(controllerId, position ?? player.Position);
                    var transmitter = SpeakerToy.GetTransmitter(controllerId);
                    transmitter.ValidPlayers = p => p != null && p == player && IsPlayerValidForAudio(p);
                    transmitter.Play(audioSamples[audioKey], queue: false, loop: loop);

                    Library_ExiledAPI.LogDebug("PlayAudio", $"Successfully played {audioKey} to {player.Nickname} using controller {controllerId}");
                    RecordMetrics(audioKey, startTime, true);
                    return true;
                }
                catch (Exception ex)
                {
                    Library_ExiledAPI.LogError("PlayAudio", $"Failed to play {audioKey} sound: {ex.Message}");
                    RecordMetrics(audioKey, startTime, false);
                    return false;
                }
            }
        }

        /// <summary>
        /// Plays the ambience audio to a collection of players using a specified controller ID.
        /// </summary>
        /// <param name="targetPlayers">The players to play the ambience for.</param>
        /// <param name="position">Optional position to play the ambience from.</param>
        /// <param name="controllerId">The controller ID to use for the speaker.</param>
        /// <param name="loop">Whether to loop the ambience.</param>
        /// <returns>True if the ambience was successfully played, false otherwise.</returns>
        public static bool PlayAmbienceToPlayers(IEnumerable<Player> targetPlayers, Vector3? position = null, byte controllerId = 2, bool loop = true)
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

            if (!ValidateControllerId(controllerId, "PlayAmbienceToPlayers"))
                return false;

            var startTime = DateTime.UtcNow;

            try
            {
                if (!IsAudioLoaded("ambience"))
                {
                    Library_ExiledAPI.LogWarn("PlayAmbienceToPlayers", "Ambience audio not loaded");
                    RecordMetrics("ambience", startTime, false);
                    return false;
                }

                var validPlayers = targetPlayers.Where(IsPlayerValidForAudio).ToList();
                if (!validPlayers.Any())
                {
                    Library_ExiledAPI.LogWarn("PlayAmbienceToPlayers", "No valid players to play ambience to");
                    RecordMetrics("ambience", startTime, false);
                    return false;
                }

                Vector3 speakerPosition = position ?? Vector3.zero;
                SpeakerToy speaker = EnsureSpeakerExists(controllerId, speakerPosition);

                speaker.Volume = 0.95f;
                speaker.IsSpatial = position.HasValue;
                speaker.MinDistance = 1.0f;
                speaker.MaxDistance = 50.0f;

                var transmitter = SpeakerToy.GetTransmitter(controllerId);
                var playerSet = new HashSet<Player>(validPlayers);
                transmitter.ValidPlayers = p => p != null && playerSet.Contains(p) && IsPlayerValidForAudio(p);
                transmitter.Play(audioSamples["ambience"], queue: false, loop: loop);

                Library_ExiledAPI.LogDebug("PlayAmbienceToPlayers", $"Started ambience for {validPlayers.Count} players on controller {controllerId}");
                RecordMetrics("ambience", startTime, true);
                return true;
            }
            catch (Exception ex)
            {
                Library_ExiledAPI.LogError("PlayAmbienceToPlayers", $"Failed to play ambience to players: {ex.Message}");
                RecordMetrics("ambience", startTime, false);
                return false;
            }
        }

        /// <summary>
        /// Plays a sound globally for all valid players.
        /// </summary>
        /// <param name="audioKey">The key of the audio clip to play.</param>
        /// <param name="centralPosition">Optional central position for the sound.</param>
        /// <param name="loop">Whether to loop the sound.</param>
        /// <param name="customLifespan">Optional custom lifespan for the speaker.</param>
        /// <param name="controllerId">Optional specific controller ID to use.</param>
        /// <returns>True if the sound was successfully played, false otherwise.</returns>
        public static bool PlayGlobalSound(string audioKey, Vector3? centralPosition = null, bool loop = false, float? customLifespan = null, byte? controllerId = null)
        {
            if (isDisposed)
            {
                Library_ExiledAPI.LogError("PlayGlobalSound", "AudioManager is disposed");
                return false;
            }

            var startTime = DateTime.UtcNow;

            lock (lockObject)
            {
                try
                {
                    if (!ValidateAudioKey(audioKey, "PlayGlobalSound") ||
                        !ValidateCustomLifespan(customLifespan, "PlayGlobalSound"))
                    {
                        RecordMetrics(audioKey, startTime, false);
                        return false;
                    }

                    if (!IsAudioLoaded(audioKey))
                    {
                        Library_ExiledAPI.LogWarn("PlayGlobalSound", $"Audio '{audioKey}' not loaded");
                        RecordMetrics(audioKey, startTime, false);
                        return false;
                    }

                    byte actualControllerId = controllerId ?? AllocateControllerId() ?? GLOBAL_AMBIENCE_ID;
                    if (!ValidateControllerId(actualControllerId, "PlayGlobalSound"))
                    {
                        RecordMetrics(audioKey, startTime, false);
                        return false;
                    }

                    if (managedSpeakers.ContainsKey(actualControllerId))
                    {
                        CleanupSpeaker(actualControllerId);
                    }

                    Vector3 position = centralPosition ?? Vector3.zero;
                    SpeakerToy speaker = CreateManagedSpeaker(actualControllerId, position, loop, customLifespan);

                    speaker.Volume = 0.85f;
                    speaker.IsSpatial = false;
                    speaker.MinDistance = 1.0f;
                    speaker.MaxDistance = 1500.0f;

                    var transmitter = SpeakerToy.GetTransmitter(actualControllerId);
                    transmitter.ValidPlayers = p => p != null && IsPlayerValidForAudio(p);
                    transmitter.Play(audioSamples[audioKey], queue: false, loop: loop);

                    if (audioKey == "ambience" && loop)
                    {
                        IsLoopingGlobalAmbience = true;
                    }

                    if (!loop || customLifespan.HasValue)
                    {
                        ScheduleCleanup(actualControllerId, audioKey, loop, customLifespan);
                    }

                    Library_ExiledAPI.LogDebug("PlayGlobalSound", $"Started global sound '{audioKey}' on controller {actualControllerId}");
                    RecordMetrics(audioKey, startTime, true);
                    return true;
                }
                catch (Exception ex)
                {
                    Library_ExiledAPI.LogError("PlayGlobalSound", $"Failed to play global sound '{audioKey}': {ex.Message}");
                    RecordMetrics(audioKey, startTime, false);
                    return false;
                }
            }
        }

        /// <summary>
        /// Plays the ambience sound globally for all valid players.
        /// </summary>
        /// <param name="centralPosition">Optional central position for the ambience.</param>
        /// <param name="loop">Whether to loop the ambience. Default is true.</param>
        /// <param name="customLifespan">Optional custom lifespan for the speaker.</param>
        /// <returns>True if the ambience was successfully played, false otherwise.</returns>
        public static bool PlayGlobalAmbience(Vector3? centralPosition = null, bool loop = true, float? customLifespan = null)
        {
            if (!IsAudioLoaded("ambience"))
            {
                Library_ExiledAPI.LogWarn("PlayGlobalAmbience", "Ambience audio not loaded");
                return false;
            }

            if (IsLoopingGlobalAmbience)
            {
                Library_ExiledAPI.LogWarn("PlayGlobalAmbience", "Global ambience already playing");
                return false;
            }

            try
            {
                Vector3 position = centralPosition ?? Vector3.zero;
                SpeakerToy speaker = CreateManagedSpeaker(GLOBAL_AMBIENCE_ID, position, loop, customLifespan);

                speaker.Volume = 0.85f;
                speaker.IsSpatial = false;
                speaker.MinDistance = 1.0f;
                speaker.MaxDistance = 1500.0f;

                var transmitter = SpeakerToy.GetTransmitter(GLOBAL_AMBIENCE_ID);
                transmitter.ValidPlayers = p => p != null && IsPlayerValidForAudio(p);
                transmitter.Play(audioSamples["ambience"], queue: false, loop: loop);

                if (loop) IsLoopingGlobalAmbience = true;

                if (!loop || customLifespan.HasValue)
                {
                    ScheduleCleanup(GLOBAL_AMBIENCE_ID, "ambience", loop, customLifespan);
                }

                Library_ExiledAPI.LogDebug("PlayGlobalAmbience", $"Started global ambience");
                return true;
            }
            catch (Exception ex)
            {
                Library_ExiledAPI.LogError("PlayGlobalAmbience", $"Failed to play global ambience: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Stops the global ambience if it is currently playing.
        /// </summary>
        public static void StopGlobalAmbience()
        {
            CleanupSpeaker(GLOBAL_AMBIENCE_ID);
            IsLoopingGlobalAmbience = false;
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
                }
            }
        }

        /// <summary>
        /// Validates an audio key.
        /// </summary>
        /// <param name="audioKey">The audio key to validate.</param>
        /// <param name="methodName">The name of the calling method for logging.</param>
        /// <returns>True if valid, false otherwise.</returns>
        private static bool ValidateAudioKey(string audioKey, string methodName)
        {
            if (string.IsNullOrWhiteSpace(audioKey))
            {
                Library_ExiledAPI.LogError(methodName, "Audio key cannot be null or empty");
                return false;
            }

            if (!AudioFiles.ContainsKey(audioKey))
            {
                Library_ExiledAPI.LogError(methodName, $"Unknown audio key: {audioKey}");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Checks if a player is valid for audio playback.
        /// </summary>
        /// <param name="player">The player to check.</param>
        /// <returns>True if valid, false otherwise.</returns>
        private static bool IsPlayerValidForAudio(Player player)
        {
            return player != null && player.IsAlive && player.ReferenceHub != null &&
                   player.ReferenceHub.connectionToClient != null && player.ReferenceHub.connectionToClient.isAuthenticated;
        }

        /// <summary>
        /// Validates a player for audio playback.
        /// </summary>
        /// <param name="player">The player to validate.</param>
        /// <param name="methodName">The name of the calling method for logging.</param>
        /// <returns>True if valid, false otherwise.</returns>
        private static bool ValidatePlayer(Player player, string methodName)
        {
            if (player == null)
            {
                Library_ExiledAPI.LogError(methodName, "Player cannot be null");
                return false;
            }

            if (!IsPlayerValidForAudio(player))
            {
                Library_ExiledAPI.LogWarn(methodName, $"Player {player.Nickname} is not valid for audio");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Validates a custom lifespan value.
        /// </summary>
        /// <param name="customLifespan">The lifespan to validate.</param>
        /// <param name="methodName">The name of the calling method for logging.</param>
        /// <returns>True if valid, false otherwise.</returns>
        private static bool ValidateCustomLifespan(float? customLifespan, string methodName)
        {
            if (!customLifespan.HasValue) return true;

            if (customLifespan.Value < MIN_CUSTOM_LIFESPAN || customLifespan.Value > MAX_CUSTOM_LIFESPAN)
            {
                Library_ExiledAPI.LogError(methodName,
                    $"Custom lifespan {customLifespan.Value} is outside valid range ({MIN_CUSTOM_LIFESPAN}-{MAX_CUSTOM_LIFESPAN})");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Validates a controller ID.
        /// </summary>
        /// <param name="controllerId">The controller ID to validate.</param>
        /// <param name="methodName">The name of the calling method for logging.</param>
        /// <returns>True if valid, false otherwise.</returns>
        private static bool ValidateControllerId(byte controllerId, string methodName)
        {
            if (controllerId < MIN_CONTROLLER_ID || controllerId > MAX_CONTROLLER_ID)
            {
                Library_ExiledAPI.LogError(methodName, $"Controller ID {controllerId} is outside valid range");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Checks if a specific audio clip has been loaded.
        /// </summary>
        /// <param name="audioKey">The key of the audio clip to check.</param>
        /// <returns>True if the audio is loaded, false otherwise.</returns>
        public static bool IsAudioLoaded(string audioKey)
        {
            return audioLoadStatus.ContainsKey(audioKey) && audioLoadStatus[audioKey] && audioSamples.ContainsKey(audioKey) && audioSamples[audioKey] != null;
        }

        /// <summary>
        /// Loads audio resources from embedded files.
        /// </summary>
        private static void LoadAudioResources()
        {
            foreach (var audio in AudioFiles)
            {
                string audioKey = audio.Key;
                string resourceName = audio.Value;

                try
                {
                    Assembly assembly = Assembly.GetExecutingAssembly();
                    using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                    {
                        if (stream == null)
                        {
                            Library_ExiledAPI.LogError("LoadAudioResources", $"Resource {resourceName} not found");
                            audioLoadStatus[audioKey] = false;
                            continue;
                        }

                        using (BinaryReader reader = new BinaryReader(stream))
                        {
                            reader.BaseStream.Seek(44, SeekOrigin.Begin);
                            byte[] rawData = reader.ReadBytes((int)(stream.Length - 44));
                            float[] samples = ConvertToFloatArray(rawData);
                            audioSamples[audioKey] = samples;
                            audioLoadStatus[audioKey] = true;
                            Library_ExiledAPI.LogDebug("LoadAudioResources", $"Loaded {audioKey} with {samples.Length} samples");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Library_ExiledAPI.LogError("LoadAudioResources", $"Failed to load {audioKey}: {ex.Message}");
                    audioLoadStatus[audioKey] = false;
                }
            }
        }

        /// <summary>
        /// Converts raw 16-bit PCM audio data to a float array.
        /// </summary>
        /// <param name="rawData">The raw audio data.</param>
        /// <returns>A float array representing the audio samples.</returns>
        private static float[] ConvertToFloatArray(byte[] rawData)
        {
            float[] samples = new float[rawData.Length / 2];
            for (int i = 0; i < samples.Length; i++)
            {
                short sample = BitConverter.ToInt16(rawData, i * 2);
                samples[i] = sample / 32768f;
            }
            return samples;
        }

        /// <summary>
        /// Internal method to play audio for a specific player.
        /// </summary>
        /// <param name="audioKey">The key of the audio clip.</param>
        /// <param name="player">The player to play the audio for.</param>
        /// <param name="position">The position to play the audio from.</param>
        /// <param name="controllerId">The controller ID to use.</param>
        /// <param name="loop">Whether to loop the audio.</param>
        /// <returns>True if successful, false otherwise.</returns>
        private static bool PlayAudioInternal(string audioKey, Player player, Vector3? position, byte controllerId, bool loop)
        {
            if (!IsAudioLoaded(audioKey))
            {
                Library_ExiledAPI.LogWarn("PlayAudioInternal", $"Audio '{audioKey}' not loaded");
                return false;
            }

            if (player == null)
            {
                Library_ExiledAPI.LogWarn("PlayAudioInternal", "Player is null");
                return false;
            }

            try
            {
                SpeakerToy speaker = CreateManagedSpeaker(controllerId, position ?? player.Position, loop);

                if (!IsPlayerValidForAudio(player))
                {
                    Library_ExiledAPI.LogWarn("PlayAudioInternal", $"Player {player.Nickname} is not valid for audio");
                    return false;
                }

                var transmitter = SpeakerToy.GetTransmitter(controllerId);
                transmitter.ValidPlayers = p => p != null && p == player && IsPlayerValidForAudio(p);
                transmitter.Play(audioSamples[audioKey], queue: false, loop: loop);

                Library_ExiledAPI.LogDebug("PlayAudioInternal", $"Playing {audioKey} to {player.Nickname} on controller {controllerId}");
                return true;
            }
            catch (Exception ex)
            {
                Library_ExiledAPI.LogError("PlayAudioInternal", $"Failed to play {audioKey}: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region Monitoring and Metrics

        /// <summary>
        /// Performs periodic health checks on managed speakers.
        /// </summary>
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
                    Library_ExiledAPI.LogError("HealthCheck", $"Health check failed: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Checks the health of all managed speakers and marks corrupted ones for cleanup.
        /// </summary>
        private static void PerformHealthCheck()
        {
            lock (lockObject)
            {
                var corruptedSpeakers = new List<byte>();

                foreach (var kvp in managedSpeakers.ToList())
                {
                    var controllerId = kvp.Key;
                    var managedSpeaker = kvp.Value;

                    if (managedSpeaker.Speaker?.Base == null || managedSpeaker.IsCorrupted)
                    {
                        corruptedSpeakers.Add(controllerId);
                        continue;
                    }

                    if (DateTime.UtcNow - managedSpeaker.LastActivity > TimeSpan.FromMinutes(10))
                    {
                        var transmitter = SpeakerToy.GetTransmitter(controllerId);
                        if (!transmitter.IsPlaying)
                        {
                            Library_ExiledAPI.LogDebug("HealthCheck", $"Cleaning up stale speaker {controllerId}");
                            corruptedSpeakers.Add(controllerId);
                        }
                    }
                }

                foreach (var controllerId in corruptedSpeakers)
                {
                    Library_ExiledAPI.LogWarn("HealthCheck", $"Recovering corrupted speaker {controllerId}");
                    CleanupSpeaker(controllerId);
                }
            }
        }

        /// <summary>
        /// Cleans up orphaned speakers and old performance metrics.
        /// </summary>
        private static void CleanupOrphanedSpeakers()
        {
            var cutoffTime = DateTime.UtcNow.AddHours(-1);

            lock (lockObject)
            {
                var keysToRemove = new List<string>();
                foreach (var kvp in performanceMetrics)
                {
                    var metrics = kvp.Value;
                    if (metrics.LastUsed < cutoffTime && metrics.TotalPlayRequests == 0)
                    {
                        keysToRemove.Add(kvp.Key);
                    }

                    if (metrics.RecentProcessingTimes.Count > MAX_PROCESSING_TIMES)
                    {
                        metrics.RecentProcessingTimes = metrics.RecentProcessingTimes
                            .Skip(metrics.RecentProcessingTimes.Count - MAX_PROCESSING_TIMES / 2)
                            .ToList();
                    }
                }

                foreach (var key in keysToRemove)
                {
                    performanceMetrics.Remove(key);
                    Library_ExiledAPI.LogDebug("CleanupOrphanedSpeakers", $"Removed unused performance metrics for {key}");
                }

                var orphanedSpeakers = new List<byte>();
                foreach (var kvp in managedSpeakers)
                {
                    var controllerId = kvp.Key;
                    var managedSpeaker = kvp.Value;

                    if (DateTime.UtcNow - managedSpeaker.LastActivity > TimeSpan.FromMinutes(30))
                    {
                        try
                        {
                            var transmitter = SpeakerToy.GetTransmitter(controllerId);
                            if (!transmitter.IsPlaying)
                            {
                                orphanedSpeakers.Add(controllerId);
                            }
                            else
                            {
                                managedSpeaker.LastActivity = DateTime.UtcNow;
                            }
                        }
                        catch (Exception ex)
                        {
                            Library_ExiledAPI.LogWarn("CleanupOrphanedSpeakers", $"Error checking speaker {controllerId}: {ex.Message}");
                            orphanedSpeakers.Add(controllerId);
                        }
                    }
                }

                foreach (var controllerId in orphanedSpeakers)
                {
                    Library_ExiledAPI.LogInfo("CleanupOrphanedSpeakers", $"Cleaning up orphaned speaker {controllerId}");
                    CleanupSpeaker(controllerId);
                }
            }
        }

        /// <summary>
        /// Records performance metrics for an audio playback attempt.
        /// </summary>
        /// <param name="audioKey">The key of the audio clip.</param>
        /// <param name="startTime">The time the playback attempt started.</param>
        /// <param name="success">Whether the playback was successful.</param>
        private static void RecordMetrics(string audioKey, DateTime startTime, bool success)
        {
            if (!performanceMetrics.TryGetValue(audioKey, out var metrics))
                return;

            lock (lockObject)
            {
                metrics.TotalPlayRequests++;
                metrics.LastUsed = DateTime.UtcNow;

                if (success)
                {
                    metrics.SuccessfulPlays++;
                }
                else
                {
                    metrics.FailedPlays++;
                }

                var processingTime = DateTime.UtcNow - startTime;
                metrics.RecentProcessingTimes.Add(processingTime);

                if (metrics.RecentProcessingTimes.Count > MAX_PROCESSING_TIMES)
                {
                    metrics.RecentProcessingTimes.RemoveAt(0);
                }

                if (metrics.RecentProcessingTimes.Count > 0)
                {
                    metrics.AverageProcessingTime = TimeSpan.FromMilliseconds(
                        metrics.RecentProcessingTimes.Average(t => t.TotalMilliseconds));
                }
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
        /// <returns>A dictionary containing speaker status information keyed by controller ID.</returns>
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
        /// <returns>A dictionary of performance metrics keyed by audio type.</returns>
        public static Dictionary<string, PerformanceMetrics> GetPerformanceMetrics()
        {
            lock (lockObject)
            {
                return new Dictionary<string, PerformanceMetrics>(performanceMetrics);
            }
        }

        #endregion
    }
}