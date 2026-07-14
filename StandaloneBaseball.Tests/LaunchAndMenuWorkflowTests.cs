using System.Drawing;
using System.Windows.Forms;

namespace StandaloneBaseball.Tests;

[Collection(WinFormsTestCollection.Name)]
public sealed class LaunchAndMenuWorkflowTests
{
    [Fact]
    public void LaunchForm_InitializesStartControlAndKeepsItInsideViewportOnResize()
    {
        WinFormsTestHost.Run(() =>
        {
            using var form = new LaunchForm();
            var start = WinFormsTestHost.Field<Button>(form, "_startButton");

            Assert.Equal("START", start.Text);
            Assert.True(start.Enabled);
            Assert.True(form.ClientRectangle.Contains(start.Bounds));

            form.ClientSize = new Size(940, 600);
            WinFormsTestHost.Invoke(form, "PositionStartButton");

            Assert.True(form.ClientRectangle.Contains(start.Bounds));
            Assert.True(start.Width >= 128);
            Assert.True(start.Height >= 42);
        });
    }

    [Fact]
    public void MainMenu_ContainsEveryActionAndWrapsKeyboardSelection()
    {
        WinFormsTestHost.Run(() =>
        {
            using var form = new MainMenuForm();
            var hotspots = (System.Collections.IList)WinFormsTestHost.Field<object>(form, "_hotspots");

            Assert.Equal(Enum.GetValues<MenuAction>().Length, hotspots.Count);
            Assert.Equal(0, WinFormsTestHost.Field<int>(form, "_selectedIndex"));

            WinFormsTestHost.Invoke(form, "MoveSelection", -1);
            Assert.Equal(hotspots.Count - 1, WinFormsTestHost.Field<int>(form, "_selectedIndex"));

            WinFormsTestHost.Invoke(form, "MoveSelection", 1);
            Assert.Equal(0, WinFormsTestHost.Field<int>(form, "_selectedIndex"));
        });
    }

    [Theory]
    [InlineData(short.MinValue, 1)]
    [InlineData((short)-32767, 1)]
    [InlineData(short.MaxValue, -1)]
    [InlineData((short)0, 0)]
    public void MainMenu_MapsControllerStickToMenuDirection(short value, int expected)
    {
        var actual = WinFormsTestHost.InvokeStatic(typeof(MainMenuForm), "StickMenuDirection", value);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(MenuAction.StartDynasty, "Start Dynasty")]
    [InlineData(MenuAction.ContinueDynasty, "Continue Dynasty")]
    [InlineData(MenuAction.Game, "Game")]
    [InlineData(MenuAction.Teams, "Teams")]
    [InlineData(MenuAction.Seasons, "Seasons")]
    [InlineData(MenuAction.Replays, "Replays")]
    [InlineData(MenuAction.Settings, "Settings")]
    public void MainMenu_ProvidesStableLabelsForEveryAction(MenuAction action, string expected)
    {
        Assert.Equal(expected, WinFormsTestHost.InvokeStatic(typeof(MainMenuForm), "LabelFor", action));
    }
}
