namespace SCP_575.Shared.Audio.Speakers
{
    using AudioManagerAPI.Defaults;
    using LabApi.Features.Wrappers;
    using SCP_575.Shared;
    using System;
    using System.Collections.Generic;
    using UnityEngine;



    /// <summary>
    /// Implements a LabAPI-specific speaker for SCP-575 audio playback using <see cref="SpeakerToy"/>.
    /// </summary>
    public partial class LabApiSpeaker : DefaultSpeakerToyAdapter
    {
        private readonly SpeakerToy speakerToy;
        private static readonly Dictionary<byte, LabApiSpeaker> speakerRegistry = new Dictionary<byte, LabApiSpeaker>();

        /// <summary>
        /// Initializes a new instance of the <see cref="LabApiSpeaker"/> class.
        /// </summary>
        /// <param name="speakerToy">The LabAPI speaker toy instance.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="speakerToy"/> is null.</exception>
        public LabApiSpeaker(SpeakerToy speakerToy) : base(speakerToy)
        {
            this.speakerToy = speakerToy ?? throw new ArgumentNullException(nameof(speakerToy));
            // Register the speaker instance
            if (!speakerRegistry.ContainsKey(speakerToy.ControllerId))
            {
                speakerRegistry[speakerToy.ControllerId] = this;
            }
        }

        /// <summary>
        /// Gets a <see cref="LabApiSpeaker"/> instance by its controller ID.
        /// </summary>
        /// <param name="controllerId">The controller ID of the speaker.</param>
        /// <returns>The <see cref="LabApiSpeaker"/> instance, or null if not found or unable to create.</returns>
        public static LabApiSpeaker GetSpeaker(byte controllerId)
        {
            // Check if speaker is already in the registry
            if (speakerRegistry.TryGetValue(controllerId, out LabApiSpeaker speaker))
            {
                return speaker;
            }

            // Attempt to get an existing SpeakerToy with the matching controllerId
            SpeakerToy speakerToy = null;
            foreach (var toy in SpeakerToy.List)
            {
                if (toy.ControllerId == controllerId)
                {
                    speakerToy = toy;
                    break;
                }
            }

            // If no SpeakerToy is found, create a new one
            if (speakerToy == null)
            {
                // Create with default position, rotation, and scale, no parent, and network spawn enabled
                speakerToy = SpeakerToy.Create(Vector3.zero, Quaternion.identity, Vector3.one, null, true);
                if (speakerToy == null)
                {
                    Library_ExiledAPI.LogWarn("GetSpeaker", $"Failed to create SpeakerToy for controller ID {controllerId}.");
                    return null;
                }
                speakerToy.ControllerId = controllerId; // Set the controllerId for the new SpeakerToy
            }

            // Verify the transmitter exists to ensure the controllerId is valid
            var transmitter = SpeakerToy.GetTransmitter(controllerId);
            if (transmitter == null)
            {
                Library_ExiledAPI.LogWarn("GetSpeaker", $"No transmitter found for controller ID {controllerId}.");
                return null;
            }

            // Create and register the new LabApiSpeaker
            speaker = new LabApiSpeaker(speakerToy);
            speakerRegistry[controllerId] = speaker;
            Library_ExiledAPI.LogDebug("GetSpeaker", $"Created and registered new LabApiSpeaker for controller ID {controllerId}.");
            return speaker;
        }

        /// <summary>
        /// Removes a speaker from the registry, typically called when destroying the speaker.
        /// </summary>
        /// <param name="controllerId">The controller ID of the speaker to remove.</param>
        public static void RemoveSpeaker(byte controllerId)
        {
            if (speakerRegistry.Remove(controllerId))
            {
                Library_ExiledAPI.LogDebug("RemoveSpeaker", $"Removed LabApiSpeaker for controller ID {controllerId} from registry.");
            }
        }

        /// <summary>
        /// Plays the provided audio samples with an option to loop.
        /// </summary>
        /// <param name="samples">The audio samples to play.</param>
        /// <param name="loop">Whether the audio should loop.</param>
        public void Play(float[] samples, bool loop)
        {
            var transmitter = SpeakerToy.GetTransmitter(speakerToy.ControllerId);
            if (transmitter != null)
            {
                transmitter.Play(samples, queue: false, loop: loop);
            }
            else
            {
                Library_ExiledAPI.LogWarn("Play", $"Failed to get transmitter for controller ID {speakerToy.ControllerId}.");
            }
        }

        /// <summary>
        /// Stops the currently playing audio.
        /// </summary>
        public void Stop()
        {
            var transmitter = SpeakerToy.GetTransmitter(speakerToy.ControllerId);
            if (transmitter != null)
            {
                transmitter?.Stop();
            }
            else
            {
                Library_ExiledAPI.LogWarn("Stop", $"Failed to get transmitter for controller ID {speakerToy.ControllerId}.");
            }
        }

        /// <summary>
        /// Destroys the speaker, releasing all associated resources.
        /// </summary>
        public void Destroy()
        {
            speakerToy.Destroy();
        }

        /// <summary>
        /// Configures which players can hear the audio using a custom filter.
        /// </summary>
        /// <param name="playerFilter">A function that determines which players can hear the audio.</param>
        public void SetValidPlayers(Func<Player, bool> playerFilter)
        {
            var transmitter = SpeakerToy.GetTransmitter(speakerToy.ControllerId);
            if (transmitter != null)
            {
                transmitter.ValidPlayers = playerFilter ?? (player => true); // Default to all players if filter is null
                Library_ExiledAPI.LogDebug("SetValidPlayers", $"Player filter updated for controller ID {speakerToy.ControllerId}.");
            }
            else
            {
                Library_ExiledAPI.LogWarn("SetValidPlayers", $"Cannot set valid players for controller ID {speakerToy.ControllerId}: Transmitter not found.");
            }
        }

        /// <summary>
        /// Configures which players can hear the audio using a combination of predefined filters.
        /// </summary>
        /// <param name="filters">A collection of filter functions to apply. A player must pass all filters to hear the audio.</param>
        public void SetValidPlayers(IEnumerable<Func<Player, bool>> filters)
        {
            Func<Player, bool> combinedFilter = player =>
            {
                if (player == null) return false;
                foreach (var filter in filters)
                {
                    if (!filter(player)) return false;
                }
                return true;
            };
            SetValidPlayers(combinedFilter);
        }

        /// <summary>
        /// Sets the volume level of the audio (0.0 to 1.0).
        /// </summary>
        /// <param name="volume">The volume level.</param>
        public void SetVolume(float volume)
        {
            speakerToy.Volume = Mathf.Clamp01(volume);
        }

        /// <summary>
        /// Sets the minimum distance where the audio starts to fall off.
        /// </summary>
        /// <param name="minDistance">The minimum distance in Unity units.</param>
        public void SetMinDistance(float minDistance)
        {
            speakerToy.MinDistance = Mathf.Max(0, minDistance);
        }

        /// <summary>
        /// Sets the maximum distance where the audio falls to zero.
        /// </summary>
        /// <param name="maxDistance">The maximum distance in Unity units.</param>
        public void SetMaxDistance(float maxDistance)
        {
            speakerToy.MaxDistance = Mathf.Max(0, maxDistance);
        }

        /// <summary>
        /// Sets whether the audio is spatialized (3D).
        /// </summary>
        /// <param name="isSpatial">Whether to use spatial audio.</param>
        public void SetSpatialization(bool isSpatial)
        {
            speakerToy.IsSpatial = isSpatial;
        }
    }
}
