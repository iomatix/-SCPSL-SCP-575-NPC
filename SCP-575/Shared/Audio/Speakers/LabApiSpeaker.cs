namespace Shared.Audio.Speakers
{
    using AudioManagerAPI.Defaults;
    using AudioManagerAPI.Features.Speakers;
    using LabApi.Features.Wrappers;
    using SCP_575.Shared;
    using System;
    using UnityEngine;

    /// <summary>
    /// Implements a LabAPI-specific speaker for SCP-575 audio playback using <see cref="SpeakerToy"/>.
    /// </summary>
    public class LabApiSpeaker : DefaultSpeakerToyAdapter
    {
        private readonly SpeakerToy speakerToy;

        /// <summary>
        /// Initializes a new instance of the <see cref="LabApiSpeaker"/> class.
        /// </summary>
        /// <param name="speakerToy">The LabAPI speaker toy instance.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="speakerToy"/> is null.</exception>
        public LabApiSpeaker(SpeakerToy speakerToy) : base(speakerToy)
        {
            this.speakerToy = speakerToy ?? throw new ArgumentNullException(nameof(speakerToy));
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
                // Assuming Exiled's Log class for SCP:SL
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
                // Assuming Exiled's Log class for SCP:SL
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
        /// Configures which players can hear the audio.
        /// </summary>
        /// <param name="playerFilter">A function that determines which players can hear the audio.</param>
        public void SetValidPlayers(Func<Player, bool> playerFilter)
        {
            var transmitter = SpeakerToy.GetTransmitter(speakerToy.ControllerId);
            if (transmitter != null)
            {
                transmitter.ValidPlayers = playerFilter;
            }
            else
            {
                Library_ExiledAPI.LogDebug("SetValidPlayers", $"Cannot set valid players for controller ID {speakerToy.ControllerId}: Transmitter not found.");
            }
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