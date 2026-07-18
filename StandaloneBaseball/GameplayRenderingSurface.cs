using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

#nullable enable annotations

namespace StandaloneBaseball
{
    internal sealed class GameplayRenderingSurface : Control
    {
        private static readonly Font HudFont = new Font("Segoe UI", 11f, FontStyle.Bold);
        private static readonly Font HudSmallFont = new Font("Segoe UI", 9f, FontStyle.Regular);
        private static readonly Font MarkerFont = new Font("Segoe UI", 8f, FontStyle.Bold);
        private static readonly Font ModeFont = new Font("Segoe UI", 13f, FontStyle.Bold);

        private GameplayRenderingGameState? _state;
        private readonly Dictionary<string, Image?> _spriteCache = new Dictionary<string, Image?>(StringComparer.OrdinalIgnoreCase);

        public GameplayRenderingSurface()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            BackColor = Color.FromArgb(25, 95, 54);
        }

        public void SetState(GameplayRenderingGameState state)
        {
            _state = state;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            Rectangle bounds = ClientRectangle;
            if (bounds.Width <= 0 || bounds.Height <= 0)
                return;

            BaseballFieldPreset preset = _state?.FieldPreset ?? BaseballFieldPresets.Default;
            bool photoBackground = DrawBackdrop(g, bounds, preset);

            Rectangle field = GetFieldBounds(bounds, _state);
            DrawField(g, field, preset, photoBackground);
            DrawFieldOverlays(g, field, preset);

            if (_state != null)
            {
                DrawBases(g, field, _state);
                DrawBatter(g, field, _state);
                DrawFielders(g, field, _state);
                DrawReplayActors(g, field, _state);
                DrawBall(g, field, _state);
                DrawHud(g, bounds, _state);
                DrawMode(g, bounds, _state);
            }
        }

        private static Rectangle GetFieldBounds(Rectangle bounds, GameplayRenderingGameState? state)
        {
            int hudHeight = GameplayScoreboardPresentation.HudHeight(bounds, state);
            int sidePadding = Math.Max(24, bounds.Width / 30);
            int bottomPadding = 24;
            return new Rectangle(
                bounds.Left + sidePadding,
                bounds.Top + hudHeight,
                Math.Max(240, bounds.Width - sidePadding * 2),
                Math.Max(240, bounds.Height - hudHeight - bottomPadding));
        }

        private bool DrawBackdrop(Graphics g, Rectangle bounds, BaseballFieldPreset preset)
        {
            if (TryDrawPresetBackground(g, bounds, preset))
                return true;

            DrawGeneratedBackdrop(g, bounds, preset);
            return false;
        }

