namespace SCP_575.Shared
{
    using LabApi.Features.Audio;
    using LabApi.Features.Wrappers;
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
        private static readonly Dictionary<byte, SpeakerToy> managedSpeakers = new();
        private static readonly object lockObject = new();

        public static bool IsLoopingGlobalAmbience = false;

        // Audio file definitions  
        private static readonly Dictionary<string, string> AudioFiles = new()
        {
            { "scream", "SCP-575.Shared.Audio.scream.wav" },
            { "ambience", "SCP-575.Shared.Audio.ambience.wav" },
        };

        public static void LoadEmbeddedAudio()
        {
            lock (lockObject)
            {
                foreach (var audioFile in AudioFiles)
                {
                    string audioKey = audioFile.Key;
                    string resourcePath = audioFile.Value;

                    if (audioLoadStatus.ContainsKey(audioKey) && audioLoadStatus[audioKey])
                        continue;

                    try
                    {
                        Assembly assembly = Assembly.GetExecutingAssembly();
                        using Stream stream = assembly.GetManifestResourceStream(resourcePath);

                        if (stream == null)
                        {
                            Library_ExiledAPI.LogError("LoadEmbeddedAudio", $"Could not find embedded audio file: {audioKey}.wav");
                            audioLoadStatus[audioKey] = false;
                            continue;
                        }

                        float[] samples = ConvertWavToPcm(stream);

                        if (samples != null && samples.Length > 0)
                        {
                            // Apply different volume boosts based on audio type  
                            float volumeMultiplier = audioKey == "scream" ? 1.5f : 1.0f;
                            ApplyVolumeBoost(samples, volumeMultiplier);

                            audioSamples[audioKey] = samples;
                            audioLoadStatus[audioKey] = true;
                            Library_ExiledAPI.LogDebug("LoadEmbeddedAudio", $"Successfully loaded {audioKey} audio with {samples.Length} samples");
                        }
                        else
                        {
                            Library_ExiledAPI.LogError("LoadEmbeddedAudio", $"Failed to convert {audioKey} WAV to PCM samples");
                            audioLoadStatus[audioKey] = false;
                        }
                    }
                    catch (Exception ex)
                    {
                        Library_ExiledAPI.LogError("LoadEmbeddedAudio", $"Failed to load {audioKey} audio: {ex.Message}");
                        audioLoadStatus[audioKey] = false;
                    }
                }
            }
        }

        public static bool PlayAudio(string audioKey, Player player, Vector3? position = null, byte controllerId = 1, bool loop = false)
        {
            if (!IsAudioLoaded(audioKey))
            {
                Library_ExiledAPI.LogWarn("PlayAudio", $"Audio '{audioKey}' not loaded or samples are null");
                return false;
            }

            if (player == null)
            {
                Library_ExiledAPI.LogWarn("PlayAudio", "Player is null");
                return false;
            }

            try
            {
                SpeakerToy speaker = EnsureSpeakerExists(controllerId, position ?? player.Position);

                if (!IsPlayerValidForAudio(player))
                {
                    Library_ExiledAPI.LogWarn("PlayAudio", $"Player {player.Nickname} is not in valid state for audio");
                    return false;
                }

                var transmitter = SpeakerToy.GetTransmitter(controllerId);
                transmitter.ValidPlayers = p => p != null && p == player && IsPlayerValidForAudio(p);

                transmitter.Play(audioSamples[audioKey], queue: false, loop: loop);

                Library_ExiledAPI.LogDebug("PlayAudio", $"Successfully played {audioKey} audio to {player.Nickname} using controller {controllerId}");
                return true;
            }
            catch (Exception ex)
            {
                Library_ExiledAPI.LogError("PlayAudio", $"Failed to play {audioKey} sound: {ex.Message}");
                return false;
            }
        }

        // Convenience methods for specific audio types  
        public static bool PlayScreamSound(Player player, Vector3? position = null, byte controllerId = 1)
            => PlayAudio("scream", player, position, controllerId, false);

