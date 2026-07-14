using System;
using System.Collections.Generic;
using System.Linq;

namespace StandaloneBaseball
{
    public static class PositionAssignmentEngine
    {
        public const int OutOfPositionFieldingPenaltyPercent = 25;
        public const int OutOfPositionInjuryRiskPerThousand = 100;
        private const int QualificationGamesRequired = 10;

        public static bool IsOutfield(string position)
            => NormalizePosition(position) is "LF" or "CF" or "RF";

        public static bool HasPosition(Player player, string position)
        {
            string normalized = NormalizePosition(position);
            var parts = PositionParts(player);
            return parts.Contains(normalized) ||
                IsOutfield(normalized) && parts.Contains("OF") ||
                IsPitcherPosition(normalized) && (player?.Role == PlayerRole.Pitcher || parts.Any(IsPitcherPosition));
        }

        public static bool IsNoPenaltyFit(Player player, string assignedPosition)
        {
            string assigned = NormalizePosition(assignedPosition);
            if (HasPosition(player, assigned))
                return true;

            var parts = PositionParts(player);
            if (IsPitcherPosition(assigned) && (player?.Role == PlayerRole.Pitcher || parts.Any(IsPitcherPosition)))
                return true;
            if (IsOutfield(assigned) && (parts.Contains("OF") || parts.Any(IsOutfield)))
                return true;
            if ((assigned == "1B" || assigned == "3B") && (parts.Contains("1B") || parts.Contains("3B")))
                return true;
            if ((assigned == "2B" || assigned == "SS") && (parts.Contains("2B") || parts.Contains("SS")))
                return true;
            if ((assigned == "2B" || assigned == "3B") && (parts.Contains("2B") || parts.Contains("3B")))
                return true;
            return false;
        }

        public static bool IsPenalizedFit(Player player, string assignedPosition)
            => player != null &&
               !string.Equals(NormalizePosition(assignedPosition), "DH", StringComparison.OrdinalIgnoreCase) &&
               !IsNoPenaltyFit(player, assignedPosition);

        public static int ApplyFieldingPenalty(Player player, string assignedPosition, int rating)
        {
            rating = Math.Clamp(rating, 0, 99);
            if (!IsPenalizedFit(player, assignedPosition))
                return rating;
            return Math.Clamp((int)Math.Round(rating * (100 - OutOfPositionFieldingPenaltyPercent) / 100.0), 0, 99);
        }

        public static bool CanAssign(Player player, string assignedPosition)
            => player != null &&
               !string.Equals(NormalizePosition(assignedPosition), "DH", StringComparison.OrdinalIgnoreCase);

        public static int FitTier(Player player, string assignedPosition)
        {
            if (HasPosition(player, assignedPosition))
                return 0;
            if (IsNoPenaltyFit(player, assignedPosition))
                return 1;
            return 2;
        }

        public static void RegisterPositionExperience(Team team, Random rng)
        {
            if (team?.Roster == null)
                return;

            var card = LineupEngine.BuildLineupCard(team);
            var assignments = card.DefensiveAssignments ?? new Dictionary<string, Player>(StringComparer.OrdinalIgnoreCase);
            var assignedByPlayer = assignments
                .Where(pair => pair.Value != null)
                .GroupBy(pair => pair.Value.Id)
                .ToDictionary(g => g.Key, g => NormalizePosition(g.First().Key));

            foreach (var player in team.Roster.Where(p => p != null))
            {
                player.UnqualifiedPositionGameStreaks ??= new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                if (!assignedByPlayer.TryGetValue(player.Id, out string assigned) ||
                    string.Equals(assigned, "DH", StringComparison.OrdinalIgnoreCase) ||
                    HasPosition(player, assigned))
                {
                    player.UnqualifiedPositionGameStreaks.Clear();
                    continue;
                }

                bool penalized = IsPenalizedFit(player, assigned);
                if (penalized)
                    InjuryEngine.TryEventInjury(player, rng, OutOfPositionInjuryRiskPerThousand);

                foreach (string key in player.UnqualifiedPositionGameStreaks.Keys.ToList())
                {
                    if (!string.Equals(key, assigned, StringComparison.OrdinalIgnoreCase))
                        player.UnqualifiedPositionGameStreaks.Remove(key);
                }

                player.UnqualifiedPositionGameStreaks.TryGetValue(assigned, out int current);
                player.UnqualifiedPositionGameStreaks[assigned] = current + 1;

                if (player.UnqualifiedPositionGameStreaks[assigned] >= QualificationGamesRequired &&
                    PositionParts(player).Count < 3 &&
                    !IsPitcherPosition(assigned))
                {
                    AddPosition(player, assigned);
                    player.UnqualifiedPositionGameStreaks.Clear();
                }
            }
        }

        public static string NormalizePosition(string position)
            => (position ?? "").Trim().ToUpperInvariant();

        private static bool IsPitcherPosition(string position)
            => NormalizePosition(position) is "P" or "SP" or "RP" or "CL" or "CP" or "LR" or "MR";

        private static void AddPosition(Player player, string position)
        {
            var parts = PositionParts(player).ToList();
            if (parts.Contains(position))
                return;
            parts.Add(position);
            player.Positions = string.Join("/", parts.Take(3));
        }

        private static HashSet<string> PositionParts(Player player)
            => new HashSet<string>((player?.Positions ?? "")
                .ToUpperInvariant()
                .Split(new[] { '/', ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries),
                StringComparer.OrdinalIgnoreCase);
    }
}
