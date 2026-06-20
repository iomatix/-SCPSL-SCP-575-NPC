namespace SCP_575.Shared.Audio
{
    using AudioManagerAPI.Features.Enums;

    /// <summary>
    /// Global configuration profile for distinct manifest audio resources.
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
        {
            Key = key;
            Volume = volume;
            MinDistance = minDistance;
            MaxDistance = maxDistance;
            IsSpatial = isSpatial;
            Priority = priority;
            DefaultLifespan = defaultLifespan;
        }
    }
}