#nullable enable annotations

using System;
using System.Collections.Generic;
using System.Linq;

namespace StandaloneBaseball
{
    public static class Simulator
    {
        private static readonly string[] FirstNames =
        {
            "Alex", "Drew", "Marco", "Ty", "Luis", "Nico", "Sam", "Cole", "Evan", "Rafi",
            "Mason", "Leo", "Jace", "Owen", "Noah", "Max", "Andre", "Theo", "Kai", "Miles"
        };

        private static readonly string[] LastNames =
        {
            "Stone", "Reed", "Vega", "Cross", "Hayes", "Ford", "Hart", "Pena", "Rojas", "Blake",
            "Lane", "Ward", "Cruz", "Keller", "Shaw", "Banks", "Meyer", "King", "Soto", "Bell"
        };

        private static readonly string[] CatcherCombos = { "C", "C/1B", "C/3B", "C/DH" };
        private static readonly string[] CornerCombos = { "1B", "3B", "1B/3B", "1B/DH", "3B/DH" };
        private static readonly string[] MiddleCombos = { "2B", "SS", "2B/SS", "2B/3B", "SS/3B" };
        private static readonly string[] OutfieldCombos = { "LF", "CF", "RF", "LF/CF", "CF/RF", "LF/RF", "OF" };
        private static readonly string[] PitcherCombos = { "P", "P/DH", "P/1B", "P/3B", "P/OF" };
        private static readonly string[][] PositionPlayerGroups = { CatcherCombos, CornerCombos, MiddleCombos, OutfieldCombos };

        public static void FillRandomRoster(Team team, Random rng)
        {
            team.Roster.Clear();
            for (int i = 0; i < 18; i++)
                team.Roster.Add(RandomPlayer(rng, PlayerRole.Batter));
            for (int i = 0; i < 12; i++)
                team.Roster.Add(RandomPlayer(rng, PlayerRole.Pitcher));
        }

        public static Player RandomPlayer(Random rng, PlayerRole role, string? name = null)
        {
            int Roll(int min, int max) => min + rng.Next(max - min + 1);
            var classification = RandomClassification(rng);
            var player = new Player
            {
                Name = string.IsNullOrWhiteSpace(name)
                    ? FirstNames[rng.Next(FirstNames.Length)] + " " + LastNames[rng.Next(LastNames.Length)]
                    : name,
                Role = role,
                Classification = classification,
                InitialClassification = classification,
                Positions = RandomPositions(rng, role),
                Bats = RandomBatSide(rng),
                Throws = RandomThrowSide(rng, role),
                CareerPitchCount = role == PlayerRole.Pitcher ? RandomCareerPitchCount(rng) : 0,
                Potential = RandomDevelopmentRating(rng, 40, 95),
                WorkEthic = RandomDevelopmentRating(rng, 30, 95),
                Durability = RandomDevelopmentRating(rng, 35, 95),
                RegressionRisk = RandomDevelopmentRating(rng, 5, 55),
                Contact = ApplyClassificationModifier(Roll(35, 95), classification),
                Power = ApplyClassificationModifier(Roll(25, 95), classification),
                Speed = ApplyClassificationModifier(Roll(30, 95), classification),
                StealAggression = ApplyClassificationModifier(Roll(20, 90), classification),
                BaseRunning = ApplyClassificationModifier(Roll(30, 95), classification),
                Fielding = ApplyClassificationModifier(Roll(35, 95), classification),
                HoldRunner = ApplyClassificationModifier(role == PlayerRole.Pitcher ? Roll(30, 95) : Roll(10, 55), classification),
                Pickoff = ApplyClassificationModifier(role == PlayerRole.Pitcher ? Roll(25, 90) : Roll(10, 45), classification),
                DeliveryTime = ApplyClassificationModifier(role == PlayerRole.Pitcher ? Roll(30, 95) : Roll(10, 50), classification),
                ArmStrength = ApplyClassificationModifier(Roll(30, 95), classification),
                PopTime = ApplyClassificationModifier(Roll(30, 95), classification),
                Accuracy = ApplyClassificationModifier(Roll(30, 95), classification),
                TagRating = ApplyClassificationModifier(Roll(30, 95), classification),
                Pitching = ApplyClassificationModifier(role == PlayerRole.Pitcher ? Roll(35, 95) : Roll(10, 45), classification),
                Stamina = ApplyClassificationModifier(role == PlayerRole.Pitcher ? Roll(30, 95) : Roll(10, 50), classification)
            };
            PitchProfileEngine.NormalizePlayerPitchProfiles(player, rng);
            return player;
        }

        public static int RandomDevelopmentRating(Random rng, int min, int max)
        {
            return Math.Clamp(min + rng.Next(max - min + 1), 0, 99);
        }

        public static string RandomPositions(Random rng, PlayerRole role)
        {
            if (role == PlayerRole.Pitcher)
                return PitcherCombos[rng.Next(PitcherCombos.Length)];

            var group = PositionPlayerGroups[rng.Next(PositionPlayerGroups.Length)];
            return group[rng.Next(group.Length)];
        }

        public static string RandomBatSide(Random rng)
        {
            int roll = rng.Next(100);
            if (roll < 45) return "R";
            if (roll < 80) return "L";
            return "S";
        }

        public static string RandomThrowSide(Random rng, PlayerRole role)
        {
            int leftChance = role == PlayerRole.Pitcher ? 28 : 18;
            return rng.Next(100) < leftChance ? "L" : "R";
        }

        public static int RandomCareerPitchCount(Random rng)
            => rng.Next(85, 116);

        public static PlayerClassification RandomClassification(Random rng)
        {
            return rng.Next(4) switch
            {
                0 => PlayerClassification.Freshman,
                1 => PlayerClassification.Sophomore,
                2 => PlayerClassification.Junior,
                _ => PlayerClassification.Senior
            };
        }

        public static int ApplyClassificationModifier(int rating, PlayerClassification classification)
        {
            double multiplier = classification switch
            {
                PlayerClassification.Freshman => 0.90,
                PlayerClassification.Sophomore => 0.95,
                PlayerClassification.Junior => 1.05,
                PlayerClassification.Senior => 1.10,
                _ => 1.0
            };

            return Math.Clamp((int)Math.Round(rating * multiplier), 0, 99);
        }

        public static GameResult SimGame(LeagueFile? league, Team away, Team home, Random? rng, RankingGameModifier? rankingModifier = null)
        {
            return SimulatedGameEngine.Simulate(league, away, home, rng, rankingModifier);
        }
    }
}
