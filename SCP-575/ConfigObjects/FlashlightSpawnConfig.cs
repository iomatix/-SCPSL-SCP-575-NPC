namespace SCP_575.ConfigObjects
{
    using System.ComponentModel;
    using UnityEngine;

    public sealed class FlashlightSpawnConfig
    {
        [Description("Enable or disable the independent flashlight generation pipeline on round start.")]
        public bool IsEnabled { get; set; } = true;

        [Description("Spawn chance (%) per individual room in the Light Containment Zone.")]
        public float ChanceLight { get; set; } = 15f;

        [Description("Spawn chance (%) per individual room in the Heavy Containment Zone.")]
        public float ChanceHeavy { get; set; } = 5f;

        [Description("Spawn chance (%) per individual room in the Entrance Zone.")]
        public float ChanceEntrance { get; set; } = 3f;

        [Description("Spawn chance (%) per individual room on the Surface (for the meme/joke).")]
        public float ChanceSurface { get; set; } = 1f;

        [Description("Spawn chance (%) per individual room in unmapped or custom zones.")]
        public float ChanceOther { get; set; } = 0f;

        /// <summary>
        /// Validates and clamps configuration values to ensure physics and math integrity.
        /// </summary>
        public void Validate()
        {
            ChanceLight = Mathf.Clamp(ChanceLight, 0f, 100f);
            ChanceHeavy = Mathf.Clamp(ChanceHeavy, 0f, 100f);
            ChanceEntrance = Mathf.Clamp(ChanceEntrance, 0f, 100f);
            ChanceSurface = Mathf.Clamp(ChanceSurface, 0f, 100f);
            ChanceOther = Mathf.Clamp(ChanceOther, 0f, 100f);
        }
    }
}