        // Method for global ambience playback
        public static bool PlayGlobalAmbience(Vector3? centralPosition = null, byte controllerId = 2, bool loop = true)
        {
            if (!IsAudioLoaded("ambience"))
            {
                Library_ExiledAPI.LogWarn("PlayGlobalAmbience", "Ambience audio not loaded");
                return false;
            }

            if (IsLoopingGlobalAmbience)
            {
                Library_ExiledAPI.LogWarn("PlayGlobalAmbience", "Ambience is already on Loop, request canceled.");
                return false;
            }

            try
            {
                // Use a central position or default to server spawn  
                Vector3 position = centralPosition ?? Vector3.zero;
                SpeakerToy speaker = EnsureSpeakerExists(controllerId, position);

                // Configure speaker for global ambience
                speaker.Volume = 0.8f; // Slightly lower for ambience
                speaker.IsSpatial = false; // Non-spatial for global effect
                speaker.MinDistance = 1.0f;
                speaker.MaxDistance = 1500.0f; // Large range

                var transmitter = SpeakerToy.GetTransmitter(controllerId);

                // Set to broadcast to all valid players  
                transmitter.ValidPlayers = p => p != null && IsPlayerValidForAudio(p);

                transmitter.Play(audioSamples["ambience"], queue: false, loop: loop);
                if (loop == true) IsLoopingGlobalAmbience = true;

                Library_ExiledAPI.LogDebug("PlayGlobalAmbience", $"Started global ambience on controller {controllerId}");
                return true;
            }
            catch (Exception ex)
            {
                Library_ExiledAPI.LogError("PlayGlobalAmbience", $"Failed to play global ambience: {ex.Message}");
                return false;
            }
        }

        // Enhanced method for targeted ambience (multiple players, single transmission)  
        public static bool PlayAmbienceToPlayers(IEnumerable<Player> targetPlayers, Vector3? position = null, byte controllerId = 2, bool loop = true)
        {
            if (!IsAudioLoaded("ambience"))
            {
                Library_ExiledAPI.LogWarn("PlayAmbienceToPlayers", "Ambience audio not loaded");
                return false;
            }

            var validPlayers = targetPlayers.Where(IsPlayerValidForAudio).ToList();
            if (!validPlayers.Any())
            {
                Library_ExiledAPI.LogWarn("PlayAmbienceToPlayers", "No valid players to play ambience to");
                return false;
            }

            try
            {
                Vector3 speakerPosition = position ?? Vector3.zero;
                SpeakerToy speaker = EnsureSpeakerExists(controllerId, speakerPosition);

                speaker.Volume = 0.8f;
                speaker.IsSpatial = position.HasValue; // Spatial if position specified  
                speaker.MinDistance = 1.0f;
                speaker.MaxDistance = 50.0f;

                var transmitter = SpeakerToy.GetTransmitter(controllerId);

                // Efficient player filtering - single transmission to multiple players  
                var playerSet = new HashSet<Player>(validPlayers);
                transmitter.ValidPlayers = p => p != null && playerSet.Contains(p) && IsPlayerValidForAudio(p);

                transmitter.Play(audioSamples["ambience"], queue: false, loop: loop);

                Library_ExiledAPI.LogDebug("PlayAmbienceToPlayers", $"Started ambience for {validPlayers.Count} players on controller {controllerId}");
                return true;
            }
            catch (Exception ex)
            {
                Library_ExiledAPI.LogError("PlayAmbienceToPlayers", $"Failed to play ambience to players: {ex.Message}");
                return false;
            }
        }

        public static void StopGlobalAmbienceLoop(byte controllerId = 2)
        {
            StopAudio(controllerId);
            IsLoopingGlobalAmbience = false;
        }

        // Method for single player use
        public static bool PlayAmbienceSound(Player player, Vector3? position = null, byte controllerId = 2, bool loop = true)
            => PlayAudio("ambience", player, position, controllerId, loop);

        public static bool IsAudioLoaded(string audioKey)
        {
            return audioLoadStatus.ContainsKey(audioKey) &&
                   audioLoadStatus[audioKey] &&
                   audioSamples.ContainsKey(audioKey) &&
                   audioSamples[audioKey] != null;
        }

        public static void StopAudio(byte controllerId)
        {
            try
            {
                var transmitter = SpeakerToy.GetTransmitter(controllerId);
                transmitter.Stop();
                Library_ExiledAPI.LogDebug("StopAudio", $"Stopped audio on controller {controllerId}");
            }
            catch (Exception ex)
            {
                Library_ExiledAPI.LogError("StopAudio", $"Failed to stop audio on controller {controllerId}: {ex.Message}");
            }
        }

