using System;
using System.Collections.Generic;
using System.Linq;

namespace StandaloneBaseball
{
    public sealed class RecordsBookEntry
    {
        public string Level { get; set; } = "";
        public string LevelName { get; set; } = "";
        public string Scope { get; set; } = "";
        public string Category { get; set; } = "";
        public string Record { get; set; } = "";
        public string Holder { get; set; } = "";
        public string Team { get; set; } = "";
        public string Value { get; set; } = "";
        public int SeasonNumber { get; set; }
        public string SeasonName { get; set; } = "";
        public string Opponent { get; set; } = "";
        public DateTime? Date { get; set; }
        public string Game { get; set; } = "";
        public string Detail { get; set; } = "";
        public double SortValue { get; set; }
    }

    public static class RecordsBookEngine
    {
        private sealed class Placement
        {
            public Conference Conference { get; set; }
            public Region Region { get; set; }
            public District District { get; set; }
        }

        private sealed class SeasonContext
        {
            public Season Season { get; set; }
            public int Number { get; set; }
        }

        private sealed class PlayerGameRecordRow
        {
            public PlayerGameLine Line { get; set; }
            public GameResult Game { get; set; }
            public int SeasonNumber { get; set; }
            public string SeasonName { get; set; } = "";
        }

        private sealed class TeamGameRecordRow
        {
            public Guid TeamId { get; set; }
            public GameResult Game { get; set; }
            public int SeasonNumber { get; set; }
            public string SeasonName { get; set; } = "";
            public PlayerGameLine Stats { get; set; }
            public int Runs { get; set; }
            public int RunsAllowed { get; set; }
        }

        private sealed class PlayerAggregate
        {
            public Guid PlayerId { get; set; }
            public Guid TeamId { get; set; }
            public string PlayerName { get; set; } = "";
            public string TeamName { get; set; } = "";
            public int SeasonNumber { get; set; }
            public string SeasonName { get; set; } = "";
            public PlayerGameLine Stats { get; set; } = new PlayerGameLine();
        }

        private sealed class TeamAggregate
        {
            public Guid TeamId { get; set; }
            public string TeamName { get; set; } = "";
            public int SeasonNumber { get; set; }
            public string SeasonName { get; set; } = "";
            public int Wins { get; set; }
            public int Losses { get; set; }
            public int Runs { get; set; }
            public int RunsAllowed { get; set; }
            public PlayerGameLine Stats { get; set; } = new PlayerGameLine();
        }

        public static List<RecordsBookEntry> Build(LeagueFile league, string level, Guid? entityId, string scope)
        {
            var entries = new List<RecordsBookEntry>();
            if (league?.Teams == null || league.Seasons == null)
                return entries;

            level = string.IsNullOrWhiteSpace(level) ? "League" : level;
            scope = string.IsNullOrWhiteSpace(scope) ? "All" : scope;
            var placements = BuildPlacements(league);
            string levelName = LevelName(league, level, entityId);
            var teamIds = TeamIdsForLevel(league, placements, level, entityId);
            var teams = league.Teams.ToDictionary(t => t.Id);
            var seasons = league.Seasons.Select((season, index) => new SeasonContext { Season = season, Number = index + 1 }).ToList();

            if (scope == "All" || scope == "Game")
            {
                AddPlayerGameRecords(entries, seasons, teams, teamIds, level, levelName);
                AddTeamGameRecords(entries, seasons, teams, teamIds, level, levelName);
            }
            if (scope == "All" || scope == "Season")
            {
                AddPlayerSeasonRecords(entries, seasons, teams, teamIds, level, levelName);
                AddTeamSeasonRecords(entries, seasons, teams, teamIds, level, levelName);
            }
            if (scope == "All" || scope == "Career")
            {
                AddPlayerCareerRecords(entries, seasons, teams, teamIds, level, levelName);
                AddTeamCareerRecords(entries, seasons, teams, teamIds, level, levelName);
            }

            return entries
                .OrderBy(e => ScopeOrder(e.Scope))
                .ThenBy(e => e.Category)
                .ThenBy(e => e.Record)
                .ThenBy(e => e.Holder)
                .ToList();
        }

        public static List<(string Level, Guid? Id, string Name)> EntitiesForLevel(LeagueFile league, string level)
        {
            var list = new List<(string, Guid?, string)>();
            if (league == null)
                return list;
            if (level == "League")
            {
                list.Add(("League", null, league.Name ?? "League"));
                return list;
            }
            if (level == "Team")
            {
                list.AddRange((league.Teams ?? new List<Team>()).OrderBy(t => t.DisplayName).Select(t => ("Team", (Guid?)t.Id, t.DisplayName)));
                return list;
            }
            foreach (var conference in league.Structure?.Conferences ?? new List<Conference>())
            {
                if (level == "Conference")
                    list.Add(("Conference", conference.Id, conference.Name));
                foreach (var region in conference.Regions ?? new List<Region>())
                {
                    if (level == "Region")
                        list.Add(("Region", region.Id, conference.Name + " - " + region.Name));
                    foreach (var district in region.Districts ?? new List<District>())
                    {
                        if (level == "District")
                            list.Add(("District", district.Id, conference.Name + " - " + region.Name + " - " + district.Name));
                    }
                }
            }
            return list;
        }

