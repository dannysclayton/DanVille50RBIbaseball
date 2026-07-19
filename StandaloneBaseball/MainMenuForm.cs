using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace StandaloneBaseball
{
    public sealed class MainMenuForm : Form
    {
        private const string MenuImageName = "Dan's RBI Baseball 2026 menu screen.png";
        private readonly Image? _menuImage;
        private readonly LaunchSoundPlayer _menuMusic = new LaunchSoundPlayer();
        private readonly LaunchSoundPlayer _buttonSound = new LaunchSoundPlayer();
        private readonly System.Windows.Forms.Timer _controllerTimer;
        private readonly List<MenuHotspot> _hotspots;
        private XInputButtons _previousButtons;
        private XInputButtons _currentButtons;
        private int _lastStickDirection;
        private DateTime _lastStickMove = DateTime.MinValue;
        private int _selectedIndex;
        private bool _launching;
        private string? _controllerDeviceId;
        private string? _controllerDisplayName;

        public MainMenuForm()
        {
            Text = "Dan's RBI Baseball 2026";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1120, 790);
            MinimumSize = new Size(900, 640);
            DoubleBuffered = true;
            KeyPreview = true;
            _menuImage = LoadMenuImage();
            _hotspots = BuildHotspots();

            MouseMove += (s, e) => UpdateSelectionFromPoint(e.Location);
            MouseLeave += (s, e) => { Cursor = Cursors.Default; };
            MouseClick += (s, e) => ActivateAtPoint(e.Location);
            KeyDown += OnMenuKeyDown;

            _controllerTimer = new System.Windows.Forms.Timer { Interval = 60 };
            _controllerTimer.Tick += (s, e) => PollController();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _menuMusic.PlayLoop(LaunchSoundPlayer.FindMenuLoop());
            _controllerTimer.Start();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _menuMusic.Dispose();
                _buttonSound.Dispose();
                _controllerTimer?.Dispose();
                _menuImage?.Dispose();
            }
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.Clear(Color.FromArgb(9, 30, 86));

            Rectangle imageBounds = ImageBounds();
            if (_menuImage == null)
            {
                DrawFallback(e.Graphics);
                DrawControllerStatus(e.Graphics);
                return;
            }

            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            e.Graphics.DrawImage(_menuImage, imageBounds);

            DrawSelection(e.Graphics, imageBounds);
            DrawControllerStatus(e.Graphics);
        }

        private void DrawControllerStatus(Graphics graphics)
        {
            string text = string.IsNullOrWhiteSpace(_controllerDisplayName)
                ? "Controller: scanning - PlayStation 3 profile (keyboard ready)"
                : "Controller ready: " + PlayStation3ControllerProfile.Status(_controllerDisplayName);
            Rectangle bounds = new Rectangle(18, Math.Max(8, ClientSize.Height - 42), Math.Min(470, ClientSize.Width - 36), 28);
            using var fill = new SolidBrush(Color.FromArgb(205, 5, 16, 34));
            using var outline = new Pen(string.IsNullOrWhiteSpace(_controllerDisplayName)
                ? Color.FromArgb(155, 175, 195)
                : Color.FromArgb(80, 220, 140), 1.5f);
            graphics.FillRoundedRectangle(fill, bounds, 6);
            graphics.DrawRoundedRectangle(outline, bounds, 6);
            TextRenderer.DrawText(graphics, text, Font, bounds, Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        private void DrawFallback(Graphics graphics)
        {
            using var titleFont = new Font(Font.FontFamily, 30f, FontStyle.Bold);
            TextRenderer.DrawText(
                graphics,
                "Dan's RBI Baseball 2026",
                titleFont,
                new Rectangle(20, 34, Math.Max(1, ClientSize.Width - 40), 70),
                Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            using var itemFont = new Font(Font.FontFamily, 18f, FontStyle.Bold);
            for (int index = 0; index < _hotspots.Count; index++)
            {
                Rectangle bounds = FallbackMenuRect(index);
                bool selected = index == _selectedIndex;
                using var fill = new SolidBrush(selected
                    ? Color.FromArgb(210, 166, 58)
                    : Color.FromArgb(24, 56, 126));
                using var outline = new Pen(selected ? Color.White : Color.FromArgb(120, 160, 220), selected ? 3f : 1.5f);
                graphics.FillRoundedRectangle(fill, bounds, 8);
                graphics.DrawRoundedRectangle(outline, bounds, 8);
                TextRenderer.DrawText(
                    graphics,
                    LabelFor(_hotspots[index].Action),
                    itemFont,
                    bounds,
                    Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
        }

        private void DrawSelection(Graphics graphics, Rectangle imageBounds)
        {
            if (_selectedIndex < 0 || _selectedIndex >= _hotspots.Count)
                return;

            Rectangle rect = ScaleRect(_hotspots[_selectedIndex].ImageRect, imageBounds);
            using var fill = new SolidBrush(Color.FromArgb(42, Color.White));
            using var outline = new Pen(Color.White, Math.Max(2f, imageBounds.Width / 640f));
            graphics.FillRoundedRectangle(fill, rect, 14);
            graphics.DrawRoundedRectangle(outline, rect, 14);
        }

        private void OnMenuKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Down || e.KeyCode == Keys.S)
            {
                MoveSelection(1);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Up || e.KeyCode == Keys.W)
            {
                MoveSelection(-1);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)
            {
                Activate(_hotspots[_selectedIndex].Action);
                e.Handled = true;
            }
        }

        private void PollController()
        {
            if (_launching || !GameControllerDiscovery.TryReadPreferredOrFirst(-1, _controllerDeviceId, out GameControllerReading reading))
            {
                bool changed = !string.IsNullOrWhiteSpace(_controllerDeviceId);
                _controllerDeviceId = null;
                _controllerDisplayName = null;
                _previousButtons = XInputButtons.None;
                _currentButtons = XInputButtons.None;
                _lastStickDirection = 0;
                if (changed)
                    Invalidate();
                return;
            }

            bool deviceChanged = !string.Equals(_controllerDeviceId, reading.DeviceId, StringComparison.Ordinal);
            _controllerDeviceId = reading.DeviceId;
            _controllerDisplayName = reading.DisplayName;
            XInputGamepadState state = reading.State;
            _previousButtons = _currentButtons;
            _currentButtons = state.Buttons;
            if (deviceChanged)
                Invalidate();

            if (PressedThisPoll(XInputButtons.DPadDown))
                MoveSelection(1);
            if (PressedThisPoll(XInputButtons.DPadUp))
                MoveSelection(-1);

            int stickDirection = StickMenuDirection(state.LeftThumbY);
            if (stickDirection != 0)
            {
                bool canMove = stickDirection != _lastStickDirection
                    || (DateTime.UtcNow - _lastStickMove).TotalMilliseconds >= 260;
                if (canMove)
                {
                    MoveSelection(stickDirection);
                    _lastStickMove = DateTime.UtcNow;
                }
            }
            _lastStickDirection = stickDirection;

            if (PressedThisPoll(XInputButtons.A)
                || PressedThisPoll(XInputButtons.Start))
            {
                Activate(_hotspots[_selectedIndex].Action);
            }
        }

        private void MoveSelection(int delta)
        {
            _selectedIndex = (_selectedIndex + _hotspots.Count + delta) % _hotspots.Count;
            Invalidate();
        }

        private bool PressedThisPoll(XInputButtons button)
        {
            return (_currentButtons & button) == button && (_previousButtons & button) != button;
        }

        private static int StickMenuDirection(short y)
        {
            if (Math.Abs((int)y) <= XInputController.DefaultLeftThumbDeadzone)
                return 0;
            return y < 0 ? 1 : -1;
        }

        private void UpdateSelectionFromPoint(Point point)
        {
            int index = HotspotIndexAt(point);
            Cursor = index >= 0 ? Cursors.Hand : Cursors.Default;
            if (index == _selectedIndex || index < 0)
                return;
            _selectedIndex = index;
            Invalidate();
        }

        private void ActivateAtPoint(Point point)
        {
            int index = HotspotIndexAt(point);
            if (index < 0)
                return;
            _selectedIndex = index;
            Activate(_hotspots[index].Action);
        }

        private void Activate(MenuAction action)
        {
            if (_launching)
                return;
            _launching = true;
            _controllerTimer.Stop();
            _menuMusic.Stop();
            _buttonSound.PlayOnce(LaunchSoundPlayer.FindMenuButtonSound());
            Hide();

            using (var loading = new LoadingTransitionForm("Loading " + LabelFor(action)))
                loading.ShowDialog(this);

            var main = new MainForm(action);
            main.FormClosed += (s, e) => Close();
            main.Show();
        }

        private int HotspotIndexAt(Point point)
        {
            if (_menuImage == null)
            {
                for (int index = 0; index < _hotspots.Count; index++)
                {
                    if (FallbackMenuRect(index).Contains(point))
                        return index;
                }
                return -1;
            }

            Rectangle imageBounds = ImageBounds();
            for (int i = 0; i < _hotspots.Count; i++)
            {
                if (ScaleRect(_hotspots[i].ImageRect, imageBounds).Contains(point))
                    return i;
            }
            return -1;
        }

        private Rectangle FallbackMenuRect(int index)
        {
            int width = Math.Min(480, Math.Max(300, ClientSize.Width - 120));
            int height = 56;
            int gap = 12;
            int totalHeight = _hotspots.Count * height + Math.Max(0, _hotspots.Count - 1) * gap;
            int top = Math.Max(118, (ClientSize.Height - totalHeight) / 2 + 32);
            return new Rectangle((ClientSize.Width - width) / 2, top + index * (height + gap), width, height);
        }

        private Rectangle ImageBounds()
        {
            if (_menuImage == null || ClientSize.Width <= 0 || ClientSize.Height <= 0)
                return ClientRectangle;

            double scale = Math.Min(
                (double)ClientSize.Width / _menuImage.Width,
                (double)ClientSize.Height / _menuImage.Height);
            int width = Math.Max(1, (int)Math.Round(_menuImage.Width * scale));
            int height = Math.Max(1, (int)Math.Round(_menuImage.Height * scale));
            return new Rectangle(
                (ClientSize.Width - width) / 2,
                (ClientSize.Height - height) / 2,
                width,
                height);
        }

        private static Rectangle ScaleRect(Rectangle source, Rectangle imageBounds)
        {
            double scaleX = imageBounds.Width / 1493.0;
            double scaleY = imageBounds.Height / 1054.0;
            return new Rectangle(
                imageBounds.Left + (int)Math.Round(source.Left * scaleX),
                imageBounds.Top + (int)Math.Round(source.Top * scaleY),
                Math.Max(1, (int)Math.Round(source.Width * scaleX)),
                Math.Max(1, (int)Math.Round(source.Height * scaleY)));
        }

        private static string LabelFor(MenuAction action)
        {
            return action switch
            {
                MenuAction.StartDynasty => "Start Dynasty",
                MenuAction.ContinueDynasty => "Continue Dynasty",
                MenuAction.Game => "Game",
                MenuAction.Teams => "Teams",
                MenuAction.Seasons => "Seasons",
                MenuAction.Replays => "Replays",
                MenuAction.Settings => "Settings",
                _ => "Dan's RBI Baseball 2026"
            };
        }

        private static List<MenuHotspot> BuildHotspots()
        {
            return new List<MenuHotspot>
            {
                new MenuHotspot(MenuAction.StartDynasty, new Rectangle(660, 210, 470, 78)),
                new MenuHotspot(MenuAction.ContinueDynasty, new Rectangle(590, 296, 575, 78)),
                new MenuHotspot(MenuAction.Game, new Rectangle(540, 382, 330, 78)),
                new MenuHotspot(MenuAction.Teams, new Rectangle(505, 468, 365, 78)),
                new MenuHotspot(MenuAction.Seasons, new Rectangle(470, 554, 390, 78)),
                new MenuHotspot(MenuAction.Replays, new Rectangle(470, 640, 390, 78)),
                new MenuHotspot(MenuAction.Settings, new Rectangle(486, 724, 430, 78)),
            };
        }

        private static Image? LoadMenuImage()
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Assets", MenuImageName);
            if (!File.Exists(path))
                return null;
            using var source = Image.FromFile(path);
            return new Bitmap(source);
        }

        private sealed class MenuHotspot
        {
            public MenuHotspot(MenuAction action, Rectangle imageRect)
            {
                Action = action;
                ImageRect = imageRect;
            }

            public MenuAction Action { get; }
            public Rectangle ImageRect { get; }
        }
    }

    internal static class RoundedRectangleGraphicsExtensions
    {
        public static void FillRoundedRectangle(this Graphics graphics, Brush brush, Rectangle bounds, int radius)
        {
            using var path = RoundedPath(bounds, radius);
            graphics.FillPath(brush, path);
        }

        public static void DrawRoundedRectangle(this Graphics graphics, Pen pen, Rectangle bounds, int radius)
        {
            using var path = RoundedPath(bounds, radius);
            graphics.DrawPath(pen, path);
        }

        private static GraphicsPath RoundedPath(Rectangle bounds, int radius)
        {
            int diameter = Math.Max(1, radius * 2);
            var path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
