#nullable enable annotations

using System;
using System.Linq;
using System.Windows.Forms;

namespace StandaloneBaseball
{
    internal static class MainMenuNavigationService
    {
        public static bool RequestReturn(Form source)
        {
            ArgumentNullException.ThrowIfNull(source);
            MainMenuForm? menu = Application.OpenForms
                .Cast<Form>()
                .OfType<MainMenuForm>()
                .FirstOrDefault(form => !form.IsDisposed);
            if (menu == null || menu.IsDisposed || !menu.IsHandleCreated)
                return false;

            menu.BeginInvoke(new Action(menu.ReturnFromChildScreens));
            return true;
        }
    }
}