        private bool TryDrawPresetBackground(Graphics g, Rectangle bounds, BaseballFieldPreset preset)
        {
            string? path = ResolvePresetAssetPath(preset);
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return false;

            try
            {
                if (!_spriteCache.TryGetValue(path, out Image? image))
                {
                    image = Image.FromFile(path);
                    _spriteCache[path] = image;
                }

                if (image == null)
                    return false;

                Rectangle dest = CoverImage(image.Size, bounds);
                g.DrawImage(image, dest);
                using var shade = new SolidBrush(Color.FromArgb(45, Color.Black));
                g.FillRectangle(shade, bounds);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string? ResolvePresetAssetPath(BaseballFieldPreset preset)
        {
            if (preset == null || string.IsNullOrWhiteSpace(preset.BackgroundAssetPath))
                return null;
            return AssetPathResolver.ResolvePath(preset.BackgroundAssetPath);
        }

        private static string? ResolveAssetPath(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
                return null;
            return AssetPathResolver.ResolvePath(assetPath);
        }

        private static Rectangle CoverImage(Size imageSize, Rectangle bounds)
        {
            if (imageSize.Width <= 0 || imageSize.Height <= 0 || bounds.Width <= 0 || bounds.Height <= 0)
                return bounds;

            double scale = Math.Max((double)bounds.Width / imageSize.Width, (double)bounds.Height / imageSize.Height);
            int width = (int)Math.Ceiling(imageSize.Width * scale);
            int height = (int)Math.Ceiling(imageSize.Height * scale);
            return new Rectangle(
                bounds.Left + (bounds.Width - width) / 2,
                bounds.Top + (bounds.Height - height) / 2,
                width,
                height);
        }

        private static void DrawGeneratedBackdrop(Graphics g, Rectangle bounds, BaseballFieldPreset preset)
        {
            using var grass = new LinearGradientBrush(bounds, preset.GrassColor, preset.DarkGrassColor, 90f);
            g.FillRectangle(grass, bounds);

            using var stripe = new SolidBrush(Color.FromArgb(18, Color.White));
            int stripeWidth = Math.Max(34, bounds.Width / 18);
            for (int x = -stripeWidth; x < bounds.Width + stripeWidth; x += stripeWidth * 2)
            {
                Point[] poly =
                {
                    new Point(x, bounds.Top),
                    new Point(x + stripeWidth, bounds.Top),
                    new Point(x + stripeWidth * 2, bounds.Bottom),
                    new Point(x + stripeWidth, bounds.Bottom)
                };
                g.FillPolygon(stripe, poly);
            }
        }

        private static void DrawField(Graphics g, Rectangle field, BaseballFieldPreset preset, bool photoBackground)
        {
            PointF home = Map(field, 0.5f, 0.86f);
            PointF first = Map(field, 0.64f, 0.72f);
            PointF second = Map(field, 0.5f, 0.58f);
            PointF third = Map(field, 0.36f, 0.72f);
            PointF leftFoul = Map(field, 0.08f, 0.18f);
            PointF rightFoul = Map(field, 0.92f, 0.18f);

            using var outfieldBrush = new SolidBrush(preset.GrassColor);
            using var infieldBrush = new SolidBrush(preset.InfieldColor);
            using var clayBrush = new SolidBrush(preset.ClayColor);
            using var chalk = new Pen(Color.FromArgb(235, 245, 230), 2f);
            using var fence = new Pen(preset.WallColor, 4f);

            if (photoBackground)
            {
                DrawPhotoFieldGuides(g, field, preset, home, leftFoul, rightFoul);
                DrawFieldLabel(g, field, preset);
                return;
            }

            Rectangle fenceRect = new Rectangle(
                field.Left,
                field.Top + (int)(field.Height * preset.FenceTopOffset),
                field.Width,
                field.Height);
            g.FillPie(outfieldBrush, fenceRect, preset.FenceStartAngle, preset.FenceSweepAngle);
            g.DrawArc(fence, fenceRect, preset.FenceStartAngle, preset.FenceSweepAngle);
            DrawStadiumArchitecture(g, field, preset, fenceRect);

            PointF[] infield = { home, first, second, third };
            g.FillPolygon(infieldBrush, infield);
            g.DrawPolygon(chalk, infield);

            float moundRadius = Math.Max(18f, field.Width * 0.035f);
            PointF mound = Map(field, 0.5f, 0.62f);
            FillEllipse(g, clayBrush, mound, moundRadius * 1.35f, moundRadius);

            g.DrawLine(chalk, home, leftFoul);
            g.DrawLine(chalk, home, rightFoul);

            PointF plateTop = Map(field, 0.5f, 0.835f);
            PointF[] plate =
            {
                new PointF(home.X - 11, plateTop.Y),
                new PointF(home.X + 11, plateTop.Y),
                new PointF(home.X + 9, home.Y + 9),
                new PointF(home.X, home.Y + 16),
                new PointF(home.X - 9, home.Y + 9)
            };
            using var plateBrush = new SolidBrush(Color.White);
            g.FillPolygon(plateBrush, plate);
            g.DrawPolygon(Pens.DimGray, plate);

            DrawFieldLabel(g, field, preset);
        }

        private static void DrawPhotoFieldGuides(Graphics g, Rectangle field, BaseballFieldPreset preset, PointF home, PointF leftFoul, PointF rightFoul)
        {
            using var chalk = new Pen(Color.FromArgb(180, 255, 255, 255), 2f);
            using var clay = new SolidBrush(Color.FromArgb(85, preset.ClayColor));
            using var accent = new Pen(Color.FromArgb(210, preset.AccentColor), 4f);
            g.DrawLine(chalk, home, leftFoul);
            g.DrawLine(chalk, home, rightFoul);
            PointF mound = Map(field, 0.5f, 0.62f);
            FillEllipse(g, clay, mound, Math.Max(26f, field.Width * 0.045f), Math.Max(14f, field.Width * 0.024f));
            g.DrawEllipse(accent, mound.X - 18, mound.Y - 9, 36, 18);
        }

        private static void DrawStadiumArchitecture(Graphics g, Rectangle field, BaseballFieldPreset preset, Rectangle fenceRect)
        {
            using var structure = new SolidBrush(Color.FromArgb(225, preset.StructureColor));
            using var seats = new SolidBrush(Color.FromArgb(230, preset.SeatColor));
            using var accent = new SolidBrush(Color.FromArgb(230, preset.AccentColor));
            using var line = new Pen(Color.FromArgb(135, 20, 25, 25), 2f);

            Rectangle upper = new Rectangle(field.Left + field.Width / 8, field.Top + 8, field.Width * 3 / 4, Math.Max(28, field.Height / 9));
            switch (preset.Variant)
            {
                case 1:
                    g.FillRectangle(accent, field.Left + field.Width * 2 / 3, field.Top + field.Height / 8, field.Width / 10, field.Height / 3);
                    g.DrawRectangle(line, field.Left + field.Width * 2 / 3, field.Top + field.Height / 8, field.Width / 10, field.Height / 3);
                    DrawGrandstand(g, upper, structure, seats, line, split: true);
                    break;
                case 3:
                case 13:
                case 15:
                    DrawGrandstand(g, upper, structure, seats, line, split: false);
                    using (var water = new SolidBrush(Color.FromArgb(60, 94, 170, 180)))
                        g.FillEllipse(water, field.Left - field.Width / 8, field.Top, field.Width / 4, field.Height / 2);
                    break;
                case 6:
                    DrawMissionArches(g, upper, structure, accent, line);
                    break;
                case 7:
                case 16:
                    DrawHills(g, field, preset);
                    DrawGrandstand(g, upper, structure, seats, line, split: false);
                    break;
                case 18:
                    DrawGrandstand(g, upper, structure, seats, line, split: false);
                    g.FillRectangle(accent, upper.Left + upper.Width / 5, upper.Top + 4, upper.Width / 5, upper.Height - 8);
                    break;
                case 19:
                    DrawPalmPark(g, field, preset);
                    DrawGrandstand(g, upper, structure, seats, line, split: true);
                    break;
                default:
                    DrawGrandstand(g, upper, structure, seats, line, split: preset.Variant % 3 == 2);
                    break;
            }

            using var wallBrush = new SolidBrush(Color.FromArgb(85, preset.WallColor));
            g.DrawArc(new Pen(Color.FromArgb(180, preset.WallColor), 8f), fenceRect, preset.FenceStartAngle, preset.FenceSweepAngle);
        }

        private static void DrawGrandstand(Graphics g, Rectangle bounds, Brush structure, Brush seats, Pen line, bool split)
        {
            if (split)
            {
                Rectangle left = new Rectangle(bounds.Left, bounds.Top + bounds.Height / 5, bounds.Width * 2 / 5, bounds.Height);
                Rectangle right = new Rectangle(bounds.Right - bounds.Width * 2 / 5, bounds.Top + bounds.Height / 5, bounds.Width * 2 / 5, bounds.Height);
                DrawStandBlock(g, left, structure, seats, line);
                DrawStandBlock(g, right, structure, seats, line);
                return;
            }

            DrawStandBlock(g, bounds, structure, seats, line);
        }

        private static void DrawStandBlock(Graphics g, Rectangle rect, Brush structure, Brush seats, Pen line)
        {
            g.FillRectangle(structure, rect);
            g.DrawRectangle(line, rect);
            int rows = 4;
            for (int i = 1; i <= rows; i++)
            {
                int y = rect.Top + i * rect.Height / (rows + 1);
                g.DrawLine(line, rect.Left + 6, y, rect.Right - 6, y);
            }

            Rectangle seatRect = new Rectangle(rect.Left + 8, rect.Top + 6, Math.Max(1, rect.Width - 16), Math.Max(1, rect.Height / 3));
            g.FillRectangle(seats, seatRect);
        }

        private static void DrawMissionArches(Graphics g, Rectangle bounds, Brush structure, Brush accent, Pen line)
        {
            g.FillRectangle(structure, bounds);
            g.DrawRectangle(line, bounds);
            int archCount = 5;
            int archWidth = bounds.Width / archCount;
            for (int i = 0; i < archCount; i++)
            {
                Rectangle arch = new Rectangle(bounds.Left + i * archWidth + 6, bounds.Top + 8, Math.Max(8, archWidth - 12), bounds.Height - 10);
                g.FillPie(accent, arch, 180, 180);
                g.DrawArc(line, arch, 180, 180);
            }
        }

        private static void DrawHills(Graphics g, Rectangle field, BaseballFieldPreset preset)
        {
            using var hill = new SolidBrush(Color.FromArgb(120, preset.StructureColor));
            Point[] leftHill =
            {
                new Point(field.Left, field.Top + field.Height / 3),
                new Point(field.Left + field.Width / 8, field.Top + field.Height / 8),
                new Point(field.Left + field.Width / 4, field.Top + field.Height / 3)
            };
            Point[] rightHill =
            {
                new Point(field.Right - field.Width / 4, field.Top + field.Height / 3),
                new Point(field.Right - field.Width / 8, field.Top + field.Height / 10),
                new Point(field.Right, field.Top + field.Height / 3)
            };
            g.FillPolygon(hill, leftHill);
            g.FillPolygon(hill, rightHill);
        }

        private static void DrawPalmPark(Graphics g, Rectangle field, BaseballFieldPreset preset)
        {
            using var trunk = new Pen(Color.FromArgb(120, 91, 62, 35), 3f);
            using var leaf = new Pen(Color.FromArgb(160, preset.GrassColor), 4f);
            for (int i = 0; i < 4; i++)
            {
                float x = field.Left + field.Width * (0.12f + i * 0.25f);
                float y = field.Top + field.Height * 0.19f;
                g.DrawLine(trunk, x, y + 28, x + 5, y);
                g.DrawLine(leaf, x + 5, y, x - 14, y + 9);
                g.DrawLine(leaf, x + 5, y, x + 22, y + 7);
                g.DrawLine(leaf, x + 5, y, x + 3, y - 16);
            }
        }

        private static void DrawFieldLabel(Graphics g, Rectangle field, BaseballFieldPreset preset)
        {
            Rectangle label = new Rectangle(field.Left + 12, field.Bottom - 42, Math.Min(field.Width - 24, 520), 30);
            using var bg = new SolidBrush(Color.FromArgb(165, 18, 26, 24));
            g.FillRectangle(bg, label);
            string text = preset.Name + " (" + preset.OpenedYear + ") - " + preset.TeamLabel;
            TextRenderer.DrawText(g, text, HudSmallFont, label, Color.White, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        private void DrawFieldOverlays(Graphics g, Rectangle field, BaseballFieldPreset preset)
        {
            if (preset?.Overlays == null || preset.Overlays.Count == 0)
                return;

            foreach (var overlay in preset.Overlays)
            {
                string? path = ResolveAssetPath(overlay.AssetPath);
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                    continue;

                try
                {
                    if (!_spriteCache.TryGetValue(path, out Image? image))
                    {
                        image = Image.FromFile(path);
                        _spriteCache[path] = image;
                    }

                    if (image == null)
                        continue;

                    float width = Math.Clamp(overlay.Width, 0.02f, 1f) * field.Width;
                    float height = Math.Clamp(overlay.Height, 0.02f, 1f) * field.Height;
                    float x = field.Left + Math.Clamp(overlay.X, 0f, 1f) * field.Width - width / 2f;
                    float y = field.Top + Math.Clamp(overlay.Y, 0f, 1f) * field.Height - height / 2f;
                    var dest = new RectangleF(x, y, width, height);
                    int opacity = Math.Clamp(overlay.Opacity, 0, 255);

                    if (opacity >= 255)
                    {
                        g.DrawImage(image, dest);
                    }
                    else
                    {
                        using var attributes = new ImageAttributes();
                        var matrix = new ColorMatrix { Matrix33 = opacity / 255f };
                        attributes.SetColorMatrix(matrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
                        g.DrawImage(image, Rectangle.Round(dest), 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attributes);
                    }
                }
                catch
                {
                    // Ignore invalid user-added images; the editor keeps the field usable.
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (Image? image in _spriteCache.Values)
                    image?.Dispose();
                _spriteCache.Clear();
            }

            base.Dispose(disposing);
        }

        private void DrawBases(Graphics g, Rectangle field, GameplayRenderingGameState state)
        {
            PointF[] bases =
            {
                Map(field, 0.64f, 0.72f),
                Map(field, 0.5f, 0.58f),
                Map(field, 0.36f, 0.72f)
            };

            for (int i = 0; i < bases.Length; i++)
            {
                GraphicsState saved = g.Save();
                g.TranslateTransform(bases[i].X, bases[i].Y);
                g.RotateTransform(45f);
                RectangleF baseRect = new RectangleF(-9, -9, 18, 18);
                using var brush = new SolidBrush(Color.White);
                g.FillRectangle(brush, baseRect);
                g.DrawRectangle(Pens.DimGray, baseRect.X, baseRect.Y, baseRect.Width, baseRect.Height);
                g.Restore(saved);

                if (state.Bases[i].Occupied)
                {
                    PointF runnerPoint = bases[i];
                    if (state.Phase == GameplayRenderingPhase.BallInPlay)
                    {
                        PointF next = i == 0 ? bases[1] : i == 1 ? bases[2] : Map(field, 0.5f, 0.84f);
                        runnerPoint = Lerp(runnerPoint, next, Math.Min(0.88f, state.AnimationProgress * 0.9f));
                    }
                    DrawSpriteOrMarker(g, runnerPoint.X, runnerPoint.Y - 24, 12, state.Bases[i].RunnerColor, "R", true,
                        state.Bases[i].Player, state.Bases[i].Team,
                        state.Phase == GameplayRenderingPhase.BallInPlay ? 6 : 14);
                }
            }
        }

        private void DrawBatter(Graphics g, Rectangle field, GameplayRenderingGameState state)
        {
            Player? batter = state.CurrentBatterPlayer();
            if (batter == null)
                return;

            bool leftHanded = string.Equals(batter.Bats, "L", StringComparison.OrdinalIgnoreCase);
            PointF home = Map(field, leftHanded ? 0.535f : 0.465f, 0.835f);
            int frame = 4;
            if (state.Phase == GameplayRenderingPhase.Pitching && state.BallPosition.Y >= 0.73f)
                frame = 5;
            else if (state.Phase == GameplayRenderingPhase.BallInPlay)
            {
                home = Lerp(home, Map(field, 0.64f, 0.72f), Math.Min(0.92f, state.AnimationProgress));
                frame = 6;
            }

            DrawSpriteOrMarker(g, home.X, home.Y - 24, 14, state.OffenseColor, "B", true,
                batter, state.BattingTeam, frame);
        }

        private void DrawFielders(Graphics g, Rectangle field, GameplayRenderingGameState state)
        {
            for (int i = 0; i < state.Fielders.Count; i++)
            {
                GameplayRenderingPlayerMarker marker = state.Fielders[i];
                PointF p = Map(field, marker.Position.X, marker.Position.Y);
                DrawSpriteOrMarker(g, p.X, p.Y, i == state.ActiveFielderIndex ? 15 : 12, marker.Color, marker.Label,
                    i == state.ActiveFielderIndex, marker.Player, marker.Team, FielderFrame(marker, state, i));
            }
        }

        private void DrawReplayActors(Graphics g, Rectangle field, GameplayRenderingGameState state)
        {
            foreach (GameplayRenderingPlayerMarker marker in state.ReplayActors)
            {
                PointF point = Map(field, marker.Position.X, marker.Position.Y);
                DrawSpriteOrMarker(g, point.X, point.Y, marker.Runner ? 13 : 14, marker.Color,
                    string.IsNullOrWhiteSpace(marker.Label) ? (marker.Runner ? "R" : "F") : marker.Label,
                    marker.Detail == "highlight", marker.Player, marker.Team, marker.Runner ? 6 : 1);
            }
        }

        private static void DrawBall(Graphics g, Rectangle field, GameplayRenderingGameState state)
        {
            if (!state.BallVisible)
                return;

            PointF ball = Map(field, state.BallPosition.X, state.BallPosition.Y);
            float lift = Math.Clamp(state.BallHeight, 0f, 1f) * Math.Max(20f, field.Height * 0.16f);
            ball.Y -= lift;
            if (state.BallTrail > 0.01f)
            {
                PointF mound = state.Phase == GameplayRenderingPhase.BallInPlay
                    ? Map(field, 0.5f, 0.84f)
                    : Map(field, 0.5f, 0.62f);
                using var trail = new Pen(Color.FromArgb(120, Color.White), 3f);
                g.DrawLine(trail, mound, ball);
            }

            float radius = 6f + Math.Clamp(state.BallHeight, 0f, 1f) * 3f;
            using var shadow = new SolidBrush(Color.FromArgb(90, Color.Black));
            g.FillEllipse(shadow, ball.X - radius + 1, ball.Y + radius - 2, radius * 2, Math.Max(5f, radius));

            using var brush = new SolidBrush(Color.White);
            using var seam = new Pen(Color.FromArgb(200, 45, 45), 1.2f);
            g.FillEllipse(brush, ball.X - radius, ball.Y - radius, radius * 2, radius * 2);
            g.DrawEllipse(Pens.DimGray, ball.X - radius, ball.Y - radius, radius * 2, radius * 2);
            g.DrawArc(seam, ball.X - radius * 0.65f, ball.Y - radius * 0.85f, radius * 1.3f, radius * 1.7f, 75, 205);
            g.DrawArc(seam, ball.X - radius * 0.65f, ball.Y - radius * 0.85f, radius * 1.3f, radius * 1.7f, -105, 205);
        }

        private static void DrawHud(Graphics g, Rectangle bounds, GameplayRenderingGameState state)
        {
            if (GameplayScoreboardPresentation.UsesCustomScoreboard(state))
            {
                int height = GameplayScoreboardPresentation.HudHeight(bounds, state);
                ScoreboardTemplateRenderer.Draw(
                    g,
                    new Rectangle(bounds.Left, bounds.Top, bounds.Width, height),
                    state.HomeTeam,
                    state.HomeLogoPath,
                    GameplayScoreboardPresentation.ScoreText(state),
                    GameplayScoreboardPresentation.InningText(state),
                    GameplayScoreboardPresentation.CountText(state));
                return;
            }

            Rectangle hud = new Rectangle(bounds.Left, bounds.Top, bounds.Width, 82);
            using var hudBrush = new SolidBrush(Color.FromArgb(230, 19, 29, 34));
            g.FillRectangle(hudBrush, hud);

            using var borderPen = new Pen(Color.FromArgb(80, Color.White));
            g.DrawLine(borderPen, hud.Left, hud.Bottom - 1, hud.Right, hud.Bottom - 1);

            string half = state.TopHalf ? "TOP" : "BOT";
            string line = state.AwayName + " " + state.AwayScore + "    " + state.HomeName + " " + state.HomeScore;
            string count = "B " + state.Balls + "  S " + state.Strikes + "  O " + state.Outs;
            string inning = half + " " + state.Inning;

            TextRenderer.DrawText(g, line, HudFont, new Rectangle(18, 12, bounds.Width - 36, 24), Color.White, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(g, inning, HudSmallFont, new Rectangle(18, 42, 120, 22), Color.FromArgb(230, 240, 238), TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(g, count, HudSmallFont, new Rectangle(140, 42, 180, 22), Color.FromArgb(230, 240, 238), TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

            DrawCountLights(g, new Point(bounds.Right - 190, 32), state.Balls, 4, Color.FromArgb(89, 215, 116), "B");
            DrawCountLights(g, new Point(bounds.Right - 125, 32), state.Strikes, 3, Color.FromArgb(242, 198, 64), "S");
            DrawCountLights(g, new Point(bounds.Right - 66, 32), state.Outs, 3, Color.FromArgb(235, 85, 75), "O");
        }

        private static void DrawMode(Graphics g, Rectangle bounds, GameplayRenderingGameState state)
        {
            Rectangle mode = new Rectangle(bounds.Left + 18, bounds.Bottom - 44, Math.Min(420, bounds.Width - 36), 28);
            using var fill = new SolidBrush(Color.FromArgb(185, 0, 0, 0));
            using var outline = new Pen(Color.FromArgb(90, Color.White));
            g.FillRectangle(fill, mode);
            g.DrawRectangle(outline, mode);
            TextRenderer.DrawText(g, state.ModeLabel ?? "", ModeFont, mode, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            if (!string.IsNullOrWhiteSpace(state.ControlHint))
            {
                Rectangle hint = new Rectangle(mode.Right + 10, mode.Top, Math.Max(1, bounds.Right - mode.Right - 28), mode.Height);
                using var hintFill = new SolidBrush(Color.FromArgb(185, 0, 0, 0));
                g.FillRectangle(hintFill, hint);
                TextRenderer.DrawText(g, state.ControlHint, HudSmallFont, hint, Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
        }

        private static void DrawCountLights(Graphics g, Point origin, int active, int total, Color activeColor, string label)
        {
            TextRenderer.DrawText(g, label, HudSmallFont, new Rectangle(origin.X - 20, origin.Y - 9, 16, 18), Color.FromArgb(220, 230, 230), TextFormatFlags.Right);
            for (int i = 0; i < total; i++)
            {
                Color color = i < active ? activeColor : Color.FromArgb(70, 92, 96, 98);
                using var brush = new SolidBrush(color);
                g.FillEllipse(brush, origin.X + i * 15, origin.Y - 5, 10, 10);
            }
        }

        private static void DrawMarker(Graphics g, float x, float y, float radius, Color color, string label, bool highlighted)
        {
            using var shadow = new SolidBrush(Color.FromArgb(95, Color.Black));
            g.FillEllipse(shadow, x - radius + 2, y - radius + 3, radius * 2, radius * 2);

            using var brush = new SolidBrush(color);
            using var ring = new Pen(highlighted ? Color.White : Color.FromArgb(215, 245, 245, 245), highlighted ? 3f : 1.5f);
            g.FillEllipse(brush, x - radius, y - radius, radius * 2, radius * 2);
            g.DrawEllipse(ring, x - radius, y - radius, radius * 2, radius * 2);

            TextRenderer.DrawText(g, label, MarkerFont, new Rectangle((int)(x - radius), (int)(y - 8), (int)(radius * 2), 16), Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        private void DrawSpriteOrMarker(Graphics g, float x, float y, float radius, Color color, string label,
            bool highlighted, Player? player, Team? team, int frameIndex)
        {
            Image? sheet = LoadSpriteSheet(player, team);
            if (sheet == null)
            {
                DrawMarker(g, x, y, radius, color, label, highlighted);
                return;
            }

            int frameWidth = Math.Min(SpriteSheetGeneratorOptions.FrameWidth, sheet.Width);
            int frameHeight = Math.Min(SpriteSheetGeneratorOptions.FrameHeight, sheet.Height);
            if (frameWidth <= 0 || frameHeight <= 0)
            {
                DrawMarker(g, x, y, radius, color, label, highlighted);
                return;
            }

            int frameCount = Math.Max(1, (sheet.Width / frameWidth) * (sheet.Height / frameHeight));
            frameIndex = Math.Clamp(frameIndex, 0, frameCount - 1);
            int columns = Math.Max(1, sheet.Width / frameWidth);
            float drawSize = highlighted ? 72f : 62f;
            Rectangle src = new Rectangle((frameIndex % columns) * frameWidth, (frameIndex / columns) * frameHeight, frameWidth, frameHeight);
            RectangleF dest = new RectangleF(x - drawSize / 2f, y - drawSize / 2f, drawSize, drawSize);

            using var shadow = new SolidBrush(Color.FromArgb(100, Color.Black));
            g.FillEllipse(shadow, x - drawSize / 2f + 3, y + drawSize / 2f - 8, drawSize, 12);
            g.DrawImage(sheet, dest, src, GraphicsUnit.Pixel);

            if (highlighted)
            {
                using var ring = new Pen(Color.White, 3f);
                g.DrawEllipse(ring, x - drawSize / 2f, y - drawSize / 2f, drawSize, drawSize);
            }

            if (!string.IsNullOrWhiteSpace(label) && label != "B" && label != "R")
            {
                Rectangle badge = new Rectangle((int)x - 18, (int)(y + drawSize / 2f - 4), 36, 18);
                using var badgeFill = new SolidBrush(Color.FromArgb(210, color));
                g.FillEllipse(badgeFill, badge);
                TextRenderer.DrawText(g, label, MarkerFont, badge, Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
        }

        private Image? LoadSpriteSheet(Player? player, Team? team)
        {
            string? path = FirstExistingPath(player?.SpriteSheetPath, team?.SpriteSheetPath);
            if (string.IsNullOrWhiteSpace(path))
                return LoadGeneratedSpriteSheet(player, team);

            if (_spriteCache.TryGetValue(path, out Image? cached))
                return cached;

            try
            {
                using Image source = Image.FromFile(path);
                Image image = new Bitmap(source);
                _spriteCache[path] = image;
                return image;
            }
            catch
            {
                _spriteCache[path] = null;
                return null;
            }
        }

        private Image? LoadGeneratedSpriteSheet(Player? player, Team? team)
        {
            if (team == null)
                return null;
            string key = "generated:" + team.Id.ToString("N") + ":" + (player?.Id.ToString("N") ?? "generic") + ":" +
                team.PrimaryArgb + ":" + team.SecondaryArgb;
            if (_spriteCache.TryGetValue(key, out Image? cached))
                return cached;

            try
            {
                Image generated = SpriteSheetGenerator.Generate(new SpriteSheetGeneratorOptions
                {
                    Team = team,
                    Player = player,
                    Label = player?.Name ?? team.ScoreboardName,
                    CleanGameplayFrames = true
                });
                _spriteCache[key] = generated;
                return generated;
            }
            catch
            {
                _spriteCache[key] = null;
                return null;
            }
        }

        private static int FielderFrame(GameplayRenderingPlayerMarker marker, GameplayRenderingGameState state, int index)
        {
            if (marker.Label == "P" && state.Phase == GameplayRenderingPhase.Pitching)
                return state.AnimationProgress < 0.45f ? 9 : 8;
            if (marker.Label == "C")
                return 12;
            if (state.Phase == GameplayRenderingPhase.BallInPlay && index == state.ActiveFielderIndex)
                return state.AnimationProgress < 0.78f ? 1 : 3;
            return 0;
        }

        private static string? FirstExistingPath(params string?[] paths)
        {
            foreach (string? raw in paths)
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;
                string path = AssetPathResolver.ResolvePath(raw);
                if (File.Exists(path))
                    return path;
            }

            return null;
        }

        private static void FillEllipse(Graphics g, Brush brush, PointF center, float rx, float ry)
            => g.FillEllipse(brush, center.X - rx, center.Y - ry, rx * 2f, ry * 2f);

        private static PointF Map(Rectangle field, float x, float y)
            => new PointF(field.Left + field.Width * x, field.Top + field.Height * y);

        private static PointF Lerp(PointF start, PointF end, float progress)
            => new PointF(start.X + (end.X - start.X) * progress, start.Y + (end.Y - start.Y) * progress);
    }
}