        private static void AddPlayerGameRecords(List<RecordsBookEntry> entries, IEnumerable<SeasonContext> seasons, Dictionary<Guid, Team> teams, HashSet<Guid> teamIds, string level, string levelName)
        {
            var rows = new List<PlayerGameRecordRow>();
            foreach (var item in seasons)
            {
                foreach (var game in item.Season.Games ?? new List<GameResult>())
                {
                    foreach (var line in game.Lines ?? new List<PlayerGameLine>())
                    {
                        if (teamIds.Contains(line.TeamId))
                            rows.Add(new PlayerGameRecordRow { Line = line, Game = game, SeasonNumber = item.Number, SeasonName = item.Season.Name });
                    }
                }
            }

            AddBest(entries, rows, "Player Game", "Hits", r => r.Line.H, r => r.Line.H.ToString(), r => r.Line.H > 0, r => PlayerGameEntry(r, teams, level, levelName));
            AddBest(entries, rows, "Player Game", "Plate Appearances", r => r.Line.PlateAppearances, r => r.Line.PlateAppearances.ToString(), r => r.Line.PlateAppearances > 0, r => PlayerGameEntry(r, teams, level, levelName));
            AddBest(entries, rows, "Player Game", "Extra-Base Hits", r => r.Line.ExtraBaseHits, r => r.Line.ExtraBaseHits.ToString(), r => r.Line.ExtraBaseHits > 0, r => PlayerGameEntry(r, teams, level, levelName));
            AddBest(entries, rows, "Player Game", "Reached on Error", r => r.Line.ReachedOnError, r => r.Line.ReachedOnError.ToString(), r => r.Line.ReachedOnError > 0, r => PlayerGameEntry(r, teams, level, levelName));
            AddBest(entries, rows, "Player Game", "Home Runs", r => r.Line.HR, r => r.Line.HR.ToString(), r => r.Line.HR > 0, r => PlayerGameEntry(r, teams, level, levelName));
            AddBest(entries, rows, "Player Game", "RBI", r => r.Line.RBI, r => r.Line.RBI.ToString(), r => r.Line.RBI > 0, r => PlayerGameEntry(r, teams, level, levelName));
            AddBest(entries, rows, "Player Game", "Runs", r => r.Line.R, r => r.Line.R.ToString(), r => r.Line.R > 0, r => PlayerGameEntry(r, teams, level, levelName));
            AddBest(entries, rows, "Player Game", "Stolen Bases", r => r.Line.SB, r => r.Line.SB.ToString(), r => r.Line.SB > 0, r => PlayerGameEntry(r, teams, level, levelName));
            AddBest(entries, rows, "Player Game", "Pitcher Strikeouts", r => r.Line.K, r => r.Line.K.ToString(), r => r.Line.K > 0, r => PlayerGameEntry(r, teams, level, levelName));
            AddBest(entries, rows, "Player Game", "Innings Pitched", r => r.Line.IPOuts, r => FormatInnings(r.Line.IPOuts), r => r.Line.IPOuts > 0, r => PlayerGameEntry(r, teams, level, levelName));
            AddBest(entries, rows, "Player Game", "Holds", r => r.Line.Holds, r => r.Line.Holds.ToString(), r => r.Line.Holds > 0, r => PlayerGameEntry(r, teams, level, levelName));
            AddBest(entries, rows, "Player Game", "Blown Saves", r => r.Line.BlownSaves, r => r.Line.BlownSaves.ToString(), r => r.Line.BlownSaves > 0, r => PlayerGameEntry(r, teams, level, levelName));
            AddBest(entries, rows, "Player Game", "Complete Games", r => r.Line.CompleteGames, r => r.Line.CompleteGames.ToString(), r => r.Line.CompleteGames > 0, r => PlayerGameEntry(r, teams, level, levelName));
            AddBest(entries, rows, "Player Game", "Shutouts", r => r.Line.Shutouts, r => r.Line.Shutouts.ToString(), r => r.Line.Shutouts > 0, r => PlayerGameEntry(r, teams, level, levelName));
            AddBest(entries, rows, "Player Game", "Defensive Innings", r => r.Line.DefensiveOuts, r => FormatInnings(r.Line.DefensiveOuts), r => r.Line.DefensiveOuts > 0, r => PlayerGameEntry(r, teams, level, levelName));
            AddBest(entries, rows, "Player Game", "Total Chances", r => r.Line.TotalChances, r => r.Line.TotalChances.ToString(), r => r.Line.TotalChances > 0, r => PlayerGameEntry(r, teams, level, levelName));
            AddBest(entries, rows, "Player Game", "Catcher CS%", r => r.Line.CatcherCaughtStealingPercentage, r => r.Line.CatcherCaughtStealingPercentage.ToString("0.0%"), r => r.Line.CatcherStealAttempts > 0, r => PlayerGameEntry(r, teams, level, levelName));
            AddBest(entries, rows, "Player Game", "Wild Pitches", r => r.Line.WildPitches, r => r.Line.WildPitches.ToString(), r => r.Line.WildPitches > 0, r => PlayerGameEntry(r, teams, level, levelName));
            AddBest(entries, rows, "Player Game", "Balks", r => r.Line.Balks, r => r.Line.Balks.ToString(), r => r.Line.Balks > 0, r => PlayerGameEntry(r, teams, level, levelName));
            AddBest(entries, rows, "Player Game", "Passed Balls", r => r.Line.PassedBalls, r => r.Line.PassedBalls.ToString(), r => r.Line.PassedBalls > 0, r => PlayerGameEntry(r, teams, level, levelName));
            AddBest(entries, rows, "Player Game", "Grounded Into Double Plays", r => r.Line.GroundedIntoDoublePlays, r => r.Line.GroundedIntoDoublePlays.ToString(), r => r.Line.GroundedIntoDoublePlays > 0, r => PlayerGameEntry(r, teams, level, levelName));
            AddBest(entries, rows, "Player Game", "Defensive Double Plays", r => r.Line.DefensiveDoublePlays, r => r.Line.DefensiveDoublePlays.ToString(), r => r.Line.DefensiveDoublePlays > 0, r => PlayerGameEntry(r, teams, level, levelName));
            AddBest(entries, rows, "Player Game", "Errors", r => r.Line.Errors, r => r.Line.Errors.ToString(), r => r.Line.Errors > 0, r => PlayerGameEntry(r, teams, level, levelName));
        }

