namespace SCP_575.Npc.Handlers
{
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
        private readonly Dictionary<string, float> _sanityCache = new();
        private readonly Dictionary<string, float> _sanityRecoveryItemCooldowns = new();

        public PlayerSanityHandler(Plugin plugin)
        {
            _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin), "Plugin instance cannot be null.");
            if (_plugin.Config.NpcConfig == null)
                throw new InvalidOperationException("NpcConfig is not initialized.");

            if (!Plugin.Singleton.Config.NpcConfig.EnableKeterLightsourceCooldown)
            {
                Library_ExiledAPI.LogInfo("PlayerSanityHandler.Constructor", "EnableKeterLightsourceCooldown is disabled in config.");
                return;
            }
        }

        /// <summary>
        /// Stores and retrieves sanity values associated with players UserId.
        /// </summary>
        public Dictionary<string, float> SanityCache => _sanityCache;


        /// <summary>
        /// Sets the sanity value for the specified player.  
        /// Overwrites existing value or adds a new entry.
        /// </summary>
        public void SetSanityForPlayer(LabApi.Features.Wrappers.Player player, float sanity)
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
            _sanityCache[ev.Player.UserId] = Plugin.Singleton.Config.NpcConfig.SanityConfig.InitialSanity;

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

                // Check cooldown
                if (_sanityRecoveryItemCooldowns.TryGetValue(userId, out float lastUseTime))
                {
                    float timeSinceUse = Time.time - lastUseTime;
                    // Todo move sanity config to main config ? its not part of the NPC tho
                    if (timeSinceUse < Plugin.Singleton.Config.NpcConfig.SanityConfig.RegenCooldown)
                    {
                        Library_ExiledAPI.LogDebug("SanitySystem", $"Sanity recovery item on cooldown for {userId}. Sanity has not been recovered.");
                        return;
                    }
                }

                float restoreAmount = 0f;
                var config = Plugin.Singleton.Config.NpcConfig.SanityConfig;
                switch (itemType)
                {
                    case ItemType.SCP500:
                        restoreAmount = UnityEngine.Random.Range(config.SCP500RestoreMin, config.SCP500RestoreMax);
                        break;

                    case ItemType.Painkillers:
                        restoreAmount = UnityEngine.Random.Range(config.PillsRestoreMin, config.PillsRestoreMax);
                        break;

                    default:
                        return;
                }

                // Apply sanity restoration
                ChangeSanityValue(player, restoreAmount);
                _sanityRecoveryItemCooldowns[userId] = Time.time;

                Library_ExiledAPI.LogDebug("SanitySystem", $"Restored {restoreAmount}% sanity to {userId} with {itemType}");


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

        public SanityStage GetCurrentSanityStage(Player player)
        {
            float sanity = GetCurrentSanityOfPlayer(player);
            return Plugin.Singleton.Config.NpcConfig.SanityConfig.SanityStages.FirstOrDefault(stage =>
                sanity <= stage.MaxThreshold && sanity > stage.MinThreshold);
        }

        public SanityStage GetCurrentSanityStage(float sanity)
        {
            return Plugin.Singleton.Config.NpcConfig.SanityConfig.SanityStages.FirstOrDefault(stage =>
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
            var _config = Plugin.Singleton.Config.NpcConfig.SanityConfig;
            while (true)
            {
                yield return Timing.WaitForSeconds(1f);

                foreach (Player player in Player.List.Where(p => p.IsAlive))
                {
                    float decayRate = _config.DecayRateBase;

                    if (Plugin.Singleton.Npc.Methods.IsBlackoutActive)
                        decayRate *= _config.DecayMultiplierBlackout;

                    if (!player) // TODO reusable utility method if player has flashlight turned on/off inc weapon module 
                        decayRate *= _config.DecayMultiplierDarkness;
                    // TODO send hint to the player config
                    ChangeSanityValue(player, -1 * decayRate);
                }
            }
        }

        /// <summary>
        /// Applies the configured sanity stage effects to the specified player.
        /// </summary>
        /// <param name="player">The player to apply effects to.</param>
        /// <param name="stage">The sanity stage defining the effects.</param>
        public void ApplyStageEffects(Player player, SanityStage stage)
        {
            if (player == null || stage == null)
                return;

            var effects = new List<(bool Enabled, Action Apply)>
    {
        (stage.EnableWhispers,           () => Plugin.Singleton.AudioManager.PlayAudioAutoManaged(player, AudioKey.Whispers, true, 10f)),
        (stage.EnableScreenShake,        () => player.EnableEffect<Ensnared>(duration: 0.2f)), // Simulate shake with movement restrict
        (stage.EnableAudioDistortion,    () => player.EnableEffect<Deafened>(duration: 1.5f)),
        (stage.EnableHallucinations,     () => player.EnableEffect<AmnesiaVision>(duration: 2.5f)),
        (stage.EnableCameraDistortion,   () => player.EnableEffect<Concussed>(duration: 1.75f)),
        (stage.EnableMovementLag,        () => player.EnableEffect<Exhausted>(duration: 2f)),
        (stage.EnablePanicFlash,         () => player.EnableEffect<Flashed>(duration: 0.2f))
    };

            foreach (var (enabled, apply) in effects)
            {
                if (enabled)
                    apply();
            }
        }



        #endregion

        #region Private Methods

        private bool IsPlayerValidForSanitySystem(LabApi.Features.Wrappers.Player player)
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
                Timing.KillCoroutines(_cleanupCoroutine);

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
