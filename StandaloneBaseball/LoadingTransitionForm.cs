using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace StandaloneBaseball
{
    public sealed class LoadingTransitionForm : Form
    {
        private const string LoadingImageName = "game day 2.jpg";
        private readonly Image _loadingImage;
        private readonly ProgressBar _progress;
        private readonly Label _status;
        private readonly System.Windows.Forms.Timer _timer;
        private readonly LaunchSoundPlayer _loadingSound = new LaunchSoundPlayer();
        private int _ticks;

        public LoadingTransitionForm(string message = "Loading Dan's RBI Baseball 2026")
        {
            Text = "Dan's RBI Baseball 2026";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(900, 700);
            MinimumSize = new Size(720, 560);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            ControlBox = false;
            DoubleBuffered = true;

            _loadingImage = LoadLoadingImage();

            _progress = new ProgressBar
            {
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                Style = ProgressBarStyle.Continuous
            };
            Controls.Add(_progress);

            _status = new Label
            {
                Text = message,
                Font = new Font(Font.FontFamily, 13f, FontStyle.Bold),
                ForeColor = Color.FromArgb(28, 46, 126),
                BackColor = Color.FromArgb(235, 238, 232),
                TextAlign = ContentAlignment.MiddleCenter
            };
            Controls.Add(_status);

            _timer = new System.Windows.Forms.Timer { Interval = 45 };
            _timer.Tick += (s, e) => Advance();
            Resize += (s, e) => LayoutControls();
            LayoutControls();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _loadingSound.PlayLoop(LaunchSoundPlayer.FindLoadingLoop());
            _timer.Start();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _timer?.Dispose();
                _loadingSound.Dispose();
                _loadingImage?.Dispose();
            }
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.Clear(Color.FromArgb(235, 238, 232));

            if (_loadingImage == null)
            {
                using var titleFont = new Font(Font.FontFamily, 34f, FontStyle.Bold);
                TextRenderer.DrawText(
                    e.Graphics,
                    "It's Game Day Y'all",
                    titleFont,
                    ClientRectangle,
                    Color.FromArgb(28, 46, 126),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }

            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            e.Graphics.DrawImage(_loadingImage, ImageBounds());
        }

        private void Advance()
        {
            _ticks++;
            int next = Math.Min(100, _progress.Value + Math.Max(1, 3 + _ticks / 12));
            _progress.Value = next;

            if (next >= 100)
            {
                _timer.Stop();
                _loadingSound.Stop();
                DialogResult = DialogResult.OK;
                Close();
            }
        }

        private void LayoutControls()
        {
            Rectangle bounds = ImageBounds();
            int progressWidth = Math.Max(320, (int)Math.Round(bounds.Width * 0.62));
            int progressHeight = 24;
            int progressLeft = bounds.Left + (bounds.Width - progressWidth) / 2;
            int progressTop = bounds.Bottom - Math.Max(88, (int)Math.Round(bounds.Height * 0.13));

            _status.Bounds = new Rectangle(progressLeft, progressTop - 42, progressWidth, 32);
            _progress.Bounds = new Rectangle(progressLeft, progressTop, progressWidth, progressHeight);
            _status.BringToFront();
            _progress.BringToFront();
        }

        private Rectangle ImageBounds()
        {
            if (_loadingImage == null || ClientSize.Width <= 0 || ClientSize.Height <= 0)
                return ClientRectangle;

            double scale = Math.Min(
                (double)ClientSize.Width / _loadingImage.Width,
                (double)ClientSize.Height / _loadingImage.Height);
            int width = Math.Max(1, (int)Math.Round(_loadingImage.Width * scale));
            int height = Math.Max(1, (int)Math.Round(_loadingImage.Height * scale));
            return new Rectangle(
                (ClientSize.Width - width) / 2,
                (ClientSize.Height - height) / 2,
                width,
                height);
        }

        private static Image LoadLoadingImage()
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Assets", "Loading Screens", LoadingImageName);
            if (!File.Exists(path))
                return null;

            using var source = Image.FromFile(path);
            return new Bitmap(source);
        }
    }
}
