#nullable enable annotations

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace StandaloneBaseball
{
    internal static class PublicReleaseExceptionHandler
    {
        private static int _handling;

        public static void Install()
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (sender, args) => HandleUiException(args.Exception);
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
                HandleFatalException(args.ExceptionObject as Exception ?? new Exception(Convert.ToString(args.ExceptionObject)), "Background fatal exception");
            TaskScheduler.UnobservedTaskException += (sender, args) =>
            {
                WriteLog(args.Exception, "Unobserved task exception");
                args.SetObserved();
            };
        }

        public static void HandleStartupFailure(Exception exception)
            => HandleFatalException(exception, "Application startup failure");

        internal static string WriteLog(Exception exception, string context, string? logRoot = null)
        {
            exception ??= new Exception("Unknown application error.");
            string root = string.IsNullOrWhiteSpace(logRoot)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "DanVille50", "Dan's RBI Baseball 2026", "Logs", "Public")
                : Path.GetFullPath(logRoot);
            try
            {
                Directory.CreateDirectory(root);
                string path = Path.Combine(root, "error-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N")[..8] + ".log");
                File.WriteAllText(path, BuildLog(exception, context));
                return path;
            }
            catch
            {
                return "";
            }
        }

        private static void HandleUiException(Exception exception)
        {
            if (Interlocked.Exchange(ref _handling, 1) != 0)
                return;
            try
            {
                string log = WriteLog(exception, "Windows Forms UI exception");
                DialogResult result = MessageBox.Show(
                    "Dan's RBI Baseball 2026 recovered from an unexpected error.\n\n" +
                    "Your dynasty files were not intentionally changed. Save your work under a new filename before continuing.\n\n" +
                    LogMessage(log) + "\n\nContinue running the application?",
                    "Application Error",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Error,
                    MessageBoxDefaultButton.Button2);
                if (result != DialogResult.Yes)
                    Application.Exit();
            }
            catch
            {
                Application.Exit();
            }
            finally
            {
                Interlocked.Exchange(ref _handling, 0);
            }
        }

        private static void HandleFatalException(Exception exception, string context)
        {
            if (Interlocked.Exchange(ref _handling, 1) != 0)
                return;
            try
            {
                string log = WriteLog(exception, context);
                MessageBox.Show(
                    "Dan's RBI Baseball 2026 encountered a fatal error and must close.\n\n" + LogMessage(log),
                    "Fatal Application Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            catch
            {
                // The operating system will finish termination if even the recovery UI is unavailable.
            }
            finally
            {
                Interlocked.Exchange(ref _handling, 0);
            }
        }

        private static string LogMessage(string path)
            => string.IsNullOrWhiteSpace(path)
                ? "The error log could not be written."
                : "Error details were written to:\n" + path;

        private static string BuildLog(Exception exception, string context)
        {
            var text = new StringBuilder();
            text.AppendLine("Dan's RBI Baseball 2026 - Public Version 1.0");
            text.AppendLine("UTC: " + DateTime.UtcNow.ToString("O"));
            text.AppendLine("Context: " + (context ?? ""));
            text.AppendLine("OS: " + Environment.OSVersion);
            text.AppendLine("Runtime: " + Environment.Version);
            text.AppendLine();
            text.AppendLine(exception.ToString());
            return text.ToString();
        }
    }
}
