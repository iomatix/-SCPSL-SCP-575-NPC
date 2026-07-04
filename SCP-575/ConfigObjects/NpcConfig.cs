using LabApi.Extensions;
using System.ComponentModel;
using UnityEngine;
using Logger = LabApi.Extensions.Misc.iLogger;

namespace SCP_575.ConfigObjects
{
    /// <summary>
    /// Configuration settings governing the behavioral physics, combat modifiers, 
    /// tick rates, and termination constraints of the SCP-575 entity instance.
    /// </summary>
    public sealed class NpcConfig
    {
        #region NPC Termination Behavior
        [Description("Specifies whether SCP-575 can be terminated when all facility generators are fully engaged. If false, generator activation only resets its threat cycle.")]
        public bool IsNpcKillable { get; set; } = false;
        #endregion

        #region Ragdoll Settings
        [Description("Determines how kills by SCP-575 handle physical ragdoll assets. If true, no ragdoll is created on death, preventing physical tracking clutter.")]
        public bool DisableRagdolls { get; set; } = false;
        #endregion

        #region Timing Settings
        [Description("Baseline delay interval in seconds between consecutive SCP-575 behavior and damage ticking actions.")]
        public float KeterActionDelay { get; set; } = 47.75f;

        [Description("Variance randomizer threshold in seconds applied to the baseline ticking action delay (e.g., a value of 5 means a variance within a +/- 5s window).")]
        public float KeterActionDelayRandomizerValue { get; set; } = 12.5f;
        #endregion

        #region Damage Modifiers
        [Description("Armor penetration modifier coefficient assigned to SCP-575 damage profiles (0.0 for zero penetration, 1.0 for absolute true armor bypassing).")]
        public float KeterDamagePenetration { get; set; } = 0.85f;

        [Description("Velocity displacement scalar force applied to player movement tracks when sustaining a physical hit from SCP-575.")]
        public float KeterDamageVelocityModifier { get; set; } = 2.65f;
        #endregion

        #region Physics Force Modifiers
        [Description("Minimum impulse force modifier applied onto rigid-body ragdoll systems when struck by SCP-575.")]
        public float KeterForceMinModifier { get; set; } = 1.55f;

        [Description("Maximum impulse force modifier applied onto rigid-body ragdoll systems when struck by SCP-575.")]
        public float KeterForceMaxModifier { get; set; } = 2.45f;
        #endregion

        #region Validation Engine
        /// <summary>
        /// Validates core NPC configuration boundaries, sanitizing ticking metrics and protecting physics spaces against extreme limits.
        /// </summary>
        public void Validate()
        {
            // --- 1. Thread Action Loop Timing Safeguards ---
            // Fluent API Upgrade: Sanitize action deltas to prevent division-by-zero or infinite sub-frame loops
            KeterActionDelay = KeterActionDelay.LimitMin(0.5f);
            KeterActionDelayRandomizerValue = KeterActionDelayRandomizerValue.LimitMin(0f);

            // Defensive Check: Banish any risk of randomizer variance collapsing the lower-bound processing execution delays below 0.2s
            if (KeterActionDelay - KeterActionDelayRandomizerValue < 0.2f)
            {
                Logger.Warn(nameof(NpcConfig), $"Configured KeterActionDelayRandomizerValue ({KeterActionDelayRandomizerValue}s) creates a severe risk of sub-zero runtime timing scales against base delay ({KeterActionDelay}s). Normalizing to safe margin delta.");
                KeterActionDelayRandomizerValue = (KeterActionDelay - 0.2f).LimitMin(0f);
            }

            // --- 2. Armor Penetration Matrix Processing ---
            // Fluent API Upgrade: Binds firearms penetration metrics smoothly to normal percentage ranges (0.0 - 1.0)
            KeterDamagePenetration = KeterDamagePenetration.Clamp(0f, 1f);

            // --- 3. Velocity & Displacement Limiting (PhysX Boundary Protection) ---
            // Fluent API Upgrade: Restricts velocity forces to structural boundaries to insulate target movement against map geometry clipping artifacts
            KeterDamageVelocityModifier = KeterDamageVelocityModifier.Clamp(0f, 75f);

            // --- 4. Ragdoll RigidBody Impulse Constraints ---
            // Fluent API Upgrade: Clamp force coefficients inline to insulate Unity PhysX allocations against floating point infinity crashes
            KeterForceMinModifier = KeterForceMinModifier.Clamp(0f, 150f);
            KeterForceMaxModifier = KeterForceMaxModifier.Clamp(0f, 150f);

            if (KeterForceMinModifier > KeterForceMaxModifier)
            {
                Logger.Warn(nameof(NpcConfig), $"Ragdoll force coefficient bounds out of order: KeterForceMinModifier ({KeterForceMinModifier}) exceeded Max ({KeterForceMaxModifier}). Executing tuple-swap correction...");
                (KeterForceMinModifier, KeterForceMaxModifier) = (KeterForceMaxModifier, KeterForceMinModifier);
            }

            // Secure a nominal variance envelope track if vector calculations require a minimal delta offset value
            if (Mathf.Abs(KeterForceMaxModifier - KeterForceMinModifier) < 0.01f)
            {
                Logger.Warn(nameof(NpcConfig), "Identical min/max ragdoll force multipliers detected. Injected a safe 0.5 unit variance buffer envelope to the maximum bound threshold.");
                KeterForceMaxModifier += 0.5f;
            }
        }
        #endregion
    }
}