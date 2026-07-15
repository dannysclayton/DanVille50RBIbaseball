using System.Reflection;
using System.IO.Compression;
using System.Windows.Forms;
using System.Xml.Linq;

namespace StandaloneBaseball.Tests;

[Collection(WinFormsTestCollection.Name)]
public sealed class MainFormWorkflowTests
{
    [Fact]
    public void MainForm_BuildsStarterDynastyAndNavigatesRepresentativeMenuActions()
    {
        WinFormsTestHost.Run(() =>
        {
            using var form = new MainForm();
            var league = WinFormsTestHost.Field<LeagueFile>(form, "_league");
            var tabs = WinFormsTestHost.Field<TabControl>(form, "_tabs");

            Assert.Equal(4, league.Teams.Count);
            Assert.Single(league.Seasons);
            Assert.All(league.Teams, team => Assert.NotEmpty(team.Roster));
            Assert.Equal(14, tabs.TabPages.Count);
            Assert.Equal("Teams", tabs.TabPages[0].Text);

            form.ApplyMenuAction(MenuAction.Game);
            Assert.Equal("Game", tabs.SelectedTab.Text);
            form.ApplyMenuAction(MenuAction.Seasons);
            Assert.Equal("Seasons", tabs.SelectedTab.Text);
            form.ApplyMenuAction(MenuAction.Teams);
            Assert.Equal("Teams", tabs.SelectedTab.Text);
        });
    }

    [Fact]
    public void MainForm_TeamMutationSnapshotRestoresCommittedTeamState()
    {
        var league = new LeagueFile();
        var team = new Team { City = "Original", Nickname = "Nine" };
        league.Teams.Add(team);
        var snapshotType = typeof(MainForm).GetNestedType("TeamMutationSnapshot", BindingFlags.NonPublic);
        Assert.NotNull(snapshotType);
        var capture = snapshotType.GetMethod("Capture", BindingFlags.Public | BindingFlags.Static);
        var snapshot = capture.Invoke(null, new object[] { league, new[] { team } });

        team.City = "Changed";
        team.Nickname = "Club";
        snapshotType.GetMethod("Restore", BindingFlags.Public | BindingFlags.Instance)!.Invoke(snapshot, new object[] { league });

        Assert.Equal("Original Nine", league.Teams[0].DisplayName);
        Assert.NotSame(team, league.Teams[0]);
    }

    [Theory]
    [InlineData(3, 5)]
    [InlineData(7, 7)]
    [InlineData(12, 9)]
    public void MainForm_ClampsGameInningsToSupportedRange(int value, int expected)
    {
        Assert.Equal(expected, WinFormsTestHost.InvokeStatic(typeof(MainForm), "ClampGameInnings", value));
    }

    [Fact]
    public void MainForm_ProvidesTrophyAccessAcrossRequestedPages()
    {
        WinFormsTestHost.Run(() =>
        {
            using var form = new MainForm();
            List<Control> controls = Descendants(form).ToList();

            Assert.Contains(controls.OfType<Button>(), button => button.Text == "Team Trophies...");
            Assert.Contains(controls.OfType<Button>(), button => button.Text == "Lineup Cards...");
            Assert.Contains(controls.OfType<Button>(), button => button.Text == "Player Trophies...");
            Assert.Equal(2, controls.OfType<TabPage>().Count(page => page.Text == "Trophies"));
            Assert.Equal(2, controls.OfType<TrophyGalleryControl>().Count());
        });
    }

