#nullable enable annotations

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;

namespace StandaloneBaseball
{
    internal sealed class FieldEditorDialog : Form
    {
        internal sealed class TeamLogoChoice
        {
            public required Team Team { get; set; }
            public string LogoPath { get; set; } = "";
            public override string ToString() => Team?.DisplayName ?? "Team logo";
        }

        private readonly LeagueFile _league;
        private readonly Func<string, string, string> _importAsset;
        private readonly Func<List<TeamLogoChoice>> _teamLogoChoices;
        private readonly string _initialDirectory;
        private readonly ListBox _fieldList = new ListBox();
        private readonly TextBox _nameBox = new TextBox();
        private readonly TextBox _teamLabelBox = new TextBox();
        private readonly NumericUpDown _yearBox = new NumericUpDown();
        private readonly DataGridView _overlayGrid = new DataGridView();
        private readonly Panel _preview = new Panel();
        private CustomBaseballField? _selected;
        private bool _loading;
        private static readonly JsonSerializerOptions ExportJsonOptions = new JsonSerializerOptions { WriteIndented = true };

        public bool Modified { get; private set; }

        public FieldEditorDialog(
            LeagueFile league,
            Func<string, string, string> importAsset,
            Func<List<TeamLogoChoice>> teamLogoChoices,
            string initialDirectory)
        {
            _league = league ?? new LeagueFile();
            _league.CustomFields ??= new List<CustomBaseballField>();
            _importAsset = importAsset;
            _teamLogoChoices = teamLogoChoices;
            _initialDirectory = Directory.Exists(initialDirectory) ? initialDirectory : Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);

