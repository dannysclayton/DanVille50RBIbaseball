using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace StandaloneBaseball
{
    public sealed class LaunchForm : Form
    {
        private const string LaunchImageName = "Dan s RBI Baseball 2026 logo.png";
        private readonly Button _startButton;
        private readonly Image? _launchImage;
        private readonly LaunchSoundPlayer _launchSound = new LaunchSoundPlayer();
        private readonly LaunchSoundPlayer _startSound = new LaunchSoundPlayer();
        private bool _starting;

        public LaunchForm()
        {
            Text = "Dan's RBI Baseball 2026";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1180, 765);
            MinimumSize = new Size(900, 580);
            DoubleBuffered = true;
            KeyPreview = true;

            _launchImage = LoadLaunchImage();

            _startButton = new Button
            {
                Text = "START",
                Font = new Font(Font.FontFamily, 18f, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(210, 166, 58),
                ForeColor = Color.White,
                Cursor = Cursors.Hand,
                TabStop = true,
            };
            _startButton.FlatAppearance.BorderColor = Color.White;
            _startButton.FlatAppearance.BorderSize = 2;
            _startButton.Click += (s, e) => StartMainApp();
            Controls.Add(_startButton);

            Resize += (s, e) => PositionStartButton();
            KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)
                    StartMainApp();
            };
            PositionStartButton();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            PresentationAudioCoordinator.StartExclusiveLoop(
                _launchSound,
                () => _launchSound.PlayLoop(LaunchSoundPlayer.FindLaunchLoop()));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                PresentationAudioCoordinator.Stop(_launchSound);
                _launchSound.Dispose();
                _startSound.Dispose();
                _launchImage?.Dispose();
            }
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.Clear(Color.Black);

            if (_launchImage == null)
            {
                using var font = new Font(Font.FontFamily, 36f, FontStyle.Bold);
                TextRenderer.DrawText(
                    e.Graphics,
                    "Dan's RBI Baseball 2026",
                    font,
                    ClientRectangle,
                    Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                CodexWatermark.Draw(e.Graphics, ClientRectangle);
                return;
            }

            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            Rectangle imageBounds = ImageBounds();
            e.Graphics.DrawImage(_launchImage, imageBounds);
            CodexWatermark.Draw(e.Graphics, imageBounds);
        }

        private void StartMainApp()
        {
            if (_starting)
                return;
            _starting = true;
            _startButton.Enabled = false;
            PresentationAudioCoordinator.Stop(_launchSound);
            _startSound.PlayOnce(LaunchSoundPlayer.FindStartSound());
            Hide();
            using (var loading = new LoadingTransitionForm())
                loading.ShowDialog(this);

            var menu = new MainMenuForm();
            menu.FormClosed += (s, e) => Close();
            menu.Show();
        }

        private void PositionStartButton()
        {
            Rectangle bounds = ImageBounds();
            double scaleX = bounds.Width / 1558.0;
            double scaleY = bounds.Height / 1009.0;
            int width = Math.Max(128, (int)Math.Round(188 * scaleX));
            int height = Math.Max(42, (int)Math.Round(58 * scaleY));

            int centerX = bounds.Left + (int)Math.Round(bounds.Width * 0.50);
            int centerY = bounds.Top + (int)Math.Round(bounds.Height * 0.82);
            _startButton.Bounds = new Rectangle(
                centerX - width / 2,
                centerY - height / 2,
                width,
                height);
            _startButton.BringToFront();
        }

        private Rectangle ImageBounds()
        {
            if (_launchImage == null || ClientSize.Width <= 0 || ClientSize.Height <= 0)
                return ClientRectangle;

            double scale = Math.Min(
                (double)ClientSize.Width / _launchImage.Width,
                (double)ClientSize.Height / _launchImage.Height);
            int width = Math.Max(1, (int)Math.Round(_launchImage.Width * scale));
            int height = Math.Max(1, (int)Math.Round(_launchImage.Height * scale));
            return new Rectangle(
                (ClientSize.Width - width) / 2,
                (ClientSize.Height - height) / 2,
                width,
                height);
        }

        private static Image? LoadLaunchImage()
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Assets", LaunchImageName);
            if (!File.Exists(path))
                return null;

            using var source = Image.FromFile(path);
            return new Bitmap(source);
        }
    }
}
