#nullable enable annotations

using System;
using System.Drawing;
using System.Windows.Forms;

namespace StandaloneBaseball
{
    public sealed class DynastySetupDialog : Form
    {
        private readonly TextBox _nameBox;
        private readonly TextBox _ownerNameBox;
        private readonly ComboBox _inningsCombo;
        private readonly CheckBox _extraInningsBox;
        private readonly CheckBox _extraRunnerBox;
        private readonly CheckBox _mercyRuleBox;
        private readonly CheckBox _courtesyRunnerBox;
        private readonly NumericUpDown _seriesLengthBox;
        private readonly NumericUpDown _districtHomeBox;
        private readonly NumericUpDown _districtAwayBox;
        private readonly NumericUpDown _regionHomeBox;
        private readonly NumericUpDown _regionAwayBox;
        private readonly NumericUpDown _conferenceHomeBox;
        private readonly NumericUpDown _conferenceAwayBox;
        private readonly NumericUpDown _nonConferenceHomeBox;
        private readonly NumericUpDown _nonConferenceAwayBox;
        private readonly TextBox _assetLibraryBox;

        public LeagueRules SelectedRules { get; private set; } = new LeagueRules();
        public string DynastyName { get; private set; } = "";
        public string OwnerFullName { get; private set; } = "";
        public string AssetLibraryPath { get; private set; } = "";

        public DynastySetupDialog(
            LeagueRules? currentRules = null,
            string? currentName = null,
            string? currentOwnerFullName = null,
            string? currentAssetLibraryPath = null)
        {
            currentRules ??= new LeagueRules();
            currentName = string.IsNullOrWhiteSpace(currentName) ? "New Baseball Dynasty" : currentName.Trim();
            currentOwnerFullName = (currentOwnerFullName ?? "").Trim();

            Text = "Create Dynasty";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(560, 610);
            MinimumSize = new Size(520, 590);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(14)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
            Controls.Add(root);

            root.Controls.Add(new Label
            {
                Text = "Choose the rules for this dynasty.",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font(Font.FontFamily, 11, FontStyle.Bold)
            }, 0, 0);

            var rules = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 14
            };
            rules.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
            rules.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            rules.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < 14; i++)
                rules.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            root.Controls.Add(rules, 0, 1);

