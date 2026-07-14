using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace StandaloneBaseball
{
    public sealed class TeamUniformEditorDialog : Form
    {
        private readonly BindingList<TeamUniformSet> _uniforms;
        private readonly Team _team;
        private readonly string _assetDirectory;
        private readonly string _initialDirectory;
        private readonly DataGridView _grid = new DataGridView();

        public IReadOnlyList<TeamUniformSet> Uniforms => _uniforms.Select(CloneUniform).ToList();

        public TeamUniformEditorDialog(Team team, string assetDirectory, string initialDirectory)
        {
            _team = team ?? throw new ArgumentNullException(nameof(team));
            _assetDirectory = assetDirectory ?? "";
            _initialDirectory = initialDirectory ?? "";
            _team.EnsureDefaultUniformSets();
            _uniforms = new BindingList<TeamUniformSet>((team.UniformSets ?? new List<TeamUniformSet>()).Select(CloneUniform).ToList());
            NormalizeActiveUniforms();

            Text = _team.DisplayName + " Uniform Library";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(920, 520);
            MinimumSize = new Size(780, 420);

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, Padding = new Padding(10) };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
            Controls.Add(root);

            var bar = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
            AddButton(bar, "+ Add Uniform", (s, e) => AddUniform());
            AddButton(bar, "Edit Selected...", (s, e) => EditSelectedUniform());
            AddButton(bar, "Set Active", (s, e) => SetSelectedActive());
            AddButton(bar, "Delete", (s, e) => DeleteSelectedUniform());
            root.Controls.Add(bar, 0, 0);

            _grid.Dock = DockStyle.Fill;
            _grid.AutoGenerateColumns = false;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.MultiSelect = false;
            _grid.RowHeadersVisible = false;
            _grid.ReadOnly = true;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            _grid.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = nameof(TeamUniformSet.Active), HeaderText = "Active", Width = 58 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Category", Width = 135 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(TeamUniformSet.Name), HeaderText = "Name", Width = 180 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Jersey", Width = 84 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Pants", Width = 84 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Cap/Helmet", Width = 94 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Image", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            _grid.DataSource = _uniforms;
            _grid.CellFormatting += FormatCell;
            _grid.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex >= 0)
                    EditSelectedUniform();
            };
            root.Controls.Add(_grid, 0, 1);

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
            var save = new Button { Text = "Save", AutoSize = true, DialogResult = DialogResult.OK };
            save.Click += (s, e) =>
            {
                NormalizeActiveUniforms();
                DialogResult = DialogResult.OK;
                Close();
            };
            var cancel = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
            buttons.Controls.Add(save);
            buttons.Controls.Add(cancel);
            root.Controls.Add(buttons, 0, 2);

            AcceptButton = save;
            CancelButton = cancel;
        }

        private void AddUniform()
        {
            var category = SelectedUniform()?.Category ?? TeamUniformCategory.Home;
            var uniform = new TeamUniformSet
            {
                Category = category,
                Name = TeamUniformSet.CategoryLabel(category) + " " + (_uniforms.Count(u => u.Category == category) + 1),
                JerseyArgb = _team.PrimaryArgb,
                PantsArgb = category == TeamUniformCategory.Home || category == TeamUniformCategory.HomeAlternate
                    ? Color.White.ToArgb()
                    : Color.LightGray.ToArgb(),
                CapHelmetArgb = _team.SecondaryArgb
            };
            using var dlg = new UniformSetEditDialog(_team, uniform, _assetDirectory, _initialDirectory);
            if (dlg.ShowDialog(this) != DialogResult.OK)
                return;

            _uniforms.Add(CloneUniform(dlg.Uniform));
            EnsureOneActive(dlg.Uniform.Category);
            _grid.Refresh();
        }

        private void EditSelectedUniform()
        {
            var uniform = SelectedUniform();
            if (uniform == null)
                return;

            TeamUniformCategory oldCategory = uniform.Category;
            using var dlg = new UniformSetEditDialog(_team, CloneUniform(uniform), _assetDirectory, _initialDirectory);
            if (dlg.ShowDialog(this) != DialogResult.OK)
                return;

            CopyUniform(dlg.Uniform, uniform);
            EnsureOneActive(oldCategory);
            EnsureOneActive(uniform.Category);
            _grid.Refresh();
        }

        private void SetSelectedActive()
        {
            var uniform = SelectedUniform();
            if (uniform == null)
                return;

            foreach (var item in _uniforms.Where(u => u.Category == uniform.Category))
                item.Active = item.Id == uniform.Id;
            _grid.Refresh();
        }

        private void DeleteSelectedUniform()
        {
            var uniform = SelectedUniform();
            if (uniform == null)
                return;
            if (_uniforms.Count(u => u.Category == uniform.Category) <= 1)
            {
                MessageBox.Show(this, "Each category must keep at least one saved uniform.", "Delete Uniform", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (MessageBox.Show(this, "Delete " + uniform.Name + "?", "Delete Uniform", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK)
                return;

            TeamUniformCategory category = uniform.Category;
            _uniforms.Remove(uniform);
            EnsureOneActive(category);
            _grid.Refresh();
        }

        private TeamUniformSet SelectedUniform()
            => _grid.CurrentRow?.DataBoundItem as TeamUniformSet;

        private void NormalizeActiveUniforms()
        {
            foreach (TeamUniformCategory category in Enum.GetValues(typeof(TeamUniformCategory)))
                EnsureOneActive(category);
        }

        private void EnsureOneActive(TeamUniformCategory category)
        {
            var items = _uniforms.Where(u => u.Category == category).ToList();
            if (items.Count == 0)
                return;

            var active = items.FirstOrDefault(u => u.Active) ?? items[0];
            foreach (var item in items)
                item.Active = item.Id == active.Id;
        }

        private void FormatCell(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _uniforms.Count)
                return;

            var uniform = _uniforms[e.RowIndex];
            switch (e.ColumnIndex)
            {
                case 1:
                    e.Value = TeamUniformSet.CategoryLabel(uniform.Category);
                    e.FormattingApplied = true;
                    break;
                case 3:
                    e.Value = ToHex(Color.FromArgb(uniform.JerseyArgb));
                    e.FormattingApplied = true;
                    break;
                case 4:
                    e.Value = ToHex(Color.FromArgb(uniform.PantsArgb));
                    e.FormattingApplied = true;
                    break;
                case 5:
                    e.Value = ToHex(Color.FromArgb(uniform.CapHelmetArgb));
                    e.FormattingApplied = true;
                    break;
                case 6:
                    e.Value = string.IsNullOrWhiteSpace(uniform.ImagePath) ? "" : uniform.ImagePath;
                    e.FormattingApplied = true;
                    break;
            }
        }

        private static void AddButton(Control parent, string text, EventHandler handler)
        {
            var button = new Button { Text = text, AutoSize = true, Margin = new Padding(0, 6, 6, 0) };
            button.Click += handler;
            parent.Controls.Add(button);
        }

        private static TeamUniformSet CloneUniform(TeamUniformSet source)
        {
            if (source == null)
                return new TeamUniformSet();

            return new TeamUniformSet
            {
                Id = source.Id == Guid.Empty ? Guid.NewGuid() : source.Id,
                Category = source.Category,
                Name = source.Name,
                JerseyArgb = source.JerseyArgb,
                PantsArgb = source.PantsArgb,
                CapHelmetArgb = source.CapHelmetArgb,
                ImagePath = source.ImagePath ?? "",
                Active = source.Active
            };
        }

        private static void CopyUniform(TeamUniformSet source, TeamUniformSet target)
        {
            target.Id = source.Id;
            target.Category = source.Category;
            target.Name = source.Name;
            target.JerseyArgb = source.JerseyArgb;
            target.PantsArgb = source.PantsArgb;
            target.CapHelmetArgb = source.CapHelmetArgb;
            target.ImagePath = source.ImagePath ?? "";
            target.Active = source.Active;
        }

        private static string ToHex(Color color)
            => "#" + color.R.ToString("X2") + color.G.ToString("X2") + color.B.ToString("X2");

        private sealed class UniformSetEditDialog : Form
        {
            private readonly Team _team;
            private readonly string _assetDirectory;
            private readonly string _initialDirectory;
            private readonly TextBox _nameBox = new TextBox();
            private readonly ComboBox _categoryBox = new ComboBox();
            private readonly Label _imageLabel = new Label();
            private readonly Panel _jerseyPanel = new Panel();
            private readonly Panel _pantsPanel = new Panel();
            private readonly Panel _capPanel = new Panel();
            private TeamUniformSet _uniform;

            public TeamUniformSet Uniform => CloneUniform(_uniform);

            public UniformSetEditDialog(Team team, TeamUniformSet uniform, string assetDirectory, string initialDirectory)
            {
                _team = team;
                _assetDirectory = assetDirectory ?? "";
                _initialDirectory = initialDirectory ?? "";
                _uniform = CloneUniform(uniform);
                _uniform.Normalize(team);

                Text = "Uniform Editor";
                StartPosition = FormStartPosition.CenterParent;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MaximizeBox = false;
                MinimizeBox = false;
                ClientSize = new Size(500, 300);

                var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 7, ColumnCount = 2, Padding = new Padding(12) };
                root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
                root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                for (int i = 0; i < 6; i++)
                    root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
                root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
                Controls.Add(root);

                root.Controls.Add(new Label { Text = "Name", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 0);
                _nameBox.Dock = DockStyle.Fill;
                _nameBox.Text = _uniform.Name;
                root.Controls.Add(_nameBox, 1, 0);

                root.Controls.Add(new Label { Text = "Category", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 1);
                _categoryBox.Dock = DockStyle.Fill;
                _categoryBox.DropDownStyle = ComboBoxStyle.DropDownList;
                foreach (TeamUniformCategory category in Enum.GetValues(typeof(TeamUniformCategory)))
                    _categoryBox.Items.Add(category);
                _categoryBox.Format += (s, e) =>
                {
                    if (e.ListItem is TeamUniformCategory category)
                        e.Value = TeamUniformSet.CategoryLabel(category);
                };
                _categoryBox.SelectedItem = _uniform.Category;
                root.Controls.Add(_categoryBox, 1, 1);

                AddColorRow(root, 2, "Jersey", _jerseyPanel, () => _uniform.JerseyArgb, c => _uniform.JerseyArgb = c.ToArgb());
                AddColorRow(root, 3, "Pants", _pantsPanel, () => _uniform.PantsArgb, c => _uniform.PantsArgb = c.ToArgb());
                AddColorRow(root, 4, "Cap/Helmet", _capPanel, () => _uniform.CapHelmetArgb, c => _uniform.CapHelmetArgb = c.ToArgb());

                root.Controls.Add(new Label { Text = "Image", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 5);
                var imagePanel = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
                AddButton(imagePanel, "Choose...", (s, e) => ChooseImage());
                AddButton(imagePanel, "Clear", (s, e) =>
                {
                    _uniform.ImagePath = "";
                    RefreshImageLabel();
                });
                _imageLabel.Width = 210;
                _imageLabel.Height = 28;
                _imageLabel.TextAlign = ContentAlignment.MiddleLeft;
                _imageLabel.AutoEllipsis = true;
                imagePanel.Controls.Add(_imageLabel);
                root.Controls.Add(imagePanel, 1, 5);

                var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
                var ok = new Button { Text = "Save Uniform", AutoSize = true, DialogResult = DialogResult.OK };
                ok.Click += (s, e) =>
                {
                    _uniform.Name = string.IsNullOrWhiteSpace(_nameBox.Text) ? TeamUniformSet.CategoryLabel(_uniform.Category) : _nameBox.Text.Trim();
                    if (_categoryBox.SelectedItem is TeamUniformCategory selected)
                        _uniform.Category = selected;
                    _uniform.Normalize(_team);
                    DialogResult = DialogResult.OK;
                    Close();
                };
                var cancel = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
                buttons.Controls.Add(ok);
                buttons.Controls.Add(cancel);
                root.Controls.Add(buttons, 0, 6);
                root.SetColumnSpan(buttons, 2);

                RefreshColorPanels();
                RefreshImageLabel();
                AcceptButton = ok;
                CancelButton = cancel;
            }

            private void AddColorRow(TableLayoutPanel root, int row, string label, Panel swatch, Func<int> get, Action<Color> set)
            {
                root.Controls.Add(new Label { Text = label, Anchor = AnchorStyles.Left, AutoSize = true }, 0, row);
                var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
                swatch.Width = 42;
                swatch.Height = 24;
                swatch.BorderStyle = BorderStyle.FixedSingle;
                panel.Controls.Add(swatch);
                AddButton(panel, "Pick...", (s, e) =>
                {
                    using var colorDialog = new ColorDialog { Color = Color.FromArgb(get()), FullOpen = true };
                    if (colorDialog.ShowDialog(this) != DialogResult.OK)
                        return;
                    set(colorDialog.Color);
                    RefreshColorPanels();
                });
                root.Controls.Add(panel, 1, row);
            }

            private void ChooseImage()
            {
                using var dlg = new OpenFileDialog
                {
                    Title = "Choose uniform image",
                    Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All files (*.*)|*.*",
                    InitialDirectory = Directory.Exists(_initialDirectory) ? _initialDirectory : Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
                };
                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return;

                if (!Directory.Exists(_assetDirectory))
                    Directory.CreateDirectory(_assetDirectory);

                string categoryFolder = TeamUniformSet.CategoryLabel(_uniform.Category).ToUpperInvariant();
                foreach (char c in Path.GetInvalidFileNameChars())
                    categoryFolder = categoryFolder.Replace(c, '_');
                string targetDir = Path.Combine(_assetDirectory, categoryFolder, _uniform.Id.ToString("N"));
                Directory.CreateDirectory(targetDir);

                string ext = Path.GetExtension(dlg.FileName);
                if (string.IsNullOrWhiteSpace(ext))
                    ext = ".png";
                string dest = Path.Combine(targetDir, "uniform" + ext.ToLowerInvariant());
                File.Copy(dlg.FileName, dest, overwrite: true);
                _uniform.ImagePath = AssetPathResolver.ToPortablePath(dest);
                RefreshImageLabel();
            }

            private void RefreshColorPanels()
            {
                _jerseyPanel.BackColor = Color.FromArgb(_uniform.JerseyArgb);
                _pantsPanel.BackColor = Color.FromArgb(_uniform.PantsArgb);
                _capPanel.BackColor = Color.FromArgb(_uniform.CapHelmetArgb);
            }

            private void RefreshImageLabel()
            {
                _imageLabel.Text = string.IsNullOrWhiteSpace(_uniform.ImagePath) ? "No image" : Path.GetFileName(AssetPathResolver.ResolvePath(_uniform.ImagePath));
            }
        }
    }
}
