using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.Arguments.ServerEvents;
using LabApi.Events.CustomHandlers;
using LabApi.Extensions;
using LabApi.Extensions.Misc;
using LabApi.Features.Wrappers;
using MEC;
using SCP_575.ConfigObjects;
using SCP_575.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using Logger = LabApi.Extensions.Misc.iLogger;

namespace SCP_575.Handlers
{
    /// <summary>
    /// Coordinates human subject neurological integrity, tracking decay vectors and applying status effect metrics via Fluent API channels.
    /// </summary>
    public class PlayerSanityHandler : CustomEventsHandler, IDisposable
    {
        #region Fields & Registries
        public static string IdentifierName => nameof(Scp575DamageSystem);

        private readonly Plugin _plugin;
        private readonly PlayerSanityConfig _sanityConfig;
        private readonly List<PlayerSanityStageConfig> _orderedStages;

        private readonly Dictionary<int, float> _sanityCache = new();
        private readonly Dictionary<int, DateTime> _lastHintTime = new();
        private readonly Dictionary<int, DateTime> _painkillerProtectionExpiry = new();
        private readonly Dictionary<int, DateTime> _painkillerSanityBoostExpiry = new();
        private readonly Dictionary<int, DateTime> _playerEffectsCooldownExpiry = new();

        private readonly float _hintCooldown;
        private bool _isDisposed;
        private readonly object _cacheLock = new();

        private const string SanityCoroutineTag = CoroutineTags.SanityHandler;
        #endregion

        #region Properties
        /// <summary>
        /// Gets a read-only view of runtime psychological metrics.
        /// </summary>
        public IReadOnlyDictionary<int, float> SanityCache => _sanityCache;
        #endregion

        #region Constructor
        public PlayerSanityHandler(Plugin plugin)
        {
            _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin), "Plugin context unavailable.");
            _sanityConfig = _plugin.Sanity ?? throw new InvalidOperationException("SanityConfig is not initialized.");
            _hintCooldown = _sanityConfig.DecayRateBase * 20f;

            if (_sanityConfig.SanityStages is null || !_sanityConfig.SanityStages.Any())
                throw new InvalidOperationException("Sanity tracking configuration stages cannot be empty.");

            var stages = _sanityConfig.SanityStages.OrderBy(s => s.MinThreshold).ToList();

            if (stages[0].MinThreshold > 0 || stages[stages.Count - 1].MaxThreshold < 100)
                throw new InvalidOperationException("Sanity processing boundaries must span complete spectrum parameters (0-100).");

            for (int i = 0; i < stages.Count - 1; i++)
            {
                if (stages[i].MaxThreshold != stages[i + 1].MinThreshold)
                    throw new InvalidOperationException("Sanity boundary metrics overlay gap detected.");
            }

