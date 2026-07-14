using System.Reflection;
using System.Windows.Forms;

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
                Season reloadedSeason = Assert.Single(reloaded.Seasons.Where(item => item.Id == season.Id));
                GameResult reloadedGame = Assert.Single(reloadedSeason.Games.Where(item => item.Id == result.Id));
                Assert.Equal(4, reloadedGame.AwayScore);
                Assert.Equal(2, reloadedGame.HomeScore);
                Assert.Equal(result.Id, Assert.Single(reloadedSeason.Schedule.Where(item => item.Id == scheduled.Id)).PlayedGameId);
            }
            finally
            {
                AssetPathResolver.ClearLeagueFilePath();
                if (Directory.Exists(directory))
                    Directory.Delete(directory, recursive: true);
            }
        });
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
