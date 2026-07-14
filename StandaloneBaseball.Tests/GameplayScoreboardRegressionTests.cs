using System.Drawing;
using System.Reflection;
using System.Runtime.ExceptionServices;
using StandaloneBaseball;

namespace StandaloneBaseball.Tests;

public sealed class GameplayScoreboardRegressionTests
{
    [Fact]
    public void EnabledHomeTemplateReplacesGenericHudAndReservesScoreboardHeight()
    {
        Team away = RegressionTestData.CreateTeam("Away");
        Team home = RegressionTestData.CreateTeam("Home");
        home.ScoreboardTemplate = new TeamScoreboardTemplate
        {
            Enabled = true,
            SchoolNameText = "HOME SCHOOL",
            PreferredAbbreviation = "HOME",
            MascotText = "CLUB",
            BackgroundAssetPath = "",
            BoardArgb = Color.Magenta.ToArgb(),
            BoardSecondArgb = Color.Magenta.ToArgb(),
            BoardThirdArgb = Color.Magenta.ToArgb(),
            BoardFourthArgb = Color.Magenta.ToArgb(),
            AccentArgb = Color.White.ToArgb(),
            TextArgb = Color.White.ToArgb(),
            AdStripArgb = Color.Black.ToArgb()
        };
        var state = new GameplayRenderingGameState
        {
            AwayScore = 3,
            HomeScore = 5,
            Inning = 7,
            TopHalf = false,
            Balls = 2,
            Strikes = 1,
            Outs = 2
        };
        state.SetTeams(away, home);

        Assert.True(GameplayScoreboardPresentation.UsesCustomScoreboard(state));
        Assert.Equal(150, GameplayScoreboardPresentation.HudHeight(new Rectangle(0, 0, 800, 600), state));
        Assert.Equal("AWAY 3   -   HOME 5", GameplayScoreboardPresentation.ScoreText(state));
        Assert.Equal("BOTTOM 7", GameplayScoreboardPresentation.InningText(state));
        Assert.Equal("B 2  S 1  O 2", GameplayScoreboardPresentation.CountText(state));

        Color pixel = RenderScoreboardPixel(state, new Point(31, 100));
        Assert.True(pixel.R > 140 && pixel.B > 140, $"Expected custom magenta board pixel, got {pixel}.");
    }

    [Fact]
    public void DisabledHomeTemplateKeepsGenericHudDimensions()
    {
        Team away = RegressionTestData.CreateTeam("Away");
        Team home = RegressionTestData.CreateTeam("Home");
        home.ScoreboardTemplate.Enabled = false;
        var state = new GameplayRenderingGameState();
        state.SetTeams(away, home);

        Assert.False(GameplayScoreboardPresentation.UsesCustomScoreboard(state));
        Assert.Equal(GameplayScoreboardPresentation.GenericHudHeight,
            GameplayScoreboardPresentation.HudHeight(new Rectangle(0, 0, 800, 600), state));
    }

    [Fact]
    public void ReplayEmbeddedScoreboardSnapshotTakesPriorityOverCurrentTeamTemplate()
    {
        Team current = RegressionTestData.CreateTeam("Current");
        current.ScoreboardTemplate = Template("CURRENT", Color.Blue);
        Team replayTarget = RegressionTestData.CreateTeam("Replay");
        var replayTeam = new ReplayTeam
        {
            TeamName = "Replay",
            Mascot = "Club",
            ScoreboardAbbreviation = "RPLY",
            ScoreboardTemplate = Template("RECORDED", Color.Red)
        };

        ReplayScoreboardPresentation.Apply(new ReplayFile(), replayTeam, replayTarget, current, homeTeam: true);

        Assert.True(replayTarget.ScoreboardTemplate.Enabled);
        Assert.Equal("RECORDED", replayTarget.ScoreboardTemplate.TemplateName);
        Assert.Equal(Color.Red.ToArgb(), replayTarget.ScoreboardTemplate.BoardArgb);
        Assert.NotSame(replayTeam.ScoreboardTemplate, replayTarget.ScoreboardTemplate);
    }

    [Fact]
    public void OlderReplayWithoutSnapshotUsesCurrentHomeTeamTemplate()
    {
        Team current = RegressionTestData.CreateTeam("Current");
        current.ScoreboardTemplate = Template("CURRENT TEAM", Color.Green);
        Team replayTarget = RegressionTestData.CreateTeam("Replay");

        ReplayScoreboardPresentation.Apply(
            new ReplayFile(), new ReplayTeam(), replayTarget, current, homeTeam: true);

        Assert.True(replayTarget.ScoreboardTemplate.Enabled);
        Assert.Equal("CURRENT TEAM", replayTarget.ScoreboardTemplate.TemplateName);
        Assert.Equal(Color.Green.ToArgb(), replayTarget.ScoreboardTemplate.BoardArgb);
    }

