using SCP_575.Shared;

namespace Shared.Audio.Speakers
{
    using UnityEngine;
    using AudioManagerAPI.Features.Speakers;
    using LabApi.Features.Wrappers;

    /// <summary>
    /// Creates LabAPI-compatible speakers for SCP-575 audio playback.
    /// </summary>
    public class LabApiSpeakerFactory : ISpeakerFactory
    {
        /// <summary>
        /// Creates a new speaker at the specified position with a unique controller ID.
        /// </summary>
        /// <param name="position">The 3D position where the speaker should be created.</param>
        /// <param name="controllerId">The unique identifier for the speaker.</param>
        /// <returns>A new <see cref="ISpeaker"/> instance, or <c>null</c> if creation fails.</returns>
        public ISpeaker CreateSpeaker(Vector3 position, byte controllerId)
        {
            SpeakerToy speaker = SpeakerToy.Create(position, networkSpawn: true);
            if (speaker == null)
            {
                Library_ExiledAPI.LogWarn("CreateSpeaker", $"Failed to create SpeakerToy at position {position}.");
                return null;
            }
            speaker.ControllerId = controllerId;
            return new LabApiSpeaker(speaker);
        }
    }
}