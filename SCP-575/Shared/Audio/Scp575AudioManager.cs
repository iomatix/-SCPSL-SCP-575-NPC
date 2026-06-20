namespace SCP_575.Shared.Audio
{
    using AudioManagerAPI.Defaults;
    using AudioManagerAPI.Features.Enums;
    using AudioManagerAPI.Features.Filters;
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
    /// Central orchestrator for the SCP-575 audio subsystem.
    /// Manages hardware playback sessions, environment tracking layers, and non-repetitive horror ambiance
    /// while safeguarding the underlying sound engine against audio channel starvation and memory leaks.
    /// </summary>
    public class Scp575AudioManager
    {
        private readonly Plugin _plugin;
        private readonly IAudioManager _audioEngine;
        private double _lastGlobalScreamTimeTicks = 0;
        private int _ambienceAudioSessionId;

        private readonly Dictionary<string, int> _activeDroneSessions = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<CooldownKey, double> _transientCooldowns = new();
        private readonly HashSet<int> _generatorSessionIds = new();
        private readonly HashSet<int> _pluginSessionIds = new();

        private const string AudioCoroutineTag = CoroutineTags.AudioCoroutines;

        /// <summary>
        /// Maps semantic gameplay triggers to asset variation decks.
        /// Explicit instantiation is enforced to bypass Roslyn array target-typing ambiguities (CS8752).
        /// </summary>
        private readonly Dictionary<AudioKey, AudioTrackGroup> _audioRegistry = new()
        {
            // --- ENTITY VOCALIZATIONS & REACTIONS ---
            { AudioKey.ScreamStandard, new AudioTrackGroup(
                new AudioTrackProfile("scp575.scream_1", 0.95f, 65f, 150f, true, AudioPriority.High, 9f),
                new AudioTrackProfile("scp575.scream_2", 0.95f, 65f, 150f, true, AudioPriority.High, 9f),
                new AudioTrackProfile("scp575.scream_3", 0.95f, 65f, 150f, true, AudioPriority.High, 9f)
            )},
            { AudioKey.ScreamAngry, new AudioTrackGroup(new AudioTrackProfile("scp575.scream_angry", 0.95f, 175f, 450f, true, AudioPriority.High, 9f)) },
            { AudioKey.ScreamHurt, new AudioTrackGroup(new AudioTrackProfile("scp575.scream_hurt", 0.95f, 125f, 345f, true, AudioPriority.High, 7f)) },
            { AudioKey.ScreamDying, new AudioTrackGroup(new AudioTrackProfile("scp575.scream_dying", 0.95f, 255f, 480f, true, AudioPriority.High, 20f)) },
            { AudioKey.MonsterRoarGlobal, new AudioTrackGroup(new AudioTrackProfile("scp575.monster_roar_global", 0.85f, 45f, 999.99f, false, AudioPriority.High, 40f)) },
            { AudioKey.MonsterBreathLocal, new AudioTrackGroup(new AudioTrackProfile("scp575.monster_breath_local", 0.75f, 5f, 24f, true, AudioPriority.High, 11f)) },

            // --- PSYCHOLOGICAL PARANOIA & SANITY DECAY (TIERED PACING) ---
            { AudioKey.WhispersSubtle, new AudioTrackGroup(
                new AudioTrackProfile("scp575.whispers_1", 0.55f, 5f, 35f, true, AudioPriority.Medium, 11f),
                new AudioTrackProfile("scp575.whispers_2", 0.55f, 7f, 45f, true, AudioPriority.Medium, 19f)
            )},
            { AudioKey.WhispersDisturbed, new AudioTrackGroup(
                new AudioTrackProfile("scp575.whispers_3", 0.55f, 9f, 52f, true, AudioPriority.Medium, 9f),
                new AudioTrackProfile("scp575.whispers_4", 0.55f, 9f, 52f, true, AudioPriority.Medium, 8f),
                new AudioTrackProfile("scp575.whispers_5", 0.55f, 9f, 52f, true, AudioPriority.Medium, 14f),
                new AudioTrackProfile("scp575.whispers_6", 0.55f, 9f, 52f, true, AudioPriority.Medium, 17f),
                new AudioTrackProfile("scp575.whispers_7", 0.55f, 9f, 52f, true, AudioPriority.Medium, 28f)
            )},
            { AudioKey.WhispersPsychotic, new AudioTrackGroup(new AudioTrackProfile("scp575.whispers_mixed", 0.65f, 10f, 55f, true, AudioPriority.Medium, 25f)) },
            { AudioKey.WhispersShockStinger, new AudioTrackGroup(
                new AudioTrackProfile("scp575.whispers_bang", 0.65f, 12f, 65f, true, AudioPriority.High, 20f),
                new AudioTrackProfile("scp575.whispers_bang_alt", 0.65f, 12f, 65f, true, AudioPriority.High, 12f)
            )},
            { AudioKey.WhispersPanicDrone, new AudioTrackGroup(new AudioTrackProfile("scp575.whispers_long_drones", 0.65f, 12f, 65f, true, AudioPriority.High, 0f)) },
            { AudioKey.ShadowClicking, new AudioTrackGroup(new AudioTrackProfile("scp575.shadow_clicking", 0.55f, 4.75f, 33f, true, AudioPriority.High, 9f)) },

            // --- KINETIC TRAUMA & TACTICAL INTERACTIONS ---
            { AudioKey.ShadowStrike, new AudioTrackGroup(new AudioTrackProfile("scp575.shadow_strike", 0.8f, 5.5f, 30f, true, AudioPriority.High, 5f)) },
            { AudioKey.ShadowConsumingBody, new AudioTrackGroup(new AudioTrackProfile("scp575.shadow_consuming_body", 0.95f, 7.5f, 45f, true, AudioPriority.High, 5f)) },
            { AudioKey.AnomalousImpact, new AudioTrackGroup(new AudioTrackProfile("scp575.anomalous_impact", 0.9f, 3.5f, 25f, true, AudioPriority.High, 5f)) },
            { AudioKey.GeneratorHumDefense, new AudioTrackGroup(new AudioTrackProfile("scp575.generator_hum_defense", 0.7f, 6.5f, 45f, true, AudioPriority.Medium, 0f)) },
            { AudioKey.LightShortCircuit, new AudioTrackGroup(new AudioTrackProfile("scp575.light_short_circuit", 0.85f, 2.5f, 18f, true, AudioPriority.Max, 1.5f)) },
            { AudioKey.LightSwitch, new AudioTrackGroup(
                new AudioTrackProfile("scp575.light_switch_1", 0.85f, 2.5f, 18f, true, AudioPriority.Max, 1.5f),
                new AudioTrackProfile("scp575.light_switch_2", 0.85f, 2.5f, 18f, true, AudioPriority.Max, 1.5f),
                new AudioTrackProfile("scp575.light_switch_3", 0.85f, 2.5f, 18f, true, AudioPriority.Max, 1.5f)
            )},
            { AudioKey.StaticBuzz, new AudioTrackGroup(new AudioTrackProfile("scp575.light_short_circuit", 0.45f, 1.5f, 10f, true, AudioPriority.Low, 0f)) },
            { AudioKey.BlackoutImpactGlobal, new AudioTrackGroup(new AudioTrackProfile("scp575.blackout_impact_global", 0.95f, 0f, 999.99f, false, AudioPriority.High, 13f)) },

            // --- BACKGROUND SPATIAL TEXTURES ---
            { AudioKey.Ambience, new AudioTrackGroup(new AudioTrackProfile("scp575.ambience", 0.6f, 0f, 999.99f, false, AudioPriority.Low, 0f)) },
            { AudioKey.SanityLowDrone, new AudioTrackGroup(new AudioTrackProfile("scp575.sanity_low_drone", 0.6f, 200.0f, 999.99f, false, AudioPriority.Medium, 0f)) }
        };

        public Scp575AudioManager(Plugin plugin)
        {
            _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin), "Plugin context wrapper must be provided to initialize audio config structures.");
            _audioEngine = DefaultAudioManager.Instance;
            RegisterAudioResources();
        }

        #region Public Execution Interface

        /// <summary>
        /// Emits non-spatialized atmospheric textures globally to all connected clients.
        /// Intended for facility-wide environmental state shifts or unlocalized creature announcements.
        /// </summary>
        public int PlayGlobal(AudioKey key, float? lifespan = null, bool queue = false, float fadeInDuration = 0f, bool loop = false)
        {
            var config = GetConfigOrThrow(key);
            if (IsScreamAsset(key) && !TryPassGlobalScreamCooldown()) return 0;

            if (key == AudioKey.Ambience && _ambienceAudioSessionId != 0) StopAmbience();

            int sessionId = _audioEngine.PlayGlobalAudio(
                config.Key, loop: loop, volume: config.Volume, priority: config.Priority,
                validPlayersFilter: null, queue: queue, fadeInDuration: fadeInDuration, lifespan: lifespan, autoCleanup: true);

            if (sessionId != 0) RegisterSessionLifetime(sessionId, key, loop, null);
            return sessionId;
        }

        /// <summary>
        /// Materializes a localized static 3D audio emitter anchored to structural vectors.
        /// Routes playback based on loop context to handle persistent environmental mechanics.
        /// </summary>
        public int PlayAtPosition(AudioKey key, Vector3 position, float? lifespan = null, bool isTransient = false, Player sourcePlayer = null, bool loop = false)
        {
            var config = GetConfigOrThrow(key);
            if (isTransient && sourcePlayer != null && !TryAcquireTransientLock(sourcePlayer.UserId, key)) return 0;

            Vector3 targetPosition = SanitizePosition(position);

            if (key == AudioKey.GeneratorHumDefense || loop)
            {
                bool isGenerator = key == AudioKey.GeneratorHumDefense;
                Func<Player, bool> distanceFilter = p => p != null && p.IsReady && !p.IsHost
                    && Vector3.Distance(p.Position, targetPosition) <= config.MaxDistance;

                int sessionId = _audioEngine.PlayAudio(
                    config.Key, targetPosition, loop: true, config.Volume,
                    config.MinDistance, config.MaxDistance, config.IsSpatial, config.Priority,
                    validPlayersFilter: distanceFilter, queue: false, fadeInDuration: 0f, lifespan: lifespan, autoCleanup: true);

                if (sessionId != 0)
                {
                    if (isGenerator)
                        _generatorSessionIds.Add(sessionId);
                    else
                        _pluginSessionIds.Add(sessionId);
                }
                return sessionId;
            }

            var sessions = _audioEngine.PlaySpatialSmart(
                config.Key, targetPosition, sourcePlayer, config.Priority,
                lifespan ?? config.DefaultLifespan, config.Volume, config.MinDistance, config.MaxDistance);

            if (sessions.worldSessionId != 0) _pluginSessionIds.Add(sessions.worldSessionId);
            if (sessions.sourceSessionId != 0) _pluginSessionIds.Add(sessions.sourceSessionId);

            return sessions.worldSessionId;
        }

        /// <summary>
        /// Binds a spatialized audio tracking node directly to a target client transform topology.
        /// Isolates audio to private headspaces or broadcasts public 3D cues based on parameters.
        /// </summary>
        public int PlayAttached(Player target, AudioKey key, bool hearableForAll = false, float? lifespan = null, float fadeInDuration = 0f, bool isTransient = false, bool loop = false)
        {
            if (target == null) throw new ArgumentNullException(nameof(target), "Cannot attach physical acoustics to a null entity context.");
            var config = GetConfigOrThrow(key);

            if (isTransient && !TryAcquireTransientLock(target.UserId, key)) return 0;

            Vector3 playPosition = SanitizePosition(target.Position);
            Func<Player, bool> playerFilter = CreatePlayerFilter(target, hearableForAll, playPosition, config.MaxDistance);

            int sessionId = _audioEngine.PlayAudio(
                config.Key, playPosition, loop || key == AudioKey.GeneratorHumDefense, volume: config.Volume,
                minDistance: config.MinDistance, maxDistance: config.MaxDistance, isSpatial: config.IsSpatial, priority: config.Priority,
                validPlayersFilter: playerFilter, queue: false, fadeInDuration: fadeInDuration, lifespan: lifespan, autoCleanup: true);

            if (sessionId != 0) RegisterSessionLifetime(sessionId, key, loop, target.UserId);
            return sessionId;
        }

        /// <summary>
        /// Spawns the central environmental background loop with built-in dark room visibility criteria.
        /// Maintains core match horror tension exclusively for unlit vulnerable actors.
        /// </summary>
        public int PlayAmbience(bool loop = true, float fadeInDuration = 0f, bool queue = false)
        {
            if (_ambienceAudioSessionId != 0) return _ambienceAudioSessionId;

            var config = GetConfigOrThrow(AudioKey.Ambience);
            Func<Player, bool> blackoutFilter = p => p != null && p.IsReady && !p.IsHost && _plugin.IsEventActive && _plugin.LibraryLabAPI.IsPlayerInDarkRoom(p);

            int sessionId = _audioEngine.PlayGlobalAudio(
                config.Key, loop, config.Volume, config.Priority,
                validPlayersFilter: blackoutFilter, queue, fadeInDuration, persistent: true, lifespan: null, autoCleanup: true);

            if (sessionId != 0) _ambienceAudioSessionId = sessionId;
            return sessionId;
        }

        /// <summary>
        /// Gracefully dampens and releases the environment lock on background horror assets.
        /// Relieves player stress metrics during light restoration phases.
        /// </summary>
        public void StopAmbience()
        {
            if (_ambienceAudioSessionId == 0) return;
            FadeOutSessionInternal(_ambienceAudioSessionId);
            _ambienceAudioSessionId = 0;
        }

        #region Kinetic Movement Translators

        /// <summary>
        /// Forces an acoustic vector loop to orbit around a shifting target client position.
        /// Simulates proximity stalkers or complex private localized psychological breakdowns.
        /// </summary>
        public void PlayOrbitingAudio(Player player, AudioKey audioKey, float? lifespan = null, float maxRadius = 3.2f, float minRadius = 0.6f, float angularSpeed = 1.1f, float approachSpeed = 1.5f, bool isolated = false)
        {
            PlayOrbitingAudioCore(() => player.Position, () => player != null && player.IsAlive, isolated ? player : null, audioKey, lifespan, maxRadius, minRadius, angularSpeed, approachSpeed);
        }

        /// <summary>
        /// Forces an acoustic vector loop to rotate around fixed structural geometry.
        /// Intended for unauthored spatial anomalies or localized mechanical malfunctions.
        /// </summary>
        public void PlayOrbitingAudio(Vector3 staticPosition, AudioKey audioKey, float? lifespan = null, float maxRadius = 3.2f, float minRadius = 0.6f, float angularSpeed = 1.1f, float approachSpeed = 1.5f, float heightOffset = 0.85f)
        {
            PlayOrbitingAudioCore(() => staticPosition, () => true, null, audioKey, lifespan, maxRadius, minRadius, angularSpeed, approachSpeed, heightOffset);
        }

        private void PlayOrbitingAudioCore(Func<Vector3> positionProvider, Func<bool> validationCheck, Player listener, AudioKey audioKey, float? lifespan = null, float maxRadius = 3.2f, float minRadius = 0.6f, float angularSpeed = 1.1f, float approachSpeed = 1.5f, float heightOffset = 0.85f)
        {
            if (positionProvider == null || validationCheck == null || !validationCheck()) return;
            var profile = GetConfigOrThrow(audioKey);

            float effectiveLifespan = lifespan ?? profile.DefaultLifespan;
            if (effectiveLifespan <= 0f) return;

            OrbitSettings orbitSettings = new(maxRadius, minRadius, angularSpeed, approachSpeed, heightOffset);

            int sessionId = _audioEngine.PlayOrbitingAudio(
                profile.Key, positionProvider, validationCheck, profile.Volume, profile.MinDistance, profile.MaxDistance,
                orbitSettings, profile.Priority, effectiveLifespan,
                targetPlayerFilter: listener == null ? null : p => p != null && p.UserId == listener.UserId
            );

            if (sessionId != 0) _pluginSessionIds.Add(sessionId);
        }

        /// <summary>
        /// Locks a moving audio vector to trace a client's chest height and orientation.
        /// Simulates internal biological sound generation or physical shadows attached to a body.
        /// </summary>
        public void PlayTrackingAudio(Player player, AudioKey audioKey, float? lifespan = null, bool hearableForAllPlayers = true, Vector3? customOffset = null)
        {
            if (player == null || !player.IsReady) return;
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
                profile.Priority, effectiveLifespan, hearableForAllPlayers ? null : p => p != null && p.UserId == player.UserId,
                profile.Volume, profile.MinDistance, profile.MaxDistance
            );

            if (sessionId != 0) _pluginSessionIds.Add(sessionId);
        }

        #endregion

        #region Behavioral Probability Matrices

        /// <summary>
        /// Introduces randomized acoustic anomalies to interrupt monotonous loops during survival phases.
        /// Breaks pattern recognition metrics to heighten baseline gameplay stress.
        /// </summary>
        public void PlayRandomAudioEffect(Player player, params AudioKey[] options)
        {
            // FIXED: Updated legacy WhispersMixed identifier to its semantic equivalent WhispersPsychotic
            var pool = (options == null || options.Length == 0)
                ? new[] { AudioKey.WhispersPsychotic, AudioKey.WhispersSubtle, AudioKey.WhispersDisturbed, AudioKey.ShadowClicking }
                : options;

            PlayAttached(player, pool[UnityEngine.Random.Range(0, pool.Length)], hearableForAll: false);
        }

        /// <summary>
        /// Injects violent audio cues directly into headspaces to simulate hunting activities.
        /// Artificially simulates sudden close-quarters proximity panic factors.
        /// </summary>
        public void PlayAggressiveAudio(Player player)
        {
            EvaluateAndPlayProbability(player, new[] { AudioKey.AnomalousImpact }, 0.15f, orbit: false, isolated: true);
            EvaluateAndPlayProbability(player, new[] { AudioKey.ShadowStrike }, 0.10f, orbit: false, isolated: true);
            EvaluateAndPlayProbability(player, new[] { AudioKey.ScreamStandard, AudioKey.ScreamAngry }, 0.10f, orbit: true);
            EvaluateAndPlayProbability(player, new[] { AudioKey.ShadowClicking }, 0.04f, orbit: false, isolated: true);
        }

        /// <summary>
        /// Coordinates low-frequency background warning stingers to telegraph defensive anomalies.
        /// Deliberately drops gameplay pacing prior to physical entity manifestations.
        /// </summary>
        public void PlayDefensiveAudio(Player player)
        {
            EvaluateAndPlayProbability(player, new[] { AudioKey.AnomalousImpact }, 0.10f, orbit: false, isolated: true);
            EvaluateAndPlayProbability(player, new[] { AudioKey.ShadowStrike }, 0.05f, orbit: false, isolated: true);
            EvaluateAndPlayProbability(player, new[] { AudioKey.WhispersSubtle, AudioKey.WhispersDisturbed, AudioKey.WhispersShockStinger }, 0.10f, orbit: true);
            EvaluateAndPlayProbability(player, new[] { AudioKey.ShadowClicking }, 0.07f, orbit: false, isolated: true);
        }

        private void EvaluateAndPlayProbability(Player player, AudioKey[] pool, float chance, bool orbit, bool isolated = false)
        {
            if (player == null || UnityEngine.Random.value > chance) return;
            AudioKey selected = pool[UnityEngine.Random.Range(0, pool.Length)];

            if (orbit)
            {
                PlayOrbitingAudio(player, selected);
            }
            else if (isolated)
            {
                PlayAttached(player, selected, hearableForAll: false, lifespan: null, fadeInDuration: 0f, isTransient: true);
            }
            else
            {
                PlayAtPosition(selected, player.Position, isTransient: true);
            }
        }

        #endregion

        #region Personal Player Soundscapes

        /// <summary>
        /// Controls personal low-frequency cognitive sub-drones based on environmental exposure states.
        /// Maintains continuous claustrophobic auditory cues for unlit actors.
        /// </summary>
        public bool UpdatePlayerBackgroundAmbient(Player player, bool shouldPlayDrone)
        {
            if (player == null || string.IsNullOrEmpty(player.UserId)) return false;
            string userId = player.UserId;

            if (shouldPlayDrone)
            {
                if (_activeDroneSessions.ContainsKey(userId)) return true;

                int sessionId = PlayAttached(player, AudioKey.SanityLowDrone, hearableForAll: false, lifespan: null, fadeInDuration: 2.0f, isTransient: false, loop: true);
                if (sessionId == 0) return false;

                _activeDroneSessions[userId] = sessionId;
                _pluginSessionIds.Add(sessionId);
                return true;
            }

            if (!_activeDroneSessions.TryGetValue(userId, out int activeSessionId)) return true;

            FadeOutSessionInternal(activeSessionId);
            _pluginSessionIds.Remove(activeSessionId);
            _activeDroneSessions.Remove(userId);
            return true;
        }

        /// <summary>
        /// Severs all active private hardware audio channels linked to a single client context.
        /// Mandatory safety routine during actor deaths, role swaps, or containment events.
        /// </summary>
        public void ForceStopAllPlayerAudio(Player player)
        {
            if (player == null || string.IsNullOrEmpty(player.UserId)) return;

            if (_activeDroneSessions.TryGetValue(player.UserId, out int sessionId))
            {
                FadeOutSessionInternal(sessionId);
                // TODO check if it really removes all player's audio sessions
                _activeDroneSessions.Remove(player.UserId);
                _pluginSessionIds.Remove(sessionId);
            }
        }

        /// <summary>
        /// Stops panic drones associated with a single client context.
        /// </summary>
        public void StopPlayerPanicDrone(Player player)
        {
            if (player == null || string.IsNullOrEmpty(player.UserId)) return;

            if (_activeDroneSessions.TryGetValue(player.UserId, out int sessionId))
            {
                FadeOutSessionInternal(sessionId);
                _pluginSessionIds.Remove(sessionId);
                _activeDroneSessions.Remove(player.UserId);
                Log.Debug($"[Scp575AudioManager] Successfully faded out drone session {sessionId} for {player.Nickname}");
            }
        }

        /// <summary>
        /// Clears all state machines and debouncers associated with a network token identifier.
        /// Evicts tracking nodes immediately upon network disconnection to prevent memory leaks.
        /// </summary>
        public void OnPlayerDisconnect(string userId)
        {
            if (string.IsNullOrEmpty(userId)) return;

            if (_activeDroneSessions.TryGetValue(userId, out int sessionId))
            {
                FadeOutSessionInternal(sessionId);
                _pluginSessionIds.Remove(sessionId);
                _activeDroneSessions.Remove(userId);
            }

            List<CooldownKey> keysToRemove = null;
            foreach (var key in _transientCooldowns.Keys)
            {
                if (string.Equals(key.UserId, userId, StringComparison.OrdinalIgnoreCase))
                {
                    keysToRemove ??= new List<CooldownKey>();
                    keysToRemove.Add(key);
                }
            }

            if (keysToRemove == null) return;

            for (int i = 0; i < keysToRemove.Count; i++)
            {
                _transientCooldowns.Remove(keysToRemove[i]);
            }
        }

        #endregion

        /// <summary>
        /// Forcibly releases resources and closes tracked audio sessions.
        /// Clears state tables during round updates or live assembly reloads.
        /// </summary>
        public void Clean(bool fullShutdown = false)
        {
            Timing.KillCoroutines(AudioCoroutineTag);

            if (fullShutdown)
            {
                StopAmbience();
                ClearSessionCollection(_activeDroneSessions.Values);
                _activeDroneSessions.Clear();
                ClearSessionCollection(_generatorSessionIds);
                _generatorSessionIds.Clear();
            }

            ClearSessionCollection(_pluginSessionIds);
            _pluginSessionIds.Clear();

            Log.Debug($"[Scp575AudioManager] Clean executed cleanly. (FullShutdown: {fullShutdown})");
        }

        /// <summary>
        /// Forwards manual sample offset translations down to the hardware core driver layers.
        /// </summary>
        public void SkipAudio(int sessionId, int count) => _audioEngine.SkipAudio(sessionId, count);

        #endregion

        #region Internal Resource Sanitization

        /// <summary>
        /// Resolves an asset request by drawing a non-repeating profile from the corresponding group.
        /// </summary>
        private AudioTrackProfile GetConfigOrThrow(AudioKey key)
        {
            if (!_audioRegistry.TryGetValue(key, out var group))
                throw new ArgumentException($"Audio key '{key}' is completely missing from configuration profiles.", nameof(key));

            return group.GetNextProfile();
        }

        /// <summary>
        /// Enforces a global cooldown lock over entity vocals to ensure unexpected scare dynamics are preserved.
        /// </summary>
        private bool IsScreamAsset(AudioKey key)
        {
            return key is AudioKey.ScreamStandard or AudioKey.ScreamAngry or AudioKey.ScreamHurt or AudioKey.ScreamDying;
        }

        /// <summary>
        /// Evaluates global timing ticks to throttle scream frequencies.
        /// Shields players from acoustic clumping anomalies during high-activity scenarios.
        /// </summary>
        private bool TryPassGlobalScreamCooldown()
        {
            double secondsSinceLastScream = Timing.LocalTime - _lastGlobalScreamTimeTicks;
            if (secondsSinceLastScream < _plugin.Config.AudioConfig.GlobalScreamCooldown) return false;
            _lastGlobalScreamTimeTicks = Timing.LocalTime;
            return true;
        }

        /// <summary>
        /// Sets up filtering delegates to route public spatial audio or isolate private headspaces.
        /// </summary>
        private Func<Player, bool> CreatePlayerFilter(Player target, bool hearableForAll, Vector3 playPosition, float maxDistance)
        {
            if (!hearableForAll)
                return p => p != null && p.UserId == target.UserId;

            return p => p != null && p.IsReady && !p.IsHost
                        && Vector3.Distance(p.Position, playPosition) <= maxDistance;
        }

        /// <summary>
        /// Maps runtime session indices to specific systemic resource pools based on priority metadata.
        /// </summary>
        private void RegisterSessionLifetime(int sessionId, AudioKey key, bool loop, string userId)
        {
            if (key == AudioKey.Ambience)
            {
                _ambienceAudioSessionId = sessionId;
                return;
            }

            if (key == AudioKey.GeneratorHumDefense)
            {
                _generatorSessionIds.Add(sessionId);
            }
            else if (loop && !string.IsNullOrEmpty(userId))
            {
                _activeDroneSessions[userId] = sessionId;
            }
            else
            {
                _pluginSessionIds.Add(sessionId);
            }
        }

        /// <summary>
        /// Iterates over a collection of active channel tokens to perform hardware-level execution tear-downs.
        /// </summary>
        private void ClearSessionCollection(IEnumerable<int> sessionIds)
        {
            foreach (int id in sessionIds)
            {
                if (id != 0) FadeOutSessionInternal(id);
            }
        }

        /// <summary>
        /// Dispatches fade commands to smooth out sudden hardware playback termination.
        /// Intercepts and suppresses hardware-level driver exceptions gracefully.
        /// </summary>
        private void FadeOutSessionInternal(int sessionId)
        {
            try
            {
                _audioEngine.FadeOutAudio(sessionId, _plugin.Config.AudioConfig.DefaultFadeDuration);
            }
            catch (Exception ex)
            {
                Log.Debug($"[Scp575AudioManager] Suppressed structural audio engine exception during session fadeout {sessionId}: {ex.Message}");
            }
        }

        /// <summary>
        /// Enforces short atomic lockout blocks to secure networks against exploit packet flooding.
        /// Throttles processing overhead from rapid physical client inputs.
        /// </summary>
        private bool TryAcquireTransientLock(string userId, AudioKey key)
        {
            if (string.IsNullOrEmpty(userId)) return true;

            CooldownKey lookupKey = new CooldownKey(userId, key);
            double currentTime = Timing.LocalTime;

            if (_transientCooldowns.TryGetValue(lookupKey, out double nextAllowedTime) && currentTime < nextAllowedTime)
                return false;

            _transientCooldowns[lookupKey] = currentTime + 0.090; // Fixed 90ms network ingestion gate
            return true;
        }

        /// <summary>
        /// Validates spatial physics vectors to shield engine nodes from float breakdown values.
        /// </summary>
        private Vector3 SanitizePosition(Vector3 position)
        {
            if (float.IsNaN(position.x) || float.IsNaN(position.y) || float.IsNaN(position.z) ||
                float.IsInfinity(position.x) || float.IsInfinity(position.y) || float.IsInfinity(position.z))
            {
                return Vector3.zero;
            }
            return position;
        }

        /// <summary>
        /// Flattens nested tracking groups to bind individual manifest resources to the audio engine.
        /// Matches embedded names with fallback formatting rules used across different compilation tools.
        /// </summary>
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

                    if (string.IsNullOrEmpty(resourceName))
                        continue;

                    _audioEngine.RegisterAudio(key, () => assembly.GetManifestResourceStream(resourceName));
                }
            }
        }

        #endregion

        #region Internal Data Structures

        /// <summary>
        /// Handles shuffle bag mechanics for grouped audio profiles.
        /// Uses non-repeating selection logic to preserve unpredictability and horror immersion.
        /// </summary>
        private sealed class AudioTrackGroup
        {
            private readonly List<AudioTrackProfile> _profiles;
            private readonly List<AudioTrackProfile> _shuffleBag = new();
            private readonly System.Random _random = new();

            public IReadOnlyList<AudioTrackProfile> Profiles => _profiles;

            public AudioTrackGroup(params AudioTrackProfile[] profiles)
            {
                if (profiles == null || profiles.Length == 0)
                    throw new ArgumentException("An audio group must contain at least one track profile.", nameof(profiles));

                _profiles = new List<AudioTrackProfile>(profiles);
            }

            /// <summary>
            /// Returns the next profile from the group, handling shuffle resets automatically once all options have been exhausted.
            /// </summary>
            public AudioTrackProfile GetNextProfile()
            {
                if (_profiles.Count == 1)
                    return _profiles[0];

                if (_shuffleBag.Count == 0)
                {
                    _shuffleBag.AddRange(_profiles);
                }

                int index = _random.Next(_shuffleBag.Count);
                AudioTrackProfile selected = _shuffleBag[index];
                _shuffleBag.RemoveAt(index);

                return selected;
            }
        }

        private readonly struct CooldownKey : IEquatable<CooldownKey>
        {
            public string UserId { get; }
            public AudioKey Key { get; }

            public CooldownKey(string userId, AudioKey key)
            {
                UserId = userId;
                Key = key;
            }

            public bool Equals(CooldownKey other) =>
                Key == other.Key && string.Equals(UserId, other.UserId, StringComparison.OrdinalIgnoreCase);

            public override bool Equals(object obj) => obj is CooldownKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((UserId != null ? StringComparer.OrdinalIgnoreCase.GetHashCode(UserId) : 0) * 397) ^ (int)Key;
                }
            }
        }

        private sealed class AudioTrackProfile
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
                Key = key; Volume = volume; MinDistance = minDistance; MaxDistance = maxDistance; IsSpatial = isSpatial; Priority = priority; DefaultLifespan = defaultLifespan;
            }
        }

        #endregion
    }
}