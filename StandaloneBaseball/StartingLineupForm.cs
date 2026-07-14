#nullable enable annotations

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;

#nullable enable annotations

namespace StandaloneBaseball
{
    internal sealed class StartingLineupForm : Form
    {
        private static readonly string[] PositionOrder = { "C", "P", "1B", "2B", "3B", "SS", "LF", "CF", "RF" };

        private readonly Team _team;
        private readonly string _logoPath;
        private readonly bool _homeTeam;
        private readonly TeamUniformSet? _uniformSet;
        private readonly LineupCard _lineupCard;
        private readonly List<PlayerSpot> _spots;
        private readonly List<Player> _battingOrder;
        private readonly Dictionary<string, Image?> _imageCache = new Dictionary<string, Image?>(StringComparer.OrdinalIgnoreCase);
        private readonly LaunchSoundPlayer _sound = new LaunchSoundPlayer();
        private readonly System.Windows.Forms.Timer _timer;
        private int _activeIndex = -1;
        private bool _closing;

        public StartingLineupForm(Team team, string logoPath, bool homeTeam, TeamUniformSet? uniformSet = null)
        {
            _team = team ?? throw new ArgumentNullException(nameof(team));
            _logoPath = logoPath ?? "";
            _homeTeam = homeTeam;
            _uniformSet = uniformSet;
            _lineupCard = LineupEngine.BuildLineupCard(team);
            _battingOrder = _lineupCard.BattingOrder.Select(s => s.Player).Where(p => p != null).ToList();
            _spots = BuildSpots(_lineupCard);

            Text = (homeTeam ? "Home" : "Visitor") + " Starting Lineup";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(960, 620);
            MinimumSize = new Size(860, 560);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            DoubleBuffered = true;

            _timer = new System.Windows.Forms.Timer { Interval = 900 };
            _timer.Tick += (s, e) => AdvancePositionCall();

            var skip = new Button
            {
                Text = "Continue",
                Width = 110,
                Height = 30,
                Anchor = AnchorStyles.Right | AnchorStyles.Bottom,
                Location = new Point(ClientSize.Width - 126, ClientSize.Height - 42)
            };
            skip.Click += (s, e) =>
            {
                DialogResult = DialogResult.OK;
                Close();
            };
            Controls.Add(skip);
            AcceptButton = skip;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            AdvancePositionCall();
        }

        private void AdvancePositionCall()
        {
            if (_closing)
                return;

            _timer.Stop();
            _activeIndex++;
            if (_activeIndex >= PositionOrder.Length)
            {
                _closing = true;
                DialogResult = DialogResult.OK;
                Close();
                return;
            }

            string position = PositionOrder[_activeIndex];
            string path = LaunchSoundPlayer.FindLineupPositionCall(position);
            _sound.PlayOnce(path);
            Invalidate();
            _timer.Interval = Math.Clamp(LaunchSoundPlayer.GetDurationMilliseconds(path, 900) + 350, 700, 5000);
            _timer.Start();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            Rectangle bounds = ClientRectangle;
            DrawBackground(g, bounds);
            DrawTitle(g, bounds);

            Rectangle orderRect = new Rectangle(34, 145, 276, bounds.Height - 194);
            Rectangle fieldRect = new Rectangle(330, 138, bounds.Width - 368, bounds.Height - 188);
            DrawBattingOrder(g, orderRect);
            DrawLogo(g, new Rectangle(fieldRect.Left + fieldRect.Width / 2 - 58, 88, 116, 60));
            DrawField(g, fieldRect);
            DrawPlayers(g, fieldRect);
        }

