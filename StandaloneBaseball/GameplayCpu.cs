#nullable enable annotations

using System;
using System.Collections.Generic;
using System.Linq;

namespace StandaloneBaseball
{
    public static class GameplayCpu
    {
        public enum CpuMode
        {
            UserVsCpu,
            CpuVsCpuWatch
        }

        public enum PitchType
        {
            Fastball,
            Changeup,
            Curveball,
            Slider,
            Splitter,
            Forkball,
            Knuckleball
        }

        public enum SwingType
        {
            Take,
            Contact,
            Power,
            Bunt
        }

        public enum BaserunningAction
        {
            Hold,
            Advance,
            TakeExtraBase,
            Steal,
            TagUp,
            Retreat
        }

        public enum ThrowTarget
        {
            None,
            FirstBase,
            SecondBase,
            ThirdBase,
            HomePlate,
            Cutoff,
            Pitcher
        }

        public enum BallLocation
        {
            InfieldLeft,
            InfieldMiddle,
            InfieldRight,
            OutfieldLeft,
            OutfieldCenter,
            OutfieldRight,
            Gap,
            Wall,
            Foul
        }

        public readonly struct PitchDecision
        {
            public PitchDecision(PitchType pitchType, double aimX, double aimY, int windupMs, int releaseOffsetMs, bool wastePitch)
            {
                PitchType = pitchType;
                AimX = aimX;
                AimY = aimY;
                WindupMs = windupMs;
                ReleaseOffsetMs = releaseOffsetMs;
                WastePitch = wastePitch;
            }

            public PitchType PitchType { get; }
            public double AimX { get; }
            public double AimY { get; }
            public int WindupMs { get; }
            public int ReleaseOffsetMs { get; }
            public bool WastePitch { get; }
        }

        public readonly struct SwingDecision
        {
            public SwingDecision(SwingType swingType, int timingOffsetMs, double confidence)
            {
                SwingType = swingType;
                TimingOffsetMs = timingOffsetMs;
                Confidence = confidence;
            }

            public SwingType SwingType { get; }
            public bool ShouldSwing => SwingType != SwingType.Take;
            public int TimingOffsetMs { get; }
            public double Confidence { get; }
        }

        public readonly struct BaserunningDecision
        {
            public BaserunningDecision(BaserunningAction action, int targetBase, double confidence)
            {
                Action = action;
                TargetBase = targetBase;
                Confidence = confidence;
            }

            public BaserunningAction Action { get; }
            public int TargetBase { get; }
            public double Confidence { get; }
        }

        public readonly struct ThrowDecision
        {
            public ThrowDecision(ThrowTarget target, bool useCutoff, double urgency)
            {
                Target = target;
                UseCutoff = useCutoff;
                Urgency = urgency;
            }

            public ThrowTarget Target { get; }
            public bool UseCutoff { get; }
            public double Urgency { get; }
        }

