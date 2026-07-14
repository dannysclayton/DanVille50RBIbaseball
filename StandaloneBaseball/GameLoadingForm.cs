#nullable enable annotations

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace StandaloneBaseball
{
    public sealed class GameLoadingForm : Form
    {
        private readonly List<Image> _images = new List<Image>();
        private readonly System.Windows.Forms.Timer _timer;
        private readonly LaunchSoundPlayer _playoffPregameSound = new LaunchSoundPlayer();
        private readonly LaunchSoundPlayer _introSound = new LaunchSoundPlayer();
        private readonly LaunchSoundPlayer _openingSound = new LaunchSoundPlayer();
        private readonly bool _playoffGame;
        private bool _introStarted;
        private bool _openingStarted;
        private bool _gameStarting;

        public GameLoadingForm(
            Team awayTeam,
            string awayRecord,
            string? awayLogoPath,
            Team homeTeam,
            string homeRecord,
            string? homeLogoPath,
            string gameTitle,
            string modeLabel,
            bool playoffGame = false,
            string? previewImagePath = null)
        {
            if (awayTeam == null) throw new ArgumentNullException(nameof(awayTeam));
            if (homeTeam == null) throw new ArgumentNullException(nameof(homeTeam));

            _playoffGame = playoffGame;
            Text = string.IsNullOrWhiteSpace(gameTitle) ? "Loading Game" : gameTitle;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(820, 470);
            MinimumSize = new Size(720, 420);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 4,
                ColumnCount = 1,
                Padding = new Padding(18),
                BackColor = Color.FromArgb(245, 247, 250)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            Controls.Add(root);

            var header = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
            header.RowStyles.Add(new RowStyle(SizeType.Percent, 64));
            header.RowStyles.Add(new RowStyle(SizeType.Percent, 36));
            root.Controls.Add(header, 0, 0);

            header.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = string.IsNullOrWhiteSpace(gameTitle) ? "Regular Season Game" : gameTitle,
                Font = new Font(Font.FontFamily, 21, FontStyle.Bold),
                ForeColor = Color.FromArgb(31, 41, 55),
                TextAlign = ContentAlignment.MiddleCenter
            }, 0, 0);

            header.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = string.IsNullOrWhiteSpace(modeLabel) ? "Loading Game" : modeLabel,
                Font = new Font(Font.FontFamily, 10, FontStyle.Regular),
                ForeColor = Color.FromArgb(75, 85, 99),
                TextAlign = ContentAlignment.TopCenter
            }, 0, 1);

            var match = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                Padding = new Padding(0, 10, 0, 10)
            };
            match.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 43));
            match.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 14));
            match.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 43));

            var previewImage = LoadImage(previewImagePath);
            if (previewImage != null)
            {
                _images.Add(previewImage);
                root.Controls.Add(BuildPreviewPanel(previewImage, awayTeam, awayRecord, homeTeam, homeRecord), 0, 1);
            }
            else
            {
                match.Controls.Add(BuildTeamPanel(awayTeam, awayRecord, awayLogoPath), 0, 0);
                match.Controls.Add(new Label
                {
                    Dock = DockStyle.Fill,
                    Text = "VS",
                    Font = new Font(Font.FontFamily, 34, FontStyle.Bold),
                    ForeColor = Color.FromArgb(17, 24, 39),
                    TextAlign = ContentAlignment.MiddleCenter
                }, 1, 0);
                match.Controls.Add(BuildTeamPanel(homeTeam, homeRecord, homeLogoPath), 2, 0);
                root.Controls.Add(match, 0, 1);
            }

            var progress = new ProgressBar
            {
                Dock = DockStyle.Fill,
                Style = ProgressBarStyle.Marquee,
                MarqueeAnimationSpeed = 28
            };
            root.Controls.Add(progress, 0, 2);

            var start = new Button
            {
                Text = "Start Game",
                DialogResult = DialogResult.OK,
                Anchor = AnchorStyles.Right,
                Width = 120,
                Height = 30
            };
            start.Click += (s, e) => _gameStarting = true;
            root.Controls.Add(start, 0, 3);
            AcceptButton = start;

            _timer = new System.Windows.Forms.Timer { Interval = 8000 };
            _timer.Tick += (s, e) =>
            {
                _timer.Stop();
                ContinuePregameAudioSequence();
            };
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (_playoffGame)
            {
                string playoffPregame = LaunchSoundPlayer.FindPlayoffPregame();
                if (!string.IsNullOrWhiteSpace(playoffPregame))
                {
                    _playoffPregameSound.PlayOnce(playoffPregame);
                    StartAudioTimer(playoffPregame, 8000);
                    return;
                }
            }

            StartIntroAudio();
        }

        private void ContinuePregameAudioSequence()
        {
            if (_gameStarting || DialogResult == DialogResult.OK || !Visible)
                return;

            if (!_introStarted)
            {
                StartIntroAudio();
                return;
            }

            if (!_openingStarted)
            {
                _openingStarted = true;
                _openingSound.PlayOnce(LaunchSoundPlayer.FindGameOpening());
            }
        }

        private void StartIntroAudio()
        {
            _introStarted = true;
            string intro = LaunchSoundPlayer.FindGameIntro();
            _introSound.PlayOnce(intro);
            StartAudioTimer(intro, 8000);
        }

        private void StartAudioTimer(string path, int fallbackMilliseconds)
        {
            _timer.Interval = Math.Clamp(LaunchSoundPlayer.GetDurationMilliseconds(path, fallbackMilliseconds) + 150, 1000, 600000);
            _timer.Start();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _timer?.Stop();
                _timer?.Dispose();
                _playoffPregameSound.Dispose();
                _introSound.Dispose();
                _openingSound.Dispose();
                foreach (var image in _images)
                    image.Dispose();
            }

            base.Dispose(disposing);
        }

        private Control BuildTeamPanel(Team team, string record, string? logoPath)
        {
            Color primary = Color.FromArgb(team.PrimaryArgb);
            Color secondary = Color.FromArgb(team.SecondaryArgb);
            Color textColor = ReadableTextColor(primary);

            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 4,
                ColumnCount = 1,
                BackColor = primary,
                Padding = new Padding(14)
            };
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));

            var logo = BuildLogo(team, logoPath, secondary);
            panel.Controls.Add(logo, 0, 0);

            panel.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = team.DisplayName,
                ForeColor = textColor,
                Font = new Font(Font.FontFamily, 17, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter
            }, 0, 1);

            panel.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = team.ScoreboardName,
                BackColor = secondary,
                ForeColor = ReadableTextColor(secondary),
                Font = new Font(Font.FontFamily, 15, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                Margin = new Padding(38, 4, 38, 4)
            }, 0, 2);

            panel.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = string.IsNullOrWhiteSpace(record) ? "0-0" : record,
                ForeColor = textColor,
                Font = new Font(Font.FontFamily, 16, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter
            }, 0, 3);

            return panel;
        }

        private Control BuildPreviewPanel(Image previewImage, Team awayTeam, string awayRecord, Team homeTeam, string homeRecord)
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black,
                Padding = new Padding(0)
            };

            var picture = new PictureBox
            {
                Dock = DockStyle.Fill,
                Image = previewImage,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Black
            };
            panel.Controls.Add(picture);

            var banner = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 56,
                Text = awayTeam.DisplayName + " (" + (string.IsNullOrWhiteSpace(awayRecord) ? "All-Stars" : awayRecord) + ")    VS    " +
                    homeTeam.DisplayName + " (" + (string.IsNullOrWhiteSpace(homeRecord) ? "All-Stars" : homeRecord) + ")",
                BackColor = Color.FromArgb(210, 10, 24, 50),
                ForeColor = Color.White,
                Font = new Font(Font.FontFamily, 16, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter
            };
            panel.Controls.Add(banner);
            banner.BringToFront();
            return panel;
        }

        private Control BuildLogo(Team team, string? logoPath, Color fallbackColor)
        {
            Image? image = LoadImage(logoPath);
            if (image != null)
            {
                _images.Add(image);
                return new PictureBox
                {
                    Dock = DockStyle.Fill,
                    Image = image,
                    SizeMode = PictureBoxSizeMode.Zoom,
                    BackColor = Color.FromArgb(20, 24, 32),
                    BorderStyle = BorderStyle.FixedSingle
                };
            }

            return new Label
            {
                Dock = DockStyle.Fill,
                Text = team.ScoreboardName,
                BackColor = fallbackColor,
                ForeColor = ReadableTextColor(fallbackColor),
                Font = new Font(Font.FontFamily, 42, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                BorderStyle = BorderStyle.FixedSingle
            };
        }

        private static Image? LoadImage(string? path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return null;

            using var source = Image.FromFile(path);
            return new Bitmap(source);
        }

        private static Color ReadableTextColor(Color background)
        {
            int brightness = (background.R * 299 + background.G * 587 + background.B * 114) / 1000;
            return brightness >= 145 ? Color.FromArgb(20, 24, 32) : Color.White;
        }
    }
}