    [Fact]
    public void MainForm_CommittedGameAutosavesDynasty()
    {
        WinFormsTestHost.Run(() =>
        {
            string directory = Path.Combine(Path.GetTempPath(), "DansRBI-AutosaveTests", Guid.NewGuid().ToString("N"));
            string path = Path.Combine(directory, "autosave" + LeagueStore.Extension);
            Directory.CreateDirectory(directory);

            try
            {
                using var form = new MainForm();
                var league = WinFormsTestHost.Field<LeagueFile>(form, "_league");
                var season = league.Seasons[0];
                var away = league.Teams[0];
                var home = league.Teams[1];
                var scheduled = new ScheduledGame
                {
                    GameNumber = season.Schedule.Count + 1,
                    AwayTeamId = away.Id,
                    HomeTeamId = home.Id,
                    Type = ScheduledGameType.NonConference
                };
                season.Schedule.Add(scheduled);

                typeof(MainForm).GetField("_path", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(form, path);
                AssetPathResolver.SetLeagueFilePath(path);

                var result = new GameResult
                {
                    AwayTeamId = away.Id,
                    HomeTeamId = home.Id,
                    AwayScore = 4,
                    HomeScore = 2
                };

                bool saved = (bool)WinFormsTestHost.Invoke(
                    form,
                    "CommitGameResult",
                    season,
                    scheduled,
                    result,
                    false,
                    false,
                    true);

                Assert.True(saved);
                Assert.True(File.Exists(path));
                Assert.False(WinFormsTestHost.Field<bool>(form, "_dirty"));

                LeagueFile reloaded = LeagueStore.Load(path);
                Season reloadedSeason = Assert.Single(reloaded.Seasons, item => item.Id == season.Id);
                GameResult reloadedGame = Assert.Single(reloadedSeason.Games, item => item.Id == result.Id);
                Assert.Equal(4, reloadedGame.AwayScore);
                Assert.Equal(2, reloadedGame.HomeScore);
                Assert.Equal(result.Id, Assert.Single(reloadedSeason.Schedule, item => item.Id == scheduled.Id).PlayedGameId);
            }
            finally
            {
                AssetPathResolver.ClearLeagueFilePath();
                if (Directory.Exists(directory))
                    Directory.Delete(directory, recursive: true);
            }
        });
    }

    [Fact]
    public void MainForm_GridExportBuilderPreservesTypedValuesThroughXlsxExport()
    {
        WinFormsTestHost.Run(() =>
        {
            string path = Path.Combine(Path.GetTempPath(), "MainFormGridExport-" + Guid.NewGuid().ToString("N") + ".xlsx");
            try
            {
                using var grid = new DataGridView { AllowUserToAddRows = false };
                grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Number", ValueType = typeof(decimal) });
                grid.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "Active", ValueType = typeof(bool) });
                grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Started", ValueType = typeof(DateTime) });
                grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Offset", ValueType = typeof(DateTimeOffset) });
                grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Day", ValueType = typeof(DateOnly) });
                grid.Columns[0].DefaultCellStyle.Format = "N2";
                grid.Columns[2].DefaultCellStyle.Format = "g";

                var started = new DateTime(2026, 7, 14, 19, 30, 45, DateTimeKind.Utc);
                var offset = new DateTimeOffset(2026, 7, 15, 8, 5, 0, TimeSpan.FromHours(-5));
                var day = new DateOnly(2026, 7, 16);
                grid.Rows.Add(1234.5m, true, started, offset, day);

                var section = Assert.IsType<ExportSection>(
                    WinFormsTestHost.InvokeStatic(typeof(MainForm), "BuildGridExportSection", "Typed Grid", grid));
                NativeDocumentExporter.WriteXlsx(path, "Typed Export", new[] { section });

                using var archive = ZipFile.OpenRead(path);
                using Stream worksheetStream = archive.GetEntry("xl/worksheets/sheet1.xml")!.Open();
                XDocument worksheet = XDocument.Load(worksheetStream);
                XNamespace spreadsheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

                AssertExportCell(worksheet, spreadsheet, "A5", null, "1234.5");
                AssertExportCell(worksheet, spreadsheet, "B5", "b", "1");
                AssertExportCell(worksheet, spreadsheet, "C5", "d", "2026-07-14T19:30:45Z");
                AssertExportCell(worksheet, spreadsheet, "D5", "d", "2026-07-15T08:05:00-05:00");
                AssertExportCell(worksheet, spreadsheet, "E5", "d", "2026-07-16");
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        });
    }

    [Fact]
    public void MainForm_RankingExportBuilderPreservesNumbersThroughXlsxExport()
    {
        string path = Path.Combine(Path.GetTempPath(), "MainFormRankingExport-" + Guid.NewGuid().ToString("N") + ".xlsx");
        try
        {
            var poll = new SeasonRankingPoll
            {
                Name = "Week 4",
                Rankings = new List<SeasonRankingEntry>
                {
                    new()
                    {
                        Rank = 1,
                        PreviousRank = 2,
                        TeamName = "Danville",
                        Wins = 9,
                        Losses = 1,
                        Score = 97.25,
                        PollScore = 98.5,
                        ComputerScore = 96.125,
                        RankedWins = 3,
                        StrengthOfSchedule = 0.742,
                        RunDifferential = 41
                    }
                }
            };

            var sections = Assert.IsType<List<ExportSection>>(
                WinFormsTestHost.InvokeStatic(typeof(MainForm), "BuildRankingPollsExportSections", (object)new[] { poll }));
            NativeDocumentExporter.WriteXlsx(path, "Rankings", sections);

            using var archive = ZipFile.OpenRead(path);
            using Stream worksheetStream = archive.GetEntry("xl/worksheets/sheet1.xml")!.Open();
            XDocument worksheet = XDocument.Load(worksheetStream);
            XNamespace spreadsheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

            AssertExportCell(worksheet, spreadsheet, "A5", null, "1");
            AssertExportCell(worksheet, spreadsheet, "B5", null, "2");
            AssertExportCell(worksheet, spreadsheet, "D5", null, "9");
            AssertExportCell(worksheet, spreadsheet, "G5", null, "97.25");
            AssertExportCell(worksheet, spreadsheet, "K5", null, "0.742");
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    private static void AssertExportCell(XDocument worksheet, XNamespace spreadsheet, string reference, string type, string value)
    {
        XElement cell = Assert.Single(
            worksheet.Descendants(spreadsheet + "c"),
            element => (string)element.Attribute("r") == reference);
        Assert.Equal(type, (string)cell.Attribute("t"));
        Assert.Equal(value, cell.Element(spreadsheet + "v")?.Value);
    }

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (Control descendant in Descendants(child))
                yield return descendant;
        }
    }
}