    [Fact]
    public void ReplayStoreLoadsPortableScoreboardSnapshotFromReplayJson()
    {
        string directory = Path.Combine(Path.GetTempPath(), "DansRBI-ScoreboardReplay", Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "scoreboard" + ReplayStore.Extension);
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(path, """
            {
              "replay_schema_version": 2,
              "teams": {
                "away": { "team_name": "Away" },
                "home": {
                  "team_name": "Home",
                  "scoreboard_template": {
                    "enabled": true,
                    "template_name": "Recorded Board",
                    "school_name_text": "Home School",
                    "preferred_abbreviation": "HOME",
                    "mascot_text": "Club",
                    "board_color_layout": 3,
                    "board_argb": -65536,
                    "ads": ["LOCAL BANK", "BOOSTER CLUB"]
                  }
                }
              }
            }
            """);

            ReplayFile replay = ReplayStore.Load(path);

            Assert.NotNull(replay.Teams.Home.ScoreboardTemplate);
            Assert.True(replay.Teams.Home.ScoreboardTemplate.Enabled);
            Assert.Equal("Recorded Board", replay.Teams.Home.ScoreboardTemplate.TemplateName);
            Assert.Equal(ScoreboardBoardColorLayout.Quarters,
                replay.Teams.Home.ScoreboardTemplate.BoardColorLayout);
            Assert.Equal(new[] { "LOCAL BANK", "BOOSTER CLUB" }, replay.Teams.Home.ScoreboardTemplate.Ads);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void LegacyReplayScoreboardJsonRemainsUsableWithoutEmbeddedSnapshot()
    {
        string directory = Path.Combine(Path.GetTempPath(), "DansRBI-LegacyScoreboard", Guid.NewGuid().ToString("N"));
        string templatePath = Path.Combine(directory, "legacy-scoreboard.json");
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(templatePath, """
            {
              "enabled": true,
              "template_name": "Legacy Board",
              "school_name_text": "Legacy School",
              "preferred_abbreviation": "LEG",
              "mascot_text": "Legends",
              "board_argb": -16776961,
              "ads": ["LEGACY SPONSOR"]
            }
            """);
            var replay = new ReplayFile
            {
                SourceDirectory = directory,
                Assets = new ReplayAssets { ScoreboardTemplate = "legacy-scoreboard.json" }
            };
            Team target = RegressionTestData.CreateTeam("Replay");

            ReplayScoreboardPresentation.Apply(
                replay, new ReplayTeam(), target, currentLeagueTeam: null, homeTeam: true);

            Assert.True(target.ScoreboardTemplate.Enabled);
            Assert.Equal("Legacy Board", target.ScoreboardTemplate.TemplateName);
            Assert.Equal("LEG", target.ScoreboardTemplate.PreferredAbbreviation);
            Assert.Equal(new[] { "LEGACY SPONSOR" }, target.ScoreboardTemplate.Ads);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static TeamScoreboardTemplate Template(string name, Color color)
    {
        return new TeamScoreboardTemplate
        {
            Enabled = true,
            TemplateName = name,
            BackgroundAssetPath = "",
            SchoolNameText = name,
            PreferredAbbreviation = "TEAM",
            MascotText = "CLUB",
            BoardArgb = color.ToArgb(),
            BoardSecondArgb = color.ToArgb(),
            BoardThirdArgb = color.ToArgb(),
            BoardFourthArgb = color.ToArgb(),
            AccentArgb = Color.White.ToArgb(),
            TextArgb = Color.White.ToArgb(),
            AdStripArgb = Color.Black.ToArgb(),
            Ads = new List<string> { "BOOSTER CLUB" }
        };
    }

    private static Color RenderScoreboardPixel(GameplayRenderingGameState state, Point sample)
    {
        Color color = Color.Empty;
        Exception failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                using var surface = new GameplayRenderingSurface { Size = new Size(800, 600) };
                surface.SetState(state);
                using var bitmap = new Bitmap(surface.Width, surface.Height);
                using Graphics graphics = Graphics.FromImage(bitmap);
                using var paint = new PaintEventArgs(graphics, surface.ClientRectangle);
                MethodInfo onPaint = typeof(GameplayRenderingSurface).GetMethod(
                    "OnPaint", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(onPaint);
                onPaint.Invoke(surface, new object[] { paint });
                color = bitmap.GetPixel(sample.X, sample.Y);
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(15)), "Gameplay scoreboard render test timed out.");
        if (failure != null)
            ExceptionDispatchInfo.Capture(failure).Throw();
        return color;
    }
}