        private static SpeakerToy EnsureSpeakerExists(byte controllerId, Vector3 position)
        {
            if (managedSpeakers.TryGetValue(controllerId, out SpeakerToy existingSpeaker) &&
                existingSpeaker != null && existingSpeaker.Base != null)
            {
                return existingSpeaker;
            }

            SpeakerToy speaker = SpeakerToy.Create(position, networkSpawn: true);
            speaker.ControllerId = controllerId;
            speaker.Volume = 1.0f;
            speaker.IsSpatial = true;
            speaker.MinDistance = 1.0f;
            speaker.MaxDistance = 15.0f;

            managedSpeakers[controllerId] = speaker;

            Library_ExiledAPI.LogDebug("EnsureSpeakerExists", $"Created new speaker with controller ID {controllerId} at position {position}");
            return speaker;
        }

        private static bool IsPlayerValidForAudio(Player player)
        {
            return player != null &&
                   player.IsAlive &&
                   player.ReferenceHub != null &&
                   player.ReferenceHub.connectionToClient != null &&
                   player.ReferenceHub.connectionToClient.isAuthenticated;
        }

        private static void ApplyVolumeBoost(float[] samples, float multiplier)
        {
            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] = Mathf.Clamp(samples[i] * multiplier, -1.0f, 1.0f);
            }
        }

        private static float[] ConvertWavToPcm(Stream wavStream)
        {
            try
            {
                if (!ValidateWavHeader(wavStream))
                {
                    Library_ExiledAPI.LogError("ConvertWavToPcm", "Invalid WAV file format");
                    return null;
                }

                wavStream.Seek(44, SeekOrigin.Begin);

                var buffer = new byte[wavStream.Length - 44];
                int bytesRead = wavStream.Read(buffer, 0, buffer.Length);

                if (bytesRead != buffer.Length)
                {
                    Library_ExiledAPI.LogWarn("ConvertWavToPcm", $"Expected {buffer.Length} bytes but read {bytesRead}");
                }

                var samples = new float[buffer.Length / 2];
                for (int i = 0; i < samples.Length; i++)
                {
                    short sample = BitConverter.ToInt16(buffer, i * 2);
                    samples[i] = sample / 32768f;
                }

                Library_ExiledAPI.LogDebug("ConvertWavToPcm", $"Converted {samples.Length} samples from WAV");
                return samples;
            }
            catch (Exception ex)
            {
                Library_ExiledAPI.LogError("ConvertWavToPcm", $"Failed to convert WAV: {ex.Message}");
                return null;
            }
        }

        private static bool ValidateWavHeader(Stream wavStream)
        {
            if (wavStream.Length < 44) return false;

            wavStream.Seek(0, SeekOrigin.Begin);
            byte[] header = new byte[44];
            wavStream.Read(header, 0, 44);

            if (System.Text.Encoding.ASCII.GetString(header, 0, 4) != "RIFF") return false;
            if (System.Text.Encoding.ASCII.GetString(header, 8, 4) != "WAVE") return false;
            if (System.Text.Encoding.ASCII.GetString(header, 12, 4) != "fmt ") return false;

            short audioFormat = BitConverter.ToInt16(header, 20);
            if (audioFormat != 1) return false;

            short channels = BitConverter.ToInt16(header, 22);
            if (channels != 1) return false;

            int sampleRate = BitConverter.ToInt32(header, 24);
            if (sampleRate != AudioTransmitter.SampleRate)
            {
                Library_ExiledAPI.LogWarn("ValidateWavHeader", $"Sample rate mismatch: WAV has {sampleRate}Hz, expected {AudioTransmitter.SampleRate}Hz");
            }

            short bitsPerSample = BitConverter.ToInt16(header, 34);
            if (bitsPerSample != 16) return false;

            return true;
        }

        public static void CleanupSpeakers()
        {
            foreach (var speaker in managedSpeakers.Values)
            {
                try
                {
                    if (speaker?.Base != null)
                    {
                        speaker.Destroy();
                    }
                }
                catch (Exception ex)
                {
                    Library_ExiledAPI.LogError("CleanupSpeakers", $"Failed to cleanup speaker: {ex.Message}");
                }
            }
            managedSpeakers.Clear();
        }
    }
}