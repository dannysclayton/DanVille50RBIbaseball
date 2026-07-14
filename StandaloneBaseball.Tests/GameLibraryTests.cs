using System.IO.Compression;
using System.Text;
using StandaloneBaseball;

namespace StandaloneBaseball.Tests;

public sealed class GameLibraryTests
{
    [Fact]
    public void StartingLineupAndTracker_KeepDhPitcherReplacementAndPositionHistory()
    {
        Team team = RegressionTestData.CreateTeam("Archive");
        List<GameLineupEntry> entries = LineupEngine.CaptureStartingLineup(team);
        Player starter = team.Roster.Single(player => player.Id == team.BaseLineup.StartingPitcherId);
        Player reliever = team.Roster.First(player => player.Role == PlayerRole.Pitcher && player.Id != starter.Id);

        Assert.Contains(entries, entry => entry.PlayerId == starter.Id && entry.BattingOrder == 0 && entry.DefensivePosition == "P");
        int originalCount = entries.Count;

        GameLineupTracker.RecordPitcherChange(entries, reliever, starter, 6, HalfInning.Top, reason: "Pitching change");
        GameLineupTracker.RecordPositionChange(entries, reliever, 8, HalfInning.Bottom, "1B", "Double switch");

        Assert.Equal(originalCount + 1, entries.Count);
        GameLineupEntry replacement = entries.Single(entry => entry.PlayerId == reliever.Id);
        Assert.False(replacement.IsStarter);
        Assert.Equal(starter.Id, replacement.ReplacedPlayerId);
        Assert.Equal(6, replacement.EnteredInning);
        Assert.Contains(replacement.PositionHistory, change => change.Position == "P" && change.Inning == 6);
        Assert.Contains(replacement.PositionHistory, change => change.Position == "1B" && change.Inning == 8);
        Assert.Equal(6, entries.Single(entry => entry.PlayerId == starter.Id).ExitedInning);
    }

