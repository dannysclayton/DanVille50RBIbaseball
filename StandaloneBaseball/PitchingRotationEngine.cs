using System;
using System.Collections.Generic;
using System.Linq;

namespace StandaloneBaseball
{
    public static class PitchingRotationEngine
    {
        public const int ThreeManPitchCountPenaltyPercent = 10;
        public const int FourManPitchCountPenaltyPercent = 5;
        public const int ThreeManInjuryRiskBonusPercent = 5;
        public const int FourManInjuryRiskBonusPercent = 3;

        public static TeamPitchingPlan CreatePitchingPlan(Team team, int? requestedRotationSize = null)
        {
            var staff = EligiblePitchers(team).OrderByDescending(StarterScore).ToList();
            int rotationSize = Math.Clamp(requestedRotationSize ?? team?.PitchingPlan?.RotationSize ?? 5, 3, 5);
            if (staff.Count > 0)
                rotationSize = Math.Min(rotationSize, staff.Count);

            var starters = staff.Take(rotationSize).ToList();
            var bullpen = staff.Skip(rotationSize).ToList();

            var plan = new TeamPitchingPlan
            {
                RotationSize = Math.Clamp(rotationSize, 3, 5),
                NextStarterSlot = Math.Clamp(team?.PitchingPlan?.NextStarterSlot ?? 0, 0, Math.Max(0, rotationSize - 1)),
                LastCalculatedAt = DateTime.Now,
                StarterRotationIds = starters.Select(p => p.Id).ToList(),
                Status = staff.Count < 3
                    ? "Needs at least 3 available pitchers for a legal rotation."
                    : "Auto-calculated from available pitching staff."
            };

            foreach (var player in AssignBullpenRoles(bullpen))
                plan.BullpenRoles.Add(player);

            return plan;
        }

        public static void NormalizePitchingPlan(Team team)
        {
            if (team == null)
                return;

            team.PitchingPlan ??= new TeamPitchingPlan();
            team.PitchingPlan.RotationSize = Math.Clamp(team.PitchingPlan.RotationSize, 3, 5);
            team.PitchingPlan.NextStarterSlot = Math.Max(0, team.PitchingPlan.NextStarterSlot);
            team.PitchingPlan.AllStarPitchingScheduleIds ??= new List<Guid>();
            team.PitchingPlan.StarterRotationIds ??= new List<Guid>();
            team.PitchingPlan.BullpenRoles ??= new List<BullpenRoleAssignment>();

            var rosterIds = (team.Roster ?? new List<Player>()).Where(p => p != null).Select(p => p.Id).ToHashSet();
            team.PitchingPlan.StarterRotationIds = team.PitchingPlan.StarterRotationIds
                .Where(id => id != Guid.Empty && rosterIds.Contains(id))
                .Distinct()
                .Take(team.PitchingPlan.RotationSize)
                .ToList();
            team.PitchingPlan.AllStarPitchingScheduleIds = team.PitchingPlan.AllStarPitchingScheduleIds
                .Where(id => id != Guid.Empty && rosterIds.Contains(id))
                .Distinct()
                .ToList();

            team.PitchingPlan.BullpenRoles = team.PitchingPlan.BullpenRoles
                .Where(r => r != null && r.PlayerId != Guid.Empty && rosterIds.Contains(r.PlayerId))
                .GroupBy(r => r.PlayerId)
                .Select(g => g.First())
                .ToList();

            if (team.PitchingPlan.StarterRotationIds.Count == 0)
                team.PitchingPlan = CreatePitchingPlan(team);
            if (team.PitchingPlan.StarterRotationIds.Count > 0)
                team.PitchingPlan.NextStarterSlot %= team.PitchingPlan.StarterRotationIds.Count;
        }

