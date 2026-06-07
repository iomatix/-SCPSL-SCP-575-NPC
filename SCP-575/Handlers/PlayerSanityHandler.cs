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
    using System.Linq;
    using UnityEngine;

    /// <summary>
    /// Manages the internal psychological state of human actors, governing neurological decay vectors, 
    /// systemic affliction mapping, and panic-induced defensive feedback loops under absolute darkness.
    /// </summary>
    public class PlayerSanityHandler : CustomEventsHandler, IDisposable
    {
        private readonly Plugin _plugin;
        private readonly LibraryLabAPI _libraryLabAPI;
        private readonly PlayerSanityConfig _sanityConfig;

        private readonly Dictionary<string, float> _sanityCache = new();
        private readonly Dictionary<string, DateTime> _lastHintTime = new();
        private readonly List<PlayerSanityStageConfig> _orderedStages;

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
        private readonly Dictionary<string, bool> _activeAmbientState = new();

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
            // Coroutine initialization can be triggered here or via server start events.
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
                _plugin.AudioManager.UpdatePlayerBackgroundAmbient(ev.Player, shouldPlayDrone: false);
            }
        }

        public override void OnPlayerLeft(PlayerLeftEventArgs ev)
        {
            if (ev?.Player == null) return;
            string userId = NormalizeUserId(ev.Player.UserId);

            lock (_cacheLock)
            {
                _sanityCache.Remove(userId);
                _lastHintTime.Remove(userId);
                _activeAmbientState.Remove(userId);
            }

            // Explicitly notify the audio manager to release tracking keys upon network disconnect
            _plugin.AudioManager.ForceStopAllPlayerAudio(ev.Player);
        }

        public override void OnPlayerUsedItem(PlayerUsedItemEventArgs ev)
        {
            if (!_plugin.IsEventActive || !IsValidPlayer(ev?.Player) || ev.UsableItem?.Type == null) return;

            float restoreAmount = GetItemRestoreAmount(ev.UsableItem.Type);
            if (restoreAmount <= 0f) return;

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

            string userId = NormalizeUserId(player.UserId);
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

            string userId = NormalizeUserId(player.UserId);
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
        public void ApplyStageEffects(Player player)
        {
            if (!IsValidPlayer(player)) return;

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

            var stage = GetCurrentSanityStage(player);
            if (stage == null) return;

            if (Helpers.IsHumanWithoutLight(player) || stage.OverrideLightSourceSanityProtection)
            {
                float culmDamage = stage.DamageOnStrike + (stage.AdditionalDamagePerStack * _plugin.Npc.Methods.GetCurrentBlackoutStacks);
                if (culmDamage > 0)
                {
                    _plugin.AudioManager.PlayAudioAtPosition(AudioKey.ShadowStrike, player.Position, isTransient: true);
                    Scp575DamageSystem.DamagePlayer(player, culmDamage);
                }
            }
            else
            {
                float culmDamage = stage.DamageOnStrikeWhenLightsourceActive + (stage.AdditionalDamagePerStackWhenLightsourceActive * _plugin.Npc.Methods.GetCurrentBlackoutStacks);
                if (culmDamage > 0)
                {
                    _plugin.AudioManager.PlayAudioAtPosition(AudioKey.ShadowStrike, player.Position, isTransient: true);
                    Scp575DamageSystem.DamagePlayer(player, culmDamage);
                }
            }
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

            // 2. Localized Hallucination Logic (3D Isolated Spatialized Auditory Jumpscares)
            var stage = GetCurrentSanityStage(newSanity);
            if (stage != null)
            {
                if (UnityEngine.Random.value < 0.035f) // 3.5% chance per tick
                {
                    AudioKey? whisperToPlay = null;

                    if (newSanity <= 10f) whisperToPlay = AudioKey.WhispersMixed;
                    else if (newSanity <= 25f) whisperToPlay = AudioKey.Whispers_3;
                    else if (newSanity <= 55f) whisperToPlay = AudioKey.Whispers_2;
                    else if (newSanity <= 85f) whisperToPlay = AudioKey.Whispers_1;

                    if (whisperToPlay.HasValue)
                    {
                        _plugin.AudioManager.PlayIsolatedSpatialAudio(player, whisperToPlay.Value, player.Position);
                    }
                }
            }

            // 3. Process UI alert prompt updates
            if (_plugin.Config.HintsConfig.IsEnabledSanityHint)
            {
                string userId = NormalizeUserId(player.UserId);
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

            float newSanity = ChangeSanityValue(player, _sanityConfig.PassiveRegenRate);

            // 2. Continuous Background State Evaluation
            bool requiresLowDrone = newSanity <= 35f;
            SafeUpdateAmbient(player, requiresLowDrone);

            // 3. Process UI alert prompt updates
            if (_plugin.Config.HintsConfig.IsEnabledSanityHint)
            {
                string userId = NormalizeUserId(player.UserId);
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

        private string NormalizeUserId(string userId)
        {
            return userId?.ToLowerInvariant() ?? string.Empty;
        }

        private float GetItemRestoreAmount(ItemType itemType)
        {
            return itemType switch
            {
                ItemType.SCP500 => UnityEngine.Random.Range(_sanityConfig.Scp500RestoreMin, _sanityConfig.Scp500RestoreMax),
                ItemType.Painkillers => UnityEngine.Random.Range(_sanityConfig.PillsRestoreMin, _sanityConfig.PillsRestoreMax),
                _ => 0f
            };
        }

        private void SafeUpdateAmbient(Player player, bool shouldPlayDrone)
        {
            string userId = NormalizeUserId(player.UserId);

            lock (_cacheLock)
            {
                if (_activeAmbientState.TryGetValue(userId, out bool currentState) && currentState == shouldPlayDrone)
                {
                    return;
                }
                _activeAmbientState[userId] = shouldPlayDrone;
            }

            _plugin.AudioManager.UpdatePlayerBackgroundAmbient(player, shouldPlayDrone);
            LibraryLabAPI.LogDebug("PlayerSanityHandler", $"Ambient state changed for {player.Nickname} to: {shouldPlayDrone}");
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