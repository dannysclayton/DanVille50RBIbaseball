using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;

#nullable enable annotations

namespace StandaloneBaseball
{
    internal static class ScoreboardTemplateRenderer
    {
        public static void Draw(
            Graphics g,
            Rectangle bounds,
            Team? homeTeam,
            string logoPath,
            string scoreText,
            string inningText,
            string countText = "")
        {
            if (g == null || bounds.Width <= 0 || bounds.Height <= 0)
                return;

            TeamScoreboardTemplate template = homeTeam?.ScoreboardTemplate ?? new TeamScoreboardTemplate();
            if (homeTeam != null)
                template.Normalize(homeTeam);

            g.SmoothingMode = SmoothingMode.AntiAlias;
            DrawBackground(g, bounds, template);

            Color board = Color.FromArgb(template.BoardArgb);
            Color accent = Color.FromArgb(template.AccentArgb);
            Color text = Color.FromArgb(template.TextArgb);
            Color ads = Color.FromArgb(template.AdStripArgb);

            Rectangle boardRect = new Rectangle(
                bounds.Left + Math.Max(10, bounds.Width / 28),
                bounds.Top + Math.Max(8, bounds.Height / 18),
                bounds.Width - Math.Max(20, bounds.Width / 14),
                Math.Max(96, (int)(bounds.Height * 0.72)));
            Rectangle adRect = new Rectangle(boardRect.Left, boardRect.Bottom + 4, boardRect.Width, Math.Max(22, bounds.Bottom - boardRect.Bottom - 8));

            DrawBoardColorLayout(g, boardRect, template);
            using (var border = new Pen(accent, Math.Max(2, bounds.Width / 260)))
            {
                g.DrawRectangle(border, boardRect);
            }

            int topHeight = Math.Max(34, boardRect.Height / 4);
            Rectangle topRow = new Rectangle(boardRect.Left + 10, boardRect.Top + 8, boardRect.Width - 20, topHeight);
            Rectangle abbreviationRect = new Rectangle(topRow.Left, topRow.Top, Math.Max(72, topRow.Width / 6), topRow.Height);
            Rectangle logoRect = new Rectangle(topRow.Right - topRow.Height, topRow.Top, topRow.Height, topRow.Height);
            Rectangle schoolRect = new Rectangle(abbreviationRect.Right + 8, topRow.Top, Math.Max(20, logoRect.Left - abbreviationRect.Right - 16), topRow.Height);

            DrawFittedText(g, template.PreferredAbbreviation, abbreviationRect, accent, FontStyle.Bold, 34, 10, StringAlignment.Center);
            DrawFittedText(g, template.SchoolNameText, schoolRect, text, FontStyle.Bold, 42, 12, StringAlignment.Center);
            DrawLogo(g, logoPath, logoRect, accent, homeTeam?.ScoreboardName ?? template.PreferredAbbreviation);

            Rectangle scoreRect = new Rectangle(boardRect.Left + 18, topRow.Bottom + 8, boardRect.Width - 36, Math.Max(36, boardRect.Height / 4));
            DrawFittedText(g, scoreText, scoreRect, text, FontStyle.Bold, 34, 12, StringAlignment.Center);

            Rectangle lower = new Rectangle(boardRect.Left + 18, scoreRect.Bottom + 4, boardRect.Width - 36, boardRect.Bottom - scoreRect.Bottom - 12);
            int halfWidth = Math.Max(1, lower.Width / 2);
            string gameState = string.IsNullOrWhiteSpace(inningText) ? "HOME HALF" : inningText;
            if (!string.IsNullOrWhiteSpace(countText))
                gameState += "  |  " + countText.Trim();
            DrawFittedText(g, gameState, new Rectangle(lower.Left, lower.Top, halfWidth, lower.Height), accent, FontStyle.Bold, 24, 9, StringAlignment.Near);
            DrawFittedText(g, template.MascotText, new Rectangle(lower.Left + halfWidth, lower.Top, lower.Width - halfWidth, lower.Height), text, FontStyle.Bold, 30, 10, StringAlignment.Far);

            using (var adBrush = new SolidBrush(Color.FromArgb(235, ads)))
            using (var adBorder = new Pen(accent, 1.5f))
            {
                g.FillRectangle(adBrush, adRect);
                g.DrawRectangle(adBorder, adRect);
            }

            DrawAds(g, template.Ads, adRect, accent);
        }

