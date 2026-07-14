using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace StandaloneBaseball
{
    public sealed class UserControlledTeamsDialog : Form
    {
        private readonly CheckedListBox _teamList;
        private readonly List<Team> _teams;

        public List<Guid> SelectedTeamIds { get; private set; } = new List<Guid>();

        public UserControlledTeamsDialog(IEnumerable<Team> teams, IEnumerable<Guid> selectedTeamIds)
        {
            _teams = (teams ?? Enumerable.Empty<Team>()).ToList();
            var selected = new HashSet<Guid>(selectedTeamIds ?? Enumerable.Empty<Guid>());

            Text = "User Controlled Teams";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(420, 500);
            MinimumSize = new Size(360, 360);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                ColumnCount = 1,
                Padding = new Padding(12)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            Controls.Add(root);

            root.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = "Select every team the user can control by default. A scheduled game can still override this on the Game tab.",
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 0);

            _teamList = new CheckedListBox
            {
                Dock = DockStyle.Fill,
                CheckOnClick = true,
                IntegralHeight = false
            };
            foreach (var team in _teams)
                _teamList.Items.Add(team.DisplayName, selected.Contains(team.Id));
            root.Controls.Add(_teamList, 0, 1);

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false
            };
            var save = new Button { Text = "Save", AutoSize = true };
            save.Click += (s, e) =>
            {
                SelectedTeamIds = _teamList.CheckedIndices
                    .Cast<int>()
                    .Where(i => i >= 0 && i < _teams.Count)
                    .Select(i => _teams[i].Id)
                    .ToList();
                DialogResult = DialogResult.OK;
                Close();
            };
            var cancel = new Button { Text = "Cancel", AutoSize = true };
            cancel.Click += (s, e) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };
            buttons.Controls.Add(save);
            buttons.Controls.Add(cancel);
            root.Controls.Add(buttons, 0, 2);

            AcceptButton = save;
            CancelButton = cancel;
        }
    }
}