        private static void AddTeamGameRecords(List<RecordsBookEntry> entries, IEnumerable<SeasonContext> seasons, Dictionary<Guid, Team> teams, HashSet<Guid> teamIds, string level, string levelName)
        {
            var rows = new List<TeamGameRecordRow>();
            foreach (var item in seasons)
            {
                foreach (var game in item.Season.Games ?? new List<GameResult>())
                {
                    if (teamIds.Contains(game.AwayTeamId))
                        rows.Add(new TeamGameRecordRow { TeamId = game.AwayTeamId, Game = game, SeasonNumber = item.Number, SeasonName = item.Season.Name, Stats = SumLines(game, game.AwayTeamId), Runs = game.AwayScore, RunsAllowed = game.HomeScore });
                    if (teamIds.Contains(game.HomeTeamId))
                        rows.Add(new TeamGameRecordRow { TeamId = game.HomeTeamId, Game = game, SeasonNumber = item.Number, SeasonName = item.Season.Name, Stats = SumLines(game, game.HomeTeamId), Runs = game.HomeScore, RunsAllowed = game.AwayScore });
                }
            }

            AddBest(entries, rows, "Team Game", "Runs", r => r.Runs, r => r.Runs.ToString(), r => r.Runs > 0, r => TeamGameEntry(r, teams, level, levelName));
            AddBest(entries, rows, "Team Game", "Hits", r => r.Stats.H, r => r.Stats.H.ToString(), r => r.Stats.H > 0, r => TeamGameEntry(r, teams, level, levelName));
            AddBest(entries, rows, "Team Game", "Plate Appearances", r => r.Stats.PlateAppearances, r => r.Stats.PlateAppearances.ToString(), r => r.Stats.PlateAppearances > 0, r => TeamGameEntry(r, teams, level, levelName));
            AddBest(entries, rows, "Team Game", "Extra-Base Hits", r => r.Stats.ExtraBaseHits, r => r.Stats.ExtraBaseHits.ToString(), r => r.Stats.ExtraBaseHits > 0, r => TeamGameEntry(r, teams, level, levelName));
            AddBest(entries, rows, "Team Game", "Home Runs", r => r.Stats.HR, r => r.Stats.HR.ToString(), r => r.Stats.HR > 0, r => TeamGameEntry(r, teams, level, levelName));
            AddBest(entries, rows, "Team Game", "Stolen Bases", r => r.Stats.SB, r => r.Stats.SB.ToString(), r => r.Stats.SB > 0, r => TeamGameEntry(r, teams, level, levelName));
            AddBest(entries, rows, "Team Game", "Pitcher Strikeouts", r => r.Stats.K, r => r.Stats.K.ToString(), r => r.Stats.K > 0, r => TeamGameEntry(r, teams, level, levelName));
            AddBest(entries, rows, "Team Game", "Holds", r => r.Stats.Holds, r => r.Stats.Holds.ToString(), r => r.Stats.Holds > 0, r => TeamGameEntry(r, teams, level, levelName));
            AddBest(entries, rows, "Team Game", "Blown Saves", r => r.Stats.BlownSaves, r => r.Stats.BlownSaves.ToString(), r => r.Stats.BlownSaves > 0, r => TeamGameEntry(r, teams, level, levelName));
            AddBest(entries, rows, "Team Game", "Complete Games", r => r.Stats.CompleteGames, r => r.Stats.CompleteGames.ToString(), r => r.Stats.CompleteGames > 0, r => TeamGameEntry(r, teams, level, levelName));
            AddBest(entries, rows, "Team Game", "Shutouts", r => r.Stats.Shutouts, r => r.Stats.Shutouts.ToString(), r => r.Stats.Shutouts > 0, r => TeamGameEntry(r, teams, level, levelName));
            AddBest(entries, rows, "Team Game", "Total Chances", r => r.Stats.TotalChances, r => r.Stats.TotalChances.ToString(), r => r.Stats.TotalChances > 0, r => TeamGameEntry(r, teams, level, levelName));
            AddBest(entries, rows, "Team Game", "Wild Pitches", r => r.Stats.WildPitches, r => r.Stats.WildPitches.ToString(), r => r.Stats.WildPitches > 0, r => TeamGameEntry(r, teams, level, levelName));
            AddBest(entries, rows, "Team Game", "Balks", r => r.Stats.Balks, r => r.Stats.Balks.ToString(), r => r.Stats.Balks > 0, r => TeamGameEntry(r, teams, level, levelName));
            AddBest(entries, rows, "Team Game", "Passed Balls", r => r.Stats.PassedBalls, r => r.Stats.PassedBalls.ToString(), r => r.Stats.PassedBalls > 0, r => TeamGameEntry(r, teams, level, levelName));
            AddBest(entries, rows, "Team Game", "Double Plays Turned", r => r.Stats.TeamDoublePlaysTurned, r => r.Stats.TeamDoublePlaysTurned.ToString(), r => r.Stats.TeamDoublePlaysTurned > 0, r => TeamGameEntry(r, teams, level, levelName));
            AddBest(entries, rows, "Team Game", "Errors", r => r.Stats.Errors, r => r.Stats.Errors.ToString(), r => r.Stats.Errors > 0, r => TeamGameEntry(r, teams, level, levelName));
        }