        private static void DrawBoardColorLayout(Graphics g, Rectangle boardRect, TeamScoreboardTemplate template)
        {
            Color first = Color.FromArgb(225, Color.FromArgb(template.BoardArgb));
            Color second = Color.FromArgb(225, Color.FromArgb(template.BoardSecondArgb));
            Color third = Color.FromArgb(225, Color.FromArgb(template.BoardThirdArgb));
            Color fourth = Color.FromArgb(225, Color.FromArgb(template.BoardFourthArgb));

            switch (template.BoardColorLayout)
            {
                case ScoreboardBoardColorLayout.VerticalHalves:
                    Fill(g, first, new Rectangle(boardRect.Left, boardRect.Top, boardRect.Width / 2, boardRect.Height));
                    Fill(g, second, new Rectangle(boardRect.Left + boardRect.Width / 2, boardRect.Top, boardRect.Width - boardRect.Width / 2, boardRect.Height));
                    break;
                case ScoreboardBoardColorLayout.HorizontalHalves:
                    Fill(g, first, new Rectangle(boardRect.Left, boardRect.Top, boardRect.Width, boardRect.Height / 2));
                    Fill(g, second, new Rectangle(boardRect.Left, boardRect.Top + boardRect.Height / 2, boardRect.Width, boardRect.Height - boardRect.Height / 2));
                    break;
                case ScoreboardBoardColorLayout.Quarters:
                    int halfWidth = boardRect.Width / 2;
                    int halfHeight = boardRect.Height / 2;
                    Fill(g, first, new Rectangle(boardRect.Left, boardRect.Top, halfWidth, halfHeight));
                    Fill(g, second, new Rectangle(boardRect.Left + halfWidth, boardRect.Top, boardRect.Width - halfWidth, halfHeight));
                    Fill(g, third, new Rectangle(boardRect.Left, boardRect.Top + halfHeight, halfWidth, boardRect.Height - halfHeight));
                    Fill(g, fourth, new Rectangle(boardRect.Left + halfWidth, boardRect.Top + halfHeight, boardRect.Width - halfWidth, boardRect.Height - halfHeight));
                    break;
                default:
                    Fill(g, first, boardRect);
                    break;
            }
        }

        private static void Fill(Graphics g, Color color, Rectangle rect)
        {
            using var brush = new SolidBrush(color);
            g.FillRectangle(brush, rect);
        }

        private static void DrawBackground(Graphics g, Rectangle bounds, TeamScoreboardTemplate template)
        {
            string path = AssetPathResolver.ResolveExistingFile(template.BackgroundAssetPath);
            if (!string.IsNullOrWhiteSpace(path))
            {
                try
                {
                    using var image = Image.FromFile(path);
                    g.DrawImage(image, Cover(image.Size, bounds));
                    using var shade = new SolidBrush(Color.FromArgb(70, Color.Black));
                    g.FillRectangle(shade, bounds);
                    return;
                }
                catch
                {
                }
            }

            using var fallback = new LinearGradientBrush(bounds, Color.FromArgb(26, 77, 50), Color.FromArgb(12, 28, 24), 90f);
            g.FillRectangle(fallback, bounds);
        }

