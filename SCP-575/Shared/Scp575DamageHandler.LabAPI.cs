namespace SCP_575.Shared
{
    using CustomPlayerEffects;
    using LabApi.Features.Wrappers;

    public static class Scp575DamageHandler_LabAPI
    {
        public static void ApplyDamageEffects(Player player)
        {
            // Visual & status effects from LabAPI
            player.EnableEffect<Ensnared>(duration: 0.35f);
            player.EnableEffect<Flashed>(duration: 0.075f);
            player.EnableEffect<Blurred>(duration: 0.25f);

            player.EnableEffect<Deafened>(duration: 3.75f);
            player.EnableEffect<AmnesiaVision>(duration: 3.65f);
            player.EnableEffect<Sinkhole>(duration: 3.25f);
            player.EnableEffect<Concussed>(duration: 3.15f);
            player.EnableEffect<Blindness>(duration: 2.65f);
            player.EnableEffect<Burned>(duration: 2.5f, intensity: 3); // Intensity of three: Damage is increased by 8.75%.

            player.EnableEffect<AmnesiaItems>(duration: 1.65f);
            player.EnableEffect<Stained>(duration: 0.75f);
            player.EnableEffect<Asphyxiated>(duration: 1.25f, intensity: 3); // Intensity of three: Stamina drains at 1.75% per second. HP drains at 0.7 per second.

            player.EnableEffect<Bleeding>(duration: 3.65f, intensity: 3); // Intensity of three: Damage values are 7, 3.5, 1.75, 0.875 and 0.7.
            player.EnableEffect<Disabled>(duration: 4.75f, intensity: 1); // Intensity of one: Movement is slowed down by 12%.
            player.EnableEffect<Exhausted>(duration: 6.75f);
            player.EnableEffect<Traumatized>(duration: 9.5f);
        }
    }
}