        private static void AddPlayerSeasonRecords(List<RecordsBookEntry> entries, IEnumerable<SeasonContext> seasons, Dictionary<Guid, Team> teams, HashSet<Guid> teamIds, string level, string levelName)
        {
            var rows = new List<PlayerAggregate>();
            foreach (var item in seasons)
                rows.AddRange(PlayerAggregates(item.Season, item.Number, teamIds, teams));
            AddPlayerAggregateRecords(entries, rows, "Player Season", level, levelName);
        }

        private static void AddPlayerCareerRecords(List<RecordsBookEntry> entries, IEnumerable<SeasonContext> seasons, Dictionary<Guid, Team> teams, HashSet<Guid> teamIds, string level, string levelName)
        {
            var rows = seasons
                .SelectMany(item => PlayerAggregates(item.Season, item.Number, teamIds, teams))
                .GroupBy(r => new { r.PlayerId, r.TeamId })
                .Select(g => MergePlayerAggregate(g, "Career", 0))
                .ToList();
            AddPlayerAggregateRecords(entries, rows, "Player Career", level, levelName);
        }

        private static void AddTeamSeasonRecords(List<RecordsBookEntry> entries, IEnumerable<SeasonContext> seasons, Dictionary<Guid, Team> teams, HashSet<Guid> teamIds, string level, string levelName)
        {
            var rows = seasons.SelectMany(item => TeamAggregates(item.Season, item.Number, teamIds, teams)).ToList();
            AddTeamAggregateRecords(entries, rows, "Team Season", level, levelName);
        }

        private static void AddTeamCareerRecords(List<RecordsBookEntry> entries, IEnumerable<SeasonContext> seasons, Dictionary<Guid, Team> teams, HashSet<Guid> teamIds, string level, string levelName)
        {
            var rows = seasons
                .SelectMany(item => TeamAggregates(item.Season, item.Number, teamIds, teams))
                .GroupBy(r => r.TeamId)
                .Select(g => MergeTeamAggregate(g, "Career", 0))
                .ToList();
            AddTeamAggregateRecords(entries, rows, "Team Career", level, levelName);
        }

        private static void AddPlayerAggregateRecords(List<RecordsBookEntry> entries, List<PlayerAggregate> rows, string category, string level, string levelName)
        {
            AddBest(entries, rows, category, "Hits", r => r.Stats.H, r => r.Stats.H.ToString(), r => r.Stats.H > 0, r => PlayerAggregateEntry(r, level, levelName));
            AddBest(entries, rows, category, "Plate Appearances", r => r.Stats.PlateAppearances, r => r.Stats.PlateAppearances.ToString(), r => r.Stats.PlateAppearances > 0, r => PlayerAggregateEntry(r, level, levelName));
            AddBest(entries, rows, category, "Extra-Base Hits", r => r.Stats.ExtraBaseHits, r => r.Stats.ExtraBaseHits.ToString(), r => r.Stats.ExtraBaseHits > 0, r => PlayerAggregateEntry(r, level, levelName));
            AddBest(entries, rows, category, "Reached on Error", r => r.Stats.ReachedOnError, r => r.Stats.ReachedOnError.ToString(), r => r.Stats.ReachedOnError > 0, r => PlayerAggregateEntry(r, level, levelName));
            AddBest(entries, rows, category, "Home Runs", r => r.Stats.HR, r => r.Stats.HR.ToString(), r => r.Stats.HR > 0, r => PlayerAggregateEntry(r, level, levelName));
            AddBest(entries, rows, category, "RBI", r => r.Stats.RBI, r => r.Stats.RBI.ToString(), r => r.Stats.RBI > 0, r => PlayerAggregateEntry(r, level, levelName));
            AddBest(entries, rows, category, "Runs", r => r.Stats.R, r => r.Stats.R.ToString(), r => r.Stats.R > 0, r => PlayerAggregateEntry(r, level, levelName));
            AddBest(entries, rows, category, "Stolen Bases", r => r.Stats.SB, r => r.Stats.SB.ToString(), r => r.Stats.SB > 0, r => PlayerAggregateEntry(r, level, levelName));
            AddBest(entries, rows, category, "AVG", r => Average(r.Stats.H, r.Stats.AB), r => Average(r.Stats.H, r.Stats.AB).ToString("0.000"), r => r.Stats.AB >= 20, r => PlayerAggregateEntry(r, level, levelName));
            AddBest(entries, rows, category, "OPS", r => Ops(r.Stats), r => Ops(r.Stats).ToString("0.000"), r => r.Stats.AB >= 20, r => PlayerAggregateEntry(r, level, levelName));
            AddBest(entries, rows, category, "Pitching Wins", r => r.Stats.Wins, r => r.Stats.Wins.ToString(), r => r.Stats.Wins > 0, r => PlayerAggregateEntry(r, level, levelName));
            AddBest(entries, rows, category, "Saves", r => r.Stats.Saves, r => r.Stats.Saves.ToString(), r => r.Stats.Saves > 0, r => PlayerAggregateEntry(r, level, levelName));
            AddBest(entries, rows, category, "Holds", r => r.Stats.Holds, r => r.Stats.Holds.ToString(), r => r.Stats.Holds > 0, r => PlayerAggregateEntry(r, level, levelName));
            AddBest(entries, rows, category, "Blown Saves", r => r.Stats.BlownSaves, r => r.Stats.BlownSaves.ToString(), r => r.Stats.BlownSaves > 0, r => PlayerAggregateEntry(r, level, levelName));
            AddBest(entries, rows, category, "Complete Games", r => r.Stats.CompleteGames, r => r.Stats.CompleteGames.ToString(), r => r.Stats.CompleteGames > 0, r => PlayerAggregateEntry(r, level, levelName));
            AddBest(entries, rows, category, "Shutouts", r => r.Stats.Shutouts, r => r.Stats.Shutouts.ToString(), r => r.Stats.Shutouts > 0, r => PlayerAggregateEntry(r, level, levelName));
            AddBest(entries, rows, category, "Strikeouts", r => r.Stats.K, r => r.Stats.K.ToString(), r => r.Stats.K > 0, r => PlayerAggregateEntry(r, level, levelName));
            AddBest(entries, rows, category, "ERA", r => Era(r.Stats.ER, r.Stats.IPOuts), r => Era(r.Stats.ER, r.Stats.IPOuts).ToString("0.00"), r => r.Stats.IPOuts >= 15, r => PlayerAggregateEntry(r, level, levelName), lowerIsBetter: true);
            AddBest(entries, rows, category, "WHIP", r => Whip(r.Stats.WalksAllowed, r.Stats.HitsAllowed, r.Stats.IPOuts), r => Whip(r.Stats.WalksAllowed, r.Stats.HitsAllowed, r.Stats.IPOuts).ToString("0.00"), r => r.Stats.IPOuts >= 15, r => PlayerAggregateEntry(r, level, levelName), lowerIsBetter: true);
            AddBest(entries, rows, category, "Wild Pitches", r => r.Stats.WildPitches, r => r.Stats.WildPitches.ToString(), r => r.Stats.WildPitches > 0, r => PlayerAggregateEntry(r, level, levelName));
            AddBest(entries, rows, category, "Balks", r => r.Stats.Balks, r => r.Stats.Balks.ToString(), r => r.Stats.Balks > 0, r => PlayerAggregateEntry(r, level, levelName));
            AddBest(entries, rows, category, "Passed Balls", r => r.Stats.PassedBalls, r => r.Stats.PassedBalls.ToString(), r => r.Stats.PassedBalls > 0, r => PlayerAggregateEntry(r, level, levelName));
            AddBest(entries, rows, category, "Grounded Into Double Plays", r => r.Stats.GroundedIntoDoublePlays, r => r.Stats.GroundedIntoDoublePlays.ToString(), r => r.Stats.GroundedIntoDoublePlays > 0, r => PlayerAggregateEntry(r, level, levelName));
            AddBest(entries, rows, category, "Defensive Double Plays", r => r.Stats.DefensiveDoublePlays, r => r.Stats.DefensiveDoublePlays.ToString(), r => r.Stats.DefensiveDoublePlays > 0, r => PlayerAggregateEntry(r, level, levelName));
            AddBest(entries, rows, category, "Defensive Innings", r => r.Stats.DefensiveOuts, r => FormatInnings(r.Stats.DefensiveOuts), r => r.Stats.DefensiveOuts > 0, r => PlayerAggregateEntry(r, level, levelName));
            AddBest(entries, rows, category, "Total Chances", r => r.Stats.TotalChances, r => r.Stats.TotalChances.ToString(), r => r.Stats.TotalChances > 0, r => PlayerAggregateEntry(r, level, levelName));
            AddBest(entries, rows, category, "Catcher CS%", r => r.Stats.CatcherCaughtStealingPercentage, r => r.Stats.CatcherCaughtStealingPercentage.ToString("0.0%"), r => r.Stats.CatcherStealAttempts >= 20, r => PlayerAggregateEntry(r, level, levelName));
            AddBest(entries, rows, category, "Errors", r => r.Stats.Errors, r => r.Stats.Errors.ToString(), r => r.Stats.Errors > 0, r => PlayerAggregateEntry(r, level, levelName));
            AddBest(entries, rows, category, "Injury Games Missed", r => r.Stats.GamesMissedInjury, r => r.Stats.GamesMissedInjury.ToString(), r => r.Stats.GamesMissedInjury > 0, r => PlayerAggregateEntry(r, level, levelName));
        }

