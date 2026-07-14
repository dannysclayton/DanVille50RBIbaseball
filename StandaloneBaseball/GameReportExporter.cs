using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace StandaloneBaseball
{
    internal static class GameReportExporter
    {
        public static void WriteDocx(string path, LeagueFile league, Season season, IEnumerable<GameResult> games)
        {
            var selected = (games ?? Enumerable.Empty<GameResult>()).Where(game => game != null).ToList();
            if (selected.Count == 0)
                throw new InvalidOperationException("At least one completed game is required.");

            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);
            if (File.Exists(path))
                File.Delete(path);

            var sections = selected.SelectMany(game => BuildSections(league, season, game)).ToList();
            NativeDocumentExporter.WriteDocx(path, SeasonLabel(league, season) + " Game Results", sections);
        }

        public static List<ExportSection> BuildSections(LeagueFile league, Season season, GameResult game)
        {
            Team? away = league.Teams?.FirstOrDefault(team => team.Id == game.AwayTeamId);
            Team? home = league.Teams?.FirstOrDefault(team => team.Id == game.HomeTeamId);
            string awayName = away?.DisplayName ?? "Away Team";
            string homeName = home?.DisplayName ?? "Home Team";
            string gameTitle = awayName + " at " + homeName + " - " + game.PlayedAt.ToString("MMMM d, yyyy", CultureInfo.InvariantCulture);
            var sections = new List<ExportSection>
            {
                SummarySection(league, season, game, awayName, homeName, gameTitle),
                LineScoreSection(game, awayName, homeName),
                DecisionsSection(game),
                PlayerOfGameSection(game, league),
                BattingSection(game, away, game.AwayStartingLineup, gameTitle + " - " + awayName + " Batting"),
                BattingSection(game, home, game.HomeStartingLineup, gameTitle + " - " + homeName + " Batting"),
                PitchingSection(game, away, gameTitle + " - " + awayName + " Pitching"),
                PitchingSection(game, home, gameTitle + " - " + homeName + " Pitching"),
                FieldingSection(game, away, game.AwayStartingLineup, gameTitle + " - " + awayName + " Fielding"),
                FieldingSection(game, home, game.HomeStartingLineup, gameTitle + " - " + homeName + " Fielding"),
                PlayByPlaySection(game, gameTitle + " - Play-by-Play")
            };
            return sections;
        }

        private static ExportSection SummarySection(LeagueFile league, Season season, GameResult game, string awayName, string homeName, string gameTitle)
        {
            string gameType = !string.IsNullOrWhiteSpace(game.PlayoffRoundName) ? game.PlayoffRoundName : game.GameType;
            return new ExportSection
            {
                Title = gameTitle,
                Headers = new List<string> { "Item", "Details" },
                Rows = new List<List<string>>
                {
                    Row("Final", awayName + " " + game.AwayScore + ", " + homeName + " " + game.HomeScore),
                    Row("Updated Records", awayName + " " + Record(season, game, game.AwayTeamId) + " | " + homeName + " " + Record(season, game, game.HomeTeamId)),
                    Row("Season", SeasonLabel(league, season)),
                    Row("Game", string.IsNullOrWhiteSpace(gameType) ? "Scheduled Game" : gameType),
                    Row("Mode", game.GameMode),
                    Row("Stadium", game.StadiumName),
                    Row("Length", Math.Max(game.GameLengthInnings, Math.Max(game.AwayRunsByInning?.Count ?? 0, game.HomeRunsByInning?.Count ?? 0)) + " innings"),
                    Row("Rules", RulesText(game))
                }
            };
        }

        private static ExportSection LineScoreSection(GameResult game, string awayName, string homeName)
        {
            int innings = Math.Max(game.AwayRunsByInning?.Count ?? 0, game.HomeRunsByInning?.Count ?? 0);
            innings = Math.Max(innings, game.GameLengthInnings);
            var headers = new List<string> { "Team" };
            headers.AddRange(Enumerable.Range(1, Math.Max(1, innings)).Select(number => number.ToString(CultureInfo.InvariantCulture)));
            headers.AddRange(new[] { "R", "H", "E", "LOB" });
            return new ExportSection
            {
                Title = "Box Score",
                Headers = headers,
                Rows = new List<List<string>>
                {
                    LineScoreRow(awayName, game.AwayRunsByInning, innings, game.AwayScore, game.AwayHits, game.AwayErrors, game.AwayLeftOnBase),
                    LineScoreRow(homeName, game.HomeRunsByInning, innings, game.HomeScore, game.HomeHits, game.HomeErrors, game.HomeLeftOnBase)
                }
            };
        }

        private static ExportSection DecisionsSection(GameResult game)
        {
            string holds = string.Join(", ", (game.Lines ?? new List<PlayerGameLine>())
                .Where(line => line.Holds > 0)
                .Select(line => line.PlayerName + (line.Holds > 1 ? " (" + line.Holds + ")" : "")));
            string blown = string.Join(", ", (game.Lines ?? new List<PlayerGameLine>())
                .Where(line => line.BlownSaves > 0)
                .Select(line => line.PlayerName));
            return new ExportSection
            {
                Title = "Pitcher Decisions",
                Headers = new List<string> { "Decision", "Pitcher" },
                Rows = new List<List<string>>
                {
                    Row("Winning Pitcher", ValueOrNone(game.WinningPitcherName)),
                    Row("Losing Pitcher", ValueOrNone(game.LosingPitcherName)),
                    Row("Save", ValueOrNone(game.SavePitcherName)),
                    Row("Holds", ValueOrNone(holds)),
                    Row("Blown Saves", ValueOrNone(blown))
                }
            };
        }

        private static ExportSection PlayerOfGameSection(GameResult game, LeagueFile league)
        {
            PlayerGameLine? line = PlayerOfGame(game);
            Team? team = line == null ? null : league?.Teams?.FirstOrDefault(item => item.Id == line.TeamId);
            return new ExportSection
            {
                Title = "Player of the Game",
                Headers = new List<string> { "Player", "Team", "Performance" },
                Rows = new List<List<string>>
                {
                    new List<string>
                    {
                        line?.PlayerName ?? "Not available",
                        team?.DisplayName ?? "",
                        line == null ? "" : Performance(line)
                    }
                }
            };
        }

        private static ExportSection BattingSection(GameResult game, Team? team, IEnumerable<GameLineupEntry>? lineup, string title)
        {
            var positions = PositionMap(lineup);
            var lines = TeamLines(game, team).Where(HasBattingLine).OrderBy(line => OrderOf(line, lineup)).ThenBy(line => line.PlayerName);
            return new ExportSection
            {
                Title = title,
                Headers = new List<string> { "Player", "POS", "PA", "AB", "R", "H", "2B", "3B", "HR", "RBI", "BB", "IBB", "HBP", "SO", "SB", "CS", "SH", "SF", "GIDP", "ROE" },
                Rows = lines.Select(line => new List<string>
                {
                    line.PlayerName, PositionOf(line, positions), N(line.PlateAppearances), N(line.AB), N(line.R), N(line.H), N(line.Doubles), N(line.Triples),
                    N(line.HR), N(line.RBI), N(line.BB), N(line.IBB), N(line.HBP), N(line.SO), N(line.SB), N(line.CS), N(line.SH), N(line.SF),
                    N(line.GroundedIntoDoublePlays), N(line.ReachedOnError)
                }).ToList()
            };
        }

        private static ExportSection PitchingSection(GameResult game, Team? team, string title)
        {
            var lines = TeamLines(game, team).Where(HasPitchingLine).OrderByDescending(line => line.StartingPitcher).ThenByDescending(line => line.IPOuts);
            return new ExportSection
            {
                Title = title,
                Headers = new List<string> { "Pitcher", "IP", "H", "R", "ER", "2B", "3B", "HR", "BB", "IBB", "K", "HBP", "WP", "BK", "BF", "PC", "W", "L", "S", "HLD", "BS", "CG", "SHO" },
                Rows = lines.Select(line => new List<string>
                {
                    line.PlayerName, Innings(line.IPOuts), N(line.HitsAllowed), N(line.RunsAllowed), N(line.ER), N(line.DoublesAllowed), N(line.TriplesAllowed),
                    N(line.HomeRunsAllowed), N(line.WalksAllowed), N(line.IntentionalWalksAllowed), N(line.K), N(line.HitBatters), N(line.WildPitches), N(line.Balks),
                    N(line.BattersFaced), N(line.PitchCount), N(line.Wins), N(line.Losses), N(line.Saves), N(line.Holds), N(line.BlownSaves), N(line.CompleteGames), N(line.Shutouts)
                }).ToList()
            };
        }

        private static ExportSection FieldingSection(GameResult game, Team? team, IEnumerable<GameLineupEntry>? lineup, string title)
        {
            var positions = PositionMap(lineup);
            var lines = TeamLines(game, team).Where(HasFieldingLine).OrderBy(line => OrderOf(line, lineup)).ThenBy(line => line.PlayerName);
            return new ExportSection
            {
                Title = title,
                Headers = new List<string> { "Player", "POS", "INN", "PO", "A", "E", "TC", "DP", "PB", "SB Allowed", "CS", "CS%", "Injury Games Missed" },
                Rows = lines.Select(line => new List<string>
                {
                    line.PlayerName, PositionOf(line, positions), Innings(line.DefensiveOuts), N(line.Putouts), N(line.Assists), N(line.Errors), N(line.TotalChances),
                    N(line.DefensiveDoublePlays), N(line.PassedBalls), N(line.StolenBasesAllowed), N(line.CatcherCaughtStealing),
                    line.CatcherStealAttempts == 0 ? "-" : line.CatcherCaughtStealingPercentage.ToString("P1", CultureInfo.InvariantCulture), N(line.GamesMissedInjury)
                }).ToList()
            };
        }

        private static ExportSection PlayByPlaySection(GameResult game, string title)
        {
            var rows = new List<List<string>>();
            foreach (var group in (game.PlayByPlay ?? new List<GamePlayByPlayEntry>())
                .OrderBy(play => play.Sequence)
                .GroupBy(play => new { play.Inning, play.Half }))
            {
                int plateAppearance = 0;
                foreach (var play in group)
                {
                    if (LooksLikePlateAppearance(play.Description))
                        plateAppearance++;
                    string eventLabel = plateAppearance > 0 && LooksLikePlateAppearance(play.Description)
                        ? OrdinalWord(plateAppearance) + " Batter"
                        : "Event";
                    rows.Add(new List<string>
                    {
                        (group.Key.Half == HalfInning.Top ? "Top" : "Bottom") + " of " + group.Key.Inning,
                        eventLabel,
                        play.Description,
                        play.Outs + (play.Outs == 1 ? " out" : " outs"),
                        play.AwayScore + "-" + play.HomeScore,
                        play.Bases
                    });
                }
            }
            if (rows.Count == 0)
                rows.Add(new List<string> { "", "", "No play-by-play was stored for this game.", "", "", "" });
            return new ExportSection
            {
                Title = title,
                Headers = new List<string> { "Inning", "Sequence", "Play", "Outs", "Score (A-H)", "Bases" },
                Rows = rows
            };
        }

        private static IEnumerable<PlayerGameLine> TeamLines(GameResult game, Team? team)
            => (game.Lines ?? new List<PlayerGameLine>()).Where(line => team != null && line.TeamId == team.Id);

        private static Dictionary<Guid, string> PositionMap(IEnumerable<GameLineupEntry>? lineup)
            => (lineup ?? Enumerable.Empty<GameLineupEntry>()).Where(entry => entry != null)
                .GroupBy(entry => entry.PlayerId).ToDictionary(group => group.Key, group => group.First().DesignatedHitter ? "DH" : group.First().DefensivePosition);

        private static string PositionOf(PlayerGameLine line, Dictionary<Guid, string> positions)
            => positions.TryGetValue(line.PlayerId, out string? value) ? value : (line.Pitcher ? "P" : "");

        private static int OrderOf(PlayerGameLine line, IEnumerable<GameLineupEntry>? lineup)
            => (lineup ?? Enumerable.Empty<GameLineupEntry>()).FirstOrDefault(entry => entry.PlayerId == line.PlayerId)?.BattingOrder ?? 99;

        private static bool HasBattingLine(PlayerGameLine line) => line.PlateAppearances > 0 || line.R > 0 || line.SB > 0 || line.CS > 0;
        private static bool HasPitchingLine(PlayerGameLine line) => line.Pitcher || line.IPOuts > 0 || line.BattersFaced > 0 || line.PitchCount > 0;
        private static bool HasFieldingLine(PlayerGameLine line) => line.DefensiveOuts > 0 || line.Putouts > 0 || line.Assists > 0 || line.Errors > 0 || line.PassedBalls > 0 || line.CatcherStealAttempts > 0 || line.GamesMissedInjury > 0;

        private static List<string> LineScoreRow(string name, List<int>? runs, int innings, int total, int hits, int errors, int leftOnBase)
        {
            var row = new List<string> { name };
            for (int inning = 0; inning < Math.Max(1, innings); inning++)
                row.Add(inning < (runs?.Count ?? 0) ? runs![inning].ToString(CultureInfo.InvariantCulture) : "-");
            row.AddRange(new[] { N(total), N(hits), N(errors), N(leftOnBase) });
            return row;
        }

        private static string Record(Season season, GameResult throughGame, Guid teamId)
        {
            var ordered = season?.Games ?? new List<GameResult>();
            int throughIndex = ordered.FindIndex(game => game.Id == throughGame.Id);
            IEnumerable<GameResult> completed = throughIndex >= 0 ? ordered.Take(throughIndex + 1) : ordered.Where(game => game.PlayedAt <= throughGame.PlayedAt);
            var games = completed.Where(game => game.AwayTeamId == teamId || game.HomeTeamId == teamId).ToList();
            int wins = games.Count(game => game.AwayTeamId == teamId ? game.AwayScore > game.HomeScore : game.HomeScore > game.AwayScore);
            int losses = games.Count(game => game.AwayTeamId == teamId ? game.AwayScore < game.HomeScore : game.HomeScore < game.AwayScore);
            int ties = games.Count(game => game.AwayScore == game.HomeScore);
            return wins + "-" + losses + (ties > 0 ? "-" + ties : "");
        }

        private static string RulesText(GameResult game)
        {
            var rules = new List<string> { game.RegulationInnings + " regulation innings" };
            rules.Add(game.ExtraInningsEnabled ? "extra innings enabled" : "no extra innings");
            if (game.ExtraInningRunnerOnSecond) rules.Add("runner on second in extras");
            if (game.MercyRuleEnabled) rules.Add(game.MercyRuleRuns + "-run mercy rule after inning " + game.MercyRuleMinimumInning);
            if (game.EndedByMercyRule) rules.Add("ended by mercy rule");
            return string.Join("; ", rules);
        }

        private static PlayerGameLine? PlayerOfGame(GameResult game)
        {
            Guid winner = game.AwayScore == game.HomeScore ? Guid.Empty : game.AwayScore > game.HomeScore ? game.AwayTeamId : game.HomeTeamId;
            return (game.Lines ?? new List<PlayerGameLine>()).OrderByDescending(line => Score(line, winner)).FirstOrDefault();
        }

        private static double Score(PlayerGameLine line, Guid winner)
        {
            double value = line.H * 2 + line.Doubles + line.Triples * 2 + line.HR * 4 + line.RBI * 2 + line.R + line.BB * .8 + line.HBP * .8 +
                line.SB * 1.4 - line.CS - line.SO * .25 + line.IPOuts * .75 + line.K * 1.2 + line.Wins * 4 + line.Saves * 3 - line.ER * 2 -
                line.HitsAllowed * .6 - line.WalksAllowed * .6 - line.HomeRunsAllowed * 2 + line.Putouts * .05 + line.Assists * .1 - line.Errors * 1.5;
            return value + (winner != Guid.Empty && line.TeamId == winner ? 2 : 0);
        }

        private static string Performance(PlayerGameLine line)
        {
            var details = new List<string>();
            if (line.PlateAppearances > 0) details.Add(line.H + "-for-" + line.AB);
            if (line.HR > 0) details.Add(line.HR + " HR");
            if (line.RBI > 0) details.Add(line.RBI + " RBI");
            if (line.R > 0) details.Add(line.R + " R");
            if (line.SB > 0) details.Add(line.SB + " SB");
            if (line.IPOuts > 0) details.Add(Innings(line.IPOuts) + " IP");
            if (line.K > 0) details.Add(line.K + " K");
            if (line.IPOuts > 0) details.Add(line.ER + " ER");
            return details.Count == 0 ? "Key contributor" : string.Join(", ", details);
        }

        private static bool LooksLikePlateAppearance(string? description)
        {
            string value = description ?? "";
            return new[] { "strikes out", "singles", "doubles", "triples", "home run", "walks", "hit by pitch", "flies out", "grounds out", "pops out", "reaches on", "sacrifice" }
                .Any(marker => value.Contains(marker, StringComparison.OrdinalIgnoreCase));
        }

        private static string OrdinalWord(int value) => value switch
        {
            1 => "First", 2 => "Second", 3 => "Third", 4 => "Fourth", 5 => "Fifth", 6 => "Sixth", 7 => "Seventh", 8 => "Eighth", 9 => "Ninth",
            _ => value.ToString(CultureInfo.InvariantCulture) + "th"
        };

        private static string SeasonLabel(LeagueFile? league, Season? season)
        {
            int index = league?.Seasons?.IndexOf(season!) ?? -1;
            return index >= 0 ? "Season " + (index + 1) : season?.Name ?? "Season";
        }

        private static string Innings(int outs) => (outs / 3).ToString(CultureInfo.InvariantCulture) + "." + (outs % 3).ToString(CultureInfo.InvariantCulture);
        private static string N(int value) => value.ToString(CultureInfo.InvariantCulture);
        private static string ValueOrNone(string? value) => string.IsNullOrWhiteSpace(value) ? "None" : value;
        private static List<string> Row(string left, string right) => new List<string> { left, right ?? "" };
    }
}
