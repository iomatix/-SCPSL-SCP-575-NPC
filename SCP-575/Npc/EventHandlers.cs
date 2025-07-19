namespace SCP_575.Npc
{
    using System;
    using System.Collections.Generic;
    using MEC;
    using SCP_575.ConfigObjects;
    using SCP_575.Shared;

    /// <summary>
    /// Handles server events related to SCP-575 NPC behavior, managing coroutines and interactions with game mechanics.
    /// </summary>
    public class EventHandlers
    {
        private readonly Plugin _plugin;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventHandlers"/> class.
        /// </summary>
        /// <param name="plugin">The plugin instance providing access to configuration and NPC methods.</param>
        public EventHandlers(Plugin plugin)
        {
            _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin), "Plugin instance cannot be null.");
        }

        /// <summary>
        /// Gets the NPC configuration from the plugin.
        /// </summary>
        private NpcConfig Config => _plugin.Config.NpcConfig;

        /// <summary>
        /// Gets or sets the list of active coroutine handles for managing SCP-575 behaviors.
        /// </summary>
        public List<CoroutineHandle> Coroutines { get; set; } = new List<CoroutineHandle>();

        /// <summary>
        /// Handles the round start event, determining whether to spawn the SCP-575 NPC and start related coroutines.
        /// </summary>
        public void OnRoundStart()
        {
            var roll = Library_ExiledAPI.Loader_Random_Next(100);
            Library_ExiledAPI.LogDebug("SCP-575.Npc.EventHandlers", $"OnRoundStart: SpawnChance Roll = {roll}, EnableKeter = {Config.EnableKeter}");

            if (roll <= Config.SpawnChance)
            {
                Library_ExiledAPI.LogDebug("SCP-575.Npc.EventHandlers", "OnRoundStart: SCP-575 NPC spawning due to roll being within spawn chance.");
                Coroutines.Add(Timing.RunCoroutine(_plugin.Npc.Methods.RunBlackoutTimer()));

                if (Config.EnableKeter)
                {
                    Library_ExiledAPI.LogDebug("SCP-575.Npc.EventHandlers", "OnRoundStart: Keter mode enabled, starting Keter damage coroutine.");
                    Coroutines.Add(Timing.RunCoroutine(_plugin.Npc.Methods.KeterDamage(), tag: "SCP575keter"));
                }
            }
        }

        /// <summary>
        /// Handles the round end event, disabling the SCP-575 NPC and cleaning up all active coroutines.
        /// </summary>
        /// <param name="ev">The event arguments for the round end event.</param>
        public void OnRoundEnd(Exiled.Events.EventArgs.Server.RoundEndedEventArgs ev)
        {
            _plugin.Npc.Methods.Disable();
            foreach (CoroutineHandle handle in Coroutines)
            {
                Timing.KillCoroutines(handle);
            }
            Coroutines.Clear();
        }

        /// <summary>
        /// Handles the waiting for players event, disabling the SCP-575 NPC and cleaning up all active coroutines.
        /// </summary>
        public void OnWaitingPlayers()
        {
            _plugin.Npc.Methods.Disable();
            foreach (CoroutineHandle handle in Coroutines)
            {
                Timing.KillCoroutines(handle);
            }
            Coroutines.Clear();
        }

        /// <summary>
        /// Handles the generator activation event, enabling lights in the affected room and its neighbors,
        /// playing a global sound, and triggering SCP-575 behavior if all generators are active.
        /// </summary>
        /// <param name="ev">The event arguments for the generator activation event.</param>
        public void OnGeneratorActivated(LabApi.Events.Arguments.ServerEvents.GeneratorActivatedEventArgs ev)
        {
            // Convert the LabAPI room to an Exiled room for compatibility
            Exiled.API.Features.Room exiledRoom = Library_ExiledAPI.ToExiledRoom(ev.Generator.Room);
            Library_ExiledAPI.LogDebug("OnGeneratorActivated", $"Generator activated in room: {exiledRoom.Name}");

            // Enable and flicker lights in the room and its neighbors
            Library_ExiledAPI.EnableAndFlickerRoomAndNeighborLights(exiledRoom);

            // Play a global angry sound as a creepy audio cue
            AudioManager.PlayGlobalAngrySound();

            // Check if all generators are engaged to trigger SCP-575 behavior
            if (_plugin.Npc.Methods.AreAllGeneratorsEngaged())
            {
                Timing.CallDelayed(3.75f, () =>
                {
                    AudioManager.PlayGlobalDyingSound();
                });
                if (Library_LabAPI.NpcConfig.IsNpcKillable)
                {
                    _plugin.Npc.Methods.Kill575();
                }
                else
                {
                    _plugin.Npc.Methods.Reset575();
                }
            }
        }

        /// <summary>
        /// Handles the explosion spawned event, enabling lights and playing a global sound 
        /// if a dangerous projectile explodes in a dark room during an active blackout.
        /// </summary>
        /// <param name="ev">The event arguments for the explosion spawned event.</param>
        public void OnExplosionSpawned(LabApi.Events.Arguments.ServerEvents.ExplosionSpawnedEventArgs ev)
        {
            if (ev.ExplosionType == null)
            {
                Library_ExiledAPI.LogDebug("OnExplosionSpawned", "Explosion event had no explosion type data.");
                return;
            }

            Scp575Helpers.Scp575ImpactType impactType = Scp575Helpers.ClassifyExplosionImpact(ev.ExplosionType);
            if (impactType != Scp575Helpers.Scp575ImpactType.Dangerous)
                return;

            Exiled.API.Features.Room room = Library_ExiledAPI.GetRoomAtPosition(ev.Position);
            if (room == null || !room.AreLightsOff || !_plugin.Npc.Methods.IsBlackoutActive)
                return;

            Library_ExiledAPI.LogDebug("OnExplosionSpawned", $"Dangerous explosive used in dark SCP-575 room: {room.Name}");
            Library_ExiledAPI.EnableAndFlickerRoomAndNeighborLights(room);
            AudioManager.PlayGlobalAngrySound();
        }

        /// <summary>
        /// Handles the projectile explosion event, disabling or enabling lights 
        /// depending on the grenade type during a blackout.
        /// </summary>
        /// <param name="ev">The event arguments for the projectile explosion event.</param>
        public void OnProjectileExploded(LabApi.Events.Arguments.ServerEvents.ProjectileExplodedEventArgs ev)
        {
            if (ev.TimedGrenade == null)
            {
                Library_ExiledAPI.LogDebug("OnProjectileExploded", "Explosion event had no grenade data.");
                return;
            }

            var impact = Scp575Helpers.ClassifyProjectileImpact(ev.TimedGrenade);

            Exiled.API.Features.Room room = Library_ExiledAPI.GetRoomAtPosition(ev.Position);
            if (room == null)
                return;

            switch (impact)
            {
                case Scp575Helpers.Scp575ImpactType.Helpful:
                    Library_ExiledAPI.LogDebug("OnProjectileExploded", $"SCP2176 used in room: {room.Name}");
                    Library_ExiledAPI.DisableAndFlickerRoomAndNeighborLights(room);
                    return;

                case Scp575Helpers.Scp575ImpactType.Dangerous:
                    if (!room.AreLightsOff || !_plugin.Npc.Methods.IsBlackoutActive)
                        return;

                    Library_ExiledAPI.LogDebug("OnProjectileExploded", $"Dangerous grenade used in dark SCP-575 room: {room.Name}");
                    Library_ExiledAPI.EnableAndFlickerRoomAndNeighborLights(room);
                    AudioManager.PlayGlobalAngrySound();
                    return;

                case Scp575Helpers.Scp575ImpactType.Neutral:
                case Scp575Helpers.Scp575ImpactType.Unknown:
                    return;
            }
        }
    }
}