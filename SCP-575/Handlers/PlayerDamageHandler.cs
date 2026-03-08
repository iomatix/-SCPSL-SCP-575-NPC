namespace SCP_575.Handlers
{
    using LabApi.Events.Arguments.PlayerEvents;
    using LabApi.Events.CustomHandlers;
    using MEC;
    using SCP_575.Shared;
    using SCP_575.Systems;
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Handles all SCP-575 related player damage events.
    /// Responsible for validating SCP-575 damage and triggering
    /// additional effects when players are hurt or killed.
    /// </summary>
    public class PlayerDamageHandler : CustomEventsHandler
    {
        private readonly Plugin _plugin;

        /// <summary>
        /// Active coroutine handles started by this handler.
        /// </summary>
        private readonly List<CoroutineHandle> _coroutines = new();

        public PlayerDamageHandler(Plugin plugin)
        {
            _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
        }

        public override void OnPlayerHurting(PlayerHurtingEventArgs ev)
        {
            if (!_plugin.IsEventActive)
                return;

            try
            {
                if (ev?.Player == null || ev.DamageHandler == null)
                {
                    LibraryLabAPI.LogDebug("PlayerDamageHandler.Hurting", "Invalid event arguments.");
                    return;
                }

                LibraryLabAPI.LogDebug(
                    "PlayerDamageHandler.Hurting",
                    $"Player {ev.Player.Nickname} is being hurt by {ev.Attacker?.Nickname ?? "SCP-575 NPC"}.");

                if (!Scp575DamageSystem.IsScp575Damage(ev.DamageHandler))
                {
                    LibraryLabAPI.LogDebug(
                        "PlayerDamageHandler.Hurting",
                        "Damage not caused by SCP-575. Ignoring.");
                    return;
                }

                LibraryLabAPI.LogDebug(
                    "PlayerDamageHandler.Hurting",
                    $"Damage confirmed from {Scp575DamageSystem.IdentifierName}.");
            }
            catch (Exception ex)
            {
                LibraryLabAPI.LogError(
                    "PlayerDamageHandler.Hurting",
                    $"Error while processing PlayerHurting: {ex}");
            }
        }

        public override void OnPlayerHurt(PlayerHurtEventArgs ev)
        {
            if (!_plugin.IsEventActive)
                return;

            try
            {
                if (ev?.Player == null || ev.DamageHandler == null)
                {
                    LibraryLabAPI.LogDebug("PlayerDamageHandler.Hurt", "Invalid event arguments.");
                    return;
                }

                LibraryLabAPI.LogDebug(
                    "PlayerDamageHandler.Hurt",
                    $"Player {ev.Player.Nickname} was hurt by {ev.Attacker?.Nickname ?? "SCP-575 NPC"}.");

                if (!Scp575DamageSystem.IsScp575Damage(ev.DamageHandler))
                {
                    LibraryLabAPI.LogDebug(
                        "PlayerDamageHandler.Hurt",
                        "Damage not caused by SCP-575. Ignoring.");
                    return;
                }

                LibraryLabAPI.LogDebug(
                    "PlayerDamageHandler.Hurt",
                    $"Damage confirmed from {Scp575DamageSystem.IdentifierName}.");
            }
            catch (Exception ex)
            {
                LibraryLabAPI.LogError(
                    "PlayerDamageHandler.Hurt",
                    $"Error while processing PlayerHurt: {ex}");
            }
        }

        public override void OnPlayerDying(PlayerDyingEventArgs ev)
        {
            if (!_plugin.IsEventActive)
                return;

            try
            {
                if (ev?.Player == null || ev.DamageHandler == null)
                {
                    LibraryLabAPI.LogDebug("PlayerDamageHandler.Dying", "Invalid event arguments.");
                    return;
                }

                LibraryLabAPI.LogDebug(
                    "PlayerDamageHandler.Dying",
                    $"Player {ev.Player.Nickname} is dying.");

                if (!Scp575DamageSystem.IsScp575Damage(ev.DamageHandler))
                {
                    LibraryLabAPI.LogDebug(
                        "PlayerDamageHandler.Dying",
                        "Death not caused by SCP-575. Ignoring.");
                    return;
                }

                LibraryLabAPI.LogDebug(
                    "PlayerDamageHandler.Dying",
                    $"Death confirmed from {Scp575DamageSystem.IdentifierName}.");

                var coroutine = _plugin.Npc.Methods.RunTrackedCoroutine(
                    Scp575DamageSystem.DropAndPushItems(ev.Player));

                _coroutines.Add(coroutine);
            }
            catch (Exception ex)
            {
                LibraryLabAPI.LogError(
                    "PlayerDamageHandler.Dying",
                    $"Error while processing PlayerDying: {ex}");
            }
        }

        public override void OnPlayerDeath(PlayerDeathEventArgs ev)
        {
            if (!_plugin.IsEventActive)
                return;

            try
            {
                if (ev?.Player == null || ev.DamageHandler == null)
                {
                    LibraryLabAPI.LogDebug("PlayerDamageHandler.Death", "Invalid event arguments.");
                    return;
                }

                LibraryLabAPI.LogDebug(
                    "PlayerDamageHandler.Death",
                    $"Player {ev.Player.Nickname} died.");

                if (!Scp575DamageSystem.IsScp575Damage(ev.DamageHandler))
                {
                    LibraryLabAPI.LogDebug(
                        "PlayerDamageHandler.Death",
                        "Death not caused by SCP-575. Ignoring.");
                    return;
                }

                LibraryLabAPI.LogDebug(
                    "PlayerDamageHandler.Death",
                    $"Death confirmed from {Scp575DamageSystem.IdentifierName}.");
            }
            catch (Exception ex)
            {
                LibraryLabAPI.LogError(
                    "PlayerDamageHandler.Death",
                    $"Error while processing PlayerDeath: {ex}");
            }
        }
    }
}