        public static PitchDecision ChoosePitch(
            Random rng,
            Player pitcher,
            Player batter,
            int balls,
            int strikes,
            bool runnerOnFirst,
            bool runnerOnSecond,
            bool runnerOnThird,
            int outs,
            int pitchCount,
            CpuMode mode = CpuMode.UserVsCpu)
        {
            RequireRandom(rng);

            balls = ClampInt(balls, 0, 3);
            strikes = ClampInt(strikes, 0, 2);
            outs = ClampInt(outs, 0, 2);
            int pitching = Rating(pitcher, p => p.Pitching, 50);
            int stamina = Rating(pitcher, p => p.Stamina, 50);
            int contact = Rating(batter, p => p.Contact, 50);
            int power = Rating(batter, p => p.Power, 50);
            double fatigue = Math.Clamp((pitchCount - stamina) / 70.0, 0.0, 0.35);
            double command = Math.Clamp((pitching * 0.7 + stamina * 0.3) / 100.0 - fatigue, 0.15, 0.95);

            bool behind = balls > strikes;
            bool ahead = strikes > balls || strikes == 2;
            bool dangerousBatter = power >= 70 || contact >= 75;
            bool scoringThreat = runnerOnSecond || runnerOnThird;
            bool waste = ahead && dangerousBatter && balls < 3 && rng.NextDouble() < 0.22 + (scoringThreat ? 0.08 : 0.0);

            PitchType type = ChoosePitchType(rng, pitcher, batter, pitching, stamina, contact, power, behind, ahead, waste);
            double miss = (1.0 - command) * (mode == CpuMode.CpuVsCpuWatch ? 0.75 : 1.0);
            double edgeBias = dangerousBatter ? 0.42 : 0.24;
            double aimX = RandomRange(rng, -edgeBias, edgeBias) + RandomRange(rng, -miss, miss);
            double aimY = RandomRange(rng, -0.34, 0.34) + RandomRange(rng, -miss, miss);

            if (waste)
            {
                aimX += rng.Next(2) == 0 ? -0.55 : 0.55;
                aimY += rng.Next(2) == 0 ? -0.35 : 0.35;
            }
            else if (behind)
            {
                aimX *= 0.45;
                aimY *= 0.45;
            }
            else if (runnerOnThird && outs < 2)
            {
                aimY = Math.Max(aimY, 0.18);
            }

            int baseWindup = runnerOnFirst || runnerOnSecond || runnerOnThird ? 620 : 820;
            int windupMs = ClampInt(baseWindup + rng.Next(-80, 81) - pitching, 420, 980);
            int releaseOffsetMs = ClampInt(rng.Next(-70, 71) + (int)((0.5 - command) * 70.0), -140, 140);

            return new PitchDecision(type, Clamp(aimX, -1.0, 1.0), Clamp(aimY, -1.0, 1.0), windupMs, releaseOffsetMs, waste);
        }

        public static SwingDecision DecideSwing(
            Random rng,
            Player batter,
            Player pitcher,
            PitchType pitchType,
            double pitchX,
            double pitchY,
            int balls,
            int strikes,
            int millisecondsUntilPlate,
            bool hitAndRun = false,
            bool buntSign = false)
        {
            RequireRandom(rng);

            balls = ClampInt(balls, 0, 3);
            strikes = ClampInt(strikes, 0, 2);
            int contact = Rating(batter, p => p.Contact, 50);
            int power = Rating(batter, p => p.Power, 50);
            int speed = Rating(batter, p => p.Speed, 50);
            int pitching = Rating(pitcher, p => p.Pitching, 50);
            double distance = DistanceFromStrikeCenter(pitchX, pitchY);
            double zonePenalty = Math.Max(0.0, distance - 0.62) * 1.4;
            double discipline = Math.Clamp((contact * 0.65 + speed * 0.15 + (100 - power) * 0.2) / 100.0, 0.1, 0.95);
            double countPressure = strikes == 2 ? 0.24 : balls == 3 ? -0.16 : 0.0;
            double pitchDifficulty = PitchDifficulty(pitchType, pitching, pitcher, batter) + zonePenalty;
            double swingChance = 0.42 + countPressure + hitAndRunBonus(hitAndRun) - pitchDifficulty * 0.22 + (contact - 50) / 260.0;

            if (buntSign)
            {
                double buntChance = 0.55 + (speed - 50) / 180.0 + (strikes == 2 ? -0.35 : 0.0);
                if (rng.NextDouble() < Clamp(buntChance, 0.05, 0.9))
                    return new SwingDecision(SwingType.Bunt, TimingOffset(rng, contact, pitching, millisecondsUntilPlate), buntChance);
            }

            bool shouldSwing = rng.NextDouble() < Clamp(swingChance - zonePenalty * discipline, 0.04, 0.92);
            if (!shouldSwing)
                return new SwingDecision(SwingType.Take, 0, 1.0 - swingChance);

            bool powerSwing = strikes < 2
                && power >= contact - 8
                && distance < 0.72
                && rng.NextDouble() < Clamp(0.25 + (power - 50) / 150.0 - (pitchDifficulty * 0.12), 0.05, 0.68);

            double confidence = Clamp(swingChance + (contact - pitching) / 220.0 - zonePenalty, 0.05, 0.95);
            return new SwingDecision(powerSwing ? SwingType.Power : SwingType.Contact, TimingOffset(rng, contact, pitching, millisecondsUntilPlate), confidence);
        }

