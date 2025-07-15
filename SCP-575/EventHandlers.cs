namespace SCP_575
{
    using MEC;
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
            if (ev.DamageHandler is Scp575DamageHandler scp575Handler)
            {
                Library_ExiledAPI.LogDebug("OnPlayerHurting", $"The event was caused by {Scp575DamageHandler.IdentifierName}");

            }

        }

        public void OnPlayerHurt(LabApi.Events.Arguments.PlayerEvents.PlayerHurtEventArgs ev)
        {
            Library_ExiledAPI.LogDebug("Catched Event", $"OnPlayerHurt: {ev.Attacker?.Nickname ?? "SCP-575 NPC"} -> {ev.Player.Nickname}");

            if (ev.DamageHandler is Scp575DamageHandler scp575Handler)
            {
                Library_ExiledAPI.LogDebug("OnPlayerHurt", $"The event was caused by {Scp575DamageHandler.IdentifierName}");

                LabApi.Features.Wrappers.Player player = ev.Player;
                
                // Play horror sound effect
                AudioManager.PlayDamagedScream(player, isKill: false, customLifespan: 15f);
            }

        }

        public void OnPlayerDying(LabApi.Events.Arguments.PlayerEvents.PlayerDyingEventArgs ev)
        {
            Library_ExiledAPI.LogDebug("Catched Event", $"OnPlayerDying: {ev.Player.Nickname}");

            if (ev.DamageHandler is Scp575DamageHandler scp575Handler)
            {
                Library_ExiledAPI.LogDebug("OnPlayerDying", $"The event was caused by {Scp575DamageHandler.IdentifierName}");

                LabApi.Features.Wrappers.Player player = ev.Player;

                // Effects
                AudioManager.PlayDamagedScream(player, isKill: true, customLifespan: 15f);
                Timing.RunCoroutine(_methods.DropAndPushItems(player, scp575Handler));

            }
        }

        public void OnSpawningRagdoll(LabApi.Events.Arguments.PlayerEvents.PlayerSpawningRagdollEventArgs ev)
        {
            Library_ExiledAPI.LogDebug("Catched Event", $"OnSpawningRagdoll: {ev.Player.Nickname}");

            if (ev.DamageHandler is not Scp575DamageHandler scp575Handler)
                return;

            Library_ExiledAPI.LogDebug("OnSpawningRagdoll", $"The event was caused by {Scp575DamageHandler.IdentifierName}");

            try
            {
                // Set proper ragdoll position at spawn time  
                Vector3 deathPosition = ev.Player.Position;
                ev.Ragdoll.Position = deathPosition;

                // Ensure proper rotation (face up for dramatic effect)  
                ev.Ragdoll.Rotation = Quaternion.Euler(0, ev.Player.Rotation.eulerAngles.y, 0);

                // Set ragdoll properties before spawning  
                ev.Ragdoll.Nickname = ev.Player.DisplayName;

                Library_ExiledAPI.LogDebug("OnSpawningRagdoll", $"Configured ragdoll - Position: {ev.Ragdoll.Position}, Death Position: {deathPosition}");
            }
            catch (Exception ex)
            {
                Library_ExiledAPI.LogError("OnSpawningRagdoll", $"Failed to configure ragdoll: {ex.Message}");
            }
        }

        public void OnSpawnedRagdoll(LabApi.Events.Arguments.PlayerEvents.PlayerSpawnedRagdollEventArgs ev)
        {
            Library_ExiledAPI.LogDebug("Catched Event", $"OnSpawnedRagdoll: {ev.Player.Nickname}");

            if (ev.DamageHandler is not Scp575DamageHandler scp575Handler)
                return;

            Library_ExiledAPI.LogDebug("OnSpawnedRagdoll", $"The event was caused by {Scp575DamageHandler.IdentifierName}");

            try
            {
                Scp575Helpers.RagdollProcess(ev.Ragdoll, scp575Handler);
            }
            catch (Exception ex)
            {
                Library_ExiledAPI.LogError("OnSpawnedRagdoll", $"Failed to process SCP-575 ragdoll: {ex.Message}");
            }
        }



        // Todo Turn On Lights in the room/whole heavy on generator completed, Play creepy sound audio manager in the room (angry)

        // Todo turn On ALL lights in the facility on three generators, Play creepy sound audio manager global (Dying 1)

        // Todo kill SCP 575 on kill switch for computer SCP, Play creepy sound audio manager global (Angry->Dying 2)

        // ToDo turn On lights for 5 seconds On FLASHNADE explosion in the SCP575 dark room, Play creepy sound audio manager in the room->where nade exploded (angry)

        // TODO 2176 triggers SCP-575 in the room (scream, ambient) in the room/where scp-2176 exploded

        public void OnPlayerDeath(LabApi.Events.Arguments.PlayerEvents.PlayerDeathEventArgs ev)
        {
            Library_ExiledAPI.LogDebug("Catched Event", $"OnPlayerDeath: {ev.Player.Nickname}");
            if (ev.DamageHandler is Scp575DamageHandler scp575Handler)
            {
                Library_ExiledAPI.LogDebug("OnPlayerDeath", $"The event was caused by {Scp575DamageHandler.IdentifierName}");

            }
        }

    }
}