            Text = "Field Editor";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1180, 760);
            MinimumSize = new Size(980, 640);
            BuildUi();
            RefreshFieldList();
            if (_fieldList.Items.Count > 0)
                _fieldList.SelectedIndex = 0;
        }

        private void BuildUi()
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Padding = new Padding(10) };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 360));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            Controls.Add(root);

            var left = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
            left.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            left.RowStyles.Add(new RowStyle(SizeType.Absolute, 86));
            _fieldList.Dock = DockStyle.Fill;
            _fieldList.SelectedIndexChanged += (s, e) => SelectField(_fieldList.SelectedItem as CustomBaseballField);
            left.Controls.Add(_fieldList, 0, 0);
            var fieldButtons = new FlowLayoutPanel { Dock = DockStyle.Fill };
            AddButton(fieldButtons, "New", (s, e) => NewField());
            AddButton(fieldButtons, "Duplicate", (s, e) => DuplicateField());
            AddButton(fieldButtons, "Delete", (s, e) => DeleteField());
            left.Controls.Add(fieldButtons, 0, 1);
            root.Controls.Add(left, 0, 0);

            var editor = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 12, ColumnCount = 2 };
            editor.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
            editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < 10; i++)
                editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            editor.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
            root.Controls.Add(editor, 1, 0);

            AddLabeled(editor, "Name", _nameBox, 0);
            AddLabeled(editor, "Team Label", _teamLabelBox, 1);
            _yearBox.Minimum = 1800;
            _yearBox.Maximum = 2200;
            _yearBox.Value = DateTime.Now.Year;
            AddLabeled(editor, "Year", _yearBox, 2);
            _nameBox.TextChanged += (s, e) => SaveBasicFields();
            _teamLabelBox.TextChanged += (s, e) => SaveBasicFields();
            _yearBox.ValueChanged += (s, e) => SaveBasicFields();

            AddColorButton(editor, "Grass", 3, f => f.GrassArgb, (f, c) => f.GrassArgb = c.ToArgb());
            AddColorButton(editor, "Dark Grass", 4, f => f.DarkGrassArgb, (f, c) => f.DarkGrassArgb = c.ToArgb());
            AddColorButton(editor, "Infield", 5, f => f.InfieldArgb, (f, c) => f.InfieldArgb = c.ToArgb());
            AddColorButton(editor, "Clay", 6, f => f.ClayArgb, (f, c) => f.ClayArgb = c.ToArgb());
            AddColorButton(editor, "Wall", 7, f => f.WallArgb, (f, c) => f.WallArgb = c.ToArgb());
            AddColorButton(editor, "Seats", 8, f => f.SeatArgb, (f, c) => f.SeatArgb = c.ToArgb());
            AddColorButton(editor, "Accent", 9, f => f.AccentArgb, (f, c) => f.AccentArgb = c.ToArgb());

            var imageButtons = new FlowLayoutPanel { Dock = DockStyle.Fill };
            AddButton(imageButtons, "Background...", (s, e) => ChooseBackground());
            AddButton(imageButtons, "Clear Background", (s, e) =>
            {
                if (_selected == null) return;
                _selected.BackgroundAssetPath = "";
                MarkChanged();
            });
            editor.Controls.Add(imageButtons, 0, 10);
            editor.SetColumnSpan(imageButtons, 2);

            var bottom = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            AddButton(bottom, "Close", (s, e) => Close());
            AddButton(bottom, "Export...", (s, e) => ExportSelectedField());
            editor.Controls.Add(bottom, 0, 11);
            editor.SetColumnSpan(bottom, 2);

            var right = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3 };
            right.RowStyles.Add(new RowStyle(SizeType.Percent, 58));
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            right.RowStyles.Add(new RowStyle(SizeType.Percent, 42));
            root.Controls.Add(right, 2, 0);

            _preview.Dock = DockStyle.Fill;
            _preview.BackColor = Color.FromArgb(32, 92, 56);
            _preview.Paint += PaintPreview;
            right.Controls.Add(_preview, 0, 0);

            var overlayButtons = new FlowLayoutPanel { Dock = DockStyle.Fill };
            AddButton(overlayButtons, "Add Image...", (s, e) => AddImageOverlay());
            AddButton(overlayButtons, "Add Team Logo...", (s, e) => AddTeamLogoOverlay());
            AddButton(overlayButtons, "Remove Image", (s, e) => RemoveSelectedOverlay());
            right.Controls.Add(overlayButtons, 0, 1);

            _overlayGrid.Dock = DockStyle.Fill;
            _overlayGrid.AllowUserToAddRows = false;
            _overlayGrid.RowHeadersVisible = false;
            _overlayGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _overlayGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _overlayGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "name", HeaderText = "Name", FillWeight = 130 });
            _overlayGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "x", HeaderText = "X", FillWeight = 48 });
            _overlayGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "y", HeaderText = "Y", FillWeight = 48 });
            _overlayGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "w", HeaderText = "W", FillWeight = 48 });
            _overlayGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "h", HeaderText = "H", FillWeight = 48 });
            _overlayGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "opacity", HeaderText = "Opacity", FillWeight = 70 });
            _overlayGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "asset", HeaderText = "Asset", FillWeight = 180 });
            _overlayGrid.CellEndEdit += (s, e) => SaveOverlayRow(e.RowIndex);
            _overlayGrid.DataError += (s, e) => { e.ThrowException = false; };
            right.Controls.Add(_overlayGrid, 0, 2);
        }

        private static Button AddButton(Control host, string text, EventHandler click)
        {
            var button = new Button { Text = text, AutoSize = true, Margin = new Padding(4) };
            button.Click += click;
            host.Controls.Add(button);
            return button;
        }

        private static void AddLabeled(TableLayoutPanel panel, string label, Control control, int row)
        {
            panel.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, row);
            control.Dock = DockStyle.Fill;
            panel.Controls.Add(control, 1, row);
        }

        private void AddColorButton(
            TableLayoutPanel panel,
            string label,
            int row,
            Func<CustomBaseballField, int> getter,
            Action<CustomBaseballField, Color> setter)
        {
            panel.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, row);
            var button = new Button { Dock = DockStyle.Left, Width = 110, Text = "Pick..." };
            button.Click += (s, e) =>
            {
                if (_selected == null) return;
                using var dlg = new ColorDialog { Color = Color.FromArgb(getter(_selected)), FullOpen = true };
                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return;
                setter(_selected, dlg.Color);
                button.BackColor = dlg.Color;
                button.ForeColor = ReadableTextColor(dlg.Color);
                MarkChanged();
            };
            panel.Controls.Add(button, 1, row);
        }

        private void RefreshFieldList()
        {
            _fieldList.Items.Clear();
            foreach (var field in _league.CustomFields.OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase))
                _fieldList.Items.Add(field);
        }

        private void SelectField(CustomBaseballField? field)
        {
            _selected = field;
            _loading = true;
            _nameBox.Text = field?.Name ?? "";
            _teamLabelBox.Text = field?.TeamLabel ?? "";
            _yearBox.Value = Math.Clamp(field?.OpenedYear ?? DateTime.Now.Year, (int)_yearBox.Minimum, (int)_yearBox.Maximum);
            RefreshOverlayGrid();
            _loading = false;
            _preview.Invalidate();
        }

        private void NewField()
        {
            var field = new CustomBaseballField { Name = "Custom Field " + (_league.CustomFields.Count + 1) };
            _league.CustomFields.Add(field);
            Modified = true;
            RefreshFieldList();
            _fieldList.SelectedItem = field;
        }

        private void DuplicateField()
        {
            if (_selected == null)
            {
                NewField();
                return;
            }

            var copy = new CustomBaseballField
            {
                Id = "custom-" + Guid.NewGuid().ToString("N"),
                Name = _selected.Name + " Copy",
                TeamLabel = _selected.TeamLabel,
                OpenedYear = _selected.OpenedYear,
                GrassArgb = _selected.GrassArgb,
                DarkGrassArgb = _selected.DarkGrassArgb,
                InfieldArgb = _selected.InfieldArgb,
                ClayArgb = _selected.ClayArgb,
                WallArgb = _selected.WallArgb,
                SeatArgb = _selected.SeatArgb,
                StructureArgb = _selected.StructureArgb,
                AccentArgb = _selected.AccentArgb,
                BackgroundAssetPath = _selected.BackgroundAssetPath,
                Overlays = _selected.Overlays.Select(o => new FieldImageOverlay
                {
                    Name = o.Name,
                    AssetPath = o.AssetPath,
                    X = o.X,
                    Y = o.Y,
                    Width = o.Width,
                    Height = o.Height,
                    Opacity = o.Opacity
                }).ToList()
            };
            _league.CustomFields.Add(copy);
            Modified = true;
            RefreshFieldList();
            _fieldList.SelectedItem = copy;
        }

        private void DeleteField()
        {
            if (_selected == null)
                return;
            if (MessageBox.Show(this, "Delete " + _selected.Name + "?", "Delete field", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            _league.CustomFields.Remove(_selected);
            _selected = null;
            Modified = true;
            RefreshFieldList();
            if (_fieldList.Items.Count > 0)
                _fieldList.SelectedIndex = 0;
            else
                SelectField(null);
        }

        private void ExportSelectedField()
        {
            if (_selected == null)
            {
                MessageBox.Show(this, "Select a custom field to export.");
                return;
            }

            using var dlg = new SaveFileDialog
            {
                Title = "Export Custom Field",
                Filter = "Dan's RBI custom field (*.dbfield)|*.dbfield|Zip package (*.zip)|*.zip|All files (*.*)|*.*",
                FileName = SanitizeFileName(_selected.Name) + ".dbfield",
                DefaultExt = "dbfield",
                AddExtension = true
            };
            if (dlg.ShowDialog(this) != DialogResult.OK)
                return;

            try
            {
                ExportFieldPackage(_selected, dlg.FileName);
                MessageBox.Show(this, "Exported custom field to:\n\n" + dlg.FileName, "Field exported", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Custom field could not be exported: " + ex.Message, "Export failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private static void ExportFieldPackage(CustomBaseballField source, string packagePath)
        {
            if (File.Exists(packagePath))
                File.Delete(packagePath);

            var export = CloneFieldForExport(source);
            var usedAssetNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using var archive = ZipFile.Open(packagePath, ZipArchiveMode.Create);

            export.BackgroundAssetPath = AddAssetToArchive(archive, export.BackgroundAssetPath, usedAssetNames);
            foreach (var overlay in export.Overlays)
                overlay.AssetPath = AddAssetToArchive(archive, overlay.AssetPath, usedAssetNames);

            var jsonEntry = archive.CreateEntry("field.json", CompressionLevel.Optimal);
            using var stream = jsonEntry.Open();
            JsonSerializer.Serialize(stream, export, ExportJsonOptions);
        }

        private static CustomBaseballField CloneFieldForExport(CustomBaseballField source)
        {
            return new CustomBaseballField
            {
                Id = source.Id,
                Name = source.Name,
                TeamLabel = source.TeamLabel,
                OpenedYear = source.OpenedYear,
                GrassArgb = source.GrassArgb,
                DarkGrassArgb = source.DarkGrassArgb,
                InfieldArgb = source.InfieldArgb,
                ClayArgb = source.ClayArgb,
                WallArgb = source.WallArgb,
                SeatArgb = source.SeatArgb,
                StructureArgb = source.StructureArgb,
                AccentArgb = source.AccentArgb,
                BackgroundAssetPath = source.BackgroundAssetPath,
                Overlays = (source.Overlays ?? new List<FieldImageOverlay>())
                    .Select(o => new FieldImageOverlay
                    {
                        Name = o.Name,
                        AssetPath = o.AssetPath,
                        X = o.X,
                        Y = o.Y,
                        Width = o.Width,
                        Height = o.Height,
                        Opacity = o.Opacity
                    })
                    .ToList()
            };
        }

        private static string AddAssetToArchive(ZipArchive archive, string assetPath, HashSet<string> usedAssetNames)
        {
            string? path = ResolveAssetPath(assetPath);
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return "";

            string cleanName = SanitizeFileName(Path.GetFileNameWithoutExtension(path));
            string ext = Path.GetExtension(path);
            if (string.IsNullOrWhiteSpace(cleanName))
                cleanName = "field_asset";
            if (string.IsNullOrWhiteSpace(ext))
                ext = ".png";

            string assetName = cleanName + ext.ToLowerInvariant();
            int n = 2;
            while (!usedAssetNames.Add(assetName))
                assetName = cleanName + "_" + n++ + ext.ToLowerInvariant();

            string entryName = "assets/" + assetName;
            archive.CreateEntryFromFile(path, entryName, CompressionLevel.Optimal);
            return entryName;
        }

        private void SaveBasicFields()
        {
            if (_loading || _selected == null)
                return;
            _selected.Name = string.IsNullOrWhiteSpace(_nameBox.Text) ? "Custom Field" : _nameBox.Text.Trim();
            _selected.TeamLabel = string.IsNullOrWhiteSpace(_teamLabelBox.Text) ? "Custom Home Field" : _teamLabelBox.Text.Trim();
            _selected.OpenedYear = (int)_yearBox.Value;
            Modified = true;
            RefreshCurrentFieldLabel();
            _preview.Invalidate();
        }

        private void RefreshCurrentFieldLabel()
        {
            int index = _fieldList.SelectedIndex;
            if (index < 0) return;
            _fieldList.Items[index] = _selected ?? throw new InvalidOperationException("No field is selected.");
            _fieldList.SelectedIndex = index;
        }

        private void ChooseBackground()
        {
            if (!EnsureFieldSelected() || _selected is not CustomBaseballField selectedField)
                return;
            string? source = ChooseImage("Choose field background");
            if (string.IsNullOrWhiteSpace(source))
                return;
            string? asset = ImportAssetSafe(source);
            if (string.IsNullOrWhiteSpace(asset))
                return;
            selectedField.BackgroundAssetPath = asset;
            MarkChanged();
        }

        private void AddImageOverlay()
        {
            if (!EnsureFieldSelected() || _selected is not CustomBaseballField selectedField)
                return;
            string? source = ChooseImage("Choose image or logo to place on the field");
            if (string.IsNullOrWhiteSpace(source))
                return;
            string? asset = ImportAssetSafe(source);
            if (string.IsNullOrWhiteSpace(asset))
                return;
            selectedField.Overlays.Add(new FieldImageOverlay
            {
                Name = Path.GetFileNameWithoutExtension(source),
                AssetPath = asset,
                X = 0.5f,
                Y = 0.58f,
                Width = 0.18f,
                Height = 0.12f,
                Opacity = 230
            });
            MarkChanged();
        }

        private void AddTeamLogoOverlay()
        {
            if (!EnsureFieldSelected() || _selected is not CustomBaseballField selectedField)
                return;

            var choices = _teamLogoChoices?.Invoke() ?? new List<TeamLogoChoice>();
            if (choices.Count == 0)
            {
                MessageBox.Show(this, "No team logos are available yet.");
                return;
            }

            using var form = new Form
            {
                Text = "Add Team Logo",
                StartPosition = FormStartPosition.CenterParent,
                Size = new Size(420, 150),
                MinimizeBox = false,
                MaximizeBox = false
            };
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, Padding = new Padding(12) };
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            var combo = new ComboBox { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList };
            foreach (var choice in choices)
                combo.Items.Add(choice);
            combo.SelectedIndex = 0;
            root.Controls.Add(combo, 0, 0);
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            AddButton(buttons, "Cancel", (s, e) => form.DialogResult = DialogResult.Cancel);
            AddButton(buttons, "Add", (s, e) => form.DialogResult = DialogResult.OK);
            root.Controls.Add(buttons, 0, 1);
            form.Controls.Add(root);

            if (form.ShowDialog(this) != DialogResult.OK || combo.SelectedItem is not TeamLogoChoice selected)
                return;

            string? asset = ImportAssetSafe(selected.LogoPath);
            if (string.IsNullOrWhiteSpace(asset))
                return;
            selectedField.Overlays.Add(new FieldImageOverlay
            {
                Name = selected.Team.DisplayName + " Logo",
                AssetPath = asset,
                X = 0.5f,
                Y = 0.74f,
                Width = 0.2f,
                Height = 0.12f,
                Opacity = 235
            });
            MarkChanged();
        }

        private void RemoveSelectedOverlay()
        {
            if (_selected == null || _overlayGrid.CurrentRow?.Tag is not FieldImageOverlay overlay)
                return;
            _selected.Overlays.Remove(overlay);
            MarkChanged();
        }

        private string? ChooseImage(string title)
        {
            using var dlg = new OpenFileDialog
            {
                Title = title,
                Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.dib|All files (*.*)|*.*",
                InitialDirectory = _initialDirectory
            };
            return dlg.ShowDialog(this) == DialogResult.OK ? dlg.FileName : null;
        }

        private string? ImportAssetSafe(string sourcePath)
        {
            if (_selected is not CustomBaseballField selectedField)
                return null;
            try
            {
                return _importAsset(sourcePath, selectedField.Id);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Image could not be imported: " + ex.Message, "Field image", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return null;
            }
        }

        private bool EnsureFieldSelected()
        {
            if (_selected != null)
                return true;
            NewField();
            return _selected != null;
        }

        private void RefreshOverlayGrid()
        {
            _overlayGrid.Rows.Clear();
            if (_selected?.Overlays == null)
                return;

            foreach (var overlay in _selected.Overlays)
            {
                int row = _overlayGrid.Rows.Add(
                    overlay.Name,
                    overlay.X.ToString("0.###"),
                    overlay.Y.ToString("0.###"),
                    overlay.Width.ToString("0.###"),
                    overlay.Height.ToString("0.###"),
                    overlay.Opacity,
                    overlay.AssetPath);
                _overlayGrid.Rows[row].Tag = overlay;
            }
        }

        private void SaveOverlayRow(int rowIndex)
        {
            if (_loading || rowIndex < 0 || rowIndex >= _overlayGrid.Rows.Count)
                return;
            if (_overlayGrid.Rows[rowIndex].Tag is not FieldImageOverlay overlay)
                return;

            var row = _overlayGrid.Rows[rowIndex];
            overlay.Name = Convert.ToString(row.Cells["name"].Value)?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(overlay.Name))
                overlay.Name = "Image";
            overlay.X = ReadFloat(row.Cells["x"].Value, overlay.X, 0f, 1f);
            overlay.Y = ReadFloat(row.Cells["y"].Value, overlay.Y, 0f, 1f);
            overlay.Width = ReadFloat(row.Cells["w"].Value, overlay.Width, 0.02f, 1f);
            overlay.Height = ReadFloat(row.Cells["h"].Value, overlay.Height, 0.02f, 1f);
            overlay.Opacity = ReadInt(row.Cells["opacity"].Value, overlay.Opacity, 0, 255);
            overlay.AssetPath = Convert.ToString(row.Cells["asset"].Value)?.Trim() ?? "";
            MarkChanged();
        }

        private static float ReadFloat(object value, float fallback, float min, float max)
            => float.TryParse(Convert.ToString(value), out float parsed) ? Math.Clamp(parsed, min, max) : fallback;

        private static int ReadInt(object value, int fallback, int min, int max)
            => int.TryParse(Convert.ToString(value), out int parsed) ? Math.Clamp(parsed, min, max) : fallback;

        private void MarkChanged()
        {
            Modified = true;
            RefreshOverlayGrid();
            _preview.Invalidate();
        }

        private void PaintPreview(object? sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var bounds = _preview.ClientRectangle;
            if (bounds.Width <= 0 || bounds.Height <= 0)
                return;

            var preset = BaseballFieldPresets.FromCustom(_selected);
            string? background = ResolveAssetPath(preset.BackgroundAssetPath);
            if (!string.IsNullOrWhiteSpace(background) && File.Exists(background))
            {
                try
                {
                    using var image = Image.FromFile(background);
                    g.DrawImage(image, CoverImage(image.Size, bounds));
                    using var shade = new SolidBrush(Color.FromArgb(35, Color.Black));
                    g.FillRectangle(shade, bounds);
                }
                catch
                {
                    DrawGeneratedPreview(g, bounds, preset);
                }
            }
            else
            {
                DrawGeneratedPreview(g, bounds, preset);
            }

            DrawPreviewOverlays(g, bounds, preset);
            DrawPreviewLabel(g, bounds, preset);
        }

        private static void DrawGeneratedPreview(Graphics g, Rectangle r, BaseballFieldPreset preset)
        {
            g.Clear(preset.DarkGrassColor);
            Point c = new Point(r.Width / 2, r.Height / 2 + 58);
            using var wall = new Pen(preset.WallColor, 8);
            g.DrawArc(wall, c.X - 240, c.Y - 300, 480, 380, 25, 130);
            using var seats = new SolidBrush(Color.FromArgb(150, preset.SeatColor));
            g.FillRectangle(seats, c.X - 190, c.Y - 270, 380, 34);
            using var dirt = new SolidBrush(preset.InfieldColor);
            var home = new Point(c.X, c.Y + 120);
            var first = new Point(c.X + 130, c.Y);
            var second = new Point(c.X, c.Y - 120);
            var third = new Point(c.X - 130, c.Y);
            g.FillPolygon(dirt, new[] { home, first, second, third });
            using var grass = new SolidBrush(preset.GrassColor);
            g.FillEllipse(grass, c.X - 80, c.Y - 80, 160, 160);
            using var white = new SolidBrush(Color.White);
            foreach (var p in new[] { home, first, second, third })
                g.FillRectangle(white, p.X - 7, p.Y - 7, 14, 14);
        }

        private static void DrawPreviewOverlays(Graphics g, Rectangle bounds, BaseballFieldPreset preset)
        {
            if (preset?.Overlays == null)
                return;

            foreach (var overlay in preset.Overlays)
            {
                string? path = ResolveAssetPath(overlay.AssetPath);
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                    continue;

                try
                {
                    using var image = Image.FromFile(path);
                    float width = Math.Clamp(overlay.Width, 0.02f, 1f) * bounds.Width;
                    float height = Math.Clamp(overlay.Height, 0.02f, 1f) * bounds.Height;
                    float x = bounds.Left + Math.Clamp(overlay.X, 0f, 1f) * bounds.Width - width / 2f;
                    float y = bounds.Top + Math.Clamp(overlay.Y, 0f, 1f) * bounds.Height - height / 2f;
                    g.DrawImage(image, new RectangleF(x, y, width, height));
                }
                catch { }
            }
        }

        private void DrawPreviewLabel(Graphics g, Rectangle bounds, BaseballFieldPreset preset)
        {
            using var font = new Font(Font.FontFamily, 10, FontStyle.Bold);
            using var bg = new SolidBrush(Color.FromArgb(175, 18, 26, 24));
            var label = new Rectangle(14, bounds.Bottom - 40, Math.Min(bounds.Width - 28, 620), 28);
            g.FillRectangle(bg, label);
            TextRenderer.DrawText(g, preset.Name + " (" + preset.OpenedYear + ") - " + preset.TeamLabel,
                font, label, Color.White, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        private static string? ResolveAssetPath(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
                return null;
            return AssetPathResolver.ResolvePath(assetPath);
        }

        private static string SanitizeFileName(string value)
        {
            value = string.IsNullOrWhiteSpace(value) ? "custom-field" : value.Trim();
            foreach (char c in Path.GetInvalidFileNameChars())
                value = value.Replace(c, '_');
            return value;
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

        private static Color ReadableTextColor(Color background)
        {
            int brightness = (background.R * 299 + background.G * 587 + background.B * 114) / 1000;
            return brightness >= 145 ? Color.FromArgb(20, 24, 32) : Color.White;
        }
    }
}
