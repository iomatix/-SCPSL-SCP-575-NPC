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

    public static class AudioManager
    {
        private static readonly Dictionary<string, float[]> audioSamples = new();
        private static readonly Dictionary<string, bool> audioLoadStatus = new();
        private static readonly Dictionary<byte, ManagedSpeaker> managedSpeakers = new();
        private static readonly HashSet<byte> availableControllerIds = new();
        private static readonly object lockObject = new();

        // Performance metrics  
        private static readonly Dictionary<string, PerformanceMetrics> performanceMetrics = new();
        private static volatile bool isDisposed = false;
        private static CoroutineHandle healthCheckCoroutine;

        private const int MAX_CONCURRENT_SPEAKERS = 50;
        private const byte MIN_CONTROLLER_ID = 101;
        private const byte MAX_CONTROLLER_ID = 199;
        private const byte GLOBAL_AMBIENCE_ID = 157;
        private const float MAX_CUSTOM_LIFESPAN = 3600f; // 1 hour max
        private const float MIN_CUSTOM_LIFESPAN = 0.1f; // 100ms min

        public static bool IsLoopingGlobalAmbience = false;
        private static bool isInitialized;

        // Audio file definitions
        private static readonly Dictionary<string, string> AudioFiles = new()
        {
            { "scream", "SCP-575.Shared.Audio.scream.wav" },
            { "scream-angry", "SCP-575.Shared.Audio.scream-angry.wav" },
            { "ambience", "SCP-575.Shared.Audio.ambience.wav" },
        };

        // Performance monitoring
        public class PerformanceMetrics
        {
            public int TotalPlayRequests { get; set; }
            public int SuccessfulPlays { get; set; }
            public int FailedPlays { get; set; }
            public DateTime LastUsed { get; set; }
            public TimeSpan AverageProcessingTime { get; set; }
            public List<TimeSpan> RecentProcessingTimes { get; set; } = new();
        }

        // Enhanced speaker tracking
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

        private static void Initialize()
        {
            if (isDisposed) return;

            // Initialize available controller IDs  
            for (byte i = MIN_CONTROLLER_ID; i <= MAX_CONTROLLER_ID; i++)
            {
                if (i != GLOBAL_AMBIENCE_ID)
                    availableControllerIds.Add(i);
            }

            // Initialize performance metrics for each audio type  
            foreach (var audioKey in AudioFiles.Keys)
            {
                performanceMetrics[audioKey] = new PerformanceMetrics();
            }

            LoadAudioResources();

            // Start health check coroutine  
            healthCheckCoroutine = Timing.RunCoroutine(HealthCheckCoroutine(), Segment.Update);

            Library_ExiledAPI.LogInfo("AudioManager", "Initialized with automatic management and monitoring");
        }

        private static SpeakerToy CreateManagedSpeaker(byte controllerId, Vector3 position, bool isLooped, float? customLifespan = null)
        {
            // Clean up existing speaker if it exists  
            if (managedSpeakers.ContainsKey(controllerId))
            {
                CleanupSpeaker(controllerId);
            }

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

        private static SpeakerToy EnsureSpeakerExists(byte controllerId, Vector3 position)
        {
            lock (lockObject)
            {
                if (managedSpeakers.TryGetValue(controllerId, out ManagedSpeaker existingSpeaker) &&
                    existingSpeaker?.Speaker?.Base != null)
                {
                    return existingSpeaker.Speaker;
                }

                // Clean up any existing entry  
                if (managedSpeakers.ContainsKey(controllerId))
                {
                    CleanupSpeaker(controllerId);
                }

                // Create new managed speaker  
                return CreateManagedSpeaker(controllerId, position, false);
            }
        }

        private static void ScheduleCleanup(byte controllerId, string audioKey, bool isLooped, float? customLifespan)
        {
            lock (lockObject)
            {
                if (!managedSpeakers.TryGetValue(controllerId, out var managedSpeaker))
                    return;

                // Add validation  
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
                        // Add safety check for SampleRate  
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

        public static void CleanupSpeakers()
        {
            lock (lockObject)
            {
                var controllerIds = managedSpeakers.Keys.ToList();
                foreach (var controllerId in controllerIds)
                {
                    CleanupSpeaker(controllerId);
                }
                managedSpeakers.Clear();
            }
        }

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

        // Input validation methods  
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
        private static bool IsPlayerValidForAudio(Player player) { return player != null && player.IsAlive && player.ReferenceHub != null && player.ReferenceHub.connectionToClient != null && player.ReferenceHub.connectionToClient.isAuthenticated; }

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

        private static bool ValidateControllerId(byte controllerId, string methodName)
        {
            if (controllerId < MIN_CONTROLLER_ID || controllerId > MAX_CONTROLLER_ID)
            {
                Library_ExiledAPI.LogError(methodName, $"Controller ID {controllerId} is outside valid range");
                return false;
            }

            return true;
        }

        // Play methods with full validation
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
                // Input validation  
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

        public static byte? PlayAmbienceAutoManaged(Player player, Vector3? position = null, bool loop = true, float? customLifespan = null)
        {
            return PlayAudioAutoManaged("ambience", player, position, loop, customLifespan);
        }

        public static byte? PlayScreamAutoManaged(Player player, Vector3? position = null, float? customLifespan = null)
        {
            return PlayAudioAutoManaged("scream", player, position, false, customLifespan);
        }

        public static bool PlayAudio(string audioKey, Player player, Vector3? position = null, byte controllerId = 1, bool loop = false)
        {
            if (isDisposed)
            {
                Library_ExiledAPI.LogError("PlayAudio", "AudioManager is disposed");
                return false;
            }

            var startTime = DateTime.UtcNow;

            lock (lockObject) // Add lock
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
                    Library_ExiledAPI.LogError("PlayAudio", $"Failed to play {audioKey} sound: {ex.Message}\nStackTrace: {ex.StackTrace}");
                    RecordMetrics(audioKey, startTime, false);
                    return false;
                }
            }
        }

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

        // Error recovery and health monitoring  
        private static IEnumerator<float> HealthCheckCoroutine()
        {
            while (!isDisposed)
            {
                yield return Timing.WaitForSeconds(30f); // Check every 30 seconds  

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

        private static void PerformHealthCheck()
        {
            lock (lockObject)
            {
                var corruptedSpeakers = new List<byte>();

                foreach (var kvp in managedSpeakers.ToList())
                {
                    var controllerId = kvp.Key;
                    var managedSpeaker = kvp.Value;

                    // Check for corrupted speakers  
                    if (managedSpeaker.Speaker?.Base == null || managedSpeaker.IsCorrupted)
                    {
                        corruptedSpeakers.Add(controllerId);
                        continue;
                    }

                    // Check for stale speakers (inactive for too long)  
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

                // Clean up corrupted speakers  
                foreach (var controllerId in corruptedSpeakers)
                {
                    Library_ExiledAPI.LogWarn("HealthCheck", $"Recovering corrupted speaker {controllerId}");
                    CleanupSpeaker(controllerId);
                }
            }
        }
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

        public static void CleanupSpeaker(byte controllerId)
        {
            lock (lockObject)
            {
                if (!managedSpeakers.TryGetValue(controllerId, out var managedSpeaker))
                    return;

                try
                {
                    // Stop audio transmission  
                    var transmitter = SpeakerToy.GetTransmitter(controllerId);
                    transmitter.Stop();

                    // Destroy speaker  
                    if (managedSpeaker.Speaker?.Base != null)
                    {
                        managedSpeaker.Speaker.Destroy();
                    }

                    // Cancel cleanup coroutine if running  
                    if (managedSpeaker.CleanupCoroutine.IsRunning)
                    {
                        Timing.KillCoroutines(managedSpeaker.CleanupCoroutine);
                    }

                    // Release controller ID  
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

        public static void StopAndCleanup(byte controllerId)
        {
            CleanupSpeaker(controllerId);
        }

        public static void CleanupAllSpeakers()
        {
            lock (lockObject)
            {
                var controllerIds = managedSpeakers.Keys.ToList();
                foreach (var controllerId in controllerIds)
                {
                    CleanupSpeaker(controllerId);
                }

                // Reset global ambience state  
                IsLoopingGlobalAmbience = false;
            }
        }

        private static void CleanupOrphanedSpeakers()
        {
            // Clean up performance metrics for unused audio keys  
            var cutoffTime = DateTime.UtcNow.AddHours(-1); // Remove metrics older than 1 hour  

            lock (lockObject)
            {
                // Clean up old performance metrics  
                var keysToRemove = new List<string>();
                foreach (var kvp in performanceMetrics)
                {
                    var metrics = kvp.Value;
                    if (metrics.LastUsed < cutoffTime && metrics.TotalPlayRequests == 0)
                    {
                        keysToRemove.Add(kvp.Key);
                    }

                    // Trim recent processing times to keep memory usage reasonable  
                    if (metrics.RecentProcessingTimes.Count > 100)
                    {
                        metrics.RecentProcessingTimes = metrics.RecentProcessingTimes
                            .Skip(metrics.RecentProcessingTimes.Count - 50)
                            .ToList();
                    }
                }

                foreach (var key in keysToRemove)
                {
                    performanceMetrics.Remove(key);
                    Library_ExiledAPI.LogDebug("CleanupOrphanedSpeakers", $"Removed unused performance metrics for {key}");
                }

                // Check for speakers that may have become orphaned  
                var orphanedSpeakers = new List<byte>();
                foreach (var kvp in managedSpeakers)
                {
                    var controllerId = kvp.Key;
                    var managedSpeaker = kvp.Value;

                    // Check if speaker has been inactive for too long  
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
                                // Update last activity if still playing  
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

                // Clean up orphaned speakers  
                foreach (var controllerId in orphanedSpeakers)
                {
                    Library_ExiledAPI.LogInfo("CleanupOrphanedSpeakers", $"Cleaning up orphaned speaker {controllerId}");
                    CleanupSpeaker(controllerId);
                }
            }
        }

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

                // Calculate rolling average  
                if (metrics.RecentProcessingTimes.Count > 0)
                {
                    metrics.AverageProcessingTime = TimeSpan.FromMilliseconds(
                        metrics.RecentProcessingTimes.Average(t => t.TotalMilliseconds));
                }
            }
        }

        public static Dictionary<string, PerformanceMetrics> GetPerformanceMetrics()
        {
            lock (lockObject)
            {
                return new Dictionary<string, PerformanceMetrics>(performanceMetrics);
            }
        }

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

                        // Read WAV file and extract PCM data
                        using (BinaryReader reader = new BinaryReader(stream))
                        {
                            // Skip WAV header (simplified; assumes 44-byte header for mono 16-bit PCM)
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

        private static float[] ConvertToFloatArray(byte[] rawData)
        {
            // Assuming 16-bit PCM, convert to float[-1, 1]
            float[] samples = new float[rawData.Length / 2];
            for (int i = 0; i < samples.Length; i++)
            {
                short sample = BitConverter.ToInt16(rawData, i * 2);
                samples[i] = sample / 32768f; // Normalize to [-1, 1]
            }
            return samples;
        }

        // Internal play method
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
                    // Validate inputs
                    if (!ValidateAudioKey(audioKey, "PlayGlobalSound"))
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

                    if (!ValidateCustomLifespan(customLifespan, "PlayGlobalSound"))
                    {
                        RecordMetrics(audioKey, startTime, false);
                        return false;
                    }

                    // Allocate controller ID if not provided
                    byte actualControllerId = controllerId ?? AllocateControllerId() ?? GLOBAL_AMBIENCE_ID;
                    if (!ValidateControllerId(actualControllerId, "PlayGlobalSound"))
                    {
                        RecordMetrics(audioKey, startTime, false);
                        return false;
                    }

                    // Check if the controller ID is already in use
                    if (managedSpeakers.ContainsKey(actualControllerId))
                    {
                        CleanupSpeaker(actualControllerId);
                    }

                    // Create speaker
                    Vector3 position = centralPosition ?? Vector3.zero;
                    SpeakerToy speaker = CreateManagedSpeaker(actualControllerId, position, loop, customLifespan);

                    // Configure for global playback
                    speaker.Volume = 0.85f;
                    speaker.IsSpatial = false; // Non-spatial for global sound
                    speaker.MinDistance = 1.0f;
                    speaker.MaxDistance = 1500.0f; // Large range to cover entire map

                    var transmitter = SpeakerToy.GetTransmitter(actualControllerId);
                    transmitter.ValidPlayers = p => p != null && IsPlayerValidForAudio(p);
                    transmitter.Play(audioSamples[audioKey], queue: false, loop: loop);

                    // Update looping state if needed
                    if (audioKey == "ambience" && loop)
                    {
                        IsLoopingGlobalAmbience = true;
                    }

                    // Schedule cleanup if not looped or has custom lifespan
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
                    Library_ExiledAPI.LogError("PlayGlobalSound", $"Failed to play global sound '{audioKey}': {ex.Message}\nStackTrace: {ex.StackTrace}");
                    RecordMetrics(audioKey, startTime, false);
                    return false;
                }
            }
        }

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

                // Schedule cleanup if not looped or has custom lifespan  
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

        public static bool IsAudioLoaded(string audioKey) { return audioLoadStatus.ContainsKey(audioKey) && audioLoadStatus[audioKey] && audioSamples.ContainsKey(audioKey) && audioSamples[audioKey] != null; }

        public static void StopGlobalAmbience()
        {
            CleanupSpeaker(GLOBAL_AMBIENCE_ID);
            IsLoopingGlobalAmbience = false;
        }

        // Status and monitoring methods  
        public static int GetActiveSpeakerCount()
        {
            lock (lockObject)
            {
                return managedSpeakers.Count;
            }
        }

        public static int GetAvailableControllerCount()
        {
            lock (lockObject)
            {
                return availableControllerIds.Count;
            }
        }

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
    }
}