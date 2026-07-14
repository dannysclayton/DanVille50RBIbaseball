using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

#nullable enable annotations

namespace StandaloneBaseball
{
    internal sealed class ReplayLibraryDialog : Form
    {
        private readonly string _folder;
        private readonly DataGridView _grid = new DataGridView();
        private readonly Button _watchButton = new Button();
        private readonly Button _removeButton = new Button();
        private readonly Label _status = new Label();

        public string SelectedReplayPath { get; private set; } = "";

        public ReplayLibraryDialog(string folder)
        {
            _folder = ReplayStore.EnsureReplayFolder(folder);
            Text = "Replay Library";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(940, 540);
            MinimumSize = new Size(760, 430);

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(12)
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            root.Controls.Add(new Label
            {
                AutoSize = true,
                MaximumSize = new Size(900, 0),
                Text = "Import replay JSON files into this game's managed library, then select a replay to watch."
            }, 0, 0);

            ConfigureGrid();
            root.Controls.Add(_grid, 0, 1);

            _status.AutoSize = true;
            _status.Padding = new Padding(0, 7, 0, 4);
            root.Controls.Add(_status, 0, 2);

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Padding = new Padding(0, 6, 0, 0)
            };
            var close = new Button { Text = "Close", AutoSize = true, DialogResult = DialogResult.Cancel };
            _watchButton.Text = "Watch";
            _watchButton.AutoSize = true;
            _watchButton.Click += (sender, args) => AcceptSelection();
            _removeButton.Text = "Remove";
            _removeButton.AutoSize = true;
            _removeButton.Click += (sender, args) => RemoveSelected();
            var openFolder = new Button { Text = "Open Folder", AutoSize = true };
            openFolder.Click += (sender, args) => OpenLibraryFolder();
            var import = new Button { Text = "Import Replay...", AutoSize = true };
            import.Click += (sender, args) => ImportReplays();
            var saveTemplate = new Button { Text = "Save Template...", AutoSize = true };
            saveTemplate.Click += (sender, args) => SaveTemplate();
            buttons.Controls.Add(close);
            buttons.Controls.Add(_watchButton);
            buttons.Controls.Add(_removeButton);
            buttons.Controls.Add(openFolder);
            buttons.Controls.Add(import);
            buttons.Controls.Add(saveTemplate);
            root.Controls.Add(buttons, 0, 3);

            Controls.Add(root);
            CancelButton = close;
            RefreshLibrary();
        }

        private void ConfigureGrid()
        {
            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.AllowUserToResizeRows = false;
            _grid.MultiSelect = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.AutoGenerateColumns = false;
            _grid.RowHeadersVisible = false;
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Replay", Width = 230 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Visiting team", Width = 165 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Home team", Width = 165 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Date", Width = 110 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Playback", Width = 95 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Status", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            _grid.SelectionChanged += (sender, args) => UpdateButtons();
            _grid.CellDoubleClick += (sender, args) =>
            {
                if (args.RowIndex >= 0)
                    AcceptSelection();
            };
        }

        private void RefreshLibrary(string? selectPath = null)
        {
            _grid.Rows.Clear();
            IReadOnlyList<string> paths;
            try
            {
                paths = ReplayStore.LibraryReplayFiles(_folder);
            }
            catch (Exception ex)
            {
                _status.Text = "Replay library unavailable: " + ex.Message;
                _status.ForeColor = Color.DarkRed;
                UpdateButtons();
                return;
            }

            foreach (string path in paths)
            {
                ReplayLibraryItem item = ReadLibraryItem(path);
                int rowIndex = _grid.Rows.Add(
                    Path.GetFileName(path),
                    TeamName(item.Replay?.Teams?.Away),
                    TeamName(item.Replay?.Teams?.Home),
                    item.Replay?.Game?.DatePlayed ?? "",
                    item.Replay?.PlaybackQuality ?? "Unavailable",
                    item.Status);
                DataGridViewRow row = _grid.Rows[rowIndex];
                row.Tag = item;
                if (!item.Valid)
                    row.DefaultCellStyle.ForeColor = Color.DarkRed;
                if (!string.IsNullOrWhiteSpace(selectPath) &&
                    string.Equals(path, selectPath, StringComparison.OrdinalIgnoreCase))
                {
                    _grid.ClearSelection();
                    row.Selected = true;
                    _grid.CurrentCell = row.Cells[0];
                }
            }

            if (_grid.SelectedRows.Count == 0 && _grid.Rows.Count > 0)
            {
                _grid.Rows[0].Selected = true;
                _grid.CurrentCell = _grid.Rows[0].Cells[0];
            }
            _status.ForeColor = SystemColors.ControlText;
            _status.Text = paths.Count == 0
                ? "No replays imported. Library: " + _folder
                : paths.Count + " replay" + (paths.Count == 1 ? "" : "s") + " in " + _folder;
            UpdateButtons();
        }

