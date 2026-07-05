namespace SCP_575.Shared.Audio
{
    using AudioManagerAPI.Features.Enums;

    /// <summary>
    /// Configuration profile for specific audio stream assets.
    /// </summary>
    public sealed class AudioTrackProfile
    {
        public string Key { get; }
        public float Volume { get; }
        public float MinDistance { get; }
        public float MaxDistance { get; }
        public bool IsSpatial { get; }
        public AudioPriority Priority { get; }
        public float DefaultLifespan { get; }

        public AudioTrackProfile(string key, float volume, float minDistance, float maxDistance, bool isSpatial, AudioPriority priority, float defaultLifespan)
            => (Key, Volume, MinDistance, MaxDistance, IsSpatial, Priority, DefaultLifespan) = (key, volume, minDistance, maxDistance, isSpatial, priority, defaultLifespan);
    }
}