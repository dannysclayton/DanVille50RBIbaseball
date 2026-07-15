#nullable enable annotations

using System;
using System.Collections.Generic;
using System.Linq;

namespace StandaloneBaseball
{
    public static class ScheduleGenerator
    {
        private sealed class Placement
        {
            public Guid TeamId { get; set; }
            public Guid ConferenceId { get; set; }
            public Guid RegionId { get; set; }
            public Guid DistrictId { get; set; }
        }

        private sealed class SeriesBlock
        {
            public ScheduledGameType Type { get; set; }
            public Guid AwayTeamId { get; set; }
            public Guid HomeTeamId { get; set; }
            public int FirstIndex { get; set; }
            public List<ScheduledGame> Games { get; set; } = new List<ScheduledGame>();
        }

        public static List<ScheduledGame> Generate(LeagueFile league, SeasonScheduleRules rules, out string? error)
        {
            error = null;
            var schedule = new List<ScheduledGame>();
            if (league == null || league.Teams == null || league.Teams.Count == 0)
                return schedule;

            rules ??= new SeasonScheduleRules();
            if (!rules.HasAnyGames)
                return schedule;

            PlayoffEngine.EnsureDefaultStructure(league);
            var placements = BuildPlacements(league);
            var teamIds = league.Teams.Select(t => t.Id).ToList();
            if (placements.Count != teamIds.Count)
            {
                error = "Every team must be assigned to the league structure before a schedule can be generated.";
                return schedule;
            }

            if (!ValidateBalanced("District", rules.DistrictHomeGames, rules.DistrictAwayGames, out error)
                || !ValidateBalanced("Region", rules.RegionHomeGames, rules.RegionAwayGames, out error)
                || !ValidateBalanced("Conference", rules.ConferenceHomeGames, rules.ConferenceAwayGames, out error)
                || !ValidateBalanced("Non-conference", rules.NonConferenceHomeGames, rules.NonConferenceAwayGames, out error))
            {
                return schedule;
            }

            int seriesLength = Math.Clamp(rules.SeriesLength <= 0 ? 3 : rules.SeriesLength, 1, 6);

            AddCategory(schedule, teamIds, placements, ScheduledGameType.District, rules.DistrictHomeGames, seriesLength, SameDistrict, ref error);
            if (error != null) return new List<ScheduledGame>();
            AddCategory(schedule, teamIds, placements, ScheduledGameType.Region, rules.RegionHomeGames, seriesLength, SameRegionDifferentDistrict, ref error);
            if (error != null) return new List<ScheduledGame>();
            AddCategory(schedule, teamIds, placements, ScheduledGameType.Conference, rules.ConferenceHomeGames, seriesLength, SameConferenceDifferentRegion, ref error);
            if (error != null) return new List<ScheduledGame>();
            AddCategory(schedule, teamIds, placements, ScheduledGameType.NonConference, rules.NonConferenceHomeGames, seriesLength, DifferentConference, ref error);
            if (error != null) return new List<ScheduledGame>();

            AssignCalendarSlots(schedule, seriesLength);
            return schedule;
        }

        private static void AssignCalendarSlots(List<ScheduledGame> schedule, int seriesLength)
        {
            if (schedule == null || schedule.Count == 0)
                return;

            seriesLength = Math.Clamp(seriesLength <= 0 ? 3 : seriesLength, 1, 6);
            var pending = BuildSeriesBlocks(schedule, seriesLength)
                .OrderBy(b => b.Type)
                .ThenBy(b => PairKey(b.AwayTeamId, b.HomeTeamId))
                .ThenBy(b => b.FirstIndex)
                .ToList();
            var assigned = new List<ScheduledGame>();
            int week = 1;
            int gameNumber = 1;

            while (pending.Count > 0)
            {
                var usedTeams = new HashSet<Guid>();
                int weekGameNumber = 1;
                int assignedThisWeek = 0;

                for (int i = 0; i < pending.Count;)
                {
                    var block = pending[i];
                    if (usedTeams.Contains(block.AwayTeamId) || usedTeams.Contains(block.HomeTeamId))
                    {
                        i++;
                        continue;
                    }

                    usedTeams.Add(block.AwayTeamId);
                    usedTeams.Add(block.HomeTeamId);
                    AssignSeriesBlock(block, week, ref gameNumber, ref weekGameNumber, assigned);
                    pending.RemoveAt(i);
                    assignedThisWeek++;
                }

                if (assignedThisWeek == 0)
                {
                    int fallbackWeekGameNumber = 1;
                    AssignSeriesBlock(pending[0], week, ref gameNumber, ref fallbackWeekGameNumber, assigned);
                    pending.RemoveAt(0);
                }

                week++;
            }

            schedule.Clear();
            schedule.AddRange(assigned.OrderBy(g => g.GameNumber));
        }