        private static ReplayLibraryItem ReadLibraryItem(string path)
        {
            try
            {
                ReplayFile replay = ReplayStore.Load(path);
                string issues = replay.ReplayIssues?.Count > 0
                    ? replay.ReplayIssues.Count + " playback note" + (replay.ReplayIssues.Count == 1 ? "" : "s")
                    : "Ready";
                return new ReplayLibraryItem(path, replay, issues, "");
            }
            catch (Exception ex)
            {
                return new ReplayLibraryItem(path, null, ex.Message, ex.Message);
            }
        }

        private static string TeamName(ReplayTeam? team)
        {
            if (team == null)
                return "";
            string fullName = string.Join(" ", new[] { team.TeamName, team.Mascot }
                .Where(value => !string.IsNullOrWhiteSpace(value)));
            return string.IsNullOrWhiteSpace(fullName) ? team.ScoreboardAbbreviation ?? "" : fullName;
        }

        private ReplayLibraryItem? SelectedItem()
            => _grid.SelectedRows.Count == 0 ? null : _grid.SelectedRows[0].Tag as ReplayLibraryItem;

        private void UpdateButtons()
        {
            ReplayLibraryItem item = SelectedItem();
            _watchButton.Enabled = item?.Valid == true;
            _removeButton.Enabled = item != null;
        }

        private void AcceptSelection()
        {
            ReplayLibraryItem item = SelectedItem();
            if (item?.Valid != true)
                return;
            SelectedReplayPath = item.Path;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void ImportReplays()
        {
            using var dialog = new OpenFileDialog
            {
                Title = "Import Replay Files",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                Filter = "RBI replay (*" + ReplayStore.Extension + ")|*" + ReplayStore.Extension + "|JSON (*.json)|*.json|All files (*.*)|*.*",
                Multiselect = true,
                CheckFileExists = true
            };
            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;

            string? lastImported = null;
            var failures = new List<string>();
            foreach (string source in dialog.FileNames)
            {
                try
                {
                    lastImported = ReplayStore.ImportToLibrary(source, _folder);
                }
                catch (Exception ex)
                {
                    failures.Add(Path.GetFileName(source) + ": " + ex.Message);
                }
            }
            RefreshLibrary(lastImported);
            if (failures.Count > 0)
            {
                MessageBox.Show(this,
                    "Some replay files could not be imported.\n\n" + string.Join("\n", failures),
                    "Replay import",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private void SaveTemplate()
        {
            using var dialog = new SaveFileDialog
            {
                Title = "Save Replay Template",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                FileName = "ReplayTemplate" + ReplayStore.Extension,
                DefaultExt = ReplayStore.Extension.TrimStart('.'),
                Filter = "RBI replay (*" + ReplayStore.Extension + ")|*" + ReplayStore.Extension + "|JSON (*.json)|*.json",
                AddExtension = true,
                OverwritePrompt = true
            };
            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;
            try
            {
                ReplayStore.SaveBundledTemplate(dialog.FileName);
                MessageBox.Show(this,
                    "The replay template and ExactReplaySchema.md format guide were saved. Edit the template copy, then use Import Replay to add it to this library.",
                    "Replay template",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not save the replay template.\n\n" + ex.Message,
                    "Replay template", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RemoveSelected()
        {
            ReplayLibraryItem item = SelectedItem();
            if (item == null)
                return;
            if (MessageBox.Show(this,
                    "Remove this replay from the managed library?\n\n" + Path.GetFileName(item.Path),
                    "Remove replay",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            try
            {
                ReplayStore.DeleteLibraryReplay(item.Path, _folder);
                RefreshLibrary();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not remove replay.\n\n" + ex.Message,
                    "Replay library", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OpenLibraryFolder()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = ReplayStore.EnsureReplayFolder(_folder),
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not open the replay folder.\n\n" + ex.Message,
                    "Replay library", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private sealed class ReplayLibraryItem
        {
            public string Path { get; }
            public ReplayFile? Replay { get; }
            public string Error { get; }
            public string Status { get; }
            public bool Valid => Replay != null && string.IsNullOrWhiteSpace(Error);

            public ReplayLibraryItem(string path, ReplayFile? replay, string status, string error)
            {
                Path = path;
                Replay = replay;
                Status = status ?? "";
                Error = error ?? "";
            }
        }
    }
}