        private void DrawBackground(Graphics g, Rectangle bounds)
        {
            Color top = _homeTeam ? Color.FromArgb(6, 48, 22) : Color.FromArgb(4, 4, 5);
            Color bottom = _homeTeam ? Color.FromArgb(18, 88, 39) : Color.FromArgb(18, 18, 21);
            using var bg = new LinearGradientBrush(bounds, top, bottom, LinearGradientMode.Vertical);
            g.FillRectangle(bg, bounds);

            using var vignette = new GraphicsPath();
            vignette.AddEllipse(new Rectangle(-bounds.Width / 3, -bounds.Height / 4, bounds.Width * 5 / 3, bounds.Height * 3 / 2));
            using var pathBrush = new PathGradientBrush(vignette)
            {
                CenterColor = Color.FromArgb(_homeTeam ? 18 : 8, Color.White),
                SurroundColors = new[] { Color.FromArgb(120, Color.Black) }
            };
            g.FillRectangle(pathBrush, bounds);

            if (_homeTeam)
            {
                using var ballPen = new Pen(Color.FromArgb(32, Color.White), 4f);
                g.DrawArc(ballPen, -130, 300, 420, 420, 292, 120);
                g.DrawArc(ballPen, -145, 300, 420, 420, 292, 120);
            }
        }

