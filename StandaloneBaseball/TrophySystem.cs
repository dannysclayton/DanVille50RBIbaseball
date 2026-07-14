#nullable enable annotations

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace StandaloneBaseball
{
    public sealed class AwardTrophyRecord
    {
        public Guid SeasonId { get; set; }
        public int SeasonNumber { get; set; }
        public Guid RecipientId { get; set; }
        public Guid TeamId { get; set; }
        public string AwardName { get; set; } = "";
        public string RecipientName { get; set; } = "";
        public string TeamName { get; set; } = "";
        public DateTime AwardedAt { get; set; }

        public override string ToString()
            => "Season " + SeasonNumber + " | " + AwardName + " | " + RecipientName;
    }

    public static class TrophyCatalog
    {
        public static List<AwardTrophyRecord> Build(LeagueFile? league)
        {
            var trophies = new List<AwardTrophyRecord>();
            if (league?.Seasons == null)
                return trophies;

            for (int index = 0; index < league.Seasons.Count; index++)
            {
                Season season = league.Seasons[index];
                foreach (SeasonAwardSelection award in (season.Awards ?? new List<SeasonAwardSelection>())
                    .Where(award => award != null && award.Winner))
                {
                    trophies.Add(new AwardTrophyRecord
                    {
                        SeasonId = season.Id,
                        SeasonNumber = index + 1,
                        RecipientId = award.PlayerId,
                        TeamId = award.TeamId,
                        AwardName = award.AwardName ?? "Award",
                        RecipientName = award.PlayerName ?? "",
                        TeamName = award.TeamName ?? "",
                        AwardedAt = award.FinalizedAt
                    });
                }
            }

            return trophies
                .OrderByDescending(trophy => trophy.SeasonNumber)
                .ThenBy(trophy => trophy.AwardName)
                .ThenBy(trophy => trophy.RecipientName)
                .ToList();
        }
    }

    public static class TrophyRenderer
    {
        public static string TemplatePath => Path.Combine(
            AppContext.BaseDirectory,
            "Assets",
            "Trophies",
            "baseball-mvp-trophy-template.jpg");

        public static Bitmap Render(AwardTrophyRecord trophy, TrophyPlaqueStyle plaqueStyle = TrophyPlaqueStyle.AwardWinnerPlaque)
        {
            if (trophy == null)
                throw new ArgumentNullException(nameof(trophy));

            Bitmap result = LoadTemplate();
            if (plaqueStyle == TrophyPlaqueStyle.OriginalPlaque)
                return result;

            using Graphics graphics = Graphics.FromImage(result);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

            float scaleX = result.Width / 544f;
            float scaleY = result.Height / 736f;
            RectangleF plaque = Scale(new RectangleF(137, 381, 300, 282), scaleX, scaleY);
            using var plaqueBrush = new LinearGradientBrush(
                plaque,
                Color.FromArgb(255, 15, 15, 13),
                Color.FromArgb(255, 34, 28, 18),
                LinearGradientMode.Vertical);
            using var borderPen = new Pen(Color.FromArgb(155, 124, 74), Math.Max(2f, 2f * scaleX));
            graphics.FillRectangle(plaqueBrush, plaque);
            graphics.DrawRectangle(borderPen, plaque.X, plaque.Y, plaque.Width, plaque.Height);

            using var centered = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisWord,
                FormatFlags = StringFormatFlags.LineLimit
            };
            using var goldBrush = new SolidBrush(Color.FromArgb(223, 200, 121));
            using var lightGoldBrush = new SolidBrush(Color.FromArgb(242, 225, 161));

            RectangleF awardRect = Scale(new RectangleF(150, 397, 274, 75), scaleX, scaleY);
            using Font awardFont = FitFont(graphics, (trophy.AwardName ?? "AWARD").ToUpperInvariant(), awardRect,
                25f * scaleY, 10f * scaleY, FontStyle.Bold);
            graphics.DrawString((trophy.AwardName ?? "AWARD").ToUpperInvariant(), awardFont, lightGoldBrush, awardRect, centered);

            float dividerY = 482f * scaleY;
            graphics.DrawLine(borderPen, 160f * scaleX, dividerY, 414f * scaleX, dividerY);

            string recipient = (trophy.RecipientName ?? "").ToUpperInvariant();
            string seasonAndRecipient = "SEASON " + trophy.SeasonNumber + " - " + recipient;
            RectangleF recipientRect = Scale(new RectangleF(150, 494, 274, 87), scaleX, scaleY);
            using Font recipientFont = FitFont(graphics, seasonAndRecipient, recipientRect,
                21f * scaleY, 9f * scaleY, FontStyle.Bold);
            graphics.DrawString(seasonAndRecipient, recipientFont, lightGoldBrush, recipientRect, centered);

            string team = (trophy.TeamName ?? "").ToUpperInvariant();
            RectangleF teamRect = Scale(new RectangleF(150, 590, 274, 32), scaleX, scaleY);
            using Font teamFont = FitFont(graphics, team, teamRect,
                13f * scaleY, 8f * scaleY, FontStyle.Regular);
            graphics.DrawString(team, teamFont, goldBrush, teamRect, centered);
            return result;
        }

        private static Bitmap LoadTemplate()
        {
            if (File.Exists(TemplatePath))
            {
                using Image source = Image.FromFile(TemplatePath);
                return new Bitmap(source);
            }

            var fallback = new Bitmap(544, 768, PixelFormat.Format32bppArgb);
            using Graphics graphics = Graphics.FromImage(fallback);
            graphics.Clear(Color.FromArgb(228, 232, 237));
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var gold = new SolidBrush(Color.FromArgb(197, 155, 54));
            using var darkGold = new Pen(Color.FromArgb(116, 83, 19), 8f);
            using var cup = new GraphicsPath();
            cup.AddBezier(150, 100, 155, 280, 220, 340, 272, 350);
            cup.AddBezier(272, 350, 325, 340, 390, 280, 394, 100);
            cup.AddLine(340, 165, 204, 165);
            cup.CloseFigure();
            graphics.FillPath(gold, cup);
            graphics.DrawPath(darkGold, cup);
            graphics.FillRectangle(gold, 250, 342, 44, 95);
            graphics.FillRectangle(gold, 180, 430, 184, 35);
            return fallback;
        }

        private static RectangleF Scale(RectangleF rectangle, float scaleX, float scaleY)
            => new RectangleF(rectangle.X * scaleX, rectangle.Y * scaleY, rectangle.Width * scaleX, rectangle.Height * scaleY);

        private static Font FitFont(Graphics graphics, string text, RectangleF bounds, float maximum, float minimum, FontStyle style)
        {
            string value = string.IsNullOrWhiteSpace(text) ? " " : text;
            for (float size = maximum; size >= minimum; size -= 0.5f)
            {
                var font = new Font(FontFamily.GenericSerif, size, style, GraphicsUnit.Pixel);
                SizeF measured = graphics.MeasureString(value, font, new SizeF(bounds.Width, bounds.Height));
                if (measured.Width <= bounds.Width && measured.Height <= bounds.Height)
                    return font;
                font.Dispose();
            }
            return new Font(FontFamily.GenericSerif, minimum, style, GraphicsUnit.Pixel);
        }
    }

    public enum TrophyPlaqueStyle
    {
        OriginalPlaque,
        AwardWinnerPlaque
    }

    public sealed class TrophyGalleryControl : UserControl
    {
        private readonly ListBox _list;
        private readonly PictureBox _picture;
        private readonly Label _details;
        private readonly ComboBox _plaqueStyle;
        private readonly Button _saveButton;
        private Image? _renderedImage;

        public TrophyGalleryControl()
        {
            Dock = DockStyle.Fill;
            var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 340, FixedPanel = FixedPanel.Panel1 };
            _list = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };
            _list.SelectedIndexChanged += (sender, args) => ShowSelection();
            split.Panel1.Controls.Add(_list);

            var right = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1, Padding = new Padding(8) };
            right.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            _picture = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.FromArgb(35, 38, 43),
                BorderStyle = BorderStyle.FixedSingle
            };
            var plaqueBar = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
            plaqueBar.Controls.Add(new Label
            {
                Text = "Plaque",
                AutoSize = true,
                Margin = new Padding(4, 10, 6, 0)
            });
            _plaqueStyle = new ComboBox
            {
                Width = 190,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _plaqueStyle.Items.AddRange(new object[] { "Original Plaque", "Award Winner Plaque" });
            plaqueBar.Controls.Add(_plaqueStyle);
            _details = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, AutoEllipsis = true };
            _saveButton = new Button { Text = "Save Trophy...", AutoSize = true, Anchor = AnchorStyles.None, Enabled = false };
            _saveButton.Click += (sender, args) => SaveSelection();
            _plaqueStyle.SelectedIndexChanged += (sender, args) => ShowSelection();
            _plaqueStyle.SelectedIndex = 0;
            right.Controls.Add(_picture, 0, 0);
            right.Controls.Add(plaqueBar, 0, 1);
            right.Controls.Add(_details, 0, 2);
            right.Controls.Add(_saveButton, 0, 3);
            split.Panel2.Controls.Add(right);
            Controls.Add(split);
        }

        public void SetTrophies(IEnumerable<AwardTrophyRecord>? trophies)
        {
            _list.BeginUpdate();
            _list.Items.Clear();
            foreach (AwardTrophyRecord trophy in trophies ?? Enumerable.Empty<AwardTrophyRecord>())
                _list.Items.Add(trophy);
            _list.EndUpdate();
            if (_list.Items.Count > 0)
                _list.SelectedIndex = 0;
            else
                ClearSelection("No award trophies are available for this selection.");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                ReplaceImage(null);
            base.Dispose(disposing);
        }

        private void ShowSelection()
        {
            if (_list.SelectedItem is not AwardTrophyRecord trophy)
            {
                ClearSelection("Select a trophy.");
                return;
            }

            ReplaceImage(TrophyRenderer.Render(trophy, SelectedPlaqueStyle()));
            _details.Text = trophy.AwardName + " | Season " + trophy.SeasonNumber + " | " +
                trophy.RecipientName + " | " + trophy.TeamName;
            _saveButton.Enabled = true;
        }

        private void SaveSelection()
        {
            if (_list.SelectedItem is not AwardTrophyRecord trophy)
                return;
            using var dialog = new SaveFileDialog
            {
                Title = "Save award trophy",
                Filter = "PNG image (*.png)|*.png|JPEG image (*.jpg)|*.jpg",
                FileName = SafeFileName("Season " + trophy.SeasonNumber + " " + trophy.AwardName + " " + trophy.RecipientName) + ".png"
            };
            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;
            using Bitmap image = TrophyRenderer.Render(trophy, SelectedPlaqueStyle());
            image.Save(dialog.FileName, Path.GetExtension(dialog.FileName).Equals(".jpg", StringComparison.OrdinalIgnoreCase)
                ? ImageFormat.Jpeg
                : ImageFormat.Png);
        }

        private void ClearSelection(string message)
        {
            ReplaceImage(null);
            _details.Text = message;
            _saveButton.Enabled = false;
        }

        private void ReplaceImage(Image? image)
        {
            Image? old = _renderedImage;
            _renderedImage = image;
            _picture.Image = image;
            old?.Dispose();
        }

        private static string SafeFileName(string value)
        {
            string result = value;
            foreach (char invalid in Path.GetInvalidFileNameChars())
                result = result.Replace(invalid, '_');
            return result.Trim();
        }

        private TrophyPlaqueStyle SelectedPlaqueStyle()
            => _plaqueStyle.SelectedIndex == 0
                ? TrophyPlaqueStyle.OriginalPlaque
                : TrophyPlaqueStyle.AwardWinnerPlaque;
    }

    public sealed class TrophyGalleryDialog : Form
    {
        public TrophyGalleryDialog(string title, IEnumerable<AwardTrophyRecord> trophies)
        {
            Text = title;
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(900, 650);
            Size = new Size(1100, 780);
            var gallery = new TrophyGalleryControl();
            gallery.SetTrophies(trophies);
            Controls.Add(gallery);
        }
    }
}