        private static void AddTeamAggregateRecords(List<RecordsBookEntry> entries, List<TeamAggregate> rows, string category, string level, string levelName)
        {
            AddBest(entries, rows, category, "Wins", r => r.Wins, r => r.Wins.ToString(), r => r.Wins > 0, r => TeamAggregateEntry(r, level, levelName));
            AddBest(entries, rows, category, "Runs Scored", r => r.Runs, r => r.Runs.ToString(), r => r.Runs > 0, r => TeamAggregateEntry(r, level, levelName));
            AddBest(entries, rows, category, "Run Differential", r => r.Runs - r.RunsAllowed, r => (r.Runs - r.RunsAllowed).ToString(), r => r.Wins + r.Losses > 0, r => TeamAggregateEntry(r, level, levelName));
            AddBest(entries, rows, category, "Hits", r => r.Stats.H, r => r.Stats.H.ToString(), r => r.Stats.H > 0, r => TeamAggregateEntry(r, level, levelName));
            AddBest(entries, rows, category, "Plate Appearances", r => r.Stats.PlateAppearances, r => r.Stats.PlateAppearances.ToString(), r => r.Stats.PlateAppearances > 0, r => TeamAggregateEntry(r, level, levelName));
            AddBest(entries, rows, category, "Extra-Base Hits", r => r.Stats.ExtraBaseHits, r => r.Stats.ExtraBaseHits.ToString(), r => r.Stats.ExtraBaseHits > 0, r => TeamAggregateEntry(r, level, levelName));
            AddBest(entries, rows, category, "Home Runs", r => r.Stats.HR, r => r.Stats.HR.ToString(), r => r.Stats.HR > 0, r => TeamAggregateEntry(r, level, levelName));
            AddBest(entries, rows, category, "Stolen Bases", r => r.Stats.SB, r => r.Stats.SB.ToString(), r => r.Stats.SB > 0, r => TeamAggregateEntry(r, level, levelName));
            AddBest(entries, rows, category, "Pitcher Strikeouts", r => r.Stats.K, r => r.Stats.K.ToString(), r => r.Stats.K > 0, r => TeamAggregateEntry(r, level, levelName));
            AddBest(entries, rows, category, "Holds", r => r.Stats.Holds, r => r.Stats.Holds.ToString(), r => r.Stats.Holds > 0, r => TeamAggregateEntry(r, level, levelName));
            AddBest(entries, rows, category, "Blown Saves", r => r.Stats.BlownSaves, r => r.Stats.BlownSaves.ToString(), r => r.Stats.BlownSaves > 0, r => TeamAggregateEntry(r, level, levelName));
            AddBest(entries, rows, category, "Complete Games", r => r.Stats.CompleteGames, r => r.Stats.CompleteGames.ToString(), r => r.Stats.CompleteGames > 0, r => TeamAggregateEntry(r, level, levelName));
            AddBest(entries, rows, category, "Shutouts", r => r.Stats.Shutouts, r => r.Stats.Shutouts.ToString(), r => r.Stats.Shutouts > 0, r => TeamAggregateEntry(r, level, levelName));
            AddBest(entries, rows, category, "ERA", r => Era(r.Stats.ER, r.Stats.IPOuts), r => Era(r.Stats.ER, r.Stats.IPOuts).ToString("0.00"), r => r.Stats.IPOuts >= 15, r => TeamAggregateEntry(r, level, levelName), lowerIsBetter: true);
            AddBest(entries, rows, category, "WHIP", r => Whip(r.Stats.WalksAllowed, r.Stats.HitsAllowed, r.Stats.IPOuts), r => Whip(r.Stats.WalksAllowed, r.Stats.HitsAllowed, r.Stats.IPOuts).ToString("0.00"), r => r.Stats.IPOuts >= 15, r => TeamAggregateEntry(r, level, levelName), lowerIsBetter: true);
            AddBest(entries, rows, category, "Wild Pitches", r => r.Stats.WildPitches, r => r.Stats.WildPitches.ToString(), r => r.Stats.WildPitches > 0, r => TeamAggregateEntry(r, level, levelName));
            AddBest(entries, rows, category, "Balks", r => r.Stats.Balks, r => r.Stats.Balks.ToString(), r => r.Stats.Balks > 0, r => TeamAggregateEntry(r, level, levelName));
            AddBest(entries, rows, category, "Passed Balls", r => r.Stats.PassedBalls, r => r.Stats.PassedBalls.ToString(), r => r.Stats.PassedBalls > 0, r => TeamAggregateEntry(r, level, levelName));
            AddBest(entries, rows, category, "Double Plays Turned", r => r.Stats.TeamDoublePlaysTurned, r => r.Stats.TeamDoublePlaysTurned.ToString(), r => r.Stats.TeamDoublePlaysTurned > 0, r => TeamAggregateEntry(r, level, levelName));
            AddBest(entries, rows, category, "Defensive Innings", r => r.Stats.DefensiveOuts, r => FormatInnings(r.Stats.DefensiveOuts), r => r.Stats.DefensiveOuts > 0, r => TeamAggregateEntry(r, level, levelName));
            AddBest(entries, rows, category, "Total Chances", r => r.Stats.TotalChances, r => r.Stats.TotalChances.ToString(), r => r.Stats.TotalChances > 0, r => TeamAggregateEntry(r, level, levelName));
            AddBest(entries, rows, category, "Catcher CS%", r => r.Stats.CatcherCaughtStealingPercentage, r => r.Stats.CatcherCaughtStealingPercentage.ToString("0.0%"), r => r.Stats.CatcherStealAttempts >= 20, r => TeamAggregateEntry(r, level, levelName));
            AddBest(entries, rows, category, "Errors", r => r.Stats.Errors, r => r.Stats.Errors.ToString(), r => r.Stats.Errors > 0, r => TeamAggregateEntry(r, level, levelName));
            AddBest(entries, rows, category, "Injury Games Missed", r => r.Stats.GamesMissedInjury, r => r.Stats.GamesMissedInjury.ToString(), r => r.Stats.GamesMissedInjury > 0, r => TeamAggregateEntry(r, level, levelName));
        }

