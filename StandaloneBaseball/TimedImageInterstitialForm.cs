using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace StandaloneBaseball
{
    internal sealed class TimedImageInterstitialForm : Form
    {
        private readonly System.Windows.Forms.Timer _timer;
        private readonly Image _image;

        public TimedImageInterstitialForm(string imagePath, int durationMilliseconds)
        {
            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
                throw new FileNotFoundException("The interstitial image could not be found.", imagePath);

            using (Image source = Image.FromFile(imagePath))
                _image = new Bitmap(source);

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            WindowState = FormWindowState.Maximized;
            BackColor = Color.Black;
            ShowInTaskbar = false;
            KeyPreview = true;

            Controls.Add(new PictureBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black,
                Image = _image,
                SizeMode = PictureBoxSizeMode.Zoom
            });

            _timer = new System.Windows.Forms.Timer
            {
                Interval = Math.Clamp(durationMilliseconds, 1000, 60000)
            };
            _timer.Tick += (sender, args) =>
            {
                _timer.Stop();
                DialogResult = DialogResult.OK;
                Close();
            };
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _timer.Start();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _timer.Stop();
                _timer.Dispose();
                _image.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
