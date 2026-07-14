#nullable enable annotations

using System;

namespace StandaloneBaseball
{
    public static class CoachDecisionEngine
    {
        public static int CorrectCallChance(Coach? coach)
        {
            return coach?.Style switch
            {
                CoachStyle.BelowAverage => 25,
                CoachStyle.AboveAverage => 75,
                CoachStyle.Championship => 100,
                _ => 50
            };
        }

        public static bool MakesCorrectCall(Random? rng, Coach? coach)
        {
            if (coach?.Style == CoachStyle.Championship)
                return true;

            return (rng ?? Random.Shared).Next(100) < CorrectCallChance(coach);
        }

        public static bool ShouldCallRiskyOffense(
            Random? rng,
            Coach? coach,
            bool rightCall,
            bool gameOnLine,
            bool scoringOpportunity)
        {
            rng ??= Random.Shared;
            bool correct = MakesCorrectCall(rng, coach);
            return coach?.Strategy switch
            {
                CoachStrategy.Safe => false,
                CoachStrategy.Aggressive => correct
                    ? (rightCall || scoringOpportunity || gameOnLine)
                    : (scoringOpportunity || gameOnLine || rng.Next(100) < 65),
                _ => correct
                    ? (rightCall && gameOnLine)
                    : (!rightCall && gameOnLine && rng.Next(100) < 35)
            };
        }

        public static bool ShouldCallSafeOffense(
            Random rng,
            Coach coach,
            bool rightCall,
            bool gameOnLine,
            bool scoringOpportunity)
        {
            rng ??= Random.Shared;
            bool correct = MakesCorrectCall(rng, coach);
            return coach?.Strategy switch
            {
                CoachStrategy.Safe => rightCall || scoringOpportunity,
                CoachStrategy.Aggressive => correct
                    ? (rightCall && scoringOpportunity)
                    : (rightCall && rng.Next(100) < 25),
                _ => correct
                    ? (rightCall && (gameOnLine || scoringOpportunity))
                    : (!rightCall && gameOnLine && rng.Next(100) < 25)
            };
        }

        public static bool ShouldCallPreventDefense(
            Random rng,
            Coach coach,
            bool rightCall,
            bool gameOnLine,
            bool runThreat)
        {
            rng ??= Random.Shared;
            bool correct = MakesCorrectCall(rng, coach);
            return coach?.Strategy switch
            {
                CoachStrategy.Safe => correct && rightCall,
                CoachStrategy.Aggressive => correct
                    ? (rightCall || runThreat || gameOnLine)
                    : (runThreat || gameOnLine || rng.Next(100) < 65),
                _ => correct
                    ? (rightCall && (gameOnLine || runThreat))
                    : (!rightCall && gameOnLine && rng.Next(100) < 30)
            };
        }

        public static int StrategyExecutionModifier(Coach coach, bool correctCall)
        {
            int chance = CorrectCallChance(coach);
            int qualityBonus = chance switch
            {
                >= 100 => 12,
                >= 75 => 8,
                >= 50 => 4,
                _ => 0
            };

            return correctCall ? qualityBonus : -(12 - qualityBonus / 2);
        }
    }
}
