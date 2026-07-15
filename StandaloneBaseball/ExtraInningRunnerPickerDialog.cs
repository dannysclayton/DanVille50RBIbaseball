#nullable enable annotations

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace StandaloneBaseball
{
    public sealed class ExtraInningRunnerPickerDialog : Form
    {
        private readonly DataGridView _grid;
        private readonly Button _pickButton;

        public Player? SelectedPlayer { get; private set; }

        public ExtraInningRunnerPickerDialog(
            Team team,
            IEnumerable<Player> candidates,
            IReadOnlyDictionary<Guid, int> pinchUses,
            string title = "Choose Runner on Second",
            string? instruction = null)
        {
            Text = title;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(680, 420);
            MinimumSize = new Size(560, 340);

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(10)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            Controls.Add(root);

            root.Controls.Add(new Label
            {
                Text = instruction ?? ((team?.ScoreboardName ?? "Team") + " extra-inning runner: next 8 scheduled batters are ineligible."),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 0);

            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None
            };
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Player", Width = 190 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Role", Width = 80 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Positions", Width = 100 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Speed", Width = 70 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Pinch Uses", Width = 90 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Class", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            _grid.SelectionChanged += (s, e) => UpdateSelection();
            _grid.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex >= 0 && SelectedPlayer != null)
                {
                    DialogResult = DialogResult.OK;
                    Close();
                }
            };
            root.Controls.Add(_grid, 0, 1);

            foreach (var player in candidates.OrderByDescending(p => p.Speed).ThenByDescending(p => p.Overall))
            {
                int uses = 0;
                pinchUses?.TryGetValue(player.Id, out uses);
                int row = _grid.Rows.Add(player.Name, player.Role, player.Positions, player.Speed, uses, player.Classification);
                _grid.Rows[row].Tag = player;
            }

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft
            };
            _pickButton = new Button { Text = "Use Runner", AutoSize = true, Enabled = false };
            _pickButton.Click += (s, e) =>
            {
                UpdateSelection();
                if (SelectedPlayer == null) return;
                DialogResult = DialogResult.OK;
                Close();
            };
            var cancel = new Button { Text = "Auto Pick", AutoSize = true };
            cancel.Click += (s, e) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };
            buttons.Controls.Add(_pickButton);
            buttons.Controls.Add(cancel);
            root.Controls.Add(buttons, 0, 2);

            if (_grid.Rows.Count > 0)
            {
                _grid.CurrentCell = _grid.Rows[0].Cells[0];
                _grid.Rows[0].Selected = true;
            }
            UpdateSelection();
        }

        private void UpdateSelection()
        {
            SelectedPlayer = _grid.CurrentRow?.Tag as Player;
            _pickButton.Enabled = SelectedPlayer != null;
        }
    }
}