        public static BaserunningDecision DecideBaserunning(
            Random rng,
            Player runner,
            int currentBase,
            int outs,
            BallLocation ballLocation,
            int ballDepth,
            int fielderArm,
            int scoreDifferential,
            bool forced,
            bool runnerAheadOccupied)
        {
            RequireRandom(rng);

            currentBase = ClampInt(currentBase, 1, 3);
            outs = ClampInt(outs, 0, 2);
            ballDepth = ClampInt(ballDepth, 0, 100);
            fielderArm = ClampInt(fielderArm, 0, 99);
            int speed = Rating(runner, p => p.Speed, 50);
            int targetBase = Math.Min(4, currentBase + 1);

            if (forced)
                return new BaserunningDecision(BaserunningAction.Advance, targetBase, 0.96);

            if (runnerAheadOccupied)
                return new BaserunningDecision(BaserunningAction.Hold, currentBase, 0.9);

            if (ballLocation == BallLocation.Foul)
            {
                bool tag = outs < 2 && currentBase == 3 && ballDepth >= 58 && speed + rng.Next(18) > fielderArm + 18;
                return tag
                    ? new BaserunningDecision(BaserunningAction.TagUp, 4, 0.62)
                    : new BaserunningDecision(BaserunningAction.Hold, currentBase, 0.8);
            }

            double aggression = 0.35 + (speed - fielderArm) / 130.0 + (ballDepth - 45) / 140.0;
            aggression += outs == 2 ? 0.12 : 0.0;
            aggression += scoreDifferential < 0 ? 0.08 : scoreDifferential > 2 ? -0.1 : 0.0;
            aggression += IsExtraBaseLocation(ballLocation) ? 0.18 : -0.08;

            if (currentBase == 3 && ballDepth >= 35)
                aggression += 0.18;

            if (rng.NextDouble() < Clamp(aggression, 0.05, 0.9))
            {
                bool extraBase = currentBase <= 2
                    && IsExtraBaseLocation(ballLocation)
                    && ballDepth >= 70
                    && rng.NextDouble() < Clamp((speed - fielderArm + ballDepth) / 140.0, 0.04, 0.75);

                return new BaserunningDecision(extraBase ? BaserunningAction.TakeExtraBase : BaserunningAction.Advance, extraBase ? Math.Min(4, currentBase + 2) : targetBase, aggression);
            }

            bool retreat = ballDepth < 20 && outs < 2 && rng.NextDouble() < 0.18;
            return new BaserunningDecision(retreat ? BaserunningAction.Retreat : BaserunningAction.Hold, retreat ? currentBase - 1 : currentBase, 1.0 - aggression);
        }

        public static BaserunningDecision DecideSteal(
            Random rng,
            Player runner,
            Player pitcher,
            int currentBase,
            int outs,
            int balls,
            int strikes,
            int catcherArm,
            int scoreDifferential,
            bool nextBaseOccupied)
        {
            RequireRandom(rng);

            currentBase = ClampInt(currentBase, 1, 3);
            if (currentBase >= 3 || nextBaseOccupied)
                return new BaserunningDecision(BaserunningAction.Hold, currentBase, 0.95);

            int speed = Rating(runner, p => p.Speed, 50);
            int pitching = Rating(pitcher, p => p.Pitching, 50);
            int stamina = Rating(pitcher, p => p.Stamina, 50);
            catcherArm = ClampInt(catcherArm, 0, 99);
            double chance = 0.12 + (speed - catcherArm) / 135.0 + (100 - pitching) / 300.0 + (100 - stamina) / 360.0;
            chance += outs == 2 ? -0.03 : 0.04;
            chance += balls > strikes ? 0.04 : strikes == 2 ? -0.08 : 0.0;
            chance += scoreDifferential < 0 ? 0.05 : scoreDifferential > 2 ? -0.07 : 0.0;

            bool steal = rng.NextDouble() < Clamp(chance, 0.02, 0.72);
            return new BaserunningDecision(steal ? BaserunningAction.Steal : BaserunningAction.Hold, steal ? currentBase + 1 : currentBase, steal ? chance : 1.0 - chance);
        }