        private static List<SeriesBlock> BuildSeriesBlocks(List<ScheduledGame> schedule, int seriesLength)
        {
            var blocks = new List<SeriesBlock>();
            var indexed = schedule.Select((game, index) => new { game, index });
            foreach (var group in indexed.GroupBy(x => SeriesKey(x.game)))
            {
                var ordered = group.OrderBy(x => x.index).ToList();
                for (int i = 0; i < ordered.Count; i += seriesLength)
                {
                    var chunk = ordered.Skip(i).Take(seriesLength).ToList();
                    if (chunk.Count == 0)
                        continue;

                    var first = chunk[0].game;
                    blocks.Add(new SeriesBlock
                    {
                        Type = first.Type,
                        AwayTeamId = first.AwayTeamId,
                        HomeTeamId = first.HomeTeamId,
                        FirstIndex = chunk[0].index,
                        Games = chunk.Select(x => x.game).ToList()
                    });
                }
            }

            return blocks;
        }

        private static void AssignSeriesBlock(
            SeriesBlock block,
            int week,
            ref int gameNumber,
            ref int weekGameNumber,
            List<ScheduledGame> assigned)
        {
            var slots = block.Games
                .Select((game, index) => new { game, slot = SeriesSlot(index + 1, block.Games.Count) })
                .ToList();
            var dayCounts = slots
                .GroupBy(x => x.slot.day)
                .ToDictionary(g => g.Key, g => g.Count());

            foreach (var item in slots)
            {
                var game = item.game;
                var slot = item.slot;
                game.Week = week;
                game.DayLabel = dayCounts[slot.day] > 1 ? slot.day + " DH" + slot.dayGameNumber : slot.day;
                game.DayGameNumber = slot.dayGameNumber;
                game.GameNumber = gameNumber++;
                game.WeekGameNumber = weekGameNumber++;
                assigned.Add(game);
            }
        }

        private static (string day, int dayGameNumber) SeriesSlot(int gameInSeries, int seriesSize)
        {
            seriesSize = Math.Clamp(seriesSize, 1, 6);
            return seriesSize switch
            {
                1 => ("Friday", 1),
                2 => gameInSeries == 1 ? ("Friday", 1) : ("Saturday", 1),
                3 => gameInSeries == 1 ? ("Friday", 1) : gameInSeries == 2 ? ("Saturday", 1) : ("Sunday", 1),
                4 => gameInSeries == 1 ? ("Friday", 1) : gameInSeries == 2 ? ("Saturday", 1) : ("Sunday", gameInSeries - 2),
                5 => gameInSeries == 1 ? ("Friday", 1) : gameInSeries <= 3 ? ("Saturday", gameInSeries - 1) : ("Sunday", gameInSeries - 3),
                _ => gameInSeries <= 2 ? ("Friday", gameInSeries) : gameInSeries <= 4 ? ("Saturday", gameInSeries - 2) : ("Sunday", gameInSeries - 4)
            };
        }

        private static bool ValidateBalanced(string name, int home, int away, out string? error)
        {
            error = null;
            if (home < 0 || away < 0)
            {
                error = name + " game counts cannot be negative.";
                return false;
            }

            if (home != away)
            {
                error = name + " home and away counts must match so every scheduled game has one home team and one away team.";
                return false;
            }

            return true;
        }

