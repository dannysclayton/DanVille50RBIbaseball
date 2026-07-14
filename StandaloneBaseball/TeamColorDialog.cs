#nullable enable annotations

using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace StandaloneBaseball
{
    public sealed class TeamColorDialog : Form
    {
        private readonly Panel _preview;
        private readonly TextBox _hexBox;
        private readonly NumericUpDown _rBox;
        private readonly NumericUpDown _gBox;
        private readonly NumericUpDown _bBox;
        private bool _suppress;

        public Color SelectedColor { get; private set; }

        public TeamColorDialog(Color initial, string title)
        {
            SelectedColor = Color.FromArgb(255, initial.R, initial.G, initial.B);
            Text = title;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            ClientSize = new Size(430, 330);
            MaximizeBox = false;
            MinimizeBox = false;

            _preview = new Panel
            {
                Location = new Point(16, 16),
                Size = new Size(120, 72),
                BorderStyle = BorderStyle.FixedSingle
            };
            Controls.Add(_preview);

            Controls.Add(new Label { Text = "Hex", Location = new Point(156, 20), AutoSize = true });
            _hexBox = new TextBox { Location = new Point(206, 16), Width = 110 };
            _hexBox.Leave += (s, e) => ApplyHex();
            _hexBox.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    ApplyHex();
                    e.SuppressKeyPress = true;
                }
            };
            Controls.Add(_hexBox);

            var native = new Button { Text = "Palette...", Location = new Point(326, 15), Size = new Size(84, 25) };
            native.Click += (s, e) => OpenNativePalette();
            Controls.Add(native);

            int y = 104;
            _rBox = AddChannel("R", y, SelectedColor.R); y += 36;
            _gBox = AddChannel("G", y, SelectedColor.G); y += 36;
            _bBox = AddChannel("B", y, SelectedColor.B);

            Controls.Add(new Label { Text = "Presets", Location = new Point(16, 220), AutoSize = true });
            AddSwatches();

            var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(238, 292), Size = new Size(82, 26) };
            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(328, 292), Size = new Size(82, 26) };
            Controls.Add(ok);
            Controls.Add(cancel);
            AcceptButton = ok;
            CancelButton = cancel;

            SetColor(SelectedColor);
        }

        private NumericUpDown AddChannel(string label, int y, int value)
        {
            Controls.Add(new Label { Text = label, Location = new Point(16, y + 4), AutoSize = true });
            var slider = new TrackBar
            {
                Location = new Point(48, y),
                Size = new Size(240, 30),
                Minimum = 0,
                Maximum = 255,
                TickFrequency = 32,
                Value = value
            };
            var box = new NumericUpDown
            {
                Location = new Point(306, y + 2),
                Width = 64,
                Minimum = 0,
                Maximum = 255,
                Value = value
            };

            slider.ValueChanged += (s, e) =>
            {
                if (_suppress) return;
                box.Value = slider.Value;
                SetColor(Color.FromArgb((int)_rBox.Value, (int)_gBox.Value, (int)_bBox.Value));
            };
            box.ValueChanged += (s, e) =>
            {
                if (_suppress) return;
                slider.Value = (int)box.Value;
                SetColor(Color.FromArgb((int)_rBox.Value, (int)_gBox.Value, (int)_bBox.Value));
            };

            Controls.Add(slider);
            Controls.Add(box);
            return box;
        }

        private void AddSwatches()
        {
            Color[] colors =
            {
                Color.Black, Color.White, Color.Silver, Color.FromArgb(35, 38, 45),
                Color.FromArgb(0, 90, 156), Color.FromArgb(196, 30, 58), Color.FromArgb(0, 104, 71),
                Color.FromArgb(255, 184, 28), Color.FromArgb(253, 93, 53), Color.FromArgb(111, 38, 61),
                Color.FromArgb(12, 35, 64), Color.FromArgb(0, 45, 114), Color.FromArgb(134, 38, 51),
                Color.FromArgb(92, 71, 56), Color.FromArgb(0, 163, 173), Color.FromArgb(122, 38, 139)
            };

            for (int i = 0; i < colors.Length; i++)
            {
                var swatch = new Panel
                {
                    Location = new Point(16 + (i % 8) * 36, 244 + (i / 8) * 30),
                    Size = new Size(28, 22),
                    BackColor = colors[i],
                    BorderStyle = BorderStyle.FixedSingle,
                    Cursor = Cursors.Hand,
                    Tag = colors[i]
                };
                swatch.Click += (s, e) =>
                {
                    if (s is Control control && control.Tag is Color color)
                        SetColor(color);
                };
                Controls.Add(swatch);
            }
        }

        private void ApplyHex()
        {
            if (TryParseHex(_hexBox.Text, out var c))
            {
                SetColor(c);
                return;
            }

            MessageBox.Show(this, "Enter a color as #RRGGBB or RRGGBB.", "Invalid hex color",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _hexBox.Text = ToHex(SelectedColor);
            _hexBox.SelectAll();
        }

        private void OpenNativePalette()
        {
            using var dlg = new ColorDialog { Color = SelectedColor, FullOpen = true };
            if (dlg.ShowDialog(this) == DialogResult.OK)
                SetColor(dlg.Color);
        }

        private void SetColor(Color color)
        {
            SelectedColor = Color.FromArgb(255, color.R, color.G, color.B);
            _suppress = true;
            _preview.BackColor = SelectedColor;
            _hexBox.Text = ToHex(SelectedColor);
            _rBox.Value = SelectedColor.R;
            _gBox.Value = SelectedColor.G;
            _bBox.Value = SelectedColor.B;
            _suppress = false;
        }

        private static string ToHex(Color c) => "#" + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2");

        private static bool TryParseHex(string text, out Color color)
        {
            color = Color.Black;
            string s = (text ?? "").Trim();
            if (s.StartsWith("#")) s = s.Substring(1);
            if (s.Length != 6) return false;
            if (!int.TryParse(s.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int r)) return false;
            if (!int.TryParse(s.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int g)) return false;
            if (!int.TryParse(s.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int b)) return false;
            color = Color.FromArgb(r, g, b);
            return true;
        }
    }
}
