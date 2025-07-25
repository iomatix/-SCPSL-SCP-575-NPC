namespace SCP_575.ConfigObjects
{
    using System.ComponentModel;

    public sealed class PlayerLightsourceConfig
    {
        /// <summary>
        /// Gets or sets the cooldown duration (in seconds) for light sources after being hit by SCP-575.
        /// </summary>
        [Description("Cooldown on the light source triggered on hit by SCP-575.")]
        public float KeterLightsourceCooldown
        {
            get => _keterLightsourceCooldown;
            private set => _keterLightsourceCooldown = value < 0f ? 0f : value;
        }
        private float _keterLightsourceCooldown = 7.25f;
    }
}