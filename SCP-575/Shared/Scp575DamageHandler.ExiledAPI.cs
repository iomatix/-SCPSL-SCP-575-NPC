namespace SCP_575.Shared
{
    using Exiled.API.Features;
    using SCP_575.ConfigObjects;
    using UnityEngine;
    using static PlayerStatsSystem.DamageHandlerBase;

    /// <summary>
    /// Provides Exiled API–based utility methods for handling visual and debug feedback 
    /// during SCP-575 damage processing.
    /// </summary>
    public static class Scp575DamageHandler_ExiledAPI
    {

        /// <summary>
        /// Applies visual feedback and structured debug logging when damage is dealt to a player.
        /// </summary>
        /// <param name="ply">The reference hub of the damaged player.</param>
        /// <param name="damage">The amount of damage dealt.</param>
        /// <param name="result">The result of the damage handling process, indicating the outcome (e.g., death, damage).</param>
        /// <remarks>
        /// This method places a blood decal behind the player and logs outcome-specific damage data. 
        /// It supports unified logging for death, partial damage, and non-damaging interactions.
        /// </remarks>
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
