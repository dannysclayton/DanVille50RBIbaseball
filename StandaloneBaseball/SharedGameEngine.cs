using System;

namespace StandaloneBaseball
{
    public enum SharedSwingType
    {
        Take,
        Normal,
        Contact,
        Power
    }

    public enum SharedPitchResultType
    {
        Ball,
        CalledStrike,
        SwingingStrike,
        Foul,
        InPlay,
        HitByPitch
    }

    public enum SharedBattedBallResultType
    {
        Out,
        Error,
        Single,
        Double,
        Triple,
        HomeRun
    }

    public sealed class SharedPitchRequest
    {
        public Player Batter { get; set; }
        public Player Pitcher { get; set; }
        public GameplayPitchType PitchType { get; set; } = GameplayPitchType.Fastball;
        public SharedSwingType SwingType { get; set; }
        public double PitchX { get; set; }
        public double PitchY { get; set; }
        public double TimingQuality { get; set; } = 0.75;
        public int Balls { get; set; }
        public int Strikes { get; set; }
        public int PitcherAdjustmentPercent { get; set; }
        public int OffensiveStrategyModifier { get; set; }
        public int BatterBoostPercent { get; set; }
        public int PitcherBoostPercent { get; set; }
    }

    public sealed class SharedPitchResolution
    {
        public SharedPitchResultType ResultType { get; set; }
        public bool InStrikeZone { get; set; }
        public double ContactQuality { get; set; }
    }

    public sealed class SharedBattedBallRequest
    {
        public Player Batter { get; set; }
        public Player Pitcher { get; set; }
        public GameplayPitchType PitchType { get; set; } = GameplayPitchType.Fastball;
        public double ContactQuality { get; set; }
        public int PitcherAdjustmentPercent { get; set; }
        public int BatterBoostPercent { get; set; }
        public int PitcherBoostPercent { get; set; }
        public int DefenseFieldingRating { get; set; } = 50;
        public bool SafeApproach { get; set; }
        public bool NoDoublesDefense { get; set; }
        public bool OutfieldIn { get; set; }
    }

    public static class SharedGameEngine
    {
        public static SharedPitchResolution ResolvePitch(Random rng, SharedPitchRequest request)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            request ??= new SharedPitchRequest();

            int pitchEffectiveness = PitchProfileEngine.PitchEffectiveness(request.Pitcher, request.PitchType);
            int batterPitchAdjustment = PitchProfileEngine.BatterPitchAdjustment(request.Batter, request.PitchType);
            int pitching = Rating(request.Pitcher, p => p.Pitching, 50, request.PitcherBoostPercent) + request.PitcherAdjustmentPercent + (pitchEffectiveness - 50) / 2;
            int control = (Rating(request.Pitcher, p => p.Pitching, 50, request.PitcherBoostPercent) + Rating(request.Pitcher, p => p.Accuracy, 50, request.PitcherBoostPercent)) / 2;
            int contact = Rating(request.Batter, p => p.Contact, 50, request.BatterBoostPercent) + batterPitchAdjustment;
            int power = Rating(request.Batter, p => p.Power, 50, request.BatterBoostPercent);
            double distance = Math.Sqrt(request.PitchX * request.PitchX + request.PitchY * request.PitchY);
            bool inZone = Math.Abs(request.PitchX) <= 0.72 && Math.Abs(request.PitchY) <= 0.62;

            int hbpChance = Math.Clamp(4 + Math.Max(0, 42 - control) / 5 - request.PitcherAdjustmentPercent / 10, 1, 18);
            if (request.SwingType == SharedSwingType.Take && Math.Abs(request.PitchX) > 1.15 && rng.Next(1000) < hbpChance)
            {
                return new SharedPitchResolution { ResultType = SharedPitchResultType.HitByPitch, InStrikeZone = false };
            }

            if (request.SwingType == SharedSwingType.Take)
            {
                return new SharedPitchResolution
                {
                    ResultType = inZone ? SharedPitchResultType.CalledStrike : SharedPitchResultType.Ball,
                    InStrikeZone = inZone
                };
            }

            double styleContact = request.SwingType switch
            {
                SharedSwingType.Contact => 0.13,
                SharedSwingType.Power => -0.12,
                _ => 0.0
            };
            double zonePenalty = Math.Max(0.0, distance - 0.52) * 0.32;
            double pitchDifficulty = PitchDifficulty(request.PitchType) + (pitching - 50) / 250.0 + (pitchEffectiveness - 50) / 360.0;
            double timing = Math.Clamp(request.TimingQuality + TimingDifficultyAdjustment(request.PitchType, pitchEffectiveness), 0.0, 1.0);
            double contactChance = 0.48 + (contact - pitching) / 180.0 + styleContact +
                request.OffensiveStrategyModifier / 200.0 + (timing - 0.5) * 0.48 - zonePenalty - pitchDifficulty;
            contactChance = Math.Clamp(contactChance, 0.04, 0.94);

