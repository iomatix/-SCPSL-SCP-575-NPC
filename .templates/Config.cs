namespace Company.PluginProject
{
    using System.ComponentModel;
    using UnityEngine;
    using PlayerRoles;
    using MapGeneration;
    using PlayerStatsSystem;

    public sealed class Config
    {
        [Description("Enable or disable the plugin.")]
        public bool IsEnabled { get; set; } = true;

        [Description("Enable debug logging.")]
        public bool Debug { get; set; } = false;

        /// <summary>
        /// Validates core plugin settings.
        /// </summary>
        public void Validate()
        {
            // Fallback rules block
        }
    }
}