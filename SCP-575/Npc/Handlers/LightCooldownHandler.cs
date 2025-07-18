namespace SCP_575.Npc
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using InventorySystem.Items.ToggleableLights;
    using LabApi.Events.Arguments.PlayerEvents;
    using LabApi.Events.CustomHandlers;
    using LabApi.Features.Wrappers;
    using SCP_575.Shared;
    using Utils.Networking;

    /// <summary>
    /// Handles flashlight and weapon light toggling restrictions for players affected by SCP-575.
    /// Applies cooldowns, flickering effects, and forced light disables upon attack events.
    /// </summary>
    public class LightCooldownHandler : CustomEventsHandler
    {
        private readonly Dictionary<string, DateTime> _playerCooldowns = new();
        private readonly Dictionary<string, CancellationTokenSource> _flickerTokens = new();
        private readonly Dictionary<string, CancellationTokenSource> _weaponFlickerTokens = new();
        private readonly Random _random = new();

        private readonly TimeSpan _cooldownDuration = TimeSpan.FromSeconds(Library_LabAPI.NpcConfig.KeterLightsourceCooldown);

        /// <inheritdoc/>
        public override void OnPlayerTogglingFlashlight(PlayerTogglingFlashlightEventArgs ev)
        {
            bool isAllowed = ev.IsAllowed;
            HandleLightToggling(ev.Player, ref isAllowed, "Light source on cooldown!");
            ev.IsAllowed = isAllowed;
        }

        /// <inheritdoc/>
        public override void OnPlayerTogglingWeaponFlashlight(PlayerTogglingWeaponFlashlightEventArgs ev)
        {
            bool isAllowed = ev.IsAllowed;
            HandleLightToggling(ev.Player, ref isAllowed, "Light source on cooldown!");
            ev.IsAllowed = isAllowed;
        }

        /// <inheritdoc/>
        public override void OnPlayerToggledFlashlight(PlayerToggledFlashlightEventArgs ev)
        {
            if (ev.NewState)
                _ = StartFlickerEffectAsync(ev.LightItem, ev.Player.UserId);
        }

        /// <inheritdoc/>
        public override void OnPlayerToggledWeaponFlashlight(PlayerToggledWeaponFlashlightEventArgs ev)
        {
            if (ev.NewState)
                _ = StartWeaponFlickerEffectAsync(ev.FirearmItem, ev.Player.UserId);
        }

        /// <summary>
        /// Prevents flashlight toggling if the cooldown is still active.
        /// </summary>
        /// <param name="player">The player toggling the light.</param>
        /// <param name="isAllowed">Whether the action is allowed.</param>
        /// <param name="cooldownMessage">Message to display if the action is denied.</param>
        private void HandleLightToggling(Player player, ref bool isAllowed, string cooldownMessage)
        {
            if (_playerCooldowns.TryGetValue(player.UserId, out DateTime lastUse) &&
                DateTime.Now - lastUse < _cooldownDuration)
            {
                isAllowed = false;
                player.SendHint(cooldownMessage, 2);
                return;
            }

            _playerCooldowns[player.UserId] = DateTime.Now;
        }

        /// <summary>
        /// Triggers a flickering light effect for held flashlight items.
        /// </summary>
        /// <param name="lightItem">The flashlight item to manipulate.</param>
        /// <param name="userId">The user ID associated with the item.</param>
        private async Task StartFlickerEffectAsync(LightItem lightItem, string userId)
        {
            if (_flickerTokens.TryGetValue(userId, out var existingCts))
                existingCts.Cancel();

            using var cts = new CancellationTokenSource();
            _flickerTokens[userId] = cts;

            try
            {
                var pattern = new[] { true, false, true, false, true };

                foreach (bool state in pattern)
                {
                    if (lightItem?.Base == null || cts.Token.IsCancellationRequested)
                        break;

                    lightItem.IsEmitting = state;
                    await Task.Delay(_random.Next(45, 251), cts.Token);
                }
            }
            catch (TaskCanceledException) { }
            catch (Exception ex)
            {
                Library_ExiledAPI.LogWarn(nameof(StartFlickerEffectAsync), $"Error during flashlight flicker: {ex.Message}");
            }
            finally
            {
                _flickerTokens.Remove(userId);
            }
        }

        /// <summary>
        /// Triggers a flickering light effect for weapon flashlights using reflection.
        /// </summary>
        /// <param name="firearmItem">The firearm item containing the flashlight.</param>
        /// <param name="userId">The user ID associated with the weapon.</param>
        private async Task StartWeaponFlickerEffectAsync(FirearmItem firearmItem, string userId)
        {
            if (firearmItem?.Base == null || string.IsNullOrWhiteSpace(userId))
                return;

            if (_weaponFlickerTokens.TryGetValue(userId, out var existingCts))
            {
                existingCts.Cancel();
                existingCts.Dispose();
            }

            var cts = new CancellationTokenSource();
            _weaponFlickerTokens[userId] = cts;

            try
            {
                var pattern = new[] { true, false, true, false, true };

                foreach (bool state in pattern)
                {
                    if (firearmItem.Base == null || cts.Token.IsCancellationRequested)
                        break;

                    var prop = firearmItem.Base.GetType().GetProperty("IsEmittingLight", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (prop?.CanWrite == true)
                    {
                        prop.SetValue(firearmItem.Base, state);
                        new FlashlightNetworkHandler.FlashlightMessage(firearmItem.Serial, state).SendToAuthenticated();
                    }

                    await Task.Delay(_random.Next(45, 251), cts.Token);
                }
            }
            catch (TaskCanceledException) { }
            catch (Exception ex)
            {
                Library_ExiledAPI.LogWarn(nameof(StartWeaponFlickerEffectAsync), $"Error during weapon flashlight flicker: {ex.Message}");
            }
            finally
            {
                _weaponFlickerTokens.Remove(userId);
                cts.Dispose();
            }
        }

        /// <summary>
        /// Called when SCP-575 attacks a player.
        /// Disables light sources and applies cooldown logic.
        /// </summary>
        /// <param name="target">The player being attacked.</param>
        public void OnScp575AttacksPlayer(Player target)
        {
            switch (target.CurrentItem)
            {
                case LightItem lightItem:
                    lightItem.IsEmitting = false;
                    _ = StartFlickerEffectAsync(lightItem, target.UserId);
                    break;

                case FirearmItem firearm:
                    var prop = firearm.Base.GetType().GetProperty("IsEmittingLight", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (prop?.CanWrite == true)
                    {
                        prop.SetValue(firearm.Base, false);
                        new FlashlightNetworkHandler.FlashlightMessage(firearm.Serial, false).SendToAuthenticated();
                    }
                    _ = StartWeaponFlickerEffectAsync(firearm, target.UserId);
                    break;
            }

            ForceCooldown(target);
        }

        /// <summary>
        /// Immediately triggers the cooldown on the given player's light source.
        /// </summary>
        /// <param name="player">The affected player.</param>
        public void ForceCooldown(Player player)
        {
            _playerCooldowns[player.UserId] = DateTime.Now;
            player.SendHint("Your light has been disabled!", 2);
        }

        /// <summary>
        /// Periodically removes expired cooldowns to reduce memory usage.
        /// Should be invoked in a scheduled maintenance task or tick loop.
        /// </summary>
        public void CleanupOldCooldowns()
        {
            var cutoffTime = DateTime.Now - _cooldownDuration;
            var keysToRemove = new List<string>();

            foreach (var kvp in _playerCooldowns)
            {
                if (kvp.Value < cutoffTime)
                    keysToRemove.Add(kvp.Key);
            }

            foreach (var key in keysToRemove)
                _playerCooldowns.Remove(key);
        }
    }
}
