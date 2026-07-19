#nullable enable annotations

using System.Windows.Forms;

namespace StandaloneBaseball.Tests;

[Collection(WinFormsTestCollection.Name)]
public sealed class MainMenuNavigationTests
{
    [Fact]
    public void ReturningFromChildScreen_ClosesChildAndRestoresMainMenu()
    {
        WinFormsTestHost.Run(() =>
        {
            using var menu = new MainMenuForm();
            var child = new Form { Text = "Team Editor" };
            menu.Show();
            child.Show();
            menu.Hide();

            menu.ReturnFromChildScreens();

            Assert.True(menu.Visible);
            Assert.True(child.IsDisposed);
        });
    }
}
