using System;

namespace StandaloneBaseball
{
    public enum PitchEscapeKind
    {
        None,
        WildPitch,
        PassedBall
    }

    public sealed class PitchEscapeAdvanceResult
    {
        public bool Attempt { get; set; }
        public bool RunnerOut { get; set; }
        public int TargetBase { get; set; }
        public string Detail { get; set; } = "";
    }

    public static class PitchEscapeEngine
    {
        public static PitchEscapeKind Roll(
            Random rng,
            Player pitcher,
            Player catcher,
            GameplayPitchType pitchType,
            double pitchX,
            double pitchY,
            int pitcherAdjustmentPercent,
            int catcherBlockingRating)
        {
            if (rng == null)
                return PitchEscapeKind.None;

            int control = PitcherControl(pitcher);
            int fatigueRisk = Math.Abs(pitcherAdjustmentPercent);
            int pitchDifficulty = PitchBlockDifficulty(pitchType);
            double distance = Math.Sqrt(pitchX * pitchX + pitchY * pitchY);
            int locationRisk = (int)Math.Round(Math.Max(0.0, distance - 0.72) * 22.0);
            int block = Math.Clamp(catcherBlockingRating, 1, 99);
            int escapeChance = 4 +
                pitchDifficulty +
                Math.Max(0, 55 - control) / 4 +
                Math.Max(0, 46 - block) / 5 +
                locationRisk +
                fatigueRisk / 8;

            escapeChance = Math.Clamp(escapeChance, 0, 95);
            if (rng.Next(1000) >= escapeChance)
                return PitchEscapeKind.None;

            int pitcherFault = Math.Max(1, Math.Max(0, 54 - control) + locationRisk * 2 + pitchDifficulty + fatigueRisk / 3);
            int catcherFault = Math.Max(1, Math.Max(0, 58 - block) + pitchDifficulty / 2);
            if (distance > 1.18)
                pitcherFault += 18;

            return rng.Next(pitcherFault + catcherFault) < pitcherFault
                ? PitchEscapeKind.WildPitch
                : PitchEscapeKind.PassedBall;
        }

        public static PitchEscapeAdvanceResult ResolveAdvance(
            Random rng,
            Player runner,
            int fromBase,
            int outs,
            int scoreDifferential,
            Player catcher,
            Player targetFielder,
            PitchEscapeKind kind,
            bool forcedAdvance = false)
        {
            var result = new PitchEscapeAdvanceResult
            {
                TargetBase = Math.Clamp(fromBase + 1, 1, 4)
            };
            if (rng == null || runner == null || kind == PitchEscapeKind.None || fromBase < 1 || fromBase > 3)
                return result;

            int runnerScore = Rating(runner, p => p.Speed, 50) +
                Rating(runner, p => p.BaseRunning, 50) +
                rng.Next(-16, 17);
            if (fromBase == 3)
                runnerScore += 8;
            if (outs == 2)
                runnerScore += 4;
            if (scoreDifferential < 0 && scoreDifferential >= -2)
                runnerScore += 6;
            if (forcedAdvance)
                runnerScore += 18;

            int holdThreshold = fromBase == 3 ? 92 : 84;
            if (!forcedAdvance && runnerScore < holdThreshold)
            {
                result.Detail = runner.Name + " holds.";
                return result;
            }

            result.Attempt = true;
            int defenseScore = Rating(catcher, p => p.Fielding, 50) +
                Rating(catcher, p => p.Accuracy, 50) / 2 +
                Rating(catcher, p => p.ArmStrength, 50) / 2 +
                Rating(targetFielder, p => p.TagRating, 50) / 2 +
                rng.Next(-16, 17);
            if (kind == PitchEscapeKind.WildPitch)
                defenseScore -= 6;
            if (result.TargetBase == 4)
                defenseScore -= 8;

            result.RunnerOut = defenseScore >= runnerScore + 18;
            result.Detail = result.RunnerOut
                ? runner.Name + " is thrown out trying to advance."
                : runner.Name + " advances.";
            return result;
        }

        private static int PitcherControl(Player pitcher)
            => pitcher == null
                ? 45
                : (Rating(pitcher, p => p.Pitching, 50) + Rating(pitcher, p => p.Accuracy, 50)) / 2;

        private static int PitchBlockDifficulty(GameplayPitchType pitchType)
            => pitchType switch
            {
                GameplayPitchType.Changeup => 2,
                GameplayPitchType.Slider => 3,
                GameplayPitchType.Curveball => 4,
                GameplayPitchType.Splitter => 6,
                GameplayPitchType.Forkball => 7,
                GameplayPitchType.Knuckleball => 8,
                _ => 0
            };

        private static int Rating(Player player, Func<Player, int> selector, int fallback)
            => player == null ? fallback : InjuryEngine.EffectiveRating(player, selector(player));
    }
}
