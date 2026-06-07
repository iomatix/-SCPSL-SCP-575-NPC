namespace SCP_575.ConfigObjects
{
    using System.ComponentModel;
    using Exiled.API.Features;
    using UnityEngine;

    public sealed class NpcConfig
    {
        #region NPC Termination Behavior

        /// <summary>
        /// Specifies whether SCP-575 can be terminated when all generators are engaged.
        /// If false, generator activation only halts SCP-575's behavior and resets its event state.
        /// </summary>
        [Description("Specifies whether SCP-575 can be terminated when all generators are engaged.")]
        public bool IsNpcKillable { get; set; } = false;

        #endregion

        #region Ragdoll Settings

        /// <summary>
        /// Determines how kills by SCP-575 handle ragdolls.
        /// If false, a skeleton ragdoll is spawned instead of the default one.
        /// If true, no ragdoll is created upon death.
        /// </summary>
        [Description("Determines whether to disable ragdolls for SCP-575 kills.")]
        public bool DisableRagdolls { get; set; } = false;

        #endregion

        #region Timing Settings

        /// <summary>
        /// Delay in seconds between SCP-575 action ticks.
        /// </summary>
        [Description("The delay of receiving damage.")]
        public float KeterActionDelay { get; set; } = 17.485f;

        #endregion

        #region Damage Modifiers

        /// <summary>
        /// Penetration modifier for SCP-575 damage (0.0 to 1.0).
        /// </summary>
        [Description("Penetration modifier same as in FirearmsDamageHandler.")]
        public float KeterDamagePenetration { get; set; } = 0.75f;

        /// <summary>
        /// Modifier applied to player velocity when damaged by SCP-575.
        /// </summary>
        [Description("The modifier applied to velocity when players are damaged by SCP-575.")]
        public float KeterDamageVelocityModifier { get; set; } = 2.45f;

        #endregion

        #region Physics Force Modifiers

        /// <summary>
        /// Minimum force modifier applied to ragdolls damaged by SCP-575.
        /// </summary>
        [Description("The minimum modifier applied to ragdolls when they were damaged by SCP-575.")]
        public float KeterForceMinModifier { get; set; } = 0.75f;

        /// <summary>
        /// Maximum force modifier applied to ragdolls damaged by SCP-575.
        /// </summary>
        [Description("The maximum modifier applied to ragdolls when they were damaged by SCP-575.")]
        public float KeterForceMaxModifier { get; set; } = 2.35f;

        #endregion

        /// <summary>
        /// Validates the NPC configuration parameters and corrects invalid input.
        /// </summary>
        public void Validate()
        {
            if (KeterActionDelay < 0f)
            {
                Log.Warn("[NpcConfig] KeterActionDelay cannot be negative. Resetting to 0.");
                KeterActionDelay = 0f;
            }

            KeterDamagePenetration = Mathf.Clamp(KeterDamagePenetration, 0f, 1f);

            if (KeterDamageVelocityModifier < 0f)
            {
                Log.Warn("[NpcConfig] KeterDamageVelocityModifier cannot be negative. Resetting to 0.");
                KeterDamageVelocityModifier = 0f;
            }

            if (KeterForceMinModifier < 0f) KeterForceMinModifier = 0f;
            if (KeterForceMaxModifier < 0f) KeterForceMaxModifier = 0f;

            if (KeterForceMinModifier > KeterForceMaxModifier)
            {
                float temp = KeterForceMinModifier;
                KeterForceMinModifier = KeterForceMaxModifier;
                KeterForceMaxModifier = temp;
                Log.Warn("[NpcConfig] KeterForceMinModifier was greater than KeterForceMaxModifier. Values have been swapped.");
            }
        }
    }
}