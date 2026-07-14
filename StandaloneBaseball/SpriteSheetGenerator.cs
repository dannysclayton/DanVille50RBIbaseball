#nullable enable annotations

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

namespace StandaloneBaseball
{
    internal sealed class SpriteSheetGeneratorOptions
    {
        public const int FrameWidth = 64;
        public const int FrameHeight = 64;
        public const int Columns = 4;
        public const int Rows = 5;

        public Team? Team { get; set; }
        public Player? Player { get; set; }
        public IReadOnlyList<string> SourceImagePaths { get; set; } = Array.Empty<string>();
        public string Label { get; set; } = "";
    }

    internal static class SpriteSheetGenerator
    {
        private static readonly string[] PoseLabels =
        {
            "IDLE", "FIELD", "THROW", "CATCH",
            "BAT", "SWING", "RUN", "SLIDE",
            "PITCH", "WIND", "SET", "COVER",
            "C", "TAG", "LEAD", "DIVE",
            "WIN", "HIT", "SAFE", "OUT"
        };

        public static Bitmap Generate(SpriteSheetGeneratorOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            var sheet = new Bitmap(
                SpriteSheetGeneratorOptions.FrameWidth * SpriteSheetGeneratorOptions.Columns,
                SpriteSheetGeneratorOptions.FrameHeight * SpriteSheetGeneratorOptions.Rows,
                PixelFormat.Format32bppArgb);

            using var g = Graphics.FromImage(sheet);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.Clear(Color.Transparent);

            List<Bitmap> sourceImages = LoadSourceImages(options.SourceImagePaths);
            try
            {
                int frameCount = SpriteSheetGeneratorOptions.Columns * SpriteSheetGeneratorOptions.Rows;
                for (int i = 0; i < frameCount; i++)
                {
                    Rectangle frame = FrameRectangle(i);
                    DrawFrame(g, frame, options, i, sourceImages.Count == 0 ? null : sourceImages[i % sourceImages.Count]);
                }
            }
            finally
            {
                foreach (Bitmap image in sourceImages)
                    image.Dispose();
            }

            return sheet;
        }

        public static void SavePng(Bitmap sheet, string outputPath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
            sheet.Save(outputPath, ImageFormat.Png);
        }

        private static void DrawFrame(Graphics g, Rectangle frame, SpriteSheetGeneratorOptions options, int index, Bitmap? source)
        {
            Color jersey = options.Player?.JerseyColor(options.Team)
                ?? Color.FromArgb(options.Team?.PrimaryArgb ?? unchecked((int)0xFF1F6FEB));
            Color pants = options.Player?.PantsColor(options.Team) ?? Color.White;
            Color cap = options.Player?.CapHelmetColor(options.Team)
                ?? Color.FromArgb(options.Team?.SecondaryArgb ?? unchecked((int)0xFFFFC857));

            using var backdrop = new SolidBrush(Color.FromArgb(32, 0, 0, 0));
            g.FillRectangle(backdrop, frame);

            if (source != null)
                DrawSourceImage(g, source, frame);
            else
                DrawGeneratedPlayer(g, frame, jersey, pants, cap, index);

            DrawUniformSwatches(g, frame, jersey, pants, cap);

            string pose = index < PoseLabels.Length ? PoseLabels[index] : index.ToString();
            using var font = new Font("Segoe UI", 6f, FontStyle.Bold);
            using var labelBrush = new SolidBrush(Color.FromArgb(220, Color.White));
            using var labelShadow = new SolidBrush(Color.FromArgb(170, Color.Black));
            Rectangle labelRect = new Rectangle(frame.Left + 2, frame.Bottom - 11, frame.Width - 4, 10);
            g.DrawString(pose, font, labelShadow, labelRect.Left + 1, labelRect.Top + 1);
            g.DrawString(pose, font, labelBrush, labelRect.Left, labelRect.Top);

            using var border = new Pen(Color.FromArgb(55, Color.White));
            g.DrawRectangle(border, frame.Left, frame.Top, frame.Width - 1, frame.Height - 1);
        }

        private static void DrawSourceImage(Graphics g, Bitmap image, Rectangle frame)
        {
            Rectangle padded = Rectangle.Inflate(frame, -6, -6);
            padded.Height -= 5;
            SizeF fit = Fit(image.Size, padded.Size);
            var dest = new RectangleF(
                padded.Left + (padded.Width - fit.Width) / 2f,
                padded.Top + (padded.Height - fit.Height) / 2f,
                fit.Width,
                fit.Height);
            g.DrawImage(image, dest);
        }

