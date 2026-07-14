using System.Drawing;
using System.Drawing.Drawing2D;

namespace StandaloneBaseball
{
    internal static class CodexWatermark
    {
        public static void Draw(Graphics graphics, Rectangle bounds)
        {
            if (graphics == null || bounds.Width <= 0 || bounds.Height <= 0)
                return;

            const string text = "Created With Codex";
            float fontSize = Math.Max(18f, Math.Min(34f, bounds.Width / 32f));
            using var font = CreateScriptFont(fontSize);
            using var path = new GraphicsPath();
            using var shadow = new Pen(Color.FromArgb(170, Color.White), Math.Max(2f, fontSize / 11f)) { LineJoin = LineJoin.Round };
            using var fill = new SolidBrush(Color.FromArgb(18, 72, 210));

            SizeF size = graphics.MeasureString(text, font);
            float x = bounds.Left + Math.Max(18f, bounds.Width * 0.025f);
            float y = bounds.Bottom - size.Height - Math.Max(14f, bounds.Height * 0.025f);

            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            path.AddString(text, font.FontFamily, (int)font.Style, graphics.DpiY * font.Size / 72f, new PointF(x, y), StringFormat.GenericDefault);
            graphics.DrawPath(shadow, path);
            graphics.FillPath(fill, path);
        }

        private static Font CreateScriptFont(float size)
        {
            try { return new Font("Brush Script MT", size, FontStyle.Italic); }
            catch { return new Font("Segoe Script", size, FontStyle.Italic); }
        }
    }
}
