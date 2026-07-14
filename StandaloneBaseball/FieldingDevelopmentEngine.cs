using System;

namespace StandaloneBaseball
{
    public static class FieldingDevelopmentEngine
    {
        public const int ErrorPenalty = 1;
        public const int CleanChancesForRecovery = 10;

        public static int EffectiveRating(Player player)
        {
            if (player == null) return 50;
            double classification = player.Classification switch
            {
                PlayerClassification.Freshman => 0.70,
                PlayerClassification.Sophomore => 0.80,
                PlayerClassification.Junior => 0.90,
                _ => 1.00
            };
            return Math.Clamp((int)Math.Round(InjuryEngine.EffectiveRating(player, player.Fielding) * classification), 0, 99);
        }

        public static int ApplyError(Player player)
        {
            if (player == null) return 0;
            player.ErrorFreeFieldingChanceStreak = 0;
            int before = player.Fielding;
            player.Fielding = Math.Max(0, player.Fielding - ErrorPenalty);
            int loss = before - player.Fielding;
            player.FieldingErrorPenaltyDebt += loss;
            return loss;
        }

        public static bool RegisterCleanChance(Player player)
        {
            if (player == null || player.FieldingErrorPenaltyDebt <= 0) return false;
            player.ErrorFreeFieldingChanceStreak++;
            if (player.ErrorFreeFieldingChanceStreak < CleanChancesForRecovery) return false;
            int before = player.Fielding;
            player.Fielding = Math.Min(99, player.Fielding + 1);
            if (player.Fielding > before)
                player.FieldingErrorPenaltyDebt--;
            player.ErrorFreeFieldingChanceStreak = 0;
            return player.Fielding > before;
        }
    }
}