    [Fact]
    public void ArchiveGame_CreatesBothFormsForBothParticipatingTeams()
    {
        string root = TempDirectory();
        Team away = RegressionTestData.CreateTeam("Away");
        Team home = RegressionTestData.CreateTeam("Home");
        LeagueFile league = RegressionTestData.CreateLeague(away, home);
        var season = new Season { Name = "Opening Year" };
        league.Seasons.Add(season);
        GameResult game = DetailedResult(away, home);
        season.Games.Add(game);

        try
        {
            List<GameLibraryArtifact> artifacts = GameLibraryService.ArchiveGame(
                league,
                season,
                game,
                team => Path.Combine(root, team.Id.ToString("N")),
                team => null);

            Assert.Equal(2, artifacts.Count);
            Assert.All(artifacts, artifact =>
            {
                Assert.True(File.Exists(artifact.LineupPath));
                Assert.True(File.Exists(artifact.GameResultPath));
                Assert.Contains(Path.DirectorySeparatorChar + GameLibraryService.LineupFolderName + Path.DirectorySeparatorChar, artifact.LineupPath);
                Assert.Contains(Path.DirectorySeparatorChar + GameLibraryService.ResultsFolderName + Path.DirectorySeparatorChar, artifact.GameResultPath);
            });

            string reportXml = DocumentXml(artifacts[0].GameResultPath);
            Assert.Contains("Box Score", reportXml);
            Assert.Contains("Winning Pitcher", reportXml);
            Assert.Contains("Holds", reportXml);
            Assert.Contains("Player of the Game", reportXml);
            Assert.Contains("Batting", reportXml);
            Assert.Contains("Pitching", reportXml);
            Assert.Contains("Fielding", reportXml);
            Assert.Contains("Play-by-Play", reportXml);
            Assert.Contains("strikes out", reportXml);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void LineupDocument_IncludesMoreThanNineParticipantsAndPositionChanges()
    {
        string root = TempDirectory();
        string path = Path.Combine(root, "lineup.docx");
        Team team = RegressionTestData.CreateTeam("Deep");
        List<GameLineupEntry> entries = LineupEngine.CaptureStartingLineup(team);
        Player starter = team.Roster.Single(player => player.Id == team.BaseLineup.StartingPitcherId);
        Player reliever = team.Roster.First(player => player.Role == PlayerRole.Pitcher && player.Id != starter.Id);
        GameLineupTracker.RecordPitcherChange(entries, reliever, starter, 5, HalfInning.Bottom);
        GameLineupTracker.RecordPositionChange(entries, reliever, 7, HalfInning.Top, "3B", "Position change");

        try
        {
            LineupCardExporter.WriteDocx(path, "Participation Card", new[] { LineupCardExporter.BuildPage(team, null, entries) });
            string xml = DocumentXml(path);
            Assert.Contains(reliever.Name, xml);
            Assert.Contains("Entered Bottom 5", xml);
            Assert.Contains("3B - Top 7", xml);
            Assert.True(Count(xml, "<w:tr>") > 11);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static GameResult DetailedResult(Team away, Team home)
    {
        Player awayPitcher = away.Roster.First(player => player.Role == PlayerRole.Pitcher);
        Player homePitcher = home.Roster.First(player => player.Role == PlayerRole.Pitcher);
        Player hitter = away.Roster.First(player => player.Role == PlayerRole.Batter);
        return new GameResult
        {
            AwayTeamId = away.Id,
            HomeTeamId = home.Id,
            AwayScore = 3,
            HomeScore = 1,
            AwayHits = 7,
            HomeHits = 4,
            AwayErrors = 1,
            HomeErrors = 0,
            AwayLeftOnBase = 6,
            HomeLeftOnBase = 5,
            AwayRunsByInning = new List<int> { 1, 0, 2, 0, 0 },
            HomeRunsByInning = new List<int> { 0, 1, 0, 0, 0 },
            GameLengthInnings = 5,
            WinningPitcherName = awayPitcher.Name,
            LosingPitcherName = homePitcher.Name,
            SavePitcherName = "Away Closer",
            AwayStartingLineup = LineupEngine.CaptureStartingLineup(away),
            HomeStartingLineup = LineupEngine.CaptureStartingLineup(home),
            Lines = new List<PlayerGameLine>
            {
                new PlayerGameLine { TeamId = away.Id, PlayerId = hitter.Id, PlayerName = hitter.Name, AB = 3, H = 2, R = 1, RBI = 2, Putouts = 3, DefensiveOuts = 15 },
                new PlayerGameLine { TeamId = away.Id, PlayerId = awayPitcher.Id, PlayerName = awayPitcher.Name, Pitcher = true, StartingPitcher = true, IPOuts = 15, K = 7, HitsAllowed = 4, RunsAllowed = 1, ER = 1, Wins = 1, PitchCount = 74 },
                new PlayerGameLine { TeamId = away.Id, PlayerId = Guid.NewGuid(), PlayerName = "Away Setup", Pitcher = true, IPOuts = 3, Holds = 1 },
                new PlayerGameLine { TeamId = home.Id, PlayerId = homePitcher.Id, PlayerName = homePitcher.Name, Pitcher = true, StartingPitcher = true, IPOuts = 15, K = 4, HitsAllowed = 7, RunsAllowed = 3, ER = 3, Losses = 1 }
            },
            PlayByPlay = new List<GamePlayByPlayEntry>
            {
                new GamePlayByPlayEntry { Sequence = 1, Inning = 1, Half = HalfInning.Top, Outs = 1, AwayScore = 0, HomeScore = 0, Bases = "Empty", Description = hitter.Name + " strikes out." },
                new GamePlayByPlayEntry { Sequence = 2, Inning = 1, Half = HalfInning.Top, Outs = 1, AwayScore = 1, HomeScore = 0, Bases = "1B", Description = "A run scores." }
            }
        };
    }

    private static string DocumentXml(string path)
    {
        using var archive = ZipFile.OpenRead(path);
        using var reader = new StreamReader(archive.GetEntry("word/document.xml")!.Open(), Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static int Count(string value, string needle)
    {
        int count = 0;
        for (int index = 0; (index = value.IndexOf(needle, index, StringComparison.Ordinal)) >= 0; index += needle.Length)
            count++;
        return count;
    }

    private static string TempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "DansRBI-GameLibraryTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
