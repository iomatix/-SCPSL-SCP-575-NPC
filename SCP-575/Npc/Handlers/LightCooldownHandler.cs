namespace SCP_575.Npc
{
    using InventorySystem.Items.ToggleableLights;
    using LabApi.Events.Arguments.PlayerEvents;
    using LabApi.Events.CustomHandlers;
    using LabApi.Features.Wrappers;
    using SCP_575.Shared;
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using Utils.Networking;

    /// <summary>
    /// Handles flashlight and weapon light cooldowns, flickering effects, and disabling light sources when SCP-575 attacks.
    /// </summary>
    public class LightCooldownHandler : CustomEventsHandler
    {
        private readonly Dictionary<string, DateTime> _playerCooldowns = new();
        private readonly Dictionary<string, CancellationTokenSource> _flickerTokens = new();
        private readonly Dictionary<string, CancellationTokenSource> _weaponFlickerTokens = new();
        private readonly Random _random = new();

        private readonly TimeSpan _cooldownDuration = TimeSpan.FromSeconds(Library_LabAPI.NpcConfig.KeterLightsourceCooldown);

        public override void OnPlayerTogglingFlashlight(PlayerTogglingFlashlightEventArgs ev)
        {
            bool isAllowed = ev.IsAllowed;
            HandleLightToggling(ev.Player, ref isAllowed, "Light source on cooldown!");
            ev.IsAllowed = isAllowed;
        }

        public override void OnPlayerTogglingWeaponFlashlight(PlayerTogglingWeaponFlashlightEventArgs ev)
        {
            bool isAllowed = ev.IsAllowed;
            HandleLightToggling(ev.Player, ref isAllowed, "Light source on cooldown!");
            ev.IsAllowed = isAllowed;
        }

        public override void OnPlayerToggledFlashlight(PlayerToggledFlashlightEventArgs ev)
        {
            if (ev.NewState)
                _ = StartFlickerEffectAsync(ev.LightItem, ev.Player.UserId);
        }

        public override void OnPlayerToggledWeaponFlashlight(PlayerToggledWeaponFlashlightEventArgs ev)
        {
            if (ev.NewState)
                _ = StartWeaponFlickerEffectAsync(ev.FirearmItem, ev.Player.UserId);
        }

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
                Library_ExiledAPI.LogWarn(nameof(StartFlickerEffectAsync), $"Error in light emitter flicker effect: {ex.Message}");
            }
            finally
            {
                _flickerTokens.Remove(userId);
            }
        }

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
                        // Set the property via reflection  
                        prop.SetValue(firearmItem.Base, state);

                        // Send network message to sync with all clients
                        new FlashlightNetworkHandler.FlashlightMessage(firearmItem.Serial, state).SendToAuthenticated();
                    }

                    await Task.Delay(_random.Next(45, 251), cts.Token);
                }
            }
            catch (TaskCanceledException) { }
            catch (Exception ex)
            {
                Library_ExiledAPI.LogWarn(nameof(StartWeaponFlickerEffectAsync), $"Error in weapon flicker effect: {ex.Message}");
            }
            finally
            {
                _weaponFlickerTokens.Remove(userId);
                cts.Dispose();
            }
        }

        /// <summary>
        /// Called when SCP-575 attacks a player. Disables their light source and enforces cooldown.
        /// </summary>
        /// <param name="target">The player being attacked.</param>
        public void OnScp575AttacksPlayer(Player target)
        {
            if (target.CurrentItem is LightItem lightItem)
            {
                lightItem.IsEmitting = false;
                _ = StartFlickerEffectAsync(lightItem, target.UserId);
            }

            else if (target.CurrentItem is FirearmItem weapon)
            {
                var prop = weapon.Base.GetType().GetProperty("IsEmittingLight", BindingFlags.NonPublic | BindingFlags.Instance);
                if (prop?.CanWrite == true)
                {
                    prop.SetValue(weapon.Base, false);
                    new FlashlightNetworkHandler.FlashlightMessage(weapon.Serial, false).SendToAuthenticated();
                }
                _ = StartWeaponFlickerEffectAsync(weapon, target.UserId);
            }

            ForceCooldown(target);
        }

        /// <summary>
        /// Forces cooldown on a player's flashlight/weapon.
        /// </summary>
        public void ForceCooldown(Player player)
        {
            _playerCooldowns[player.UserId] = DateTime.Now;
            player.SendHint("Your light has been disabled!", 2);
        }

        /// <summary>
        /// Cleans up expired cooldowns to avoid memory leaks.
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
