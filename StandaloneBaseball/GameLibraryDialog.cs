using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace StandaloneBaseball
{
    internal sealed class GameLibraryDialog : Form
    {
        private readonly LeagueFile _league;
        private readonly Func<Team, string?> _logoPath;
        private readonly Func<Team, string> _assetDirectory;
        private readonly ComboBox _seasonCombo = new ComboBox();
        private readonly CheckedListBox _teams = new CheckedListBox();
        private readonly DataGridView _games = new DataGridView();

        public GameLibraryDialog(LeagueFile league, Team? selectedTeam, Func<Team, string?> logoPath, Func<Team, string> assetDirectory)
        {
            _league = league ?? throw new ArgumentNullException(nameof(league));
            _logoPath = logoPath ?? throw new ArgumentNullException(nameof(logoPath));
            _assetDirectory = assetDirectory ?? throw new ArgumentNullException(nameof(assetDirectory));
            Text = "Season Game Library";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1050, 680);
            MinimumSize = new Size(900, 560);
            BuildUi();
            LoadChoices(selectedTeam);
        }

        private void BuildUi()
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3, Padding = new Padding(10) };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 275));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            Controls.Add(root);

            var seasonBar = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
            seasonBar.Controls.Add(new Label { Text = "Season", AutoSize = true, Padding = new Padding(0, 8, 6, 0) });
            _seasonCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            _seasonCombo.Width = 190;
            _seasonCombo.SelectedIndexChanged += (s, e) => RefreshGames();
            seasonBar.Controls.Add(_seasonCombo);
            root.Controls.Add(seasonBar, 0, 0);
            root.SetColumnSpan(seasonBar, 2);

            var teamPanel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Padding = new Padding(0, 0, 8, 0) };
            teamPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            teamPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var teamButtons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
            AddButton(teamButtons, "League Wide", (s, e) => CheckAllTeams(true));
            AddButton(teamButtons, "Clear", (s, e) => CheckAllTeams(false));
            teamPanel.Controls.Add(teamButtons, 0, 0);
            _teams.Dock = DockStyle.Fill;
            _teams.CheckOnClick = true;
            _teams.ItemCheck += (s, e) => BeginInvoke(new Action(RefreshGames));
            teamPanel.Controls.Add(_teams, 0, 1);
            root.Controls.Add(teamPanel, 0, 1);

            _games.Dock = DockStyle.Fill;
            _games.AllowUserToAddRows = false;
            _games.AllowUserToDeleteRows = false;
            _games.ReadOnly = true;
            _games.RowHeadersVisible = false;
            _games.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _games.MultiSelect = true;
            _games.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _games.Columns.Add("date", "Date");
            _games.Columns.Add("matchup", "Matchup");
            _games.Columns.Add("final", "Final");
            _games.Columns.Add("type", "Game Type");
            _games.Columns[0].FillWeight = 65;
            _games.Columns[1].FillWeight = 170;
            _games.Columns[2].FillWeight = 80;
            _games.Columns[3].FillWeight = 100;
            root.Controls.Add(_games, 1, 1);

            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
            AddButton(actions, "Close", (s, e) => Close());
            AddButton(actions, "Save Both...", (s, e) => SaveBoth());
            AddButton(actions, "Save Game Results...", (s, e) => SaveResults());
            AddButton(actions, "Save Line Ups...", (s, e) => SaveLineups());
            AddButton(actions, "Open Team Library", (s, e) => OpenTeamLibrary());
            root.Controls.Add(actions, 0, 2);
            root.SetColumnSpan(actions, 2);
        }

        private void LoadChoices(Team? selectedTeam)
        {
            foreach (var item in (_league.Seasons ?? new List<Season>()).Select((season, index) => new SeasonItem(season, index + 1)))
                _seasonCombo.Items.Add(item);
            if (_seasonCombo.Items.Count > 0)
                _seasonCombo.SelectedIndex = _seasonCombo.Items.Count - 1;

            foreach (Team team in (_league.Teams ?? new List<Team>()).OrderBy(team => team.DisplayName))
            {
                int index = _teams.Items.Add(new TeamItem(team));
                if (selectedTeam != null && team.Id == selectedTeam.Id)
                    _teams.SetItemChecked(index, true);
            }
            if (_teams.CheckedItems.Count == 0 && _teams.Items.Count > 0)
                _teams.SetItemChecked(0, true);
            RefreshGames();
        }

        private void CheckAllTeams(bool value)
        {
            for (int index = 0; index < _teams.Items.Count; index++)
                _teams.SetItemChecked(index, value);
            RefreshGames();
        }

        private void RefreshGames()
        {
            _games.Rows.Clear();
            Season? season = SelectedSeason();
            var teamIds = SelectedTeams().Select(team => team.Id).ToHashSet();
            if (season == null || teamIds.Count == 0)
                return;

            foreach (GameResult game in (season.Games ?? new List<GameResult>())
                .Where(game => teamIds.Contains(game.AwayTeamId) || teamIds.Contains(game.HomeTeamId))
                .OrderBy(game => game.PlayedAt))
            {
                Team? away = _league.Teams.FirstOrDefault(team => team.Id == game.AwayTeamId);
                Team? home = _league.Teams.FirstOrDefault(team => team.Id == game.HomeTeamId);
                string type = !string.IsNullOrWhiteSpace(game.PlayoffRoundName) ? game.PlayoffRoundName : game.GameType;
                int row = _games.Rows.Add(
                    game.PlayedAt.ToString("yyyy-MM-dd"),
                    (away?.DisplayName ?? "Away") + " at " + (home?.DisplayName ?? "Home"),
                    game.AwayScore + "-" + game.HomeScore,
                    type);
                _games.Rows[row].Tag = game;
            }
            _games.ClearSelection();
        }

        private void SaveLineups()
        {
            if (!TryGetExportSelection(out Season? season, out List<Team> teams, out List<GameResult> games)) return;
            using var dialog = new SaveFileDialog { Filter = "Microsoft Word (*.docx)|*.docx", FileName = "Season Line Up Library.docx" };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            TryExport(() => GameLibraryService.ExportLineups(dialog.FileName, _league, season!, games, teams, _logoPath), "Lineup forms saved.");
        }

        private void SaveResults()
        {
            if (!TryGetExportSelection(out Season? season, out _, out List<GameResult> games)) return;
            using var dialog = new SaveFileDialog { Filter = "Microsoft Word (*.docx)|*.docx", FileName = "Season Game Results.docx" };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            TryExport(() => GameLibraryService.ExportResults(dialog.FileName, _league, season!, games), "Game-result forms saved.");
        }

        private void SaveBoth()
        {
            if (!TryGetExportSelection(out Season? season, out List<Team> teams, out List<GameResult> games)) return;
            using var dialog = new FolderBrowserDialog { Description = "Choose where to save the lineup and game-result libraries." };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            TryExport(() => GameLibraryService.ExportBoth(dialog.SelectedPath, _league, season!, games, teams, _logoPath), "Both form libraries were saved.");
        }

        private bool TryGetExportSelection(out Season? season, out List<Team> teams, out List<GameResult> games)
        {
            season = SelectedSeason();
            teams = SelectedTeams();
            games = SelectedGames();
            if (season == null || teams.Count == 0 || games.Count == 0)
            {
                MessageBox.Show(this, "Select at least one team and one completed game.", "Season Game Library", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }
            return true;
        }

        private void OpenTeamLibrary()
        {
            var teams = SelectedTeams();
            if (teams.Count != 1)
            {
                MessageBox.Show(this, "Select exactly one team to open its library folder.", "Season Game Library", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            string directory = _assetDirectory(teams[0]);
            Directory.CreateDirectory(Path.Combine(directory, GameLibraryService.LineupFolderName));
            Directory.CreateDirectory(Path.Combine(directory, GameLibraryService.ResultsFolderName));
            Process.Start(new ProcessStartInfo { FileName = directory, UseShellExecute = true });
        }

        private void TryExport(Action export, string message)
        {
            try
            {
                export();
                MessageBox.Show(this, message, "Season Game Library", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "The forms could not be saved.\n\n" + ex.Message, "Export failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private Season? SelectedSeason() => (_seasonCombo.SelectedItem as SeasonItem)?.Season;
        private List<Team> SelectedTeams() => _teams.CheckedItems.Cast<TeamItem>().Select(item => item.Team).ToList();
        private List<GameResult> SelectedGames()
        {
            var selected = _games.SelectedRows.Cast<DataGridViewRow>().Select(row => row.Tag as GameResult).Where(game => game != null).Cast<GameResult>().ToList();
            return selected.Count > 0 ? selected : _games.Rows.Cast<DataGridViewRow>().Select(row => row.Tag as GameResult).Where(game => game != null).Cast<GameResult>().ToList();
        }

        private static void AddButton(Control parent, string text, EventHandler click)
        {
            var button = new Button { Text = text, AutoSize = true, Height = 30 };
            button.Click += click;
            parent.Controls.Add(button);
        }

        private sealed class SeasonItem
        {
            public Season Season { get; }
            private int Number { get; }
            public SeasonItem(Season season, int number) { Season = season; Number = number; }
            public override string ToString() => "Season " + Number + " - " + Season.Name;
        }

        private sealed class TeamItem
        {
            public Team Team { get; }
            public TeamItem(Team team) { Team = team; }
            public override string ToString() => Team.DisplayName;
        }
    }
}
