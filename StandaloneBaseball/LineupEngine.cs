#nullable enable annotations

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace StandaloneBaseball
{
    public sealed class LineupSlot
    {
        public int BattingOrder { get; set; }
        public Player Player { get; set; }
        public string DefensivePosition { get; set; } = "";
        public bool DesignatedHitter { get; set; }
    }

    public sealed class LineupCard
    {
        public List<LineupSlot> BattingOrder { get; set; } = new List<LineupSlot>();
        public Dictionary<string, Player> DefensiveAssignments { get; set; } = new Dictionary<string, Player>(StringComparer.OrdinalIgnoreCase);
        public Player StartingPitcher { get; set; }
        public bool HasDesignatedHitter { get; set; }
        public bool IsValid { get; set; }
        public List<string> MissingPositions { get; set; } = new List<string>();
        public string Status => IsValid
            ? (HasDesignatedHitter ? "Valid lineup with DH" : "Valid lineup")
            : "Invalid lineup: missing " + string.Join(", ", MissingPositions);
    }

    public static class LineupEngine
    {
        private static readonly string[] MandatoryDefensivePositions = { "C", "P", "1B", "2B", "3B", "SS", "LF", "CF", "RF" };

        public static LineupCard BuildLineupCard(Team team)
        {
            if (TryBuildSavedLineupCard(team, out var savedCard))
                return savedCard;

            return CalculateLineupCard(team);
        }

        public static bool TryValidateForGame(Team team, out string error)
        {
            error = "";
            if (team == null)
            {
                error = "Team is missing.";
                return false;
            }
            var card = BuildLineupCard(team);
            if (!card.IsValid || card.BattingOrder.Count != 9)
            {
                string missing = card.MissingPositions.Count == 0 ? "a complete batting order" : string.Join(", ", card.MissingPositions.Distinct());
                error = team.DisplayName + " cannot start: missing or invalid " + missing + ".";
                return false;
            }
            return true;
        }

        public static LineupCard CalculateLineupCard(Team team)
        {
            var card = new LineupCard();
            var available = EligiblePlayers(team).ToList();
            if (team == null || available.Count == 0)
            {
                card.MissingPositions.AddRange(MandatoryDefensivePositions);
                return card;
            }

            var used = new HashSet<Guid>();
            foreach (string position in MandatoryDefensivePositions)
            {
                Player player = position == "P"
                    ? BestPitcher(available, used)
                    : BestFielderForPosition(available, position, used);
                if (player == null)
                {
                    card.MissingPositions.Add(position);
                    continue;
                }

                card.DefensiveAssignments[position] = player;
                used.Add(player.Id);
                if (position == "P")
                    card.StartingPitcher = player;
            }

            card.IsValid = card.MissingPositions.Count == 0;
            var defensivePlayers = card.DefensiveAssignments.Values
                .Where(p => p != null)
                .GroupBy(p => p.Id)
                .Select(g => g.First())
                .ToList();

            var pitcher = card.StartingPitcher;
            var dh = available
                .Where(p => pitcher == null || p.Id != pitcher.Id)
                .Where(p => !defensivePlayers.Any(d => d.Id == p.Id))
                .OrderByDescending(DhScore)
                .FirstOrDefault();

            List<Player> battingPool;
            if (dh != null && pitcher != null)
            {
                card.HasDesignatedHitter = true;
                battingPool = defensivePlayers.Where(p => p.Id != pitcher.Id).Concat(new[] { dh }).ToList();
            }
            else
            {
                battingPool = defensivePlayers.ToList();
            }

            if (battingPool.Count < 9)
            {
                battingPool.AddRange(available
                    .Where(p => !battingPool.Any(b => b.Id == p.Id))
                    .OrderByDescending(HitterBenchScore)
                    .Take(9 - battingPool.Count));
            }

            card.BattingOrder = BuildBattingOrder(battingPool.Take(9).ToList(), dh, card.DefensiveAssignments)
                .Select((slot, index) =>
                {
                    slot.BattingOrder = index + 1;
                    return slot;
                })
                .ToList();

            if (card.BattingOrder.Count < 9)
            {
                card.IsValid = false;
                if (!card.MissingPositions.Contains("BAT"))
                    card.MissingPositions.Add("BAT");
            }

            return card;
        }

        public static TeamBaseLineup CreateBaseLineup(Team team)
            => ToBaseLineup(CalculateLineupCard(team));

        public static IReadOnlyList<Player> GetBattingOrder(Team team)
            => BuildLineupCard(team).BattingOrder.Select(s => s.Player).Where(p => p != null).ToList();

        public static IReadOnlyList<Player> GetPitchingStaff(Team team)
            => PitchingRotationEngine.GetPitchingStaff(team);

        public static Player? GetPitcher(Team team, int pitcherIndex)
        {
            var staff = GetPitchingStaff(team);
            if (staff.Count == 0)
                return null;
            return staff[PositiveModulo(pitcherIndex, staff.Count)];
        }

        public static int FindStartingPitcherIndex(Team team)
            => PitchingRotationEngine.FindStartingPitcherIndex(team);

        public static bool HasPosition(Player player, string position)
            => PositionAssignmentEngine.HasPosition(player, position);

        public static bool CanAssignPosition(Player player, string position)
            => PositionAssignmentEngine.CanAssign(player, position);

        public static bool IsPenalizedPositionAssignment(Player player, string position)
            => PositionAssignmentEngine.IsPenalizedFit(player, position);

        public static int ApplyPositionFieldingPenalty(Player player, string position, int rating)
            => PositionAssignmentEngine.ApplyFieldingPenalty(player, position, rating);

        public static void RegisterPositionExperience(Team team, Random rng)
            => PositionAssignmentEngine.RegisterPositionExperience(team, rng);

        private static IEnumerable<Player> EligiblePlayers(Team team)
            => (team?.Roster ?? new List<Player>())
                .Where(p => p != null && InjuryEngine.IsAvailable(p) && !p.RedshirtActive);

        private static Player? BestPitcher(IEnumerable<Player> available, HashSet<Guid> used)
            => available
                .Where(p => !used.Contains(p.Id))
                .OrderBy(p => PositionAssignmentEngine.FitTier(p, "P"))
                .ThenByDescending(PitcherScore)
                .FirstOrDefault();

        private static Player? BestFielderForPosition(IEnumerable<Player> available, string position, HashSet<Guid> used)
        {
            return available
                .Where(p => !used.Contains(p.Id) && CanAssignPosition(p, position))
                .OrderBy(p => PositionAssignmentEngine.FitTier(p, position))
                .ThenByDescending(p => PositionFitScore(p, position))
                .ThenByDescending(p => p.Overall)
                .FirstOrDefault();
        }

        private static bool TryBuildSavedLineupCard(Team team, [NotNullWhen(true)] out LineupCard? card)
        {
            card = null;
            var saved = team?.BaseLineup;
            if (team?.Roster == null || saved?.BattingOrder == null || saved.BattingOrder.Count != 9)
                return false;

            saved.DefensiveAssignments ??= new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
            var available = EligiblePlayers(team).ToDictionary(p => p.Id, p => p);
            if (available.Count == 0)
                return false;

            var defensive = new Dictionary<string, Player>(StringComparer.OrdinalIgnoreCase);
            var usedDefense = new HashSet<Guid>();
            foreach (string position in MandatoryDefensivePositions)
            {
                if (!saved.DefensiveAssignments.TryGetValue(position, out Guid playerId))
                    return false;
                if (!available.TryGetValue(playerId, out Player player))
                    return false;
                if (!CanAssignPosition(player, position))
                    return false;
                if (!usedDefense.Add(player.Id))
                    return false;
                defensive[position] = player;
            }

            var batting = new List<LineupSlot>();
            var usedBatters = new HashSet<Guid>();
            foreach (var slot in saved.BattingOrder.OrderBy(s => s.BattingOrder))
            {
                if (!available.TryGetValue(slot.PlayerId, out Player player))
                    return false;
                if (!usedBatters.Add(player.Id))
                    return false;

                batting.Add(new LineupSlot
                {
                    BattingOrder = batting.Count + 1,
                    Player = player,
                    DefensivePosition = slot.DesignatedHitter ? "DH" : (slot.DefensivePosition ?? DefensivePositionFor(player)),
                    DesignatedHitter = slot.DesignatedHitter
                });
            }

            if (batting.Count != 9)
                return false;

            defensive.TryGetValue("P", out Player pitcher);
            Player dh = batting.FirstOrDefault(s => s.DesignatedHitter)?.Player;
            if (saved.HasDesignatedHitter)
            {
                if (dh == null || pitcher == null || batting.Any(s => s.Player?.Id == pitcher.Id))
                    return false;
            }
            else if (pitcher != null && !batting.Any(s => s.Player?.Id == pitcher.Id))
            {
                return false;
            }

            card = new LineupCard
            {
                BattingOrder = batting,
                DefensiveAssignments = defensive,
                StartingPitcher = pitcher,
                HasDesignatedHitter = dh != null,
                IsValid = true
            };
            return true;
        }

        private static TeamBaseLineup ToBaseLineup(LineupCard card)
        {
            var baseLineup = new TeamBaseLineup
            {
                LastCalculatedAt = DateTime.Now,
                HasDesignatedHitter = card?.HasDesignatedHitter ?? false,
                StartingPitcherId = card?.StartingPitcher?.Id,
                DesignatedHitterId = card?.BattingOrder?.FirstOrDefault(s => s.DesignatedHitter)?.Player?.Id,
                Status = card?.Status ?? "Invalid lineup"
            };
            if (card?.DefensiveAssignments != null)
            {
                foreach (var pair in card.DefensiveAssignments)
                    if (pair.Value != null)
                        baseLineup.DefensiveAssignments[pair.Key] = pair.Value.Id;
            }
            if (card?.BattingOrder != null)
            {
                baseLineup.BattingOrder = card.BattingOrder
                    .Where(s => s.Player != null)
                    .OrderBy(s => s.BattingOrder)
                    .Select(s => new TeamBaseLineupSlot
                    {
                        BattingOrder = s.BattingOrder,
                        PlayerId = s.Player.Id,
                        PlayerName = s.Player.Name,
                        DefensivePosition = s.DefensivePosition,
                        DesignatedHitter = s.DesignatedHitter
                    })
                    .ToList();
            }
            return baseLineup;
        }

        private static List<LineupSlot> BuildBattingOrder(List<Player> players, Player? designatedHitter, Dictionary<string, Player> defensiveAssignments)
        {
            var remaining = players.Where(p => p != null).DistinctBy(p => p.Id).ToList();
            var result = new List<LineupSlot>();
            AddPick(p => LeadoffScore(p));
            AddPick(p => TwoHoleScore(p));
            AddPick(p => ThreeHoleScore(p));
            AddPick(p => CleanupScore(p));
            AddPick(p => FiveHoleScore(p));
            while (remaining.Count > 0)
                AddPick(p => BottomOrderScore(p));
            return result;

            void AddPick(Func<Player, double> score)
            {
                if (remaining.Count == 0)
                    return;
                var pick = remaining
                    .OrderByDescending(score)
                    .ThenByDescending(p => p.Overall)
                    .First();
                remaining.RemoveAll(p => p.Id == pick.Id);
                string assignedPosition = DefensivePositionFor(pick);
                if (defensiveAssignments != null)
                {
                    var assignment = defensiveAssignments.FirstOrDefault(pair => pair.Value?.Id == pick.Id);
                    if (!string.IsNullOrWhiteSpace(assignment.Key))
                        assignedPosition = assignment.Key;
                }
                result.Add(new LineupSlot
                {
                    Player = pick,
                    DefensivePosition = designatedHitter != null && pick.Id == designatedHitter.Id ? "DH" : assignedPosition,
                    DesignatedHitter = designatedHitter != null && pick.Id == designatedHitter.Id
                });
            }
        }

        private static string DefensivePositionFor(Player player)
        {
            if (player == null)
                return "";
            if (player.Role == PlayerRole.Pitcher || HasPosition(player, "P"))
                return "P";
            return PositionParts(player).FirstOrDefault() ?? "DH";
        }

        private static double LeadoffScore(Player p)
            => p.Contact * 1.25 + p.Speed * 1.15 + p.BaseRunning * 0.85 + ObpProxy(p) * 1.1;

        private static double TwoHoleScore(Player p)
            => p.Contact * 1.15 + p.Speed * 0.8 + ObpProxy(p) * 1.05 + p.Power * 0.45;

        private static double ThreeHoleScore(Player p)
            => p.Contact * 1.0 + p.Power * 1.1 + RbiProxy(p) * 1.0;

        private static double CleanupScore(Player p)
            => p.Power * 1.45 + RbiProxy(p) * 1.15 + p.Contact * 0.35;

        private static double FiveHoleScore(Player p)
            => p.Power * 1.2 + RbiProxy(p) * 1.0 + p.Contact * 0.5;

        private static double BottomOrderScore(Player p)
            => p.Contact * 0.9 + p.Speed * 0.65 + p.Power * 0.65 + p.BaseRunning * 0.35;

        private static double DhScore(Player p)
            => p.Contact * 0.95 + p.Power * 1.2 + p.Speed * 0.25 + RbiProxy(p) * 0.8;

        private static double HitterBenchScore(Player p)
            => p.Contact + p.Power + p.Speed * 0.55 + p.BaseRunning * 0.35;

        private static double ObpProxy(Player p)
            => p.Contact * 0.75 + p.BaseRunning * 0.15 + p.WorkEthic * 0.10;

        private static double RbiProxy(Player p)
            => p.Power * 0.65 + p.Contact * 0.35;

        private static double PitcherScore(Player p)
            => p == null ? 0 : p.Pitching * 1.35 + p.Stamina * 0.95 + p.Accuracy * 0.25;

        private static double PositionFitScore(Player p, string position)
        {
            double score = p.Fielding * 1.2 + p.ArmStrength * 0.25 + p.Speed * (IsOutfield(position) ? 0.35 : 0.1);
            if (position == "C")
                score += p.PopTime * 0.45 + p.ArmStrength * 0.35 + p.TagRating * 0.25;
            if (position == "SS" || position == "2B")
                score += p.Accuracy * 0.35 + p.Speed * 0.25;
            if (position == "1B" || position == "3B")
                score += p.Accuracy * 0.25 + p.TagRating * 0.2;
            if (PositionParts(p).Contains(position))
                score += 25;
            if (IsOutfield(position) && PositionParts(p).Contains("OF"))
                score += 16;
            int fitTier = PositionAssignmentEngine.FitTier(p, position);
            if (fitTier == 1)
                score -= 8;
            else if (fitTier >= 2)
                score -= 32;
            return score;
        }

        private static HashSet<string> PositionParts(Player player)
            => new HashSet<string>((player?.Positions ?? "")
                .ToUpperInvariant()
                .Split(new[] { '/', ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries));

        private static bool IsOutfield(string position)
            => PositionAssignmentEngine.IsOutfield(position);

        private static int PositiveModulo(int value, int divisor)
        {
            if (divisor <= 0) return 0;
            int result = value % divisor;
            return result < 0 ? result + divisor : result;
        }
    }
}
