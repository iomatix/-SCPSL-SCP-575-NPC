namespace SCP_575.Shared
{
    using System;
    using System.IO;
    using System.Reflection;
    using System.Collections.Generic;
    using UnityEngine;
    using LabApi.Features.Audio;
    using LabApi.Features.Wrappers;

    public static class AudioManager
    {
        private static float[] screamSamples;
        private static bool audioLoaded = false;
        private static readonly Dictionary<byte, SpeakerToy> managedSpeakers = new();
        private static readonly object lockObject = new();

        public static void LoadEmbeddedAudio()
        {
            if (audioLoaded) return;

            lock (lockObject)
            {
                if (audioLoaded) return;

                try
                {
                    Assembly assembly = Assembly.GetExecutingAssembly();
                    using Stream stream = assembly.GetManifestResourceStream("SCP-575.Shared.Audio.scream.wav");

                    if (stream == null)
                    {
                        Library_ExiledAPI.LogError("LoadEmbeddedAudio", "Could not find embedded audio file: scream.wav");
                        return;
                    }

                    screamSamples = ConvertWavToPcm(stream);

                    // Validate sample rate and apply volume boost  
                    if (screamSamples != null && screamSamples.Length > 0)
                    {
                        // Apply volume boost to compensate for potential quietness  
                        ApplyVolumeBoost(screamSamples, 1.5f);
                        audioLoaded = true;
                        Library_ExiledAPI.LogDebug("LoadEmbeddedAudio", $"Successfully loaded scream audio with {screamSamples.Length} samples");
                    }
                    else
                    {
                        Library_ExiledAPI.LogError("LoadEmbeddedAudio", "Failed to convert WAV to PCM samples");
                    }
                }
                catch (Exception ex)
                {
                    Library_ExiledAPI.LogError("LoadEmbeddedAudio", $"Failed to load audio: {ex.Message}");
                }
            }
        }

        public static bool PlayScreamSound(Player player, Vector3? position = null, byte controllerId = 1)
        {
            if (!audioLoaded || screamSamples == null)
            {
                Library_ExiledAPI.LogWarn("PlayScreamSound", "Audio not loaded or samples are null");
                return false;
            }

            if (player == null)
            {
                Library_ExiledAPI.LogWarn("PlayScreamSound", "Player is null");
                return false;
            }

            try
            {
                // Ensure speaker exists for the controller ID  
                SpeakerToy speaker = EnsureSpeakerExists(controllerId, position ?? player.Position);

                // Validate player is authenticated and in correct state  
                if (!IsPlayerValidForAudio(player))
                {
                    Library_ExiledAPI.LogWarn("PlayScreamSound", $"Player {player.Nickname} is not in valid state for audio");
                    return false;
                }

                // Create transmitter with proper player filtering  
                var transmitter = SpeakerToy.GetTransmitter(controllerId);
                transmitter.ValidPlayers = p => p != null && p == player && IsPlayerValidForAudio(p);

                // Play audio with proper settings  
                transmitter.Play(screamSamples, queue: false, loop: false);

                Library_ExiledAPI.LogDebug("PlayScreamSound", $"Successfully played scream audio to {player.Nickname} using controller {controllerId}");
                return true;
            }
            catch (Exception ex)
            {
                Library_ExiledAPI.LogError("PlayScreamSound", $"Failed to play scream sound: {ex.Message}");
                return false;
            }
        }

        private static SpeakerToy EnsureSpeakerExists(byte controllerId, Vector3 position)
        {
            if (managedSpeakers.TryGetValue(controllerId, out SpeakerToy existingSpeaker) &&
                existingSpeaker != null && existingSpeaker.Base != null)
            {
                return existingSpeaker;
            }

            // Create new speaker at specified position  
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
                // Validate WAV header  
                if (!ValidateWavHeader(wavStream))
                {
                    Library_ExiledAPI.LogError("ConvertWavToPcm", "Invalid WAV file format");
                    return null;
                }

                // Skip WAV header (44 bytes for standard PCM WAV)  
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
                    samples[i] = sample / 32768f; // Convert to -1.0f to 1.0f range  
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

            // Check RIFF header  
            if (System.Text.Encoding.ASCII.GetString(header, 0, 4) != "RIFF") return false;
            if (System.Text.Encoding.ASCII.GetString(header, 8, 4) != "WAVE") return false;
            if (System.Text.Encoding.ASCII.GetString(header, 12, 4) != "fmt ") return false;

            // Check format (PCM = 1)  
            short audioFormat = BitConverter.ToInt16(header, 20);
            if (audioFormat != 1) return false;

            // Check channels (should be 1 for mono)  
            short channels = BitConverter.ToInt16(header, 22);
            if (channels != 1) return false;

            // Check sample rate (should be 48000 for LabAPI)  
            int sampleRate = BitConverter.ToInt32(header, 24);
            if (sampleRate != AudioTransmitter.SampleRate)
            {
                Library_ExiledAPI.LogWarn("ValidateWavHeader", $"Sample rate mismatch: WAV has {sampleRate}Hz, expected {AudioTransmitter.SampleRate}Hz");
            }

            // Check bits per sample (should be 16)  
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