using System;
using System.Windows.Forms;

namespace StandaloneBaseball
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
#if PUBLIC_RELEASE
            PublicReleaseExceptionHandler.Install();
#else
            LocalV2ExceptionHandler.Install();
#endif
            try
            {
                if (args.Length == 3 && string.Equals(args[0], "--import-rom", StringComparison.OrdinalIgnoreCase))
                {
                    var league = RomSnapshotImporter.Import(args[1]);
                    LeagueStore.Save(args[2], league);
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new LaunchForm());
            }
            catch (Exception ex)
            {
#if PUBLIC_RELEASE
                PublicReleaseExceptionHandler.HandleStartupFailure(ex);
#else
                LocalV2ExceptionHandler.HandleStartupFailure(ex);
#endif
            }
        }
    }
}