        public static IReadOnlyList<Player> GetPitchingStaff(Team team)
        {
            var staff = EligiblePitchers(team).ToList();
            if (staff.Count == 0)
                return EligiblePlayers(team).OrderByDescending(StarterScore).ToList();

            NormalizePitchingPlan(team);
            var byId = staff.ToDictionary(p => p.Id, p => p);
            var ordered = new List<Player>();
            if (team.PitchingPlan.UseAllStarPitchingRules && team.PitchingPlan.AllStarPitchingScheduleIds.Count > 0)
            {
                foreach (Guid id in team.PitchingPlan.AllStarPitchingScheduleIds)
                {
                    if (byId.TryGetValue(id, out var player) && ordered.All(p => p.Id != player.Id))
                        ordered.Add(player);
                }

                ordered.AddRange(staff.Where(p => ordered.All(o => o.Id != p.Id)).OrderByDescending(StarterScore));
                return ordered;
            }

            Guid currentStarterId = team.PitchingPlan.StarterRotationIds.Count == 0
                ? Guid.Empty
                : team.PitchingPlan.StarterRotationIds[Math.Clamp(team.PitchingPlan.NextStarterSlot, 0, team.PitchingPlan.StarterRotationIds.Count - 1)];
            if (byId.TryGetValue(currentStarterId, out var currentStarter))
                ordered.Add(currentStarter);

            var roleMap = team.PitchingPlan.BullpenRoles
                .Where(r => r != null)
                .GroupBy(r => r.PlayerId)
                .ToDictionary(g => g.Key, g => g.First().Role);

            ordered.AddRange(staff
                .Where(p => ordered.All(o => o.Id != p.Id))
                .Where(p => !team.PitchingPlan.StarterRotationIds.Contains(p.Id))
                .OrderBy(p => BullpenRolePriority(roleMap.TryGetValue(p.Id, out var role) ? role : BullpenRole.MiddleRelief))
                .ThenByDescending(RelieverScore));

            ordered.AddRange(staff
                .Where(p => ordered.All(o => o.Id != p.Id))
                .Where(p => team.PitchingPlan.StarterRotationIds.Contains(p.Id))
                .OrderByDescending(StarterScore));

            return ordered;
        }

        public static int FindStartingPitcherIndex(Team team)
        {
            var staff = GetPitchingStaff(team).ToList();
            if (staff.Count == 0)
                return 0;

            NormalizePitchingPlan(team);
            if (team.PitchingPlan.UseAllStarPitchingRules && team.PitchingPlan.AllStarPitchingScheduleIds.Count > 0)
            {
                Guid firstAllStarPitcherId = team.PitchingPlan.AllStarPitchingScheduleIds.First();
                int allStarIndex = staff.FindIndex(p => p.Id == firstAllStarPitcherId);
                return allStarIndex >= 0 ? allStarIndex : 0;
            }

            var starterId = team.PitchingPlan.StarterRotationIds.Count == 0
                ? Guid.Empty
                : team.PitchingPlan.StarterRotationIds[Math.Clamp(team.PitchingPlan.NextStarterSlot, 0, team.PitchingPlan.StarterRotationIds.Count - 1)];
            int index = staff.FindIndex(p => p.Id == starterId);
            return index >= 0 ? index : 0;
        }

        public static bool IsRotationStarter(Team team, Player player)
            => team?.PitchingPlan?.StarterRotationIds?.Contains(player?.Id ?? Guid.Empty) == true;

        public static bool IsCurrentScheduledStarter(Team team, Player player)
        {
            if (team?.PitchingPlan?.StarterRotationIds == null || player == null)
                return false;
            NormalizePitchingPlan(team);
            if (team.PitchingPlan.StarterRotationIds.Count == 0)
                return false;
            return team.PitchingPlan.StarterRotationIds[team.PitchingPlan.NextStarterSlot] == player.Id;
        }

        public static bool IsStarterBlockedFromRelief(Team team, Player player)
        {
            if (!IsRotationStarter(team, player) || IsCurrentScheduledStarter(team, player))
                return false;

            NormalizePitchingPlan(team);
            var starters = team.PitchingPlan.StarterRotationIds;
            int count = starters.Count;
            if (count <= 1)
                return false;

            int slot = starters.FindIndex(id => id == player.Id);
            if (slot < 0)
                return false;

            int nextSlot = (team.PitchingPlan.NextStarterSlot + 1) % count;
            int previousSlot = (team.PitchingPlan.NextStarterSlot - 1 + count) % count;
            return slot == nextSlot || slot == previousSlot;
        }

        public static bool CanUseStarterInRelief(Team team, Player player)
            => IsRotationStarter(team, player) &&
                !IsCurrentScheduledStarter(team, player) &&
                !IsStarterBlockedFromRelief(team, player);

        public static void AdvanceRotationAfterStart(Team team, Guid starterId)
        {
            if (team?.PitchingPlan?.StarterRotationIds == null || team.PitchingPlan.StarterRotationIds.Count == 0)
                return;

            int index = team.PitchingPlan.StarterRotationIds.FindIndex(id => id == starterId);
            if (index < 0)
                index = team.PitchingPlan.NextStarterSlot;
            team.PitchingPlan.NextStarterSlot = (index + 1) % team.PitchingPlan.StarterRotationIds.Count;
        }