        private static void DrawLogo(Graphics g, string logoPath, Rectangle bounds, Color fallbackColor, string fallbackText)
        {
            string path = AssetPathResolver.ResolveExistingFile(logoPath);
            if (!string.IsNullOrWhiteSpace(path))
            {
                try
                {
                    using var image = Image.FromFile(path);
                    g.DrawImage(image, Fit(image.Size, bounds));
                    return;
                }
                catch
                {
                }
            }

            using var brush = new SolidBrush(fallbackColor);
            g.FillEllipse(brush, bounds);
            DrawFittedText(g, fallbackText, Rectangle.Inflate(bounds, -5, -5), ReadableTextColor(fallbackColor), FontStyle.Bold, 18, 7, StringAlignment.Center);
        }

        private static void DrawAds(Graphics g, List<string> ads, Rectangle bounds, Color text)
        {
            var cleanAds = (ads ?? new List<string>()).Where(ad => !string.IsNullOrWhiteSpace(ad)).Select(ad => ad.Trim()).ToList();
            if (cleanAds.Count == 0)
                cleanAds.Add("BOOSTER CLUB");

            int itemWidth = Math.Max(1, bounds.Width / cleanAds.Count);
            for (int i = 0; i < cleanAds.Count; i++)
            {
                var rect = new Rectangle(bounds.Left + i * itemWidth + 4, bounds.Top + 2, i == cleanAds.Count - 1 ? bounds.Right - (bounds.Left + i * itemWidth) - 8 : itemWidth - 8, bounds.Height - 4);
                DrawFittedText(g, cleanAds[i], rect, text, FontStyle.Bold, 16, 7, StringAlignment.Center);
            }
        }

        private static void DrawFittedText(Graphics g, string text, Rectangle bounds, Color color, FontStyle style, int maxSize, int minSize, StringAlignment alignment)
        {
            text = string.IsNullOrWhiteSpace(text) ? "" : text.Trim();
            if (string.IsNullOrWhiteSpace(text) || bounds.Width <= 0 || bounds.Height <= 0)
                return;

            Font font = null;
            for (int size = Math.Max(minSize, maxSize); size >= minSize; size--)
            {
                var candidate = new Font(FontFamily.GenericSansSerif, size, style);
                Size measured = TextRenderer.MeasureText(text, candidate, bounds.Size, TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
                if (measured.Width <= bounds.Width && measured.Height <= bounds.Height)
                {
                    font = candidate;
                    break;
                }
                candidate.Dispose();
            }

            font ??= new Font(FontFamily.GenericSansSerif, Math.Max(1, minSize), style);

            using (font)
            using (var brush = new SolidBrush(color))
            using (var format = new StringFormat { Alignment = alignment, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter })
            {
                g.DrawString(text, font, brush, bounds, format);
            }
        }

        private static Rectangle Cover(Size image, Rectangle bounds)
        {
            if (image.Width <= 0 || image.Height <= 0)
                return bounds;
            double scale = Math.Max((double)bounds.Width / image.Width, (double)bounds.Height / image.Height);
            int width = (int)Math.Ceiling(image.Width * scale);
            int height = (int)Math.Ceiling(image.Height * scale);
            return new Rectangle(bounds.Left + (bounds.Width - width) / 2, bounds.Top + (bounds.Height - height) / 2, width, height);
        }

        private static Rectangle Fit(Size image, Rectangle bounds)
        {
            if (image.Width <= 0 || image.Height <= 0)
                return bounds;
            double scale = Math.Min((double)bounds.Width / image.Width, (double)bounds.Height / image.Height);
            int width = Math.Max(1, (int)Math.Round(image.Width * scale));
            int height = Math.Max(1, (int)Math.Round(image.Height * scale));
            return new Rectangle(bounds.Left + (bounds.Width - width) / 2, bounds.Top + (bounds.Height - height) / 2, width, height);
        }

        private static Color ReadableTextColor(Color background)
        {
            int brightness = (background.R * 299 + background.G * 587 + background.B * 114) / 1000;
            return brightness >= 145 ? Color.FromArgb(20, 24, 32) : Color.White;
        }
    }
}
