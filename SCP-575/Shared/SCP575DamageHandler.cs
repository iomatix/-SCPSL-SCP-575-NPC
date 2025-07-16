namespace SCP_575.Shared
{
    using Footprinting;
    using InventorySystem.Items.Armor;
    using Mirror;
    using PlayerRoles;
    using PlayerRoles.Ragdolls;
    using PlayerStatsSystem;
    using System.Collections.Generic;
    using UnityEngine;

    /// <summary>  
    /// Custom damage handler for SCP-575 entity that provides specialized damage processing,  
    /// ragdoll handling, and hitbox-based damage calculations while preventing unwanted ragdoll physics.
    /// </summary>  
    /// <remarks>  
    /// This handler extends <see cref="AttackerDamageHandler"/> to provide SCP-575 specific functionality  
    /// including custom death messages, armor penetration, and controlled ragdoll positioning.
    /// </remarks>  
    public sealed class Scp575DamageHandler : AttackerDamageHandler
    {
        #region Constants and Static Properties  

        /// <summary>  
        /// Gets the unique identifier name for this damage handler type.
        /// </summary>  
        public static string IdentifierName => nameof(Scp575DamageHandler);

        /// <summary>  
        /// Gets the unique byte identifier for network serialization.
        /// </summary>  
        public static byte IdentifierByte => 175;

        /// <summary>  
        /// Defines force multipliers applied to different hitbox types during ragdoll processing.
        /// </summary>  
        /// <remarks>  
        /// These values are currently unused due to ragdoll position restoration but maintained for future use.
        /// </remarks>  
        public static readonly IReadOnlyDictionary<HitboxType, float> HitboxToForce = new Dictionary<HitboxType, float>
        {
            [HitboxType.Body] = 0.08f,
            [HitboxType.Headshot] = 0.085f,
            [HitboxType.Limb] = 0.016f
        };

        /// <summary>  
        /// Defines damage multipliers for different hitbox types to simulate realistic damage scaling.
        /// </summary>  
        public static readonly IReadOnlyDictionary<HitboxType, float> HitboxDamageMultipliers = new Dictionary<HitboxType, float>
        {
            [HitboxType.Headshot] = 1.85f,
            [HitboxType.Limb] = 0.75f
        };

        #endregion

        #region Fields  

        /// <summary>  
        /// Stores the hit direction on the X-axis for network synchronization.
        /// </summary>  
        private sbyte _hitDirectionX;

        /// <summary>  
        /// Stores the hit direction on the Z-axis for network synchronization.
        /// </summary>  
        private sbyte _hitDirectionZ;

        /// <summary>  
        /// The armor penetration value for this damage instance.
        /// </summary>  
        private readonly float _penetration;

        /// <summary>  
        /// The format string for death reason display on ragdoll inspection.
        /// </summary>  
        private readonly string _deathReasonFormat;

        /// <summary>  
        /// Indicates whether human-specific hitbox multipliers should be applied.
        /// </summary>  
        private readonly bool _useHumanHitboxes;

        #endregion

        #region Properties  

        /// <summary>  
        /// Gets or sets the damage amount to be applied.
        /// </summary>  
        public override float Damage { get; set; }

        /// <summary>  
        /// Gets a value indicating whether this damage handler allows self-damage.
        /// SCP-575 cannot damage itself.
        /// </summary>  
        public override bool AllowSelfDamage => false;

        /// <summary>  
        /// Gets or sets the attacker's footprint for tracking purposes.
        /// </summary>  
        public override Footprint Attacker { get; set; }

        /// <summary>  
        /// Gets the CASSIE announcement for deaths caused by this handler.
        /// SCP-575 deaths do not trigger CASSIE announcements.
        /// </summary>  
        public override CassieAnnouncement CassieDeathAnnouncement => null;

        /// <summary>  
        /// Gets the text displayed in server logs for deaths caused by this handler.
        /// </summary>  
        public override string ServerLogsText =>
            $"Killed by {Library_LabAPI.NpcConfig.KilledBy}, Attacker: {Library_LabAPI.NpcConfig.KilledBy}, Hitbox: {Hitbox}";

        /// <summary>  
        /// Gets the text displayed on the player's death screen.
        /// </summary>  
        public override string DeathScreenText => Library_LabAPI.NpcConfig.KilledByMessage;

        /// <summary>  
        /// Gets the text displayed when inspecting a ragdoll killed by this handler.
        /// </summary>  
        public override string RagdollInspectText =>
            string.Format(_deathReasonFormat, Library_LabAPI.NpcConfig.RagdollInspectText);

        /// <summary>  
        /// Gets the text used for server metrics reporting.
        /// </summary>  
        public override string ServerMetricsText =>
            base.ServerMetricsText + "," + Library_LabAPI.NpcConfig.KilledByMessage;

        #endregion

        #region Constructors  

        /// <summary>  
        /// Initializes a new instance of the <see cref="Scp575DamageHandler"/> class with default values.
        /// </summary>  
        /// <remarks>  
        /// This constructor is primarily used for deserialization and should not be called directly.
        /// Use the parameterized constructor for normal instantiation.
        /// </remarks>  
        public Scp575DamageHandler()
        {
            _deathReasonFormat = Scp575DeathTranslations.CustomDeathTranslation_arg1.RagdollTranslation;
        }

        /// <summary>  
        /// Initializes a new instance of the <see cref="Scp575DamageHandler"/> class with specified parameters.
        /// </summary>  
        /// <param name="damage">The amount of damage to apply.</param>
        /// <param name="attacker">The player or entity causing the damage. If null, defaults to server host.</param>
        /// <param name="useHumanMultipliers">Whether to apply human-specific hitbox damage multipliers.</param>
        /// <exception cref="System.ArgumentOutOfRangeException">Thrown when damage is negative.</exception>  
        public Scp575DamageHandler(float damage, LabApi.Features.Wrappers.Player attacker = null, bool useHumanMultipliers = true)
            : this()
        {
            if (damage < 0)
                throw new System.ArgumentOutOfRangeException(nameof(damage), "Damage cannot be negative.");

            Library_ExiledAPI.LogDebug("Scp575DamageHandler",
                $"Handler initialized with damage: {damage}, Attacker: {attacker?.Nickname ?? "SCP-575 NPC"}");

            Damage = damage;
            _penetration = Library_LabAPI.NpcConfig.KeterDamagePenetration;
            _useHumanHitboxes = useHumanMultipliers;

            // Set attacker footprint with fallback to server host  
            Attacker = attacker?.ReferenceHub != null
                ? new Footprint(attacker.ReferenceHub)
                : LabApi.Features.Wrappers.Server.Host?.ReferenceHub != null
                ? new Footprint(LabApi.Features.Wrappers.Server.Host.ReferenceHub)
                : default;
        }

        #endregion

        #region Network Serialization  

        /// <summary>  
        /// Writes additional data required for network synchronization.
        /// </summary>  
        /// <param name="writer">The network writer to write data to.</param>
        /// <exception cref="System.ArgumentNullException">Thrown when writer is null.</exception>  
        public override void WriteAdditionalData(NetworkWriter writer)
        {
            if (writer == null)
                throw new System.ArgumentNullException(nameof(writer));

            base.WriteAdditionalData(writer);
            writer.WriteByte((byte)Hitbox);
            writer.WriteSByte(_hitDirectionX);
            writer.WriteSByte(_hitDirectionZ);
        }

        /// <summary>  
        /// Reads additional data from network synchronization.
        /// </summary>  
        /// <param name="reader">The network reader to read data from.</param>
        /// <exception cref="System.ArgumentNullException">Thrown when reader is null.</exception>  
        public override void ReadAdditionalData(NetworkReader reader)
        {
            if (reader == null)
                throw new System.ArgumentNullException(nameof(reader));

            base.ReadAdditionalData(reader);
            Hitbox = (HitboxType)reader.ReadByte();
            _hitDirectionX = reader.ReadSByte();
            _hitDirectionZ = reader.ReadSByte();
        }

        #endregion

        #region Damage Processing  

        /// <summary>  
        /// Applies damage to the specified player and handles SCP-575 specific effects.
        /// </summary>  
        /// <param name="ply">The reference hub of the player receiving damage.</param>
        /// <returns>The result of the damage application.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when ply is null.</exception>  
        public override HandlerOutput ApplyDamage(ReferenceHub ply)
        {
            if (ply == null)
                throw new System.ArgumentNullException(nameof(ply));

            Exiled.API.Features.Log.Debug(
                $"[ApplyDamage] Applying damage to {ply.nicknameSync.MyNick} with Hitbox: {Hitbox} and Damage: {Damage:F1}");

            // Apply SCP-575 specific damage effects if enabled  
            var labPlayer = LabApi.Features.Wrappers.Player.Get(ply);
            if (Library_LabAPI.NpcConfig.EnableKeterOnDealDamageEffects)
                Scp575DamageHandler_LabAPI.ApplyDamageEffects(labPlayer);

            HandlerOutput handlerOutput = base.ApplyDamage(ply);

            // Handle damage feedback through ExiledAPI integration  
            Scp575DamageHandler_ExiledAPI.HandleApplyDamageFeedback(ply, Damage, handlerOutput);

            // Calculate and store hit direction for network synchronization  
            Vector3 forward = Library_LabAPI.GetPlayer(ply).ReferenceHub.PlayerCameraReference.forward;
            _hitDirectionX = (sbyte)Mathf.RoundToInt(forward.x * 127f);
            _hitDirectionZ = (sbyte)Mathf.RoundToInt(forward.z * 127f);

            return handlerOutput;
        }

        /// <summary>  
        /// Processes damage calculations including hitbox multipliers and armor interactions.
        /// </summary>  
        /// <param name="ply">The reference hub of the player receiving damage.</param>
        /// <exception cref="System.ArgumentNullException">Thrown when ply is null.</exception>  
        public override void ProcessDamage(ReferenceHub ply)
        {
            if (ply == null)
                throw new System.ArgumentNullException(nameof(ply));

            Library_ExiledAPI.LogDebug("ProcessDamage",
                $"Processing damage for {ply.nicknameSync.MyNick} with Hitbox: {Hitbox} and Damage: {Damage:F1}");

            // Override hitbox for non-human entities if human multipliers are disabled  
            if (!_useHumanHitboxes && ply.IsHuman())
            {
                Library_ExiledAPI.LogDebug("ProcessDamage",
                    $"Using human hitboxes is disabled, setting Hitbox to Body for {ply.nicknameSync.MyNick}");
                Hitbox = HitboxType.Body;
            }

            // Apply hitbox-specific damage multipliers  
            if (_useHumanHitboxes && HitboxDamageMultipliers.TryGetValue(Hitbox, out var damageMul))
            {
                Damage *= damageMul;
                Library_ExiledAPI.LogDebug("ProcessDamage",
                    $"Hitbox {Hitbox} found in HitboxDamageMultipliers, applying multiplier: {damageMul} to Damage: {Damage:F1}");
            }

            Library_ExiledAPI.LogDebug("ProcessDamage",
                $"Processing base for ProcessDamage(ply) after multipliers: {Damage:F1} for player: {ply.nicknameSync.MyNick}");

            base.ProcessDamage(ply);

            // Handle armor interactions for armored roles  
            ProcessArmorInteraction(ply);
        }

        /// <summary>  
        /// Processes armor interactions and applies penetration calculations.
        /// </summary>  
        /// <param name="ply">The reference hub of the player with armor.</param>
        private void ProcessArmorInteraction(ReferenceHub ply)
        {
            if (Damage == 0f || ply.roleManager.CurrentRole is not IArmoredRole armoredRole)
                return;

            Exiled.API.Features.Log.Debug(
                $"[ProcessDamage] Player {ply.nicknameSync.MyNick} is an armored role: {armoredRole}");

            int armorEfficacy = armoredRole.GetArmorEfficacy(Hitbox);
            int penetrationPercent = Mathf.RoundToInt(_penetration * 100f);

            float shieldNum = Mathf.Clamp(ply.playerStats.GetModule<HumeShieldStat>().CurValue, 0f, Damage);
            float baseDamage = Mathf.Max(0f, Damage - shieldNum);
            float postArmorNum = BodyArmorUtils.ProcessDamage(armorEfficacy, baseDamage, penetrationPercent);

            Damage = postArmorNum + shieldNum;

            Library_ExiledAPI.LogDebug("ProcessDamage",
                $"Player {ply.nicknameSync.MyNick} armor efficacy: {armorEfficacy}, penetration percent: {penetrationPercent}, " +
                $"base damage: {baseDamage:F1}, processed damage: {postArmorNum:F1}, final Damage: {Damage:F1}");
        }

        #endregion

        #region Ragdoll Processing  

        /// <summary>  
        /// Processes ragdoll creation while preventing unwanted physics-based movement.
        /// </summary>  
        /// <param name="ragdoll">The basic ragdoll to process.</param>
        /// <remarks>  
        /// This method calls the base implementation to apply standard ragdoll processing,  
        /// then immediately restores the original position to prevent physics-based displacement.
        /// This ensures SCP-575 victims remain at their death location for dramatic effect.
        /// </remarks>  
        /// <exception cref="System.ArgumentNullException">Thrown when ragdoll is null.</exception>  
        public override void ProcessRagdoll(BasicRagdoll ragdoll)
        {
            if (ragdoll == null)
                throw new System.ArgumentNullException(nameof(ragdoll));

            Library_ExiledAPI.LogDebug("ProcessRagdoll",
                $"Ragdoll role: {ragdoll.NetworkInfo.RoleType}, Position: {ragdoll.transform.position}");
            Library_ExiledAPI.LogDebug("ProcessRagdoll",
                $"Attacker: {Attacker.Hub?.nicknameSync.MyNick ?? "NULL"}");

            Vector3 originalPosition = ragdoll.transform.position;

            // Call base processing  
            try
            {
                Library_ExiledAPI.LogDebug("ProcessRagdoll", "Calling base.ProcessRagdoll");
                base.ProcessRagdoll(ragdoll);
                Library_ExiledAPI.LogDebug("ProcessRagdoll", "base.ProcessRagdoll completed successfully");
            }
            catch (System.Exception ex)
            {
                Library_ExiledAPI.LogError("ProcessRagdoll", $"base.ProcessRagdoll failed: {ex.Message}");
                Library_ExiledAPI.LogError("ProcessRagdoll", $"Stack trace: {ex.StackTrace}");
                throw; // Re-throw
            }

            // Get LabAPI wrapper immediately after base processing  
            LabApi.Features.Wrappers.Ragdoll labRagdoll = Library_LabAPI.GetRagdoll(ragdoll);

            // Force immediate synchronization using LabAPI's Position property  
            labRagdoll.Position = originalPosition;

            // Add a small delay to ensure network synchronization completes  
            MEC.Timing.CallDelayed(0.05f, () => {
                if (labRagdoll?.Base != null)
                {
                    labRagdoll.Position = originalPosition; // Second sync attempt  
                    Scp575Helpers.RagdollProcess(labRagdoll, this);
                }
            });

            Library_ExiledAPI.LogDebug("ProcessRagdoll", $"Processed SCP-575 ragdoll with delayed helper call");
        }

        #endregion

        #region Utility Methods  

        /// <summary>
        /// Calculates a randomized force push multiplier within configured bounds.
        /// </summary>  
        /// <param name="baseValue">The base value to multiply by the random factor.</param>
        /// <returns>A randomized force multiplier value.</returns>
        /// <remarks>
        /// This method is currently unused due to ragdoll position restoration but maintained  
        /// for potential future use in item physics or other SCP-575 effects.
        /// </remarks>
        public float CalculateForcePush(float baseValue = 1.0f)
        {
            float randomFactor = Random.Range(
                Library_LabAPI.NpcConfig.KeterForceMinModifier,
                Library_LabAPI.NpcConfig.KeterForceMaxModifier);

            return baseValue * randomFactor;
        }

        /// <summary>
        /// Generates a random unit sphere velocity vector with damage-based scaling.
        /// </summary>  
        /// <param name="baseValue">The base velocity multiplier.</param>
        /// <returns>A scaled random velocity vector.</returns>
        /// <remarks>
        /// This method includes logic to prevent downward-pointing vectors that could cause  
        /// items to fall through the floor. The velocity is scaled logarithmically based on  
        /// damage amount to provide realistic physics effects.
        /// Currently unused for ragdolls due to position restoration.
        /// </remarks>
        public Vector3 GetRandomUnitSphereVelocity(float baseValue = 1.0f)
        {
            Vector3 randomDirection = Random.onUnitSphere;

            // Prevent downward vectors that could cause items to clip through floors  
            // If the vector points more than 45° downward, reflect it upward  
            if (Vector3.Dot(randomDirection, Vector3.down) > 0.707f) // cos(45°) ≈ 0.707  
            {
                Exiled.API.Features.Log.Debug(
                    "[GetRandomUnitSphereVelocity] Vector pointing downward, reflecting upward.");
                randomDirection = Vector3.Reflect(randomDirection, Vector3.up);
            }

            // Apply logarithmic scaling based on damage for realistic force distribution  
            float modifier = baseValue *
                           Mathf.Log((3 * Damage) + 1) *
                           CalculateForcePush(Library_LabAPI.NpcConfig.KeterDamageVelocityModifier);

            return randomDirection * modifier;
        }

        #endregion
    }

}