            _orderedStages = stages.OrderByDescending(s => s.MaxThreshold).ToList();
            Logger.Debug(nameof(PlayerSanityHandler), $"Loaded {stages.Count} structural integrity steps successfully.", _plugin.Debug);
        }
        #endregion

        #region Lifecycle
        public void Initialize()
        {
            if (_isDisposed) return;

            CoroutineHandle handle = Timing.RunCoroutine(HandleSanityDecay());
            handle.Tag = SanityCoroutineTag;

            Logger.Debug(nameof(PlayerSanityHandler), "Neurological monitoring thread loop engaged.", _plugin.Debug);
        }

        public void Clean()
        {
            SanityCoroutineTag.KillCoroutine();
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
            Logger.Info(nameof(PlayerSanityHandler), "Psychological tracking registry closed cleanly.");
        }

        public override void OnServerRoundEnded(RoundEndedEventArgs ev) => Clean();
        public override void OnServerWaitingForPlayers() => Clean();
        #endregion

        #region Events Matrix
        public override void OnPlayerSpawned(PlayerSpawnedEventArgs ev) => ResetPlayerSanity(ev?.Player);

        public override void OnPlayerChangedRole(PlayerChangedRoleEventArgs ev)
        {
            if (ev?.Player is null) return;
            ResetPlayerSanity(ev.Player);
        }

        public override void OnPlayerLeft(PlayerLeftEventArgs ev)
        {
            if (ev?.Player?.GameObject is null) return;
            int instanceId = ev.Player.GameObject.GetInstanceID();

            lock (_cacheLock)
            {
                _sanityCache.Remove(instanceId);
                _lastHintTime.Remove(instanceId);
                _painkillerProtectionExpiry.Remove(instanceId);
                _painkillerSanityBoostExpiry.Remove(instanceId);
                _playerEffectsCooldownExpiry.Remove(instanceId);
            }

            _plugin.AudioDirector?.OnPlayerLeft(ev.Player);
        }

        public override void OnPlayerUsedItem(PlayerUsedItemEventArgs ev)
        {
            if (!_plugin.IsEventActive || !IsValidPlayer(ev?.Player) || ev.UsableItem?.Type is null) return;

            float restoreAmount = GetItemRestoreAmount(ev.UsableItem.Type);
            if (restoreAmount <= 0f) return;

            int instanceId = ev.Player.GameObject.GetInstanceID();

            if (ev.UsableItem.Type is ItemType.Painkillers)
            {
                lock (_cacheLock)
                {
                    _painkillerProtectionExpiry.TryAcquireLock(instanceId, TimeSpan.FromSeconds(_sanityConfig.PainkillersProtectionDuration));
                    _painkillerSanityBoostExpiry.TryAcquireLock(instanceId, TimeSpan.FromSeconds(_sanityConfig.PainkillersRegenDuration));
                }

                Logger.Debug(nameof(PlayerSanityHandler), $"Medical mitigation markers committed for {ev.Player.Nickname}.", _plugin.Debug);
            }

            float newSanity = ChangeSanityValue(ev.Player, restoreAmount);

            if (_plugin.Hints.IsEnabledSanityHint)
                SendSanityHint(ev.Player, _plugin.Hints.SanityIncreasedMedicalHint, newSanity);

            Logger.Debug(nameof(PlayerSanityHandler), $"Restored {restoreAmount} sanity to {ev.Player.Nickname}. Current boundary: {newSanity}", _plugin.Debug);
        }
        #endregion

        #region Anomaly Sanity Core Abstractions
        public float GetCurrentSanity(Player player)
        {
            if (player?.GameObject is null) return _sanityConfig.InitialSanity;

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
                SetPlayerSanity(player, _sanityConfig.InitialSanity);
            }
        }

        public float SetPlayerSanity(Player player, float sanity)
        {
            if (player?.GameObject is null) return 0f;

            int instanceId = player.GameObject.GetInstanceID();
            float clampedSanity = sanity.Clamp(0f, 100f);

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
            return SetPlayerSanity(player, currentSanity + amount);
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

        public void ApplyStageEffects(Player player, bool bypassBlackoutGate = false, bool forceIgnoreCooldown = false)
        {
            if (!IsValidPlayer(player) || IsProtectedByPainkillers(player)) return;

            int playerInstanceId = player.GameObject.GetInstanceID();

            if (!forceIgnoreCooldown && _playerEffectsCooldownExpiry.IsCooldownActive(playerInstanceId)) return;

            if (!_plugin.NpcLogic.IsBlackoutActive && !bypassBlackoutGate) return;

            var stage = GetCurrentSanityStage(player);
            if (stage?.Effects is null) return;

            if (!player.HasActiveLightSource() || stage.OverrideLightSourceSanityProtection)
            {
                foreach (var effectConfig in stage.Effects)
                {
                    try
                    {
                        FacilityEffectType targetEffect = effectConfig.EffectType.ToString().ParseOrDefault<FacilityEffectType>();
                        player.EnableEffect(targetEffect, effectConfig.Intensity, effectConfig.Duration);
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn(nameof(PlayerSanityHandler), $"Sensory payload delivery rejected for {player.Nickname}: {ex.Message}");
                    }
                }

                float burstCooldown = _sanityConfig.EffectsBurstCooldown;
                if (burstCooldown > 0f)
                {
                    lock (_cacheLock)
                    {
                        _playerEffectsCooldownExpiry.TryAcquireLock(playerInstanceId, TimeSpan.FromSeconds(burstCooldown));
                    }
                }
            }
        }

        public void ApplyDamageToPlayer(Player player)
        {
            if (!IsValidPlayer(player) || IsProtectedByPainkillers(player)) return;

            var stage = GetCurrentSanityStage(player);
            if (stage is null) return;

            bool isVulnerable = !player.HasActiveLightSource() || stage.OverrideLightSourceSanityProtection;

            float culmDamage = isVulnerable
                ? stage.DamageOnStrike + (stage.AdditionalDamagePerStack * _plugin.NpcLogic.GetCurrentBlackoutStacks)
                : stage.DamageOnStrikeWhenLightsourceActive + (stage.AdditionalDamagePerStackWhenLightsourceActive * _plugin.NpcLogic.GetCurrentBlackoutStacks);

            if (culmDamage <= 0) return;

            _plugin.AudioDirector?.ProcessAnomalousCombatStinger(player, isVulnerable);
            _plugin.DamageSystem.DamagePlayer(player, culmDamage);
        }
        #endregion

        #region Pacing System Loop
        /// <summary>
        /// Asynchronously tracks active environment nodes to update client mental integrity states.
        /// </summary>
        public IEnumerator<float> HandleSanityDecay()
        {
            while (true)
            {
                yield return Timing.WaitForSeconds(1f);

                if (!_plugin.IsEventActive)
                    continue;

                DateTime now = DateTime.UtcNow;

                foreach (Player player in Player.ReadyList)
                {
                    if (!IsValidPlayer(player)) continue;

                    if (player.IsInTrueDarkness())
                    {
                        ProcessDecayTick(player, now);
                    }
                    else if (player.IsInDarkRoom())
                    {
                        continue;
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
            float newSanity = ChangeSanityValue(player, -decayRate);

            ApplyStageEffects(player, bypassBlackoutGate: false, forceIgnoreCooldown: false);

            int instanceId = player.GameObject.GetInstanceID();
            if (_plugin.Hints.IsEnabledSanityHint)
            {
                if (_lastHintTime.TryAcquireLock(instanceId, TimeSpan.FromSeconds(_hintCooldown)))
                {
                    SendSanityHint(player, _plugin.Hints.SanityDecreasedHint, newSanity);
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
                if (_painkillerSanityBoostExpiry.IsCooldownActive(instanceId))
                {
                    regenRate += _sanityConfig.PainkillersExtraSanityRegen;
                }
            }

            float newSanity = ChangeSanityValue(player, regenRate);

            if (_plugin.Hints.IsEnabledSanityHint)
            {
                if (_lastHintTime.TryAcquireLock(instanceId, TimeSpan.FromSeconds(_hintCooldown)))
                {
                    SendSanityHint(player, _plugin.Hints.SanityIncreasedHint, newSanity);
                }
            }
        }

        private float CalculateDecayRate(Player player)
        {
            float decayRate = _sanityConfig.DecayRateBase;

            if (_plugin.NpcLogic.IsBlackoutActive)
                decayRate *= _sanityConfig.DecayMultiplierBlackout;

            if (!player.HasActiveLightSource() || player.IsInTrueDarkness())
                decayRate *= _sanityConfig.DecayMultiplierDarkness;

            return decayRate;
        }
        #endregion

        #region Technical Infrastructure Hooks & Context Resolvers
        public bool IsValidPlayer(Player player)
        {
            return player is not null &&
                   !string.IsNullOrEmpty(player.UserId) &&
                   player.IsLivingHuman() &&
                   player.Room is not null &&
                   !player.IsInRoom(MapGeneration.RoomName.Pocket);
        }

        private float GetItemRestoreAmount(ItemType itemType) => itemType switch
        {
            ItemType.SCP500 => SafeRandom.Range(_sanityConfig.Scp500RestoreMin, _sanityConfig.Scp500RestoreMax),
            ItemType.Painkillers => SafeRandom.Range(_sanityConfig.PainkillersRestoreMin, _sanityConfig.PainkillersRestoreMax),
            _ => 0f
        };

        public bool IsProtectedByPainkillers(Player player)
        {
            if (player?.GameObject is null) return false;
            lock (_cacheLock)
            {
                return _painkillerProtectionExpiry.IsCooldownActive(player.GameObject.GetInstanceID());
            }
        }

        private static void SendSanityHint(Player player, string hintMessage, float sanity)
        {
            string formatted = string.Format(hintMessage, sanity.ToString("F1"));
            player.SendHint(formatted, 5f);
        }
        #endregion
    }
}