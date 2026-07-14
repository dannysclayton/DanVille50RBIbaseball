using System;

namespace StandaloneBaseball
{
    public sealed class BalkResult
    {
        public bool IsBalk { get; set; }
        public int ChanceBasisPoints { get; set; }
        public string Reason { get; set; } = "";
    }

    public static class BalkEngine
    {
        public static BalkResult Roll(
            Random rng,
            Player pitcher,
            int pitcherAdjustmentPercent,
            DefensiveStealCall defensiveCall,
            int repeatedPickoffAttempts,
            bool runnerOnThird,
            bool stealThreat,
            bool highPressure)
        {
            if (rng == null)
                throw new ArgumentNullException(nameof(rng));

            int accuracy = Rating(pitcher, p => p.Accuracy, 50);
            int hold = Rating(pitcher, p => p.HoldRunner, 50);
            int pickoff = Rating(pitcher, p => p.Pickoff, 50);
            int delivery = Rating(pitcher, p => p.DeliveryTime, 50);
            int chance = 4;

            chance += Math.Max(0, 62 - accuracy) / 2;
            chance += Math.Max(0, 60 - ((hold + pickoff + delivery) / 3)) / 3;
            chance += ClassificationRisk(pitcher?.Classification ?? PlayerClassification.Unassigned);
            chance += Math.Max(0, -pitcherAdjustmentPercent) / 2;
            if (pitcher?.InjuryStatus == PlayerInjuryStatus.DayToDay)
                chance += 5;
            if (runnerOnThird)
                chance += 2;
            if (stealThreat)
                chance += 3;
            if (highPressure)
                chance += 4;

            chance += defensiveCall switch
            {
                DefensiveStealCall.HoldRunner => 1,
                DefensiveStealCall.SlideStep => 4,
                DefensiveStealCall.Pitchout => 2,
                DefensiveStealCall.Pickoff => 6,
                _ => 0
            };
            chance += Math.Clamp(repeatedPickoffAttempts - 1, 0, 4) * 5;
            chance = Math.Clamp(chance, 2, 75);

            bool isBalk = rng.Next(10000) < chance;
            return new BalkResult
            {
                IsBalk = isBalk,
                ChanceBasisPoints = chance,
                Reason = isBalk ? BalkReason(rng, defensiveCall, repeatedPickoffAttempts) : ""
            };
        }

        private static int ClassificationRisk(PlayerClassification classification)
            => classification switch
            {
                PlayerClassification.Freshman => 9,
                PlayerClassification.Sophomore => 6,
                PlayerClassification.Junior => 3,
                PlayerClassification.Senior => 0,
                _ => 5
            };

        private static string BalkReason(Random rng, DefensiveStealCall call, int repeatedPickoffAttempts)
        {
            if (call == DefensiveStealCall.Pickoff || repeatedPickoffAttempts > 1)
                return rng.Next(2) == 0 ? "illegal pickoff move" : "failed to step toward the base";
            if (call == DefensiveStealCall.SlideStep || call == DefensiveStealCall.Pitchout)
                return rng.Next(2) == 0 ? "quick pitch" : "failed to come set";

            string[] reasons = { "failed to come set", "interrupted delivery", "illegal pitching motion", "dropped the ball on the rubber" };
            return reasons[rng.Next(reasons.Length)];
        }

        private static int Rating(Player player, Func<Player, int> selector, int fallback)
            => player == null ? fallback : Math.Clamp(selector(player), 0, 99);
    }
}
