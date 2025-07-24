namespace SCP_575.Npc
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using MEC;
    using Utils.Networking;
    using InventorySystem.Items.Firearms;
    using InventorySystem.Items.Firearms.Attachments;
    using InventorySystem.Items.Firearms.Attachments.Components;
    using InventorySystem.Items.ToggleableLights;
    using LabApi.Events.Arguments.PlayerEvents;
    using LabApi.Events.CustomHandlers;
    using LabApi.Features.Wrappers;
    using SCP_575.Shared;

    // TODO: Refactor Weapon flashlight handling to same logic like flashlight when LabAPI has it ready: https://github.com/northwood-studios/LabAPI/issues/220?notification_referrer_id=NT_kwDOAMgLkbQxNzY3OTc2MTM2ODoxMzExMDE2MQ#issuecomment-3092327154

    /// <summary>
    /// Manages flashlight and weapon light restrictions for players affected by SCP-575, applying cooldowns,
    /// randomized flickering effects, and forced disables on attack events.
    /// </summary>
    public class LightCooldownHandler : CustomEventsHandler, IDisposable
    {
        private readonly Plugin _plugin;
        private readonly ConcurrentDictionary<string, DateTime> _cooldownUntil = new();
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _flickerTokens = new();
        private readonly HashSet<string> _flickeringPlayers = new();
        private readonly Random _random = new();
        private readonly CoroutineHandle _cleanupCoroutine;
        private bool _weaponFlashlightDisabled;
        private static readonly FieldInfo? _attachmentsField = InitializeAttachmentsField();

        /// <summary>
        /// Initializes a new instance of the <see cref="LightCooldownHandler"/> class.
        /// </summary>
        /// <param name="plugin">The plugin instance providing access to configuration.</param>
        /// <exception cref="ArgumentNullException">Thrown if the plugin instance is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown if NpcConfig is null.</exception>
        public LightCooldownHandler(Plugin plugin)
        {
            _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin), "Plugin instance cannot be null.");
            if (_plugin.Config.NpcConfig == null)
                throw new InvalidOperationException("NpcConfig is not initialized.");

            _weaponFlashlightDisabled = _attachmentsField == null;
            if (_weaponFlashlightDisabled)
                Library_ExiledAPI.LogWarn("LightCooldownHandler.Constructor", "Weapon flashlight support disabled due to missing attachments field.");
            _cleanupCoroutine = Timing.RunCoroutine(CleanupCoroutine(), "SCP575LightCleanup");
            Library_ExiledAPI.LogInfo("LightCooldownHandler.Constructor", "Initialized light cooldown handler and started cleanup coroutine.");
        }

        /// <summary>
        /// Gets the cleanup interval from configuration, defaulting to 160 seconds if not specified.
        /// </summary>
        private float CleanupInterval => _plugin.Config.NpcConfig?.HandlerCleanupInterval ?? 160f;

        /// <summary>
        /// Gets the cooldown duration from configuration, defaulting to 1 second if invalid.
        /// </summary>
        private TimeSpan CooldownDuration => TimeSpan.FromSeconds(Math.Max(1, _plugin.Config.NpcConfig?.KeterLightsourceCooldown ?? 1));

        #region Event Handlers

        /// <summary>
        /// Handles the PlayerChangedItem event, turning the flashlight OFF by default to prevent cheating SCP-575 mechanics.
        /// </summary>
        /// <param name="ev">The event arguments for the item changed event.</param>
        public override void OnPlayerChangedItem(LabApi.Events.Arguments.PlayerEvents.PlayerChangedItemEventArgs ev)
        {
            try
            {
                // Check if the newly equipped item is a flashlight
                if (ev.NewItem is LabApi.Features.Wrappers.LightItem lightItem)
                {
                    Library_ExiledAPI.LogInfo("OnPlayerChangedItem", $"Player {ev?.Player?.Nickname ?? "unknown"} equipped a flashlight (Item ID: {lightItem.Base.ItemId})");

                    // Turn off the flashlight if it's currently on
                    Timing.CallDelayed(0.05f, () =>
                    {
                        if (lightItem.IsEmitting)
                        {
                            Library_ExiledAPI.LogWarn("OnPlayerChangedItem", $"Flashlight is emitting for player {ev?.Player?.Nickname ?? "unknown"}, turning it off to prevent SCP-575 mechanic abuse");
                            lightItem.IsEmitting = false;
                            Library_ExiledAPI.LogInfo("OnPlayerChangedItem", $"Flashlight turned off successfully for player {ev?.Player?.Nickname ?? "unknown"}");
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Library_ExiledAPI.LogError("OnPlayerChangedItem", $"Failed to handle PlayerChangedItem for {ev?.Player?.Nickname ?? "unknown"}: {ex.Message}\nStackTrace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Blocks flashlight toggling if the player is on cooldown or a flicker is active.
        /// </summary>
        /// <param name="ev">The event arguments containing player and toggle state.</param>
        public override void OnPlayerTogglingFlashlight(LabApi.Events.Arguments.PlayerEvents.PlayerTogglingFlashlightEventArgs ev)
        {
            try
            {
                if (ev?.Player == null || string.IsNullOrEmpty(ev.Player.UserId))
                {
                    Library_ExiledAPI.LogDebug("OnPlayerTogglingFlashlight", "Player or UserId is null. Skipping.");
                    return;
                }

                if (!Plugin.Singleton.Npc.Methods.IsBlackoutActive)
                {
                    Library_ExiledAPI.LogDebug("OnPlayerTogglingFlashlight", "SCP-575 is not active. Skipping.");
                    return;
                }

                bool isAllowed = ev.IsAllowed;
                bool newState = ev.NewState;
                HandleLightToggling(ev.Player, ref isAllowed, ref newState, _plugin.Config.NpcConfig?.LightEmitterCooldownHint ?? "Cooldown active.");
                ev.IsAllowed = isAllowed;
                ev.NewState = newState;
                Library_ExiledAPI.LogDebug("OnPlayerTogglingFlashlight", $"Processed flashlight toggle for {ev.Player.Nickname}: IsAllowed={isAllowed}, NewState={newState}");
            }
            catch (Exception ex)
            {
                Library_ExiledAPI.LogError("OnPlayerTogglingFlashlight", $"Failed to handle PlayerTogglingFlashlight for {ev?.Player?.Nickname ?? "unknown"}: {ex.Message}\nStackTrace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Blocks weapon flashlight toggling if the player is on cooldown, a flicker is active, or weapon flashlight support is disabled.
        /// </summary>
        /// <param name="ev">The event arguments containing player and toggle state.</param>
        public override void OnPlayerTogglingWeaponFlashlight(PlayerTogglingWeaponFlashlightEventArgs ev)
        {
            try
            {
                if (ev?.Player == null || string.IsNullOrEmpty(ev.Player.UserId) || _weaponFlashlightDisabled)
                {
                    Library_ExiledAPI.LogDebug("OnPlayerTogglingWeaponFlashlight", $"Skipping: Player={ev?.Player?.Nickname ?? "null"}, UserId={(ev?.Player != null ? ev.Player.UserId : "null")}, WeaponFlashlightDisabled={_weaponFlashlightDisabled}");
                    return;
                }

                if (!Plugin.Singleton.Npc.Methods.IsBlackoutActive)
                {
                    Library_ExiledAPI.LogDebug("OnPlayerTogglingFlashlight", "SCP-575 is not active. Skipping.");
                    return;
                }

                bool isAllowed = ev.IsAllowed;
                bool newState = ev.NewState;
                HandleLightToggling(ev.Player, ref isAllowed, ref newState, _plugin.Config.NpcConfig?.LightEmitterCooldownHint ?? "Cooldown active.");
                ev.IsAllowed = isAllowed;
                ev.NewState = newState;
                Library_ExiledAPI.LogDebug("OnPlayerTogglingWeaponFlashlight", $"Processed weapon flashlight toggle for {ev.Player.Nickname}: IsAllowed={isAllowed}, NewState={newState}");
            }
            catch (Exception ex)
            {
                Library_ExiledAPI.LogError("OnPlayerTogglingWeaponFlashlight", $"Failed to handle PlayerTogglingWeaponFlashlight for {ev?.Player?.Nickname ?? "unknown"}: {ex.Message}\nStackTrace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Triggers a flicker effect for a flashlight when toggled on in a dark room.
        /// </summary>
        /// <param name="ev">The event arguments containing player and light item.</param>
        public override void OnPlayerToggledFlashlight(PlayerToggledFlashlightEventArgs ev)
        {
            try
            {
                if (ev?.Player == null || string.IsNullOrEmpty(ev.Player.UserId) || !ev.NewState || !Library_LabAPI.IsPlayerInDarkRoom(ev.Player) || !_plugin.Npc.Methods.IsBlackoutActive)
                {
                    Library_ExiledAPI.LogDebug("OnPlayerToggledFlashlight", $"Skipping: Player={ev?.Player?.Nickname ?? "null"}, UserId={(ev?.Player != null ? ev.Player.UserId : "null")}, NewState={ev?.NewState}, InDarkRoom={ev != null && Library_LabAPI.IsPlayerInDarkRoom(ev.Player)}, IsBlackoutActive={_plugin.Npc.Methods.IsBlackoutActive}");
                    return;
                }

                _ = StartFlickerEffectAsync(ev.Player.UserId, "Flashlight", () => ev.LightItem.IsEmitting, state => ev.LightItem.IsEmitting = state);
            }
            catch (Exception ex)
            {
                Library_ExiledAPI.LogError("OnPlayerToggledFlashlight", $"Failed to handle PlayerToggledFlashlight for {ev?.Player?.Nickname ?? "unknown"}: {ex.Message}\nStackTrace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Triggers a flicker effect for a weapon flashlight when toggled on in a dark room, if supported.
        /// </summary>
        /// <param name="ev">The event arguments containing player and firearm item.</param>
        public override void OnPlayerToggledWeaponFlashlight(PlayerToggledWeaponFlashlightEventArgs ev)
        {
            try
            {
                if (ev?.Player == null || string.IsNullOrEmpty(ev.Player.UserId) || !ev.NewState || !Library_LabAPI.IsPlayerInDarkRoom(ev.Player) || !_plugin.Npc.Methods.IsBlackoutActive || _weaponFlashlightDisabled)
                {
                    Library_ExiledAPI.LogDebug("OnPlayerToggledWeaponFlashlight", $"Skipping: Player={ev?.Player?.Nickname ?? "null"}, UserId={(ev?.Player != null ? ev.Player.UserId : "null")}, NewState={ev?.NewState}, InDarkRoom={ev != null && Library_LabAPI.IsPlayerInDarkRoom(ev.Player)}, IsBlackoutActive={_plugin.Npc.Methods.IsBlackoutActive}, WeaponFlashlightDisabled={_weaponFlashlightDisabled}");
                    return;
                }

                _ = StartFlickerEffectAsync(ev.Player.UserId, "WeaponFlashlight", () => GetWeaponFlashlightState(ev.FirearmItem), state => ToggleWeaponFlashlight(ev.FirearmItem, state, nameof(OnPlayerToggledWeaponFlashlight)));
            }
            catch (Exception ex)
            {
                Library_ExiledAPI.LogError("OnPlayerToggledWeaponFlashlight", $"Failed to handle PlayerToggledWeaponFlashlight for {ev?.Player?.Nickname ?? "unknown"}: {ex.Message}\nStackTrace: {ex.StackTrace}");
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Forces light off, triggers flicker, and applies cooldown when SCP-575 attacks a player.
        /// </summary>
        /// <param name="target">The player attacked by SCP-575.</param>
        public void OnScp575AttacksPlayer(Player target)
        {
            try
            {
                if (target?.UserId == null)
                {
                    Library_ExiledAPI.LogDebug("OnScp575AttacksPlayer", "Target or UserId is null. Skipping.");
                    return;
                }

                var cooldownUntil = DateTime.Now + CooldownDuration;
                _cooldownUntil[target.UserId] = cooldownUntil;
                Library_ExiledAPI.LogDebug("OnScp575AttacksPlayer", $"Applied cooldown to {target.Nickname} until {cooldownUntil:T}");

                switch (target.CurrentItem)
                {
                    case LightItem lightItem:
                        lightItem.IsEmitting = false;
                        Library_ExiledAPI.LogDebug("OnScp575AttacksPlayer", $"Forced off flashlight for {target.Nickname}");
                        _ = StartFlickerEffectAsync(target.UserId, "Flashlight", () => lightItem.IsEmitting, state => lightItem.IsEmitting = state);
                        break;
                    case FirearmItem firearm when !_weaponFlashlightDisabled:
                        ToggleWeaponFlashlight(firearm, false, nameof(OnScp575AttacksPlayer));
                        Library_ExiledAPI.LogDebug("OnScp575AttacksPlayer", $"Forced off weapon flashlight for {target.Nickname}");
                        _ = StartFlickerEffectAsync(target.UserId, "WeaponFlashlight", () => GetWeaponFlashlightState(firearm), state => ToggleWeaponFlashlight(firearm, state, nameof(OnScp575AttacksPlayer)));
                        break;
                }

                if (_plugin.Config.NpcConfig?.EnableLightEmitterCooldownHint ?? false)
                    target.SendHint(_plugin.Config.NpcConfig?.LightEmitterDisabledHint ?? "Your light is disabled!", 1.75f);
            }
            catch (Exception ex)
            {
                Library_ExiledAPI.LogError("OnScp575AttacksPlayer", $"Failed to handle SCP-575 attack for {target?.Nickname ?? "unknown"}: {ex.Message}\nStackTrace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Forces a cooldown on a player's light source, preventing immediate reactivation.
        /// </summary>
        /// <param name="player">The player to apply the cooldown to.</param>
        public void ForceCooldown(Player player)
        {
            try
            {
                if (player?.UserId == null)
                {
                    Library_ExiledAPI.LogDebug("ForceCooldown", "Player or UserId is null. Skipping.");
                    return;
                }

                _cooldownUntil[player.UserId] = DateTime.Now + CooldownDuration;
                Library_ExiledAPI.LogDebug("ForceCooldown", $"Forced cooldown on {player.Nickname}");
                if (_plugin.Config.NpcConfig?.EnableLightEmitterCooldownHint ?? false)
                    player.SendHint(_plugin.Config.NpcConfig?.LightEmitterDisabledHint ?? "Your light is disabled!", 1.75f);
            }
            catch (Exception ex)
            {
                Library_ExiledAPI.LogError("ForceCooldown", $"Failed to force cooldown for {player?.Nickname ?? "unknown"}: {ex.Message}\nStackTrace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Clears cooldowns for a specific player or all players if null.
        /// </summary>
        /// <param name="player">The player to clear the cooldown for, or null to clear all.</param>
        public void ClearCooldown(Player? player = null)
        {
            try
            {
                if (player?.UserId != null)
                {
                    _cooldownUntil.TryRemove(player.UserId, out _);
                    Library_ExiledAPI.LogDebug("ClearCooldown", $"Cleared cooldown for {player.Nickname}");
                }
                else
                {
                    _cooldownUntil.Clear();
                    Library_ExiledAPI.LogDebug("ClearCooldown", "Cleared cooldown for all players");
                }
            }
            catch (Exception ex)
            {
                Library_ExiledAPI.LogError("ClearCooldown", $"Failed to clear cooldown for {player?.Nickname ?? "all players"}: {ex.Message}\nStackTrace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Cleans up all resources, including coroutines and cancellation tokens, on disposal.
        /// </summary>
        public void Dispose()
        {
            try
            {
                Timing.KillCoroutines(_cleanupCoroutine);
                foreach (var cts in _flickerTokens.Values)
                {
                    cts?.Cancel();
                    cts?.Dispose();
                }
                _flickerTokens.Clear();
                _cooldownUntil.Clear();
                _flickeringPlayers.Clear();
                Library_ExiledAPI.LogInfo("LightCooldownHandler.Dispose", "Disposed light cooldown handler and cleaned up resources.");
            }
            catch (Exception ex)
            {
                Library_ExiledAPI.LogError("LightCooldownHandler.Dispose", $"Failed to dispose light cooldown handler: {ex.Message}\nStackTrace: {ex.StackTrace}");
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Initializes weapon flashlight support by checking for the attachments field.
        /// </summary>
        /// <returns>The attachments field info if found, otherwise null.</returns>
        private static FieldInfo? InitializeAttachmentsField()
        {
            try
            {
                var field = typeof(Firearm).GetField("_attachments", BindingFlags.Instance | BindingFlags.NonPublic);
                if (field == null || field.FieldType != typeof(Attachment[]))
                {
                    Library_ExiledAPI.LogWarn("InitializeAttachmentsField", "Failed to find _attachments field on Firearm. Dumping fields:");
                    foreach (var f in typeof(Firearm).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
                        Library_ExiledAPI.LogDebug("InitializeAttachmentsField", $"Field: {f.Name}, Type: {f.FieldType}");
                    return null;
                }
                Library_ExiledAPI.LogDebug("InitializeAttachmentsField", "Successfully initialized _attachments field for weapon flashlight support.");
                return field;
            }
            catch (Exception ex)
            {
                Library_ExiledAPI.LogError("InitializeAttachmentsField", $"Failed to initialize attachments field: {ex.Message}\nStackTrace: {ex.StackTrace}");
                return null;
            }
        }

        /// <summary>
        /// Gets the state of a weapon's flashlight by checking its attachments.
        /// </summary>
        /// <param name="firearm">The firearm item.</param>
        /// <returns>True if the flashlight is emitting light, false otherwise.</returns>
        private bool GetWeaponFlashlightState(FirearmItem firearm)
        {
            if (firearm?.Base == null || _weaponFlashlightDisabled)
            {
                Library_ExiledAPI.LogDebug("GetWeaponFlashlightState", $"Skipping: Firearm={firearm?.Base?.GetType().Name ?? "null"}, WeaponFlashlightDisabled={_weaponFlashlightDisabled}");
                return false;
            }

            try
            {
                var attachments = _attachmentsField?.GetValue(firearm.Base) as Attachment[];
                if (attachments == null)
                {
                    Library_ExiledAPI.LogWarn("GetWeaponFlashlightState", $"Attachments array not found for {firearm.Base.GetType().Name}. Disabling weapon flashlight functionality.");
                    _weaponFlashlightDisabled = true;
                    return false;
                }

                var flashlightAttachment = attachments.FirstOrDefault(a => a.Name == AttachmentName.Flashlight);
                if (flashlightAttachment == null)
                {
                    Library_ExiledAPI.LogWarn("GetWeaponFlashlightState", $"Flashlight attachment not found for {firearm.Base.GetType().Name}. Disabling weapon flashlight functionality.");
                    _weaponFlashlightDisabled = true;
                    return false;
                }

                var enabledProp = flashlightAttachment.GetType().GetProperty("IsEnabled", BindingFlags.Instance | BindingFlags.Public);
                if (enabledProp == null || enabledProp.PropertyType != typeof(bool))
                {
                    Library_ExiledAPI.LogWarn("GetWeaponFlashlightState", $"IsEnabled property not found or invalid for flashlight attachment on {firearm.Base.GetType().Name}. Disabling weapon flashlight functionality.");
                    _weaponFlashlightDisabled = true;
                    return false;
                }

                if (!(enabledProp.GetValue(flashlightAttachment) is bool isEnabled && isEnabled))
                {
                    Library_ExiledAPI.LogDebug("GetWeaponFlashlightState", $"Flashlight attachment for {firearm.Base.GetType().Name} is not enabled.");
                    return false;
                }

                var emissionProp = flashlightAttachment.GetType().GetProperty("IsEmittingLight", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (emissionProp == null || emissionProp.PropertyType != typeof(bool))
                {
                    Library_ExiledAPI.LogWarn("GetWeaponFlashlightState", $"IsEmittingLight property not found or invalid for flashlight attachment on {firearm.Base.GetType().Name}. Disabling weapon flashlight functionality.");
                    _weaponFlashlightDisabled = true;
                    return false;
                }

                bool isEmitting = emissionProp.GetValue(flashlightAttachment) is bool emitting && emitting;
                Library_ExiledAPI.LogDebug("GetWeaponFlashlightState", $"Flashlight attachment for {firearm.Base.GetType().Name} is {(isEmitting ? "emitting" : "not emitting")} light.");
                return isEmitting;
            }
            catch (Exception ex)
            {
                Library_ExiledAPI.LogError("GetWeaponFlashlightState", $"Failed to get flashlight state for {firearm?.Base?.GetType().Name ?? "unknown"}: {ex.Message}\nStackTrace: {ex.StackTrace}");
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
            try
            {
                if (!newState)
                {
                    Library_ExiledAPI.LogDebug("HandleLightToggling", $"No action needed for {player.Nickname}: NewState is false.");
                    return;
                }

                if (!_plugin.Npc.Methods.IsBlackoutActive)
                {
                    Library_ExiledAPI.LogDebug("HandleLightToggling", $"No action needed for {player.Nickname}: SCP-575 is not active.");
                    return;
                }


                if (_flickeringPlayers.Contains(player.UserId))
                {
                    isAllowed = newState = false;
                    Library_ExiledAPI.LogDebug("HandleLightToggling", $"Blocked toggle for {player.Nickname} due to active flicker.");
                    return;
                }

                if (_cooldownUntil.TryGetValue(player.UserId, out var until) && DateTime.Now < until)
                {
                    isAllowed = true;
                    newState = false;
                    Library_ExiledAPI.LogDebug("HandleLightToggling", $"Blocked toggle for {player.Nickname} due to cooldown ({(until - DateTime.Now).TotalSeconds:F1}s left).");
                    if (_plugin.Config.NpcConfig?.EnableLightEmitterCooldownHint ?? false)
                        player.SendHint(cooldownMessage, 1.75f);
                }
            }
            catch (Exception ex)
            {
                Library_ExiledAPI.LogError("HandleLightToggling", $"Failed to handle light toggling for {player.Nickname}: {ex.Message}\nStackTrace: {ex.StackTrace}");
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
            try
            {
                if (!_flickeringPlayers.Add(userId))
                {
                    Library_ExiledAPI.LogDebug("StartFlickerEffectAsync", $"Flicker already active for {userId}. Skipping.");
                    return;
                }

                var cts = new CancellationTokenSource();
                _flickerTokens[userId] = cts;
                Library_ExiledAPI.LogDebug("StartFlickerEffectAsync", $"Started {lightType} flicker for {userId}");

                int flickerCount = _random.Next(3, 11);
                for (int i = 0; i < flickerCount && !cts.Token.IsCancellationRequested; i++)
                {
                    setState(!getState());
                    await Task.Delay(_random.Next(100, 450), cts.Token);
                }
                setState(false);
            }
            catch (TaskCanceledException)
            {
                setState(false);
                Library_ExiledAPI.LogDebug("StartFlickerEffectAsync", $"Flicker cancelled for {userId}.");
            }
            catch (Exception ex)
            {
                Library_ExiledAPI.LogWarn("StartFlickerEffectAsync", $"{lightType} flicker error for {userId}: {ex.Message}\nStackTrace: {ex.StackTrace}");
            }
            finally
            {
                _flickeringPlayers.Remove(userId);
                _flickerTokens.TryRemove(userId, out var cts);
                cts?.Dispose();
                Library_ExiledAPI.LogDebug("StartFlickerEffectAsync", $"Ended {lightType} flicker for {userId}");
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
            if (firearm?.Base == null || _weaponFlashlightDisabled)
            {
                Library_ExiledAPI.LogDebug("ToggleWeaponFlashlight", $"Skipping: Firearm={firearm?.Base?.GetType().Name ?? "null"}, WeaponFlashlightDisabled={_weaponFlashlightDisabled}");
                return;
            }

            try
            {
                var attachments = _attachmentsField?.GetValue(firearm.Base) as Attachment[];
                if (attachments == null)
                {
                    Library_ExiledAPI.LogWarn("ToggleWeaponFlashlight", $"Attachments array not found for {firearm.Base.GetType().Name}. Disabling weapon flashlight functionality.");
                    _weaponFlashlightDisabled = true;
                    return;
                }

                var flashlightAttachment = attachments.FirstOrDefault(a => a.Name == AttachmentName.Flashlight);
                if (flashlightAttachment == null)
                {
                    Library_ExiledAPI.LogWarn("ToggleWeaponFlashlight", $"Flashlight attachment not found for {firearm.Base.GetType().Name}. Disabling weapon flashlight functionality.");
                    _weaponFlashlightDisabled = true;
                    return;
                }

                var enabledProp = flashlightAttachment.GetType().GetProperty("IsEnabled", BindingFlags.Instance | BindingFlags.Public);
                if (enabledProp == null || !enabledProp.CanWrite || enabledProp.PropertyType != typeof(bool))
                {
                    Library_ExiledAPI.LogWarn("ToggleWeaponFlashlight", $"Cannot access IsEnabled property on flashlight attachment for {firearm.Base.GetType().Name}. Disabling weapon flashlight functionality.");
                    _weaponFlashlightDisabled = true;
                    return;
                }

                if (!(enabledProp.GetValue(flashlightAttachment) is bool isEnabled && isEnabled))
                {
                    enabledProp.SetValue(flashlightAttachment, true);
                    Library_ExiledAPI.LogDebug("ToggleWeaponFlashlight", $"Enabled flashlight attachment for {firearm.Base.GetType().Name}.");
                }

                new FlashlightNetworkHandler.FlashlightMessage(firearm.Serial, enabled).SendToAuthenticated();
                Library_ExiledAPI.LogDebug("ToggleWeaponFlashlight", $"Toggled flashlight emission state to {enabled} for {firearm.Base.GetType().Name} in context {context}.");
            }
            catch (Exception ex)
            {
                Library_ExiledAPI.LogError("ToggleWeaponFlashlight", $"Failed to toggle flashlight for {firearm.Base.GetType().Name} in context {context}: {ex.Message}\nStackTrace: {ex.StackTrace}");
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
                try
                {
                    var cutoff = DateTime.Now - CooldownDuration;
                    foreach (var kvp in _cooldownUntil.Where(k => k.Value < cutoff).ToList())
                        _cooldownUntil.TryRemove(kvp.Key, out _);
                    foreach (var kvp in _flickerTokens.Where(k => !Player.List.Any(p => p.UserId == k.Key)).ToList())
                        if (_flickerTokens.TryRemove(kvp.Key, out var cts))
                        {
                            cts.Cancel();
                            cts.Dispose();
                        }
                    Library_ExiledAPI.LogDebug("CleanupCoroutine", "Completed cleanup of expired cooldowns and flicker tokens.");
                }
                catch (Exception ex)
                {
                    Library_ExiledAPI.LogError("CleanupCoroutine", $"Failed to run cleanup coroutine: {ex.Message}\nStackTrace: {ex.StackTrace}");
                }
            }
        }

        #endregion
    }
}