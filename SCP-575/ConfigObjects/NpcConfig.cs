using System;
using System.ComponentModel;

namespace SCP_575.ConfigObjects
{
    public sealed class NpcConfig
    {
        #region NPC Termination Behavior

        /// <summary>
        /// Specifies whether SCP-575 can be terminated when all generators are engaged.
        /// If false, generator activation only halts SCP-575's behavior and resets its event state.
        /// </summary>
        [Description("Specifies whether SCP-575 can be terminated when all generators are engaged.")]
        public bool IsNpcKillable { get; private set; } = false;

        #endregion

        #region Ragdoll Settings

        /// <summary>
        /// Determines how kills by SCP-575 handle ragdolls.
        /// If false, a skeleton ragdoll is spawned instead of the default one.
        /// If true, no ragdoll is created upon death.
        /// </summary>
        [Description("Determines whether to disable ragdolls for SCP-575 kills.")]
        public bool DisableRagdolls { get; private set; } = false;

        #endregion

        #region Timing Settings

        /// <summary>
        /// Delay in seconds between SCP-575 action ticks.
        /// </summary>
        [Description("The delay of receiving damage.")]
        public float KeterActionDelay
        {
            get => _keterActionDelay;
            private set => _keterActionDelay = value < 0f ? 0f : value;
        }
        private float _keterActionDelay = 13.85f;

        #endregion

        #region Damage Modifiers

        /// <summary>
        /// Penetration modifier for SCP-575 damage (0.0 to 1.0).
        /// </summary>
        [Description("Penetration modifier same as in FirearmsDamageHandler.")]
        public float KeterDamagePenetration
        {
            get => _keterDamagePenetration;
            set => _keterDamagePenetration = value < 0f ? 0f : value > 1f ? 1f : value;
        }
        private float _keterDamagePenetration = 0.75f;

        /// <summary>
        /// Modifier applied to player velocity when damaged by SCP-575.
        /// </summary>
        [Description("The modifier applied to velocity when players are damaged by SCP-575.")]
        public float KeterDamageVelocityModifier
        {
            get => _keterDamageVelocityModifier;
            set => _keterDamageVelocityModifier = value < 0f ? 0f : value;
        }
        private float _keterDamageVelocityModifier = 1.25f;

        #endregion

        #region Physics Force Modifiers

        /// <summary>
        /// Minimum force modifier applied to ragdolls damaged by SCP-575.
        /// </summary>
        [Description("The minimum modifier applied to ragdolls when they were damaged by SCP-575.")]
        public float KeterForceMinModifier
        {
            get => _keterForceMinModifier;
            set => _keterForceMinModifier = value < 0f ? 0f : value;
        }
        private float _keterForceMinModifier = 0.75f;

        /// <summary>
        /// Maximum force modifier applied to ragdolls damaged by SCP-575.
        /// </summary>
        [Description("The maximum modifier applied to ragdolls when they were damaged by SCP-575.")]
        public float KeterForceMaxModifier
        {
            get => _keterForceMaxModifier;
            set => _keterForceMaxModifier = value < 0f ? 0f : value;
        }
        private float _keterForceMaxModifier = 2.35f;

        #endregion
    }
}