        private static void AddBest<T>(List<RecordsBookEntry> entries, IEnumerable<T> rows, string category, string record, Func<T, double> rank, Func<T, string> value, Func<T, bool> qualifies, Func<T, RecordsBookEntry> entryFactory, bool lowerIsBetter = false)
        {
            var qualified = rows.Where(qualifies).ToList();
            if (qualified.Count == 0)
                return;
            double best = lowerIsBetter ? qualified.Min(rank) : qualified.Max(rank);
            foreach (var row in qualified.Where(r => Math.Abs(rank(r) - best) < 0.0001))
            {
                var entry = entryFactory(row);
                entry.Category = category;
                entry.Record = record;
                entry.Value = value(row);
                entry.SortValue = best;
                entries.Add(entry);
            }
        }

        private static RecordsBookEntry PlayerGameEntry(PlayerGameRecordRow row, Dictionary<Guid, Team> teams, string level, string levelName)
        {
            Guid teamId = row.Line.TeamId;
            Guid opponentId = row.Game.AwayTeamId == teamId ? row.Game.HomeTeamId : row.Game.AwayTeamId;
            return new RecordsBookEntry
            {
                Level = level,
                LevelName = levelName,
                Scope = "Game",
                Holder = row.Line.PlayerName,
                Team = TeamName(teams, teamId),
                SeasonNumber = row.SeasonNumber,
                SeasonName = row.SeasonName,
                Opponent = TeamName(teams, opponentId),
                Date = row.Game.PlayedAt,
                Game = ScoreLine(row.Game, teams),
                Detail = row.Game.IsPlayoff ? row.Game.PlayoffRoundName : "Regular season"
            };
        }