        public static Player? AllStarPitcherForInning(Team team, int inning)
        {
            if (team?.PitchingPlan?.UseAllStarPitchingRules != true || inning <= 0)
                return null;

            NormalizePitchingPlan(team);
            var schedule = team.PitchingPlan.AllStarPitchingScheduleIds ?? new List<Guid>();
            if (inning > schedule.Count)
                return null;

            Guid playerId = schedule[inning - 1];
            return (team.Roster ?? new List<Player>()).FirstOrDefault(p => p != null && p.Id == playerId);
        }

        public static int AllStarPitcherIndexForInning(Team team, int inning)
        {
            var player = AllStarPitcherForInning(team, inning);
            if (player == null)
                return -1;

            var staff = GetPitchingStaff(team).ToList();
            return staff.FindIndex(p => p.Id == player.Id);
        }

        public static int RotationPitchCountPenaltyPercent(Team? team)
        {
            int size = Math.Clamp(team?.PitchingPlan?.RotationSize ?? 5, 3, 5);
            return size switch
            {
                3 => ThreeManPitchCountPenaltyPercent,
                4 => FourManPitchCountPenaltyPercent,
                _ => 0
            };
        }

        public static int RotationInjuryRiskBonusPercent(Team? team)
        {
            int size = Math.Clamp(team?.PitchingPlan?.RotationSize ?? 5, 3, 5);
            return size switch
            {
                3 => ThreeManInjuryRiskBonusPercent,
                4 => FourManInjuryRiskBonusPercent,
                _ => 0
            };
        }

        public static int ApplyStarterPitchCountPenalty(Player? pitcher, Team? team, int baseLimit)
        {
            int rotationPenalty = RotationPitchCountPenaltyPercent(team);
            int nextStartPenalty = Math.Clamp(pitcher?.NextStartPitchCountPenaltyPercent ?? 0, 0, 90);
            double multiplier = Math.Max(0.1, 1.0 - (rotationPenalty + nextStartPenalty) / 100.0);
            return Math.Max(1, (int)Math.Round(baseLimit * multiplier));
        }

        public static int RelieverBackToBackPenaltyPercent(int consecutiveReliefGames)
            => Math.Max(0, consecutiveReliefGames - 1) * 10;

        public static void UpdateSeasonPitcherUsage(Season season, Team team, GameResult result)
        {
            if (season == null || team == null || result == null)
                return;

            season.PitcherUsage ??= new Dictionary<Guid, PitcherUsageState>();
            NormalizePitchingPlan(team);
            int teamGameNumber = CountTeamGamesThroughResult(season, team.Id, result.Id);
            var starterIds = (team.PitchingPlan?.StarterRotationIds ?? new List<Guid>()).ToHashSet();
            var usedPitcherIds = result.Lines.Where(l => l.TeamId == team.Id && l.Pitcher).Select(l => l.PlayerId).ToHashSet();
            foreach (var restedReliever in (team.Roster ?? new List<Player>())
                .Where(p => p != null && !starterIds.Contains(p.Id) && !usedPitcherIds.Contains(p.Id)))
            {
                restedReliever.ConsecutiveReliefGames = 0;
                if (season.PitcherUsage.TryGetValue(restedReliever.Id, out var restedUsage))
                    restedUsage.ConsecutiveReliefGames = 0;
            }

            foreach (var line in result.Lines.Where(l => l.TeamId == team.Id && l.Pitcher))
            {
                var player = (team.Roster ?? new List<Player>()).FirstOrDefault(p => p.Id == line.PlayerId);
                if (!season.PitcherUsage.TryGetValue(line.PlayerId, out var usage))
                {
                    usage = new PitcherUsageState { PlayerId = line.PlayerId, TeamId = team.Id };
                    season.PitcherUsage[line.PlayerId] = usage;
                }

                usage.TeamId = team.Id;
                usage.LastTeamGameNumber = teamGameNumber;

                if (line.StartingPitcher)
                {
                    usage.LastStartTeamGameNumber = teamGameNumber;
                    usage.OutsSinceLastStart = 0;
                    usage.NextStartPitchCountPenaltyPercent = 0;
                    usage.ConsecutiveReliefGames = 0;
                    usage.Notes = "";
                    if (player != null)
                    {
                        player.StarterReliefOutsSinceLastStart = 0;
                        player.NextStartPitchCountPenaltyPercent = 0;
                        player.ConsecutiveReliefGames = 0;
                    }
                    AdvanceRotationAfterStart(team, line.PlayerId);
                    continue;
                }

                bool rotationStarterUsedInRelief = starterIds.Contains(line.PlayerId);
                if (rotationStarterUsedInRelief)
                {
                    int excessOutsThisGame = Math.Max(0, line.IPOuts - 3);
                    usage.OutsSinceLastStart += line.IPOuts;
                    usage.NextStartPitchCountPenaltyPercent += excessOutsThisGame * 10;
                    if (player != null)
                    {
                        player.StarterReliefOutsSinceLastStart = usage.OutsSinceLastStart;
                        player.NextStartPitchCountPenaltyPercent = usage.NextStartPitchCountPenaltyPercent;
                    }
                    usage.Notes = excessOutsThisGame > 0
                        ? "Starter relief exceeded 3 outs this game; next start pitch count reduced by " + usage.NextStartPitchCountPenaltyPercent + "% total."
                        : "Starter used in relief within 3-out limit for this game.";
                }
                else
                {
                    usage.ConsecutiveReliefGames = usage.LastReliefTeamGameNumber == teamGameNumber - 1
                        ? usage.ConsecutiveReliefGames + 1
                        : 1;
                    usage.LastReliefTeamGameNumber = teamGameNumber;
                    if (player != null)
                        player.ConsecutiveReliefGames = usage.ConsecutiveReliefGames;
                    int penalty = RelieverBackToBackPenaltyPercent(usage.ConsecutiveReliefGames);
                    usage.Notes = penalty > 0 ? "Back-to-back relief penalty: -" + penalty + "% stats." : "";
                }
            }
        }