        private static void AddCategory(
            List<ScheduledGame> schedule,
            List<Guid> teamIds,
            Dictionary<Guid, Placement> placements,
            ScheduledGameType type,
            int homeAwayCount,
            int seriesLength,
            Func<Placement, Placement, bool> relation,
            ref string? error)
        {
            if (homeAwayCount <= 0)
                return;

            seriesLength = Math.Clamp(seriesLength <= 0 ? 3 : seriesLength, 1, 6);
            var homeNeeds = teamIds.ToDictionary(id => id, id => homeAwayCount);
            var awayNeeds = teamIds.ToDictionary(id => id, id => homeAwayCount);
            var pairCounts = new Dictionary<string, int>();

            while (homeNeeds.Values.Any(v => v > 0))
            {
                Guid home = homeNeeds
                    .Where(kv => kv.Value > 0)
                    .OrderByDescending(kv => kv.Value)
                    .ThenBy(kv => TeamTotalGames(kv.Key, schedule))
                    .First().Key;

                var candidates = teamIds
                    .Where(id => id != home && awayNeeds[id] > 0)
                    .Where(id => relation(placements[home], placements[id]))
                    .OrderBy(id => PairCount(pairCounts, home, id))
                    .ThenBy(id => TeamTotalGames(id, schedule))
                    .ToList();

                if (candidates.Count == 0)
                {
                    error = "Could not create a complete " + type + " schedule. Add enough teams in the matching structure level or lower that category's game count.";
                    return;
                }

                Guid away = candidates[0];
                int gamesInSeries = Math.Min(seriesLength, Math.Min(homeNeeds[home], awayNeeds[away]));
                for (int i = 0; i < gamesInSeries; i++)
                {
                    schedule.Add(new ScheduledGame
                    {
                        Type = type,
                        HomeTeamId = home,
                        AwayTeamId = away
                    });
                }
                homeNeeds[home] -= gamesInSeries;
                awayNeeds[away] -= gamesInSeries;
                string key = home.ToString("N") + "-" + away.ToString("N");
                pairCounts.TryGetValue(key, out int count);
                pairCounts[key] = count + gamesInSeries;
            }
        }

        private static int TeamTotalGames(Guid teamId, IEnumerable<ScheduledGame> schedule)
            => schedule.Count(g => g.AwayTeamId == teamId || g.HomeTeamId == teamId);

        private static int PairCount(Dictionary<string, int> pairCounts, Guid home, Guid away)
        {
            pairCounts.TryGetValue(home.ToString("N") + "-" + away.ToString("N"), out int count);
            return count;
        }

        private static string PairKey(Guid awayTeamId, Guid homeTeamId)
        {
            string a = awayTeamId.ToString("N");
            string b = homeTeamId.ToString("N");
            return string.CompareOrdinal(a, b) <= 0 ? a + "-" + b : b + "-" + a;
        }

        private static string SeriesKey(ScheduledGame game)
            => ((int)game.Type).ToString() + ":" + game.AwayTeamId.ToString("N") + ":" + game.HomeTeamId.ToString("N");

        private static Dictionary<Guid, Placement> BuildPlacements(LeagueFile league)
        {
            var result = new Dictionary<Guid, Placement>();
            foreach (var conference in league.Structure.Conferences)
            {
                foreach (var region in conference.Regions ?? Enumerable.Empty<Region>())
                {
                    foreach (var district in region.Districts ?? Enumerable.Empty<District>())
                    {
                        foreach (var teamId in district.TeamIds ?? new List<Guid>())
                        {
                            result[teamId] = new Placement
                            {
                                TeamId = teamId,
                                ConferenceId = conference.Id,
                                RegionId = region.Id,
                                DistrictId = district.Id
                            };
                        }
                    }
                }
            }

            return result;
        }

        private static bool SameDistrict(Placement a, Placement b)
            => a.DistrictId == b.DistrictId;

        private static bool SameRegionDifferentDistrict(Placement a, Placement b)
            => a.RegionId == b.RegionId && a.DistrictId != b.DistrictId;

        private static bool SameConferenceDifferentRegion(Placement a, Placement b)
            => a.ConferenceId == b.ConferenceId && a.RegionId != b.RegionId;

        private static bool DifferentConference(Placement a, Placement b)
            => a.ConferenceId != b.ConferenceId;
    }
}
