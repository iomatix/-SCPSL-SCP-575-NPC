using LabApi.Extensions;
using System.ComponentModel;
using Logger = LabApi.Extensions.Misc.iLogger;

namespace SCP_575.ConfigObjects
{
    /// <summary>
    /// Configuration settings managing the procedural flashlight spawning pipeline during round start initialization.
    /// Controls independent item drop distribution frequencies across distinct facility zones.
    /// </summary>
    public sealed class FlashlightSpawnConfig
    {
        #region Serialized Properties
        [Description("Enable or disable the independent flashlight generation pipeline on round start.")]
        public bool IsEnabled { get; set; } = true;

        [Description("Spawn chance percentage per individual room tracked within the Light Containment Zone.")]
        public float ChanceLight { get; set; } = 15f;

        [Description("Spawn chance percentage per individual room tracked within the Heavy Containment Zone.")]
        public float ChanceHeavy { get; set; } = 5f;

        [Description("Spawn chance percentage per individual room tracked within the Entrance Zone.")]
        public float ChanceEntrance { get; set; } = 3f;

        [Description("Spawn chance percentage per individual room located on the Surface sector grid.")]
        public float ChanceSurface { get; set; } = 1f;

        [Description("Spawn chance percentage per individual room mapped inside unindexed or custom zones.")]
        public float ChanceOther { get; set; } = 0f;
        #endregion

        #region Validation Engine
        /// <summary>
        /// Validates zone probability distributions and clamps generation ranges utilizing high-performance fluent math extensions.
        /// </summary>
        public void Validate()
        {
            // Fluent API Upgrade: Eradicate UnityEngine.Mathf boilerplate completely via inline float clamping extensions
            ChanceLight = ChanceLight.Clamp(0f, 100f);
            ChanceHeavy = ChanceHeavy.Clamp(0f, 100f);
            ChanceEntrance = ChanceEntrance.Clamp(0f, 100f);
            ChanceSurface = ChanceSurface.Clamp(0f, 100f);
            ChanceOther = ChanceOther.Clamp(0f, 100f);

            // --- Pipeline Resource Conservation Safeguard ---
            // If the operational generation loop is active but all probability matrices evaluate to an absolute zero baseline,
            // intercept execution and force-disable the module. This cleanly short-circuits expensive map-wide Room iteration 
            // sequences during heavy server-side round initialization tasks, optimizing overall CPU headroom scaling tracks.
            if (IsEnabled && ChanceLight <= 0f && ChanceHeavy <= 0f && ChanceEntrance <= 0f && ChanceSurface <= 0f && ChanceOther <= 0f)
            {
                Logger.Warn(nameof(FlashlightSpawnConfig), "Flashlight generation pipeline is enabled, but all room spawn probability metrics evaluate to 0%. Forcibly disabling the generation module to conserve server processing resources.");
                IsEnabled = false;
            }
        }
        #endregion
    }
}