        public static ThrowDecision DecideThrowTarget(
            Random rng,
            Player fielder,
            int outs,
            BallLocation ballLocation,
            int ballDepth,
            int scoreDifferential,
            bool runnerOnFirst,
            bool runnerOnSecond,
            bool runnerOnThird,
            int batterRunnerSpeed,
            bool forceAtSecond,
            bool forceAtThird,
            bool forceAtHome)
        {
            RequireRandom(rng);

            outs = ClampInt(outs, 0, 2);
            ballDepth = ClampInt(ballDepth, 0, 100);
            batterRunnerSpeed = ClampInt(batterRunnerSpeed, 0, 99);
            int arm = Rating(fielder, p => p.Pitching, 50);
            double deepBall = IsOutfield(ballLocation) ? ballDepth / 100.0 : ballDepth / 180.0;
            bool useCutoff = deepBall > 0.72 && arm < 78;

            if (runnerOnThird && (forceAtHome || scoreDifferential <= 1))
            {
                double homeUrgency = 0.72 + (scoreDifferential <= 0 ? 0.12 : 0.0) - deepBall * 0.18;
                if (forceAtHome || rng.NextDouble() < Clamp(homeUrgency, 0.2, 0.95))
                    return new ThrowDecision(useCutoff ? ThrowTarget.Cutoff : ThrowTarget.HomePlate, useCutoff, homeUrgency);
            }

            if (outs < 2)
            {
                if (forceAtThird && runnerOnSecond)
                    return new ThrowDecision(ThrowTarget.ThirdBase, false, 0.78);
                if (forceAtSecond && runnerOnFirst)
                    return new ThrowDecision(ThrowTarget.SecondBase, false, 0.82);
            }

            double firstChance = 0.72 + (arm - batterRunnerSpeed) / 170.0 - deepBall * 0.5;
            if (!IsOutfield(ballLocation) && rng.NextDouble() < Clamp(firstChance, 0.08, 0.95))
                return new ThrowDecision(ThrowTarget.FirstBase, false, firstChance);

            if (runnerOnSecond && scoreDifferential <= 2 && deepBall < 0.78)
                return new ThrowDecision(ThrowTarget.ThirdBase, false, 0.55);

            if (runnerOnFirst && outs < 2 && deepBall < 0.66)
                return new ThrowDecision(ThrowTarget.SecondBase, false, 0.5);

            return new ThrowDecision(useCutoff ? ThrowTarget.Cutoff : ThrowTarget.Pitcher, useCutoff, useCutoff ? 0.48 : 0.35);
        }

        private static PitchType ChoosePitchType(Random rng, Player pitcher, Player batter, int pitching, int stamina, int contact, int power, bool behind, bool ahead, bool waste)
        {
            var weights = new Dictionary<PitchType, int>
            {
                [PitchType.Fastball] = behind ? 48 : 34,
                [PitchType.Changeup] = 18 + Math.Max(0, pitching - contact) / 6,
                [PitchType.Curveball] = 18 + Math.Max(0, pitching - 50) / 8,
                [PitchType.Slider] = 18 + Math.Max(0, power - contact) / 8,
                [PitchType.Splitter] = ahead ? 14 : 8,
                [PitchType.Forkball] = ahead ? 16 : 6,
                [PitchType.Knuckleball] = 6 + Math.Max(0, pitching - 65) / 6
            };

            if (stamina < 35)
            {
                weights[PitchType.Fastball] += 8;
                weights[PitchType.Forkball] -= 4;
                weights[PitchType.Splitter] -= 4;
                weights[PitchType.Curveball] -= 4;
            }

            if (waste)
            {
                weights[PitchType.Slider] += 8;
                weights[PitchType.Curveball] += 8;
                weights[PitchType.Fastball] -= 10;
            }

            foreach (var pitch in weights.Keys.ToList())
            {
                var gameplayPitch = MapPitchType(pitch);
                if (!PitchProfileEngine.CanThrow(pitcher, gameplayPitch))
                {
                    weights[pitch] = 0;
                    continue;
                }

                int effectiveness = PitchProfileEngine.PitchEffectiveness(pitcher, gameplayPitch);
                int batterAdjustment = PitchProfileEngine.BatterPitchAdjustment(batter, gameplayPitch);
                weights[pitch] += (effectiveness - 50) / 2 - batterAdjustment;
            }

            return WeightedPitch(rng, weights);
        }

