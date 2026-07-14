#nullable enable annotations

using System;

namespace StandaloneBaseball
{
    public enum DefensiveStealCall
    {
        Normal,
        HoldRunner,
        SlideStep,
        Pitchout,
        Pickoff
    }

    public enum StealAttemptOutcome
    {
        Safe,
        CaughtStealing,
        PickedOff,
        ThrowingError
    }

    public sealed class StealAttemptResult
    {
        public StealAttemptOutcome Outcome { get; set; }
        public int FromBase { get; set; }
        public int TargetBase { get; set; }
        public int FinalBase { get; set; }
        public int RunsScored { get; set; }
        public int JumpScore { get; set; }
        public int ThrowScore { get; set; }
        public int TagScore { get; set; }
        public string Detail { get; set; } = "";
        public bool SuccessfulSteal => Outcome == StealAttemptOutcome.Safe || Outcome == StealAttemptOutcome.ThrowingError;
        public bool RunnerOut => Outcome == StealAttemptOutcome.CaughtStealing || Outcome == StealAttemptOutcome.PickedOff;
    }

    public static class StealEngine
    {
        public static StealAttemptResult Resolve(
            Random rng,
            Player? runner,
            Player? pitcher,
            Player? catcher,
            Player? tagFielder,
            int fromBase,
            int outs,
            int balls,
            int strikes,
            int scoreDifferential,
            DefensiveStealCall defensiveCall)
        {
            if (rng == null)
                throw new ArgumentNullException(nameof(rng));

            fromBase = Math.Clamp(fromBase, 1, 3);
            outs = Math.Clamp(outs, 0, 2);
            balls = Math.Clamp(balls, 0, 3);
            strikes = Math.Clamp(strikes, 0, 2);
            int targetBase = fromBase + 1;

            int speed = Rating(runner, p => p.Speed, 50);
            int aggression = Rating(runner, p => p.StealAggression, 50);
            int baserunning = Rating(runner, p => p.BaseRunning, 50);
            int hold = Rating(pitcher, p => p.HoldRunner, Rating(pitcher, p => p.Pitching, 50));
            int pickoff = Rating(pitcher, p => p.Pickoff, Rating(pitcher, p => p.Pitching, 50));
            int delivery = Rating(pitcher, p => p.DeliveryTime, Rating(pitcher, p => p.Stamina, 50));
            int arm = Rating(catcher, p => p.ArmStrength, Rating(catcher, p => p.Fielding, 50));
            int pop = Rating(catcher, p => p.PopTime, Rating(catcher, p => p.Fielding, 50));
            int accuracy = Rating(catcher, p => p.Accuracy, Rating(catcher, p => p.Fielding, 50));
            int tag = Rating(tagFielder, p => p.TagRating, Rating(tagFielder, p => p.Fielding, 50));

            FormationModifiers(defensiveCall, out int jumpMod, out int throwMod, out int tagMod, out int pickoffMod, out int errorMod);
            int countMod = balls > strikes ? 4 : strikes == 2 ? -5 : 0;
            int pressureMod = scoreDifferential < 0 ? 4 : scoreDifferential > 2 ? -5 : 0;
            int baseMod = fromBase == 3 ? -12 : fromBase == 2 ? -4 : 0;

            int jumpScore = speed * 2 + aggression + baserunning + countMod + pressureMod + baseMod + rng.Next(-16, 17)
                - hold - delivery + jumpMod;

            if (defensiveCall == DefensiveStealCall.Pickoff)
            {
                int pickoffScore = pickoff + hold + rng.Next(-18, 19) + pickoffMod - speed - baserunning / 2;
                if (pickoffScore >= 24)
                {
                    return new StealAttemptResult
                    {
                        Outcome = StealAttemptOutcome.PickedOff,
                        FromBase = fromBase,
                        TargetBase = targetBase,
                        FinalBase = 0,
                        JumpScore = jumpScore,
                        ThrowScore = pickoffScore,
                        TagScore = tag,
                        Detail = "Picked off"
                    };
                }
            }

            int throwScore = arm + pop + accuracy / 2 + rng.Next(-18, 19) + throwMod - (speed + baserunning / 2);
            int tagScore = tag + rng.Next(-12, 13) + tagMod - baserunning / 3;
            int defenseScore = throwScore + tagScore / 2;
            int stealScore = jumpScore - defenseScore;

            int errorChance = Math.Clamp(9 + errorMod + (70 - accuracy) / 5, 1, 24);
            if (rng.Next(100) < errorChance && stealScore > -18)
            {
                return new StealAttemptResult
                {
                    Outcome = StealAttemptOutcome.ThrowingError,
                    FromBase = fromBase,
                    TargetBase = targetBase,
                    FinalBase = Math.Min(4, targetBase + 1),
                    RunsScored = targetBase + 1 >= 4 ? 1 : 0,
                    JumpScore = jumpScore,
                    ThrowScore = throwScore,
                    TagScore = tagScore,
                    Detail = "Steal plus throwing error"
                };
            }

            bool safe = stealScore >= -6;
            return new StealAttemptResult
            {
                Outcome = safe ? StealAttemptOutcome.Safe : StealAttemptOutcome.CaughtStealing,
                FromBase = fromBase,
                TargetBase = targetBase,
                FinalBase = safe ? targetBase : 0,
                RunsScored = safe && targetBase >= 4 ? 1 : 0,
                JumpScore = jumpScore,
                ThrowScore = throwScore,
                TagScore = tagScore,
                Detail = safe ? "Stolen base" : "Caught stealing"
            };
        }

