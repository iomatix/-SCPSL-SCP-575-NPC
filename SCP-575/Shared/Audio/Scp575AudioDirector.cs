namespace Shared.Audio
{
    using LabApi.Features.Wrappers;
    using MEC;
    using SCP_575;
    using SCP_575.Handlers;
    using SCP_575.Shared;
    using SCP_575.Shared.Audio;
    using SCP_575.Shared.Audio.Enums;
    using SCP_575.Types;
    using System;
    using System.Collections.Generic;
    using UnityEngine;

    /// <summary>
    /// Autonomous narrative engine managing survival horror tension mechanics.
    /// Tracks individual player stress profiles, accumulates tension based on sanity decay,
    /// and triggers threshold-bound auditory scares to eliminate predictable pattern recognition.
    /// </summary>
    public class Scp575AudioDirector : IDisposable
    {
        private readonly Plugin _plugin;
        private readonly Scp575AudioManager _audioManager;
        private readonly PlayerSanityHandler _sanityHandler;

        private readonly Dictionary<string, PlayerTensionProfile> _tensionCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, DateTime> _acousticSuppressionCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _directorLock = new();
        private bool _isDisposed;

        private const string DirectorCoroutineTag = CoroutineTags.AudioCoroutines;


        public Scp575AudioDirector(Plugin plugin, Scp575AudioManager audioManager, PlayerSanityHandler sanityHandler)
        {
            _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
            _audioManager = audioManager ?? throw new ArgumentNullException(nameof(audioManager));
            _sanityHandler = sanityHandler ?? throw new ArgumentNullException(nameof(sanityHandler));
        }

        /// <summary>
        /// Registers the background tracking loop to monitor and pace environmental stress factors.
        /// </summary>
        public void Initialize()
        {
            if (_isDisposed) return;

            var handle = Timing.RunCoroutine(HandleTensionPacingLoop());
            handle.Tag = DirectorCoroutineTag;
        }

        /// <summary>
        /// Main execution thread evaluating spatial threat metrics to build up individual stress peaks.
        /// </summary>
        private IEnumerator<float> HandleTensionPacingLoop()
        {
            while (true)
            {
                yield return Timing.WaitForSeconds(1.0f);

                if (!_plugin.IsEventActive)
                    continue;

                DateTime now = DateTime.Now;

                foreach (var player in Player.ReadyList)
                {
                    if (!_sanityHandler.IsValidPlayer(player))
                    {
                        lock (_directorLock)
                        {
                            _tensionCache.Remove(player.UserId);
                            _acousticSuppressionCache.Remove(player.UserId);
                            _lastCombatAudioTime.Remove(player.UserId);
                        }
                        continue;
                    }

                    // Budgeting filter: Freeze tension accumulation if a heavy physical event took priority
                    if (IsAcousticBudgetSaturated(player, now))
                        continue;

                    bool isInDarkness = _plugin.LibraryLabAPI.IsPlayerInDarkRoom(player);
                    if (isInDarkness)
                    {
                        ProcessPlayerStressTick(player, now);
                    }
                    else
                    {
                        DecayPlayerStressPassive(player);
                    }
                }
            }
        }

        /// <summary>
        /// Evicts all runtime table references assigned to a specific network token.
        /// Must be invoked during player disconnect lifecycles to guarantee zero memory leaks.
        /// </summary>
        public void OnPlayerLeft(string userId)
        {
            if (string.IsNullOrEmpty(userId)) return;

            lock (_directorLock)
            {
                _tensionCache.Remove(userId);
                _acousticSuppressionCache.Remove(userId);
                _lastCombatAudioTime.Remove(userId);
            }
        }

        /// <summary>
        /// Accumulates tension weighted inversely by the subject's remaining cognitive capacity (Sanity).
        /// Low sanity scores accelerate panic build-ups exponentially.
        /// </summary>
        private void ProcessPlayerStressTick(Player player, DateTime now)
        {
            string userId = player.UserId;
            float currentSanity = _sanityHandler.GetCurrentSanity(player);

            lock (_directorLock)
            {
                if (!_tensionCache.TryGetValue(userId, out var profile))
                {
                    profile = new PlayerTensionProfile();
                    _tensionCache[userId] = profile;
                }

                // Horror curve modifier: Scale tension accumulation speed based on cognitive collapse
                float sanityRiskFactor = 1.0f - currentSanity / 100f;
                float tensionGain = _plugin.Config.SanityConfig.DecayRateBase * (1.0f + sanityRiskFactor * 3.5f);

                profile.CurrentTension = Mathf.Clamp(profile.CurrentTension + tensionGain, 0f, 100f);

                // Threshold cross check: Evaluates if the pacing curve has reached its randomized climax
                if (profile.CurrentTension >= profile.NextTriggerThreshold)
                {
                    ExecuteAuditoryClimax(player, currentSanity);

                    // Auditory Release phase: Flatten the pacing curve and roll a new threshold for the next cycle
                    profile.ResetCurve();
                }
            }
        }

        /// <summary>
        /// Slowly drains tension when the player enters secure, illuminated zones to reward defensive playstyles.
        /// </summary>
        private void DecayPlayerStressPassive(Player player)
        {
            lock (_directorLock)
            {
                if (_tensionCache.TryGetValue(player.UserId, out var profile))
                {
                    profile.CurrentTension = Mathf.Clamp(profile.CurrentTension - 2.5f, 0f, 100f);
                }
            }
        }

        /// <summary>
        /// Selects and executes a semantic scare event matched dynamically to the player's trauma profile.
        /// </summary>
        private void ExecuteAuditoryClimax(Player player, float currentSanity)
        {
            AudioKey scareKey = AudioKey.WhispersSubtle;

            // Semantic tier mapping based on historical neurological integrity data
            if (currentSanity <= 10f) scareKey = AudioKey.WhispersShockStinger;
            else if (currentSanity <= 25f) scareKey = AudioKey.WhispersPsychotic;
            else if (currentSanity <= 55f) scareKey = AudioKey.WhispersDisturbed;

            // High-intensity breakthroughs or jumpscares utilize complex orbiting trajectories to confuse tracking vectors
            if (scareKey == AudioKey.WhispersShockStinger)
            {
                _audioManager.PlayOrbitingAudio(player, scareKey, maxRadius: 2.2f, minRadius: 0.5f, angularSpeed: 2.5f);
            }
            else
            {
                _audioManager.PlayAttached(player, scareKey, hearableForAll: false, isTransient: true);
            }
        }

        /// <summary>
        /// Implements structural sound budgeting to avoid cluttering the audio mix during combat or explosion updates.
        /// </summary>
        private bool IsAcousticBudgetSaturated(Player player, DateTime now)
        {
            lock (_directorLock)
            {
                // Evaluates if the specific client headspace is currently flagged to prioritize high-energy gameplay cues
                if (_acousticSuppressionCache.TryGetValue(player.UserId, out var expiryTime))
                {
                    return now < expiryTime;
                }
            }
            return false;
        }

        /// <summary>
        /// Explicitly locks out low-priority psychological cues for a specific player to prioritize direct feedback channels.
        /// </summary>
        public void SuppressPsychologicalAudio(Player player, float durationSeconds)
        {
            if (player == null || string.IsNullOrEmpty(player.UserId)) return;

            lock (_directorLock)
            {
                _acousticSuppressionCache[player.UserId] = DateTime.Now.AddSeconds(durationSeconds);
            }

            LibraryLabAPI.LogDebug("AudioDirector.Budget", $"Acoustic suppression registered for {player.Nickname} for {durationSeconds}s.");
        }

        /// <summary>
        /// Sweeps a fixed spatial radius and suppresses psychological audio for all vulnerable players caught inside the sector.
        /// </summary>
        public void SuppressPsychologicalAudioInRadius(Vector3 position, float radiusMeter, float durationSeconds)
        {
            DateTime expiry = DateTime.Now.AddSeconds(durationSeconds);

            lock (_directorLock)
            {
                foreach (var player in Player.ReadyList)
                {
                    if (player == null || !player.IsReady || player.IsHost || !player.IsAlive)
                        continue;

                    if (Vector3.Distance(player.Position, position) <= radiusMeter)
                    {
                        _acousticSuppressionCache[player.UserId] = expiry;
                    }
                }
            }

            LibraryLabAPI.LogDebug("AudioDirector.Budget", $"Spatial acoustic suppression applied inside {radiusMeter}m radius for {durationSeconds}s.");
        }

        /// <summary>
        /// Processes the narrative and psychological consequences of an environmental explosion.
        /// Orchestrates the acoustic mix by balancing heavy kinetic impacts with state-dependent entity feedback.
        /// </summary>
        public void ProcessExplosionImpact(Vector3 position, ScpProjectileImpactType.ProjectileImpactType impactType, bool isBlackoutActive)
        {
            // Step 1: Secure acoustic headroom for the structural blast
            SuppressPsychologicalAudioInRadius(position, radiusMeter: 28f, durationSeconds: 5.0f);

            // Step 2: Deliver the immutable physical baseline impact
            _audioManager.PlayAtPosition(AudioKey.AnomalousImpact, position);

            // Step 3: Evaluate environmental and psychological variables to deploy secondary cues
            switch (impactType)
            {
                case ScpProjectileImpactType.ProjectileImpactType.Helpful:
                    // Empowered entity vortex behavior
                    _audioManager.PlayOrbitingAudio(position, AudioKey.ScreamAngry, maxRadius: 9.0f, minRadius: 0.5f, angularSpeed: 3.8f, approachSpeed: 2.5f);

                    // The director channels a heavier multi-vocal intrusion due to the spatial distortion
                    _audioManager.PlayGlobal(AudioKey.WhispersDisturbed);
                    break;

                case ScpProjectileImpactType.ProjectileImpactType.Dangerous:
                    // Entity trauma feedback resolved dynamically through the internal shuffle deck
                    _audioManager.PlayOrbitingAudio(position, AudioKey.ScreamStandard, maxRadius: 12.0f, minRadius: 1.0f, angularSpeed: 1.2f, approachSpeed: 1.4f);
                    break;

                default:
                    // Ambient unlit fallback handled via Tier 1 subtle paranoia triggers
                    _audioManager.PlayAtPosition(AudioKey.WhispersSubtle, position);
                    break;
            }
        }

        /// <summary>
        /// Governs acoustic consequences triggered by facility power grid substation modifications.
        /// Manages defensive entity responses, permanent containment scream sequences, and structural background hum loops.
        /// </summary>
        public void ProcessGeneratorActivation(Vector3 position, bool allGeneratorsEngaged, bool retaliationConfigured)
        {
            // Step 1: Establish the persistent mechanical protection hum loop at the substation node
            _audioManager.PlayAtPosition(AudioKey.GeneratorHumDefense, position, loop: true);

            // Step 2: Evaluate global containment states to determine entity reaction intensity
            if (allGeneratorsEngaged)
            {
                // Catastrophic entity breakdown frequency fired when permanent containment gates are satisfied
                _audioManager.PlayAtPosition(AudioKey.ScreamDying, position);
                return;
            }

            if (retaliationConfigured)
            {
                // Violent defensive reaction tracking around the disturbed substation coordinates
                // Variables tuned to spin tightly and collapse rapidly into the generator core
                _audioManager.PlayOrbitingAudio(
                    staticPosition: position,
                    audioKey: AudioKey.ScreamAngry,
                    maxRadius: 7.5f,
                    minRadius: 1.2f,
                    angularSpeed: 2.4f,
                    approachSpeed: 2.8f
                );
            }
            else
            {
                // Standard responsive warning vocalization drawn safely from the automated non-repeating shuffle bag
                _audioManager.PlayAtPosition(AudioKey.ScreamStandard, position);
            }
        }

        /// <summary>
        /// Evaluates and injects psychological paranoia audio layers when an actor's mobile light source 
        /// experiences anomalous voltage fluctuations. Respects global acoustic budgeting constraints.
        /// </summary>
        public void ProcessLightsourceFlicker(Player player)
        {
            if (player == null || !player.IsReady) return;

            // Bypasses horror injection entirely if the client's mix headroom is occupied by high-priority explosions
            if (IsAcousticBudgetSaturated(player, DateTime.Now))
                return;

            // Tier 1: Localized entity breathing loop collapsing tight over the player's neck
            if (UnityEngine.Random.value <= 0.25f)
            {
                _audioManager.PlayOrbitingAudio(player, AudioKey.MonsterBreathLocal, isolated: true,
                    maxRadius: 2.2f,
                    minRadius: 0.4f,
                    angularSpeed: 2.8f,
                    approachSpeed: 1.6f);
            }

            // Tier 2: Frantic shadow clicks tracking rapidly across room boundaries
            if (UnityEngine.Random.value <= 0.15f)
            {
                _audioManager.PlayOrbitingAudio(player, AudioKey.ShadowClicking, isolated: true,
                    maxRadius: 3.8f,
                    minRadius: 0.7f,
                    angularSpeed: 4.5f,
                    approachSpeed: 2.2f);
            }
        }

        // Add this private dictionary field near your other caches inside Scp575AudioDirector:
        private readonly Dictionary<string, DateTime> _lastCombatAudioTime = new(StringComparer.OrdinalIgnoreCase);

        // Add this public method to Scp575AudioDirector:
        /// <summary>
        /// Sequences anomalous combat audio stingers during trauma transactions.
        /// Enforces temporal spacing locks per client identity node to prevent voice channel accumulation artifacts.
        /// </summary>
        public void ProcessAnomalousCombatStinger(Player player, bool isVulnerable)
        {
            if (player == null || string.IsNullOrEmpty(player.UserId)) return;
            string userId = player.UserId;

            lock (_directorLock)
            {
                if (_lastCombatAudioTime.TryGetValue(userId, out var lastCombatAudio) && (DateTime.Now - lastCombatAudio).TotalSeconds < 1.6)
                {
                    return;
                }
                _lastCombatAudioTime[userId] = DateTime.Now;
            }

            // Direct the mix output dynamically based on structural vulnerability states
            if (isVulnerable)
            {
                _audioManager.PlayAggressiveAudio(player);
            }
            else
            {
                _audioManager.PlayDefensiveAudio(player);
            }
        }

        /// <summary>
        /// Coordinates the dark narrative acoustics triggered during post-mortem corpse modifications.
        /// Layers heavy visceral body consumption textures with erratic, spiraling shadow signatures.
        /// </summary>
        public void ProcessRagdollConsumption(Vector3 position)
        {
            // Visceral organic feeding textures anchored directly to the localized physical remains
            _audioManager.PlayAtPosition(AudioKey.ShadowConsumingBody, position);

            // Chaotic clicking sequences wrapping around the corpse as if shadows are closing in.
            // Bounding floor threshold raised to 0.45m to eliminate native Unity panning phase pops near the listener plane.
            _audioManager.PlayOrbitingAudio(
                staticPosition: position,
                audioKey: AudioKey.ShadowClicking,
                maxRadius: 2.5f,
                minRadius: 0.45f,
                angularSpeed: 2.15f,
                approachSpeed: 3.25f,
                heightOffset: 0.15f
            );
        }

        public void Clean()
        {
            Timing.KillCoroutines(DirectorCoroutineTag);
            lock (_directorLock)
            {
                _lastCombatAudioTime.Clear();
                _tensionCache.Clear();
                _acousticSuppressionCache.Clear();
            }
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            Clean();
            _isDisposed = true;
        }

        #region Internal Data Structures

        /// <summary>
        /// Runtime container tracking individual threat pacing coordinates.
        /// </summary>
        private sealed class PlayerTensionProfile
        {
            private static readonly System.Random _random = new();

            public float CurrentTension { get; set; }
            public float NextTriggerThreshold { get; private set; }

            public PlayerTensionProfile()
            {
                ResetCurve();
            }

            /// <summary>
            /// Flattens stress curves and randomizes the next threshold index to guarantee unpredictable pacing.
            /// </summary>
            public void ResetCurve()
            {
                CurrentTension = 0f;
                // Distributes scares dynamically between 45% and 85% of maximum accumulated stress
                NextTriggerThreshold = (float)(_random.NextDouble() * (85.0 - 45.0) + 45.0);
            }
        }

        #endregion
    }
}