namespace SCP_575.Shared
{
    using System;
    using System.IO;
    using System.Reflection;
    using UnityEngine;
    using LabApi.Features.Audio;
    using LabApi.Features.Wrappers;

    public static class AudioManager
    {
        private static float[] screamSamples;
        private static bool audioLoaded = false;

        public static void LoadEmbeddedAudio()
        {
            if (audioLoaded) return;

            try
            {
                // Load the embedded .wav file  
                Assembly assembly = Assembly.GetExecutingAssembly();
                string[] resourceNames = assembly.GetManifestResourceNames();
                foreach (string name in resourceNames)
                {
                    Library_ExiledAPI.LogInfo("LoadEmbeddedAudio", $"Found embedded resource: {name}");
                }
                using Stream stream = assembly.GetManifestResourceStream("SCP-575.Shared.Audio.scream.wav"); // must be set to "Embedded Resource" in the .csproj file

                if (stream == null)
                {
                    Library_ExiledAPI.LogError("LoadEmbeddedAudio", "Could not find embedded audio file: scream.wav");
                    return;
                }

                // Convert WAV to PCM samples  
                screamSamples = ConvertWavToPcm(stream);
                audioLoaded = true;
                Library_ExiledAPI.LogDebug("LoadEmbeddedAudio", "Successfully loaded scream audio");
            }
            catch (Exception ex)
            {
                Library_ExiledAPI.LogError("LoadEmbeddedAudio", $"Failed to load audio: {ex.Message}");
            }
        }

        public static void PlayScreamSound(Player player)
        {
            if (!audioLoaded || screamSamples == null) return;

            try
            {
                var transmitter = new AudioTransmitter(1); // Controller ID 1  
                transmitter.ValidPlayers = p => p == player; // Only play to the hit player  
                transmitter.Play(screamSamples, queue: false, loop: false);
                Library_ExiledAPI.LogDebug("PlayScreamSound", "Successfully played scream audio");
            }
            catch (Exception ex)
            {
                Library_ExiledAPI.LogError("PlayScreamSound", $"Failed to play scream sound: {ex.Message}");
            }
        }

        private static float[] ConvertWavToPcm(Stream wavStream)
        {
            // Simple WAV to PCM conversion (assumes 16-bit mono WAV)
            // Skip WAV header (44 bytes)  
            wavStream.Seek(44, SeekOrigin.Begin);

            var buffer = new byte[wavStream.Length - 44];
            wavStream.Read(buffer, 0, buffer.Length);

            var samples = new float[buffer.Length / 2];
            for (int i = 0; i < samples.Length; i++)
            {
                short sample = BitConverter.ToInt16(buffer, i * 2);
                samples[i] = sample / 32768f; // Convert to -1.0f to 1.0f range
            }

            return samples;
        }
    }
}