        private static void DrawGeneratedPlayer(Graphics g, Rectangle frame, Color jersey, Color pants, Color cap, int index)
        {
            float cx = frame.Left + frame.Width / 2f;
            float top = frame.Top + 8f;
            float stride = (index % 4 - 1.5f) * 1.4f;

            using var skin = new SolidBrush(Color.FromArgb(235, 198, 154));
            using var jerseyBrush = new SolidBrush(jersey);
            using var pantsBrush = new SolidBrush(pants);
            using var capBrush = new SolidBrush(cap);
            using var outline = new Pen(Color.FromArgb(210, 24, 28, 34), 2f);
            using var shoe = new Pen(Color.FromArgb(35, 35, 35), 3f);

            g.FillEllipse(skin, cx - 8, top + 6, 16, 16);
            g.DrawEllipse(outline, cx - 8, top + 6, 16, 16);
            g.FillPie(capBrush, cx - 9, top + 3, 18, 14, 180, 180);
            g.DrawArc(outline, cx - 9, top + 3, 18, 14, 180, 180);

            PointF[] torso =
            {
                new PointF(cx - 12, top + 24),
                new PointF(cx + 12, top + 24),
                new PointF(cx + 9, top + 43),
                new PointF(cx - 9, top + 43)
            };
            g.FillPolygon(jerseyBrush, torso);
            g.DrawPolygon(outline, torso);

            g.DrawLine(outline, cx - 10, top + 28, cx - 20, top + 38 + stride);
            g.DrawLine(outline, cx + 10, top + 28, cx + 20, top + 37 - stride);
            g.DrawLine(outline, cx - 4, top + 43, cx - 12 - stride, top + 56);
            g.DrawLine(outline, cx + 4, top + 43, cx + 12 + stride, top + 56);
            using var pantsPen = new Pen(pants, 5f);
            g.DrawLine(pantsPen, cx - 4, top + 43, cx - 12 - stride, top + 54);
            g.DrawLine(pantsPen, cx + 4, top + 43, cx + 12 + stride, top + 54);
            g.DrawLine(shoe, cx - 15 - stride, top + 56, cx - 8 - stride, top + 56);
            g.DrawLine(shoe, cx + 9 + stride, top + 56, cx + 16 + stride, top + 56);
        }

        private static void DrawUniformSwatches(Graphics g, Rectangle frame, Color jersey, Color pants, Color cap)
        {
            Color[] colors = { jersey, pants, cap };
            for (int i = 0; i < colors.Length; i++)
            {
                using var brush = new SolidBrush(colors[i]);
                Rectangle swatch = new Rectangle(frame.Left + 3 + i * 8, frame.Top + 3, 7, 7);
                g.FillRectangle(brush, swatch);
                g.DrawRectangle(Pens.Black, swatch);
            }
        }

        private static Rectangle FrameRectangle(int index)
        {
            int column = index % SpriteSheetGeneratorOptions.Columns;
            int row = index / SpriteSheetGeneratorOptions.Columns;
            return new Rectangle(
                column * SpriteSheetGeneratorOptions.FrameWidth,
                row * SpriteSheetGeneratorOptions.FrameHeight,
                SpriteSheetGeneratorOptions.FrameWidth,
                SpriteSheetGeneratorOptions.FrameHeight);
        }

        private static List<Bitmap> LoadSourceImages(IReadOnlyList<string> paths)
        {
            var images = new List<Bitmap>();
            foreach (string path in paths?.Where(File.Exists) ?? Enumerable.Empty<string>())
            {
                try
                {
                    using Image image = Image.FromFile(path);
                    images.Add(new Bitmap(image));
                }
                catch
                {
                    // Bad source images are ignored so one corrupt file does not block the whole sheet.
                }
            }
            return images;
        }

        private static SizeF Fit(Size source, Size bounds)
        {
            if (source.Width <= 0 || source.Height <= 0 || bounds.Width <= 0 || bounds.Height <= 0)
                return SizeF.Empty;
            float ratio = Math.Min(bounds.Width / (float)source.Width, bounds.Height / (float)source.Height);
            return new SizeF(source.Width * ratio, source.Height * ratio);
        }
    }
}
