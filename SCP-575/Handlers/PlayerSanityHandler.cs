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

    // TODO IF Plugin.Singleton.Config.NpcConfig.Sanity.IsEnabled
    public class PlayerSanityHandler : CustomEventsHandler, IDisposable
    {
        private readonly Plugin _plugin;
        private readonly PlayerSanityConfig _sanityConfig;

        private readonly Dictionary<string, float> _sanityCache = new();
        private readonly CoroutineHandle _sanityDecayCoroutine;

        public PlayerSanityHandler(Plugin plugin)
        {
            _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin), "Plugin instance cannot be null.");
            if (_plugin.Config == null)
                throw new InvalidOperationException("Config is not initialized.");

            _sanityConfig = Plugin.Singleton.Config.SanityConfig;

            _sanityDecayCoroutine = Timing.RunCoroutine(HandleSanityDecay());
        }

        /// <summary>
        /// Stores and retrieves sanity values associated with players UserId.
        /// </summary>
        public Dictionary<string, float> SanityCache => _sanityCache;


        /// <summary>
        /// Sets the sanity value for the specified player.  
        /// Overwrites existing value or adds a new entry.
        /// </summary>
        public void SetSanityForPlayer(Player player, float sanity)
        {
            _sanityCache[player.UserId] = Mathf.Clamp(sanity, 0f, 100f);
        }


        #region Event Handlers

        public override void OnPlayerSpawned(PlayerSpawnedEventArgs ev)
        {
            base.OnPlayerSpawned(ev);
            if (ev?.Player == null || string.IsNullOrEmpty(ev.Player.UserId))
            {
                Library_ExiledAPI.LogDebug("PlayerSanityHandler.OnPlayerSpawned", "Player or UserId is null. Skipping.");
                return;
            }
            _sanityCache[ev.Player.UserId] = Plugin.Singleton.Config.SanityConfig.InitialSanity;

        }

        public override void OnPlayerUsedItem(PlayerUsedItemEventArgs ev)
        {
            base.OnPlayerUsedItem(ev);

            if (ev?.Player == null || string.IsNullOrEmpty(ev.Player.UserId))
            {
                Library_ExiledAPI.LogDebug("OnPlayerUsedItem.OnPlayerUsedItem", "Player or UserId is null. Skipping.");
                return;
            }

            if (!IsPlayerValidForSanitySystem(ev.Player))
            {
                Library_ExiledAPI.LogDebug("OnPlayerUsedItem.OnPlayerUsedItem", "Player is not valid to apply sanity mechanics");
                return;
            }

            if (ev.UsableItem != null)
            {
                Player player = ev.Player;
                ItemType itemType = ev.UsableItem.Type;
                string userId = player.UserId;

                float restoreAmount = 0f;
                var _sanityConfig = Plugin.Singleton.Config.SanityConfig;
                switch (itemType)
                {
                    case ItemType.SCP500:
                        restoreAmount = UnityEngine.Random.Range(_sanityConfig.SCP500RestoreMin, _sanityConfig.SCP500RestoreMax);
                        break;

                    case ItemType.Painkillers:
                        restoreAmount = UnityEngine.Random.Range(_sanityConfig.PillsRestoreMin, _sanityConfig.PillsRestoreMax);
                        break;

                    default:
                        return;
                }

                // Apply sanity restoration
                float newValue = ChangeSanityValue(player, restoreAmount);
                if (Plugin.Singleton.Config.HintsConfig.IsEnabledSanityHint)
                {
                    player.SendHint(Plugin.Singleton.Config.HintsConfig.SanityIncreasedHint,
                        new[] { new FloatHintParameter(newValue, "F1") });
                }

                Library_ExiledAPI.LogDebug("SanitySystem", $"Restored {restoreAmount} sanity to {userId} with {itemType}");


            }
        }

        #endregion

        #region Public Methods
        /// <summary>
        /// Gets the sanity value for the specified player.
        /// Returns 100f if the player has no entry yet.
        /// </summary>
        public float GetCurrentSanityOfPlayer(Player player)
        {
            return _sanityCache.TryGetValue(player.UserId, out float sanity) ? sanity : 100f;
        }

        public PlayerSanityStageConfig GetCurrentSanityStage(Player player)
        {
            float sanity = GetCurrentSanityOfPlayer(player);
            return Plugin.Singleton.Config.SanityConfig.SanityStages.FirstOrDefault(stage =>
                sanity <= stage.MaxThreshold && sanity > stage.MinThreshold);
        }

        public PlayerSanityStageConfig GetCurrentSanityStage(float sanity)
        {
            return Plugin.Singleton.Config.SanityConfig.SanityStages.FirstOrDefault(stage =>
                sanity <= stage.MaxThreshold && sanity > stage.MinThreshold);
        }

        public float ChangeSanityValue(Player player, float sanityChangeAmount)
        {
            float cachedSanity = GetCurrentSanityOfPlayer(player);
            float updatedSanity = cachedSanity + sanityChangeAmount;
            SetSanityForPlayer(player, Mathf.Clamp(updatedSanity, 0f, 100f));
            // TODO send hint to the player
            Library_ExiledAPI.LogDebug("PlayerSanityHandler.ChangeSanityValue", $"Applied new sanity value for player {player.UserId}. New value = {_sanityCache[player.UserId]}, tried to change sanity by {sanityChangeAmount}");
            return updatedSanity;
        }

        // TODO
        public IEnumerator<float> HandleSanityDecay()
        {
            while (true)
            {
                yield return Timing.WaitForSeconds(1f);

                foreach (Player player in Player.List.Where(p => p.IsAlive))
                {
                    float decayRate = _sanityConfig.DecayRateBase;

                    if (Plugin.Singleton.Npc.Methods.IsBlackoutActive) decayRate *= _sanityConfig.DecayMultiplierBlackout;
                    if (Library_LabAPI.IsPlayerInDarkRoom(player)) decayRate *= _sanityConfig.DecayMultiplierDarkness;
                    float newValue = ChangeSanityValue(player, -1 * decayRate);
                    if (Plugin.Singleton.Config.HintsConfig.IsEnabledSanityHint)
                    {
                        player.SendHint(Plugin.Singleton.Config.HintsConfig.SanityDecreasedHint,
                            new[] { new FloatHintParameter(newValue, "F1") });
                    }
                }
            }
        }

        /// <summary>  
        /// Applies the configured sanity stage effects to the specified player.  
        /// </summary>  
        /// <param name="player">The player to apply effects to.</param>  
        public void ApplyStageEffects(Player player)
        {
            PlayerSanityStageConfig stage = GetCurrentSanityStage(player);
            if (player == null || stage == null) return;

            // Get the EnableEffect method with the correct signature  
            var enableEffectMethod = typeof(Player).GetMethod("EnableEffect", new[] { typeof(byte), typeof(float), typeof(bool) });

            foreach (var effectConfig in stage.Effects)
            {
                try
                {
                    // Make the method generic with the effect type  
                    var genericMethod = enableEffectMethod.MakeGenericMethod(effectConfig.EffectType);

                    // Invoke with the configuration values  
                    genericMethod.Invoke(player, new object[] {
                    effectConfig.Intensity,
                    effectConfig.Duration,
                    false 
                    });
                }
                catch (Exception ex)
                {
                    Library_ExiledAPI.LogWarn("PlayerSanityHandler.ApplyStageEffects", $"Failed to apply effect {effectConfig.EffectType.Name}: {ex.Message}");
                }
            }
        }

        #endregion

        #region Private Methods

        private bool IsPlayerValidForSanitySystem(Player player)
        {
            if (player == null || string.IsNullOrEmpty(player.UserId))
            {
                Library_ExiledAPI.LogDebug("PlayerSanityHandler.IsPlayerValidForSanitySystem", "Player or UserId is null.");
                return false;
            }

            if (!player.IsAlive || !player.IsHuman)
            {
                Library_ExiledAPI.LogDebug("PlayerSanityHandler.IsPlayerValidForSanitySystem", $"Player is not valid for sanity mechanics. IsAlive = {player.IsAlive}, IsHuman = {player.IsHuman}");
                return false;
            }

            return true;
        }


        #endregion

        #region Cleaning Up
        private void CleanAllocatedResources()
        {
            _sanityCache.Clear();

        }


        /// <summary>
        /// Cleans up all resources, including coroutines and cancellation tokens, on disposal.
        /// </summary>
        public void Dispose()
        {
            try
            {
                CleanAllocatedResources();
                Timing.KillCoroutines(_sanityDecayCoroutine);

                Library_ExiledAPI.LogInfo("LightCooldownHandler.Dispose", "Disposed light cooldown handler and cleaned up resources.");
            }
            catch (Exception ex)
            {
                Library_ExiledAPI.LogError("LightCooldownHandler.Dispose", $"Failed to dispose light cooldown handler: {ex.Message}\nStackTrace: {ex.StackTrace}");
            }
        }

        #endregion
    }
}