        private static RecordsBookEntry TeamGameEntry(TeamGameRecordRow row, Dictionary<Guid, Team> teams, string level, string levelName)
        {
            Guid opponentId = row.Game.AwayTeamId == row.TeamId ? row.Game.HomeTeamId : row.Game.AwayTeamId;
            return new RecordsBookEntry
            {
                Level = level,
                LevelName = levelName,
                Scope = "Game",
                Holder = TeamName(teams, row.TeamId),
                Team = TeamName(teams, row.TeamId),
                SeasonNumber = row.SeasonNumber,
                SeasonName = row.SeasonName,
                Opponent = TeamName(teams, opponentId),
                Date = row.Game.PlayedAt,
                Game = ScoreLine(row.Game, teams),
                Detail = row.Game.IsPlayoff ? row.Game.PlayoffRoundName : "Regular season"
            };
        }

        private static RecordsBookEntry PlayerAggregateEntry(PlayerAggregate row, string level, string levelName)
            => new RecordsBookEntry
            {
                Level = level,
                LevelName = levelName,
                Scope = row.SeasonNumber <= 0 ? "Career" : "Season",
                Holder = row.PlayerName,
                Team = row.TeamName,
                SeasonNumber = row.SeasonNumber,
                SeasonName = row.SeasonName,
                Detail = row.SeasonNumber <= 0 ? "Career total" : "Season total"
            };

        private static RecordsBookEntry TeamAggregateEntry(TeamAggregate row, string level, string levelName)
            => new RecordsBookEntry
            {
                Level = level,
                LevelName = levelName,
                Scope = row.SeasonNumber <= 0 ? "Career" : "Season",
                Holder = row.TeamName,
                Team = row.TeamName,
                SeasonNumber = row.SeasonNumber,
                SeasonName = row.SeasonName,
                Detail = row.SeasonNumber <= 0 ? "Team career total" : row.Wins + "-" + row.Losses
            };

        private static IEnumerable<PlayerAggregate> PlayerAggregates(Season season, int seasonNumber, HashSet<Guid> teamIds, Dictionary<Guid, Team> teams)
        {
            return (season.Games ?? new List<GameResult>())
                .SelectMany(g => g.Lines ?? new List<PlayerGameLine>())
                .Where(l => teamIds.Contains(l.TeamId))
                .GroupBy(l => new { l.PlayerId, l.TeamId })
                .Select(g => MergePlayerAggregate(g.Select(l => new PlayerAggregate
                {
                    PlayerId = l.PlayerId,
                    TeamId = l.TeamId,
                    PlayerName = l.PlayerName,
                    TeamName = TeamName(teams, l.TeamId),
                    SeasonNumber = seasonNumber,
                    SeasonName = season.Name,
                    Stats = CopyLine(l)
                }), season.Name, seasonNumber));
        }

        private static IEnumerable<TeamAggregate> TeamAggregates(Season season, int seasonNumber, HashSet<Guid> teamIds, Dictionary<Guid, Team> teams)
        {
            foreach (var teamId in teamIds)
            {
                var aggregate = new TeamAggregate
                {
                    TeamId = teamId,
                    TeamName = TeamName(teams, teamId),
                    SeasonNumber = seasonNumber,
                    SeasonName = season.Name
                };
                foreach (var game in season.Games ?? new List<GameResult>())
                {
                    if (game.AwayTeamId != teamId && game.HomeTeamId != teamId)
                        continue;
                    bool away = game.AwayTeamId == teamId;
                    int runs = away ? game.AwayScore : game.HomeScore;
                    int allowed = away ? game.HomeScore : game.AwayScore;
                    if (runs > allowed) aggregate.Wins++;
                    else if (allowed > runs) aggregate.Losses++;
                    aggregate.Runs += runs;
                    aggregate.RunsAllowed += allowed;
                    Accumulate(aggregate.Stats, SumLines(game, teamId));
                }
                if (aggregate.Wins + aggregate.Losses > 0)
                    yield return aggregate;
            }
        }

        private static PlayerAggregate MergePlayerAggregate(IEnumerable<PlayerAggregate> rows, string seasonName, int seasonNumber)
        {
            var list = rows.ToList();
            var first = list.FirstOrDefault() ?? new PlayerAggregate();
            var merged = new PlayerAggregate
            {
                PlayerId = first.PlayerId,
                TeamId = first.TeamId,
                PlayerName = first.PlayerName,
                TeamName = first.TeamName,
                SeasonName = seasonName,
                SeasonNumber = seasonNumber
            };
            foreach (var row in list)
                Accumulate(merged.Stats, row.Stats);
            return merged;
        }

        private static TeamAggregate MergeTeamAggregate(IEnumerable<TeamAggregate> rows, string seasonName, int seasonNumber)
        {
            var list = rows.ToList();
            var first = list.FirstOrDefault() ?? new TeamAggregate();
            var merged = new TeamAggregate
            {
                TeamId = first.TeamId,
                TeamName = first.TeamName,
                SeasonName = seasonName,
                SeasonNumber = seasonNumber,
                Wins = list.Sum(r => r.Wins),
                Losses = list.Sum(r => r.Losses),
                Runs = list.Sum(r => r.Runs),
                RunsAllowed = list.Sum(r => r.RunsAllowed)
            };
            foreach (var row in list)
                Accumulate(merged.Stats, row.Stats);
            return merged;
        }

        private static PlayerGameLine SumLines(GameResult game, Guid teamId)
        {
            var sum = new PlayerGameLine { TeamId = teamId };
            foreach (var line in (game.Lines ?? new List<PlayerGameLine>()).Where(l => l.TeamId == teamId))
                Accumulate(sum, line);
            return sum;
        }

