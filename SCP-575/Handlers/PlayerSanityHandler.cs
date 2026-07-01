namespace SCP_575.Handlers
{
    using LabApi.Events.Arguments.PlayerEvents;
    using LabApi.Events.Arguments.ServerEvents;
    using LabApi.Events.CustomHandlers;
    using LabApi.Features.Wrappers;
    using MEC;
    using SCP_575.ConfigObjects;
    using SCP_575.Shared;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEngine;

    /// <summary>
    /// Manages the runtime neurological integrity models for human subjects. 
    /// Tracks milestone decay curves, applies item restoration variances, and drives biological status updates 
    /// while offloading all drama-bound audio responses to the central Audio Director.
    /// </summary>
    public class PlayerSanityHandler : CustomEventsHandler, IDisposable
    {
        public static string IdentifierName => nameof(Scp575DamageSystem);

        private readonly Plugin _plugin;
        private readonly LibraryLabAPI _libraryLabAPI;
        private readonly PlayerSanityConfig _sanityConfig;

        private readonly Dictionary<int, float> _sanityCache = new();
        private readonly Dictionary<int, DateTime> _lastHintTime = new();
        private readonly Dictionary<int, DateTime> _painkillerProtectionExpiry = new();
        private readonly Dictionary<int, DateTime> _painkillerSanityBoostExpiry = new();
        private readonly Dictionary<int, DateTime> _playerEffectsCooldownExpiry = new();
        private readonly List<PlayerSanityStageConfig> _orderedStages;

        private readonly float _hintCooldown;
        private bool _isDisposed;
        private readonly object _cacheLock = new();

        private const string SanityCoroutineTag = CoroutineTags.SanityHandler;

        /// <summary>
        /// Exposes a read-only view of runtime psychological metrics for external integration tracking.
        /// </summary>
        public IReadOnlyDictionary<int, float> SanityCache => _sanityCache;

        public PlayerSanityHandler(Plugin plugin)
        {
            _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin), "Plugin instance cannot be null.");
            _libraryLabAPI = _plugin.LibraryLabAPI;
            _sanityConfig = plugin.Config?.SanityConfig ?? throw new InvalidOperationException("SanityConfig is not initialized.");
            _hintCooldown = _sanityConfig.DecayRateBase * 20f;

            if (_sanityConfig.SanityStages == null || !_sanityConfig.SanityStages.Any())
                throw new InvalidOperationException("SanityStages is null or empty.");

            var stages = _sanityConfig.SanityStages.OrderBy(s => s.MinThreshold).ToList();

            if (stages[0].MinThreshold > 0 || stages[stages.Count - 1].MaxThreshold < 100)
                throw new InvalidOperationException("SanityStages do not cover the full range (0–100).");

            for (int i = 0; i < stages.Count - 1; i++)
            {
                if (stages[i].MaxThreshold != stages[i + 1].MinThreshold)
                    throw new InvalidOperationException("SanityStages have gaps or overlaps.");
            }

            // Descending sort optimizes linear scanning loops by assessing critical 
            // breakdown states before lower-tier cognitive baselines.
            _orderedStages = stages.OrderByDescending(s => s.MaxThreshold).ToList();
            LibraryLabAPI.LogDebug("PlayerSanityHandler", $"Loaded {stages.Count} sanity stages.");
        }

        #region Lifecycle Management

        public void Initialize()
        {
            if (_isDisposed) return;
            var handle = Timing.RunCoroutine(HandleSanityDecay());
            handle.Tag = SanityCoroutineTag;
            LibraryLabAPI.LogInfo("PlayerSanityHandler", "Sanity decay processing loop successfully started.");
        }

        public void Clean()
        {
            Timing.KillCoroutines(SanityCoroutineTag);
            lock (_cacheLock)
            {
                _sanityCache.Clear();
                _lastHintTime.Clear();
                _painkillerProtectionExpiry.Clear();
                _painkillerSanityBoostExpiry.Clear();
                _playerEffectsCooldownExpiry.Clear();
            }
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            Clean();
            _isDisposed = true;
            LibraryLabAPI.LogInfo("PlayerSanityHandler", "Disposed PlayerSanityHandler and cleaned up resources.");
        }

        public override void OnServerRoundEnded(RoundEndedEventArgs ev) => Clean();
        public override void OnServerWaitingForPlayers() => Clean();

        #endregion

        #region Event Handlers

        public override void OnPlayerSpawned(PlayerSpawnedEventArgs ev) => ResetPlayerSanity(ev?.Player);

        public override void OnPlayerChangedRole(PlayerChangedRoleEventArgs ev)
        {
            if (ev?.Player == null) return;

            ResetPlayerSanity(ev.Player);

            if (!IsValidPlayer(ev.Player))
            {

            }
        }

        public override void OnPlayerLeft(PlayerLeftEventArgs ev)
        {
            if (ev?.Player?.GameObject == null) return;
            int instanceId = ev.Player.GameObject.GetInstanceID();

            lock (_cacheLock)
            {
                _sanityCache.Remove(instanceId);
                _lastHintTime.Remove(instanceId);
                _painkillerProtectionExpiry.Remove(instanceId);
                _painkillerSanityBoostExpiry.Remove(instanceId);
                _playerEffectsCooldownExpiry.Remove(instanceId);
            }

            // Direct decoupled notification to the director
            _plugin.AudioDirector?.OnPlayerLeft(ev.Player);
        }

        public override void OnPlayerUsedItem(PlayerUsedItemEventArgs ev)
        {
            if (!_plugin.IsEventActive || !IsValidPlayer(ev?.Player) || ev.UsableItem?.Type == null) return;

            float restoreAmount = GetItemRestoreAmount(ev.UsableItem.Type);
            if (restoreAmount <= 0f) return;

            int instanceId = ev.Player.GameObject.GetInstanceID();

            if (ev.UsableItem.Type == ItemType.Painkillers)
            {
                lock (_cacheLock)
                {
                    _painkillerProtectionExpiry[instanceId] = DateTime.Now.AddSeconds(_sanityConfig.PainkillersProtectionDuration);
                    _painkillerSanityBoostExpiry[instanceId] = DateTime.Now.AddSeconds(_sanityConfig.PainkillersRegenDuration);
                }

                if (_plugin.Config.Debug)
                {
                    LibraryLabAPI.LogDebug("PlayerSanityHandler", $"Painkillers consumed by {ev.Player.Nickname}. Protection registered for {_sanityConfig.PainkillersProtectionDuration}s, Sanity boost registered for {_sanityConfig.PainkillersRegenDuration}s.");
                }
            }

            float newSanity = ChangeSanityValue(ev.Player, restoreAmount);
            if (_plugin.Config.HintsConfig.IsEnabledSanityHint)
                SendSanityHint(ev.Player, _plugin.Config.HintsConfig.SanityIncreasedMedicalHint, newSanity);

            if (_plugin.Config.Debug)
            {
                LibraryLabAPI.LogDebug("PlayerSanityHandler", $"Restored {restoreAmount} sanity to {ev.Player.Nickname}. New sanity: {newSanity}");
            }
        }

        #endregion

        #region Sanity Management

        public float GetCurrentSanity(Player player)
        {
            if (player?.GameObject == null) return _sanityConfig.InitialSanity;

            int instanceId = player.GameObject.GetInstanceID();
            lock (_cacheLock)
            {
                if (!_sanityCache.TryGetValue(instanceId, out float sanity))
                {
                    sanity = _sanityConfig.InitialSanity;
                    _sanityCache[instanceId] = sanity;
                }
                return sanity;
            }
        }

        private void ResetPlayerSanity(Player player)
        {
            if (IsValidPlayer(player))
            {
                SetSanity(player, _sanityConfig.InitialSanity);
            }
        }

        public float SetSanity(Player player, float sanity)
        {
            if (player?.GameObject == null) return 0f;

            int instanceId = player.GameObject.GetInstanceID();
            float clampedSanity = Mathf.Clamp(sanity, 0f, 100f);

            lock (_cacheLock)
            {
                _sanityCache[instanceId] = clampedSanity;
            }
            return clampedSanity;
        }

        public float ChangeSanityValue(Player player, float amount)
        {
            if (!IsValidPlayer(player)) return 0f;

            float currentSanity = GetCurrentSanity(player);
            return SetSanity(player, currentSanity + amount);
        }

        public PlayerSanityStageConfig GetCurrentSanityStage(float sanity)
        {
            foreach (var s in _orderedStages)
            {
                if (sanity <= s.MaxThreshold && (sanity > s.MinThreshold || (sanity == 0 && s.MinThreshold == 0)))
                    return s;
            }
            return null;
        }

        public PlayerSanityStageConfig GetCurrentSanityStage(Player player)
        {
            if (!IsValidPlayer(player)) return null;
            return GetCurrentSanityStage(GetCurrentSanity(player));
        }

        /// <summary>
        /// Translates cognitive decay milestones into tangible gameplay sensory impairments.
        /// Enforces strict rate-limiting bounds across consecutive bursts to protect client rendering tracks.
        /// </summary>
        public void ApplyStageEffects(Player player, bool bypassBlackoutGate = false, bool forceIgnoreCooldown = false)
        {
            if (!IsValidPlayer(player)) return;
            if (IsProtectedByPainkillers(player)) return;

            int playerInstanceId = player.GameObject.GetInstanceID();
            DateTime currentTime = DateTime.UtcNow;

            // Strict protection barrier against consecutive sensory burst spams (e.g., rapid screen blur overrides)
            if (!forceIgnoreCooldown)
            {
                if (_playerEffectsCooldownExpiry.TryGetValue(playerInstanceId, out DateTime expiryTime) && currentTime < expiryTime)
                {
                    return;
                }
            }

            if (!_plugin.Npc.Methods.IsBlackoutActive && !bypassBlackoutGate)
                return;

            var stage = GetCurrentSanityStage(player);
            if (stage == null) return;

            // FLASHLIGHT PROTECTION GATE: Evaluates if holding a light source mitigates the panic effects
            if (Helpers.IsHumanWithoutLight(player) || stage.OverrideLightSourceSanityProtection)
            {
                if (stage.Effects != null)
                {
                    foreach (var effectConfig in stage.Effects)
                    {
                        try
                        {
                            ApplyEffect(player, effectConfig.EffectType, effectConfig.Intensity, effectConfig.Duration);
                        }
                        catch (Exception ex)
                        {
                            LibraryLabAPI.LogWarn("PlayerSanityHandler", $"Failed to apply effect {effectConfig.EffectType} to {player.Nickname}: {ex.Message}");
                        }
                    }

                    // Impose the configuration-defined cooling window before another sensory explosion can be queued
                    float burstCooldown = _plugin.Config.SanityConfig.EffectsBurstCooldown;
                    if (burstCooldown > 0f)
                    {
                        _playerEffectsCooldownExpiry[playerInstanceId] = currentTime + TimeSpan.FromSeconds(burstCooldown);
                    }
                }
            }
        }

        /// <summary>
        /// Inflicts mechanical and physiological damage to players vulnerable to active entity zones.
        /// Hands operational audio cues off to the central AudioDirector subsystem.
        /// </summary>
        public void ApplyDamageToPlayer(Player player)
        {
            if (!IsValidPlayer(player)) return;
            if (IsProtectedByPainkillers(player)) return;

            var stage = GetCurrentSanityStage(player);
            if (stage == null) return;

            bool isVulnerable = Helpers.IsHumanWithoutLight(player) || stage.OverrideLightSourceSanityProtection;

            float culmDamage = isVulnerable
                ? stage.DamageOnStrike + (stage.AdditionalDamagePerStack * _plugin.Npc.Methods.GetCurrentBlackoutStacks)
                : stage.DamageOnStrikeWhenLightsourceActive + (stage.AdditionalDamagePerStackWhenLightsourceActive * _plugin.Npc.Methods.GetCurrentBlackoutStacks);

            if (culmDamage <= 0) return;

            // Redirects combat audio stinger execution parameters completely to the director component boundary
            _plugin.AudioDirector?.ProcessAnomalousCombatStinger(player, isVulnerable);

            // Delegate execution straight to the damage core.
            // This will naturally fire OnPlayerHurting, centralizing the sanity drop and effects processing.
            Scp575DamageSystem.DamagePlayer(player, culmDamage);
        }

        #endregion

        #region Sanity Processing Loop

        /// <summary>
        /// Central background loop managing active environmental context checks to apply sanity decay or regeneration.
        /// </summary>
        public IEnumerator<float> HandleSanityDecay()
        {
            while (true)
            {
                yield return Timing.WaitForSeconds(1f);

                if (!_plugin.IsEventActive)
                    continue;

                DateTime now = DateTime.Now;

                foreach (var player in Player.ReadyList)
                {
                    if (!IsValidPlayer(player))
                    {
                        continue;
                    }

                    bool isInDarkness = _libraryLabAPI.IsPlayerInDarkRoom(player);

                    if (isInDarkness)
                    {
                        ProcessDecayTick(player, now);
                    }
                    else
                    {
                        ProcessRegenTick(player, now);
                    }
                }
            }
        }

        private void ProcessDecayTick(Player player, DateTime now)
        {
            float decayRate = CalculateDecayRate(player);
            float oldSanity = GetCurrentSanity(player);
            float newSanity = ChangeSanityValue(player, -decayRate);

            bool requiresLowDrone = newSanity <= 35f;

            // Passive ticks are strictly bound to configuration rate-limiting bounds
            ApplyStageEffects(player, bypassBlackoutGate: false, forceIgnoreCooldown: false);

            int instanceId = player.GameObject.GetInstanceID();
            if (_plugin.Config.HintsConfig.IsEnabledSanityHint)
            {
                if (!_lastHintTime.TryGetValue(instanceId, out var lastTime) || (now - lastTime).TotalSeconds >= _hintCooldown)
                {
                    SendSanityHint(player, _plugin.Config.HintsConfig.SanityDecreasedHint, newSanity);
                    _lastHintTime[instanceId] = now;
                }
            }
        }

        private void ProcessRegenTick(Player player, DateTime now)
        {
            float oldSanity = GetCurrentSanity(player);

            if (oldSanity >= 100f) return;

            float regenRate = _sanityConfig.PassiveRegenRate;
            int instanceId = player.GameObject.GetInstanceID();

            lock (_cacheLock)
            {
                if (_painkillerSanityBoostExpiry.TryGetValue(instanceId, out DateTime boostExpiry) && now < boostExpiry)
                {
                    regenRate += _sanityConfig.PainkillersExtraSanityRegen;
                }
            }

            float newSanity = ChangeSanityValue(player, regenRate);

            if (_plugin.Config.HintsConfig.IsEnabledSanityHint)
            {
                if (!_lastHintTime.TryGetValue(instanceId, out var lastTime) || (now - lastTime).TotalSeconds >= _hintCooldown)
                {
                    SendSanityHint(player, _plugin.Config.HintsConfig.SanityIncreasedHint, newSanity);
                    _lastHintTime[instanceId] = now;
                }
            }
        }

        private float CalculateDecayRate(Player player)
        {
            float decayRate = _sanityConfig.DecayRateBase;
            if (_plugin.Npc?.Methods?.IsBlackoutActive == true)
                decayRate *= _sanityConfig.DecayMultiplierBlackout;
            if (Helpers.IsHumanWithoutLight(player))
                decayRate *= _sanityConfig.DecayMultiplierDarkness;
            return decayRate;
        }

        #endregion

        #region Helper Methods

        public bool IsValidPlayer(Player player)
        {
            return player != null &&
                   !string.IsNullOrEmpty(player.UserId) &&
                   player.IsAlive &&
                   player.IsHuman &&
                   player.Room != null &&
                   player.Room.Name != MapGeneration.RoomName.Pocket;
        }

        private float GetItemRestoreAmount(ItemType itemType)
        {
            return itemType switch
            {
                ItemType.SCP500 => UnityEngine.Random.Range(_sanityConfig.Scp500RestoreMin, _sanityConfig.Scp500RestoreMax),
                ItemType.Painkillers => UnityEngine.Random.Range(_sanityConfig.PainkillersRestoreMin, _sanityConfig.PainkillersRestoreMax),
                _ => 0f
            };
        }
        public bool IsProtectedByPainkillers(Player player)
        {
            if (player?.GameObject == null) return false;

            int instanceId = player.GameObject.GetInstanceID();
            lock (_cacheLock)
            {
                if (_painkillerProtectionExpiry.TryGetValue(instanceId, out DateTime expiryTime))
                {
                    return DateTime.Now < expiryTime;
                }
            }
            return false;
        }

        private void SendSanityHint(Player player, string hintMessage, float sanity)
        {
            string formatted = string.Format(hintMessage, sanity.ToString("F1"));
            player.SendHint(formatted, 5f);
        }

        private static void ApplyEffect(Player player, SanityEffectType effectType, byte intensity, float duration)
        {
            switch (effectType)
            {
                case SanityEffectType.Blurred: player.EnableEffect<CustomPlayerEffects.Blurred>(intensity, duration); break;
                case SanityEffectType.Blindness: player.EnableEffect<CustomPlayerEffects.Blindness>(intensity, duration); break;
                case SanityEffectType.Flashed: player.EnableEffect<CustomPlayerEffects.Flashed>(intensity, duration); break;
                case SanityEffectType.Deafened: player.EnableEffect<CustomPlayerEffects.Deafened>(intensity, duration); break;
                case SanityEffectType.Slowness: player.EnableEffect<CustomPlayerEffects.Slowness>(intensity, duration); break;
                case SanityEffectType.SilentWalk: player.EnableEffect<CustomPlayerEffects.SilentWalk>(intensity, duration); break;
                case SanityEffectType.Exhausted: player.EnableEffect<CustomPlayerEffects.Exhausted>(intensity, duration); break;
                case SanityEffectType.Disabled: player.EnableEffect<CustomPlayerEffects.Disabled>(intensity, duration); break;
                case SanityEffectType.Bleeding: player.EnableEffect<CustomPlayerEffects.Bleeding>(intensity, duration); break;
                case SanityEffectType.Poisoned: player.EnableEffect<CustomPlayerEffects.Poisoned>(intensity, duration); break;
                case SanityEffectType.Burned: player.EnableEffect<CustomPlayerEffects.Burned>(intensity, duration); break;
                case SanityEffectType.Corroding: player.EnableEffect<CustomPlayerEffects.Corroding>(intensity, duration); break;
                case SanityEffectType.Concussed: player.EnableEffect<CustomPlayerEffects.Concussed>(intensity, duration); break;
                case SanityEffectType.Traumatized: player.EnableEffect<CustomPlayerEffects.Traumatized>(intensity, duration); break;
                case SanityEffectType.Invisible: player.EnableEffect<CustomPlayerEffects.Invisible>(intensity, duration); break;
                case SanityEffectType.Scp207: player.EnableEffect<CustomPlayerEffects.Scp207>(intensity, duration); break;
                case SanityEffectType.AntiScp207: player.EnableEffect<CustomPlayerEffects.AntiScp207>(intensity, duration); break;
                case SanityEffectType.MovementBoost: player.EnableEffect<CustomPlayerEffects.MovementBoost>(intensity, duration); break;
                case SanityEffectType.DamageReduction: player.EnableEffect<CustomPlayerEffects.DamageReduction>(intensity, duration); break;
                case SanityEffectType.RainbowTaste: player.EnableEffect<CustomPlayerEffects.RainbowTaste>(intensity, duration); break;
                case SanityEffectType.BodyshotReduction: player.EnableEffect<CustomPlayerEffects.BodyshotReduction>(intensity, duration); break;
                case SanityEffectType.Scp1853: player.EnableEffect<CustomPlayerEffects.Scp1853>(intensity, duration); break;
                case SanityEffectType.CardiacArrest: player.EnableEffect<CustomPlayerEffects.CardiacArrest>(intensity, duration); break;
                case SanityEffectType.InsufficientLighting: player.EnableEffect<CustomPlayerEffects.InsufficientLighting>(intensity, duration); break;
                case SanityEffectType.SoundtrackMute: player.EnableEffect<CustomPlayerEffects.SoundtrackMute>(intensity, duration); break;
                case SanityEffectType.SpawnProtected: player.EnableEffect<CustomPlayerEffects.SpawnProtected>(intensity, duration); break;
                case SanityEffectType.Ensnared: player.EnableEffect<CustomPlayerEffects.Ensnared>(intensity, duration); break;
                case SanityEffectType.Ghostly: player.EnableEffect<CustomPlayerEffects.Ghostly>(intensity, duration); break;
                case SanityEffectType.SeveredHands: player.EnableEffect<CustomPlayerEffects.SeveredHands>(intensity, duration); break;
                case SanityEffectType.Stained: player.EnableEffect<CustomPlayerEffects.Stained>(intensity, duration); break;
                case SanityEffectType.Vitality: player.EnableEffect<CustomPlayerEffects.Vitality>(intensity, duration); break;
                case SanityEffectType.Asphyxiated: player.EnableEffect<CustomPlayerEffects.Asphyxiated>(intensity, duration); break;
                case SanityEffectType.Decontaminating: player.EnableEffect<CustomPlayerEffects.Decontaminating>(intensity, duration); break;
                case SanityEffectType.PocketCorroding: player.EnableEffect<CustomPlayerEffects.PocketCorroding>(intensity, duration); break;
                default: throw new ArgumentException($"Unknown effect type: {effectType}");
            }
        }

        #endregion
    }
}