namespace SCP_575.Handlers
{
    using Hints;
    using LabApi.Events.Arguments.PlayerEvents;
    using LabApi.Events.Arguments.ServerEvents;
    using LabApi.Events.CustomHandlers;
    using LabApi.Features.Wrappers;
    using MEC;
    using SCP_575.ConfigObjects;
    using SCP_575.Shared;
    using SCP_575.Systems;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEngine;

    /// <summary>
    /// Manages player sanity mechanics, including tracking, decay, and applying effects based on sanity thresholds.
    /// </summary>
    public class PlayerSanityHandler : CustomEventsHandler, IDisposable
    {
        private readonly Plugin _plugin;
        private readonly LibraryLabAPI _libraryLabAPI;
        private readonly PlayerSanityConfig _sanityConfig;

        private readonly Dictionary<string, float> _sanityCache = new();
        private readonly Dictionary<string, DateTime> _lastHintTime = new();

        // Cache sorted stages to avoid memory allocation every tick
        private readonly List<PlayerSanityStageConfig> _orderedStages;

        private readonly float _hintCooldown;
        private bool _isDisposed;
        private readonly object _cacheLock = new();

        private const string SanityCoroutineTag = "SCP575-SanityHandler";

        public IReadOnlyDictionary<string, float> SanityCache => _sanityCache;

        public PlayerSanityHandler(Plugin plugin)
        {
            _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin), "Plugin instance cannot be null.");
            _libraryLabAPI = _plugin.LibraryLabAPI;
            _sanityConfig = plugin.Config?.SanityConfig ?? throw new InvalidOperationException("SanityConfig is not initialized.");
            _hintCooldown = _sanityConfig.DecayRateBase * 20f;

            if (_sanityConfig.SanityStages == null || !_sanityConfig.SanityStages.Any())
                throw new InvalidOperationException("SanityStages is null or empty.");

            // Sort and cache stages ascending for validation, descending for evaluation
            var stages = _sanityConfig.SanityStages.OrderBy(s => s.MinThreshold).ToList();

            if (stages[0].MinThreshold > 0 || stages[stages.Count - 1].MaxThreshold < 100)
                throw new InvalidOperationException("SanityStages do not cover the full range (0–100).");

            for (int i = 0; i < stages.Count - 1; i++)
            {
                if (stages[i].MaxThreshold != stages[i + 1].MinThreshold)
                    throw new InvalidOperationException("SanityStages have gaps or overlaps.");
            }

            // Cache descending order for fast evaluation during gameplay
            _orderedStages = stages.OrderByDescending(s => s.MaxThreshold).ToList();

            LibraryLabAPI.LogDebug("PlayerSanityHandler", $"Loaded {stages.Count} sanity stages.");
        }

        #region Lifecycle Management

        public void Initialize()
        {
            if (_isDisposed) return;
        }

        public void Clean()
        {
            Timing.KillCoroutines(SanityCoroutineTag);

            lock (_cacheLock)
            {
                _sanityCache.Clear();
                _lastHintTime.Clear();
            }
        }

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
        public override void OnPlayerChangedRole(PlayerChangedRoleEventArgs ev) => ResetPlayerSanity(ev?.Player);

        private void ResetPlayerSanity(Player player)
        {
            if (!_plugin.IsEventActive || !IsValidPlayer(player)) return;

            string userId = NormalizeUserId(player.UserId);
            lock (_cacheLock)
            {
                _sanityCache[userId] = _sanityConfig.InitialSanity;
            }
        }

        public override void OnPlayerUsedItem(PlayerUsedItemEventArgs ev)
        {
            if (!_plugin.IsEventActive || !IsValidPlayer(ev?.Player) || ev.UsableItem?.Type == null) return;

            float restoreAmount = GetItemRestoreAmount(ev.UsableItem.Type);
            if (restoreAmount <= 0f) return;

            float newSanity = ChangeSanityValue(ev.Player, restoreAmount);
            if (_plugin.Config.HintsConfig.IsEnabledSanityHint)
                SendSanityHint(ev.Player, _plugin.Config.HintsConfig.SanityIncreasedHint, newSanity);

            LibraryLabAPI.LogDebug("PlayerSanityHandler", $"Restored {restoreAmount} sanity to {ev.Player.Nickname}. New sanity: {newSanity}");
        }

        #endregion

        #region Sanity Management

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

        public float ChangeSanityValue(Player player, float amount)
        {
            if (!IsValidPlayer(player)) return 0f;

            float currentSanity = GetCurrentSanity(player);
            return SetSanity(player, currentSanity + amount);
        }

        public PlayerSanityStageConfig GetCurrentSanityStage(float sanity)
        {
            foreach (var s in _orderedStages)
            {
                if (sanity <= s.MaxThreshold && (sanity > s.MinThreshold || (sanity == 0 && s.MinThreshold == 0)))
                    return s;
            }
            return null;
        }

        public PlayerSanityStageConfig GetCurrentSanityStage(Player player)
        {
            if (!IsValidPlayer(player)) return null;
            return GetCurrentSanityStage(GetCurrentSanity(player));
        }

        public void ApplyStageEffects(Player player)
        {
            if (!IsValidPlayer(player)) return;

            var stage = GetCurrentSanityStage(player);
            if (stage == null) return;

            if (Helpers.IsHumanWithoutLight(player) || stage.OverrideLightSourceSanityProtection)
            {
                float culmDamage = stage.DamageOnStrike + (stage.AdditionalDamagePerStack * _plugin.Npc.Methods.GetCurrentBlackoutStacks);
                if (culmDamage > 0)
                {
                    Scp575DamageSystem.DamagePlayer(player, culmDamage);
                }

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

        #endregion

        #region Sanity Decay

        /// <summary>
        /// Periodically reduces sanity for eligible players based on game conditions.
        /// </summary>
        public IEnumerator<float> HandleSanityDecay()
        {
            while (true)
            {
                if (!_plugin.IsEventActive)
                {
                    yield return Timing.WaitForSeconds(1f);
                    continue;
                }

                yield return Timing.WaitForSeconds(1f);

                foreach (var player in Player.ReadyList)
                {
                    if (!IsValidPlayer(player) || !_libraryLabAPI.IsPlayerInDarkRoom(player))
                        continue;

                    float decayRate = CalculateDecayRate(player);
                    float newSanity = ChangeSanityValue(player, -decayRate);

                    if (_plugin.Config.HintsConfig.IsEnabledSanityHint && ShouldSendHint(player.UserId))
                    {
                        SendSanityHint(player, _plugin.Config.HintsConfig.SanityDecreasedHint, newSanity);
                        _lastHintTime[NormalizeUserId(player.UserId)] = DateTime.Now;
                    }
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

        private bool ShouldSendHint(string userId)
        {
            string normalizedUserId = NormalizeUserId(userId);
            return !_lastHintTime.TryGetValue(normalizedUserId, out var lastTime) ||
                   (DateTime.Now - lastTime).TotalSeconds >= _hintCooldown;
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
    }
    #endregion
}
