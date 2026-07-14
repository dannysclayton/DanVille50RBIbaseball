#nullable enable annotations

using System;
using System.Collections.Generic;
using System.Linq;

namespace StandaloneBaseball
{
    public sealed class OffseasonProgressionResult
    {
        public int GraduatedSeniors { get; set; }
        public int ProgressedPlayers { get; set; }
        public int ImprovedPlayers { get; set; }
        public int RegressedPlayers { get; set; }
        public int AddedRecruits { get; set; }
        public int JvPromotions { get; set; }
        public int MedicalTagsAwarded { get; set; }
        public int RedshirtsProcessed { get; set; }
        public int PitchCountIncreases { get; set; }
    }

    public static class PlayerProgressionEngine
    {
        public const int TargetRosterSize = 30;
        public const int MinimumPitchers = 12;

        public static OffseasonProgressionResult ApplyOffseason(LeagueFile league, Season season, Random rng)
        {
            if (league == null) throw new ArgumentNullException(nameof(league));
            if (season == null) throw new ArgumentNullException(nameof(season));
            if (rng == null) throw new ArgumentNullException(nameof(rng));

            var result = new OffseasonProgressionResult();
            var performance = BuildPerformanceMap(season);

            foreach (var team in league.Teams ?? Enumerable.Empty<Team>())
            {
                team.Roster ??= new List<Player>();
                team.JvPool ??= new List<Player>();
                team.InjuredReserve ??= new List<Player>();
                int teamGames = CountTeamGames(season, team.Id);
                int seasonNumber = Math.Max(1, (league.Seasons ?? new List<Season>()).FindIndex(item => item.Id == season.Id) + 1);
                var organizationPlayers = team.Roster
                    .Concat(team.InjuredReserve)
                    .Concat(team.JvPool.Where(player => player?.LastVarsitySeasonNumber == seasonNumber))
                    .Where(player => player != null)
                    .Distinct()
                    .ToList();
                var seniors = organizationPlayers
                    .Where(p => p.Classification == PlayerClassification.Senior &&
                        !p.MedicalTagEligible && !QualifiesForMedicalRepeat(p, teamGames))
                    .ToList();
                foreach (var senior in seniors)
                {
                    team.Roster.Remove(senior);
                    team.InjuredReserve.Remove(senior);
                    team.JvPool.Remove(senior);
                }
                result.GraduatedSeniors += seniors.Count;

                foreach (var player in organizationPlayers.Except(seniors))
                {
                    RecordVarsitySeason(player, seasonNumber, calledUp: false);
                    EnsureDevelopment(player, rng);
                    bool medicalRepeat = player.MedicalTagEligible || QualifiesForMedicalRepeat(player, teamGames);
                    if (medicalRepeat)
                    {
                        player.MedicalTag = true;
                        result.MedicalTagsAwarded++;
                    }
                    bool redshirtRepeat = player.RedshirtActive;
                    if (redshirtRepeat)
                    {
                        ApplyRedshirtBoost(player, rng);
                        result.RedshirtsProcessed++;
                    }

                    var stats = performance.TryGetValue(player.Id, out var existing) ? existing : PlayerPerformance.Empty;
                    result.PitchCountIncreases += ApplyPitchCountGrowth(player, stats, rng);
                    var before = RatingSum(player);

                    if (!redshirtRepeat)
                    {
                        ApplyStandardGrowth(player, rng);
                        ApplyPerformanceGrowth(player, stats, rng);
                        ApplyRegression(player, stats, rng);
                    }

                    if (!medicalRepeat && !redshirtRepeat)
                        AdvanceClassification(player);
                    player.RedshirtActive = false;
                    player.MedicalTagEligible = false;
                    player.InjuryMissedGamesThisSeason = 0;

                    int after = RatingSum(player);
                    result.ProgressedPlayers++;
                    if (after > before) result.ImprovedPlayers++;
                    if (after < before) result.RegressedPlayers++;
                }

                result.AddedRecruits += RefillRoster(team, seniors, rng, out int jvPromotions);
                result.JvPromotions += jvPromotions;
            }

            season.OffseasonProcessed = true;
            return result;
        }

        public static void PrepareJvCallUp(Player player, int seasonNumber, Random rng)
        {
            if (player == null)
                throw new ArgumentNullException(nameof(player));
            if (rng == null)
                throw new ArgumentNullException(nameof(rng));
            EnsureDevelopment(player, rng);
            player.RedshirtActive = false;
            RecordVarsitySeason(player, seasonNumber, calledUp: true);
        }