        private static PlayerGameLine CopyLine(PlayerGameLine line)
        {
            var copy = new PlayerGameLine { TeamId = line.TeamId, PlayerId = line.PlayerId, PlayerName = line.PlayerName };
            Accumulate(copy, line);
            return copy;
        }

        private static void Accumulate(PlayerGameLine target, PlayerGameLine source)
        {
            if (target == null || source == null) return;
            target.R += source.R; target.AB += source.AB; target.H += source.H; target.Doubles += source.Doubles;
            target.Triples += source.Triples; target.HR += source.HR; target.RBI += source.RBI; target.BB += source.BB;
            target.IBB += source.IBB; target.SO += source.SO; target.SB += source.SB; target.CS += source.CS;
            target.HBP += source.HBP; target.SH += source.SH; target.SF += source.SF; target.FlyOuts += source.FlyOuts;
            target.GroundOuts += source.GroundOuts; target.PopOuts += source.PopOuts;
            target.GroundedIntoDoublePlays += source.GroundedIntoDoublePlays; target.ReachedOnError += source.ReachedOnError;
            target.IPOuts += source.IPOuts; target.ER += source.ER; target.RunsAllowed += source.RunsAllowed;
            target.K += source.K; target.HitsAllowed += source.HitsAllowed;
            target.DoublesAllowed += source.DoublesAllowed; target.TriplesAllowed += source.TriplesAllowed;
            target.WalksAllowed += source.WalksAllowed; target.IntentionalWalksAllowed += source.IntentionalWalksAllowed;
            target.HomeRunsAllowed += source.HomeRunsAllowed; target.HitBatters += source.HitBatters; target.WildPitches += source.WildPitches;
            target.Balks += source.Balks;
            target.BattersFaced += source.BattersFaced; target.PitchCount += source.PitchCount;
            target.Wins += source.Wins; target.Losses += source.Losses; target.Saves += source.Saves;
            target.Holds += source.Holds; target.BlownSaves += source.BlownSaves;
            target.CompleteGames += source.CompleteGames; target.Shutouts += source.Shutouts;
            target.Putouts += source.Putouts; target.Assists += source.Assists; target.Errors += source.Errors;
            target.DefensiveOuts += source.DefensiveOuts;
            target.DefensiveDoublePlays += source.DefensiveDoublePlays; target.TeamDoublePlaysTurned += source.TeamDoublePlaysTurned;
            target.PassedBalls += source.PassedBalls; target.StolenBasesAllowed += source.StolenBasesAllowed;
            target.CatcherCaughtStealing += source.CatcherCaughtStealing; target.GamesMissedInjury += source.GamesMissedInjury;
        }

        private static HashSet<Guid> TeamIdsForLevel(LeagueFile league, Dictionary<Guid, Placement> placements, string level, Guid? entityId)
        {
            if (level == "League" || !entityId.HasValue)
                return new HashSet<Guid>((league.Teams ?? new List<Team>()).Select(t => t.Id));
            if (level == "Team")
                return new HashSet<Guid> { entityId.Value };
            return new HashSet<Guid>(placements
                .Where(kv => level == "Conference" && kv.Value.Conference?.Id == entityId.Value ||
                    level == "Region" && kv.Value.Region?.Id == entityId.Value ||
                    level == "District" && kv.Value.District?.Id == entityId.Value)
                .Select(kv => kv.Key));
        }

        private static Dictionary<Guid, Placement> BuildPlacements(LeagueFile league)
        {
            var result = new Dictionary<Guid, Placement>();
            foreach (var conference in league.Structure?.Conferences ?? new List<Conference>())
            foreach (var region in conference.Regions ?? new List<Region>())
            foreach (var district in region.Districts ?? new List<District>())
            foreach (var teamId in district.TeamIds ?? new List<Guid>())
                result[teamId] = new Placement { Conference = conference, Region = region, District = district };
            return result;
        }

        private static string LevelName(LeagueFile league, string level, Guid? entityId)
            => EntitiesForLevel(league, level).FirstOrDefault(e => e.Id == entityId).Name ?? league?.Name ?? "League";

        private static string TeamName(Dictionary<Guid, Team> teams, Guid id)
            => teams.TryGetValue(id, out var team) ? team.DisplayName : "";

        private static string ScoreLine(GameResult game, Dictionary<Guid, Team> teams)
            => TeamName(teams, game.AwayTeamId) + " " + game.AwayScore + " at " + TeamName(teams, game.HomeTeamId) + " " + game.HomeScore;

        private static int TotalBases(PlayerGameLine s) => s.H + s.Doubles + s.Triples * 2 + s.HR * 3;
        private static double Average(int h, int ab) => ab <= 0 ? 0.0 : h / (double)ab;
        private static double Ops(PlayerGameLine s) => Obp(s) + Slg(s);
        private static double Obp(PlayerGameLine s) => s.AB + s.BB + s.HBP + s.SF <= 0 ? 0.0 : (s.H + s.BB + s.HBP) / (double)(s.AB + s.BB + s.HBP + s.SF);
        private static double Slg(PlayerGameLine s) => s.AB <= 0 ? 0.0 : TotalBases(s) / (double)s.AB;
        private static double Era(int er, int outs) => outs <= 0 ? 0.0 : er * 27.0 / outs;
        private static double Whip(int walks, int hits, int outs) => outs <= 0 ? 0.0 : (walks + hits) / (outs / 3.0);
        private static string FormatInnings(int outs) => outs <= 0 ? "0.0" : (outs / 3) + "." + (outs % 3);
        private static int ScopeOrder(string scope) => scope == "Game" ? 0 : scope == "Season" ? 1 : 2;
    }
}
