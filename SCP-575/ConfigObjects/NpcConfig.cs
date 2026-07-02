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
        public float KeterActionDelay { get; set; } = 47.75f;

        /// <summary>
        /// Randomizer value for the delay in seconds between SCP-575 action ticks.
        /// e.g. KeterActionDelayRandomizerValue = 5 means a random between -5 and 5 seconds.
        /// </summary>
        [Description("The randomizer value for the delay of receiving damage.")]
        public float KeterActionDelayRandomizerValue { get; set; } = 12.5f;

        #endregion

        #region Damage Modifiers

        /// <summary>
        /// Penetration modifier for SCP-575 damage (0.0 to 1.0).
        /// </summary>
        [Description("Penetration modifier same as in FirearmsDamageHandler.")]
        public float KeterDamagePenetration { get; set; } = 0.85f;

        /// <summary>
        /// Modifier applied to player velocity when damaged by SCP-575.
        /// </summary>
        [Description("The modifier applied to velocity when players are damaged by SCP-575.")]
        public float KeterDamageVelocityModifier { get; set; } = 2.65f;

        #endregion

        #region Physics Force Modifiers

        /// <summary>
        /// Minimum force modifier applied to ragdolls damaged by SCP-575.
        /// </summary>
        [Description("The minimum modifier applied to ragdolls when they were damaged by SCP-575.")]
        public float KeterForceMinModifier { get; set; } = 1.55f;

        /// <summary>
        /// Maximum force modifier applied to ragdolls damaged by SCP-575.
        /// </summary>
        [Description("The maximum modifier applied to ragdolls when they were damaged by SCP-575.")]
        public float KeterForceMaxModifier { get; set; } = 2.45f;

        #endregion

        /// <summary>
        /// Validates the NPC configuration parameters and corrects invalid input.
        /// </summary>
        public void Validate()
        {
            // --- 1. Thread Action Loop Timing Safeguards ---
            // Ensure the background action loop doesn't spin infinitely on a 0s tick interval
            KeterActionDelay = Mathf.Max(0.5f, KeterActionDelay);
            KeterActionDelayRandomizerValue = Mathf.Max(0f, KeterActionDelayRandomizerValue);

            // Critical: Ensure lower bound calculation (Delay - Randomizer) never yields 0s or negative timing increments
            if (KeterActionDelay - KeterActionDelayRandomizerValue < 0.2f)
            {
                Log.Warn("[NpcConfig] KeterActionDelayRandomizerValue creates a risk of zero or sub-zero runtime windows. Enforcing safety margin.");
                KeterActionDelayRandomizerValue = Mathf.Max(0f, KeterActionDelay - 0.2f);
            }

            // --- 2. Armor Penetration Matrix Processing ---
            KeterDamagePenetration = Mathf.Clamp01(KeterDamagePenetration);

            // --- 3. Velocity & Displacement Limiting (PhysX Boundary Protection) ---
            // Forces reasonable boundaries to prevent players from clipping through geometry on impact
            KeterDamageVelocityModifier = Mathf.Clamp(KeterDamageVelocityModifier, 0f, 75f);

            // --- 4. Ragdoll RigidBody Impulse Constraints ---
            KeterForceMinModifier = Mathf.Clamp(KeterForceMinModifier, 0f, 150f);
            KeterForceMaxModifier = Mathf.Clamp(KeterForceMaxModifier, 0f, 150f);

            if (KeterForceMinModifier > KeterForceMaxModifier)
            {
                Log.Warn("[NpcConfig] KeterForceMinModifier was greater than KeterForceMaxModifier. Swapping boundaries.");
                (KeterForceMinModifier, KeterForceMaxModifier) = (KeterForceMaxModifier, KeterForceMinModifier);
            }

            // Prevent exact single-point distribution vectors if variance calculation requires a delta range
            if (Mathf.Abs(KeterForceMaxModifier - KeterForceMinModifier) < 0.01f)
            {
                KeterForceMaxModifier += 0.5f;
            }
        }
    }
}