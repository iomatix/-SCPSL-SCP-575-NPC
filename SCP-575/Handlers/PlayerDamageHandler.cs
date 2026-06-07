namespace SCP_575.Handlers
{
    using LabApi.Events.Arguments.PlayerEvents;
    using LabApi.Events.Arguments.ServerEvents;
    using LabApi.Events.CustomHandlers;
    using MEC;
    using SCP_575.Shared;
    using SCP_575.Shared.Audio.Enums;
    using SCP_575.Systems;
    using SCP_575.Shared;
    using System;

    /// <summary>
    /// Intercepts severe structural trauma vectors directed at human actors to evaluate 
    /// if the source matches anomalous physical manifestations, governing post-mortem kinetic overrides.
    /// </summary>
    public class PlayerDamageHandler : CustomEventsHandler
    {
        private readonly Plugin _plugin;
        private const string ItemPhysicsTag = CoroutineTags.ItemPhysics;

        // Anti-spam protection: tracks the exact timestamp of the last executed non-lethal attack audio.
        private DateTime _lastAttackAudioTime = DateTime.MinValue;
        private readonly TimeSpan _attackAudioCooldown = TimeSpan.FromSeconds(1.5);

        public PlayerDamageHandler(Plugin plugin)
        {
            _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
        }

        #region Lifecycle Cleanup

        public override void OnServerRoundEnded(RoundEndedEventArgs ev) => Timing.KillCoroutines(ItemPhysicsTag);
        public override void OnServerWaitingForPlayers() => Timing.KillCoroutines(ItemPhysicsTag);

        #endregion

        #region Event Handlers

        /// <summary>
        /// Intercepts the final physiological state of a human actor before death confirmation,
        /// triggering asynchronous inventory propulsion calculations and execution of the signature lethal strike audio.
        /// </summary>
        /// <param name="ev">The operational state arguments containing player data and lethal damage tracking signatures.</param>
        public override void OnPlayerDying(PlayerDyingEventArgs ev)
        {
            if (!_plugin.IsEventActive) return;

            try
            {
                if (ev?.Player == null || ev.DamageHandler == null)
                    return;

                if (!Scp575DamageSystem.IsScp575Damage(ev.DamageHandler))
                    return;

                LibraryLabAPI.LogDebug("PlayerDamageHandler", $"Death confirmed from {Scp575DamageSystem.IdentifierName} for {ev.Player.Nickname}. Triggering item physics and lethal soundscape.");

                // Executing ShadowStrike exactly on death provides a clean, deterministic acoustic punctuation
                // to the kill event, syncing perfectly with the physical item scattering.
                _plugin.AudioManager.PlayAudioAtPosition(AudioKey.ShadowStrike, ev.Player.Position);

                // Offloading item kinetic scatter behavior to an isolated coroutine guarantees 
                // the main physics thread is not blocked during complex kinematic evaluations.
                Timing.RunCoroutine(Scp575DamageSystem.DropAndPushItems(ev.Player), ItemPhysicsTag);
            }
            catch (Exception ex)
            {
                LibraryLabAPI.LogError("PlayerDamageHandler", $"Error while processing PlayerDying: {ex}");
            }
        }

        /// <summary>
        /// Intercepts physical attacks from both playable anomalous entities and environmental 
        /// shadow manifestations, enforcing instant psychological trauma.
        /// </summary>
        public override void OnPlayerHurting(PlayerHurtingEventArgs ev)
        {
            if (!_plugin.IsEventActive || ev?.Player == null)
                return;

            bool isAnomalousAttack = false;

            if (ev.Attacker != null && ev.Attacker.IsSCP)
            {
                isAnomalousAttack = true;
            }
            else if (ev.DamageHandler != null && Scp575DamageSystem.IsScp575Damage(ev.DamageHandler))
            {
                isAnomalousAttack = true;
            }

            if (isAnomalousAttack)
            {
                float dropAmount = _plugin.Config.SanityConfig.ScpHitSanityDrop;
                if (dropAmount <= 0f) return;

                float newSanity = _plugin.SanityEventHandler.ChangeSanityValue(ev.Player, -dropAmount);

                LibraryLabAPI.LogDebug("PlayerDamageHandler", $"Anomalous trauma inflicted on {ev.Player.Nickname}. Sanity slashed by {dropAmount}. New sanity: {newSanity}");

                _plugin.SanityEventHandler.ApplyStageEffects(ev.Player);
            }
        }

        /// <summary>
        /// Optonal implementation layout for standard hurting events. 
        /// Uses a hard temporal cooldown to strictly prevent acoustic repetition and audio driver clipping.
        /// </summary>
        public void ProcessHurtAudioAntiSpam(UnityEngine.Vector3 targetPosition)
        {
            if (DateTime.UtcNow - _lastAttackAudioTime < _attackAudioCooldown)
                return;

            _lastAttackAudioTime = DateTime.UtcNow;
            _plugin.AudioManager.PlayAudioAtPosition(AudioKey.ShadowStrike, targetPosition);
        }

        #endregion
    }
}