namespace SCP_575.Handlers
{
    using LabApi.Events.Arguments.PlayerEvents;
    using LabApi.Events.CustomHandlers;
    using SCP_575.Shared;
    using SCP_575.Systems;
    using System;

    /// <summary>
    /// Handles ragdoll spawning logic for SCP-575 related kills.
    /// </summary>
    public class RagdollHandler : CustomEventsHandler
    {
        private readonly Plugin _plugin;

        public RagdollHandler(Plugin plugin)
        {
            _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
        }

        public override void OnPlayerSpawnedRagdoll(PlayerSpawnedRagdollEventArgs ev)
        {
            if (!_plugin.IsEventActive)
                return;

            try
            {
                if (ev?.Player == null || ev.Ragdoll == null || ev.DamageHandler == null)
                {
                    LibraryLabAPI.LogDebug(
                        "RagdollHandler",
                        "Invalid ragdoll event arguments.");
                    return;
                }

                LibraryLabAPI.LogDebug(
                    "RagdollHandler",
                    $"Ragdoll spawned for player {ev.Player.Nickname} at {ev.Ragdoll.Position}");

                if (!Scp575DamageSystem.IsScp575Damage(ev.DamageHandler))
                {
                    LibraryLabAPI.LogDebug(
                        "RagdollHandler",
                        "Ragdoll not caused by SCP-575.");
                    return;
                }

                LibraryLabAPI.LogDebug(
                    "RagdollHandler",
                    $"Ragdoll caused by {Scp575DamageSystem.IdentifierName}");

                if (_plugin.Config.NpcConfig.DisableRagdolls)
                {
                    LibraryLabAPI.LogDebug(
                        "RagdollHandler",
                        "DisableRagdolls enabled. Destroying ragdoll.");

                    ev.Ragdoll.Destroy();
                }
                else
                {
                    LibraryLabAPI.LogDebug(
                        "RagdollHandler",
                        "Processing ragdoll for SCP-575 skeleton spawn.");

                    Scp575DamageSystem.RagdollProcessor(ev.Player, ev.Ragdoll);
                }
            }
            catch (Exception ex)
            {
                LibraryLabAPI.LogError(
                    "RagdollHandler",
                    $"Failed to process ragdoll event: {ex}");
            }
        }
    }
}