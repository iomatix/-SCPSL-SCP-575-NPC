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
        private readonly PlayerSanityConfig _sanityConfig;
        private readonly Dictionary<string, float> _sanityCache = new();
        private readonly Dictionary<string, DateTime> _lastHintTime = new();
        private readonly float _hintCooldown;
        private CoroutineHandle _sanityDecayCoroutine;
        private bool _isDisposed;
        private readonly object _cacheLock = new();
        private readonly int _instanceId;

        public CoroutineHandle SanityDecayCoroutine
        {
            get => _sanityDecayCoroutine;
            set => _sanityDecayCoroutine = value;
        }

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
            _instanceId = GetHashCode();
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
            Library_ExiledAPI.LogDebug("PlayerSanityHandler.Constructor", $"Instance ID={_instanceId}, Loaded {stages.Count} sanity stages: {string.Join(", ", stages.Select(s => $"[Min={s.MinThreshold}, Max={s.MaxThreshold}, Damage={s.DamageOnStrike}, Effects={s.Effects?.Count}]"))}");
        }

        #region Lifecycle Management

        /// <summary>
        /// Initializes the handler and starts the sanity decay coroutine.
        /// </summary>
        public void Initialize()
        {
            if (_isDisposed) return;
        }

        public void Clean()
        {
            if (_sanityDecayCoroutine.IsRunning)
                Timing.KillCoroutines(_sanityDecayCoroutine);

            lock (_cacheLock)
            {
                _sanityCache.Clear();
                _lastHintTime.Clear();
            }
        }

        /// <summary>
        /// Disposes the handler, stopping coroutines and clearing resources.
        /// </summary>
        public void Dispose()
        {
            Clean();
            if (_isDisposed) return;

            _isDisposed = true;

            Library_ExiledAPI.LogInfo("PlayerSanityHandler.Dispose", $"Instance ID={_instanceId}, Disposed PlayerSanityHandler and cleaned up resources.");
        }


        public override void OnServerRoundEnded(RoundEndedEventArgs ev)
        {
            Clean();
        }

        public override void OnServerWaitingForPlayers()
        {
            Clean();
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Initializes a player's sanity to the configured starting value upon spawning.
        /// </summary>
        /// <param name="ev">Event arguments containing the player reference.</param>
        public override void OnPlayerSpawned(PlayerSpawnedEventArgs ev)
        {
            if (!_plugin.IsEventActive) return;
            if (!IsValidPlayer(ev?.Player)) return;

            string userId = NormalizeUserId(ev.Player.UserId);
            lock (_cacheLock)
            {
                _sanityCache[userId] = _sanityConfig.InitialSanity;
            }
        }

        /// <summary>
        /// Initializes a player's sanity to the configured starting value upon spawning.
        /// </summary>
        /// <param name="ev">Event arguments containing the player reference.</param>
        public override void OnPlayerChangedRole(PlayerChangedRoleEventArgs ev)
        {
            if (!_plugin.IsEventActive) return;
            if (!IsValidPlayer(ev?.Player)) return;

            string userId = NormalizeUserId(ev.Player.UserId);
            lock (_cacheLock)
            {
                _sanityCache[userId] = _sanityConfig.InitialSanity;
            }
        }

        /// <summary>
        /// Restores sanity when a player uses specific items (e.g., SCP-500, Painkillers).
        /// </summary>
        /// <param name="ev">Event arguments containing item and player information.</param>
        public override void OnPlayerUsedItem(PlayerUsedItemEventArgs ev)
        {
            if (!_plugin.IsEventActive) return;
            if (!IsValidPlayer(ev?.Player) || !IsPlayerValidForSanitySystem(ev.Player) || ev.UsableItem?.Type == null) return;

            float restoreAmount = GetItemRestoreAmount(ev.UsableItem.Type);
            if (restoreAmount <= 0f) return;

            float newSanity = ChangeSanityValue(ev.Player, restoreAmount);
            if (_plugin.Config.HintsConfig.IsEnabledSanityHint) SendSanityHint(ev.Player, _plugin.Config.HintsConfig.SanityIncreasedHint, newSanity);

            Library_ExiledAPI.LogDebug("PlayerSanityHandler.OnPlayerUsedItem", $"Instance ID={_instanceId}, Restored {restoreAmount} sanity to {ev.Player.UserId} ({ev.Player.Nickname}) with {ev.UsableItem.Type}. New sanity: {newSanity}");
        }



        #endregion

        #region Sanity Management

        /// <summary>
        /// Gets the current sanity value for a player.
        /// </summary>
        /// <param name="player">The player to query.</param>
        /// <returns>The player's sanity value, or the configured initial sanity if not cached.</returns>
        public float GetCurrentSanity(Player player)
        {
            if (!IsValidPlayer(player))
                throw new ArgumentNullException(nameof(player), "Player or UserId cannot be null.");
            string userId = NormalizeUserId(player.UserId);
            lock (_cacheLock)
            {
                if (!_sanityCache.TryGetValue(userId, out float sanity))
                {
                    sanity = _sanityConfig.InitialSanity;
                    _sanityCache[userId] = sanity;
                    Library_ExiledAPI.LogWarn("PlayerSanityHandler.GetCurrentSanity", $"Instance ID={_instanceId}, No sanity value for {userId}, defaulting to {sanity} (InitialSanity from config), Cache count={_sanityCache.Count}");
                }
                string nickname = "Unknown";
                bool isAlive = false;
                bool isHuman = false;
                try
                {
                    nickname = player.Nickname ?? "null";
                    isAlive = player.IsAlive;
                    isHuman = player.IsHuman;
                }
                catch (Exception ex)
                {
                    Library_ExiledAPI.LogWarn("PlayerSanityHandler.GetCurrentSanity", $"Instance ID={_instanceId}, Failed to access player properties for {userId}: {ex.Message}");
                }
                Library_ExiledAPI.LogDebug("PlayerSanityHandler.GetCurrentSanity", $"Instance ID={_instanceId}, Cache state for {userId} ({nickname}): {sanity}, IsAlive={isAlive}, IsHuman={isHuman}, Cache count={_sanityCache.Count}");
                return sanity;
            }
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
            string userId = NormalizeUserId(player.UserId);
            float clampedSanity = Mathf.Clamp(sanity, 0f, 100f);
            lock (_cacheLock)
            {
                _sanityCache[userId] = clampedSanity;
                string nickname = "Unknown";
                try
                {
                    nickname = player.Nickname ?? "null";
                }
                catch (Exception ex)
                {
                    Library_ExiledAPI.LogWarn("PlayerSanityHandler.SetSanity", $"Instance ID={_instanceId}, Failed to access nickname for {userId}: {ex.Message}");
                }
                Library_ExiledAPI.LogDebug("PlayerSanityHandler.SetSanity", $"Instance ID={_instanceId}, Set sanity for {userId} ({nickname}) to {clampedSanity}, Cache count={_sanityCache.Count}");
            }
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
                string userId = NormalizeUserId(player.UserId);
                float currentSanity = GetCurrentSanity(player);
                float newSanity = SetSanity(player, currentSanity + amount);
                Library_ExiledAPI.LogDebug("PlayerSanityHandler.ChangeSanityValue", $"Instance ID={_instanceId}, Changed sanity for {userId} ({player.Nickname ?? "null"}) by {amount}. New value: {newSanity}");
                return newSanity;
            }
            catch (Exception ex)
            {
                Library_ExiledAPI.LogError("PlayerSanityHandler.ChangeSanityValue", $"Instance ID={_instanceId}, Failed to change sanity for {player?.UserId ?? "null"} ({player?.Nickname ?? "null"}): {ex.Message}");
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
            if (_sanityConfig.SanityStages == null || !_sanityConfig.SanityStages.Any())
            {
                Library_ExiledAPI.LogWarn("PlayerSanityHandler.GetCurrentSanityStage", $"Instance ID={_instanceId}, SanityStages is null or empty");
                return null;
            }
            Library_ExiledAPI.LogDebug("PlayerSanityHandler.GetCurrentSanityStage", $"Instance ID={_instanceId}, SanityStages: {string.Join(", ", _sanityConfig.SanityStages.Select(s => $"[Min={s.MinThreshold}, Max={s.MaxThreshold}, Damage={s.DamageOnStrike}, Effects={s.Effects?.Count}]"))}");
            var orderedStages = _sanityConfig.SanityStages.OrderByDescending(s => s.MaxThreshold);
            foreach (var s in orderedStages)
            {
                bool matches = sanity <= s.MaxThreshold && (sanity > s.MinThreshold || (sanity == 0 && s.MinThreshold == 0));
                Library_ExiledAPI.LogDebug("PlayerSanityHandler.GetCurrentSanityStage", $"Instance ID={_instanceId}, Checking stage [Min={s.MinThreshold}, Max={s.MaxThreshold}]: sanity={sanity}, matches={matches}");
                if (matches) return s;
            }
            Library_ExiledAPI.LogWarn("PlayerSanityHandler.GetCurrentSanityStage", $"Instance ID={_instanceId}, No stage found for sanity {sanity}");
            return null;
        }

        /// <summary>
        /// Gets the sanity stage configuration for a player.
        /// </summary>
        /// <param name="player">The player to evaluate.</param>
        /// <returns>The matching <see cref="PlayerSanityStageConfig"/> or null if none found.</returns>
        public PlayerSanityStageConfig GetCurrentSanityStage(Player player)
        {
            if (!IsValidPlayer(player))
            {
                Library_ExiledAPI.LogWarn("PlayerSanityHandler.GetCurrentSanityStage", $"Instance ID={_instanceId}, Invalid player: {player?.UserId ?? "null"} ({player?.Nickname ?? "null"})");
                return null;
            }
            string userId = NormalizeUserId(player.UserId);
            float sanity = GetCurrentSanity(player);
            Library_ExiledAPI.LogDebug("PlayerSanityHandler.GetCurrentSanityStage", $"Instance ID={_instanceId}, Player: {userId} ({player.Nickname ?? "null"}), Sanity: {sanity}");
            return GetCurrentSanityStage(sanity);
        }

        /// <summary>
        /// Applies status effects to a player based on their current sanity stage.
        /// </summary>
        /// <param name="player">The player to apply effects to.</param>
        public void ApplyStageEffects(Player player)
        {
            if (!IsValidPlayer(player))
            {
                Library_ExiledAPI.LogWarn("PlayerSanityHandler.ApplyStageEffects", $"Instance ID={_instanceId}, Invalid player: {player?.UserId ?? "null"} ({player?.Nickname ?? "null"})");
                return;
            }
            string userId = NormalizeUserId(player.UserId);
            string nickname = "Unknown";
            bool isAlive = false;
            bool isHuman = false;
            try
            {
                nickname = player.Nickname ?? "null";
                isAlive = player.IsAlive;
                isHuman = player.IsHuman;
                Library_ExiledAPI.LogDebug("PlayerSanityHandler.ApplyStageEffects", $"Instance ID={_instanceId}, Starting ApplyStageEffects for {userId} ({nickname}), IsAlive={isAlive}, IsHuman={isHuman}");
            }
            catch (Exception ex)
            {
                Library_ExiledAPI.LogWarn("PlayerSanityHandler.ApplyStageEffects", $"Instance ID={_instanceId}, Failed to access player properties for {userId}: {ex.Message}");
                return;
            }
            try
            {
                float sanity = GetCurrentSanity(player);
                Library_ExiledAPI.LogDebug("PlayerSanityHandler.ApplyStageEffects", $"Instance ID={_instanceId}, Retrieved sanity {sanity} for {userId} ({nickname})");
                var stage = GetCurrentSanityStage(player);
                if (stage == null)
                {
                    Library_ExiledAPI.LogWarn("PlayerSanityHandler.ApplyStageEffects", $"Instance ID={_instanceId}, No stage found for {userId} ({nickname}), sanity: {sanity}, StackTrace: {Environment.StackTrace}");
                    return;
                }
                Library_ExiledAPI.LogDebug("PlayerSanityHandler.ApplyStageEffects", $"Instance ID={_instanceId}, Applying stage for {userId} ({nickname}), Sanity: {sanity}, Stage: Min={stage.MinThreshold}, Max={stage.MaxThreshold}, Damage={stage.DamageOnStrike}, Effects={stage.Effects?.Count ?? 0}, StackTrace: {Environment.StackTrace}");

                if (Helpers.IsHumanWithoutLight(player) && stage.DamageOnStrike > 0)
                {
                    try
                    {
                        Scp575DamageSystem.DamagePlayer(player, stage.DamageOnStrike);
                        Library_ExiledAPI.LogDebug("PlayerSanityHandler.ApplyStageEffects", $"Instance ID={_instanceId}, Applied damage {stage.DamageOnStrike} to {userId} ({nickname})");
                    }
                    catch (Exception ex)
                    {
                        Library_ExiledAPI.LogError("PlayerSanityHandler.ApplyStageEffects", $"Instance ID={_instanceId}, Failed to apply damage to {userId} ({nickname}): {ex.Message}, StackTrace: {ex.StackTrace}");
                    }
                }

                if (stage.Effects != null)
                {
                    foreach (var effectConfig in stage.Effects)
                    {
                        try
                        {
                            ApplyEffect(player, effectConfig.EffectType, effectConfig.Intensity, effectConfig.Duration);
                            Library_ExiledAPI.LogDebug("PlayerSanityHandler.ApplyStageEffects", $"Instance ID={_instanceId}, Applied effect {effectConfig.EffectType} to {userId} ({nickname}) with intensity {effectConfig.Intensity}, duration {effectConfig.Duration}");
                        }
                        catch (Exception ex)
                        {
                            Library_ExiledAPI.LogWarn("PlayerSanityHandler.ApplyStageEffects", $"Instance ID={_instanceId}, Failed to apply effect {effectConfig.EffectType} to {userId} ({nickname}): {ex.Message}, StackTrace: {ex.StackTrace}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Library_ExiledAPI.LogError("PlayerSanityHandler.ApplyStageEffects", $"Instance ID={_instanceId}, Unexpected error for {userId} ({nickname}): {ex.Message}, StackTrace: {ex.StackTrace}");
            }
        }

        #endregion

        #region Sanity Decay

        /// <summary>
        /// Periodically reduces sanity for eligible players based on game conditions (e.g., darkness, blackout).
        /// </summary>
        /// <returns>An enumerator for the MEC coroutine system.</returns>
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
                foreach (var player in Player.ReadyList.Where(p => IsPlayerValidForSanitySystem(p)))
                {
                    if (!Library_LabAPI.IsPlayerInDarkRoom(player)) continue;

                    try
                    {
                        float decayRate = CalculateDecayRate(player);
                        float newSanity = ChangeSanityValue(player, -decayRate);

                        if (_plugin.Config.HintsConfig.IsEnabledSanityHint && ShouldSendHint(player.UserId))
                        {
                            SendSanityHint(player, _plugin.Config.HintsConfig.SanityDecreasedHint, newSanity);
                            _lastHintTime[NormalizeUserId(player.UserId)] = DateTime.Now;
                        }
                    }
                    catch (Exception ex)
                    {
                        Library_ExiledAPI.LogError("PlayerSanityHandler.HandleSanityDecay", $"Instance ID={_instanceId}, Failed to process sanity decay for {player?.UserId ?? "null"} ({player?.Nickname ?? "null"}): {ex.Message}, StackTrace: {ex.StackTrace}");
                    }
                }
            }
        }

        /// <summary>
        /// Calculates the rate at which a player's sanity should decay based on conditions.
        /// </summary>
        /// <param name="player">The player to calculate the decay rate for.</param>
        /// <returns>The calculated decay rate.</returns>
        private float CalculateDecayRate(Player player)
        {
            float decayRate = _sanityConfig.DecayRateBase;
            if (_plugin.Npc?.Methods?.IsBlackoutActive == true)
                decayRate *= _sanityConfig.DecayMultiplierBlackout;
            if (Library_LabAPI.IsPlayerInDarkRoom(player) && Helpers.IsHumanWithoutLight(player))
                decayRate *= _sanityConfig.DecayMultiplierDarkness;
            return decayRate;
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Validates if a player is eligible for sanity mechanics.
        /// </summary>
        /// <param name="player">The player to validate.</param>
        /// <returns>True if the player is valid, false otherwise.</returns>
        public bool IsValidPlayer(Player player)
        {
            if (player == null || player.UserId == null)
            {
                Library_ExiledAPI.LogDebug("PlayerSanityHandler.IsValidPlayer", $"Instance ID={_instanceId}, Player or UserId is null.");
                return false;
            }
            try
            {
                bool isValid = player.IsAlive && player.IsHuman && player.Nickname != null;
                Library_ExiledAPI.LogDebug("PlayerSanityHandler.IsValidPlayer", $"Instance ID={_instanceId}, Player: {player.UserId} ({player.Nickname}), IsAlive={player.IsAlive}, IsHuman={player.IsHuman}, Nickname={(player.Nickname != null ? "non-null" : "null")}, IsValid={isValid}");
                return isValid;
            }
            catch (Exception ex)
            {
                Library_ExiledAPI.LogWarn("PlayerSanityHandler.IsValidPlayer", $"Instance ID={_instanceId}, Error validating player {player.UserId} ({player.Nickname ?? "null"}): {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Checks if a player is valid for the sanity system (alive and human).
        /// </summary>
        /// <param name="player">The player to check.</param>
        /// <returns>True if the player is valid for the sanity system, false otherwise.</returns>
        private bool IsPlayerValidForSanitySystem(Player player)
        {
            return IsValidPlayer(player);
        }

        /// <summary>
        /// Normalizes the UserId to ensure consistent caching.
        /// </summary>
        /// <param name="userId">The UserId to normalize.</param>
        /// <returns>The normalized UserId.</returns>
        private string NormalizeUserId(string userId)
        {
            try
            {
                string normalized = userId?.ToLowerInvariant() ?? throw new ArgumentNullException(nameof(userId), "UserId cannot be null.");
                Library_ExiledAPI.LogDebug("PlayerSanityHandler.NormalizeUserId", $"Instance ID={_instanceId}, Normalized UserId: {normalized}");
                return normalized;
            }
            catch (Exception ex)
            {
                Library_ExiledAPI.LogError("PlayerSanityHandler.NormalizeUserId", $"Instance ID={_instanceId}, Failed to normalize UserId {userId ?? "null"}: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Determines the amount of sanity restored by using a specific item.
        /// </summary>
        /// <param name="itemType">The type of item used.</param>
        /// <returns>The amount of sanity to restore.</returns>
        private float GetItemRestoreAmount(ItemType itemType)
        {
            return itemType switch
            {
                ItemType.SCP500 => UnityEngine.Random.Range(_sanityConfig.Scp500RestoreMin, _sanityConfig.Scp500RestoreMax),
                ItemType.Painkillers => UnityEngine.Random.Range(_sanityConfig.PillsRestoreMin, _sanityConfig.PillsRestoreMax),
                _ => 0f
            };
        }

        /// <summary>
        /// Checks if a hint should be sent based on the cooldown.
        /// </summary>
        /// <param name="userId">The player's UserId.</param>
        /// <returns>True if a hint can be sent, false otherwise.</returns>
        private bool ShouldSendHint(string userId)
        {
            string normalizedUserId = NormalizeUserId(userId);
            return !_lastHintTime.TryGetValue(normalizedUserId, out var lastTime) ||
                   (DateTime.Now - lastTime).TotalSeconds >= _hintCooldown;
        }

        /// <summary>
        /// Sends a hint to the player with their current sanity value.
        /// </summary>
        /// <param name="player">The player to send the hint to.</param>
        /// <param name="hintMessage">The hint message to display.</param>
        /// <param name="sanity">The current sanity value.</param>
        private void SendSanityHint(Player player, string hintMessage, float sanity)
        {
            try
            {
                player.SendHint(hintMessage, new[] { new FloatHintParameter(sanity, "F1") });
            }
            catch (Exception ex)
            {
                Library_ExiledAPI.LogWarn("PlayerSanityHandler.SendSanityHint", $"Instance ID={_instanceId}, Failed to send hint to {player?.UserId ?? "null"} ({player?.Nickname ?? "null"}): {ex.Message}");
            }
        }

        /// <summary>
        /// Applies a specific status effect to a player.
        /// </summary>
        /// <param name="player">The player to apply the effect to.</param>
        /// <param name="effectType">The type of effect to apply.</param>
        /// <param name="intensity">The intensity of the effect.</param>
        /// <param name="duration">The duration of the effect.</param>
        private static void ApplyEffect(Player player, SanityEffectType effectType, byte intensity, float duration)
        {
            try
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
            catch (Exception ex)
            {
                Library_ExiledAPI.LogError("PlayerSanityHandler.ApplyEffect", $"Failed to apply effect {effectType} to {player?.UserId ?? "null"} ({player?.Nickname ?? "null"}): {ex.Message}, StackTrace: {ex.StackTrace}");
                throw;
            }
        }

        #endregion
    }
}