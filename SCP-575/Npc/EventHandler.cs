namespace SCP_575.Npc
{
    using LabApi.Features.Wrappers;
    using MEC;
    using SCP_575.Shared;
    using SCP_575.Shared.Audio.Enums;
    using System;
    using System.Collections.Generic;
    using Types;
    using UnityEngine;

    // Todo Revork Event Handling to LabAPIs CustomHandlers

    /// <summary>
    /// Handles events for the SCP-575 plugin, including explosion and projectile events during blackouts.
    /// </summary>
    public class EventHandler
    {
        private readonly Plugin _plugin;
        private readonly LibraryLabAPI _libraryLabAPI;
        private readonly Config _config;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventHandler"/> class.
        /// </summary>
        /// <param name="plugin">The SCP-575 plugin instance.</param>
        public EventHandler(Plugin plugin)
        {
            _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin), "Plugin instance cannot be null.");
            _libraryLabAPI = _plugin.LibraryLabAPI;
            _config = _plugin.Config;
        }

        /// <summary>
        /// Gets or sets the list of active coroutine handles for managing SCP-575 behaviors.
        /// </summary>
        public List<CoroutineHandle> Coroutines { get; set; } = new List<CoroutineHandle>();

        /// <summary>
        /// Handles the RoundStarted event, initializing SCP-575 blackout mechanics.
        /// </summary>
        public void OnRoundStart()
        {
            try
            {
                _plugin.IsEventActive = false;

                
                float roll = UnityEngine.Random.Range(0f,100f);
                LibraryExiledAPI.LogDebug("SCP-575.Npc.EventHandlers", $"OnRoundStart: SpawnChance Roll = {roll}");

                if (roll <= _config.BlackoutConfig.EventChance)
                {
                    _plugin.IsEventActive = true;
                    LibraryExiledAPI.LogDebug("SCP-575.Npc.EventHandlers", "SCP-575 NPC spawning due to roll being within spawn chance.");

                    Timing.KillCoroutines("SCP575keter");
                    Coroutines.RemoveAll(handle => handle.IsRunning);
                    Coroutines.Add(Timing.RunCoroutine(_plugin.Npc.Methods.RunBlackoutTimer()));

                    Timing.KillCoroutines("SCP575keter");
                    Coroutines.RemoveAll(handle => handle.IsRunning);
                    _plugin.Npc.Methods.StartKeterAction();

                    if (_plugin.SanityEventHandler.SanityDecayCoroutine.IsRunning)
                        Timing.KillCoroutines(_plugin.SanityEventHandler.SanityDecayCoroutine);
                    _plugin.SanityEventHandler.SanityDecayCoroutine = Timing.RunCoroutine(_plugin.SanityEventHandler.HandleSanityDecay());

                    foreach (var player in LabApi.Features.Wrappers.Player.List)
                    {
                        if (_plugin.SanityEventHandler.IsValidPlayer(player))
                        {
                            _plugin.SanityEventHandler.SetSanity(player, _plugin.Config.SanityConfig.InitialSanity);
                        }
                    }
                }
                else
                {
                    _plugin.IsEventActive = false;
                    Timing.KillCoroutines("SCP575keter");
                    Coroutines.RemoveAll(handle => handle.IsRunning);
                }
            }
            catch (Exception ex)
            {
                LibraryExiledAPI.LogError("EventHandler.OnRoundStart", $"Failed to handle RoundStarted event: {ex.Message}\nStackTrace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Handles the RoundEnded event, cleaning up SCP-575 blackout mechanics.
        /// </summary>
        /// <param name="ev">The event arguments for the round ended event.</param>
        public void OnRoundEnd(LabApi.Events.Arguments.ServerEvents.RoundEndedEventArgs ev)
        {
            _plugin.IsEventActive = false; // Explicitly disable event
            _plugin.Npc.Methods.StopKeterAction();
            _plugin.Npc.Methods.Disable();
            foreach (CoroutineHandle handle in Coroutines)
            {
                Timing.KillCoroutines(handle);
            }
            Coroutines.Clear();
            LibraryExiledAPI.LogInfo("OnRoundEnd", "SCP-575 event disabled, coroutines cleared.");
        }

        /// <summary>
        /// Handles the waiting for players event, disabling the SCP-575 NPC and cleaning up all active coroutines.
        /// </summary>
        public void OnWaitingPlayers()
        {
            _plugin.IsEventActive = false; // Explicitly disable event
            _plugin.Npc.Methods.StopKeterAction();
            _plugin.Npc.Methods.Disable();
            foreach (CoroutineHandle handle in Coroutines)
            {
                Timing.KillCoroutines(handle);
            }
            Coroutines.Clear();
            LibraryExiledAPI.LogInfo("OnWaitingPlayers", "SCP-575 event disabled, coroutines cleared.");
        }

        /// <summary>
        /// Handles the GeneratorActivated event, enabling lights in the room if a generator is activated during a blackout.
        /// </summary>
        /// <param name="ev">The event arguments for the generator activated event.</param>
        public void OnGeneratorActivated(LabApi.Events.Arguments.ServerEvents.GeneratorActivatedEventArgs ev)
        {
            if (!_plugin.IsEventActive) return;
            try
            {
                if (ev?.Generator == null)
                {
                    LibraryExiledAPI.LogDebug("EventHandler.OnGeneratorActivated", "Generator event or generator is null. Skipping.");
                    return;
                }

                
                Room room = _libraryLabAPI.GetRoomAtPosition(ev.Generator.Position);
                if (room == null)
                {
                    LibraryExiledAPI.LogDebug("EventHandler.OnGeneratorActivated", "No room data found for generator position.");
                    return;
                }

                bool isScp575Present = _plugin.Npc.Methods.IsBlackoutActive;
                if (!isScp575Present)
                {
                    LibraryExiledAPI.LogDebug("EventHandler.OnGeneratorActivated", $"Generator activated in room {room.Name}, but no blackout is active.");
                    return;
                }

                LibraryExiledAPI.LogInfo("EventHandler.OnGeneratorActivated", $"Generator activated in SCP-575 room: {room.Name}");
                _libraryLabAPI.EnableAndFlickerRoomAndNeighborLights(room);

                // Play a global angry sound as a creepy audio cue
                _plugin.AudioManager.PlayGlobalAudioAutoManaged(AudioKey.ScreamAngry, lifespan: 25f);

                // Check if all generators are engaged to trigger SCP-575 behavior
                if (_plugin.Npc.Methods.AreAllGeneratorsEngaged())
                {
                    Timing.CallDelayed(3.75f, () =>
                    {
                        _plugin.AudioManager.PlayGlobalAudioAutoManaged(AudioKey.ScreamDying, lifespan: 25f);
                    });
                    if (_plugin.Config.NpcConfig.IsNpcKillable)
                    {
                        _plugin.Npc.Methods.Kill575();
                    }
                    else
                    {
                        _plugin.Npc.Methods.Reset575();
                    }
                }
            }
            catch (Exception ex)
            {
                LibraryExiledAPI.LogError("EventHandler.OnGeneratorActivated", $"Failed to handle GeneratorActivated event: {ex.Message}\nStackTrace: {ex.StackTrace}");
            }
        }



        /// <summary>
        /// Handles the ExplosionSpawned event, enabling lights and playing a global sound if a dangerous explosion occurs in a dark room during an active blackout.
        /// </summary>
        /// <param name="ev">The event arguments for the explosion spawned event.</param>
        public void OnExplosionSpawned(LabApi.Events.Arguments.ServerEvents.ExplosionSpawnedEventArgs ev)
        {
            if (!_plugin.IsEventActive) return;
            HandleExplosionEvent(ev, null);
        }

        /// <summary>
        /// Handles the ProjectileExploded event, enabling or disabling lights based on the projectile type during a blackout.
        /// </summary>
        /// <param name="ev">The event arguments for the projectile explosion event.</param>
        public void OnProjectileExploded(LabApi.Events.Arguments.ServerEvents.ProjectileExplodedEventArgs ev)
        {
            if (!_plugin.IsEventActive) return;
            HandleExplosionEvent(null, ev);
        }

        /// <summary>
        /// Handles explosion-related events (ExplosionSpawned or ProjectileExploded), enabling or disabling lights and playing sounds based on the impact type.
        /// </summary>
        /// <param name="explosionEv">The ExplosionSpawned event arguments, if available.</param>
        /// <param name="projectileEv">The ProjectileExploded event arguments, if available.</param>
        private void HandleExplosionEvent(LabApi.Events.Arguments.ServerEvents.ExplosionSpawnedEventArgs explosionEv, LabApi.Events.Arguments.ServerEvents.ProjectileExplodedEventArgs projectileEv)
        {
            try
            {
                Vector3 position;
                ScpProjectileImpactType.ProjectileImpactType impactType;

                if (explosionEv != null)
                {
                    if (explosionEv.Position == null)
                    {
                        LibraryExiledAPI.LogDebug("EventHandler.OnExplosionSpawned", "Explosion event position is null. Skipping.");
                        return;
                    }
                    if (explosionEv.ExplosionType == null)
                    {
                        LibraryExiledAPI.LogWarn("EventHandler.OnExplosionSpawned", "Explosion event had no explosion type data.");
                        return;
                    }

                    position = explosionEv.Position;
                    impactType = ScpProjectileImpactType.ClassifyExplosionImpact(explosionEv.ExplosionType);
                    LibraryExiledAPI.LogDebug("EventHandler.OnExplosionSpawned", $"Impact type: {impactType}");
                }
                else if (projectileEv != null)
                {
                    if (projectileEv.Position == null)
                    {
                        LibraryExiledAPI.LogDebug("EventHandler.OnProjectileExploded", "Explosion event position is null. Skipping.");
                        return;
                    }
                    if (projectileEv.TimedGrenade == null)
                    {
                        LibraryExiledAPI.LogDebug("EventHandler.OnProjectileExploded", "Explosion event had no grenade data.");
                        return;
                    }

                    position = projectileEv.Position;
                    impactType = ScpProjectileImpactType.ClassifyProjectileImpact(projectileEv.TimedGrenade);
                    LibraryExiledAPI.LogDebug("EventHandler.OnProjectileExploded", $"Impact type: {impactType} (int={(int)impactType})");
                }
                else
                {
                    LibraryExiledAPI.LogWarn("EventHandler.HandleExplosionEvent", "Both explosion and projectile event arguments are null. Skipping.");
                    return;
                }

                Room room = _libraryLabAPI.GetRoomAtPosition(position);
                if (room == null)
                {
                    LibraryExiledAPI.LogDebug("EventHandler.HandleExplosionEvent", "Explosion event had no room data.");
                    return;
                }

                bool isScp575Present = _plugin.Npc.Methods.IsBlackoutActive;
                switch (impactType)
                {
                    case ScpProjectileImpactType.ProjectileImpactType.Helpful:
                        LibraryExiledAPI.LogInfo("EventHandler.HandleExplosionEvent", $"Helpful impact type used in room: {room.Name}");

                        _libraryLabAPI.DisableRoomAndNeighborLights(room);
                        _plugin.AudioManager.PlayGlobalAudioAutoManaged(AudioKey.WhispersBang, lifespan: 25f);
                        _plugin.AudioManager.PlayAmbience();
                        break;

                    case ScpProjectileImpactType.ProjectileImpactType.Dangerous:
                        if (room.LightController.LightsEnabled || !isScp575Present)
                        {
                            LibraryExiledAPI.LogDebug("EventHandler.HandleExplosionEvent", $"Event in safe room, Lights are On or SCP-575 is not active. LightsOff: {room.AreLightsOff}, IsBlackoutActive: {isScp575Present}");
                            return;
                        }
                        LibraryExiledAPI.LogInfo("EventHandler.HandleExplosionEvent", $"Dangerous explosive used in dark SCP-575 room: {room.Name}");
                        _libraryLabAPI.EnableAndFlickerRoomAndNeighborLights(room);
                        _plugin.AudioManager.PlayGlobalAudioAutoManaged(AudioKey.ScreamAngry, lifespan: 25f);
                        break;

                    case ScpProjectileImpactType.ProjectileImpactType.Neutral:
                    case ScpProjectileImpactType.ProjectileImpactType.Unknown:
                        LibraryExiledAPI.LogDebug("EventHandler.HandleExplosionEvent", $"Non-dangerous or unknown impact type: {impactType}");
                        _plugin.AudioManager.PlayGlobalAudioAutoManaged(AudioKey.Whispers, lifespan: 25f);
                        break;

                    default:
                        LibraryExiledAPI.LogWarn("EventHandler.HandleExplosionEvent", $"Unhandled impact type: {impactType} (int={(int)impactType})");
                        break;
                }
            }
            catch (Exception ex)
            {
                LibraryExiledAPI.LogError("EventHandler.HandleExplosionEvent", $"Failed to handle explosion event: {ex.Message}\nStackTrace: {ex.StackTrace}");
            }
        }
    }
}