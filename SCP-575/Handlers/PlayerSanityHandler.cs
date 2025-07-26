namespace SCP_575.Handlers
{
    using Hints;
    using InventorySystem.Items;
    using LabApi.Events.Arguments.PlayerEvents;
    using LabApi.Events.CustomHandlers;
    using LabApi.Features.Wrappers;
    using MEC;
    using SCP_575.ConfigObjects;
    using SCP_575.Shared;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEngine;
    using CustomPlayerEffects;

    /// <summary>
    /// Handles player sanity mechanics such as tracking sanity values, applying decay over time,
    /// and triggering effects based on configured sanity thresholds.
    /// </summary>
    public class PlayerSanityHandler : CustomEventsHandler, IDisposable
    {
        private readonly Plugin _plugin;
        private readonly PlayerSanityConfig _sanityConfig;
        private readonly Dictionary<string, float> _sanityCache = new();
        private CoroutineHandle _sanityDecayCoroutine;
        private bool _isDisposed = false;

        /// <summary>
        /// Gets the internal sanity cache mapping UserId to current sanity values.
        /// </summary>
        public Dictionary<string, float> SanityCache => _sanityCache;

        /// <summary>
        /// Initializes a new instance of the <see cref="PlayerSanityHandler"/> class.
        /// </summary>
        /// <param name="plugin">Reference to the main <see cref="Plugin"/> instance.</param>
        /// <exception cref="ArgumentNullException">Thrown if the plugin instance is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the sanity configuration is not initialized.</exception>
        public PlayerSanityHandler(Plugin plugin)
        {
            _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin), "Plugin instance cannot be null.");
            if (_plugin.Config?.SanityConfig == null)
                throw new InvalidOperationException("SanityConfig is not initialized.");

            _sanityConfig = _plugin.Config.SanityConfig;
        }

        /// <summary>
        /// Initializes the handler and starts the sanity decay coroutine.
        /// Call this method after registering the handler.
        /// </summary>
        public void Initialize()
        {
            if (!_isDisposed && _sanityDecayCoroutine.IsRunning == false)
            {
                _sanityDecayCoroutine = Timing.RunCoroutine(HandleSanityDecay());
            }
        }

        /// <summary>
        /// Sets the sanity value for a specific player.
        /// </summary>
        /// <param name="player">The target player.</param>
        /// <param name="sanity">The sanity value to assign, clamped between 0 and 100.</param>
        public void SetSanityForPlayer(Player player, float sanity)
        {
            if (player?.UserId == null) return;
            _sanityCache[player.UserId] = Mathf.Clamp(sanity, 0f, 100f);
        }

        #region Event Handlers  

        /// <summary>
        /// Handles logic when a player spawns. Initializes their sanity to the configured starting value.
        /// </summary>
        /// <param name="ev">Event arguments containing the player reference.</param>
        public override void OnPlayerSpawned(PlayerSpawnedEventArgs ev)
        {
            base.OnPlayerSpawned(ev);
            if (ev?.Player?.UserId == null)
            {
                Library_ExiledAPI.LogDebug("PlayerSanityHandler.OnPlayerSpawned", "Player or UserId is null. Skipping.");
                return;
            }
            _sanityCache[ev.Player.UserId] = _sanityConfig.InitialSanity;
        }

        /// <summary>
        /// Handles logic when a player uses an item that may restore sanity (e.g., SCP-500 or Painkillers).
        /// </summary>
        /// <param name="ev">Event arguments containing item and player information.</param>
        public override void OnPlayerUsedItem(PlayerUsedItemEventArgs ev)
        {
            base.OnPlayerUsedItem(ev);

            if (ev?.Player?.UserId == null)
            {
                Library_ExiledAPI.LogDebug("PlayerSanityHandler.OnPlayerUsedItem", "Player or UserId is null. Skipping.");
                return;
            }

            if (!IsPlayerValidForSanitySystem(ev.Player))
            {
                Library_ExiledAPI.LogDebug("PlayerSanityHandler.OnPlayerUsedItem", "Player is not valid for sanity mechanics");
                return;
            }

            if (ev.UsableItem?.Type == null) return;

            float restoreAmount = ev.UsableItem.Type switch
            {
                ItemType.SCP500 => UnityEngine.Random.Range(_sanityConfig.SCP500RestoreMin, _sanityConfig.SCP500RestoreMax),
                ItemType.Painkillers => UnityEngine.Random.Range(_sanityConfig.PillsRestoreMin, _sanityConfig.PillsRestoreMax),
                _ => 0f
            };

            if (restoreAmount <= 0f) return;

            float newValue = ChangeSanityValue(ev.Player, restoreAmount);
            if (_plugin.Config.HintsConfig.IsEnabledSanityHint)
            {
                ev.Player.SendHint(_plugin.Config.HintsConfig.SanityIncreasedHint,
                    new[] { new FloatHintParameter(newValue, "F1") });
            }

            Library_ExiledAPI.LogDebug("SanitySystem", $"Restored {restoreAmount} sanity to {ev.Player.UserId} with {ev.UsableItem.Type}");
        }

        #endregion

        #region Public Methods  

        /// <summary>
        /// Gets the current sanity value for the given player.
        /// </summary>
        /// <param name="player">The player to retrieve sanity for.</param>
        /// <returns>The current sanity value, or 100f if not cached.</returns>
        public float GetCurrentSanityOfPlayer(Player player)
        {
            if (player?.UserId == null) return 100f;
            return _sanityCache.TryGetValue(player.UserId, out float sanity) ? sanity : 100f;
        }

        /// <summary>
        /// Returns the current sanity stage configuration for a player.
        /// </summary>
        /// <param name="player">The player to evaluate.</param>
        /// <returns>The matched <see cref="PlayerSanityStageConfig"/> or null if unmatched.</returns>
        public PlayerSanityStageConfig GetCurrentSanityStage(Player player)
        {
            if (player == null) return null;
            float sanity = GetCurrentSanityOfPlayer(player);
            return GetCurrentSanityStage(sanity);
        }

        /// <summary>
        /// Returns the sanity stage configuration that corresponds to a given sanity value.
        /// </summary>
        /// <param name="sanity">The sanity value to evaluate.</param>
        /// <returns>The matched <see cref="PlayerSanityStageConfig"/> or null if unmatched.</returns>
        public PlayerSanityStageConfig GetCurrentSanityStage(float sanity)
        {
            return _sanityConfig.SanityStages?.FirstOrDefault(stage =>
                sanity <= stage.MaxThreshold && sanity > stage.MinThreshold);
        }

        /// <summary>
        /// Modifies a player’s sanity by a given delta value.
        /// </summary>
        /// <param name="player">The player whose sanity will be modified.</param>
        /// <param name="sanityChangeAmount">The amount to change sanity by (can be negative).</param>
        /// <returns>The updated sanity value.</returns>
        public float ChangeSanityValue(Player player, float sanityChangeAmount)
        {
            if (player?.UserId == null) return 0f;

            float cachedSanity = GetCurrentSanityOfPlayer(player);
            float updatedSanity = cachedSanity + sanityChangeAmount;
            SetSanityForPlayer(player, updatedSanity);

            Library_ExiledAPI.LogDebug("PlayerSanityHandler.ChangeSanityValue",
                $"Applied new sanity value for player {player.UserId}. New value = {_sanityCache[player.UserId]}, tried to change sanity by {sanityChangeAmount}");

            return updatedSanity;
        }

        /// <summary>
        /// Coroutine that periodically reduces sanity for eligible players based on game conditions (e.g., darkness, blackout).
        /// </summary>
        /// <returns>An enumerator to be run by MEC's coroutine system.</returns>
        public IEnumerator<float> HandleSanityDecay()
        {
            while (!_isDisposed)
            {
                yield return Timing.WaitForSeconds(1f);

                if (_isDisposed) yield break;

                foreach (Player player in Player.List.Where(p => p?.IsAlive == true))
                {
                    if (!IsPlayerValidForSanitySystem(player)) continue;

                    float decayRate = _sanityConfig.DecayRateBase;

                    if (_plugin.Npc?.Methods?.IsBlackoutActive == true)
                        decayRate *= _sanityConfig.DecayMultiplierBlackout;
                    if (Library_LabAPI.IsPlayerInDarkRoom(player))
                        decayRate *= _sanityConfig.DecayMultiplierDarkness;

                    float newValue = ChangeSanityValue(player, -decayRate);

                    if (_plugin.Config.HintsConfig.IsEnabledSanityHint)
                    {
                        player.SendHint(_plugin.Config.HintsConfig.SanityDecreasedHint,
                            new[] { new FloatHintParameter(newValue, "F1") });
                    }
                }
            }
        }

        /// <summary>
        /// Applies status effects to a player based on their current sanity stage.
        /// </summary>
        /// <param name="player">The player to apply effects to.</param>
        public void ApplyStageEffects(Player player)
        {
            if (player == null) return;

            PlayerSanityStageConfig stage = GetCurrentSanityStage(player);
            if (stage?.Effects == null) return;

            foreach (var effectConfig in stage.Effects)
            {
                try
                {
                    // Use LabAPI's proper EnableEffect method signature  
                    if (effectConfig.EffectType.IsSubclassOf(typeof(StatusEffectBase)))
                    {
                        var method = typeof(Player).GetMethod("EnableEffect", new[] { typeof(byte), typeof(float), typeof(bool) })
                                                  ?.MakeGenericMethod(effectConfig.EffectType);

                        method?.Invoke(player, new object[] {
                            effectConfig.Intensity,
                            effectConfig.Duration,
                            false
                        });
                    }
                }
                catch (Exception ex)
                {
                    Library_ExiledAPI.LogWarn("PlayerSanityHandler.ApplyStageEffects",
                        $"Failed to apply effect {effectConfig.EffectType.Name}: {ex.Message}");
                }
            }
        }

        #endregion

        #region Private Methods  

        /// <summary>
        /// Determines whether the specified player is eligible for sanity mechanics (alive and human).
        /// </summary>
        /// <param name="player">The player to validate.</param>
        /// <returns>True if the player can be affected by the sanity system; otherwise, false.</returns>
        private bool IsPlayerValidForSanitySystem(Player player)

        {
            if (player?.UserId == null)
            {
                Library_ExiledAPI.LogDebug("PlayerSanityHandler.IsPlayerValidForSanitySystem", "Player or UserId is null.");
                return false;
            }

            if (!player.IsAlive || !player.IsHuman)
            {
                Library_ExiledAPI.LogDebug("PlayerSanityHandler.IsPlayerValidForSanitySystem",
                    $"Player is not valid for sanity mechanics. IsAlive = {player.IsAlive}, IsHuman = {player.IsHuman}");
                return false;
            }

            return true;
        }

        #endregion

        #region Disposal  

        private void CleanAllocatedResources()
        {
            _sanityCache.Clear();
        }

        public void Dispose()
        {
            if (_isDisposed) return;

            try
            {
                _isDisposed = true;
                CleanAllocatedResources();

                if (_sanityDecayCoroutine.IsRunning)
                    Timing.KillCoroutines(_sanityDecayCoroutine);

                Library_ExiledAPI.LogInfo("PlayerSanityHandler.Dispose", "Disposed PlayerSanityHandler and cleaned up resources.");
            }
            catch (Exception ex)
            {
                Library_ExiledAPI.LogError("PlayerSanityHandler.Dispose",
                    $"Failed to dispose PlayerSanityHandler: {ex.Message}\nStackTrace: {ex.StackTrace}");
            }
        }

        #endregion
    }
}