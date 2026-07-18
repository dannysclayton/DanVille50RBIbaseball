#nullable enable annotations

using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace StandaloneBaseball.Tests;

[Collection(WinFormsTestCollection.Name)]
public sealed class GameplayVisualRegressionTests
{
    [Fact]
    public void GameplaySurface_GeneratesUniformSpriteWhenNoSpriteAssetWasAssigned()
    {
        WinFormsTestHost.Run(() =>
        {
            Team away = TeamWithRoster("Away", Color.Cyan, Color.White, 11);
            Team home = TeamWithRoster("Home", Color.Magenta, Color.Gold, 22);
            var state = new GameplayRenderingGameState();
            state.SetTeams(away, home);

            using var surface = new GameplayRenderingSurface { Size = new Size(1000, 700) };
            surface.SetState(state);
            Player player = state.Fielders.First(marker => marker.Player != null).Player!;
            MethodInfo method = typeof(GameplayRenderingSurface).GetMethod(
                "LoadSpriteSheet", BindingFlags.Instance | BindingFlags.NonPublic)!;

            Image image = Assert.IsAssignableFrom<Image>(method.Invoke(surface, new object?[] { player, home }));
            Assert.Equal(SpriteSheetGeneratorOptions.FrameWidth * SpriteSheetGeneratorOptions.Columns, image.Width);
            Assert.Equal(SpriteSheetGeneratorOptions.FrameHeight * SpriteSheetGeneratorOptions.Rows, image.Height);
        });
    }

    [Fact]
    public void GameplaySurface_DrawsCurrentBatterInOffenseUniform()
    {
        WinFormsTestHost.Run(() =>
        {
            Team away = TeamWithRoster("Away", Color.Cyan, Color.White, 33);
            Team home = TeamWithRoster("Home", Color.Magenta, Color.Gold, 44);
            var state = new GameplayRenderingGameState();
            state.SetTeams(away, home);
            state.Phase = GameplayRenderingPhase.Ready;

            using var surface = new GameplayRenderingSurface { Size = new Size(1000, 700) };
            surface.SetState(state);
            using var bitmap = new Bitmap(surface.Width, surface.Height);
            using Graphics graphics = Graphics.FromImage(bitmap);
            MethodInfo onPaint = typeof(GameplayRenderingSurface).GetMethod(
                "OnPaint", BindingFlags.Instance | BindingFlags.NonPublic)!;
            using var args = new PaintEventArgs(graphics, surface.ClientRectangle);
            onPaint.Invoke(surface, new object[] { args });

            int offensePixels = CountNearColor(bitmap, new Rectangle(410, 490, 125, 150), Color.Cyan, 35);
            Assert.True(offensePixels > 20, "The current batter should be visible near home plate in the batting team's colors.");
        });
    }

    [Fact]
    public void GameplayForm_ProvidesVisiblePrimaryGameplayControlAndPlayableTiming()
    {
        WinFormsTestHost.Run(() =>
        {
            using var form = new GameplayForm(
                TeamWithRoster("Away", Color.Blue, Color.White, 55),
                TeamWithRoster("Home", Color.Red, Color.White, 66));

            Assert.Contains(Descendants(form).OfType<Button>(), button => button.Text == "Pitch / Swing");
            Assert.Contains(Descendants(form).OfType<Label>(), label => label.Text == "Game controls");

            float pitchStep = (float)typeof(GameplayForm).GetField(
                "PitchProgressPerTick", BindingFlags.Static | BindingFlags.NonPublic)!.GetRawConstantValue()!;
            float battedBallStep = (float)typeof(GameplayForm).GetField(
                "BallInPlayProgressPerTick", BindingFlags.Static | BindingFlags.NonPublic)!.GetRawConstantValue()!;
            Assert.True(pitchStep <= 0.02f, "A pitch must remain visible long enough for a user-controlled swing.");
            Assert.True(battedBallStep <= 0.015f, "A batted ball must remain visible long enough to follow the play.");
        });
    }

    private static Team TeamWithRoster(string name, Color primary, Color secondary, int seed)
    {
        var team = new Team
        {
            City = name,
            Nickname = "Club",
            ScoreboardAbbreviation = name.Substring(0, Math.Min(3, name.Length)).ToUpperInvariant(),
            PrimaryArgb = primary.ToArgb(),
            SecondaryArgb = secondary.ToArgb()
        };
        Simulator.FillRandomRoster(team, new Random(seed));
        foreach (Player player in team.Roster)
            player.SpriteSheetPath = "";
        team.SpriteSheetPath = "";
        return team;
    }

    private static int CountNearColor(Bitmap image, Rectangle region, Color target, int tolerance)
    {
        int count = 0;
        Rectangle bounds = Rectangle.Intersect(region, new Rectangle(Point.Empty, image.Size));
        for (int y = bounds.Top; y < bounds.Bottom; y++)
        {
            for (int x = bounds.Left; x < bounds.Right; x++)
            {
                Color pixel = image.GetPixel(x, y);
                if (Math.Abs(pixel.R - target.R) <= tolerance &&
                    Math.Abs(pixel.G - target.G) <= tolerance &&
                    Math.Abs(pixel.B - target.B) <= tolerance)
                    count++;
            }
        }
        return count;
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
