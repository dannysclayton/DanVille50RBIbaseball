using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace StandaloneBaseball
{
    internal sealed class NationalAnthemForm : Form
    {
        private readonly Team _awayTeam;
        private readonly Team _homeTeam;
        private readonly string _anthemPath;
        private readonly LaunchSoundPlayer _sound = new LaunchSoundPlayer();
        private readonly LaunchSoundPlayer _lineupSound = new LaunchSoundPlayer();
        private readonly System.Windows.Forms.Timer _timer;
        private readonly Panel _fieldPanel;
        private readonly Button _continueButton;
        private readonly List<Player> _awayLineup;
        private readonly List<Player> _homeLineup;
        private Image _flagsImage;
        private int _homePlayersOnLine;
        private bool _anthemStarted;

        public NationalAnthemForm(
            Team awayTeam,
            Team homeTeam,
            IEnumerable<string> awayImagePaths,
            IEnumerable<string> homeImagePaths,
            string anthemPath)
        {
            _awayTeam = awayTeam;
            _homeTeam = homeTeam;
            _anthemPath = anthemPath;
            _awayLineup = BuildLineup(awayTeam);
            _homeLineup = BuildLineup(homeTeam);
            _flagsImage = LoadImage(FindFlagsImage());

            Text = "National Anthem";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(1040, 690);
            MinimumSize = new Size(900, 600);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            DoubleBuffered = true;

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1,
                BackColor = Color.FromArgb(12, 18, 32)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
            Controls.Add(root);

            _fieldPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(19, 42, 32)
            };
            _fieldPanel.Paint += PaintCeremony;
            root.Controls.Add(_fieldPanel, 0, 0);

            var bottom = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(8),
                BackColor = Color.FromArgb(15, 23, 42)
            };
            _continueButton = new Button { Text = "Start First Pitch", AutoSize = true, Margin = new Padding(4), Enabled = false };
            _continueButton.Click += (s, e) =>
            {
                DialogResult = DialogResult.OK;
                Close();
            };
            bottom.Controls.Add(_continueButton);
            root.Controls.Add(bottom, 0, 1);
            AcceptButton = _continueButton;

            _timer = new System.Windows.Forms.Timer { Interval = 850 };
            _timer.Tick += (s, e) => AdvanceCeremony();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _timer.Start();
            _fieldPanel.Invalidate();
        }

        private void AdvanceCeremony()
        {
            _timer.Stop();
            if (!_anthemStarted && _homePlayersOnLine < _homeLineup.Count)
            {
                Player player = _homeLineup[_homePlayersOnLine];
                _homePlayersOnLine++;
                _lineupSound.PlayOnce(LaunchSoundPlayer.FindLineupPositionCall(DisplayPosition(player)));
                _fieldPanel.Invalidate();
                _timer.Interval = 900;
                _timer.Start();
                return;
            }

            if (!_anthemStarted)
            {
                _anthemStarted = true;
                _fieldPanel.Invalidate();
                _sound.PlayOnce(_anthemPath);
                _timer.Interval = Math.Clamp(LaunchSoundPlayer.GetDurationMilliseconds(_anthemPath, 90000) + 250, 1000, 600000);
                _timer.Start();
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        }

        private void PaintCeremony(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            Rectangle bounds = _fieldPanel.ClientRectangle;
            if (bounds.Width <= 0 || bounds.Height <= 0)
                return;

            DrawSkyAndOutfield(g, bounds);
            DrawField(g, bounds, out PointF home, out PointF first, out PointF third);
            DrawFlagDisplay(g, bounds);
            DrawTeamLine(g, _awayTeam, _awayLineup, third, home, _awayLineup.Count, leftSide: true);
            DrawTeamLine(g, _homeTeam, _homeLineup, home, first, _homePlayersOnLine, leftSide: false);
            DrawCeremonyText(g, bounds);
        }

        private void DrawSkyAndOutfield(Graphics g, Rectangle bounds)
        {
            using (var sky = new LinearGradientBrush(new Rectangle(bounds.Left, bounds.Top, bounds.Width, Math.Max(1, bounds.Height / 2)),
                Color.FromArgb(184, 215, 239), Color.FromArgb(234, 242, 250), LinearGradientMode.Vertical))
            {
                g.FillRectangle(sky, bounds.Left, bounds.Top, bounds.Width, bounds.Height / 2);
            }

            int wallY = bounds.Top + bounds.Height * 30 / 100;
            using (var trees = new SolidBrush(Color.FromArgb(64, 92, 58)))
                g.FillRectangle(trees, bounds.Left, wallY - 24, bounds.Width, 52);
            using (var wall = new SolidBrush(Color.FromArgb(20, 43, 67)))
                g.FillRectangle(wall, bounds.Left, wallY, bounds.Width, 34);
            using (var stripe = new SolidBrush(Color.FromArgb(244, 196, 48)))
                g.FillRectangle(stripe, bounds.Left, wallY, bounds.Width, 4);
            using (var grass = new LinearGradientBrush(new Rectangle(bounds.Left, wallY + 34, bounds.Width, bounds.Height - wallY - 34),
                Color.FromArgb(46, 129, 55), Color.FromArgb(34, 103, 46), LinearGradientMode.Vertical))
            {
                g.FillRectangle(grass, bounds.Left, wallY + 34, bounds.Width, bounds.Height - wallY - 34);
            }
        }

        private void DrawField(Graphics g, Rectangle bounds, out PointF home, out PointF first, out PointF third)
        {
            home = new PointF(bounds.Left + bounds.Width * 0.50f, bounds.Top + bounds.Height * 0.86f);
            first = new PointF(bounds.Left + bounds.Width * 0.76f, bounds.Top + bounds.Height * 0.68f);
            PointF second = new PointF(bounds.Left + bounds.Width * 0.50f, bounds.Top + bounds.Height * 0.50f);
            third = new PointF(bounds.Left + bounds.Width * 0.24f, bounds.Top + bounds.Height * 0.68f);

            using var dirt = new SolidBrush(Color.FromArgb(170, 111, 61));
            using var line = new Pen(Color.White, 4f);
            using var thin = new Pen(Color.FromArgb(210, Color.White), 2f);
            PointF[] infield = { home, first, second, third };
            g.FillPolygon(dirt, infield);
            g.DrawPolygon(thin, infield);
            g.DrawLine(line, home, first);
            g.DrawLine(line, home, third);
            g.DrawArc(line, bounds.Left + bounds.Width * 0.15f, bounds.Top + bounds.Height * 0.34f, bounds.Width * 0.70f, bounds.Height * 0.44f, 202, 136);
            DrawBase(g, home, 12);
            DrawBase(g, first, 10);
            DrawBase(g, second, 10);
            DrawBase(g, third, 10);
        }

        private void DrawFlagDisplay(Graphics g, Rectangle bounds)
        {
            var area = new Rectangle(bounds.Left + bounds.Width * 31 / 100, bounds.Top + bounds.Height * 5 / 100, bounds.Width * 38 / 100, bounds.Height * 25 / 100);
            using var shadow = new SolidBrush(Color.FromArgb(80, Color.Black));
            g.FillRectangle(shadow, area.X + 5, area.Y + 6, area.Width, area.Height);
            using var frame = new SolidBrush(Color.FromArgb(244, 246, 250));
            g.FillRectangle(frame, area);
            using var border = new Pen(Color.FromArgb(40, 55, 74), 3f);
            g.DrawRectangle(border, area);

            if (_flagsImage != null)
                g.DrawImage(_flagsImage, FitImage(_flagsImage.Size, Rectangle.Inflate(area, -5, -5)));
            else
                TextRenderer.DrawText(g, "FLAGS", new Font(Font.FontFamily, 28, FontStyle.Bold), area, Color.FromArgb(20, 24, 32), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        private void DrawTeamLine(Graphics g, Team team, List<Player> players, PointF start, PointF end, int visibleCount, bool leftSide)
        {
            if (team == null || players == null || players.Count == 0 || visibleCount <= 0)
                return;

            visibleCount = Math.Min(visibleCount, players.Count);
            Color primary = Color.FromArgb(team.PrimaryArgb);
            Color secondary = Color.FromArgb(team.SecondaryArgb);
            for (int i = 0; i < visibleCount; i++)
            {
                float t = visibleCount == 1 ? 0.15f : 0.10f + (0.80f * i / Math.Max(1, players.Count - 1));
                PointF p = Lerp(start, end, leftSide ? t : 1f - t);
                p.Y -= 16;
                DrawPlayer(g, players[i], p, primary, secondary);
            }
        }

        private void DrawPlayer(Graphics g, Player player, PointF feet, Color primary, Color secondary)
        {
            using var shadow = new SolidBrush(Color.FromArgb(70, Color.Black));
            g.FillEllipse(shadow, feet.X - 14, feet.Y + 10, 28, 8);
            using var pants = new SolidBrush(Color.White);
            using var jersey = new SolidBrush(primary);
            using var cap = new SolidBrush(secondary);
            using var skin = new SolidBrush(Color.FromArgb(226, 184, 142));
            using var outline = new Pen(Color.FromArgb(28, 34, 44), 1.5f);

            g.FillRectangle(pants, feet.X - 7, feet.Y - 17, 5, 22);
            g.FillRectangle(pants, feet.X + 2, feet.Y - 17, 5, 22);
            g.FillEllipse(jersey, feet.X - 13, feet.Y - 42, 26, 28);
            g.DrawEllipse(outline, feet.X - 13, feet.Y - 42, 26, 28);
            g.FillEllipse(skin, feet.X - 8, feet.Y - 58, 16, 16);
            g.DrawEllipse(outline, feet.X - 8, feet.Y - 58, 16, 16);
            g.FillRectangle(cap, feet.X - 10, feet.Y - 61, 20, 7);
            g.DrawRectangle(outline, feet.X - 10, feet.Y - 61, 20, 7);

            string name = ShortName(player);
            TextRenderer.DrawText(g, name, new Font(Font.FontFamily, 7f, FontStyle.Bold), new Rectangle((int)feet.X - 42, (int)feet.Y + 16, 84, 18),
                Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.EndEllipsis);
        }

        private void DrawCeremonyText(Graphics g, Rectangle bounds)
        {
            string top = _anthemStarted ? "Please rise for the National Anthem" : "Home team introductions";
            string bottom = _anthemStarted
                ? (_awayTeam?.DisplayName ?? "Visitors") + " and " + (_homeTeam?.DisplayName ?? "Home") + " are lined up for the anthem"
                : "Visitors are on the third-base line. Home players are taking the first-base line.";

            using var bg = new SolidBrush(Color.FromArgb(190, 8, 16, 32));
            var textArea = new Rectangle(bounds.Left + 20, bounds.Bottom - 84, bounds.Width - 40, 66);
            g.FillRoundedRectangle(bg, textArea, 12);
            TextRenderer.DrawText(g, top, new Font(Font.FontFamily, 18f, FontStyle.Bold), new Rectangle(textArea.Left, textArea.Top + 8, textArea.Width, 28),
                Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(g, bottom, new Font(Font.FontFamily, 10f, FontStyle.Regular), new Rectangle(textArea.Left + 12, textArea.Top + 38, textArea.Width - 24, 20),
                Color.FromArgb(225, Color.White), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        private static void DrawBase(Graphics g, PointF point, int size)
        {
            PointF[] diamond =
            {
                new PointF(point.X, point.Y - size),
                new PointF(point.X + size, point.Y),
                new PointF(point.X, point.Y + size),
                new PointF(point.X - size, point.Y)
            };
            using var brush = new SolidBrush(Color.White);
            g.FillPolygon(brush, diamond);
        }

        private static List<Player> BuildLineup(Team team)
            => LineupEngine.GetBattingOrder(team).ToList();

        private static string DisplayPosition(Player player)
        {
            if (player == null)
                return "P";
            string[] parts = (player.Positions ?? "")
                .Split(new[] { '/', ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0)
                return parts[0].Trim().ToUpperInvariant();
            return player.Role == PlayerRole.Pitcher ? "P" : "1B";
        }

        private static string ShortName(Player player)
        {
            if (player == null || string.IsNullOrWhiteSpace(player.Name))
                return "";
            string[] parts = player.Name.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1)
                return parts[0].ToUpperInvariant();
            return parts[^1].ToUpperInvariant();
        }

        private static PointF Lerp(PointF a, PointF b, float t)
            => new PointF(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);

        private static Rectangle FitImage(Size image, Rectangle bounds)
        {
            if (image.Width <= 0 || image.Height <= 0) return bounds;
            double scale = Math.Min(bounds.Width / (double)image.Width, bounds.Height / (double)image.Height);
            int w = Math.Max(1, (int)Math.Round(image.Width * scale));
            int h = Math.Max(1, (int)Math.Round(image.Height * scale));
            return new Rectangle(bounds.Left + (bounds.Width - w) / 2, bounds.Top + (bounds.Height - h) / 2, w, h);
        }

        private static string FindFlagsImage()
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Assets", "National Anthem", "flags.png");
            return File.Exists(path) ? path : "";
        }

        private static Image LoadImage(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return null;
            try
            {
                using var source = Image.FromFile(path);
                return new Bitmap(source);
            }
            catch
            {
                return null;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _timer?.Stop();
                _timer?.Dispose();
                _sound.Dispose();
                _lineupSound.Dispose();
                _flagsImage?.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
