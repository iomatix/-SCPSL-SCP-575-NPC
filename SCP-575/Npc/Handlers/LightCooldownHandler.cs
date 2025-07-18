namespace SCP_575.Npc
{
    using InventorySystem.Items;
    using InventorySystem.Items.Firearms;
    using InventorySystem.Items.Firearms.Attachments;
    using InventorySystem.Items.Firearms.Attachments.Components;
    using InventorySystem.Items.ToggleableLights;
    using LabApi.Events.Arguments.PlayerEvents;
    using LabApi.Events.CustomHandlers;
    using LabApi.Features.Wrappers;
    using MEC;
    using SCP_575.Shared;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using Utils.Networking;

    /// <summary>
    /// Manages flashlight and weapon light restrictions for players affected by SCP-575.
    /// Applies cooldowns, randomized flickering effects, and forced disables on attack events.
    /// </summary>
    public class LightCooldownHandler : CustomEventsHandler, IDisposable
    {
        private readonly ConcurrentDictionary<string, DateTime> _cooldownUntil = new();
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _flickerTokens = new();
        private readonly HashSet<string> _flickeringPlayers = new();
        private readonly Random _random = new();
        private CoroutineHandle _cleanupCoroutine;
        private bool _weaponFlashlightDisabled;
        private static readonly FieldInfo? _attachmentsField = InitializeAttachmentsField();

        /// <summary>
        /// Gets the cleanup interval, configurable via NpcConfig or defaulting to 160 seconds.
        /// </summary>
        private float CleanupInterval => Library_LabAPI.NpcConfig?.HandlerCleanupInterval ?? 160f;

        /// <summary>
        /// Gets the cooldown duration from configuration, defaulting to 1 second if invalid.
        /// </summary>
        private TimeSpan CooldownDuration => TimeSpan.FromSeconds(Math.Max(1, Library_LabAPI.NpcConfig?.KeterLightsourceCooldown ?? 1));

        /// <summary>
        /// Initializes the handler and starts periodic cleanup.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if NpcConfig is null.</exception>
        public LightCooldownHandler()
        {
            if (Library_LabAPI.NpcConfig == null)
                throw new InvalidOperationException("NpcConfig is not initialized.");
            _weaponFlashlightDisabled = _attachmentsField == null;
            if (_weaponFlashlightDisabled)
                Library_ExiledAPI.LogWarn(nameof(LightCooldownHandler), "Weapon flashlight support disabled due to missing attachments field.");
            _cleanupCoroutine = Timing.RunCoroutine(CleanupCoroutine(), "SCP575LightCleanup");
        }

        /// <summary>
        /// Blocks flashlight toggling if the player is on cooldown or a flicker is active.
        /// </summary>
        /// <param name="ev">Event arguments containing player and toggle state.</param>
        public override void OnPlayerTogglingFlashlight(PlayerTogglingFlashlightEventArgs ev)
        {
            if (ev?.Player == null || string.IsNullOrEmpty(ev.Player.UserId)) return;
            bool isAllowed = ev.IsAllowed;
            bool newState = ev.NewState;
            HandleLightToggling(ev.Player, ref isAllowed, ref newState, Library_LabAPI.NpcConfig?.LightEmitterCooldownHint ?? "Cooldown active.");
            ev.IsAllowed = isAllowed;
            ev.NewState = newState;
        }

        /// <summary>
        /// Blocks weapon flashlight toggling if the player is on cooldown, a flicker is active, or weapon flashlight support is disabled.
        /// </summary>
        /// <param name="ev">Event arguments containing player and toggle state.</param>
        public override void OnPlayerTogglingWeaponFlashlight(PlayerTogglingWeaponFlashlightEventArgs ev)
        {
            if (ev?.Player == null || string.IsNullOrEmpty(ev.Player.UserId) || _weaponFlashlightDisabled) return;
            bool isAllowed = ev.IsAllowed;
            bool newState = ev.NewState;
            HandleLightToggling(ev.Player, ref isAllowed, ref newState, Library_LabAPI.NpcConfig?.LightEmitterCooldownHint ?? "Cooldown active.");
            ev.IsAllowed = isAllowed;
            ev.NewState = newState;
        }

        /// <summary>
        /// Triggers a flicker effect for a flashlight when toggled on in a dark room.
        /// </summary>
        /// <param name="ev">Event arguments containing player and light item.</param>
        public override void OnPlayerToggledFlashlight(PlayerToggledFlashlightEventArgs ev)
        {
            if (ev?.Player == null || string.IsNullOrEmpty(ev.Player.UserId) || !ev.NewState || !Library_LabAPI.IsPlayerInDarkRoom(ev.Player)) return;
            _ = StartFlickerEffectAsync(ev.Player.UserId, "Flashlight", () => ev.LightItem.IsEmitting, state => ev.LightItem.IsEmitting = state);
        }

        /// <summary>
        /// Triggers a flicker effect for a weapon flashlight when toggled on in a dark room, if supported.
        /// </summary>
        /// <param name="ev">Event arguments containing player and firearm item.</param>
        public override void OnPlayerToggledWeaponFlashlight(PlayerToggledWeaponFlashlightEventArgs ev)
        {
            if (ev?.Player == null || string.IsNullOrEmpty(ev.Player.UserId) || !ev.NewState || !Library_LabAPI.IsPlayerInDarkRoom(ev.Player) || _weaponFlashlightDisabled) return;
            _ = StartFlickerEffectAsync(ev.Player.UserId, "WeaponFlashlight", () => GetWeaponFlashlightState(ev.FirearmItem), state => ToggleWeaponFlashlight(ev.FirearmItem, state, nameof(OnPlayerToggledWeaponFlashlight)));
        }

        /// <summary>
        /// Forces light off, triggers flicker, and applies cooldown when SCP-575 attacks a player.
        /// </summary>
        /// <param name="target">The player attacked by SCP-575.</param>
        public void OnScp575AttacksPlayer(Player target)
        {
            if (target?.UserId == null) return;
            var cooldownUntil = DateTime.Now + CooldownDuration;
            _cooldownUntil[target.UserId] = cooldownUntil;
            Library_ExiledAPI.LogDebug("Cooldown", $"Applied cooldown to {target.Nickname} until {cooldownUntil:T}");

            switch (target.CurrentItem)
            {
                case LightItem lightItem:
                    lightItem.IsEmitting = false;
                    Library_ExiledAPI.LogDebug("LightForceOff", $"Forced off flashlight for {target.Nickname}");
                    _ = StartFlickerEffectAsync(target.UserId, "Flashlight", () => lightItem.IsEmitting, state => lightItem.IsEmitting = state);
                    break;
                case FirearmItem firearm when !_weaponFlashlightDisabled:
                    ToggleWeaponFlashlight(firearm, false, nameof(OnScp575AttacksPlayer));
                    Library_ExiledAPI.LogDebug("LightForceOff", $"Forced off weapon flashlight for {target.Nickname}");
                    _ = StartFlickerEffectAsync(target.UserId, "WeaponFlashlight", () => GetWeaponFlashlightState(firearm), state => ToggleWeaponFlashlight(firearm, state, nameof(OnScp575AttacksPlayer)));
                    break;
            }

            if (Library_LabAPI.NpcConfig?.EnableLightEmitterCooldownHint ?? false)
                target.SendHint(Library_LabAPI.NpcConfig?.LightEmitterDisabledHint ?? "Your light is disabled!", 1.75f);
        }

        /// <summary>
        /// Forces a cooldown on a player's light source, preventing immediate reactivation.
        /// </summary>
        /// <param name="player">The player to apply the cooldown to.</param>
        public void ForceCooldown(Player player)
        {
            if (player?.UserId == null) return;
            _cooldownUntil[player.UserId] = DateTime.Now + CooldownDuration;
            Library_ExiledAPI.LogDebug("ForceCooldown", $"Forced cooldown on {player.Nickname}");
            if (Library_LabAPI.NpcConfig?.EnableLightEmitterCooldownHint ?? false)
                player.SendHint(Library_LabAPI.NpcConfig?.LightEmitterDisabledHint ?? "Your light is disabled!", 1.75f);
        }

        /// <summary>
        /// Cleans up all resources, including coroutines and cancellation tokens, on disposal.
        /// </summary>
        public void Dispose()
        {
            Timing.KillCoroutines(_cleanupCoroutine);
            foreach (var cts in _flickerTokens.Values) { cts?.Cancel(); cts?.Dispose(); }
            _flickerTokens.Clear();
            _cooldownUntil.Clear();
            _flickeringPlayers.Clear();
        }

        /// <summary>
        /// Initializes weapon flashlight support by checking for the attachments field.
        /// </summary>
        /// <returns>True if flashlight support is available, false otherwise.</returns>
        private static FieldInfo? InitializeAttachmentsField()
        {
            var field = typeof(Firearm).GetField("_attachments", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null || field.FieldType != typeof(Attachment[]))
            {
                Library_ExiledAPI.LogWarn(nameof(InitializeAttachmentsField), "Failed to find _attachments field on Firearm. Dumping fields:");
                foreach (var f in typeof(Firearm).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
                    Library_ExiledAPI.LogDebug(nameof(InitializeAttachmentsField), $"Field: {f.Name}, Type: {f.FieldType}");
                return null;
            }
            return field;
        }

        /// <summary>
        /// Gets the state of a weapon's flashlight by checking its attachments.
        /// </summary>
        /// <param name="firearm">The firearm item.</param>
        /// <returns>True if the flashlight is emitting light, false otherwise.</returns>
        private bool GetWeaponFlashlightState(FirearmItem firearm)
        {
            if (firearm?.Base == null || _weaponFlashlightDisabled) return false;
            try
            {
                var attachments = _attachmentsField?.GetValue(firearm.Base) as Attachment[];
                if (attachments == null)
                {
                    Library_ExiledAPI.LogWarn(nameof(GetWeaponFlashlightState), $"Attachments array not found for {firearm.Base.GetType().Name}. Disabling weapon flashlight functionality.");
                    _weaponFlashlightDisabled = true;
                    return false;
                }

                var flashlightAttachment = attachments.FirstOrDefault(a => a.Name == AttachmentName.Flashlight);
                if (flashlightAttachment == null)
                {
                    Library_ExiledAPI.LogWarn(nameof(GetWeaponFlashlightState), $"Flashlight attachment not found for {firearm.Base.GetType().Name}. Disabling weapon flashlight functionality.");
                    _weaponFlashlightDisabled = true;
                    return false;
                }

                var enabledProp = flashlightAttachment.GetType().GetProperty("IsEnabled", BindingFlags.Instance | BindingFlags.Public);
                if (enabledProp == null || enabledProp.PropertyType != typeof(bool))
                {
                    Library_ExiledAPI.LogWarn(nameof(GetWeaponFlashlightState), $"IsEnabled property not found or invalid for flashlight attachment on {firearm.Base.GetType().Name}. Disabling weapon flashlight functionality.");
                    _weaponFlashlightDisabled = true;
                    return false;
                }
                if (!(enabledProp.GetValue(flashlightAttachment) is bool isEnabled && isEnabled))
                {
                    Library_ExiledAPI.LogDebug(nameof(GetWeaponFlashlightState), $"Flashlight attachment for {firearm.Base.GetType().Name} is not enabled.");
                    return false;
                }

                var emissionProp = flashlightAttachment.GetType().GetProperty("IsEmittingLight", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (emissionProp == null || emissionProp.PropertyType != typeof(bool))
                {
                    Library_ExiledAPI.LogWarn(nameof(GetWeaponFlashlightState), $"IsEmittingLight property not found or invalid for flashlight attachment on {firearm.Base.GetType().Name}. Disabling weapon flashlight functionality.");
                    _weaponFlashlightDisabled = true;
                    return false;
                }
                bool isEmitting = emissionProp.GetValue(flashlightAttachment) is bool emitting && emitting;
                Library_ExiledAPI.LogDebug(nameof(GetWeaponFlashlightState), $"Flashlight attachment for {firearm.Base.GetType().Name} is {(isEmitting ? "emitting" : "not emitting")} light.");
                return isEmitting;
            }
            catch (Exception ex)
            {
                Library_ExiledAPI.LogWarn(nameof(GetWeaponFlashlightState), $"Error getting flashlight state for {firearm.Base.GetType().Name}: {ex.Message}. Disabling weapon flashlight functionality.");
                _weaponFlashlightDisabled = true;
                return false;
            }
        }

        /// <summary>
        /// Enforces cooldown and flicker restrictions when toggling a light source.
        /// </summary>
        /// <param name="player">The player attempting to toggle the light.</param>
        /// <param name="isAllowed">Whether the toggle action is permitted.</param>
        /// <param name="newState">The desired light state.</param>
        /// <param name="cooldownMessage">The message to display if the toggle is blocked.</param>
        private void HandleLightToggling(Player player, ref bool isAllowed, ref bool newState, string cooldownMessage)
        {
            if (!newState) return;
            if (_flickeringPlayers.Contains(player.UserId))
            {
                isAllowed = newState = false;
                Library_ExiledAPI.LogDebug("LightBlock", $"Blocked toggle for {player.Nickname} due to active flicker.");
                return;
            }
            if (_cooldownUntil.TryGetValue(player.UserId, out var until) && DateTime.Now < until)
            {
                isAllowed = newState = false;
                Library_ExiledAPI.LogDebug("LightBlock", $"Blocked toggle for {player.Nickname} due to cooldown ({(until - DateTime.Now).TotalSeconds:F1}s left).");
                if (Library_LabAPI.NpcConfig?.EnableLightEmitterCooldownHint ?? false)
                    player.SendHint(cooldownMessage, 1.75f);
            }
        }

        /// <summary>
        /// Executes a randomized flicker effect for a light source with cancellation support.
        /// </summary>
        /// <param name="userId">The player's user ID.</param>
        /// <param name="lightType">The type of light (e.g., Flashlight, WeaponFlashlight).</param>
        /// <param name="getState">Function to get the current light state.</param>
        /// <param name="setState">Action to set the new light state.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task StartFlickerEffectAsync(string userId, string lightType, Func<bool> getState, Action<bool> setState)
        {
            if (!_flickeringPlayers.Add(userId)) return;
            var cts = new CancellationTokenSource();
            _flickerTokens[userId] = cts;
            Library_ExiledAPI.LogDebug("Flicker", $"Started {lightType} flicker for {userId}");

            try
            {
                int flickerCount = _random.Next(3, 9);
                for (int i = 0; i < flickerCount && !cts.Token.IsCancellationRequested; i++)
                {
                    setState(!getState());
                    await Task.Delay(_random.Next(100, 450), cts.Token);
                }
                setState(false);
            }
            catch (TaskCanceledException) { setState(false); }
            catch (Exception ex) { Library_ExiledAPI.LogWarn(nameof(StartFlickerEffectAsync), $"{lightType} flicker error for {userId}: {ex.Message}"); }
            finally
            {
                _flickeringPlayers.Remove(userId);
                _flickerTokens.TryRemove(userId, out _);
                cts.Dispose();
                Library_ExiledAPI.LogDebug("Flicker", $"Ended {lightType} flicker for {userId}");
            }
        }

        /// <summary>
        /// Toggles a weapon's flashlight state using its attachments and sends a network update.
        /// </summary>
        /// <param name="firearm">The firearm item to toggle.</param>
        /// <param name="enabled">The desired flashlight state.</param>
        /// <param name="context">The calling context for logging purposes.</param>
        private void ToggleWeaponFlashlight(FirearmItem firearm, bool enabled, string context)
        {
            if (firearm?.Base == null || _weaponFlashlightDisabled) return;
            try
            {
                var attachments = _attachmentsField?.GetValue(firearm.Base) as Attachment[];
                if (attachments == null)
                {
                    Library_ExiledAPI.LogWarn(context, $"Attachments array not found for {firearm.Base.GetType().Name}. Disabling weapon flashlight functionality.");
                    _weaponFlashlightDisabled = true;
                    return;
                }

                var flashlightAttachment = attachments.FirstOrDefault(a => a.Name == AttachmentName.Flashlight);
                if (flashlightAttachment == null)
                {
                    Library_ExiledAPI.LogWarn(context, $"Flashlight attachment not found for {firearm.Base.GetType().Name}. Disabling weapon flashlight functionality.");
                    _weaponFlashlightDisabled = true;
                    return;
                }

                var enabledProp = flashlightAttachment.GetType().GetProperty("IsEnabled", BindingFlags.Instance | BindingFlags.Public);
                if (enabledProp == null || !enabledProp.CanWrite || enabledProp.PropertyType != typeof(bool))
                {
                    Library_ExiledAPI.LogWarn(context, $"Cannot access IsEnabled property on flashlight attachment for {firearm.Base.GetType().Name}. Disabling weapon flashlight functionality.");
                    _weaponFlashlightDisabled = true;
                    return;
                }
                if (!(enabledProp.GetValue(flashlightAttachment) is bool isEnabled && isEnabled))
                {
                    enabledProp.SetValue(flashlightAttachment, true);
                    Library_ExiledAPI.LogDebug(context, $"Enabled flashlight attachment for {firearm.Base.GetType().Name}.");
                }

                // Toggle emission state via network message
                new FlashlightNetworkHandler.FlashlightMessage(firearm.Serial, enabled).SendToAuthenticated();
                Library_ExiledAPI.LogDebug(context, $"Toggled flashlight emission state to {enabled} for {firearm.Base.GetType().Name}.");
            }
            catch (Exception ex)
            {
                Library_ExiledAPI.LogWarn(context, $"Error toggling flashlight for {firearm.Base.GetType().Name}: {ex.Message}. Disabling weapon flashlight functionality.");
                _weaponFlashlightDisabled = true;
            }
        }

        /// <summary>
        /// Periodically cleans up expired cooldowns and flicker tokens for disconnected players.
        /// </summary>
        /// <returns>An enumerator for the cleanup coroutine.</returns>
        private IEnumerator<float> CleanupCoroutine()
        {
            while (true)
            {
                yield return Timing.WaitForSeconds(CleanupInterval);
                var cutoff = DateTime.Now - CooldownDuration;
                foreach (var kvp in _cooldownUntil.Where(k => k.Value < cutoff).ToList())
                    _cooldownUntil.TryRemove(kvp.Key, out _);
                foreach (var kvp in _flickerTokens.Where(k => !Player.List.Any(p => p.UserId == k.Key)).ToList())
                    if (_flickerTokens.TryRemove(kvp.Key, out var cts)) { cts.Cancel(); cts.Dispose(); }
            }
        }

        /// <summary>
        /// Clears cooldowns for a specific player or all players if null.
        /// </summary>
        /// <param name="player">The player to clear the cooldown for, or null to clear all.</param>
        public void ClearCooldown(Player? player = null)
        {
            if (player?.UserId != null)
                _cooldownUntil.TryRemove(player.UserId, out _);
            else
                _cooldownUntil.Clear();
            Library_ExiledAPI.LogDebug("ClearCooldown", $"Cleared cooldown for {(player != null ? player.Nickname : "all players")}");
        }
    }
}