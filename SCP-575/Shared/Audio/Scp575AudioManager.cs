namespace SCP_575.Shared.Audio
{
    using AudioManagerAPI.Defaults;
    using AudioManagerAPI.Features.Enums;
    using AudioManagerAPI.Features.Management;
    using AudioManagerAPI.Features.Management.Settings;
    using LabApi.Features.Wrappers;
    using MEC;
    using SCP_575.Shared;
    using SCP_575.Shared.Audio.Enums;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using UnityEngine;
    using Log = LabApi.Features.Console.Logger;

    /// <summary>
    /// Stateless driver and orchestrator for the SCP-575 audio subsystem.
    /// Acts as a direct bridge to the hardware sound engine, preventing channel starvation.
    /// </summary>
    public class Scp575AudioManager
    {
        private readonly Plugin _plugin;
        private readonly IAudioManager _audioEngine;
        private double _lastGlobalScreamTimeTicks = 0;

        private readonly Dictionary<int, int> _playerAmbienceSessions = new Dictionary<int, int>();
        private readonly HashSet<int> _activeTrackingSessionIds = new();
        private readonly Dictionary<AudioKey, AudioTrackGroup> _audioRegistry = new();
        private readonly Dictionary<int, DateTime> _transientCooldowns = new();

        private const string AudioCoroutineTag = CoroutineTags.AudioCoroutines;

        public Scp575AudioManager(Plugin plugin)
        {
            _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
            _audioEngine = DefaultAudioManager.Instance;

            PopulateAudioRegistry();
            RegisterAudioResources();
        }

        #region Public Playback API

        /// <summary>
        /// Plays a one-shot or looping global audio stinger across the entire facility.
        /// Dedicated environmental ambience loops must be routed through PlayAmbience() instead.
        /// </summary>
        public int PlayGlobal(AudioKey key, float? lifespan = null, bool queue = false, float fadeInDuration = 0f, bool loop = false)
        {

            var config = GetConfigOrThrow(key);
            if (IsScreamAsset(key) && !TryPassGlobalScreamCooldown()) return 0;

            float? finalLifespan = loop && (lifespan ?? config.DefaultLifespan) <= 0f ? null : (lifespan ?? config.DefaultLifespan);

            int sessionId = _audioEngine.PlayGlobalAudio(
                config.Key,
                loop,
                config.Volume,
                config.Priority,
                validPlayersFilter: null, // Safe for true global stinger roars
                queue,
                fadeInDuration,
                persistent: false,
                lifespan: finalLifespan,
                autoCleanup: !loop); // Prevent infinite loops from committing suicide via timers

            if (sessionId != 0)
            {
                _activeTrackingSessionIds.Add(sessionId);
            }

            return sessionId;
        }

        public int PlayAtPosition(AudioKey key, Vector3 position, float? lifespan = null, bool isTransient = false, Player sourcePlayer = null, bool loop = false)
        {
            var config = GetConfigOrThrow(key);
            if (isTransient && sourcePlayer != null && !TryAcquireTransientLock(sourcePlayer.GameObject.GetInstanceID(), key)) return 0;

            Vector3 targetPosition = SanitizePosition(position);

            Func<Player, bool> distanceFilter = p => p != null && p.IsReady && !p.IsHost
                && Vector3.Distance(p.Position, targetPosition) <= config.MaxDistance;

            float? targetLifespan = lifespan ?? config.DefaultLifespan;
            if (loop && (targetLifespan <= 0f))
            {
                targetLifespan = null;
            }

            int sessionId = _audioEngine.PlayAudio(
                config.Key, targetPosition, loop, config.Volume,
                config.MinDistance, config.MaxDistance, config.IsSpatial, config.Priority,
                validPlayersFilter: distanceFilter, queue: false, fadeInDuration: 0f,
                lifespan: targetLifespan, autoCleanup: !loop);

            if (sessionId != 0) _activeTrackingSessionIds.Add(sessionId);
            return sessionId;
        }

        public int PlayAttached(Player target, AudioKey key, bool hearableForAll = false, float? lifespan = null, float fadeInDuration = 0f, bool loop = false)
        {
            if (target == null) return 0;
            var config = GetConfigOrThrow(key);

            Vector3 playPosition = SanitizePosition(target.Position);
            Func<Player, bool> playerFilter = p => p != null && p.IsReady && !p.IsHost &&
                (hearableForAll
                    ? Vector3.Distance(p.Position, (target?.GameObject != null ? target.Position : playPosition)) <= config.MaxDistance
                    : p.GameObject.GetInstanceID() == target.GameObject.GetInstanceID());


            float? targetLifespan = lifespan ?? config.DefaultLifespan;
            if (loop && (targetLifespan <= 0f))
            {
                targetLifespan = null;
            }
            int sessionId = _audioEngine.PlayAudio(
                config.Key, playPosition, loop, config.Volume,
                config.MinDistance, config.MaxDistance, config.IsSpatial, config.Priority,
                validPlayersFilter: playerFilter, queue: false, fadeInDuration: fadeInDuration,
                lifespan: targetLifespan, autoCleanup: !loop);

            if (sessionId != 0) _activeTrackingSessionIds.Add(sessionId);
            return sessionId;
        }

        public void PlayTrackingAudio(Player player, AudioKey audioKey, float? lifespan = null, bool hearableForAllPlayers = true, Vector3? customOffset = null)
        {
            if (player?.GameObject == null || !player.IsReady) return;
            var profile = GetConfigOrThrow(audioKey);

            float effectiveLifespan = lifespan ?? profile.DefaultLifespan;
            if (effectiveLifespan <= 0f) return;

            Func<Vector3> locationProvider = () =>
            {
                if (player?.GameObject == null) return Vector3.zero;
                if (customOffset.HasValue) return player.Position + customOffset.Value;

                Transform transformTarget = player.GameObject.transform;
                return player.Position + (transformTarget.up * 1.65f) + (transformTarget.forward * 0.001f);
            };

            int sessionId = _audioEngine.PlayTrackingAudio(
                profile.Key, locationProvider, () => player != null && player.IsReady && player.IsAlive,
                profile.Priority, effectiveLifespan, hearableForAllPlayers ? null : p => p != null && p.GameObject.GetInstanceID() == player.GameObject.GetInstanceID(),
                profile.Volume, profile.MinDistance, profile.MaxDistance
            );

            if (sessionId != 0) _activeTrackingSessionIds.Add(sessionId);
        }

        public void PlayOrbitingAudio(Player player, AudioKey audioKey, float? lifespan = null, float maxRadius = 3.2f, float minRadius = 0.6f, float angularSpeed = 1.1f, float approachSpeed = 1.5f, bool isolated = false)
        {
            if (player == null || !player.IsAlive) return;
            var profile = GetConfigOrThrow(audioKey);

            float effectiveLifespan = lifespan ?? profile.DefaultLifespan;
            if (effectiveLifespan <= 0f) return;

            OrbitSettings orbitSettings = new(maxRadius, minRadius, angularSpeed, approachSpeed, 0.85f);
            int sessionId = _audioEngine.PlayOrbitingAudio(
                profile.Key,
                () => player.Position,
                () => player != null && player.IsAlive && _plugin.LibraryLabAPI.IsPlayerInDarkRoom(player),
                profile.Volume,
                profile.MinDistance,
                profile.MaxDistance,
                orbitSettings,
                profile.Priority,
                effectiveLifespan,
                targetPlayerFilter: isolated ? p => p != null && p.GameObject.GetInstanceID() == player.GameObject.GetInstanceID() : null
            );

            if (sessionId != 0) _activeTrackingSessionIds.Add(sessionId);
        }

        public void PlayOrbitingAudio(Vector3 staticPosition, AudioKey audioKey, float? lifespan = null, float maxRadius = 3.2f, float minRadius = 0.6f, float angularSpeed = 1.1f, float approachSpeed = 1.5f, float heightOffset = 0.85f)
        {
            var profile = GetConfigOrThrow(audioKey);
            float effectiveLifespan = lifespan ?? profile.DefaultLifespan;
            if (effectiveLifespan <= 0f) return;

            OrbitSettings orbitSettings = new(maxRadius, minRadius, angularSpeed, approachSpeed, heightOffset);
            int sessionId = _audioEngine.PlayOrbitingAudio(
                profile.Key, () => staticPosition, () => true, profile.Volume, profile.MinDistance, profile.MaxDistance,
                orbitSettings, profile.Priority, effectiveLifespan, targetPlayerFilter: null
            );

            if (sessionId != 0) _activeTrackingSessionIds.Add(sessionId);
        }

        public int PlayAmbienceForPlayer(Player player, float fadeInDuration = 3.0f)
        {
            if (player == null || !player.IsReady || player.IsSCP || player.IsHost) return 0;

            int playerAssetId = player.GameObject.GetInstanceID();

            if (_playerAmbienceSessions.TryGetValue(playerAssetId, out int existingSessionId) && existingSessionId != 0)
            {
                return existingSessionId;
            }

            var config = GetConfigOrThrow(AudioKey.Ambience);

            int sessionId = PlayAttached(
                target: player,
                key: AudioKey.Ambience,
                hearableForAll: false,
                lifespan: null,
                fadeInDuration: fadeInDuration,
                loop: true
            );

            if (sessionId != 0)
            {
                _playerAmbienceSessions[playerAssetId] = sessionId;
            }

            return sessionId;
        }

        public void StopAmbienceForPlayer(Player player)
        {
            if (player == null) return;

            int playerAssetId = player.GameObject.GetInstanceID();

            if (_playerAmbienceSessions.TryGetValue(playerAssetId, out int sessionId) && sessionId != 0)
            {
                StopSession(sessionId);
                _playerAmbienceSessions.Remove(playerAssetId);
            }
        }

        public void StopSession(int sessionId)
        {
            if (sessionId == 0) return;
            try
            {
                _audioEngine.FadeOutAudio(sessionId, _plugin.Config.AudioConfig.DefaultFadeDuration);
            }
            catch (Exception ex)
            {
                Log.Debug($"[Scp575AudioManager] Engine exception during session fadeout {sessionId}: {ex.Message}");
            }
            finally
            {
                _activeTrackingSessionIds.Remove(sessionId);
            }
        }

        public void Clean(bool fullShutdown = false)
        {
            Timing.KillCoroutines(AudioCoroutineTag);

            if (fullShutdown)
            {
                foreach (int sessionId in _playerAmbienceSessions.Values.ToList())
                {
                    if (sessionId != 0) _audioEngine.FadeOutAudio(sessionId, _plugin.Config.AudioConfig.DefaultFadeDuration);
                }
                _playerAmbienceSessions.Clear();
            }

            foreach (int id in _activeTrackingSessionIds.ToList())
            {
                StopSession(id);
            }
            _activeTrackingSessionIds.Clear();
            _transientCooldowns.Clear();
        }

        #endregion

        #region Internal Infrastructure

        private bool IsScreamAsset(AudioKey key) =>
            key is AudioKey.ScreamStandard or AudioKey.ScreamAngry or AudioKey.ScreamHurt or AudioKey.ScreamDying;

        private bool TryPassGlobalScreamCooldown()
        {
            double secondsSinceLastScream = Timing.LocalTime - _lastGlobalScreamTimeTicks;
            if (secondsSinceLastScream < _plugin.Config.AudioConfig.GlobalScreamCooldown) return false;
            _lastGlobalScreamTimeTicks = Timing.LocalTime;
            return true;
        }

        private AudioTrackProfile GetConfigOrThrow(AudioKey key)
        {
            if (!_audioRegistry.TryGetValue(key, out var group))
                throw new ArgumentException($"Audio key '{key}' missing from database.", nameof(key));
            return group.GetNextProfile();
        }

        private Vector3 SanitizePosition(Vector3 position)
        {
            if (float.IsNaN(position.x) || float.IsNaN(position.y) || float.IsNaN(position.z) ||
                float.IsInfinity(position.x) || float.IsInfinity(position.y) || float.IsInfinity(position.z))
            {
                return Vector3.zero;
            }
            return position;
        }

        private bool TryAcquireTransientLock(int playerInstanceId, AudioKey key)
        {
            DateTime currentTime = DateTime.UtcNow;
            int compositeHash = playerInstanceId ^ (int)key;

            if (_transientCooldowns.TryGetValue(compositeHash, out DateTime nextAllowedTime) && currentTime < nextAllowedTime) return false;
            _transientCooldowns[compositeHash] = currentTime + TimeSpan.FromMilliseconds(90);
            return true;
        }

        private void PopulateAudioRegistry()
        {
            _audioRegistry[AudioKey.ScreamStandard] = new AudioTrackGroup(
                new AudioTrackProfile("scp575.scream_1", 0.95f, 65f, 150f, true, AudioPriority.High, 9f),
                new AudioTrackProfile("scp575.scream_2", 0.95f, 65f, 150f, true, AudioPriority.High, 9f),
                new AudioTrackProfile("scp575.scream_3", 0.95f, 65f, 150f, true, AudioPriority.High, 9f)
            );
            _audioRegistry[AudioKey.ScreamAngry] = new AudioTrackGroup(new AudioTrackProfile("scp575.scream_angry", 0.95f, 175f, 450f, true, AudioPriority.High, 9f));
            _audioRegistry[AudioKey.ScreamHurt] = new AudioTrackGroup(new AudioTrackProfile("scp575.scream_hurt", 0.95f, 125f, 345f, true, AudioPriority.High, 7f));
            _audioRegistry[AudioKey.ScreamDying] = new AudioTrackGroup(new AudioTrackProfile("scp575.scream_dying", 0.95f, 255f, 480f, true, AudioPriority.High, 20f));
            _audioRegistry[AudioKey.MonsterRoarGlobal] = new AudioTrackGroup(new AudioTrackProfile("scp575.monster_roar_global", 0.85f, 45f, 999.99f, false, AudioPriority.High, 40f));
            _audioRegistry[AudioKey.MonsterBreathLocal] = new AudioTrackGroup(new AudioTrackProfile("scp575.monster_breath_local", 0.75f, 5f, 24f, true, AudioPriority.High, 11f));
            _audioRegistry[AudioKey.WhispersSubtle] = new AudioTrackGroup(
                new AudioTrackProfile("scp575.whispers_3", 0.55f, 9f, 52f, true, AudioPriority.Medium, 9f),
                new AudioTrackProfile("scp575.whispers_4", 0.55f, 9f, 52f, true, AudioPriority.Medium, 8f),
                new AudioTrackProfile("scp575.whispers_5", 0.55f, 9f, 52f, true, AudioPriority.Medium, 14f)
            );
            _audioRegistry[AudioKey.WhispersDisturbed] = new AudioTrackGroup(
                new AudioTrackProfile("scp575.whispers_6", 0.55f, 9f, 52f, true, AudioPriority.Medium, 17f),
                new AudioTrackProfile("scp575.whispers_7", 0.55f, 9f, 52f, true, AudioPriority.Medium, 28f)
            );
            _audioRegistry[AudioKey.WhispersPsychotic] = new AudioTrackGroup(
                new AudioTrackProfile("scp575.whispers", 0.55f, 5f, 35f, true, AudioPriority.Medium, 10f),
                new AudioTrackProfile("scp575.whispers_1", 0.55f, 5f, 35f, true, AudioPriority.Medium, 11f),
                new AudioTrackProfile("scp575.whispers_2", 0.55f, 7f, 45f, true, AudioPriority.Medium, 19f),
                new AudioTrackProfile("scp575.whispers_mixed", 0.65f, 10f, 55f, true, AudioPriority.Medium, 25f)
            );
            _audioRegistry[AudioKey.WhispersShockStinger] = new AudioTrackGroup(new AudioTrackProfile("scp575.whispers_bang", 0.65f, 12f, 65f, true, AudioPriority.High, 20f));
            _audioRegistry[AudioKey.WhispersPanicDrone] = new AudioTrackGroup(new AudioTrackProfile("scp575.whispers_long_drones", 0.65f, 12f, 65f, true, AudioPriority.High, 0f));
            _audioRegistry[AudioKey.ShadowClicking] = new AudioTrackGroup(new AudioTrackProfile("scp575.shadow_clicking", 0.55f, 4.75f, 33f, true, AudioPriority.High, 9f));
            _audioRegistry[AudioKey.ShadowStrike] = new AudioTrackGroup(new AudioTrackProfile("scp575.shadow_strike", 0.8f, 5.5f, 30f, true, AudioPriority.High, 5f));
            _audioRegistry[AudioKey.ShadowConsumingBody] = new AudioTrackGroup(new AudioTrackProfile("scp575.shadow_consuming_body", 0.95f, 7.5f, 45f, true, AudioPriority.High, 5f));
            _audioRegistry[AudioKey.AnomalousImpact] = new AudioTrackGroup(new AudioTrackProfile("scp575.anomalous_impact", 0.9f, 3.5f, 25f, true, AudioPriority.High, 5f));
            _audioRegistry[AudioKey.GeneratorHumDefense] = new AudioTrackGroup(new AudioTrackProfile("scp575.generator_hum_defense", 0.7f, 6.5f, 45f, true, AudioPriority.Medium, 0f));
            _audioRegistry[AudioKey.LightShortCircuit] = new AudioTrackGroup(new AudioTrackProfile("scp575.light_short_circuit", 0.75f, 2.5f, 15f, true, AudioPriority.High, 1.5f));
            _audioRegistry[AudioKey.LightSwitch] = new AudioTrackGroup(
                new AudioTrackProfile("scp575.light_switch_1", 0.85f, 2.5f, 18f, true, AudioPriority.Max, 1.5f),
                new AudioTrackProfile("scp575.light_switch_2", 0.85f, 2.5f, 18f, true, AudioPriority.Max, 1.5f),
                new AudioTrackProfile("scp575.light_switch_3", 0.85f, 2.5f, 18f, true, AudioPriority.Max, 1.5f)
            );
            _audioRegistry[AudioKey.LightShortCircuitFinal] = new AudioTrackGroup(new AudioTrackProfile("scp575.light_short_circuit", 0.91f, 3.5f, 25.0f, true, AudioPriority.Max, 2.5f));

            _audioRegistry[AudioKey.StaticBuzz] = new AudioTrackGroup(new AudioTrackProfile("scp575.light_short_circuit", 0.45f, 1.5f, 10f, true, AudioPriority.Low, 0f));
            _audioRegistry[AudioKey.BlackoutImpactGlobal] = new AudioTrackGroup(new AudioTrackProfile("scp575.blackout_impact_global", 0.95f, 0f, 999.99f, false, AudioPriority.High, 13f));
            _audioRegistry[AudioKey.Ambience] = new AudioTrackGroup(new AudioTrackProfile("scp575.ambience", 0.6f, 0f, 999.99f, false, AudioPriority.Low, 0f));
            _audioRegistry[AudioKey.SanityLowDrone] = new AudioTrackGroup(new AudioTrackProfile("scp575.sanity_low_drone", 0.6f, 200.0f, 999.99f, false, AudioPriority.Medium, 0f));
        }

        private void RegisterAudioResources()
        {
            var assembly = Assembly.GetExecutingAssembly();
            string[] allResourceNames = assembly.GetManifestResourceNames();

            foreach (var group in _audioRegistry.Values)
            {
                foreach (var profile in group.Profiles)
                {
                    string key = profile.Key;
                    string resourceName = allResourceNames.FirstOrDefault(r =>
                        r.EndsWith($"{key}.wav", StringComparison.OrdinalIgnoreCase) ||
                        r.EndsWith($"{key.Replace(".", "_")}.wav", StringComparison.OrdinalIgnoreCase));

                    if (string.IsNullOrEmpty(resourceName)) continue;
                    _audioEngine.RegisterAudio(key, () => assembly.GetManifestResourceStream(resourceName));
                }
            }
        }

        #endregion

        #region Nested Structural Components

        private sealed class AudioTrackGroup
        {
            private readonly List<AudioTrackProfile> _profiles;
            private readonly List<AudioTrackProfile> _shuffleBag = new();
            private readonly System.Random _random = new();

            public IReadOnlyList<AudioTrackProfile> Profiles => _profiles;

            public AudioTrackGroup(params AudioTrackProfile[] profiles)
            {
                if (profiles == null || profiles.Length == 0)
                    throw new ArgumentException("Group must possess configuration profiles.", nameof(profiles));
                _profiles = new List<AudioTrackProfile>(profiles);
            }

            public AudioTrackProfile GetNextProfile()
            {
                if (_profiles.Count == 1) return _profiles[0];
                if (_shuffleBag.Count == 0) _shuffleBag.AddRange(_profiles);

                int index = _random.Next(_shuffleBag.Count);
                AudioTrackProfile selected = _shuffleBag[index];
                _shuffleBag.RemoveAt(index);
                return selected;
            }
        }

        #endregion
    }
}