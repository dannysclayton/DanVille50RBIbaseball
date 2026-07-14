using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Windows.Forms;

namespace StandaloneBaseball
{
    public sealed class CutscenePlaybackForm : Form
    {
        private static readonly HashSet<string> ImageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".bmp", ".gif"
        };

        private static readonly HashSet<string> VideoExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".mp4", ".mov", ".m4v", ".avi", ".wmv", ".webm", ".mkv"
        };

        private readonly System.Windows.Forms.Timer _timer = new System.Windows.Forms.Timer();
        private readonly CutsceneDefinition _cutscene;
        private readonly string _resolvedPath;

        private CutscenePlaybackForm(CutsceneDefinition cutscene, string resolvedPath)
        {
            _cutscene = cutscene;
            _resolvedPath = resolvedPath;

            Text = cutscene?.Name ?? "Cutscene";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            ClientSize = new Size(900, 560);
            BackColor = Color.Black;

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1,
                Padding = new Padding(10),
                BackColor = Color.Black
            };
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            Controls.Add(root);

            root.Controls.Add(BuildMediaControl(resolvedPath), 0, 0);

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            var skip = new Button { Text = "Skip", AutoSize = true };
            skip.Click += (s, e) => Close();
            buttons.Controls.Add(skip);
            if (IsVideo(resolvedPath))
            {
                var external = new Button { Text = "Open Video", AutoSize = true };
                external.Click += (s, e) => OpenExternal(resolvedPath);
                buttons.Controls.Add(external);
            }
            root.Controls.Add(buttons, 0, 1);

            _timer.Interval = Math.Clamp((cutscene?.DurationSeconds ?? 5) * 1000, 1000, 120000);
            _timer.Tick += (s, e) =>
            {
                _timer.Stop();
                Close();
            };
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (!IsVideo(_resolvedPath))
                _timer.Start();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _timer.Dispose();
            base.Dispose(disposing);
        }

        public static bool PlayFirst(IWin32Window owner, IEnumerable<CutsceneDefinition> cutscenes, CutsceneTrigger trigger)
        {
            var cutscene = cutscenes?
                .Where(c => c != null && c.Enabled && c.Trigger == trigger && !string.IsNullOrWhiteSpace(c.MediaPath))
                .FirstOrDefault(c => AssetPathResolver.FileExists(c.MediaPath));
            if (cutscene == null)
                return false;

            using var form = new CutscenePlaybackForm(cutscene, ResolveMediaPath(cutscene.MediaPath));
            form.ShowDialog(owner);
            return true;
        }

        public static bool PlayPath(IWin32Window owner, string path, string name, int durationMilliseconds, bool blocking = true)
        {
            string resolved = AssetPathResolver.ResolvePath(path);
            if (string.IsNullOrWhiteSpace(resolved) || !File.Exists(resolved) || !IsSupportedMedia(resolved))
                return false;
            var cutscene = new CutsceneDefinition
            {
                Name = string.IsNullOrWhiteSpace(name) ? "Replay cutscene" : name,
                MediaPath = resolved,
                DurationSeconds = Math.Clamp((int)Math.Ceiling(Math.Max(1, durationMilliseconds) / 1000d), 1, 120),
                Enabled = true
            };
            var form = new CutscenePlaybackForm(cutscene, resolved);
            if (blocking)
            {
                using (form)
                    form.ShowDialog(owner);
            }
            else
            {
                form.FormClosed += (s, e) => form.Dispose();
                form.Show(owner);
            }
            return true;
        }

        public static string ResolveMediaPath(string mediaPath)
        {
            if (string.IsNullOrWhiteSpace(mediaPath))
                return "";
            return AssetPathResolver.ResolvePath(mediaPath);
        }

        public static bool IsSupportedMedia(string path)
            => IsImage(path) || IsVideo(path);

        public static bool IsImage(string path)
            => ImageExtensions.Contains(Path.GetExtension(path) ?? "");

        public static bool IsVideo(string path)
            => VideoExtensions.Contains(Path.GetExtension(path) ?? "");

        private Control BuildMediaControl(string path)
        {
            if (IsImage(path))
            {
                return new PictureBox
                {
                    Dock = DockStyle.Fill,
                    Image = LoadImage(path),
                    SizeMode = PictureBoxSizeMode.Zoom,
                    BackColor = Color.Black
                };
            }

            var browser = new WebBrowser
            {
                Dock = DockStyle.Fill,
                AllowWebBrowserDrop = false,
                IsWebBrowserContextMenuEnabled = false,
                WebBrowserShortcutsEnabled = false,
                ScriptErrorsSuppressed = true
            };
            browser.DocumentText = BuildVideoHtml(path);
            return browser;
        }

        private static Image LoadImage(string path)
        {
            using var source = Image.FromFile(path);
            return new Bitmap(source);
        }

        private static string BuildVideoHtml(string path)
        {
            string src = new Uri(path).AbsoluteUri;
            string title = WebUtility.HtmlEncode(Path.GetFileName(path));
            return "<!doctype html><html><head><meta http-equiv=\"X-UA-Compatible\" content=\"IE=edge\" />" +
                "<style>html,body{margin:0;height:100%;background:#000;color:#fff;font-family:Segoe UI,Arial,sans-serif;}video{width:100%;height:100%;object-fit:contain;background:#000}.fallback{position:absolute;left:16px;bottom:16px;background:rgba(0,0,0,.65);padding:8px 10px;border-radius:4px}</style>" +
                "</head><body><video autoplay controls><source src=\"" + src + "\"></video><div class=\"fallback\">" + title + "</div></body></html>";
        }

        private static void OpenExternal(string path)
        {
            if (!File.Exists(path))
                return;
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }
    }
}
