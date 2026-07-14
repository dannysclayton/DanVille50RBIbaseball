using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace StandaloneBaseball
{
    internal sealed class LineupCardExportDialog : Form
    {
        private readonly LeagueFile _league;
        private readonly Func<Team, string?> _logoResolver;
        private readonly CheckedListBox _teams = new CheckedListBox();
        private readonly RadioButton _selectedScope = new RadioButton();
        private readonly RadioButton _leagueScope = new RadioButton();
        private readonly PictureBox _logo = new PictureBox();
        private readonly Label _previewTitle = new Label();
        private readonly DataGridView _previewGrid = new DataGridView();

        public LineupCardExportDialog(LeagueFile league, Team? selectedTeam, Func<Team, string?> logoResolver)
        {
            _league = league ?? throw new ArgumentNullException(nameof(league));
            _logoResolver = logoResolver ?? throw new ArgumentNullException(nameof(logoResolver));

            Text = "Lineup Cards";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(900, 610);
            Size = new Size(1040, 700);
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                Padding = new Padding(12)
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 280));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            Controls.Add(root);

            root.Controls.Add(BuildSelectionPanel(selectedTeam), 0, 0);
            root.Controls.Add(BuildPreviewPanel(), 1, 0);

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Padding = new Padding(0, 7, 0, 0)
            };
            buttons.Controls.Add(MakeButton("Close", (_, _) => Close()));
            buttons.Controls.Add(MakeButton("Export Cards...", (_, _) => ExportCards()));
            buttons.Controls.Add(MakeButton("Save Blank Template...", (_, _) => SaveBlankTemplate(), 156));
            root.Controls.Add(buttons, 0, 1);
            root.SetColumnSpan(buttons, 2);

            _teams.SelectedIndexChanged += (_, _) => RefreshPreview();
            _teams.ItemCheck += (_, _) => BeginInvoke(new Action(RefreshPreview));
            _selectedScope.CheckedChanged += (_, _) => ApplyScope();
            _leagueScope.CheckedChanged += (_, _) => ApplyScope();
            FormClosed += (_, _) => DisposePreviewLogo();

            _selectedScope.Checked = true;
            RefreshPreview();
        }

        private Control BuildSelectionPanel(Team? selectedTeam)
        {
            var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(0, 0, 12, 0) };
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 4));

            var scope = new GroupBox { Text = "Scope", Dock = DockStyle.Fill };
            var scopeFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(8, 8, 0, 0), WrapContents = false };
            _selectedScope.Text = "Selected teams";
            _selectedScope.AutoSize = true;
            _leagueScope.Text = "Entire league";
            _leagueScope.AutoSize = true;
            scopeFlow.Controls.Add(_selectedScope);
            scopeFlow.Controls.Add(_leagueScope);
            scope.Controls.Add(scopeFlow);
            panel.Controls.Add(scope, 0, 0);

            _teams.Dock = DockStyle.Fill;
            _teams.CheckOnClick = true;
            _teams.IntegralHeight = false;
            foreach (Team team in (_league.Teams ?? new List<Team>()).OrderBy(team => team.DisplayName, StringComparer.OrdinalIgnoreCase))
            {
                int index = _teams.Items.Add(new TeamChoice(team));
                if (selectedTeam != null && team.Id == selectedTeam.Id)
                {
                    _teams.SetItemChecked(index, true);
                    _teams.SelectedIndex = index;
                }
            }
            if (_teams.SelectedIndex < 0 && _teams.Items.Count > 0)
            {
                _teams.SelectedIndex = 0;
                _teams.SetItemChecked(0, true);
            }
            panel.Controls.Add(_teams, 0, 1);

            var selectionButtons = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
            selectionButtons.Controls.Add(MakeButton("Select All", (_, _) => CheckAll(true), 92));
            selectionButtons.Controls.Add(MakeButton("Clear", (_, _) => CheckAll(false), 72));
            panel.Controls.Add(selectionButtons, 0, 2);
            return panel;
        }

        private Control BuildPreviewPanel()
        {
            var group = new GroupBox { Text = "Card Preview", Dock = DockStyle.Fill, Padding = new Padding(10) };
            var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 104));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            _logo.Dock = DockStyle.Fill;
            _logo.SizeMode = PictureBoxSizeMode.Zoom;
            _logo.BackColor = Color.White;
            _logo.BorderStyle = BorderStyle.FixedSingle;
            panel.Controls.Add(_logo, 0, 0);

            _previewTitle.Dock = DockStyle.Fill;
            _previewTitle.TextAlign = ContentAlignment.MiddleCenter;
            _previewTitle.Font = new Font(Font.FontFamily, 18, FontStyle.Bold);
            _previewTitle.AutoEllipsis = true;
            panel.Controls.Add(_previewTitle, 1, 0);

            _previewGrid.Dock = DockStyle.Fill;
            _previewGrid.ReadOnly = true;
            _previewGrid.AllowUserToAddRows = false;
            _previewGrid.AllowUserToDeleteRows = false;
            _previewGrid.AllowUserToResizeRows = false;
            _previewGrid.RowHeadersVisible = false;
            _previewGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _previewGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _previewGrid.Columns.Add("Number", "#");
            _previewGrid.Columns.Add("Player", "Player");
            _previewGrid.Columns.Add("Role", "Role");
            _previewGrid.Columns.Add("Bat", "Bat");
            _previewGrid.Columns.Add("Positions", "Positions");
            _previewGrid.Columns[0].FillWeight = 16;
            _previewGrid.Columns[1].FillWeight = 52;
            _previewGrid.Columns[2].FillWeight = 22;
            _previewGrid.Columns[3].FillWeight = 18;
            _previewGrid.Columns[4].FillWeight = 42;
            panel.Controls.Add(_previewGrid, 0, 1);
            panel.SetColumnSpan(_previewGrid, 2);
            group.Controls.Add(panel);
            return group;
        }

        private void ApplyScope()
        {
            if (_leagueScope.Checked)
            {
                CheckAll(true);
                _teams.Enabled = false;
            }
            else
            {
                _teams.Enabled = true;
            }
            RefreshPreview();
        }

        private void CheckAll(bool value)
        {
            for (int index = 0; index < _teams.Items.Count; index++)
                _teams.SetItemChecked(index, value);
            RefreshPreview();
        }

        private List<Team> SelectedTeams()
        {
            if (_leagueScope.Checked)
                return (_league.Teams ?? new List<Team>()).OrderBy(team => team.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();

            return _teams.CheckedItems.Cast<TeamChoice>().Select(choice => choice.Team).ToList();
        }

        private Team? PreviewTeam()
        {
            if (_teams.SelectedItem is TeamChoice selected)
                return selected.Team;
            return SelectedTeams().FirstOrDefault();
        }

        private void RefreshPreview()
        {
            Team? team = PreviewTeam();
            _previewGrid.Rows.Clear();
            DisposePreviewLogo();
            if (team == null)
            {
                _previewTitle.Text = "No team selected";
                return;
            }

            LineupCardDocumentPage page = LineupCardExporter.BuildPage(team, _logoResolver(team));
            _previewTitle.Text = team.DisplayName + Environment.NewLine + "LINEUP CARD";
            _previewTitle.BackColor = Color.FromArgb(team.PrimaryArgb);
            _previewTitle.ForeColor = ContrastColor(Color.FromArgb(team.PrimaryArgb));
            foreach (LineupCardDocumentRow row in page.Rows)
                _previewGrid.Rows.Add(row.Number, row.PlayerName, row.Role, row.BatGrade, row.Positions);

            string? logoPath = _logoResolver(team);
            if (!string.IsNullOrWhiteSpace(logoPath) && File.Exists(logoPath))
            {
                try
                {
                    using var source = Image.FromFile(logoPath);
                    _logo.Image = new Bitmap(source);
                }
                catch
                {
                    _logo.Image = null;
                }
            }
        }

        private void ExportCards()
        {
            List<Team> teams = SelectedTeams();
            if (teams.Count == 0)
            {
                MessageBox.Show(this, "Select at least one team.", "Lineup Cards", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string defaultName = teams.Count == 1
                ? SafeFileName(teams[0].DisplayName + " Lineup Card")
                : SafeFileName(_league.Name + " Lineup Cards");
            using var dialog = new SaveFileDialog
            {
                Title = "Export lineup cards",
                Filter = "Microsoft Word document (*.docx)|*.docx",
                FileName = defaultName + ".docx",
                AddExtension = true,
                DefaultExt = "docx"
            };
            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;

            try
            {
                var pages = teams.Select(team => LineupCardExporter.BuildPage(team, _logoResolver(team))).ToList();
                LineupCardExporter.WriteDocx(dialog.FileName, _league.Name + " Lineup Cards", pages);
                MessageBox.Show(this, teams.Count + " lineup card(s) exported.", "Lineup Cards", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not export lineup cards.\n\n" + ex.Message, "Lineup Cards", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SaveBlankTemplate()
        {
            using var dialog = new SaveFileDialog
            {
                Title = "Save blank lineup-card template",
                Filter = "Microsoft Word document (*.docx)|*.docx",
                FileName = "Lineup Card Template.docx",
                AddExtension = true,
                DefaultExt = "docx"
            };
            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;

            try
            {
                LineupCardExporter.WriteBlankTemplate(dialog.FileName);
                MessageBox.Show(this, "Blank lineup-card template saved.", "Lineup Cards", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not save the template.\n\n" + ex.Message, "Lineup Cards", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DisposePreviewLogo()
        {
            Image? image = _logo.Image;
            _logo.Image = null;
            image?.Dispose();
        }

        private static Color ContrastColor(Color color)
        {
            double luminance = (0.299 * color.R) + (0.587 * color.G) + (0.114 * color.B);
            return luminance >= 155 ? Color.Black : Color.White;
        }

        private static string SafeFileName(string value)
        {
            var invalid = Path.GetInvalidFileNameChars().ToHashSet();
            string safe = new string((value ?? "Lineup Cards").Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray()).Trim();
            return string.IsNullOrWhiteSpace(safe) ? "Lineup Cards" : safe;
        }

        private static Button MakeButton(string text, EventHandler click, int width = 112)
        {
            var button = new Button { Text = text, AutoSize = false, Width = width, Height = 30, Margin = new Padding(4, 0, 0, 0) };
            button.Click += click;
            return button;
        }

        private sealed class TeamChoice
        {
            public TeamChoice(Team team) => Team = team;
            public Team Team { get; }
            public override string ToString() => Team.DisplayName;
        }
    }
}
