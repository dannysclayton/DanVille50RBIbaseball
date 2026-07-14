using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace StandaloneBaseball
{
    public sealed class ChampionshipDialog : Form
    {
        private readonly List<Image> _images = new List<Image>();

        public ChampionshipDialog(
            Season season,
            int seasonNumber,
            bool backToBackChampion,
            Team champion,
            PlayoffSeries series,
            string logoPath,
            IReadOnlyList<string> photoPaths)
        {
            if (season == null) throw new ArgumentNullException(nameof(season));
            if (champion == null) throw new ArgumentNullException(nameof(champion));

            string titleText = backToBackChampion
                ? "BACK TO BACK WORLD CHAMPIONS!"
                : "Season " + Math.Max(1, seasonNumber) + " World Champions!";
            Text = titleText;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(780, 560);
            MinimumSize = new Size(680, 500);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            Color primary = Color.FromArgb(champion.PrimaryArgb);
            Color secondary = Color.FromArgb(champion.SecondaryArgb);
            Color primaryText = ReadableTextColor(primary);

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 4,
                ColumnCount = 1,
                BackColor = Color.FromArgb(246, 248, 250),
                Padding = new Padding(16)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 88));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            Controls.Add(root);

            var header = new Panel { Dock = DockStyle.Fill, BackColor = primary, Padding = new Padding(16, 10, 16, 10) };
            header.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = titleText,
                ForeColor = primaryText,
                Font = new Font(Font.FontFamily, 27, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter
            });
            root.Controls.Add(header, 0, 0);

            var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Padding = new Padding(0, 14, 0, 8) };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
            root.Controls.Add(body, 0, 1);

            var textPanel = BuildChampionTextPanel(season, champion, series, secondary);
            body.Controls.Add(textPanel, 0, 0);

            var mediaPanel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1 };
            mediaPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            mediaPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 44));
            mediaPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            mediaPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 56));
            body.Controls.Add(mediaPanel, 1, 0);

            mediaPanel.Controls.Add(MediaHeader("TEAM LOGO"), 0, 0);
            mediaPanel.Controls.Add(BuildLogoPanel(champion, logoPath, secondary), 0, 1);
            mediaPanel.Controls.Add(MediaHeader("TEAM PHOTOS"), 0, 2);
            mediaPanel.Controls.Add(BuildPhotosPanel(photoPaths), 0, 3);

            root.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = "Saved to " + season.Name + " season history",
                Font = new Font(Font.FontFamily, 13, FontStyle.Regular),
                ForeColor = Color.FromArgb(55, 65, 81),
                TextAlign = ContentAlignment.MiddleCenter
            }, 0, 2);

            var close = new Button
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Anchor = AnchorStyles.Right,
                Width = 100,
                Height = 30
            };
            root.Controls.Add(close, 0, 3);
            AcceptButton = close;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (var image in _images)
                    image.Dispose();
            }

            base.Dispose(disposing);
        }

        private Control BuildChampionTextPanel(Season season, Team champion, PlayoffSeries series, Color secondary)
        {
            var textPanel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1 };
            textPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 58));
            textPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            textPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            textPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 42));

            textPanel.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = champion.DisplayName,
                Font = new Font(Font.FontFamily, 28, FontStyle.Bold),
                ForeColor = Color.FromArgb(20, 24, 32),
                TextAlign = ContentAlignment.BottomCenter
            }, 0, 0);

            textPanel.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = champion.ScoreboardName,
                BackColor = secondary,
                ForeColor = ReadableTextColor(secondary),
                Font = new Font(Font.FontFamily, 20, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                Margin = new Padding(34, 4, 34, 4)
            }, 0, 1);

            string record = series == null ? "" : "Series won " + series.TeamAWins + "-" + series.TeamBWins;
            textPanel.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = string.IsNullOrWhiteSpace(record) ? "Season champion recorded" : record,
                Font = new Font(Font.FontFamily, 13, FontStyle.Regular),
                ForeColor = Color.FromArgb(75, 85, 99),
                TextAlign = ContentAlignment.MiddleCenter
            }, 0, 2);

            textPanel.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = season.Name,
                Font = new Font(Font.FontFamily, 12, FontStyle.Regular),
                ForeColor = Color.FromArgb(75, 85, 99),
                TextAlign = ContentAlignment.TopCenter
            }, 0, 3);

            return textPanel;
        }

        private static Label MediaHeader(string text)
        {
            return new Label
            {
                Dock = DockStyle.Fill,
                Text = text,
                Font = new Font(SystemFonts.DefaultFont.FontFamily, 10, FontStyle.Bold),
                ForeColor = Color.FromArgb(75, 85, 99),
                TextAlign = ContentAlignment.MiddleLeft
            };
        }

        private Control BuildLogoPanel(Team champion, string logoPath, Color fallbackColor)
        {
            Image logo = LoadImage(logoPath);
            if (logo != null)
            {
                _images.Add(logo);
                return BuildImageBox(logo, "TEAM LOGO");
            }

            return new Label
            {
                Dock = DockStyle.Fill,
                Text = champion.ScoreboardName,
                BackColor = fallbackColor,
                ForeColor = ReadableTextColor(fallbackColor),
                Font = new Font(Font.FontFamily, 42, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                BorderStyle = BorderStyle.FixedSingle
            };
        }

        private Control BuildPhotosPanel(IReadOnlyList<string> photoPaths)
        {
            var panel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = Color.FromArgb(24, 28, 36),
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(8)
            };

            var paths = (photoPaths ?? Array.Empty<string>()).Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
            foreach (var path in paths)
            {
                Image photo = LoadImage(path);
                if (photo == null)
                    continue;

                _images.Add(photo);
                panel.Controls.Add(BuildPhotoBox(photo));
            }

            if (panel.Controls.Count == 0)
            {
                panel.Controls.Add(new Label
                {
                    Width = 320,
                    Height = 140,
                    Text = "NO TEAM PHOTOS",
                    ForeColor = Color.White,
                    Font = new Font(Font.FontFamily, 12, FontStyle.Bold),
                    TextAlign = ContentAlignment.MiddleCenter
                });
            }

            return panel;
        }

        private static PictureBox BuildImageBox(Image image, string name)
        {
            return new PictureBox
            {
                Dock = DockStyle.Fill,
                Image = image,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.FromArgb(24, 28, 36),
                BorderStyle = BorderStyle.FixedSingle,
                AccessibleName = name
            };
        }

        private static PictureBox BuildPhotoBox(Image image)
        {
            return new PictureBox
            {
                Width = 150,
                Height = 126,
                Image = image,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Black,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(4)
            };
        }

        private static Image LoadImage(string path)
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
