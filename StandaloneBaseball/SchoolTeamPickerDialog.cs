using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace StandaloneBaseball
{
    public sealed class SchoolTeamPickerDialog : Form
    {
        private readonly List<SchoolTeamRecord> _schools;
        private readonly TextBox _searchBox;
        private readonly DataGridView _grid;
        private readonly Button _createButton;

        public SchoolTeamRecord SelectedSchool { get; private set; }

        public SchoolTeamPickerDialog(IEnumerable<SchoolTeamRecord> schools)
        {
            _schools = schools.OrderBy(s => s.Name).ThenBy(s => s.State).ThenBy(s => s.Mascot).ToList();

            Text = "Create Team From Schools CSV";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(900, 560);
            MinimumSize = new Size(720, 420);

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(10)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            Controls.Add(root);

            _searchBox = new TextBox { Dock = DockStyle.Fill, PlaceholderText = "Search school, mascot, city, or state" };
            _searchBox.TextChanged += (s, e) => RefreshRows();
            root.Controls.Add(_searchBox, 0, 0);

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
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "School", Width = 210 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Mascot", Width = 150 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "City", Width = 150 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "State", Width = 60 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Primary", Width = 92 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Secondary", Width = 92 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Logo", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            _grid.SelectionChanged += (s, e) => UpdateSelection();
            _grid.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex >= 0 && SelectedSchool != null)
                {
                    DialogResult = DialogResult.OK;
                    Close();
                }
            };
            root.Controls.Add(_grid, 0, 1);

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft
            };
            _createButton = new Button { Text = "Create Team", AutoSize = true, Enabled = false };
            _createButton.Click += (s, e) =>
            {
                UpdateSelection();
                if (SelectedSchool == null) return;
                DialogResult = DialogResult.OK;
                Close();
            };
            var cancel = new Button { Text = "Cancel", AutoSize = true };
            cancel.Click += (s, e) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };
            buttons.Controls.Add(_createButton);
            buttons.Controls.Add(cancel);
            root.Controls.Add(buttons, 0, 2);

            RefreshRows();
        }

        private void RefreshRows()
        {
            string query = (_searchBox.Text ?? "").Trim();
            var rows = string.IsNullOrWhiteSpace(query)
                ? _schools
                : _schools.Where(s => Contains(s.Name, query)
                    || Contains(s.Mascot, query)
                    || Contains(s.City, query)
                    || Contains(s.State, query)
                    || Contains(s.DisplayName, query)).ToList();

            _grid.SuspendLayout();
            _grid.Rows.Clear();
            foreach (var school in rows)
            {
                int row = _grid.Rows.Add(
                    school.Name,
                    school.Mascot,
                    school.City,
                    school.State,
                    school.PrimaryColor,
                    school.SecondaryColor,
                    school.LogoAvailable ? "Available" : "");
                _grid.Rows[row].Tag = school;
            }

            if (_grid.Rows.Count > 0)
            {
                _grid.CurrentCell = _grid.Rows[0].Cells[0];
                _grid.Rows[0].Selected = true;
            }
            _grid.ResumeLayout();
            UpdateSelection();
        }

        private void UpdateSelection()
        {
            SelectedSchool = _grid.CurrentRow?.Tag as SchoolTeamRecord;
            _createButton.Enabled = SelectedSchool != null;
        }

        private static bool Contains(string value, string query)
            => (value ?? "").IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