        private void DrawTitle(Graphics g, Rectangle bounds)
        {
            using var titleFont = new Font("Arial Black", 42f, FontStyle.Bold);
            using var subFont = new Font("Segoe UI", 11f, FontStyle.Bold);
            string title = "STARTING LINEUP";
            TextRenderer.DrawText(g, title, titleFont, new Rectangle(0, 20, bounds.Width, 62), Color.FromArgb(225, Color.White), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(g, _homeTeam ? "HOME TEAM" : "VISITING TEAM", subFont, new Rectangle(0, 82, bounds.Width, 24), Color.FromArgb(210, Color.White), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        private void DrawBattingOrder(Graphics g, Rectangle rect)
        {
            using var headerFont = new Font("Arial Black", 19f, FontStyle.Bold);
            using var rowFont = new Font("Consolas", 12f, FontStyle.Bold);
            using var smallFont = new Font("Consolas", 10f, FontStyle.Bold);
            using var linePen = new Pen(Color.FromArgb(90, Color.White), 1.5f);

            TextRenderer.DrawText(g, "BATTING ORDER", headerFont, new Rectangle(rect.Left, rect.Top, rect.Width, 42), Color.FromArgb(225, Color.White), TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            g.DrawLine(linePen, rect.Left, rect.Top + 43, rect.Left + 205, rect.Top + 43);
            g.DrawLine(linePen, rect.Right - 4, rect.Top + 18, rect.Right - 4, rect.Bottom - 10);

            for (int i = 0; i < _battingOrder.Count && i < 9; i++)
            {
                Player player = _battingOrder[i];
                string pos = PrimaryDisplayPosition(player);
                Rectangle row = new Rectangle(rect.Left, rect.Top + 72 + i * 32, rect.Width - 16, 28);
                string number = (i + 1).ToString().PadLeft(2, '0');
                TextRenderer.DrawText(g, number, rowFont, new Rectangle(row.Left, row.Top, 30, row.Height), Color.White, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                TextRenderer.DrawText(g, ShortName(player), rowFont, new Rectangle(row.Left + 38, row.Top, row.Width - 74, row.Height), Color.White, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                TextRenderer.DrawText(g, pos, smallFont, new Rectangle(row.Right - 38, row.Top + 2, 36, row.Height), Color.White, TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
            }
        }

        private void DrawLogo(Graphics g, Rectangle rect)
        {
            Image logo = LoadImage(_logoPath);
            if (logo != null)
            {
                DrawImageContained(g, logo, rect);
                return;
            }

            Color secondary = Color.FromArgb(_team.SecondaryArgb);
            using var brush = new SolidBrush(secondary);
            using var border = new Pen(Color.White, 2f);
            g.FillEllipse(brush, rect);
            g.DrawEllipse(border, rect);
            using var font = new Font("Arial Black", 17f, FontStyle.Bold);
            TextRenderer.DrawText(g, _team.ScoreboardName, font, rect, ReadableTextColor(secondary), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        private void DrawField(Graphics g, Rectangle rect)
        {
            using var linePen = new Pen(Color.White, 3f);
            using var thinPen = new Pen(Color.FromArgb(170, Color.White), 1.4f);
            using var dirt = new SolidBrush(Color.FromArgb(_homeTeam ? 122 : 235, _homeTeam ? 88 : 235, _homeTeam ? 48 : 235));

            PointF home = Pt(rect, 0.50f, 0.86f);
            PointF first = Pt(rect, 0.73f, 0.66f);
            PointF second = Pt(rect, 0.50f, 0.45f);
            PointF third = Pt(rect, 0.27f, 0.66f);

            g.DrawLine(linePen, home, first);
            g.DrawLine(linePen, first, second);
            g.DrawLine(linePen, second, third);
            g.DrawLine(linePen, third, home);
            g.DrawArc(linePen, rect.Left + rect.Width * 0.05f, rect.Top + rect.Height * 0.10f, rect.Width * 0.90f, rect.Height * 0.70f, 200, 140);
            g.DrawBezier(thinPen, third, Pt(rect, 0.20f, 0.58f), Pt(rect, 0.20f, 0.32f), Pt(rect, 0.10f, 0.28f));
            g.DrawBezier(thinPen, first, Pt(rect, 0.80f, 0.58f), Pt(rect, 0.80f, 0.32f), Pt(rect, 0.90f, 0.28f));

            g.FillEllipse(dirt, rect.Left + rect.Width * 0.45f, rect.Top + rect.Height * 0.60f, rect.Width * 0.10f, rect.Height * 0.08f);
            DrawBase(g, home);
            DrawBase(g, first);
            DrawBase(g, second);
            DrawBase(g, third);
        }

        private void DrawPlayers(Graphics g, Rectangle rect)
        {
            for (int i = 0; i < _spots.Count; i++)
            {
                PlayerSpot spot = _spots[i];
                bool active = _activeIndex >= 0 && _activeIndex < PositionOrder.Length && PositionOrder[_activeIndex] == spot.Position;
                PointF point = Pt(rect, spot.X, spot.Y);
                DrawPlayerSprite(g, spot.Player, point, active);
                DrawPlayerLabel(g, spot, point, active);
            }
        }

        private void DrawPlayerSprite(Graphics g, Player player, PointF center, bool active)
        {
            float size = active ? 78f : 62f;
            Image sprite = LoadSprite(player);
            RectangleF dest = new RectangleF(center.X - size / 2f, center.Y - size / 2f, size, size);
            using var shadow = new SolidBrush(Color.FromArgb(115, Color.Black));
            g.FillEllipse(shadow, center.X - size / 2f + 4, center.Y + size / 2f - 11, size, 14);

            if (sprite != null)
            {
                Rectangle src = new Rectangle(0, 0, Math.Min(SpriteSheetGeneratorOptions.FrameWidth, sprite.Width), Math.Min(SpriteSheetGeneratorOptions.FrameHeight, sprite.Height));
                g.DrawImage(sprite, dest, src, GraphicsUnit.Pixel);
            }
            else
            {
                DrawGeneratedPlayer(g, dest, player);
            }

            using var ring = new Pen(active ? Color.Gold : Color.FromArgb(205, Color.White), active ? 4f : 1.6f);
            g.DrawEllipse(ring, dest);
        }

        private void DrawPlayerLabel(Graphics g, PlayerSpot spot, PointF point, bool active)
        {
            using var nameFont = new Font("Arial Black", active ? 8.4f : 7.3f, FontStyle.Bold);
            using var posFont = new Font("Arial Black", 7f, FontStyle.Bold);
            Rectangle nameRect = new Rectangle((int)point.X - 54, (int)point.Y + 30, 108, 26);
            string name = ShortName(spot.Player);
            TextRenderer.DrawText(g, name, nameFont, Offset(nameRect, 1, 1), Color.Black, TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(g, name, nameFont, nameRect, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.EndEllipsis);
            Rectangle posRect = new Rectangle(nameRect.Left, nameRect.Bottom - 5, nameRect.Width, 16);
            TextRenderer.DrawText(g, spot.Position, posFont, posRect, active ? Color.Gold : Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.Top);
        }

        private Image? LoadSprite(Player? player)
        {
            string path = FirstExistingPath(player?.SpriteSheetPath, _team?.SpriteSheetPath);
            if (string.IsNullOrWhiteSpace(path))
                return null;

            return LoadImage(path);
        }

        private Image? LoadImage(string? rawPath)
        {
            string path = ResolvePath(rawPath);
            if (string.IsNullOrWhiteSpace(path))
                return null;

            if (_imageCache.TryGetValue(path, out Image? cached))
                return cached;

            try
            {
                using Image source = Image.FromFile(path);
                Image image = new Bitmap(source);
                _imageCache[path] = image;
                return image;
            }
            catch
            {
                _imageCache[path] = null;
                return null;
            }
        }

        private static List<PlayerSpot> BuildSpots(LineupCard card)
        {
            var spots = new List<PlayerSpot>();
            AddSpot("C", 0.50f, 0.86f, card);
            AddSpot("P", 0.50f, 0.66f, card);
            AddSpot("1B", 0.72f, 0.64f, card);
            AddSpot("2B", 0.58f, 0.49f, card);
            AddSpot("3B", 0.31f, 0.64f, card);
            AddSpot("SS", 0.42f, 0.51f, card);
            AddSpot("LF", 0.24f, 0.34f, card);
            AddSpot("CF", 0.50f, 0.25f, card);
            AddSpot("RF", 0.78f, 0.34f, card);
            return spots;

            void AddSpot(string position, float x, float y, LineupCard lineupCard)
            {
                lineupCard.DefensiveAssignments.TryGetValue(position, out Player player);
                spots.Add(new PlayerSpot { Position = position, Player = player, X = x, Y = y });
            }
        }

        private static HashSet<string> PositionParts(Player player)
            => new HashSet<string>((player?.Positions ?? "").ToUpperInvariant().Split(new[] { '/', ',', ' ' }, StringSplitOptions.RemoveEmptyEntries));

        private static string PrimaryDisplayPosition(Player player)
        {
            if (player == null)
                return "";
            string positions = (player.Positions ?? "").ToUpperInvariant();
            if (positions.Contains("P") && player.Role == PlayerRole.Pitcher)
                return "P";
            string first = positions.Split(new[] { '/', ',', ' ' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            return string.IsNullOrWhiteSpace(first) ? (player.Role == PlayerRole.Pitcher ? "P" : "DH") : first;
        }

        private static string ShortName(Player player)
        {
            if (player == null || string.IsNullOrWhiteSpace(player.Name))
                return "PLAYER";
            return player.Name.Trim().ToUpperInvariant();
        }

        private void DrawGeneratedPlayer(Graphics g, RectangleF dest, Player player)
        {
            TeamUniformSet? uniform = _uniformSet ?? GameUniformResolver.ResolveUniform(_team, _homeTeam, null);
            Color jersey = player?.JerseyColor(_team, uniform) ?? Color.FromArgb(uniform?.JerseyArgb ?? _team.PrimaryArgb);
            Color pants = player?.PantsColor(_team, uniform) ?? Color.FromArgb(uniform?.PantsArgb ?? Color.White.ToArgb());
            Color cap = player?.CapHelmetColor(_team, uniform) ?? Color.FromArgb(uniform?.CapHelmetArgb ?? _team.SecondaryArgb);
            float cx = dest.Left + dest.Width / 2f;
            float top = dest.Top + dest.Height * 0.12f;
            float scale = dest.Width / 64f;

            using var skin = new SolidBrush(Color.FromArgb(235, 198, 154));
            using var jerseyBrush = new SolidBrush(jersey);
            using var pantsPen = new Pen(pants, 5f * scale);
            using var capBrush = new SolidBrush(cap);
            using var outline = new Pen(Color.FromArgb(215, 20, 24, 32), 2f * scale);

            g.FillEllipse(skin, cx - 8 * scale, top + 6 * scale, 16 * scale, 16 * scale);
            g.DrawEllipse(outline, cx - 8 * scale, top + 6 * scale, 16 * scale, 16 * scale);
            g.FillPie(capBrush, cx - 9 * scale, top + 3 * scale, 18 * scale, 14 * scale, 180, 180);
            PointF[] torso =
            {
                new PointF(cx - 13 * scale, top + 25 * scale),
                new PointF(cx + 13 * scale, top + 25 * scale),
                new PointF(cx + 9 * scale, top + 44 * scale),
                new PointF(cx - 9 * scale, top + 44 * scale)
            };
            g.FillPolygon(jerseyBrush, torso);
            g.DrawPolygon(outline, torso);
            g.DrawLine(outline, cx - 10 * scale, top + 30 * scale, cx - 20 * scale, top + 39 * scale);
            g.DrawLine(outline, cx + 10 * scale, top + 30 * scale, cx + 20 * scale, top + 39 * scale);
            g.DrawLine(pantsPen, cx - 5 * scale, top + 44 * scale, cx - 13 * scale, top + 57 * scale);
            g.DrawLine(pantsPen, cx + 5 * scale, top + 44 * scale, cx + 13 * scale, top + 57 * scale);
        }

        private static void DrawBase(Graphics g, PointF center)
        {
            PointF[] diamond =
            {
                new PointF(center.X, center.Y - 7),
                new PointF(center.X + 7, center.Y),
                new PointF(center.X, center.Y + 7),
                new PointF(center.X - 7, center.Y)
            };
            using var brush = new SolidBrush(Color.White);
            g.FillPolygon(brush, diamond);
        }

        private static PointF Pt(Rectangle rect, float x, float y)
            => new PointF(rect.Left + rect.Width * x, rect.Top + rect.Height * y);

        private static Rectangle Offset(Rectangle rect, int dx, int dy)
            => new Rectangle(rect.Left + dx, rect.Top + dy, rect.Width, rect.Height);

        private static void DrawImageContained(Graphics g, Image image, Rectangle rect)
        {
            if (image == null)
                return;
            float ratio = Math.Min(rect.Width / (float)image.Width, rect.Height / (float)image.Height);
            SizeF size = new SizeF(image.Width * ratio, image.Height * ratio);
            RectangleF dest = new RectangleF(rect.Left + (rect.Width - size.Width) / 2f, rect.Top + (rect.Height - size.Height) / 2f, size.Width, size.Height);
            g.DrawImage(image, dest);
        }

        private static Color ReadableTextColor(Color background)
        {
            int brightness = (background.R * 299 + background.G * 587 + background.B * 114) / 1000;
            return brightness >= 145 ? Color.FromArgb(20, 24, 32) : Color.White;
        }

        private static string FirstExistingPath(params string?[] paths)
        {
            foreach (string? raw in paths)
            {
                string path = ResolvePath(raw);
                if (!string.IsNullOrWhiteSpace(path))
                    return path;
            }
            return "";
        }

        private static string ResolvePath(string? rawPath)
        {
            if (string.IsNullOrWhiteSpace(rawPath))
                return "";
            return AssetPathResolver.ResolveExistingFile(rawPath);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _timer?.Stop();
                _timer?.Dispose();
                _sound.Dispose();
                foreach (Image image in _imageCache.Values.OfType<Image>())
                    image.Dispose();
            }

            base.Dispose(disposing);
        }

        private sealed class PlayerSpot
        {
            public string Position { get; set; }
            public Player Player { get; set; }
            public float X { get; set; }
            public float Y { get; set; }
        }
    }
}
