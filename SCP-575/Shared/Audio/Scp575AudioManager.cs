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
    /// Acts as the central orchestrator for the SCP-575 audio subsystem.
    /// Manages session lifecycles, global ambient state transitions, and psychological auditory feedback 
    /// while protecting the underlying audio engine from resource exhaustion.
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

        private readonly Dictionary<AudioKey, AudioTrackProfile> _audioRegistry = new()
        {
            { AudioKey.Scream_1, new("scp575.scream_1", 0.95f, 65f, 150f, true, AudioPriority.High, 9f) },
            { AudioKey.Scream_2, new("scp575.scream_2", 0.95f, 65f, 150f, true, AudioPriority.High, 9f) },
            { AudioKey.Scream_3, new("scp575.scream_3", 0.95f, 65f, 150f, true, AudioPriority.High, 9f) },
            { AudioKey.ScreamAngry, new("scp575.scream_angry", 0.95f, 175f, 450f, true, AudioPriority.High, 9f) },
            { AudioKey.ScreamHurt, new("scp575.scream_hurt", 0.95f, 125f, 345f, true, AudioPriority.High, 7f) },
            { AudioKey.ScreamDying, new("scp575.scream_dying", 0.95f, 255f, 480f, true, AudioPriority.High, 20f) },
            { AudioKey.MonsterRoarGlobal, new("scp575.monster_roar_global", 0.85f, 45f, 999.99f, false, AudioPriority.High, 40f) },
            { AudioKey.Whispers_1, new("scp575.whispers_1", 0.55f, 5f, 35f, true, AudioPriority.Medium, 11f) },
            { AudioKey.Whispers_2, new("scp575.whispers_2", 0.55f, 7f, 45f, true, AudioPriority.Medium, 19f) },
            { AudioKey.Whispers_3, new("scp575.whispers_3", 0.55f, 9f, 52f, true, AudioPriority.Medium, 14f) },
            { AudioKey.WhispersBang, new("scp575.whispers_bang", 0.65f, 12f, 65f, true, AudioPriority.High, 20f) },
            { AudioKey.WhispersMixed, new("scp575.whispers_mixed", 0.65f, 10f, 55f, true, AudioPriority.Medium, 25f) },
            { AudioKey.MonsterBreathLocal, new("scp575.monster_breath_local", 0.75f, 5f, 24f, true, AudioPriority.High, 11f) },
            { AudioKey.ShadowClicking, new("scp575.shadow_clicking", 0.55f, 4.75f, 33f, true, AudioPriority.High, 9f) },
            { AudioKey.ShadowStrike, new("scp575.shadow_strike", 0.8f, 5.5f, 30f, true, AudioPriority.High, 5f) },
            { AudioKey.ShadowConsumingBody, new("scp575.shadow_consuming_body", 0.95f, 7.5f, 45f, true, AudioPriority.High, 5f) },
            { AudioKey.AnomalousImpact, new("scp575.anomalous_impact", 0.9f, 3.5f, 25f, true, AudioPriority.High, 5f) },
            { AudioKey.GeneratorHumDefense, new("scp575.generator_hum_defense", 0.7f, 6.5f, 45f, true, AudioPriority.Medium, 0f) },
            { AudioKey.LightShortCircuit, new("scp575.light_short_circuit", 0.85f, 2.5f, 18f, true, AudioPriority.Max, 1.5f) },
            { AudioKey.Ambience, new("scp575.ambience", 0.6f, 0f, 999.99f, false, AudioPriority.Low, 0f) },
            { AudioKey.SanityLowDrone, new("scp575.sanity_low_drone", 0.6f, 200.0f, 999.99f, false, AudioPriority.Medium, 0f) },
            { AudioKey.BlackoutImpactGlobal, new("scp575.blackout_impact_global", 0.95f, 0f, 999.99f, false, AudioPriority.High, 13f) },
        };

        public Scp575AudioManager(Plugin plugin)
        {
            _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin), "Plugin context wrapper must be provided to initialize audio config structures.");
            _audioEngine = DefaultAudioManager.Instance;
            RegisterAudioResources();
        }

        #region Public Execution Interface (Intentional API)

        /// <summary>
        /// Broadcasts non-spatialized audio cues globally to all connected clients.
        /// Primarily used for event-wide atmospheric impact indicators or map-wide monster announcements.
        /// </summary>
        /// <param name="key">The configuration key corresponding to the target sound resource.</param>
        /// <param name="lifespan">The maximum time in seconds before the track auto-destructs. If null, defaults to the profile's configuration.</param>
        /// <param name="queue">If true, chains this sound to execute sequentially behind active non-spatial channels. If false, triggers immediate simultaneous playback.</param>
        /// <param name="fadeInDuration">The duration in seconds to transition the track's volume from 0 to full config limits.</param>
        /// <param name="loop">If true, forces the clip to repeat indefinitely until a structural cleanup phase fires. If false, acts as a one-shot effect.</param>
        /// <returns>The distinct driver channel tracker session ID, or 0 if choked by active subsystem cooldown gates.</returns>
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
        /// Emits a localized static 3D audio instance anchored to fixed environment coordinates.
        /// Seamlessly routes between dual-channel smart tracking and persistent environmental loops (like generators).
        /// </summary>
        /// <param name="key">The configuration key corresponding to the target sound resource.</param>
        /// <param name="position">The exact Vector3 target world coordinates where the audio emitter will materialize.</param>
        /// <param name="lifespan">The duration in seconds before the audio engine kills the session. If null, pulls from default asset properties.</param>
        /// <param name="isTransient">If true, validates player debounce constraints to prevent acoustic overlap artifacts. If false, bypasses the rate-limiter check.</param>
        /// <param name="sourcePlayer">The structural author entity behind the sound emission context. If null, treated as an unauthored world-grid artifact.</param>
        /// <param name="loop">If true, or if the asset matches <see cref="AudioKey.GeneratorHumDefense"/>, handles playback as an ongoing grid presence loop registered inside protected caches. If false, executes a standard smart dual-channel spatial mix.</param>
        /// <returns>The primary driving world session ID assigned by the underlying audio interface.</returns>
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
        /// Binds acoustic feedback to a specific player context. 
        /// Configurable to either isolate the sound to the target's headspace or broadcast it outward to nearby actors.
        /// </summary>
        /// <param name="target">The actor entity that the audio emitter source will track.</param>
        /// <param name="key">The configuration key corresponding to the target sound resource.</param>
        /// <param name="hearableForAll">If true, projects the sound into public 3D environments audible to close-by entities. If false, locks the channels exclusively into the target's private headspace (hallucination mode).</param>
        /// <param name="lifespan">The active duration constraint before automatic driver reclamation. If null, applies default profile configuration.</param>
        /// <param name="fadeInDuration">Transition ramping speed in seconds to smoothly introduce continuous noise streams.</param>
        /// <param name="isTransient">If true, subjects execution paths to strict zero-allocation cooldown structures to intercept exploit flooding. If false, bypasses input rate-limiting entirely.</param>
        /// <param name="loop">If true, registers the running audio channel inside persistent tracking lists for targeted loop disruption. If false, maps session lifespan as a temporary sound effect instance.</param>
        /// <returns>The active runtime channel key token, or 0 if blocked by transient rate filters.</returns>
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
        /// Deploys the main environmental loop with integrated darkness room validation.
        /// Establishes the core match horror ambiance exclusively for vulnerable players.
        /// </summary>
        /// <param name="loop">If true, locks the background sound channel to automatically cycle indefinitely. If false, plays the texture track exactly once.</param>
        /// <param name="fadeInDuration">Ramping speed in seconds to avoid jarring auditory anomalies during state synchronization initialization phases.</param>
        /// <param name="queue">If true, safely registers the audio in line behind running global audio requests. If false, interrupts/overlays immediate playback.</param>
        /// <returns>The assigned environmental tracking code, or 0 if the registration fails at the hardware integration layer.</returns>
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
        /// Gracefully dampens and releases the background ambient environment lock.
        /// Used during phase shifts or light restoration sequences to lift tension.
        /// </summary>
        public void StopAmbience()
        {
            if (_ambienceAudioSessionId == 0) return;
            FadeOutSessionInternal(_ambienceAudioSessionId);
            _ambienceAudioSessionId = 0;
        }

        #region Kinetic Movement Translators

        /// <summary>
        /// Commands a dynamic acoustic vector to orbit around a moving player target.
        /// Simulates complex localized hallucinations or proximity-based hunter tracking.
        /// </summary>
        /// <param name="player">The moving target actor that forms the epicenter of the orbital mathematics loop.</param>
        /// <param name="audioKey">The configuration key corresponding to the target sound resource.</param>
        /// <param name="lifespan">The active duration constraint. If null, defaults to asset config parameters.</param>
        /// <param name="maxRadius">The maximum distance boundary limit in meters that the floating sound vector can drift out to.</param>
        /// <param name="minRadius">The internal proximity boundary fallback limit in meters preventing sound overlapping inside the core model position.</param>
        /// <param name="angularSpeed">The rotation tracking speed factor governing vector translation updates around the target object.</param>
        /// <param name="approachSpeed">The vector interpolation velocity coefficient driving spatial collapse inward toward the subject.</param>
        /// <param name="isolated">If true, limits the resulting running audio network visibility strictly to the target's eyes. If false, allows public proximity tracing.</param>
        public void PlayOrbitingAudio(Player player, AudioKey audioKey, float? lifespan = null, float maxRadius = 3.2f, float minRadius = 0.6f, float angularSpeed = 1.1f, float approachSpeed = 1.5f, bool isolated = false)
        {
            PlayOrbitingAudioCore(() => player.Position, () => player != null && player.IsAlive, isolated ? player : null, audioKey, lifespan, maxRadius, minRadius, angularSpeed, approachSpeed);
        }

        /// <summary>
        /// Commands a dynamic acoustic vector to orbit around static landscape coordinates.
        /// Used for localized environmental anomalies or rotating structural audio warnings.
        /// </summary>
        /// <param name="staticPosition">The absolute static world coordinates serving as the focal rotation point.</param>
        /// <param name="audioKey">The configuration key corresponding to the target sound resource.</param>
        /// <param name="lifespan">The active duration constraint. If null, defaults to asset config parameters.</param>
        /// <param name="maxRadius">The maximum distance boundary limit in meters that the floating sound vector can drift out to.</param>
        /// <param name="minRadius">The internal proximity boundary fallback limit in meters preventing sound overlapping inside the core model position.</param>
        /// <param name="angularSpeed">The rotation tracking speed factor governing vector translation updates around the target object.</param>
        /// <param name="approachSpeed">The vector interpolation velocity coefficient driving spatial collapse inward toward the subject.</param>
        /// <param name="heightOffset">The absolute height modifications added to the core world position plane.</param>
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
        /// Anchors a continuous audio channel to trace an actor's player transform accurately.
        /// Simulates sounds originating directly from the player's body (e.g., breathing, attached shadows).
        /// </summary>
        /// <param name="player">The target actor framework model to attach structural tracking layers onto.</param>
        /// <param name="audioKey">The configuration key corresponding to the target sound resource.</param>
        /// <param name="lifespan">The active duration constraint. If null, falls back to structural configuration defaults.</param>
        /// <param name="hearableForAllPlayers">If true, registers no filtering layers, making the translation audible to any nearby observer. If false, masks the resulting tracking network strictly to the host user context.</param>
        /// <param name="customOffset">A manual spatial translation offset constraint. If null, calculates a default chest-level height adjustment vector.</param>
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
        /// Selects and executes a random acoustic effect to break predictable audio patterns.
        /// Breaks standard player telemetry expectations during long survival phases.
        /// </summary>
        /// <param name="player">The baseline target client chosen for the acoustic randomized feedback injection mapping.</param>
        /// <param name="options">The array of possible sound variations. If empty or null, loads a standardized array of psychological sound keys automatically.</param>
        public void PlayRandomAudioEffect(Player player, params AudioKey[] options)
        {
            var pool = (options == null || options.Length == 0)
                ? new[] { AudioKey.WhispersMixed, AudioKey.Whispers_1, AudioKey.Whispers_2, AudioKey.ShadowClicking }
                : options;

            PlayAttached(player, pool[UnityEngine.Random.Range(0, pool.Length)], hearableForAll: false);
        }

        /// <summary>
        /// Triggers high-intensity auditory shocks designed to simulate an active, aggressive entity hunt.
        /// Artificially forces extreme stress indicators directly in the victim's headspace.
        /// </summary>
        /// <param name="player">The victim user target targeted for hunting acoustic effects.</param>
        public void PlayAggressiveAudio(Player player)
        {
            EvaluateAndPlayProbability(player, new[] { AudioKey.AnomalousImpact }, 0.15f, orbit: false, isolated: true);
            EvaluateAndPlayProbability(player, new[] { AudioKey.ShadowStrike }, 0.10f, orbit: false, isolated: true);
            EvaluateAndPlayProbability(player, new[] { AudioKey.Scream_1, AudioKey.Scream_2, AudioKey.Scream_3, AudioKey.ScreamAngry }, 0.10f, orbit: true);
            EvaluateAndPlayProbability(player, new[] { AudioKey.ShadowClicking }, 0.05f, orbit: false, isolated: true);
        }

        /// <summary>
        /// Triggers low-frequency psychological audio signals indicating defensive entity behavior.
        /// Slows down pacing and increases tension when the entity is stalking without attacking.
        /// </summary>
        /// <param name="player">The victim player framework context targeted for psychological pacing modifiers.</param>
        public void PlayDefensiveAudio(Player player)
        {
            EvaluateAndPlayProbability(player, new[] { AudioKey.AnomalousImpact }, 0.10f, orbit: false, isolated: true);
            EvaluateAndPlayProbability(player, new[] { AudioKey.ShadowStrike }, 0.05f, orbit: false, isolated: true);
            EvaluateAndPlayProbability(player, new[] { AudioKey.Whispers_1, AudioKey.Whispers_2, AudioKey.WhispersBang }, 0.10f, orbit: true);
            EvaluateAndPlayProbability(player, new[] { AudioKey.ShadowClicking }, 0.10f, orbit: false, isolated: true);
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
        /// Continuous sanity tracker layer management. Toggles or updates persistent 
        /// psychological background feedback based on the player's exposure to darkness.
        /// </summary>
        /// <param name="player">The individual user target undergoing environment visibility mapping evaluation checks.</param>
        /// <param name="shouldPlayDrone">If true, creates a long-fading sanity loop channel if missing. If false, gracefully winds down and tears down existing matching channel keys.</param>
        /// <returns>True if the intended background state successfully aligns with the player registration structures; otherwise, false.</returns>
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
        /// Instantly breaks off any active personal audio loops assigned to a player.
        /// Required during player death, class changes, or team containment re-synchronization.
        /// </summary>
        /// <param name="player">The targeted actor whose active loop caches must be cleared.</param>
        public void ForceStopAllPlayerAudio(Player player)
        {
            if (player == null || string.IsNullOrEmpty(player.UserId)) return;

            if (_activeDroneSessions.TryGetValue(player.UserId, out int sessionId))
            {
                FadeOutSessionInternal(sessionId);
                _pluginSessionIds.Remove(sessionId);
                _activeDroneSessions.Remove(player.UserId);
            }
        }

        /// <summary>
        /// Evicts all cached state, tracking timers, and active loops allocated to a disconnected player.
        /// MUST be hooks-bound to network leave lifecycles to prevent cumulative memory leaks.
        /// </summary>
        /// <param name="userId">The distinct unique string identifier tracking the disconnected player client.</param>
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
        /// Recycles resources and terminates all active channel tracking sessions.
        /// Ensures absolute clean slate states during game event updates, round ends, or full plugin hot-swaps.
        /// </summary>
        /// <param name="fullShutdown">If true, executes hard teardowns that destroy persistent global environmental elements and generator channels. If false, preserves infrastructure-level loops while purging individual player effects.</param>
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
        /// Forwards internal index shifting down to the sound driver interface.
        /// </summary>
        /// <param name="sessionId">The distinct driver channel index reference targeting an active clip stream.</param>
        /// <param name="count">The integer sample displacement step index size to offset.</param>
        public void SkipAudio(int sessionId, int count) => _audioEngine.SkipAudio(sessionId, count);

        #endregion

        #region Internal Resource Sanitization (DRY Infrastructure Helpers)

        private AudioTrackProfile GetConfigOrThrow(AudioKey key)
        {
            if (!_audioRegistry.TryGetValue(key, out var config))
                throw new ArgumentException($"Audio key '{key}' is completely missing from configuration profiles.", nameof(key));
            return config;
        }

        private bool IsScreamAsset(AudioKey key)
        {
            return key is AudioKey.Scream_1 or AudioKey.Scream_2 or AudioKey.Scream_3 or AudioKey.ScreamAngry or AudioKey.ScreamDying;
        }

        private bool TryPassGlobalScreamCooldown()
        {
            double secondsSinceLastScream = Timing.LocalTime - _lastGlobalScreamTimeTicks;
            if (secondsSinceLastScream < _plugin.Config.AudioConfig.GlobalScreamCooldown) return false;
            _lastGlobalScreamTimeTicks = Timing.LocalTime;
            return true;
        }

        private Func<Player, bool> CreatePlayerFilter(Player target, bool hearableForAll, Vector3 playPosition, float maxDistance)
        {
            if (!hearableForAll)
                return p => p != null && p.UserId == target.UserId;

            return p => p != null && p.IsReady && !p.IsHost
                        && Vector3.Distance(p.Position, playPosition) <= maxDistance;
        }

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

        private void ClearSessionCollection(IEnumerable<int> sessionIds)
        {
            foreach (int id in sessionIds)
            {
                if (id != 0) FadeOutSessionInternal(id);
            }
        }

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

        private bool TryAcquireTransientLock(string userId, AudioKey key)
        {
            if (string.IsNullOrEmpty(userId)) return true;

            CooldownKey lookupKey = new CooldownKey(userId, key);
            double currentTime = Timing.LocalTime;

            if (_transientCooldowns.TryGetValue(lookupKey, out double nextAllowedTime) && currentTime < nextAllowedTime)
                return false;

            _transientCooldowns[lookupKey] = currentTime + 0.090;
            return true;
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

        private void RegisterAudioResources()
        {
            var assembly = Assembly.GetExecutingAssembly();
            string[] allResourceNames = assembly.GetManifestResourceNames();

            foreach (var pair in _audioRegistry)
            {
                string key = pair.Value.Key;
                string resourceName = allResourceNames.FirstOrDefault(r =>
                    r.EndsWith($"{key}.wav", StringComparison.OrdinalIgnoreCase) ||
                    r.EndsWith($"{key.Replace(".", "_")}.wav", StringComparison.OrdinalIgnoreCase));

                if (string.IsNullOrEmpty(resourceName)) continue;

                _audioEngine.RegisterAudio(key, () => assembly.GetManifestResourceStream(resourceName));
            }
        }

        #endregion

        #region Internal Data Structures

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