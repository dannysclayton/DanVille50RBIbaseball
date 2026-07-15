using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace StandaloneBaseball
{
    internal sealed class DynastyBackupRecoveryDialog : Form
    {
        private readonly DataGridView _grid = new DataGridView();
        private readonly Button _restoreButton = new Button();
        private readonly List<LeagueBackupInfo> _backups;

        public LeagueBackupInfo? SelectedBackup { get; private set; }

        public DynastyBackupRecoveryDialog(string primaryPath, string loadError, IEnumerable<LeagueBackupInfo> backups)
        {
            _backups = (backups ?? Enumerable.Empty<LeagueBackupInfo>()).ToList();
            Text = "Restore Dynasty Backup";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(820, 480);
            MinimumSize = new Size(680, 400);
            MinimizeBox = false;
            MaximizeBox = false;

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(12)
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            root.Controls.Add(new Label
            {
                AutoSize = true,
                MaximumSize = new Size(780, 0),
                Text = "Select a valid backup to recover. The damaged primary file will not be replaced until you save the recovered dynasty."
            }, 0, 0);

            string details = "Primary: " + (primaryPath ?? "");
            if (!string.IsNullOrWhiteSpace(loadError))
                details += Environment.NewLine + "Load error: " + loadError;
            root.Controls.Add(new TextBox
            {
                Dock = DockStyle.Top,
                Multiline = true,
                ReadOnly = true,
                Height = string.IsNullOrWhiteSpace(loadError) ? 28 : 52,
                Text = details,
                BackColor = SystemColors.Control
            }, 0, 1);

            ConfigureGrid();
            root.Controls.Add(_grid, 0, 2);

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Padding = new Padding(0, 8, 0, 0)
            };
            var cancel = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
            _restoreButton.Text = "Open Recovered Copy";
            _restoreButton.AutoSize = true;
            _restoreButton.Click += (sender, args) => AcceptSelection();
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(_restoreButton);
            root.Controls.Add(buttons, 0, 3);

            Controls.Add(root);
            CancelButton = cancel;
            PopulateRows();
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
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Backup time", Width = 170 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Status", Width = 90 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Size", Width = 90 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "File", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            _grid.SelectionChanged += (sender, args) => UpdateRestoreButton();
            _grid.CellDoubleClick += (sender, args) =>
            {
                if (args.RowIndex >= 0)
                    AcceptSelection();
            };
        }

        private void PopulateRows()
        {
            foreach (var backup in _backups)
            {
                int rowIndex = _grid.Rows.Add(
                    backup.SavedAt.ToString("g"),
                    backup.IsValid ? "Valid" : "Damaged",
                    FormatSize(backup.SizeBytes),
                    Path.GetFileName(backup.Path));
                _grid.Rows[rowIndex].Tag = backup;
                if (!backup.IsValid)
                    _grid.Rows[rowIndex].DefaultCellStyle.ForeColor = Color.DarkRed;
            }

            var firstValid = _grid.Rows.Cast<DataGridViewRow>()
                .FirstOrDefault(row => (row.Tag as LeagueBackupInfo)?.IsValid == true);
            if (firstValid != null)
            {
                _grid.ClearSelection();
                firstValid.Selected = true;
                _grid.CurrentCell = firstValid.Cells[0];
            }
            UpdateRestoreButton();
        }

        private void UpdateRestoreButton()
        {
            _restoreButton.Enabled = SelectedRowBackup()?.IsValid == true;
        }

        private LeagueBackupInfo? SelectedRowBackup()
            => _grid.SelectedRows.Count == 0 ? null : _grid.SelectedRows[0].Tag as LeagueBackupInfo;

        private void AcceptSelection()
        {
            var selected = SelectedRowBackup();
            if (selected?.IsValid != true)
                return;
            SelectedBackup = selected;
            DialogResult = DialogResult.OK;
            Close();
        }

        private static string FormatSize(long bytes)
        {
            if (bytes >= 1024 * 1024)
                return (bytes / (1024d * 1024d)).ToString("0.0") + " MB";
            if (bytes >= 1024)
                return (bytes / 1024d).ToString("0.0") + " KB";
            return Math.Max(0, bytes) + " B";
        }
    }
}