            if (rng.NextDouble() >= contactChance)
            {
                return new SharedPitchResolution
                {
                    ResultType = SharedPitchResultType.SwingingStrike,
                    InStrikeZone = inZone
                };
            }

            double contactQuality = Math.Clamp(
                0.48 + (contact - pitching) / 150.0 + (power - 50) / 300.0 +
                (timing - 0.5) * 0.7 - zonePenalty +
                (request.SwingType == SharedSwingType.Power ? 0.10 : request.SwingType == SharedSwingType.Contact ? -0.04 : 0.0),
                0.02,
                1.0);
            int foulChance = Math.Clamp(34 - (int)Math.Round(contactQuality * 22), 8, 32);
            if (rng.Next(100) < foulChance)
            {
                return new SharedPitchResolution
                {
                    ResultType = SharedPitchResultType.Foul,
                    InStrikeZone = inZone,
                    ContactQuality = contactQuality
                };
            }

            return new SharedPitchResolution
            {
                ResultType = SharedPitchResultType.InPlay,
                InStrikeZone = inZone,
                ContactQuality = contactQuality
            };
        }

        public static SharedBattedBallResultType ResolveBattedBall(Random rng, SharedBattedBallRequest request)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            request ??= new SharedBattedBallRequest();

            int contact = Rating(request.Batter, p => p.Contact, 50, request.BatterBoostPercent) + PitchProfileEngine.BatterPitchAdjustment(request.Batter, request.PitchType);
            int power = Rating(request.Batter, p => p.Power, 50, request.BatterBoostPercent);
            int speed = Rating(request.Batter, p => p.Speed, 50, request.BatterBoostPercent);
            int pitching = Rating(request.Pitcher, p => p.Pitching, 50, request.PitcherBoostPercent) + request.PitcherAdjustmentPercent;
            int defense = Math.Clamp(request.DefenseFieldingRating, 1, 99);
            double quality = Math.Clamp(request.ContactQuality, 0.0, 1.0);

            double hitChance = 0.18 + quality * 0.36 + (contact - pitching) / 330.0 + (speed - 50) / 1000.0 - (defense - 50) / 900.0;
            if (rng.NextDouble() >= Math.Clamp(hitChance, 0.08, 0.72))
            {
                int errorChance = Math.Clamp(30 - defense / 2, 2, 24);
                return rng.Next(1000) < errorChance
                    ? SharedBattedBallResultType.Error
                    : SharedBattedBallResultType.Out;
            }

            int homeRunChance = Math.Clamp(3 + (power - 45) / 4 + (int)(quality * 10), 1, 28);
            if (request.SafeApproach)
                homeRunChance = Math.Max(1, homeRunChance / 2);
            if (rng.Next(100) < homeRunChance)
                return SharedBattedBallResultType.HomeRun;

            int tripleChance = Math.Clamp(1 + (speed - 45) / 9 + (int)(quality * 3), 1, 13);
            if (request.NoDoublesDefense) tripleChance = Math.Max(1, tripleChance / 2);
            if (request.OutfieldIn) tripleChance = Math.Min(18, tripleChance + 4);
            if (rng.Next(100) < tripleChance)
                return SharedBattedBallResultType.Triple;

            int doubleChance = Math.Clamp(8 + (power - 45) / 7 + (speed - 50) / 18 + (int)(quality * 8), 4, 31);
            if (request.SafeApproach) doubleChance = Math.Max(2, doubleChance / 2);
            if (request.NoDoublesDefense) doubleChance = Math.Max(2, doubleChance / 2);
            if (request.OutfieldIn) doubleChance = Math.Min(38, doubleChance + 7);
            return rng.Next(100) < doubleChance
                ? SharedBattedBallResultType.Double
                : SharedBattedBallResultType.Single;
        }

        private static double PitchDifficulty(GameplayPitchType pitchType)
        {
            return pitchType switch
            {
                GameplayPitchType.Fastball => 0.02,
                GameplayPitchType.Changeup => 0.06,
                GameplayPitchType.Curveball => 0.08,
                GameplayPitchType.Slider => 0.075,
                GameplayPitchType.Splitter => 0.10,
                GameplayPitchType.Forkball => 0.115,
                GameplayPitchType.Knuckleball => 0.14,
                _ => 0.04
            };
        }

        private static double TimingDifficultyAdjustment(GameplayPitchType pitchType, int effectiveness)
        {
            double quality = Math.Clamp((effectiveness - 50) / 220.0, -0.12, 0.18);
            return pitchType switch
            {
                GameplayPitchType.Changeup => -0.02 - quality / 2,
                GameplayPitchType.Splitter => -0.035 - quality,
                GameplayPitchType.Forkball => -0.045 - quality,
                GameplayPitchType.Knuckleball => -0.09 - quality,
                _ => 0.0
            };
        }

        private static int Rating(Player player, Func<Player, int> selector, int fallback, int boostPercent = 0)
        {
            int rating = player == null ? fallback : InjuryEngine.EffectiveRating(player, selector(player));
            return RankingGameModifier.Apply(rating, boostPercent);
        }
    }
}
