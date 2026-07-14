#nullable enable annotations

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace StandaloneBaseball
{
    public sealed class CutsceneEditorDialog : Form
    {
        private readonly BindingList<CutsceneDefinition> _cutscenes;
        private readonly string _assetDirectory;
        private readonly string _initialDirectory;
        private readonly List<CutsceneTrigger> _allowedTriggers;
        private readonly List<TeamCutsceneUniformFolder> _uniformFolders;
        private readonly DataGridView _grid = new DataGridView();

        public IReadOnlyList<CutsceneDefinition> Cutscenes => _cutscenes.ToList();

        public CutsceneEditorDialog(
            IEnumerable<CutsceneDefinition> cutscenes,
            string assetDirectory,
            string initialDirectory,
            string title = "Cutscenes",
            IEnumerable<CutsceneTrigger>? allowedTriggers = null,
            IEnumerable<TeamCutsceneUniformFolder>? uniformFolders = null)
        {
            _assetDirectory = assetDirectory;
            _initialDirectory = Directory.Exists(initialDirectory) ? initialDirectory : Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            _allowedTriggers = (allowedTriggers ?? Enum.GetValues(typeof(CutsceneTrigger)).Cast<CutsceneTrigger>()).Distinct().ToList();
            if (_allowedTriggers.Count == 0)
                _allowedTriggers.Add(CutsceneTrigger.GameStart);
            _uniformFolders = (uniformFolders ?? new[] { TeamCutsceneUniformFolder.Any }).Distinct().ToList();
            if (_uniformFolders.Count == 0)
                _uniformFolders.Add(TeamCutsceneUniformFolder.Any);
            if (!_uniformFolders.Contains(TeamCutsceneUniformFolder.Any))
                _uniformFolders.Add(TeamCutsceneUniformFolder.Any);
            _cutscenes = new BindingList<CutsceneDefinition>((cutscenes ?? Enumerable.Empty<CutsceneDefinition>())
                .Where(c => c != null && _allowedTriggers.Contains(c.Trigger))
                .Select(Clone)
                .ToList());

            Text = title;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimizeBox = false;
            MaximizeBox = true;
            ClientSize = new Size(940, 520);

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                ColumnCount = 1,
                Padding = new Padding(10)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            Controls.Add(root);

            root.Controls.Add(new Label
            {
                Text = "Assign image or video cutscenes to game trigger events.",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font(Font.FontFamily, 10.5f, FontStyle.Bold)
            }, 0, 0);

            BuildGrid();
            root.Controls.Add(_grid, 0, 1);

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            var cancel = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
            var save = new Button { Text = "Save", AutoSize = true, DialogResult = DialogResult.OK };
            var preview = new Button { Text = "Preview", AutoSize = true };
            preview.Click += (s, e) => PreviewSelected();
            var remove = new Button { Text = "Remove", AutoSize = true };
            remove.Click += (s, e) => RemoveSelected();
            var add = new Button { Text = "Add Media...", AutoSize = true };
            add.Click += (s, e) => AddMedia();
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(save);
            buttons.Controls.Add(preview);
            buttons.Controls.Add(remove);
            buttons.Controls.Add(add);
            root.Controls.Add(buttons, 0, 2);

            AcceptButton = save;
            CancelButton = cancel;
        }

        private void BuildGrid()
        {
            _grid.Dock = DockStyle.Fill;
            _grid.AutoGenerateColumns = false;
            _grid.AllowUserToAddRows = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.MultiSelect = false;
            _grid.DataSource = _cutscenes;

            _grid.Columns.Add(new DataGridViewCheckBoxColumn
            {
                HeaderText = "Enabled",
                DataPropertyName = nameof(CutsceneDefinition.Enabled),
                Width = 70
            });
            _grid.Columns.Add(new DataGridViewComboBoxColumn
            {
                HeaderText = "Trigger",
                DataPropertyName = nameof(CutsceneDefinition.Trigger),
                DataSource = _allowedTriggers,
                Width = 170
            });
            if (_uniformFolders.Count > 1 || !_uniformFolders.Contains(TeamCutsceneUniformFolder.Any))
            {
                _grid.Columns.Add(new DataGridViewComboBoxColumn
                {
                    HeaderText = "Uniform",
                    DataPropertyName = nameof(CutsceneDefinition.UniformFolder),
                    DataSource = _uniformFolders,
                    Width = 130
                });
            }
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Name",
                DataPropertyName = nameof(CutsceneDefinition.Name),
                Width = 180
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Media Path",
                DataPropertyName = nameof(CutsceneDefinition.MediaPath),
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Seconds",
                DataPropertyName = nameof(CutsceneDefinition.DurationSeconds),
                Width = 70
            });
        }

        private void AddMedia()
        {
            using var dlg = new OpenFileDialog
            {
                Title = "Choose cutscene image or video",
                Filter = "Cutscene media|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.mp4;*.mov;*.m4v;*.avi;*.wmv;*.webm;*.mkv|All files (*.*)|*.*",
                InitialDirectory = _initialDirectory
            };
            if (dlg.ShowDialog(this) != DialogResult.OK)
                return;
            if (!CutscenePlaybackForm.IsSupportedMedia(dlg.FileName))
            {
                MessageBox.Show(this, "Choose an image or video file.", "Unsupported media", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            TeamCutsceneUniformFolder folder = ChooseUniformFolder();
            string targetDirectory = CutsceneUniformFolderPath(_assetDirectory, folder);
            Directory.CreateDirectory(targetDirectory);
            string destination = UniquePath(Path.Combine(targetDirectory, Path.GetFileName(dlg.FileName)));
            File.Copy(dlg.FileName, destination);
            string mediaPath = AssetPathResolver.ToPortablePath(destination);
            var cutscene = new CutsceneDefinition
            {
                Name = Path.GetFileNameWithoutExtension(destination),
                Trigger = _allowedTriggers.Contains(CutsceneTrigger.GameStart) ? CutsceneTrigger.GameStart : _allowedTriggers[0],
                UniformFolder = folder,
                MediaPath = mediaPath,
                DurationSeconds = CutscenePlaybackForm.IsImage(destination) ? 5 : 30,
                Enabled = true
            };
            _cutscenes.Add(cutscene);
            _grid.ClearSelection();
            int row = _cutscenes.Count - 1;
            if (row >= 0)
                _grid.Rows[row].Selected = true;
        }

        private void RemoveSelected()
        {
            if (_grid.CurrentRow?.DataBoundItem is not CutsceneDefinition selected)
                return;
            if (MessageBox.Show(this, "Remove " + selected.Name + "?", "Remove cutscene", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            _cutscenes.Remove(selected);
        }

        private void PreviewSelected()
        {
            _grid.EndEdit();
            if (_grid.CurrentRow?.DataBoundItem is not CutsceneDefinition selected)
                return;
            if (string.IsNullOrWhiteSpace(selected.MediaPath) || !File.Exists(CutscenePlaybackForm.ResolveMediaPath(selected.MediaPath)))
            {
                MessageBox.Show(this, "The selected cutscene media file was not found.", "Missing media", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            CutscenePlaybackForm.PlayFirst(this, new[] { selected }, selected.Trigger);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _grid.EndEdit();
            if (DialogResult == DialogResult.OK)
            {
                foreach (var cutscene in _cutscenes)
                {
                    if (!_allowedTriggers.Contains(cutscene.Trigger))
                        cutscene.Trigger = _allowedTriggers[0];
                    Normalize(cutscene);
                }
            }
            base.OnFormClosing(e);
        }

        private static CutsceneDefinition Clone(CutsceneDefinition? source)
            => new CutsceneDefinition
            {
                Id = source?.Id == Guid.Empty ? Guid.NewGuid() : source?.Id ?? Guid.NewGuid(),
                Name = source?.Name ?? "Cutscene",
                Trigger = source?.Trigger ?? CutsceneTrigger.GameStart,
                UniformFolder = source?.UniformFolder ?? TeamCutsceneUniformFolder.Any,
                MediaPath = source?.MediaPath ?? "",
                Enabled = source?.Enabled ?? true,
                DurationSeconds = source == null || source.DurationSeconds <= 0 ? 5 : source.DurationSeconds
            };

        private static void Normalize(CutsceneDefinition cutscene)
        {
            if (cutscene.Id == Guid.Empty)
                cutscene.Id = Guid.NewGuid();
            if (string.IsNullOrWhiteSpace(cutscene.Name))
                cutscene.Name = cutscene.Trigger.ToString();
            if (!Enum.IsDefined(typeof(TeamCutsceneUniformFolder), cutscene.UniformFolder))
                cutscene.UniformFolder = TeamCutsceneUniformFolder.Any;
            cutscene.MediaPath ??= "";
            cutscene.DurationSeconds = Math.Clamp(cutscene.DurationSeconds <= 0 ? 5 : cutscene.DurationSeconds, 1, 120);
        }

        private TeamCutsceneUniformFolder ChooseUniformFolder()
        {
            if (_uniformFolders.Count <= 1)
                return _uniformFolders[0];

            using var form = new Form
            {
                Text = "Cutscene Uniform Folder",
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MinimizeBox = false,
                MaximizeBox = false,
                ClientSize = new Size(360, 130)
            };
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, Padding = new Padding(12) };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            form.Controls.Add(root);
            root.Controls.Add(new Label { Text = "Save this cutscene media under:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
            var combo = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            combo.Items.AddRange(_uniformFolders.Cast<object>().ToArray());
            combo.SelectedItem = _uniformFolders.Contains(TeamCutsceneUniformFolder.Home) ? TeamCutsceneUniformFolder.Home : _uniformFolders[0];
            root.Controls.Add(combo, 0, 1);
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            var ok = new Button { Text = "OK", AutoSize = true, DialogResult = DialogResult.OK };
            var cancel = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(ok);
            root.Controls.Add(buttons, 0, 2);
            form.AcceptButton = ok;
            form.CancelButton = cancel;
            return form.ShowDialog(this) == DialogResult.OK && combo.SelectedItem is TeamCutsceneUniformFolder selected
                ? selected
                : _uniformFolders[0];
        }

        private static string CutsceneUniformFolderPath(string root, TeamCutsceneUniformFolder folder)
        {
            return folder switch
            {
                TeamCutsceneUniformFolder.Home => Path.Combine(root, "HOME"),
                TeamCutsceneUniformFolder.HomeAlternate => Path.Combine(root, "HOME ALTERNATE"),
                TeamCutsceneUniformFolder.Visitor => Path.Combine(root, "VISITOR"),
                TeamCutsceneUniformFolder.VisitorAlternate => Path.Combine(root, "VISITOR ALTERNATE"),
                _ => root
            };
        }

        private static string UniquePath(string path)
        {
            if (!File.Exists(path))
                return path;
            string dir = Path.GetDirectoryName(path) ?? "";
            string name = Path.GetFileNameWithoutExtension(path);
            string ext = Path.GetExtension(path);
            for (int i = 1; i < 10000; i++)
            {
                string candidate = Path.Combine(dir, name + "_" + i + ext);
                if (!File.Exists(candidate))
                    return candidate;
            }
            return Path.Combine(dir, name + "_" + Guid.NewGuid().ToString("N") + ext);
        }

        private static bool PathStartsInside(string path, string root)
        {
            try
            {
                string fullPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                return fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }
    }
}
