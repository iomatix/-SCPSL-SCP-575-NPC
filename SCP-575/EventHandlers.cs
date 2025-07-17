namespace SCP_575
{
    using MEC;
    using PlayerStatsSystem;
    using SCP_575.ConfigObjects;
    using SCP_575.Npc;
    using Shared;
    using System;
    using System.Collections.Generic;
    using UnityEngine;
    using static PlayerStatsSystem.DamageHandlerBase;

    public class EventHandlers
    {
        private readonly Plugin _plugin;


        public EventHandlers(Plugin plugin) => _plugin = plugin;

        private Methods _methods => _plugin.Npc.Methods;

        public bool TeslasDisabled = false;
        public bool NukeDisabled = false;
        public List<CoroutineHandle> Coroutines = new List<CoroutineHandle>();


        public void OnWaitingForPlayers()
        {
            if (_plugin.Config.SpawnType == InstanceType.Npc || (_plugin.Config.SpawnType == InstanceType.Random && Library_ExiledAPI.Loader_Random_Next(100) > 55))
            {
                _plugin.Npc.Methods.Init();
            }
            else
            {
                //_plugin.Playable.Methods.Init();
            }


        }
        public void OnRoundStarted()
        {

        }

        public void OnRoundEnded(LabApi.Events.Arguments.ServerEvents.RoundEndedEventArgs ev)
        {
            try
            {
                AudioManager.CleanupAllSpeakers();
                Library_ExiledAPI.LogDebug("OnRoundEnded", "Stopped global ambience and cleaned up speakers on round end");
            }
            catch (Exception ex)
            {
                Library_ExiledAPI.LogError("OnRoundEnded", $"Failed to stop global ambience: {ex.Message}\nStackTrace: {ex.StackTrace}");
            }
        }

        public void OnPlayerHurting(LabApi.Events.Arguments.PlayerEvents.PlayerHurtingEventArgs ev)
        {
            Library_ExiledAPI.LogDebug("Catched Event", $"OnPlayerHurting: {ev.Attacker?.Nickname ?? "SCP-575 NPC"} -> {ev.Player.Nickname}");
            if (!Scp575DamageSystem.IsScp575Damage(ev.DamageHandler))
                return;

            Library_ExiledAPI.LogDebug("OnPlayerHurting", $"The event was caused by {Scp575DamageSystem.IdentifierName}");



        }

        public void OnPlayerHurt(LabApi.Events.Arguments.PlayerEvents.PlayerHurtEventArgs ev)
        {
            Library_ExiledAPI.LogDebug("Catched Event", $"OnPlayerHurt: {ev.Attacker?.Nickname ?? "SCP-575 NPC"} -> {ev.Player.Nickname}");
            if (!Scp575DamageSystem.IsScp575Damage(ev.DamageHandler))
                return;


            Library_ExiledAPI.LogDebug("OnPlayerHurt", $"The event was caused by {Scp575DamageSystem.IdentifierName}");

            LabApi.Features.Wrappers.Player player = ev.Player;

            // Play horror sound effect
            AudioManager.PlayDamagedScream(player, isKill: false, customLifespan: 15f);


        }

        public void OnPlayerDying(LabApi.Events.Arguments.PlayerEvents.PlayerDyingEventArgs ev)
        {
            Library_ExiledAPI.LogDebug("Catched Event", $"OnPlayerDying: {ev.Player.Nickname}");
            if (!Scp575DamageSystem.IsScp575Damage(ev.DamageHandler))
                return;

            Library_ExiledAPI.LogDebug("OnPlayerDying", $"The event was caused by {Scp575DamageSystem.IdentifierName}");

            LabApi.Features.Wrappers.Player player = ev.Player;

            // Effects
            AudioManager.PlayDamagedScream(player, isKill: true, customLifespan: 15f);
            Timing.RunCoroutine(Scp575DamageSystem.DropAndPushItems(player));


        }


        // Use Exiled's ragdoll event instead  
        public void OnSpawnedRagdoll(Exiled.Events.EventArgs.Player.SpawnedRagdollEventArgs ev)
        {
            Library_ExiledAPI.LogDebug("Catched Event", $"OnSpawnedRagdoll: {ev.Player.Nickname}");

            if (!Scp575DamageSystem.IsScp575Damage(ev.DamageHandlerBase))
                return;

            Library_ExiledAPI.LogDebug("OnSpawnedRagdoll", $"The event was caused by {Scp575DamageSystem.IdentifierName}");



            Library_ExiledAPI.LogDebug("OnSpawnedRagdoll", $"The event was called by {ev.Ragdoll.Nickname} Ragdoll at {ev.Ragdoll.Position}, From {ev.Player.Nickname} at {ev.Player.Position}");
            Scp575DamageSystem.RagdollProcessor(ev.Player, ev.Ragdoll);

        }

        public void OnPlayerDeath(LabApi.Events.Arguments.PlayerEvents.PlayerDeathEventArgs ev)
        {
            Library_ExiledAPI.LogDebug("Catched Event", $"OnPlayerDeath: {ev.Player.Nickname}");
            if (!Scp575DamageSystem.IsScp575Damage(ev.DamageHandler))
                return;

            Library_ExiledAPI.LogDebug("OnPlayerDeath", $"The event was caused by {Scp575DamageSystem.IdentifierName}");

            
        }

    }
}
