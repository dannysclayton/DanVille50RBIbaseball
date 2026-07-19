#nullable enable annotations

using System.Windows.Forms;

namespace StandaloneBaseball.Tests;

[Collection(WinFormsTestCollection.Name)]
public sealed class ContextHelpServiceTests
{
    [Fact]
    public void Attach_AddsOneAnchoredHelpButtonToMenuForm()
    {
        WinFormsTestHost.Run(() =>
        {
            using var form = new Form { Text = "Test Menu", ClientSize = new System.Drawing.Size(900, 600) };

            Assert.True(ContextHelpService.Attach(form));
            Assert.False(ContextHelpService.Attach(form));

            Button help = Assert.Single(form.Controls.OfType<Button>(),
                button => button.Name == ContextHelpService.HelpButtonName);
            Assert.Contains("Help", help.Text, StringComparison.OrdinalIgnoreCase);
            Assert.True(help.Anchor.HasFlag(AnchorStyles.Top));
            Assert.True(help.Anchor.HasFlag(AnchorStyles.Right));
        });
    }

    [Fact]
    public void Builder_ExplainsControlsAcrossEveryTabIncludingUnavailableSettings()
    {
        WinFormsTestHost.Run(() =>
        {
            using var form = new Form { Text = "Settings" };
            var tabs = new TabControl();
            var general = new TabPage("General");
            var audio = new TabPage("Audio");
            tabs.TabPages.Add(general);
            tabs.TabPages.Add(audio);
            form.Controls.Add(tabs);

            general.Controls.Add(new CheckBox { Text = "Rotate uniforms" });
            general.Controls.Add(new ComboBox { AccessibleName = "National anthem default" });
            general.Controls.Add(new NumericUpDown { AccessibleName = "Innings", Minimum = 5, Maximum = 9 });
            audio.Controls.Add(new TrackBar { AccessibleName = "Sound effects volume", Minimum = 0, Maximum = 100 });
            audio.Controls.Add(new Button { Text = "Import music", Enabled = false });
            audio.Controls.Add(new DataGridView { AccessibleName = "Team playlist", ReadOnly = true });

            IReadOnlyList<ContextHelpItem> items = ContextHelpContentBuilder.Build(form);

            Assert.Contains(items, item => item.ControlName == "Tab: General");
            Assert.Contains(items, item => item.ControlName == "Tab: Audio");
            Assert.Contains(items, item => item.ControlName == "Rotate uniforms" && item.Instructions.Contains("enable", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(items, item => item.ControlName == "National anthem default" && item.Instructions.Contains("choose", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(items, item => item.ControlName == "Innings" && item.Instructions.Contains("5 through 9", StringComparison.Ordinal));
            Assert.Contains(items, item => item.ControlName == "Sound effects volume" && item.Instructions.Contains("slider", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(items, item => item.ControlName == "Import music" && item.Instructions.Contains("currently unavailable", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(items, item => item.ControlName == "Team playlist" && item.Instructions.Contains("table", StringComparison.OrdinalIgnoreCase));
        });
    }

    [Fact]
    public void Builder_IncludesPaintedMainMenuHotspots()
    {
        WinFormsTestHost.Run(() =>
        {
            using var form = new MainMenuForm();
            IReadOnlyList<ContextHelpItem> items = ContextHelpContentBuilder.Build(form);

            Assert.Contains(items, item => item.ControlName == "Start Dynasty");
            Assert.Contains(items, item => item.ControlName == "Continue Dynasty");
            Assert.Contains(items, item => item.ControlName == "Game");
            Assert.Contains(items, item => item.ControlName == "Teams");
            Assert.Contains(items, item => item.ControlName == "Seasons");
            Assert.Contains(items, item => item.ControlName == "Replays");
            Assert.Contains(items, item => item.ControlName == "Settings");
        });
    }
}
