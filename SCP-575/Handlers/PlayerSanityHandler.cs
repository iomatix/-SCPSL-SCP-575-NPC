namespace SCP_575.Handlers
{
    using LabApi.Events.Arguments.PlayerEvents;
    using LabApi.Events.Arguments.ServerEvents;
    using LabApi.Events.CustomHandlers;
    using LabApi.Features.Wrappers;
    using MEC;
    using SCP_575.ConfigObjects;
    using SCP_575.Shared;
    using SCP_575.Shared.Audio.Enums;
    using System;
    using System.Collections.Generic;
    using System.Data.Common;
    using System.Linq;
    using UnityEngine;

    /// <summary>
    /// Manages the internal psychological state of human actors, governing neurological decay vectors, 
    /// systemic affliction mapping, and panic-induced defensive feedback loops under absolute darkness.
    /// </summary>
    public class PlayerSanityHandler : CustomEventsHandler, IDisposable
    {
        public static string IdentifierName => nameof(Scp575DamageSystem);

        private readonly Plugin _plugin;
        private readonly LibraryLabAPI _libraryLabAPI;
        private readonly PlayerSanityConfig _sanityConfig;

        private readonly Dictionary<string, float> _sanityCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, DateTime> _lastHintTime = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<PlayerSanityStageConfig> _orderedStages;
        private readonly Dictionary<string, DateTime> _painkillerProtectionExpiry = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, DateTime> _painkillerSanityBoostExpiry = new(StringComparer.OrdinalIgnoreCase);
        // Tracks combat stinger feedback intervals to prevent machine-gun stacking artifacts
        private readonly Dictionary<string, DateTime> _lastCombatAudioTime = new(StringComparer.OrdinalIgnoreCase);
        // Tracks the next allowed timestamp for a localized whisper hallucination per player
        private readonly Dictionary<string, DateTime> _nextAllowedWhisperTime = new(StringComparer.OrdinalIgnoreCase);
        // Global timestamp to prevent multiple players from triggering intense jumpscares simultaneously
        private static DateTime _globalNextAllowedWhisperTime = DateTime.MinValue;

        private readonly float _hintCooldown;
        private bool _isDisposed;
        private readonly object _cacheLock = new();

        private const string SanityCoroutineTag = CoroutineTags.SanityHandler;

        /// <summary>
        /// Exposes a read-only view of runtime psychological metrics for external integration tracking.
        /// </summary>
        public IReadOnlyDictionary<string, float> SanityCache => _sanityCache;

        /// <summary>
        /// Contains a flag indicating whether the player has an active drone (true) or not (false).
        /// </summary>
        private readonly Dictionary<string, bool> _activeAmbientState = new(StringComparer.OrdinalIgnoreCase);

        public PlayerSanityHandler(Plugin plugin)
        {
            _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin), "Plugin instance cannot be null.");
            _libraryLabAPI = _plugin.LibraryLabAPI;
            _sanityConfig = plugin.Config?.SanityConfig ?? throw new InvalidOperationException("SanityConfig is not initialized.");
            _hintCooldown = _sanityConfig.DecayRateBase * 20f;

            if (_sanityConfig.SanityStages == null || !_sanityConfig.SanityStages.Any())
                throw new InvalidOperationException("SanityStages is null or empty.");

            var stages = _sanityConfig.SanityStages.OrderBy(s => s.MinThreshold).ToList();

            if (stages[0].MinThreshold > 0 || stages[stages.Count - 1].MaxThreshold < 100)
                throw new InvalidOperationException("SanityStages do not cover the full range (0–100).");

            for (int i = 0; i < stages.Count - 1; i++)
            {
                if (stages[i].MaxThreshold != stages[i + 1].MinThreshold)
                    throw new InvalidOperationException("SanityStages have gaps or overlaps.");
            }

            // Descending sort optimizes linear scanning loops by assessing critical 
            // breakdown states before lower-tier cognitive baselines.
            _orderedStages = stages.OrderByDescending(s => s.MaxThreshold).ToList();
            LibraryLabAPI.LogDebug("PlayerSanityHandler", $"Loaded {stages.Count} sanity stages.");
        }

        #region Lifecycle Management

        /// <summary>
        /// Provisions state machinery and prepares core collections for operational initialization.
        /// </summary>
        public void Initialize()
        {
            if (_isDisposed) return;
            var handle = Timing.RunCoroutine(HandleSanityDecay());
            handle.Tag = SanityCoroutineTag;
            LibraryLabAPI.LogInfo("PlayerSanityHandler", "Sanity decay processing loop successfully started.");
        }

        /// <summary>
        /// Dissolves all internal execution tokens and purges cached tracking data to preserve allocation overhead.
        /// </summary>
        public void Clean()
        {
            Timing.KillCoroutines(SanityCoroutineTag);
            lock (_cacheLock)
            {
                _sanityCache.Clear();
                _lastHintTime.Clear();
                _activeAmbientState.Clear();
                _nextAllowedWhisperTime.Clear();
                _lastCombatAudioTime.Clear();
                _painkillerProtectionExpiry.Clear();
                _painkillerSanityBoostExpiry.Clear();
            }
        }

        /// <summary>
        /// Explicitly terminates active execution routines and flags internal collections for garbage collection.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed) return;
            Clean();
            _isDisposed = true;
            LibraryLabAPI.LogInfo("PlayerSanityHandler", "Disposed PlayerSanityHandler and cleaned up resources.");
        }

        public override void OnServerRoundEnded(RoundEndedEventArgs ev) => Clean();
        public override void OnServerWaitingForPlayers() => Clean();

        #endregion

        #region Event Handlers

        public override void OnPlayerSpawned(PlayerSpawnedEventArgs ev) => ResetPlayerSanity(ev?.Player);
        public override void OnPlayerChangedRole(PlayerChangedRoleEventArgs ev)
        {
            if (ev?.Player == null) return;

            // Reset baseline metrics
            ResetPlayerSanity(ev.Player);

            // If their new role is invalid for sanity mechanics, silence active loops immediately
            if (!IsValidPlayer(ev.Player))
            {
                SafeUpdateAmbient(ev.Player, shouldPlayDrone: false);
            }
        }

        public override void OnPlayerLeft(PlayerLeftEventArgs ev)
        {
            if (ev?.Player == null) return;
            string userId = ev.Player.UserId;

            lock (_cacheLock)
            {
                _sanityCache.Remove(userId);
                _lastHintTime.Remove(userId);
                _activeAmbientState.Remove(userId);
                _nextAllowedWhisperTime.Remove(userId);
                _lastCombatAudioTime.Remove(userId);
                _painkillerProtectionExpiry.Remove(userId);
                _painkillerSanityBoostExpiry.Remove(userId);
            }

            // Explicitly notify the audio manager to release tracking keys upon network disconnect
            _plugin.AudioManager.ForceStopAllPlayerAudio(ev.Player);
        }

        public override void OnPlayerUsedItem(PlayerUsedItemEventArgs ev)
        {
            if (!_plugin.IsEventActive || !IsValidPlayer(ev?.Player) || ev.UsableItem?.Type == null) return;

            float restoreAmount = GetItemRestoreAmount(ev.UsableItem.Type);
            if (restoreAmount <= 0f) return;

            string userId = ev.Player.UserId;

            // Check if the consumed medical item is painkillers to trigger custom mechanics
            if (ev.UsableItem.Type == ItemType.Painkillers)
            {
                lock (_cacheLock)
                {
                    // Calculate and commit expiration timestamps based on configuration values
                    _painkillerProtectionExpiry[userId] = DateTime.Now.AddSeconds(_sanityConfig.PainkillersProtectionDuration);
                    _painkillerSanityBoostExpiry[userId] = DateTime.Now.AddSeconds(_sanityConfig.PainkillersRegenDuration);
                }

                LibraryLabAPI.LogDebug("PlayerSanityHandler", $"Painkillers consumed by {ev.Player.Nickname}. Protection registered for {_sanityConfig.PainkillersProtectionDuration}s, Sanity boost registered for {_sanityConfig.PainkillersRegenDuration}s.");
            }

            float newSanity = ChangeSanityValue(ev.Player, restoreAmount);
            if (_plugin.Config.HintsConfig.IsEnabledSanityHint)
                SendSanityHint(ev.Player, _plugin.Config.HintsConfig.SanityIncreasedMedicalHint, newSanity);

            LibraryLabAPI.LogDebug("PlayerSanityHandler", $"Restored {restoreAmount} sanity to {ev.Player.Nickname}. New sanity: {newSanity}");
        }

        #endregion

        #region Sanity Management

        /// <summary>
        /// Resolves the current psychological metric for a given target, initializing a standard baseline if untracked.
        /// </summary>
        public float GetCurrentSanity(Player player)
        {
            if (!IsValidPlayer(player)) return _sanityConfig.InitialSanity;

            string userId = player.UserId;
            lock (_cacheLock)
            {
                if (!_sanityCache.TryGetValue(userId, out float sanity))
                {
                    sanity = _sanityConfig.InitialSanity;
                    _sanityCache[userId] = sanity;
                }
                return sanity;
            }
        }

        /// <summary>
        /// Resets an actor's psychological metric to a standard baseline.
        /// </summary>
        private void ResetPlayerSanity(Player player)
        {
            if (IsValidPlayer(player))
            {
                SetSanity(player, _sanityConfig.InitialSanity);
            }
        }

        /// <summary>
        /// Forces an absolute overwrite on an actor's psychological metric, bounded by standard operational limits.
        /// </summary>
        public float SetSanity(Player player, float sanity)
        {
            if (!IsValidPlayer(player)) return 0f;

            string userId = player.UserId;
            float clampedSanity = Mathf.Clamp(sanity, 0f, 100f);

            lock (_cacheLock)
            {
                _sanityCache[userId] = clampedSanity;
            }
            return clampedSanity;
        }

        /// <summary>
        /// Applies a relative variance to an actor's psychological metric, computing bounds-safe limits.
        /// </summary>
        public float ChangeSanityValue(Player player, float amount)
        {
            if (!IsValidPlayer(player)) return 0f;

            float currentSanity = GetCurrentSanity(player);
            return SetSanity(player, currentSanity + amount);
        }

        /// <summary>
        /// Maps an abstract numerical sanity score to its corresponding structural profile.
        /// </summary>
        public PlayerSanityStageConfig GetCurrentSanityStage(float sanity)
        {
            foreach (var s in _orderedStages)
            {
                if (sanity <= s.MaxThreshold && (sanity > s.MinThreshold || (sanity == 0 && s.MinThreshold == 0)))
                    return s;
            }
            return null;
        }

        /// <summary>
        /// Resolves the structural impairment profile currently governing the targeted actor.
        /// </summary>
        public PlayerSanityStageConfig GetCurrentSanityStage(Player player)
        {
            if (!IsValidPlayer(player)) return null;
            return GetCurrentSanityStage(GetCurrentSanity(player));
        }

        /// <summary>
        /// Translates cognitive decay milestones into tangible gameplay sensory impairments and physical restrictions.
        /// </summary>
        public void ApplyStageEffects(Player player, bool bypassBlackoutGate = false)
        {
            if (!IsValidPlayer(player)) return;

            if (!_plugin.Npc.Methods.IsBlackoutActive && !bypassBlackoutGate)
                return;

            // FIX: Short-circuit and suppress any negative stage debuffs if the player is currently shielded by painkillers
            if (IsProtectedByPainkillers(player)) return;

            var stage = GetCurrentSanityStage(player);
            if (stage == null) return;

            if (Helpers.IsHumanWithoutLight(player) || stage.OverrideLightSourceSanityProtection)
            {
                if (stage.Effects != null)
                {
                    foreach (var effectConfig in stage.Effects)
                    {
                        try
                        {
                            ApplyEffect(player, effectConfig.EffectType, effectConfig.Intensity, effectConfig.Duration);
                        }
                        catch (Exception ex)
                        {
                            LibraryLabAPI.LogWarn("PlayerSanityHandler", $"Failed to apply effect {effectConfig.EffectType} to {player.Nickname}: {ex.Message}");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Inflicts physical structural integrity decay on human actors caught vulnerable in active threat sectors, 
        /// accounting for atmospheric stack multipliers and defensive gear status.
        /// </summary>
        public void ApplyDamageToPlayer(Player player)
        {
            if (!IsValidPlayer(player)) return;
            if (IsProtectedByPainkillers(player)) return;

            var stage = GetCurrentSanityStage(player);
            if (stage == null) return;

            bool isVulnerable = Helpers.IsHumanWithoutLight(player) || stage.OverrideLightSourceSanityProtection;

            float culmDamage = isVulnerable
                ? stage.DamageOnStrike + (stage.AdditionalDamagePerStack * _plugin.Npc.Methods.GetCurrentBlackoutStacks)
                : stage.DamageOnStrikeWhenLightsourceActive + (stage.AdditionalDamagePerStackWhenLightsourceActive * _plugin.Npc.Methods.GetCurrentBlackoutStacks);

            if (culmDamage <= 0) return;

            float dropAmount = _plugin.Config.SanityConfig.ScpHitSanityDrop;
            if (dropAmount > 0f)
            {
                float newSanity = ChangeSanityValue(player, -dropAmount);
                LibraryLabAPI.LogDebug(IdentifierName, $"Anomalous trauma inflicted on {player.Nickname}. Sanity lowered by {dropAmount}. New sanity: {newSanity}");
            }

            // FIX: Implement a strict combat audio pacing gate. 
            // Prevents multiple heavy threat stingers from stacking and clipping on rapid damage ticks.
            string userId = player.UserId;
            if (!_lastCombatAudioTime.TryGetValue(userId, out var lastCombatAudio) || (DateTime.Now - lastCombatAudio).TotalSeconds >= 1.6)
            {
                _lastCombatAudioTime[userId] = DateTime.Now;

                if (isVulnerable)
                    _plugin.AudioManager.PlayAggressiveAudio(player);
                else
                    _plugin.AudioManager.PlayDefensiveAudio(player);
            }

            Scp575DamageSystem.DamagePlayer(player, culmDamage);
        }

        #endregion

        #region Sanity Processing Loop

        /// <summary>
        /// Main runtime engine managing both active environmental sanity decay and peaceful context-aware regeneration.
        /// </summary>
        public IEnumerator<float> HandleSanityDecay()
        {
            while (true)
            {
                yield return Timing.WaitForSeconds(1f);

                if (!_plugin.IsEventActive)
                    continue;

                DateTime now = DateTime.Now;

                foreach (var player in Player.ReadyList)
                {
                    // FIX: If a player becomes invalid (dies/spectator), explicitly strip their ambient sound loop
                    if (!IsValidPlayer(player))
                    {
                        SafeUpdateAmbient(player, shouldPlayDrone: false);
                        continue;
                    }

                    bool isInDarkness = _libraryLabAPI.IsPlayerInDarkRoom(player);

                    if (isInDarkness)
                    {
                        ProcessDecayTick(player, now);
                    }
                    else
                    {
                        ProcessRegenTick(player, now);
                    }
                }
            }
        }

        private void ProcessDecayTick(Player player, DateTime now)
        {
            float decayRate = CalculateDecayRate(player);
            float oldSanity = GetCurrentSanity(player);
            float newSanity = ChangeSanityValue(player, -decayRate);

            // 1. Continuous Background State Evaluation (Non-Spatial Head-Space Ambience)
            bool requiresLowDrone = newSanity <= 35f;
            SafeUpdateAmbient(player, requiresLowDrone);

            // 2. PROFESSIONAL AUDIO DIRECTOR: Localized Hallucination Logic (Dynamic Spacing)
            var stage = GetCurrentSanityStage(newSanity);
            if (stage != null)
            {
                string userId = player.UserId;

                // Verify if both individual and global audio director cooldown gates are open
                bool isIndividualReady = !_nextAllowedWhisperTime.TryGetValue(userId, out var individualExpiry) || now >= individualExpiry;
                bool isGlobalReady = now >= _globalNextAllowedWhisperTime;

                if (isIndividualReady && isGlobalReady)
                {
                    // Base dynamic chance: 4.5% per second, evaluated only when the cooldown window is completely open
                    if (UnityEngine.Random.value < 0.045f)
                    {
                        AudioKey? whisperToPlay = null;

                        if (newSanity <= 10f) whisperToPlay = AudioKey.WhispersBang;
                        else if (newSanity <= 25f) whisperToPlay = AudioKey.WhispersMixed;
                        else if (newSanity <= 55f) whisperToPlay = AudioKey.Whispers_2;
                        else if (newSanity <= 85f) whisperToPlay = AudioKey.Whispers_1;

                        if (whisperToPlay.HasValue)
                        {
                            _plugin.AudioManager.PlayAttached(player, whisperToPlay.Value, hearableForAll: false, isTransient: true);

                            // ADVANCED PACING: Impose a strict, randomized individual restriction (45 to 90 seconds of pure silence)
                            // This creates a standard tension-and-release curve rather than annoying cluster loops
                            float randomCooldown = UnityEngine.Random.Range(45f, 90f);
                            _nextAllowedWhisperTime[userId] = now.AddSeconds(randomCooldown);

                            // Global suppression gate: No other player on the server can trigger a whisper for the next 6 seconds
                            // This entirely eliminates overlapping text-to-speech soundscapes across concrete walls
                            _globalNextAllowedWhisperTime = now.AddSeconds(6f);

                            LibraryLabAPI.LogDebug("AudioDirector", $"Whisper triggered for {player.Nickname}. Next individual scare in {randomCooldown:F1}s. Global gate locked for 6s.");
                        }
                    }
                }
            }

            // 3. Process UI alert prompt updates
            if (_plugin.Config.HintsConfig.IsEnabledSanityHint)
            {
                string userId = player.UserId;
                if (!_lastHintTime.TryGetValue(userId, out var lastTime) || (now - lastTime).TotalSeconds >= _hintCooldown)
                {
                    SendSanityHint(player, _plugin.Config.HintsConfig.SanityDecreasedHint, newSanity);
                    _lastHintTime[userId] = now;
                }
            }
        }

        private void ProcessRegenTick(Player player, DateTime now)
        {
            float oldSanity = GetCurrentSanity(player);

            // 1. Edge-case safety block
            if (oldSanity >= 100f)
            {
                SafeUpdateAmbient(player, shouldPlayDrone: false);
                return;
            }

            // FIX: Calculate the core regeneration rate, adding extra boost if the painkiller duration is still running
            float regenRate = _sanityConfig.PassiveRegenRate;
            lock (_cacheLock)
            {
                if (_painkillerSanityBoostExpiry.TryGetValue(player.UserId, out DateTime boostExpiry) && now < boostExpiry)
                {
                    regenRate += _sanityConfig.PainkillersExtraSanityRegen;
                }
            }

            float newSanity = ChangeSanityValue(player, regenRate);

            // 2. Continuous Background State Evaluation
            bool requiresLowDrone = newSanity <= 35f;
            SafeUpdateAmbient(player, requiresLowDrone);

            // 3. Process UI alert prompt updates
            if (_plugin.Config.HintsConfig.IsEnabledSanityHint)
            {
                string userId = player.UserId;
                if (!_lastHintTime.TryGetValue(userId, out var lastTime) || (now - lastTime).TotalSeconds >= _hintCooldown)
                {
                    SendSanityHint(player, _plugin.Config.HintsConfig.SanityIncreasedHint, newSanity);
                    _lastHintTime[userId] = now;
                }
            }
        }

        private float CalculateDecayRate(Player player)
        {
            float decayRate = _sanityConfig.DecayRateBase;
            if (_plugin.Npc?.Methods?.IsBlackoutActive == true)
                decayRate *= _sanityConfig.DecayMultiplierBlackout;
            if (Helpers.IsHumanWithoutLight(player))
                decayRate *= _sanityConfig.DecayMultiplierDarkness;
            return decayRate;
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Validates if an actor is currently eligible for psychological processing loops and environmental interactions.
        /// </summary>
        public bool IsValidPlayer(Player player)
        {
            return player != null &&
                   !string.IsNullOrEmpty(player.UserId) &&
                   player.IsAlive &&
                   player.IsHuman &&
                   player.Room != null &&
                   player.Room.Name != MapGeneration.RoomName.Pocket;
        }

        private float GetItemRestoreAmount(ItemType itemType)
        {
            return itemType switch
            {
                ItemType.SCP500 => UnityEngine.Random.Range(_sanityConfig.Scp500RestoreMin, _sanityConfig.Scp500RestoreMax),
                ItemType.Painkillers => UnityEngine.Random.Range(_sanityConfig.PainkillersRestoreMin, _sanityConfig.PainkillersRestoreMax),
                _ => 0f
            };
        }

        /// <summary>
        /// Determines whether the designated player is currently shielded by painkillers protection.
        /// </summary>
        public bool IsProtectedByPainkillers(Player player)
        {
            if (player == null || string.IsNullOrEmpty(player.UserId)) return false;

            lock (_cacheLock)
            {
                if (_painkillerProtectionExpiry.TryGetValue(player.UserId, out DateTime expiryTime))
                {
                    return DateTime.Now < expiryTime;
                }
            }
            return false;
        }

        private void SafeUpdateAmbient(Player player, bool shouldPlayDrone)
        {
            string userId = player.UserId;

            lock (_cacheLock)
            {
                if (_activeAmbientState.TryGetValue(userId, out bool currentState) && currentState == shouldPlayDrone)
                {
                    return;
                }
            }

            bool success = _plugin.AudioManager.UpdatePlayerBackgroundAmbient(player, shouldPlayDrone);

            if (success || !shouldPlayDrone)
            {
                lock (_cacheLock)
                {
                    _activeAmbientState[userId] = shouldPlayDrone;
                }
                LibraryLabAPI.LogDebug("PlayerSanityHandler", $"Ambient state committed for {player.Nickname} to: {shouldPlayDrone}");
            }
        }

        private void SendSanityHint(Player player, string hintMessage, float sanity)
        {
            string formatted = string.Format(hintMessage, sanity.ToString("F1"));
            player.SendHint(formatted, 5f);
        }

        private static void ApplyEffect(Player player, SanityEffectType effectType, byte intensity, float duration)
        {
            switch (effectType)
            {
                case SanityEffectType.Blurred: player.EnableEffect<CustomPlayerEffects.Blurred>(intensity, duration); break;
                case SanityEffectType.Blindness: player.EnableEffect<CustomPlayerEffects.Blindness>(intensity, duration); break;
                case SanityEffectType.Flashed: player.EnableEffect<CustomPlayerEffects.Flashed>(intensity, duration); break;
                case SanityEffectType.Deafened: player.EnableEffect<CustomPlayerEffects.Deafened>(intensity, duration); break;
                case SanityEffectType.Slowness: player.EnableEffect<CustomPlayerEffects.Slowness>(intensity, duration); break;
                case SanityEffectType.SilentWalk: player.EnableEffect<CustomPlayerEffects.SilentWalk>(intensity, duration); break;
                case SanityEffectType.Exhausted: player.EnableEffect<CustomPlayerEffects.Exhausted>(intensity, duration); break;
                case SanityEffectType.Disabled: player.EnableEffect<CustomPlayerEffects.Disabled>(intensity, duration); break;
                case SanityEffectType.Bleeding: player.EnableEffect<CustomPlayerEffects.Bleeding>(intensity, duration); break;
                case SanityEffectType.Poisoned: player.EnableEffect<CustomPlayerEffects.Poisoned>(intensity, duration); break;
                case SanityEffectType.Burned: player.EnableEffect<CustomPlayerEffects.Burned>(intensity, duration); break;
                case SanityEffectType.Corroding: player.EnableEffect<CustomPlayerEffects.Corroding>(intensity, duration); break;
                case SanityEffectType.Concussed: player.EnableEffect<CustomPlayerEffects.Concussed>(intensity, duration); break;
                case SanityEffectType.Traumatized: player.EnableEffect<CustomPlayerEffects.Traumatized>(intensity, duration); break;
                case SanityEffectType.Invisible: player.EnableEffect<CustomPlayerEffects.Invisible>(intensity, duration); break;
                case SanityEffectType.Scp207: player.EnableEffect<CustomPlayerEffects.Scp207>(intensity, duration); break;
                case SanityEffectType.AntiScp207: player.EnableEffect<CustomPlayerEffects.AntiScp207>(intensity, duration); break;
                case SanityEffectType.MovementBoost: player.EnableEffect<CustomPlayerEffects.MovementBoost>(intensity, duration); break;
                case SanityEffectType.DamageReduction: player.EnableEffect<CustomPlayerEffects.DamageReduction>(intensity, duration); break;
                case SanityEffectType.RainbowTaste: player.EnableEffect<CustomPlayerEffects.RainbowTaste>(intensity, duration); break;
                case SanityEffectType.BodyshotReduction: player.EnableEffect<CustomPlayerEffects.BodyshotReduction>(intensity, duration); break;
                case SanityEffectType.Scp1853: player.EnableEffect<CustomPlayerEffects.Scp1853>(intensity, duration); break;
                case SanityEffectType.CardiacArrest: player.EnableEffect<CustomPlayerEffects.CardiacArrest>(intensity, duration); break;
                case SanityEffectType.InsufficientLighting: player.EnableEffect<CustomPlayerEffects.InsufficientLighting>(intensity, duration); break;
                case SanityEffectType.SoundtrackMute: player.EnableEffect<CustomPlayerEffects.SoundtrackMute>(intensity, duration); break;
                case SanityEffectType.SpawnProtected: player.EnableEffect<CustomPlayerEffects.SpawnProtected>(intensity, duration); break;
                case SanityEffectType.Ensnared: player.EnableEffect<CustomPlayerEffects.Ensnared>(intensity, duration); break;
                case SanityEffectType.Ghostly: player.EnableEffect<CustomPlayerEffects.Ghostly>(intensity, duration); break;
                case SanityEffectType.SeveredHands: player.EnableEffect<CustomPlayerEffects.SeveredHands>(intensity, duration); break;
                case SanityEffectType.Stained: player.EnableEffect<CustomPlayerEffects.Stained>(intensity, duration); break;
                case SanityEffectType.Vitality: player.EnableEffect<CustomPlayerEffects.Vitality>(intensity, duration); break;
                case SanityEffectType.Asphyxiated: player.EnableEffect<CustomPlayerEffects.Asphyxiated>(intensity, duration); break;
                case SanityEffectType.Decontaminating: player.EnableEffect<CustomPlayerEffects.Decontaminating>(intensity, duration); break;
                case SanityEffectType.PocketCorroding: player.EnableEffect<CustomPlayerEffects.PocketCorroding>(intensity, duration); break;
                default: throw new ArgumentException($"Unknown effect type: {effectType}");
            }
        }

        #endregion
    }
}