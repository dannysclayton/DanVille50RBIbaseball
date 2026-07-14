using System.Drawing;
using System.Windows.Forms;

namespace StandaloneBaseball.Tests;

[Collection(WinFormsTestCollection.Name)]
public sealed class SimulationAndChampionshipWorkflowTests
{
    [Fact]
    public void LiveSimulation_AdvancesEventsAndEnablesCommitAtTheEnd()
    {
        WinFormsTestHost.Run(() =>
        {
            var run = new SimulatedGameEngine.SimulatedGameRun
            {
                Result = new GameResult { AwayScore = 1, HomeScore = 0 },
                Events = new List<SimulatedGameEngine.SimulatedGameEvent>
                {
                    new SimulatedGameEngine.SimulatedGameEvent
                    {
                        Inning = 1,
                        TopHalf = true,
                        Outs = 1,
                        AwayScore = 1,
                        HomeScore = 0,
                        Bases = "Runner on first",
                        Narration = "Leadoff single scores."
                    }
                }
            };
            var away = new Team { City = "North", Nickname = "Stars", ScoreboardAbbreviation = "NORTH" };
            var home = new Team { City = "South", Nickname = "Bears", ScoreboardAbbreviation = "SOUTH" };
            var type = typeof(MainForm).Assembly.GetType("StandaloneBaseball.LiveSimulationForm", throwOnError: true);
            using var form = (Form)Activator.CreateInstance(type, run, away, home, "", "");

            WinFormsTestHost.Invoke(form, "ShowNextEvent");
            Assert.Equal("NORTH 1   -   SOUTH 0", WinFormsTestHost.Field<Label>(form, "_scoreLabel").Text);
            Assert.Equal("Top 1", WinFormsTestHost.Field<Label>(form, "_inningLabel").Text);
            Assert.Equal("Runner on first | 1 out", WinFormsTestHost.Field<Label>(form, "_stateLabel").Text);
            Assert.Single(WinFormsTestHost.Field<ListBox>(form, "_playByPlay").Items);

            WinFormsTestHost.Invoke(form, "ShowNextEvent");
            var commit = WinFormsTestHost.Field<Button>(form, "_commitButton");
            Assert.True(commit.Enabled);
            WinFormsTestHost.Invoke(commit, "OnClick", EventArgs.Empty);
            Assert.True((bool)type.GetProperty("CommitRequested")!.GetValue(form));
            Assert.Same(run.Result, type.GetProperty("Result")!.GetValue(form));
        });
    }

    [Fact]
    public void ChampionshipDialog_RendersSeriesSummaryAndFallbackMedia()
    {
        WinFormsTestHost.Run(() =>
        {
            var season = new Season { Name = "2026 Championship Season", Year = 2026 };
            var champion = new Team
            {
                City = "Danville",
                Nickname = "Champions",
                ScoreboardAbbreviation = "DAN",
                PrimaryArgb = Color.Navy.ToArgb(),
                SecondaryArgb = Color.Gold.ToArgb()
            };
            var series = new PlayoffSeries { TeamAWins = 4, TeamBWins = 2 };
            using var dialog = new ChampionshipDialog(season, 3, false, champion, series, "missing-logo.png", new[] { "missing-photo.png" });

            Assert.Equal("Season 3 World Champions!", dialog.Text);
            var labels = Descendants(dialog).OfType<Label>().Select(label => label.Text).ToList();
            Assert.Contains("Danville Champions", labels);
            Assert.Contains("Series won 4-2", labels);
            Assert.Contains("NO TEAM PHOTOS", labels);
            Assert.Contains("Saved to 2026 Championship Season season history", labels);
            Assert.NotNull(dialog.AcceptButton);
        });
    }

    [Fact]
    public void ChampionshipDialog_UsesBackToBackTitleAndReadableTextColors()
    {
        WinFormsTestHost.Run(() =>
        {
            using var dialog = new ChampionshipDialog(
                new Season { Name = "Repeat Season" },
                0,
                true,
                new Team { City = "Repeat", Nickname = "Winners", PrimaryArgb = Color.White.ToArgb(), SecondaryArgb = Color.Black.ToArgb() },
                null,
                null,
                null);

            Assert.Equal("BACK TO BACK WORLD CHAMPIONS!", dialog.Text);
            Assert.Equal(Color.FromArgb(20, 24, 32), WinFormsTestHost.InvokeStatic(typeof(ChampionshipDialog), "ReadableTextColor", Color.White));
            Assert.Equal(Color.White, WinFormsTestHost.InvokeStatic(typeof(ChampionshipDialog), "ReadableTextColor", Color.Black));
        });
    }

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var descendant in Descendants(child))
                yield return descendant;
        }
    }
}
