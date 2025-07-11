namespace SCP_575.Shared
{
    using UnityEngine;
    using Exiled.API.Features;
    using static PlayerStatsSystem.DamageHandlerBase;

    public static class Scp575DamageHandler_ExiledAPI
    {
        public static void HandleApplyDamageFeedback(ReferenceHub ply, float damage, HandlerOutput result)
        {
            // Place a blood decal on the Exiled player
            Player player = Player.Get(ply);
            player.PlaceBlood(new Vector3(0f, 0f, -1f));

            // Unified debug logging
            switch (result)
            {
                case HandlerOutput.Death:
                    Log.Debug($"[ApplyDamage] {player.Nickname} died ({damage:F1})");
                    break;
                case HandlerOutput.Damaged:
                    Log.Debug(
                        $"[ApplyDamage] {player.Nickname} took {damage:F1}, " +
                        $"remaining HP: {player.Health:F1}"
                    );
                    break;
                default:
                    Log.Debug(
                        $"[ApplyDamage] Non‐damaging interaction on " +
                        $"{player.Nickname}, result={result}"
                    );
                    break;
            }
        }


    }
}