        public static bool ShouldCpuAttemptSteal(
            Random rng,
            Player? runner,
            Player? pitcher,
            Player? catcher,
            int fromBase,
            int outs,
            int balls,
            int strikes,
            int scoreDifferential,
            bool nextBaseOccupied)
        {
            if (rng == null || runner == null || nextBaseOccupied)
                return false;

            int speed = Rating(runner, p => p.Speed, 50);
            int aggression = Rating(runner, p => p.StealAggression, 50);
            int baserunning = Rating(runner, p => p.BaseRunning, 50);
            int hold = Rating(pitcher, p => p.HoldRunner, 50);
            int catcherDefense = (Rating(catcher, p => p.ArmStrength, 50) + Rating(catcher, p => p.PopTime, 50)) / 2;
            int baseChance = -22 + (speed - 50) / 2 + (aggression - 50) / 3 + (baserunning - 50) / 4
                - (hold - 50) / 5 - (catcherDefense - 50) / 4;
            baseChance += fromBase == 1 ? 12 : fromBase == 2 ? 4 : -18;
            baseChance += outs == 2 ? -3 : 4;
            baseChance += balls > strikes ? 5 : strikes == 2 ? -8 : 0;
            baseChance += scoreDifferential < 0 ? 5 : scoreDifferential > 2 ? -8 : 0;
            return rng.Next(100) < Math.Clamp(baseChance, 1, 46);
        }

        public static DefensiveStealCall ChooseCpuDefense(Random rng, Player? pitcher, Player? catcher, Player? leadRunner, int fromBase)
        {
            if (rng == null || leadRunner == null)
                return DefensiveStealCall.Normal;

            int threat = Rating(leadRunner, p => p.Speed, 50) + Rating(leadRunner, p => p.StealAggression, 50) / 2;
            int catcherDefense = Rating(catcher, p => p.ArmStrength, 50) + Rating(catcher, p => p.PopTime, 50) / 2;
            int pitcherMove = Rating(pitcher, p => p.HoldRunner, 50) + Rating(pitcher, p => p.Pickoff, 50) / 2;
            int pressure = threat - (catcherDefense + pitcherMove) / 2 + (fromBase == 3 ? 10 : 0);
            if (pressure < 28)
                return DefensiveStealCall.Normal;
            if (rng.Next(100) < Math.Clamp((pitcherMove - 40) / 2, 4, 28))
                return DefensiveStealCall.Pickoff;
            if (rng.Next(100) < Math.Clamp(pressure / 3, 8, 36))
                return DefensiveStealCall.Pitchout;
            if (rng.Next(100) < 50)
                return DefensiveStealCall.HoldRunner;
            return DefensiveStealCall.SlideStep;
        }

        private static void FormationModifiers(
            DefensiveStealCall call,
            out int jump,
            out int throwScore,
            out int tag,
            out int pickoff,
            out int error)
        {
            jump = 0;
            throwScore = 0;
            tag = 0;
            pickoff = 0;
            error = 0;
            switch (call)
            {
                case DefensiveStealCall.HoldRunner:
                    jump = -18;
                    tag = 5;
                    pickoff = 8;
                    break;
                case DefensiveStealCall.SlideStep:
                    jump = -10;
                    throwScore = 10;
                    error = 2;
                    break;
                case DefensiveStealCall.Pitchout:
                    jump = -8;
                    throwScore = 26;
                    tag = 6;
                    error = -5;
                    break;
                case DefensiveStealCall.Pickoff:
                    jump = -14;
                    pickoff = 24;
                    error = 3;
                    break;
            }
        }

        private static int Rating(Player? player, Func<Player, int> selector, int fallback)
        {
            if (player == null)
                return fallback;
            return Math.Clamp(selector(player), 0, 99);
        }
    }
}
