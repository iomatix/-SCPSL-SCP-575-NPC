namespace SCP_575.Handlers
{
    using Hints;
    using LabApi.Events.Arguments.PlayerEvents;
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
        private readonly PlayerSanityConfig _sanityConfig;
        private readonly Dictionary<string, float> _sanityCache = new();
        private readonly Dictionary<string, DateTime> _lastHintTime = new();
        private readonly float _hintCooldown;
        private CoroutineHandle _sanityDecayCoroutine;
        private bool _isDisposed;

        /// <summary>
        /// Gets the internal sanity cache mapping UserId to current sanity values.
        /// </summary>
        public IReadOnlyDictionary<string, float> SanityCache => _sanityCache;

        /// <summary>
        /// Initializes a new instance of the <see cref="PlayerSanityHandler"/> class.
        /// </summary>
        /// <param name="plugin">The main <see cref="Plugin"/> instance.</param>
        /// <exception cref="ArgumentNullException">Thrown if the plugin instance is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the sanity configuration is not initialized.</exception>
        public PlayerSanityHandler(Plugin plugin)
        {
            _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin), "Plugin instance cannot be null.");
            _sanityConfig = plugin.Config?.SanityConfig ?? throw new InvalidOperationException("SanityConfig is not initialized.");
            _hintCooldown = _sanityConfig.DecayRateBase * 20f;
        }

        #region Lifecycle Management

        /// <summary>
        /// Initializes the handler and starts the sanity decay coroutine.
        /// </summary>
        public void Initialize()
        {
            if (!_plugin.IsEventActive)
            {
                this.Dispose();
                return;
            }
            if (_isDisposed) return;

            if(!_sanityDecayCoroutine.IsRunning) _sanityDecayCoroutine = Timing.RunCoroutine(HandleSanityDecay());
            Library_ExiledAPI.LogInfo("PlayerSanityHandler.Initialize", "Sanity decay coroutine started.");
        }

        /// <summary>
        /// Disposes the handler, stopping coroutines and clearing resources.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed) return;

            _isDisposed = true;
            if (_sanityDecayCoroutine.IsRunning)
                Timing.KillCoroutines(_sanityDecayCoroutine);

            _sanityCache.Clear();
            _lastHintTime.Clear();
            Library_ExiledAPI.LogInfo("PlayerSanityHandler.Dispose", "Disposed PlayerSanityHandler and cleaned up resources.");
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Initializes a player's sanity to the configured starting value upon spawning.
        /// </summary>
        /// <param name="ev">Event arguments containing the player reference.</param>
        public override void OnPlayerSpawned(PlayerSpawnedEventArgs ev)
        {;
            if (!IsValidPlayer(ev?.Player)) return;

            _sanityCache[ev.Player.UserId] = _sanityConfig.InitialSanity;
            Library_ExiledAPI.LogDebug("PlayerSanityHandler.OnPlayerSpawned", $"Initialized sanity for {ev.Player.UserId} to {_sanityConfig.InitialSanity}.");
        }

        /// <summary>
        /// Restores sanity when a player uses specific items (e.g., SCP-500, Painkillers).
        /// </summary>
        /// <param name="ev">Event arguments containing item and player information.</param>
        public override void OnPlayerUsedItem(PlayerUsedItemEventArgs ev)
        {
            if (!IsValidPlayer(ev?.Player) || !IsPlayerValidForSanitySystem(ev.Player) || ev.UsableItem?.Type == null) return;

            float restoreAmount = GetItemRestoreAmount(ev.UsableItem.Type);
            if (restoreAmount <= 0f) return;

            float newSanity = ChangeSanityValue(ev.Player, restoreAmount);
            if (_plugin.Config.HintsConfig.IsEnabledSanityHint) SendSanityHint(ev.Player, _plugin.Config.HintsConfig.SanityIncreasedHint, newSanity);

            Library_ExiledAPI.LogDebug("PlayerSanityHandler.OnPlayerUsedItem", $"Restored {restoreAmount} sanity to {ev.Player.UserId} with {ev.UsableItem.Type}. New sanity: {newSanity}");
        }

        #endregion

        #region Sanity Management

        /// <summary>
        /// Gets the current sanity value for a player.
        /// </summary>
        /// <param name="player">The player to query.</param>
        /// <returns>The player's sanity value, or 100f if not cached.</returns>
        public float GetCurrentSanity(Player player)
        {
            return IsValidPlayer(player) && _sanityCache.TryGetValue(player.UserId, out float sanity) ? sanity : 100f;
        }

        /// <summary>
        /// Sets a player's sanity value, clamped between 0 and 100.
        /// </summary>
        /// <param name="player">The target player.</param>
        /// <param name="sanity">The sanity value to set.</param>
        /// <returns>The clamped sanity value.</returns>
        /// <exception cref="ArgumentNullException">Thrown if player or UserId is null.</exception>
        public float SetSanity(Player player, float sanity)
        {
            if (!IsValidPlayer(player))
                throw new ArgumentNullException(nameof(player), "Player or UserId cannot be null.");

            float clampedSanity = Mathf.Clamp(sanity, 0f, 100f);
            _sanityCache[player.UserId] = clampedSanity;
            Library_ExiledAPI.LogDebug("PlayerSanityHandler.SetSanity", $"Set sanity for {player.UserId} to {clampedSanity}.");
            return clampedSanity;
        }

        /// <summary>
        /// Changes a player's sanity value by the specified amount and sends a hint if applicable.
        /// </summary>
        /// <param name="player">The target player.</param>
        /// <param name="amount">The amount to change sanity by (positive or negative).</param>
        /// <returns>The new sanity value.</returns>
        public float ChangeSanityValue(Player player, float amount)
        {
            if (!IsValidPlayer(player)) return 0f;

            try
            {
                float currentSanity = GetCurrentSanity(player);
                float newSanity = SetSanity(player, currentSanity + amount);
                Library_ExiledAPI.LogDebug("PlayerSanityHandler.ChangeSanityValue", $"Changed sanity for {player.UserId} by {amount}. New value: {newSanity}");
                return newSanity;
            }
            catch (Exception ex)
            {
                Library_ExiledAPI.LogError("PlayerSanityHandler.ChangeSanityValue", $"Failed to change sanity for {player?.UserId ?? "null"}: {ex.Message}");
                return 0f;
            }
        }

        /// <summary>
        /// Gets the sanity stage configuration for a given sanity value.
        /// </summary>
        /// <param name="sanity">The sanity value to evaluate.</param>
        /// <returns>The matching <see cref="PlayerSanityStageConfig"/> or null if none found.</returns>
        public PlayerSanityStageConfig GetCurrentSanityStage(float sanity)
        {
            return _sanityConfig.SanityStages?.FirstOrDefault(stage =>
                sanity <= stage.MaxThreshold && sanity > stage.MinThreshold);
        }

        /// <summary>
        /// Gets the sanity stage configuration for a player.
        /// </summary>
        /// <param name="player">The player to evaluate.</param>
        /// <returns>The matching <see cref="PlayerSanityStageConfig"/> or null if none found.</returns>
        public PlayerSanityStageConfig GetCurrentSanityStage(Player player)
        {
            return IsValidPlayer(player) ? GetCurrentSanityStage(GetCurrentSanity(player)) : null;
        }

        /// <summary>
        /// Applies status effects to a player based on their current sanity stage.
        /// </summary>
        /// <param name="player">The player to apply effects to.</param>
        public void ApplyStageEffects(Player player)
        {
            if (!IsValidPlayer(player)) return;

            var stage = GetCurrentSanityStage(player);
            if (stage?.Effects == null) return;

            foreach (var effectConfig in stage.Effects)
            {
                try
                {
                    ApplyEffect(player, effectConfig.EffectType, effectConfig.Intensity, effectConfig.Duration);
                    if (stage.DamageOnStrike > 0) Scp575DamageSystem.DamagePlayer(player, stage.DamageOnStrike);
                }
                catch (Exception ex)
                {
                    Library_ExiledAPI.LogWarn("PlayerSanityHandler.ApplyStageEffects", $"Failed to apply effect {effectConfig.EffectType} to {player.UserId}: {ex.Message}");
                }
            }
        }

        #endregion

        #region Sanity Decay

        /// <summary>
        /// Periodically reduces sanity for eligible players based on game conditions (e.g., darkness, blackout).
        /// </summary>
        /// <returns>An enumerator for the MEC coroutine system.</returns>
        private IEnumerator<float> HandleSanityDecay()
        {
            while (!_isDisposed)
            {
                yield return Timing.WaitForSeconds(1f);

                foreach (var player in Player.List.Where(p => IsPlayerValidForSanitySystem(p)))
                {
                    if (!Library_LabAPI.IsPlayerInDarkRoom(player)) continue;

                    float decayRate = CalculateDecayRate(player);
                    float newSanity = ChangeSanityValue(player, -decayRate);

                    if (_plugin.Config.HintsConfig.IsEnabledSanityHint && ShouldSendHint(player.UserId))
                    {
                        SendSanityHint(player, _plugin.Config.HintsConfig.SanityDecreasedHint, newSanity);
                        _lastHintTime[player.UserId] = DateTime.Now;
                    }
                }
            }
        }

        private float CalculateDecayRate(Player player)
        {
            float decayRate = _sanityConfig.DecayRateBase;
            if (_plugin.Npc?.Methods?.IsBlackoutActive == true)
                decayRate *= _sanityConfig.DecayMultiplierBlackout;
            if (Library_LabAPI.IsPlayerInDarkRoom(player))
                decayRate *= _sanityConfig.DecayMultiplierDarkness;
            return decayRate;
        }

        #endregion

        #region Helper Methods

        private bool IsValidPlayer(Player player)
        {
            if (player?.UserId == null)
            {
                Library_ExiledAPI.LogDebug("PlayerSanityHandler.IsValidPlayer", "Player or UserId is null.");
                return false;
            }
            return true;
        }

        private bool IsPlayerValidForSanitySystem(Player player)
        {
            return IsValidPlayer(player) && player.IsAlive && player.IsHuman;
        }

        private float GetItemRestoreAmount(ItemType itemType)
        {
            return itemType switch
            {
                ItemType.SCP500 => UnityEngine.Random.Range(_sanityConfig.SCP500RestoreMin, _sanityConfig.SCP500RestoreMax),
                ItemType.Painkillers => UnityEngine.Random.Range(_sanityConfig.PillsRestoreMin, _sanityConfig.PillsRestoreMax),
                _ => 0f
            };
        }

        private bool ShouldSendHint(string userId)
        {
            return !_lastHintTime.TryGetValue(userId, out var lastTime) ||
                   (DateTime.Now - lastTime).TotalSeconds >= _hintCooldown;
        }

        private void SendSanityHint(Player player, string hintMessage, float sanity)
        {
            player.SendHint(hintMessage, new[] { new FloatHintParameter(sanity, "F1") });
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