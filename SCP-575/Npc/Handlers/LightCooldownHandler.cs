namespace SCP_575.Npc  
{  
    using System;  
    using System.Collections.Generic;  
    using System.Threading.Tasks;  
    using LabApi.Events.CustomHandlers;  
    using LabApi.Events.Arguments.PlayerEvents;  
    using LabApi.Features.Wrappers;  
  
    public class LightCooldownHandler : CustomEventsHandler  
    {  
        private readonly Dictionary<string, DateTime> _playerCooldowns = new();  
        private readonly Random _random = new();  
  
        // TODO: Config value  
        private readonly TimeSpan _cooldownDuration = TimeSpan.FromSeconds(5);  
  
        public override void OnPlayerTogglingFlashlight(PlayerTogglingFlashlightEventArgs ev)  
        {  
            // Todo Config value
            HandleLightToggling(ev.Player, ref ev.IsAllowed, "Light source on cooldown!");  
        }  
  
        public override void OnPlayerTogglingWeaponFlashlight(PlayerTogglingWeaponFlashlightEventArgs ev)  
        {  
            // Todo config value
            HandleLightToggling(ev.Player, ref ev.IsAllowed, "Weapon light on cooldown!");  
        }  
  
        public override void OnPlayerToggledFlashlight(PlayerToggledFlashlightEventArgs ev)  
        {  
            if (ev.NewState) // Only flicker when turning on  
            {  
                _ = StartFlickerEffectAsync(ev.LightItem);  
            }  
        }  
  
        public override void OnPlayerToggledWeaponFlashlight(PlayerToggledWeaponFlashlightEventArgs ev)  
        {  
            if (ev.NewState) // Only flicker when turning on  
            {  
                // Get the weapon's flashlight attachment and create flicker effect  
                _ = StartWeaponFlickerEffectAsync(ev.FirearmItem);  
            }  
        }  
  
        private void HandleLightToggling(Player player, ref bool isAllowed, string cooldownMessage)  
        {  
            // Check if player has cooldown  
            if (_playerCooldowns.TryGetValue(player.UserId, out DateTime lastUse))  
            {  
                if (DateTime.Now - lastUse < _cooldownDuration)  
                {  
                    isAllowed = false;  
                    player.ShowHint(cooldownMessage, 2);  
                    return;  
                }  
            }  
  
            // Set cooldown  
            _playerCooldowns[player.UserId] = DateTime.Now;  
        }  
  
        private async Task StartFlickerEffectAsync(LightItem lightItem)  
        {  
            try  
            {  
                // Flicker pattern: on-off-on-off-on  
                var flickerPattern = new[] { true, false, true, false, true };  
  
                foreach (bool state in flickerPattern)  
                {  
                    if (lightItem?.Base == null) break; // Safety check  
  
                    lightItem.IsEmitting = state;  
                    int delay = _random.Next(45, 251);  
                    await Task.Delay(delay);  
                }  
            }  
            catch (Exception ex)  
            {  
                // Log error but don't crash the plugin  
                LabApi.Features.Console.Logger.Error($"Error in flicker effect: {ex.Message}");  
            }  
        }  
  
        private async Task StartWeaponFlickerEffectAsync(FirearmItem firearmItem)  
        {  
            try  
            {  
                // Flicker pattern for weapon flashlight  
                var flickerPattern = new[] { true, false, true, false, true };  
  
                foreach (bool state in flickerPattern)  
                {  
                    if (firearmItem?.Base == null) break; // Safety check  
  
                    // Toggle weapon flashlight state  
                    firearmItem.Base.Status = new InventorySystem.Items.Firearms.FirearmStatus(  
                        firearmItem.Base.Status.Ammo,  
                        state ? InventorySystem.Items.Firearms.FirearmStatusFlags.FlashlightEnabled :   
                               InventorySystem.Items.Firearms.FirearmStatusFlags.None,  
                        firearmItem.Base.Status.Attachments  
                    );  
  
                    int delay = _random.Next(45, 251);  
                    await Task.Delay(delay);  
                }  
            }  
            catch (Exception ex)  
            {  
                // Log error but don't crash the plugin  
                LabApi.Features.Console.Logger.Error($"Error in weapon flicker effect: {ex.Message}");  
            }  
        }  
  
        // Clean up old cooldowns periodically to prevent memory leaks  
        public void CleanupOldCooldowns()  
        {  
            var cutoffTime = DateTime.Now - _cooldownDuration;  
            var keysToRemove = new List<string>();  
  
            foreach (var kvp in _playerCooldowns)  
            {  
                if (kvp.Value < cutoffTime)  
                {  
                    keysToRemove.Add(kvp.Key);  
                }  
            }  
  
            foreach (var key in keysToRemove)  
            {  
                _playerCooldowns.Remove(key);  
            }  
        }  
    }  
}