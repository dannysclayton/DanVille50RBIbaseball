#nullable enable annotations

using System.Drawing;
using System.Windows.Forms;

namespace StandaloneBaseball.Tests;

public sealed class GameControllerDiscoveryTests
{
    [Fact]
    public void LegacyJoystick_MapsButtonsAndDiagonalPovToGameplayControls()
    {
        XInputButtons mapped = LegacyJoystickController.MapButtons(0x0001 | 0x0004 | 0x0020, 4500);

        Assert.True(mapped.HasFlag(XInputButtons.A));
        Assert.True(mapped.HasFlag(XInputButtons.X));
        Assert.True(mapped.HasFlag(XInputButtons.RightShoulder));
        Assert.True(mapped.HasFlag(XInputButtons.DPadUp));
        Assert.True(mapped.HasFlag(XInputButtons.DPadRight));
        Assert.False(mapped.HasFlag(XInputButtons.DPadDown));
    }

    [Theory]
    [InlineData(0u, short.MinValue)]
    [InlineData(32768u, 0)]
    [InlineData(65535u, short.MaxValue)]
    public void LegacyJoystick_NormalizesWindowsAxisRange(uint input, short expected)
    {
        Assert.Equal(expected, LegacyJoystickController.NormalizeAxis(input));
    }

    [Fact]
    public void GameplayForm_ShowsAutomaticControllerAcquisitionStatus()
    {
        WinFormsTestHost.Run(() =>
        {
            using var form = new GameplayForm(TeamWithRoster("Away", 71), TeamWithRoster("Home", 72));
            Label status = Assert.Single(Descendants(form).OfType<Label>(),
                label => label.Text.StartsWith("Controller:", StringComparison.Ordinal));
            Assert.Contains("scanning", status.Text, StringComparison.OrdinalIgnoreCase);
        });
    }

    private static Team TeamWithRoster(string name, int seed)
    {
        var team = new Team
        {
            City = name,
            Nickname = "Club",
            ScoreboardAbbreviation = name.Substring(0, Math.Min(3, name.Length)).ToUpperInvariant(),
            PrimaryArgb = Color.Navy.ToArgb(),
            SecondaryArgb = Color.White.ToArgb()
        };
        Simulator.FillRandomRoster(team, new Random(seed));
        return team;
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