            rules.Controls.Add(new Label { Text = "Your Full Name", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
            _ownerNameBox = new TextBox { Dock = DockStyle.Fill, Text = currentOwnerFullName, MaxLength = 80 };
            rules.Controls.Add(_ownerNameBox, 1, 0);
            rules.SetColumnSpan(_ownerNameBox, 2);

            rules.Controls.Add(new Label { Text = "Dynasty Name", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
            _nameBox = new TextBox { Dock = DockStyle.Fill, Text = currentName, MaxLength = 80 };
            rules.Controls.Add(_nameBox, 1, 1);
            rules.SetColumnSpan(_nameBox, 2);

            rules.Controls.Add(new Label { Text = "Game Length", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
            var inningsWrap = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = new Padding(0) };
            _inningsCombo = new ComboBox { Width = 72, DropDownStyle = ComboBoxStyle.DropDownList };
            for (int innings = 5; innings <= 9; innings++)
                _inningsCombo.Items.Add(innings);
            _inningsCombo.SelectedItem = Math.Clamp(currentRules.Innings, 5, 9);
            inningsWrap.Controls.Add(_inningsCombo);
            inningsWrap.Controls.Add(new Label { Text = "innings", AutoSize = true, Margin = new Padding(4, 7, 0, 0) });
            rules.Controls.Add(inningsWrap, 1, 2);

            _mercyRuleBox = new CheckBox
            {
                Text = "10-run mercy rule after the top of the 5th",
                Checked = currentRules.MercyRuleEnabled,
                AutoSize = true,
                Anchor = AnchorStyles.Left
            };
            rules.Controls.Add(_mercyRuleBox, 1, 3);
            rules.SetColumnSpan(_mercyRuleBox, 2);

            _extraInningsBox = new CheckBox
            {
                Text = "Play extra innings when tied",
                Checked = currentRules.ExtraInnings,
                AutoSize = true,
                Anchor = AnchorStyles.Left
            };
            rules.Controls.Add(_extraInningsBox, 1, 4);
            rules.SetColumnSpan(_extraInningsBox, 2);

            _extraRunnerBox = new CheckBox
            {
                Text = "Use runner-on-second rule in extra innings",
                Checked = currentRules.ExtraInningRunnerOnSecond,
                Enabled = currentRules.ExtraInnings,
                AutoSize = true,
                Anchor = AnchorStyles.Left
            };
            rules.Controls.Add(_extraRunnerBox, 1, 5);
            rules.SetColumnSpan(_extraRunnerBox, 2);
            _extraInningsBox.CheckedChanged += (s, e) => _extraRunnerBox.Enabled = _extraInningsBox.Checked;

            _courtesyRunnerBox = new CheckBox
            {
                Text = "Allow courtesy runners for pitchers and catchers",
                Checked = currentRules.CourtesyRunnerForPitchersCatchers,
                AutoSize = true,
                Anchor = AnchorStyles.Left
            };
            rules.Controls.Add(_courtesyRunnerBox, 1, 6);
            rules.SetColumnSpan(_courtesyRunnerBox, 2);

            rules.Controls.Add(new Label { Text = "Series Length", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 7);
            _seriesLengthBox = new NumericUpDown
            {
                Minimum = 1,
                Maximum = 6,
                Value = Math.Clamp(currentRules.Schedule?.SeriesLength ?? 3, 1, 6),
                Width = 72
            };
            rules.Controls.Add(_seriesLengthBox, 1, 7);
            rules.Controls.Add(new Label { Text = "games per series", AutoSize = true, Anchor = AnchorStyles.Left }, 2, 7);

            rules.Controls.Add(new Label { Text = "Schedule Type", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 8);
            rules.Controls.Add(new Label { Text = "Home Games", AutoSize = true, Anchor = AnchorStyles.Left }, 1, 8);
            rules.Controls.Add(new Label { Text = "Away Games", AutoSize = true, Anchor = AnchorStyles.Left }, 2, 8);
            AddScheduleRow(rules, 9, "District", currentRules.Schedule?.DistrictHomeGames ?? 0, currentRules.Schedule?.DistrictAwayGames ?? 0, out _districtHomeBox, out _districtAwayBox);
            AddScheduleRow(rules, 10, "Region", currentRules.Schedule?.RegionHomeGames ?? 0, currentRules.Schedule?.RegionAwayGames ?? 0, out _regionHomeBox, out _regionAwayBox);
            AddScheduleRow(rules, 11, "Conference", currentRules.Schedule?.ConferenceHomeGames ?? 0, currentRules.Schedule?.ConferenceAwayGames ?? 0, out _conferenceHomeBox, out _conferenceAwayBox);
            AddScheduleRow(rules, 12, "Non-Conference", currentRules.Schedule?.NonConferenceHomeGames ?? 0, currentRules.Schedule?.NonConferenceAwayGames ?? 0, out _nonConferenceHomeBox, out _nonConferenceAwayBox);

            rules.Controls.Add(new Label { Text = "Asset Library", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 13);
            var libraryRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = new Padding(0) };
            libraryRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            libraryRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 76));
            _assetLibraryBox = new TextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                Text = (currentAssetLibraryPath ?? "").Trim(),
                PlaceholderText = "Not configured"
            };
            var libraryButton = new Button { Text = "Set Up...", Dock = DockStyle.Fill, Margin = new Padding(4, 0, 0, 0) };
            libraryButton.Click += (s, e) =>
            {
                using var dialog = new AssetLibrarySetupDialog(_assetLibraryBox.Text);
                if (dialog.ShowDialog(this) == DialogResult.OK)
                    _assetLibraryBox.Text = dialog.SelectedPath;
            };
            libraryRow.Controls.Add(_assetLibraryBox, 0, 0);
            libraryRow.Controls.Add(libraryButton, 1, 0);
            rules.Controls.Add(libraryRow, 1, 13);
            rules.SetColumnSpan(libraryRow, 2);

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft
            };
            var create = new Button { Text = "Create Dynasty", AutoSize = true };
            create.Click += (s, e) =>
            {
                DynastyName = NormalizeName(_nameBox.Text);
                OwnerFullName = NormalizeOwnerName(_ownerNameBox.Text);
                AssetLibraryPath = (_assetLibraryBox.Text ?? "").Trim();
                SelectedRules = BuildRules();
                if (!ValidateScheduleRules(SelectedRules.Schedule, out string? error))
                {
                    MessageBox.Show(this, error, "Schedule rules", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                DialogResult = DialogResult.OK;
                Close();
            };
            var cancel = new Button { Text = "Cancel", AutoSize = true };
            cancel.Click += (s, e) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };
            buttons.Controls.Add(create);
            buttons.Controls.Add(cancel);
            root.Controls.Add(buttons, 0, 2);
        }

        private LeagueRules BuildRules()
        {
            return new LeagueRules
            {
                Innings = Math.Clamp(Convert.ToInt32(_inningsCombo.SelectedItem), 5, 9),
                LineupSize = 9,
                ExtraInnings = _extraInningsBox.Checked,
                ExtraInningRunnerOnSecond = _extraInningsBox.Checked && _extraRunnerBox.Checked,
                MercyRuleEnabled = _mercyRuleBox.Checked,
                MercyRuleRuns = 10,
                MercyRuleMinimumInning = 5,
                CourtesyRunnerForPitchersCatchers = _courtesyRunnerBox.Checked,
                Schedule = new SeasonScheduleRules
                {
                    SeriesLength = (int)_seriesLengthBox.Value,
                    DistrictHomeGames = (int)_districtHomeBox.Value,
                    DistrictAwayGames = (int)_districtAwayBox.Value,
                    RegionHomeGames = (int)_regionHomeBox.Value,
                    RegionAwayGames = (int)_regionAwayBox.Value,
                    ConferenceHomeGames = (int)_conferenceHomeBox.Value,
                    ConferenceAwayGames = (int)_conferenceAwayBox.Value,
                    NonConferenceHomeGames = (int)_nonConferenceHomeBox.Value,
                    NonConferenceAwayGames = (int)_nonConferenceAwayBox.Value
                }
            };
        }

        private static void AddScheduleRow(
            TableLayoutPanel table,
            int row,
            string label,
            int homeValue,
            int awayValue,
            out NumericUpDown home,
            out NumericUpDown away)
        {
            table.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
            home = CountBox(homeValue);
            away = CountBox(awayValue);
            table.Controls.Add(home, 1, row);
            table.Controls.Add(away, 2, row);
        }

        private static NumericUpDown CountBox(int value)
        {
            return new NumericUpDown
            {
                Minimum = 0,
                Maximum = 100,
                Value = Math.Clamp(value, 0, 100),
                Width = 72
            };
        }

        private static bool ValidateScheduleRules(SeasonScheduleRules rules, out string? error)
        {
            error = null;
            if (!Balanced("District", rules.DistrictHomeGames, rules.DistrictAwayGames, out error)) return false;
            if (!Balanced("Region", rules.RegionHomeGames, rules.RegionAwayGames, out error)) return false;
            if (!Balanced("Conference", rules.ConferenceHomeGames, rules.ConferenceAwayGames, out error)) return false;
            if (!Balanced("Non-conference", rules.NonConferenceHomeGames, rules.NonConferenceAwayGames, out error)) return false;
            return true;
        }

        private static bool Balanced(string label, int home, int away, out string? error)
        {
            error = null;
            if (home == away) return true;
            error = label + " home and away games must match so the schedule can be followed exactly.";
            return false;
        }

        private static string NormalizeName(string value)
        {
            value = (value ?? "").Trim();
            return string.IsNullOrWhiteSpace(value) ? "New Baseball Dynasty" : value;
        }

        private static string NormalizeOwnerName(string value)
            => (value ?? "").Trim();
    }
}
