using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace StandaloneBaseball
{
    internal sealed class GameLibraryArtifact
    {
        public Guid TeamId { get; set; }
        public Guid GameId { get; set; }
        public string LineupPath { get; set; } = "";
        public string GameResultPath { get; set; } = "";
    }

    internal static class GameLibraryService
    {
        public const string LineupFolderName = "Line Up";
        public const string ResultsFolderName = "Game Results";

        public static List<GameLibraryArtifact> ArchiveGame(
            LeagueFile league,
            Season season,
            GameResult game,
            Func<Team, string> teamAssetDirectory,
            Func<Team, string?> teamLogoPath)
        {
            if (league == null || season == null || game == null)
                return new List<GameLibraryArtifact>();

            Team? away = league.Teams?.FirstOrDefault(team => team.Id == game.AwayTeamId);
            Team? home = league.Teams?.FirstOrDefault(team => team.Id == game.HomeTeamId);
            if (away == null || home == null)
                return new List<GameLibraryArtifact>();

            int seasonNumber = Math.Max(1, league.Seasons?.IndexOf(season) + 1 ?? 1);
            string gameLabel = FileName(game.PlayedAt.ToString("yyyy-MM-dd") + " " + away.ScoreboardName + " at " + home.ScoreboardName + " " + game.Id.ToString("N").Substring(0, 8));
            var artifacts = new List<GameLibraryArtifact>();
            foreach (Team team in new[] { away, home })
            {
                string assetRoot = teamAssetDirectory(team);
                string lineupDirectory = Path.Combine(assetRoot, LineupFolderName, "Season " + seasonNumber);
                string resultDirectory = Path.Combine(assetRoot, ResultsFolderName, "Season " + seasonNumber);
                Directory.CreateDirectory(lineupDirectory);
                Directory.CreateDirectory(resultDirectory);

                bool isAway = team.Id == away.Id;
                IEnumerable<GameLineupEntry> snapshot = isAway ? game.AwayStartingLineup : game.HomeStartingLineup;
                string lineupPath = Path.Combine(lineupDirectory, gameLabel + " - " + FileName(team.DisplayName) + " Lineup.docx");
                string resultPath = Path.Combine(resultDirectory, gameLabel + " - Game Report.docx");
                LineupCardExporter.WriteDocx(lineupPath, "Season " + seasonNumber + " - " + team.DisplayName + " Lineup", new[]
                {
                    LineupCardExporter.BuildPage(team, teamLogoPath(team), snapshot)
                });
                GameReportExporter.WriteDocx(resultPath, league, season, new[] { game });
                artifacts.Add(new GameLibraryArtifact { TeamId = team.Id, GameId = game.Id, LineupPath = lineupPath, GameResultPath = resultPath });
            }
            return artifacts;
        }

        public static void ExportLineups(string path, LeagueFile league, Season season, IEnumerable<GameResult> games, IEnumerable<Team> teams, Func<Team, string?> logoPath)
        {
            var teamIds = new HashSet<Guid>((teams ?? Enumerable.Empty<Team>()).Select(team => team.Id));
            var pages = new List<LineupCardDocumentPage>();
            foreach (GameResult game in (games ?? Enumerable.Empty<GameResult>()).OrderBy(item => item.PlayedAt))
            {
                Team? away = league.Teams.FirstOrDefault(team => team.Id == game.AwayTeamId);
                Team? home = league.Teams.FirstOrDefault(team => team.Id == game.HomeTeamId);
                if (away != null && teamIds.Contains(away.Id)) pages.Add(LineupCardExporter.BuildPage(away, logoPath(away), game.AwayStartingLineup));
                if (home != null && teamIds.Contains(home.Id)) pages.Add(LineupCardExporter.BuildPage(home, logoPath(home), game.HomeStartingLineup));
            }
            LineupCardExporter.WriteDocx(path, SeasonName(league, season) + " Lineup Library", pages);
        }

        public static void ExportResults(string path, LeagueFile league, Season season, IEnumerable<GameResult> games)
            => GameReportExporter.WriteDocx(path, league, season, games);

        public static void ExportBoth(string directory, LeagueFile league, Season season, IEnumerable<GameResult> games, IEnumerable<Team> teams, Func<Team, string?> logoPath)
        {
            Directory.CreateDirectory(directory);
            string seasonName = FileName(SeasonName(league, season));
            ExportLineups(Path.Combine(directory, seasonName + " Line Ups.docx"), league, season, games, teams, logoPath);
            ExportResults(Path.Combine(directory, seasonName + " Game Results.docx"), league, season, games);
        }

        private static string SeasonName(LeagueFile league, Season season)
        {
            int index = league.Seasons.IndexOf(season);
            return index >= 0 ? "Season " + (index + 1) : season.Name;
        }

        private static string FileName(string? value)
        {
            string result = value ?? "Game";
            foreach (char invalid in Path.GetInvalidFileNameChars())
                result = result.Replace(invalid, '_');
            return string.IsNullOrWhiteSpace(result) ? "Game" : result.Trim();
        }
    }
}