        private static int CountTeamGamesThroughResult(Season season, Guid teamId, Guid resultId)
        {
            int count = 0;
            foreach (var game in season.Games)
            {
                if (game.AwayTeamId == teamId || game.HomeTeamId == teamId)
                    count++;
                if (game.Id == resultId)
                    break;
            }
            return Math.Max(1, count);
        }

        private static IEnumerable<BullpenRoleAssignment> AssignBullpenRoles(List<Player> bullpen)
        {
            var orderedByRelief = bullpen.OrderByDescending(RelieverScore).ToList();
            var closer = orderedByRelief.FirstOrDefault();
            if (closer != null)
                yield return Role(closer, BullpenRole.Closer);

            foreach (var setup in orderedByRelief.Where(p => closer == null || p.Id != closer.Id).Take(2))
                yield return Role(setup, BullpenRole.Setup);

            var used = orderedByRelief.Take(closer == null ? 0 : 1).Concat(orderedByRelief.Where(p => closer == null || p.Id != closer.Id).Take(2)).Select(p => p.Id).ToHashSet();
            foreach (var longRelief in bullpen.Where(p => !used.Contains(p.Id)).OrderByDescending(LongReliefScore).Take(2))
            {
                used.Add(longRelief.Id);
                yield return Role(longRelief, BullpenRole.LongRelief);
            }

            foreach (var middle in bullpen.Where(p => !used.Contains(p.Id)).OrderByDescending(RelieverScore))
                yield return Role(middle, BullpenRole.MiddleRelief);
        }

        private static BullpenRoleAssignment Role(Player player, BullpenRole role)
            => new BullpenRoleAssignment { PlayerId = player.Id, PlayerName = player.Name, Role = role };

        private static IEnumerable<Player> EligiblePitchers(Team team)
            => EligiblePlayers(team).Where(p => p.Role == PlayerRole.Pitcher || LineupEngine.HasPosition(p, "P"));

        private static IEnumerable<Player> EligiblePlayers(Team team)
            => (team?.Roster ?? new List<Player>()).Where(p => p != null && InjuryEngine.IsAvailable(p) && !p.RedshirtActive);

        private static int BullpenRolePriority(BullpenRole role)
            => role switch
            {
                BullpenRole.LongRelief => 0,
                BullpenRole.Setup => 1,
                BullpenRole.Closer => 2,
                _ => 3
            };

        public static double StarterScore(Player p)
            => p == null ? 0 : p.Pitching * 1.35 + p.Stamina * 0.95 + p.Accuracy * 0.25;

        public static double RelieverScore(Player p)
            => p == null ? 0 : p.Pitching * 1.4 + p.Accuracy * 0.6 + p.Stamina * 0.2;

        private static double LongReliefScore(Player p)
            => p == null ? 0 : p.Stamina * 1.2 + p.Pitching * 0.9 + p.Accuracy * 0.35;
    }
}