        private static PitchType WeightedPitch(Random rng, Dictionary<PitchType, int> weights)
        {
            var normalized = weights
                .Select(kv => new KeyValuePair<PitchType, int>(kv.Key, Math.Max(0, kv.Value)))
                .Where(kv => kv.Value > 0)
                .ToList();
            if (normalized.Count == 0)
                return PitchType.Fastball;

            int roll = rng.Next(normalized.Sum(kv => kv.Value));
            foreach (var kv in normalized)
            {
                if (roll < kv.Value)
                    return kv.Key;
                roll -= kv.Value;
            }
            return normalized[0].Key;
        }

        private static double PitchDifficulty(PitchType pitchType, int pitching, Player? pitcher = null, Player? batter = null)
        {
            var gameplayPitch = MapPitchType(pitchType);
            int effectiveness = PitchProfileEngine.PitchEffectiveness(pitcher, gameplayPitch);
            int batterAdjustment = PitchProfileEngine.BatterPitchAdjustment(batter, gameplayPitch);
            double skill = (pitching - 50) / 180.0 + (effectiveness - 50) / 260.0 - batterAdjustment / 140.0;
            switch (pitchType)
            {
                case PitchType.Fastball:
                    return 0.18 + skill;
                case PitchType.Changeup:
                    return 0.28 + skill;
                case PitchType.Curveball:
                    return 0.32 + skill;
                case PitchType.Slider:
                    return 0.3 + skill;
                case PitchType.Splitter:
                    return 0.34 + skill;
                case PitchType.Forkball:
                    return 0.37 + skill;
                case PitchType.Knuckleball:
                    return 0.4 + skill;
                default:
                    return 0.25 + skill;
            }
        }

        private static GameplayPitchType MapPitchType(PitchType pitchType)
            => pitchType switch
            {
                PitchType.Curveball => GameplayPitchType.Curveball,
                PitchType.Slider => GameplayPitchType.Slider,
                PitchType.Changeup => GameplayPitchType.Changeup,
                PitchType.Splitter => GameplayPitchType.Splitter,
                PitchType.Forkball => GameplayPitchType.Forkball,
                PitchType.Knuckleball => GameplayPitchType.Knuckleball,
                _ => GameplayPitchType.Fastball
            };

        private static int TimingOffset(Random rng, int contact, int pitching, int millisecondsUntilPlate)
        {
            int reactionWindow = ClampInt(millisecondsUntilPlate / 10, 16, 95);
            int skillEdge = ClampInt(contact - pitching, -60, 60);
            int maxMiss = ClampInt(reactionWindow - skillEdge / 3, 12, 105);
            return rng.Next(-maxMiss, maxMiss + 1);
        }

        private static double DistanceFromStrikeCenter(double x, double y)
        {
            return Math.Sqrt(x * x + y * y);
        }

        private static bool IsExtraBaseLocation(BallLocation location)
        {
            return location == BallLocation.OutfieldLeft
                || location == BallLocation.OutfieldCenter
                || location == BallLocation.OutfieldRight
                || location == BallLocation.Gap
                || location == BallLocation.Wall;
        }

        private static bool IsOutfield(BallLocation location)
        {
            return location == BallLocation.OutfieldLeft
                || location == BallLocation.OutfieldCenter
                || location == BallLocation.OutfieldRight
                || location == BallLocation.Gap
                || location == BallLocation.Wall;
        }

        private static double hitAndRunBonus(bool hitAndRun)
        {
            return hitAndRun ? 0.28 : 0.0;
        }

        private static int Rating(Player player, Func<Player, int> selector, int fallback)
        {
            return player == null ? fallback : ClampInt(selector(player), 0, 99);
        }

        private static double RandomRange(Random rng, double min, double max)
        {
            return min + rng.NextDouble() * (max - min);
        }

        private static double Clamp(double value, double min, double max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private static int ClampInt(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private static void RequireRandom(Random rng)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));
        }
    }
}