        public static void RecordVarsitySeason(Player player, int seasonNumber, bool calledUp)
        {
            if (player == null || seasonNumber <= 0)
                return;
            if (player.LastVarsitySeasonNumber != seasonNumber)
            {
                player.VarsitySeasonsPlayed++;
                player.LastVarsitySeasonNumber = seasonNumber;
            }
            if (calledUp && player.VarsityCallUpSeasonNumber <= 0)
                player.VarsityCallUpSeasonNumber = seasonNumber;
        }

        private static void ApplyRedshirtBoost(Player player, Random rng)
        {
            double multiplier = 1.0 + RedshirtBoostPercent(player, rng) / 100.0;
            player.Contact = Boost(player.Contact, multiplier);
            player.Power = Boost(player.Power, multiplier);
            player.Speed = Boost(player.Speed, multiplier);
            player.StealAggression = Boost(player.StealAggression, multiplier);
            player.BaseRunning = Boost(player.BaseRunning, multiplier);
            player.HoldRunner = Boost(player.HoldRunner, multiplier);
            player.Pickoff = Boost(player.Pickoff, multiplier);
            player.DeliveryTime = Boost(player.DeliveryTime, multiplier);
            player.ArmStrength = Boost(player.ArmStrength, multiplier);
            player.PopTime = Boost(player.PopTime, multiplier);
            player.Accuracy = Boost(player.Accuracy, multiplier);
            player.TagRating = Boost(player.TagRating, multiplier);
            player.Pitching = Boost(player.Pitching, multiplier);
            player.Stamina = Boost(player.Stamina, multiplier);
        }

        private static int ApplyPitchCountGrowth(Player player, PlayerPerformance stats, Random rng)
        {
            if (player == null || player.Role != PlayerRole.Pitcher)
                return 0;

            if (player.CareerPitchCount <= 0)
                player.CareerPitchCount = Simulator.RandomCareerPitchCount(rng);

            int added = stats?.IPOuts > 0
                ? (int)Math.Ceiling(stats.IPOuts / 30.0)
                : 0;
            if (added <= 0)
                return 0;

            player.CareerPitchCount = Math.Clamp(player.CareerPitchCount + added, 1, 200);
            return added;
        }

        private static int RedshirtBoostPercent(Player player, Random rng)
        {
            int randomBoost = rng.Next(1, 11);
            int potentialBoost = Math.Clamp((int)Math.Round(player.Potential / 10.0), 1, 10);
            int upsideRoll = rng.Next(1, Math.Max(2, potentialBoost + 1));
            int weighted = (randomBoost + potentialBoost + upsideRoll) / 3;
            return Math.Clamp(weighted, 1, 10);
        }

        private static int Boost(int rating, double multiplier)
            => Math.Clamp((int)Math.Round(rating * multiplier), 0, 99);

        private static bool QualifiesForMedicalRepeat(Player player, int teamGames)
        {
            if (player == null || player.MedicalTag || player.MedicalTagEligible || teamGames <= 0)
                return false;

            return player.InjuryMissedGamesThisSeason * 4 > teamGames;
        }

        private static int CountTeamGames(Season season, Guid teamId)
        {
            return (season.Games ?? new List<GameResult>())
                .Count(g => g.AwayTeamId == teamId || g.HomeTeamId == teamId);
        }

        private static int RefillRoster(Team team, List<Player> graduatedSeniors, Random rng, out int jvPromotions)
        {
            int added = 0;
            jvPromotions = 0;
            team.JvPool ??= new List<Player>();

            foreach (var senior in graduatedSeniors ?? new List<Player>())
            {
                if (team.Roster.Count >= TargetRosterSize)
                    break;
                if (PromoteBestJvMatch(team, senior, rng))
                    jvPromotions++;
            }

            while (team.Roster.Count(p => p.Role == PlayerRole.Pitcher) < MinimumPitchers)
            {
                if (PromoteBestJvByPosition(team, "P", rng))
                    jvPromotions++;
                else
                {
                    team.Roster.Add(Simulator.RandomPlayer(rng, PlayerRole.Pitcher));
                    added++;
                }
            }

            while (team.Roster.Count < TargetRosterSize)
            {
                if (PromoteBestJvAvailable(team, rng))
                    jvPromotions++;
                else
                {
                    team.Roster.Add(Simulator.RandomPlayer(rng, PlayerRole.Batter));
                    added++;
                }
            }

            return added;
        }

