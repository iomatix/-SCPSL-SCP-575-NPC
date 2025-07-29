namespace SCP_575
{
    using MEC;
    using SCP_575.Handlers;
    using SCP_575.Npc;
    using SCP_575.Shared;
    using System;
    using System.Collections.Generic;
    using Systems;

    /// <summary>
    /// Handles server and player events for the SCP-575 plugin, managing blackout-related behaviors and damage interactions.
    /// </summary>
    public class EventHandler
    {
        private readonly Plugin _plugin;
        private readonly LibraryLabAPI _libraryLabAPI;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventHandler"/> class.
        /// </summary>
        /// <param name="plugin">The plugin instance providing access to configuration and NPC methods.</param>
        /// <exception cref="ArgumentNullException">Thrown when the plugin instance is null.</exception>
        public EventHandler(Plugin plugin)
        {
            _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin), "Plugin instance cannot be null.");
            _libraryLabAPI = _plugin.LibraryLabAPI;
        }

        /// <summary>
        /// Gets a value indicating whether Tesla gates are disabled during a blackout.
        /// </summary>
        public bool TeslasDisabled { get; set; }

        /// <summary>
        /// Gets a value indicating whether the nuke detonation is disabled during a blackout.
        /// </summary>
        public bool NukeDisabled { get; set; }

        /// <summary>
        /// Gets the list of active coroutine handles for managing event-related tasks.
        /// </summary>
        public List<CoroutineHandle> Coroutines { get; } = new List<CoroutineHandle>();

        /// <summary>
        /// Gets the NPC methods instance for accessing blackout and damage logic.
        /// </summary>
        private Methods Methods => _plugin.Npc.Methods;

        #region Server Events

        /// <summary>
        /// Handles the WaitingForPlayers event, initializing SCP-575 if conditions are met.
        /// </summary>
        public void OnWaitingForPlayers()
        {
            try
            {
                if (Plugin.Singleton.Config.NpcConfig == null)
                {
                    LibraryExiledAPI.LogError("OnWaitingForPlayers", "NpcConfig is null. Cannot initialize SCP-575.");
                    return;
                }

                LibraryExiledAPI.LogInfo("OnWaitingForPlayers", "SCP-575 initialized.");
                Methods.Init();

            }
            catch (Exception ex)
            {
                LibraryExiledAPI.LogError("OnWaitingForPlayers", $"Failed to handle WaitingForPlayers event: {ex.Message}\nStackTrace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Handles the RoundStarted event. Currently a placeholder for future functionality.
        /// </summary>
        public void OnRoundStarted()
        {
            try
            {
                LibraryExiledAPI.LogDebug("OnRoundStarted", "Round started. No specific actions defined for SCP-575.");
                
            }
            catch (Exception ex)
            {
                LibraryExiledAPI.LogError("OnRoundStarted", $"Failed to handle RoundStarted event: {ex.Message}\nStackTrace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Handles the RoundEnded event, cleaning up audio and coroutines.
        /// </summary>
        /// <param name="ev">The event arguments for the round end.</param>
        public void OnRoundEnded(LabApi.Events.Arguments.ServerEvents.RoundEndedEventArgs ev)
        {
            try
            {
                foreach (CoroutineHandle handle in Coroutines)
                {
                    Timing.KillCoroutines(handle);
                }
                Coroutines.Clear();
                LibraryExiledAPI.LogInfo("OnRoundEnded", "Stopped global ambience, cleaned up speakers, and cleared coroutines on round end.");
            }
            catch (Exception ex)
            {
                LibraryExiledAPI.LogError("OnRoundEnded", $"Failed to handle RoundEnded event: {ex.Message}\nStackTrace: {ex.StackTrace}");
            }
        }

        #endregion

        #region Player Events

        /// <summary>
        /// Handles the PlayerHurting event, logging and validating SCP-575 damage.
        /// </summary>
        /// <param name="ev">The event arguments for the player hurting event.</param>
        public void OnPlayerHurting(LabApi.Events.Arguments.PlayerEvents.PlayerHurtingEventArgs ev)
        {
            if (!_plugin.IsEventActive) return;
            try
            {
                if (ev?.Player == null || ev.DamageHandler == null)
                {
                    LibraryExiledAPI.LogDebug("OnPlayerHurting", "Event arguments or player is null. Skipping.");
                    return;
                }

                LibraryExiledAPI.LogDebug("OnPlayerHurting", $"Player {ev.Player.Nickname} is being hurt by {ev.Attacker?.Nickname ?? "SCP-575 NPC"}.");

                if (!Scp575DamageSystem.IsScp575Damage(ev.DamageHandler))
                {
                    LibraryExiledAPI.LogDebug("OnPlayerHurting", "Damage not caused by SCP-575. Skipping.");
                    return;
                }

                LibraryExiledAPI.LogDebug("OnPlayerHurting", $"Damage confirmed from {Scp575DamageSystem.IdentifierName}.");
            }
            catch (Exception ex)
            {
                LibraryExiledAPI.LogError("OnPlayerHurting", $"Failed to handle PlayerHurting event for {ev?.Player?.Nickname ?? "unknown"}: {ex.Message}\nStackTrace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Handles the PlayerHurt event, playing audio effects for SCP-575 damage.
        /// </summary>
        /// <param name="ev">The event arguments for the player hurt event.</param>
        public void OnPlayerHurt(LabApi.Events.Arguments.PlayerEvents.PlayerHurtEventArgs ev)
        {
            if (!_plugin.IsEventActive) return;
            try
            {
                if (ev?.Player == null || ev.DamageHandler == null)
                {
                    LibraryExiledAPI.LogDebug("OnPlayerHurt", "Event arguments or player is null. Skipping.");
                    return;
                }

                LibraryExiledAPI.LogDebug("OnPlayerHurt", $"Player {ev.Player.Nickname} was hurt by {ev.Attacker?.Nickname ?? "SCP-575 NPC"}.");

                if (!Scp575DamageSystem.IsScp575Damage(ev.DamageHandler))
                {
                    LibraryExiledAPI.LogDebug("OnPlayerHurt", "Damage not caused by SCP-575. Skipping.");
                    return;
                }

                LibraryExiledAPI.LogDebug("OnPlayerHurt", $"Damage confirmed from {Scp575DamageSystem.IdentifierName}.");
            }
            catch (Exception ex)
            {
                LibraryExiledAPI.LogError("OnPlayerHurt", $"Failed to handle PlayerHurt event for {ev?.Player?.Nickname ?? "unknown"}: {ex.Message}\nStackTrace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Handles the PlayerDying event, applying audio and item effects for SCP-575 kills.
        /// </summary>
        /// <param name="ev">The event arguments for the player dying event.</param>
        public void OnPlayerDying(LabApi.Events.Arguments.PlayerEvents.PlayerDyingEventArgs ev)
        {
            if (!_plugin.IsEventActive) return;
            try
            {
                if (ev?.Player == null || ev.DamageHandler == null)
                {
                    LibraryExiledAPI.LogDebug("OnPlayerDying", "Event arguments or player is null. Skipping.");
                    return;
                }

                LibraryExiledAPI.LogDebug("OnPlayerDying", $"Player {ev.Player.Nickname} is dying.");

                if (!Scp575DamageSystem.IsScp575Damage(ev.DamageHandler))
                {
                    LibraryExiledAPI.LogDebug("OnPlayerDying", "Damage not caused by SCP-575. Skipping.");
                    return;
                }

                LibraryExiledAPI.LogDebug("OnPlayerDying", $"Death confirmed from {Scp575DamageSystem.IdentifierName}.");
                Coroutines.Add(Timing.RunCoroutine(Scp575DamageSystem.DropAndPushItems(ev.Player)));
            }
            catch (Exception ex)
            {
                LibraryExiledAPI.LogError("OnPlayerDying", $"Failed to handle PlayerDying event for {ev?.Player?.Nickname ?? "unknown"}: {ex.Message}\nStackTrace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Handles the PlayerDeath event, logging SCP-575-related deaths.
        /// </summary>
        /// <param name="ev">The event arguments for the player death event.</param>
        public void OnPlayerDeath(LabApi.Events.Arguments.PlayerEvents.PlayerDeathEventArgs ev)
        {
            if (!_plugin.IsEventActive) return;
            try
            {
                if (ev?.Player == null || ev.DamageHandler == null)
                {
                    LibraryExiledAPI.LogDebug("OnPlayerDeath", "Event arguments or player is null. Skipping.");
                    return;
                }

                LibraryExiledAPI.LogDebug("OnPlayerDeath", $"Player {ev.Player.Nickname} died.");

                if (!Scp575DamageSystem.IsScp575Damage(ev.DamageHandler))
                {
                    LibraryExiledAPI.LogDebug("OnPlayerDeath", "Death not caused by SCP-575. Skipping.");
                    return;
                }

                LibraryExiledAPI.LogDebug("OnPlayerDeath", $"Death confirmed from {Scp575DamageSystem.IdentifierName}.");
            }
            catch (Exception ex)
            {
                LibraryExiledAPI.LogError("OnPlayerDeath", $"Failed to handle PlayerDeath event for {ev?.Player?.Nickname ?? "unknown"}: {ex.Message}\nStackTrace: {ex.StackTrace}");
            }
        }

        #endregion

        #region Ragdoll Events

        /// <summary>
        /// Handles the SpawnedRagdoll event, processing ragdolls for SCP-575 kills.
        /// </summary>
        /// <param name="ev">The event arguments for the ragdoll spawn event.</param>
        public void OnSpawnedRagdoll(Exiled.Events.EventArgs.Player.SpawnedRagdollEventArgs ev)
        {
            if (!_plugin.IsEventActive) return;
            try
            {
                if (ev?.Player == null || ev.Ragdoll == null || ev.DamageHandlerBase == null)
                {
                    LibraryExiledAPI.LogDebug("OnSpawnedRagdoll", "Event arguments, player, or ragdoll is null. Skipping.");
                    return;
                }

                LibraryExiledAPI.LogDebug("OnSpawnedRagdoll", $"Ragdoll spawned for player: {ev.Player.Nickname} at {ev.Ragdoll.Position}.");

                if (!Scp575DamageSystem.IsScp575Damage(ev.DamageHandlerBase))
                {
                    LibraryExiledAPI.LogDebug("OnSpawnedRagdoll", "Ragdoll not caused by SCP-575. Skipping.");
                    return;
                }

                LibraryExiledAPI.LogDebug("OnSpawnedRagdoll", $"Ragdoll caused by {Scp575DamageSystem.IdentifierName}.");

                if (_plugin.Config.NpcConfig.DisableRagdolls)
                {
                    LibraryExiledAPI.LogDebug("OnSpawnedRagdoll", "DisableRagdolls is true. Destroying ragdoll.");
                    ev.Ragdoll.Destroy();
                }
                else
                {
                    LibraryExiledAPI.LogDebug("OnSpawnedRagdoll", "DisableRagdolls is false. Processing ragdoll for skeleton spawn.");
                    Scp575DamageSystem.RagdollProcessor(ev.Player, ev.Ragdoll);
                }
            }
            catch (Exception ex)
            {
                LibraryExiledAPI.LogError("OnSpawnedRagdoll", $"Failed to handle SpawnedRagdoll event for {ev?.Player?.Nickname ?? "unknown"}: {ex.Message}\nStackTrace: {ex.StackTrace}");
            }
        }

        #endregion
    }
}