        private static bool PromoteBestJvMatch(Team team, Player departed, Random rng)
        {
            var promoted = team.JvPool
                .Select(p => new { Player = p, Score = ReplacementScore(p, departed) })
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .ThenByDescending(x => x.Player.Potential)
                .ThenByDescending(x => x.Player.Overall)
                .FirstOrDefault()?.Player;
            return PromoteJvPlayer(team, promoted, rng);
        }

        private static bool PromoteBestJvByPosition(Team team, string position, Random rng)
        {
            var promoted = team.JvPool
                .Where(p => PositionParts(p).Contains(position))
                .OrderByDescending(p => p.Potential)
                .ThenByDescending(p => p.Overall)
                .FirstOrDefault();
            return PromoteJvPlayer(team, promoted, rng);
        }

        private static bool PromoteBestJvAvailable(Team team, Random rng)
        {
            var promoted = team.JvPool
                .OrderByDescending(p => p.Potential)
                .ThenByDescending(p => p.Overall)
                .FirstOrDefault();
            return PromoteJvPlayer(team, promoted, rng);
        }

        private static bool PromoteJvPlayer(Team team, Player? player, Random rng)
        {
            if (team == null || player == null || team.Roster.Count >= TargetRosterSize)
                return false;

            team.JvPool.Remove(player);
            EnsureDevelopment(player, rng);
            team.Roster.Add(player);
            return true;
        }

        private static int ReplacementScore(Player candidate, Player departed)
        {
            if (candidate == null || departed == null)
                return 0;

            int score = 0;
            if (candidate.Role == departed.Role)
                score += 35;
            var candidatePositions = PositionParts(candidate);
            var departedPositions = PositionParts(departed);
            int overlap = candidatePositions.Intersect(departedPositions).Count();
            score += overlap * 30;
            if (departed.Role == PlayerRole.Pitcher && candidatePositions.Contains("P"))
                score += 40;
            if (score == 0 && candidate.Role == departed.Role)
                score = 10;
            return score;
        }

        private static List<string> PositionParts(Player player)
        {
            var positions = (player?.Positions ?? "").ToUpperInvariant();
            var parts = positions.Split(new[] { '/', ',', ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            if (parts.Contains("OF"))
            {
                parts.Add("LF");
                parts.Add("CF");
                parts.Add("RF");
            }
            if (player?.Role == PlayerRole.Pitcher && !parts.Contains("P"))
                parts.Add("P");
            return parts.Distinct().ToList();
        }

        private static Dictionary<Guid, PlayerPerformance> BuildPerformanceMap(Season season)
        {
            var map = new Dictionary<Guid, PlayerPerformance>();
            foreach (var game in season.Games ?? Enumerable.Empty<GameResult>())
            {
                foreach (var playerLines in (game.Lines ?? Enumerable.Empty<PlayerGameLine>()).GroupBy(line => line.PlayerId))
                {
                    var first = playerLines.First();
                    if (!map.TryGetValue(first.PlayerId, out var stats))
                    {
                        stats = new PlayerPerformance();
                        map[first.PlayerId] = stats;
                    }

                    if (playerLines.Any(line => line.GamesMissedInjury == 0))
                        stats.Games++;
                    foreach (var line in playerLines)
                    {
                        stats.AB += line.AB;
                        stats.H += line.H;
                        stats.HR += line.HR;
                        stats.RBI += line.RBI;
                        stats.IPOuts += line.IPOuts;
                        stats.ER += line.ER;
                        stats.K += line.K;
                        stats.Pitcher = stats.Pitcher || line.Pitcher;
                    }
                }
            }

            return map;
        }

        private static void ApplyStandardGrowth(Player player, Random rng)
        {
            int baseGrowth = player.Classification switch
            {
                PlayerClassification.Freshman => 3,
                PlayerClassification.Sophomore => 2,
                PlayerClassification.Junior => 1,
                _ => 0
            };

            double development = (player.Potential * 0.55 + player.WorkEthic * 0.45) / 100.0;
            int growth = Math.Max(0, (int)Math.Round(baseGrowth * development));
            if (growth == 0 && baseGrowth > 0 && rng.Next(100) < player.WorkEthic)
                growth = 1;

            ImproveRoleRatings(player, growth, rng);
        }

        private static void ApplyPerformanceGrowth(Player player, PlayerPerformance stats, Random rng)
        {
            int bonus = 0;
            if (stats.Pitcher || player.Role == PlayerRole.Pitcher)
            {
                if (stats.IPOuts >= 45 && stats.Era <= 3.50) bonus++;
                if (stats.IPOuts >= 45 && stats.KPerNine >= 8.0) bonus++;
            }
            else
            {
                if (stats.AB >= 60 && stats.Average >= 0.300) bonus++;
                if (stats.AB >= 60 && stats.PowerImpact >= 0.12) bonus++;
            }

            if (bonus > 0)
            {
                int chance = Math.Clamp(player.WorkEthic + player.Potential - 70, 10, 95);
                if (rng.Next(100) < chance)
                    ImproveRoleRatings(player, bonus, rng);
            }
        }

        private static void ApplyRegression(Player player, PlayerPerformance stats, Random rng)
        {
            int risk = player.RegressionRisk;
            risk -= player.WorkEthic / 5;
            risk -= player.Durability / 8;

            bool poorHitter = player.Role == PlayerRole.Batter && stats.AB >= 60 && stats.Average < 0.210;
            bool poorPitcher = player.Role == PlayerRole.Pitcher && stats.IPOuts >= 45 && stats.Era > 6.00;
            if (poorHitter || poorPitcher)
                risk += 16;

            if (player.Classification == PlayerClassification.Junior)
                risk += 4;

            risk = Math.Clamp(risk, 2, 70);
            if (rng.Next(100) >= risk)
                return;

            int drop = rng.Next(100) < 25 ? 2 : 1;
            ReduceRoleRatings(player, drop, rng);
        }

        private static void AdvanceClassification(Player player)
        {
            player.Classification = player.Classification switch
            {
                PlayerClassification.Freshman => PlayerClassification.Sophomore,
                PlayerClassification.Sophomore => PlayerClassification.Junior,
                PlayerClassification.Junior => PlayerClassification.Senior,
                _ => player.Classification
            };
        }

        private static void ImproveRoleRatings(Player player, int amount, Random rng)
        {
            if (amount <= 0) return;
            if (player.Role == PlayerRole.Pitcher)
            {
                player.Pitching = Add(player.Pitching, amount);
                player.Stamina = Add(player.Stamina, rng.Next(100) < player.Durability ? amount : 0);
                player.Fielding = Add(player.Fielding, rng.Next(100) < 35 ? 1 : 0);
                player.HoldRunner = Add(player.HoldRunner, rng.Next(100) < player.WorkEthic ? amount : 0);
                player.Pickoff = Add(player.Pickoff, rng.Next(100) < 45 ? amount : 0);
                player.DeliveryTime = Add(player.DeliveryTime, rng.Next(100) < player.Durability ? 1 : 0);
                if (CanHit(player))
                    player.Power = Add(player.Power, 1);
                return;
            }

            player.Contact = Add(player.Contact, amount);
            player.Power = Add(player.Power, rng.Next(100) < 55 ? amount : 0);
            player.Speed = Add(player.Speed, rng.Next(100) < player.Durability ? 1 : 0);
            player.Fielding = Add(player.Fielding, rng.Next(100) < player.WorkEthic ? amount : 0);
            player.StealAggression = Add(player.StealAggression, rng.Next(100) < 40 ? 1 : 0);
            player.BaseRunning = Add(player.BaseRunning, rng.Next(100) < player.WorkEthic ? amount : 0);
            player.ArmStrength = Add(player.ArmStrength, rng.Next(100) < 35 ? 1 : 0);
            player.PopTime = Add(player.PopTime, rng.Next(100) < 35 ? 1 : 0);
            player.Accuracy = Add(player.Accuracy, rng.Next(100) < player.WorkEthic ? 1 : 0);
            player.TagRating = Add(player.TagRating, rng.Next(100) < player.WorkEthic ? 1 : 0);
        }

        private static void ReduceRoleRatings(Player player, int amount, Random rng)
        {
            if (amount <= 0) return;
            if (player.Role == PlayerRole.Pitcher)
            {
                player.Pitching = Add(player.Pitching, -amount);
                player.Stamina = Add(player.Stamina, -amount);
                if (rng.Next(100) < 35) player.Fielding = Add(player.Fielding, -1);
                if (rng.Next(100) < 35) player.HoldRunner = Add(player.HoldRunner, -1);
                if (rng.Next(100) < 30) player.Pickoff = Add(player.Pickoff, -1);
                if (rng.Next(100) < 30) player.DeliveryTime = Add(player.DeliveryTime, -1);
                return;
            }

            player.Contact = Add(player.Contact, -amount);
            if (rng.Next(100) < 50) player.Speed = Add(player.Speed, -1);
            if (rng.Next(100) < 45) player.Fielding = Add(player.Fielding, -1);
            if (rng.Next(100) < 35) player.BaseRunning = Add(player.BaseRunning, -1);
            if (rng.Next(100) < 30) player.StealAggression = Add(player.StealAggression, -1);
            if (rng.Next(100) < 30) player.Accuracy = Add(player.Accuracy, -1);
            if (rng.Next(100) < 30) player.TagRating = Add(player.TagRating, -1);
        }

        private static void EnsureDevelopment(Player player, Random rng)
        {
            if (player.Classification == PlayerClassification.Unassigned)
                player.Classification = Simulator.RandomClassification(rng);
            if (string.IsNullOrWhiteSpace(player.Positions))
                player.Positions = Simulator.RandomPositions(rng, player.Role);
            if (player.Potential <= 0) player.Potential = Simulator.RandomDevelopmentRating(rng, 40, 95);
            if (player.WorkEthic <= 0) player.WorkEthic = Simulator.RandomDevelopmentRating(rng, 30, 95);
            if (player.Durability <= 0) player.Durability = Simulator.RandomDevelopmentRating(rng, 35, 95);
            if (player.RegressionRisk <= 0) player.RegressionRisk = Simulator.RandomDevelopmentRating(rng, 5, 55);
            if (player.Fielding <= 0) player.Fielding = Simulator.RandomDevelopmentRating(rng, 35, 95);
            if (player.StealAggression <= 0) player.StealAggression = Simulator.RandomDevelopmentRating(rng, 20, 90);
            if (player.BaseRunning <= 0) player.BaseRunning = Simulator.RandomDevelopmentRating(rng, 30, 95);
            if (player.HoldRunner <= 0) player.HoldRunner = Simulator.RandomDevelopmentRating(rng, player.Role == PlayerRole.Pitcher ? 30 : 10, player.Role == PlayerRole.Pitcher ? 95 : 55);
            if (player.Pickoff <= 0) player.Pickoff = Simulator.RandomDevelopmentRating(rng, player.Role == PlayerRole.Pitcher ? 25 : 10, player.Role == PlayerRole.Pitcher ? 90 : 45);
            if (player.DeliveryTime <= 0) player.DeliveryTime = Simulator.RandomDevelopmentRating(rng, player.Role == PlayerRole.Pitcher ? 30 : 10, player.Role == PlayerRole.Pitcher ? 95 : 50);
            if (player.ArmStrength <= 0) player.ArmStrength = Simulator.RandomDevelopmentRating(rng, 30, 95);
            if (player.PopTime <= 0) player.PopTime = Simulator.RandomDevelopmentRating(rng, 30, 95);
            if (player.Accuracy <= 0) player.Accuracy = Simulator.RandomDevelopmentRating(rng, 30, 95);
            if (player.TagRating <= 0) player.TagRating = Simulator.RandomDevelopmentRating(rng, 30, 95);
            if (player.Role == PlayerRole.Pitcher && player.CareerPitchCount <= 0)
                player.CareerPitchCount = Simulator.RandomCareerPitchCount(rng);
        }

        private static bool CanHit(Player player)
        {
            return (player.Positions ?? "").Split('/').Any(p => p == "DH" || p == "1B" || p == "3B" || p == "OF");
        }

        private static int RatingSum(Player player)
            => player.Contact + player.Power + player.Speed + player.StealAggression + player.BaseRunning +
               player.Fielding + player.HoldRunner + player.Pickoff + player.DeliveryTime + player.ArmStrength +
               player.PopTime + player.Accuracy + player.TagRating + player.Pitching + player.Stamina;

        private static int Add(int rating, int amount)
            => Math.Clamp(rating + amount, 0, 99);

        private sealed class PlayerPerformance
        {
            public static PlayerPerformance Empty { get; } = new PlayerPerformance();

            public bool Pitcher { get; set; }
            public int Games { get; set; }
            public int AB { get; set; }
            public int H { get; set; }
            public int HR { get; set; }
            public int RBI { get; set; }
            public int IPOuts { get; set; }
            public int ER { get; set; }
            public int K { get; set; }

            public double Average => AB <= 0 ? 0.0 : (double)H / AB;
            public double PowerImpact => AB <= 0 ? 0.0 : (double)(HR + RBI) / AB;
            public double Era => IPOuts <= 0 ? 99.0 : ER * 27.0 / IPOuts;
            public double KPerNine => IPOuts <= 0 ? 0.0 : K * 27.0 / IPOuts;
        }
    }
}
