#nullable enable annotations

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace StandaloneBaseball
{
    internal sealed class PostGameResultDialog : Form
    {
        private readonly Team _away;
        private readonly Team _home;
        private readonly GameResult _result;
        private readonly string? _winnerLogoPath;
        private readonly string _winnerRecordText;
        private readonly bool _canCommit;
        private readonly LaunchSoundPlayer _loopSound = new LaunchSoundPlayer();
        private readonly LaunchSoundPlayer _closeSound = new LaunchSoundPlayer();
        private readonly System.Windows.Forms.Timer _closeTimer = new System.Windows.Forms.Timer();
        private readonly Button _commitButton;
        private readonly Button _dismissButton;
        private readonly DataGridView _lineScoreGrid = new DataGridView();
        private readonly DataGridView _awayBattingGrid = new DataGridView();
        private readonly DataGridView _awayPitchingGrid = new DataGridView();
        private readonly DataGridView _homeBattingGrid = new DataGridView();
        private readonly DataGridView _homePitchingGrid = new DataGridView();
        private readonly TabControl _playerTabs = new TabControl();
        private Image? _winnerLogo;

        public bool CommitRequested { get; private set; }

        public PostGameResultDialog(
            Team away,
            Team home,
            GameResult result,
            string? winnerLogoPath,
            string winnerRecordText,
            string commitDescription,
            bool canCommit)
        {
            _away = away;
            _home = home;
            _result = result;
            _winnerLogoPath = winnerLogoPath;
            _winnerRecordText = winnerRecordText;
            _canCommit = canCommit;

            Text = "Game Results";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(1120, 760);
            MinimumSize = new Size(760, 600);
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = false;
            ControlBox = false;

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 4,
                ColumnCount = 1,
                Padding = new Padding(14),
                BackColor = Color.FromArgb(245, 247, 250)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 166));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
            Controls.Add(root);

            root.Controls.Add(BuildWinnerHeader(), 0, 0);
            root.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = ScoreLine(),
                Font = new Font(Font.FontFamily, 22, FontStyle.Bold),
                ForeColor = Color.FromArgb(17, 24, 39),
                TextAlign = ContentAlignment.MiddleCenter
            }, 0, 1);
            root.Controls.Add(BuildBoxScore(), 0, 2);

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            _dismissButton = AddButton(buttons, "Dismiss", (s, e) => Finish(false));
            _commitButton = AddButton(buttons, _canCommit ? "Commit Result" : "No Season Selected", (s, e) => Finish(true));
            _commitButton.Enabled = _canCommit;
            if (!string.IsNullOrWhiteSpace(commitDescription))
                _commitButton.Text = commitDescription;
            root.Controls.Add(buttons, 0, 3);

            _closeTimer.Tick += (s, e) =>
            {
                _closeTimer.Stop();
                DialogResult = CommitRequested ? DialogResult.OK : DialogResult.Cancel;
                Close();
            };
        }

        private Control BuildWinnerHeader()
        {
            Team? winner = WinnerTeam();
            Color primary = winner == null ? Color.FromArgb(31, 41, 55) : Color.FromArgb(winner.PrimaryArgb);
            Color secondary = winner == null ? Color.FromArgb(75, 85, 99) : Color.FromArgb(winner.SecondaryArgb);
            Color text = ReadableTextColor(primary);

            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = primary,
                Padding = new Padding(14)
            };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 142));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            panel.Controls.Add(BuildLogoBox(winner, secondary), 0, 0);

            var textPanel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3 };
            textPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            textPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            textPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            textPanel.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = winner == null ? "Final" : "Winner",
                ForeColor = Color.FromArgb(220, text),
                Font = new Font(Font.FontFamily, 12, FontStyle.Bold),
                TextAlign = ContentAlignment.BottomLeft
            }, 0, 0);
            textPanel.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = winner?.DisplayName ?? "Tie Game",
                ForeColor = text,
                Font = new Font(Font.FontFamily, 23, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 1);
            textPanel.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = winner == null
                    ? "No winner was recorded."
                    : "Updated record: " + (string.IsNullOrWhiteSpace(_winnerRecordText) ? "N/A" : _winnerRecordText),
                ForeColor = Color.FromArgb(230, text),
                Font = new Font(Font.FontFamily, 13, FontStyle.Regular),
                TextAlign = ContentAlignment.TopLeft
            }, 0, 2);
            panel.Controls.Add(textPanel, 1, 0);

            return panel;
        }

        private Control BuildLogoBox(Team? winner, Color fallback)
        {
            _winnerLogo = LoadImage(_winnerLogoPath);
            if (_winnerLogo != null)
            {
                return new PictureBox
                {
                    Dock = DockStyle.Fill,
                    Image = _winnerLogo,
                    SizeMode = PictureBoxSizeMode.Zoom,
                    BackColor = Color.FromArgb(20, 24, 32),
                    BorderStyle = BorderStyle.FixedSingle
                };
            }

            return new Label
            {
                Dock = DockStyle.Fill,
                Text = winner?.ScoreboardName ?? "WIN",
                BackColor = fallback,
                ForeColor = ReadableTextColor(fallback),
                Font = new Font(Font.FontFamily, 28, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                BorderStyle = BorderStyle.FixedSingle
            };
        }

        private Control BuildBoxScore()
        {
            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Margin = Padding.Empty
            };
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            ConfigureLineScoreGrid();
            bool tied = _result.AwayScore == _result.HomeScore;
            AddBoxScoreRow(_lineScoreGrid, _away, _result.AwayScore, tied ? "Tie" : _result.AwayScore > _result.HomeScore ? "Win" : "Loss");
            AddBoxScoreRow(_lineScoreGrid, _home, _result.HomeScore, tied ? "Tie" : _result.HomeScore > _result.AwayScore ? "Win" : "Loss");
            panel.Controls.Add(_lineScoreGrid, 0, 0);

            ConfigureBattingGrid(_awayBattingGrid);
            ConfigurePitchingGrid(_awayPitchingGrid);
            ConfigureBattingGrid(_homeBattingGrid);
            ConfigurePitchingGrid(_homePitchingGrid);
            AddBattingRows(_awayBattingGrid, _away, _result.AwayStartingLineup);
            AddPitchingRows(_awayPitchingGrid, _away, _result.AwayStartingLineup);
            AddBattingRows(_homeBattingGrid, _home, _result.HomeStartingLineup);
            AddPitchingRows(_homePitchingGrid, _home, _result.HomeStartingLineup);

            _playerTabs.Dock = DockStyle.Fill;
            _playerTabs.Multiline = false;
            AddPlayerTab(_away.ScoreboardName + " Batting", _awayBattingGrid, "No batting lines recorded for " + _away.DisplayName + ".");
            AddPlayerTab(_away.ScoreboardName + " Pitching", _awayPitchingGrid, "No pitching lines recorded for " + _away.DisplayName + ".");
            AddPlayerTab(_home.ScoreboardName + " Batting", _homeBattingGrid, "No batting lines recorded for " + _home.DisplayName + ".");
            AddPlayerTab(_home.ScoreboardName + " Pitching", _homePitchingGrid, "No pitching lines recorded for " + _home.DisplayName + ".");
            panel.Controls.Add(_playerTabs, 0, 1);
            return panel;
        }

        private void ConfigureLineScoreGrid()
        {
            ConfigureReadOnlyGrid(_lineScoreGrid);
            _lineScoreGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            AddGridColumn(_lineScoreGrid, "team", "Team", 150, typeof(string));
            AddGridColumn(_lineScoreGrid, "line", "Line", 150, typeof(string));
            AddGridColumn(_lineScoreGrid, "runs", "R", 48, typeof(int));
            AddGridColumn(_lineScoreGrid, "hits", "H", 48, typeof(int));
            AddGridColumn(_lineScoreGrid, "errors", "E", 48, typeof(int));
            AddGridColumn(_lineScoreGrid, "lob", "LOB", 52, typeof(int));
            AddGridColumn(_lineScoreGrid, "wildPitches", "WP", 52, typeof(int));
            AddGridColumn(_lineScoreGrid, "passedBalls", "PB", 52, typeof(int));
            AddGridColumn(_lineScoreGrid, "balks", "BK", 52, typeof(int));
            AddGridColumn(_lineScoreGrid, "doublePlays", "DP", 52, typeof(int));
            AddGridColumn(_lineScoreGrid, "result", "Result", 72, typeof(string));
            AddGridColumn(_lineScoreGrid, "record", "Projected Record", 120, typeof(string));
        }

        private static void ConfigureBattingGrid(DataGridView grid)
        {
            ConfigureReadOnlyGrid(grid);
            AddPlayerColumn(grid, "player", "Player");
            AddGridColumn(grid, "position", "POS", 58, typeof(string));
            AddGridColumn(grid, "plateAppearances", "PA", 52, typeof(int));
            AddGridColumn(grid, "atBats", "AB", 52, typeof(int));
            AddGridColumn(grid, "runs", "R", 48, typeof(int));
            AddGridColumn(grid, "hits", "H", 48, typeof(int));
            AddGridColumn(grid, "doubles", "2B", 48, typeof(int));
            AddGridColumn(grid, "triples", "3B", 48, typeof(int));
            AddGridColumn(grid, "homeRuns", "HR", 52, typeof(int));
            AddGridColumn(grid, "runsBattedIn", "RBI", 54, typeof(int));
            AddGridColumn(grid, "walks", "BB", 48, typeof(int));
            AddGridColumn(grid, "intentionalWalks", "IBB", 54, typeof(int));
            AddGridColumn(grid, "strikeouts", "SO", 48, typeof(int));
            AddGridColumn(grid, "stolenBases", "SB", 48, typeof(int));
            AddGridColumn(grid, "caughtStealing", "CS", 48, typeof(int));
            AddGridColumn(grid, "hitByPitch", "HBP", 54, typeof(int));
            AddGridColumn(grid, "sacrificeHits", "SH", 48, typeof(int));
            AddGridColumn(grid, "sacrificeFlies", "SF", 48, typeof(int));
            AddGridColumn(grid, "flyOuts", "FO", 48, typeof(int));
            AddGridColumn(grid, "groundOuts", "GO", 48, typeof(int));
            AddGridColumn(grid, "popOuts", "PO", 48, typeof(int));
            AddGridColumn(grid, "groundedIntoDoublePlays", "GIDP", 58, typeof(int));
            AddGridColumn(grid, "reachedOnError", "ROE", 54, typeof(int));
        }

        private static void ConfigurePitchingGrid(DataGridView grid)
        {
            ConfigureReadOnlyGrid(grid);
            AddPlayerColumn(grid, "pitcher", "Pitcher");
            AddGridColumn(grid, "role", "Role", 58, typeof(string));
            AddGridColumn(grid, "inningsPitched", "IP", 52, typeof(decimal), "0.0");
            AddGridColumn(grid, "hitsAllowed", "H", 48, typeof(int));
            AddGridColumn(grid, "runsAllowed", "R", 48, typeof(int));
            AddGridColumn(grid, "earnedRuns", "ER", 48, typeof(int));
            AddGridColumn(grid, "doublesAllowed", "2B", 48, typeof(int));
            AddGridColumn(grid, "triplesAllowed", "3B", 48, typeof(int));
            AddGridColumn(grid, "homeRunsAllowed", "HR", 52, typeof(int));
            AddGridColumn(grid, "walksAllowed", "BB", 48, typeof(int));
            AddGridColumn(grid, "intentionalWalksAllowed", "IBB", 54, typeof(int));
            AddGridColumn(grid, "strikeouts", "K", 48, typeof(int));
            AddGridColumn(grid, "hitBatters", "HBP", 54, typeof(int));
            AddGridColumn(grid, "wildPitches", "WP", 52, typeof(int));
            AddGridColumn(grid, "balks", "BK", 48, typeof(int));
            AddGridColumn(grid, "battersFaced", "BF", 48, typeof(int));
            AddGridColumn(grid, "pitchCount", "PC", 52, typeof(int));
            AddGridColumn(grid, "wins", "W", 44, typeof(int));
            AddGridColumn(grid, "losses", "L", 44, typeof(int));
            AddGridColumn(grid, "saves", "S", 44, typeof(int));
            AddGridColumn(grid, "holds", "HLD", 52, typeof(int));
            AddGridColumn(grid, "blownSaves", "BS", 48, typeof(int));
            AddGridColumn(grid, "completeGames", "CG", 48, typeof(int));
            AddGridColumn(grid, "shutouts", "SHO", 54, typeof(int));
        }

        private static void ConfigureReadOnlyGrid(DataGridView grid)
        {
            grid.Dock = DockStyle.Fill;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AllowUserToOrderColumns = true;
            grid.AllowUserToResizeColumns = true;
            grid.AllowUserToResizeRows = false;
            grid.ReadOnly = true;
            grid.RowHeadersVisible = false;
            grid.AutoGenerateColumns = false;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.MultiSelect = false;
            grid.ScrollBars = ScrollBars.Both;
            grid.BackgroundColor = Color.White;
            grid.BorderStyle = BorderStyle.FixedSingle;
            grid.ColumnHeadersHeight = 30;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.RowTemplate.Height = 26;
            grid.ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableAlwaysIncludeHeaderText;
        }

        private static void AddPlayerColumn(DataGridView grid, string name, string header)
        {
            var column = AddGridColumn(grid, name, header, 190, typeof(string));
            column.Frozen = true;
            column.MinimumWidth = 120;
            column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
        }

        private static DataGridViewTextBoxColumn AddGridColumn(
            DataGridView grid,
            string name,
            string header,
            int width,
            Type valueType,
            string? format = null)
        {
            var column = new DataGridViewTextBoxColumn
            {
                Name = name,
                HeaderText = header,
                Width = width,
                FillWeight = width,
                MinimumWidth = Math.Min(width, 40),
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.Automatic,
                ValueType = valueType
            };
            column.DefaultCellStyle.Alignment = valueType == typeof(string)
                ? DataGridViewContentAlignment.MiddleLeft
                : DataGridViewContentAlignment.MiddleRight;
            if (!string.IsNullOrWhiteSpace(format))
                column.DefaultCellStyle.Format = format;
            grid.Columns.Add(column);
            return column;
        }

        private void AddPlayerTab(string title, DataGridView grid, string emptyText)
        {
            bool empty = grid.Rows.Count == 0;
            var page = new TabPage(title) { Padding = new Padding(6) };
            var host = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Margin = Padding.Empty
            };
            host.RowStyles.Add(new RowStyle(SizeType.Absolute, empty ? 28 : 0));
            host.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            host.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = empty ? emptyText : "",
                ForeColor = Color.FromArgb(75, 85, 99),
                TextAlign = ContentAlignment.MiddleLeft,
                Visible = empty
            }, 0, 0);
            host.Controls.Add(grid, 0, 1);
            page.Controls.Add(host);
            _playerTabs.TabPages.Add(page);
        }

        private void AddBattingRows(DataGridView grid, Team team, IEnumerable<GameLineupEntry>? lineup)
        {
            var entries = (lineup ?? Enumerable.Empty<GameLineupEntry>()).Where(entry => entry != null).ToList();
            var lines = TeamLines(team.Id)
                .Where(line => !line.Pitcher || HasBattingStats(line))
                .OrderBy(line => BattingOrder(line.PlayerId, entries))
                .ThenBy(line => AppearanceOrder(line.PlayerId, entries))
                .ThenBy(line => line.PlayerName, StringComparer.CurrentCultureIgnoreCase);

            foreach (PlayerGameLine line in lines)
            {
                grid.Rows.Add(
                    PlayerName(line), Position(line, entries), line.PlateAppearances, line.AB, line.R, line.H,
                    line.Doubles, line.Triples, line.HR, line.RBI, line.BB, line.IBB, line.SO, line.SB, line.CS,
                    line.HBP, line.SH, line.SF, line.FlyOuts, line.GroundOuts, line.PopOuts,
                    line.GroundedIntoDoublePlays, line.ReachedOnError);
            }
        }

        private void AddPitchingRows(DataGridView grid, Team team, IEnumerable<GameLineupEntry>? lineup)
        {
            var entries = (lineup ?? Enumerable.Empty<GameLineupEntry>()).Where(entry => entry != null).ToList();
            Dictionary<Guid, int> historyOrder = PitcherHistoryOrder(entries);
            var lines = TeamLines(team.Id)
                .Where(line => line.Pitcher || HasPitchingStats(line))
                .OrderBy(line => historyOrder.ContainsKey(line.PlayerId) ? 0 : 1)
                .ThenBy(line => historyOrder.TryGetValue(line.PlayerId, out int order) ? order : int.MaxValue)
                .ThenByDescending(line => line.StartingPitcher)
                .ThenBy(line => AppearanceOrder(line.PlayerId, entries))
                .ThenBy(line => line.PlayerName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(line => line.PlayerId);

            foreach (PlayerGameLine line in lines)
            {
                grid.Rows.Add(
                    PlayerName(line), line.StartingPitcher ? "SP" : "RP", InningsPitched(line.IPOuts),
                    line.HitsAllowed, line.RunsAllowed, line.ER, line.DoublesAllowed, line.TriplesAllowed,
                    line.HomeRunsAllowed, line.WalksAllowed, line.IntentionalWalksAllowed, line.K,
                    line.HitBatters, line.WildPitches, line.Balks, line.BattersFaced, line.PitchCount,
                    line.Wins, line.Losses, line.Saves, line.Holds, line.BlownSaves, line.CompleteGames,
                    line.Shutouts);
            }
        }

        private IEnumerable<PlayerGameLine> TeamLines(Guid teamId)
            => (_result.Lines ?? new List<PlayerGameLine>())
                .Where(line => line != null && line.TeamId == teamId);

        private static bool HasBattingStats(PlayerGameLine line)
            => line.PlateAppearances > 0 || line.R > 0 || line.H > 0 || line.SB > 0 || line.CS > 0 ||
               line.FlyOuts > 0 || line.GroundOuts > 0 || line.PopOuts > 0 || line.GroundedIntoDoublePlays > 0 ||
               line.ReachedOnError > 0;

        private static bool HasPitchingStats(PlayerGameLine line)
            => line.IPOuts > 0 || line.ER > 0 || line.RunsAllowed > 0 || line.K > 0 || line.HitsAllowed > 0 ||
               line.WalksAllowed > 0 || line.HitBatters > 0 || line.WildPitches > 0 || line.Balks > 0 ||
               line.BattersFaced > 0 || line.PitchCount > 0 || line.Wins > 0 || line.Losses > 0 ||
               line.Saves > 0 || line.Holds > 0 || line.BlownSaves > 0 || line.CompleteGames > 0 || line.Shutouts > 0;

        private static int BattingOrder(Guid playerId, IEnumerable<GameLineupEntry> lineup)
        {
            int value = lineup.Where(entry => entry.PlayerId == playerId && entry.BattingOrder > 0)
                .Select(entry => entry.BattingOrder)
                .DefaultIfEmpty(int.MaxValue)
                .Min();
            return value;
        }

        private static int AppearanceOrder(Guid playerId, IEnumerable<GameLineupEntry> lineup)
            => lineup.Where(entry => entry.PlayerId == playerId && entry.AppearanceOrder > 0)
                .Select(entry => entry.AppearanceOrder)
                .DefaultIfEmpty(int.MaxValue)
                .Min();

        private static string Position(PlayerGameLine line, IEnumerable<GameLineupEntry> lineup)
        {
            List<GameLineupEntry> entries = lineup.Where(item => item.PlayerId == line.PlayerId).ToList();
            List<string> positions = PositionEvents(entries)
                .OrderBy(item => item.Inning)
                .ThenBy(item => item.Half)
                .ThenBy(item => item.AppearanceOrder)
                .ThenBy(item => item.EntryIndex)
                .ThenBy(item => item.HistoryIndex)
                .Select(item => item.Position)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (positions.Count == 0)
            {
                positions = entries
                    .OrderBy(item => item.AppearanceOrder <= 0 ? int.MaxValue : item.AppearanceOrder)
                    .Select(item => item.DesignatedHitter ? "DH" : item.DefensivePosition?.Trim() ?? "")
                    .Where(position => !string.IsNullOrWhiteSpace(position))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            return positions.Count > 0 ? string.Join("/", positions) : line.Pitcher ? "P" : "";
        }

        private static Dictionary<Guid, int> PitcherHistoryOrder(IEnumerable<GameLineupEntry> lineup)
            => PositionEvents(lineup)
                .Where(item => IsPitcherPosition(item.Position) && item.PlayerId != Guid.Empty)
                .OrderBy(item => item.Inning)
                .ThenBy(item => item.Half)
                .ThenBy(item => item.AppearanceOrder)
                .ThenBy(item => item.EntryIndex)
                .ThenBy(item => item.HistoryIndex)
                .Select(item => item.PlayerId)
                .Distinct()
                .Select((playerId, order) => new { playerId, order })
                .ToDictionary(item => item.playerId, item => item.order);

        private static IEnumerable<(Guid PlayerId, int Inning, int Half, int AppearanceOrder, int EntryIndex, int HistoryIndex, string Position)>
            PositionEvents(IEnumerable<GameLineupEntry> lineup)
        {
            int entryIndex = 0;
            foreach (GameLineupEntry entry in lineup)
            {
                int historyIndex = 0;
                foreach (GamePositionChange? change in entry.PositionHistory ?? new List<GamePositionChange>())
                {
                    if (change != null)
                    {
                        string position = change.Position?.Trim() ?? "";
                        if (!string.IsNullOrWhiteSpace(position))
                        {
                            yield return (
                                entry.PlayerId,
                                Math.Max(1, change.Inning),
                                change.Half == HalfInning.Bottom ? 1 : 0,
                                entry.AppearanceOrder <= 0 ? int.MaxValue : entry.AppearanceOrder,
                                entryIndex,
                                historyIndex,
                                position);
                        }
                    }
                    historyIndex++;
                }
                entryIndex++;
            }
        }

        private static bool IsPitcherPosition(string position)
            => string.Equals(position, "P", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(position, "SP", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(position, "RP", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(position, "CL", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(position, "CP", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(position, "LR", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(position, "MR", StringComparison.OrdinalIgnoreCase);

        private static string PlayerName(PlayerGameLine line)
            => string.IsNullOrWhiteSpace(line.PlayerName) ? "Unknown player" : line.PlayerName;

        private static decimal InningsPitched(int outs)
        {
            int safeOuts = Math.Max(0, outs);
            return safeOuts / 3 + (safeOuts % 3) / 10m;
        }

        private void AddBoxScoreRow(DataGridView grid, Team team, int runs, string result)
        {
            var lines = TeamLines(team.Id).ToList();
            grid.Rows.Add(
                team.DisplayName,
                LineScoreText(team.Id),
                runs,
                TeamHits(team.Id, lines),
                TeamErrors(team.Id, lines),
                TeamLeftOnBase(team.Id),
                lines.Sum(line => line.WildPitches),
                lines.Sum(line => line.PassedBalls),
                lines.Sum(line => line.Balks),
                lines.Sum(line => line.TeamDoublePlaysTurned),
                result,
                TeamRecordText(team));
        }

        private string LineScoreText(Guid teamId)
        {
            var values = teamId == _result.AwayTeamId
                ? _result.AwayRunsByInning
                : teamId == _result.HomeTeamId
                    ? _result.HomeRunsByInning
                    : null;
            return values == null || values.Count == 0 ? "-" : string.Join(" ", values);
        }

        private int TeamHits(Guid teamId, System.Collections.Generic.List<PlayerGameLine> lines)
        {
            if (teamId == _result.AwayTeamId && _result.AwayHits > 0)
                return _result.AwayHits;
            if (teamId == _result.HomeTeamId && _result.HomeHits > 0)
                return _result.HomeHits;
            return lines.Sum(line => line.H);
        }

        private int TeamErrors(Guid teamId, System.Collections.Generic.List<PlayerGameLine> lines)
        {
            if (teamId == _result.AwayTeamId && _result.AwayErrors > 0)
                return _result.AwayErrors;
            if (teamId == _result.HomeTeamId && _result.HomeErrors > 0)
                return _result.HomeErrors;
            return lines.Sum(line => line.Errors);
        }

        private int TeamLeftOnBase(Guid teamId)
        {
            if (teamId == _result.AwayTeamId)
                return _result.AwayLeftOnBase;
            if (teamId == _result.HomeTeamId)
                return _result.HomeLeftOnBase;
            return 0;
        }

        private string ScoreLine()
            => (_away?.ScoreboardName ?? "AWAY") + " " + _result.AwayScore + "  @  " +
               (_home?.ScoreboardName ?? "HOME") + " " + _result.HomeScore;

        private Team? WinnerTeam()
            => _result.AwayScore == _result.HomeScore
                ? null
                : _result.AwayScore > _result.HomeScore ? _away : _home;

        private string TeamRecordText(Team? team)
        {
            if (team == null || string.IsNullOrWhiteSpace(_winnerRecordText))
                return "";
            return WinnerTeam()?.Id == team.Id ? _winnerRecordText : "";
        }

        private static Button AddButton(Control host, string text, EventHandler click)
        {
            var button = new Button { Text = text, AutoSize = true, Margin = new Padding(6), Height = 34 };
            button.Click += click;
            host.Controls.Add(button);
            return button;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _loopSound.PlayLoop(LaunchSoundPlayer.FindPostGameLoop());
        }

        private void Finish(bool commit)
        {
            if (commit && !_canCommit)
                return;

            CommitRequested = commit;
            _commitButton.Enabled = false;
            _dismissButton.Enabled = false;
            _loopSound.Stop();
            string closePath = LaunchSoundPlayer.FindThatsTheBallGame();
            _closeSound.PlayOnce(closePath);
            _closeTimer.Interval = Math.Clamp(LaunchSoundPlayer.GetDurationMilliseconds(closePath, 900) + 80, 250, 5000);
            _closeTimer.Start();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _closeTimer?.Stop();
                _closeTimer?.Dispose();
                _loopSound.Dispose();
                _closeSound.Dispose();
                _winnerLogo?.Dispose();
            }

            base.Dispose(disposing);
        }

        private static Image? LoadImage(string? path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return null;

            try
            {
                using var source = Image.FromFile(path);
                return new Bitmap(source);
            }
            catch
            {
                return null;
            }
        }

        private static Color ReadableTextColor(Color background)
        {
            int brightness = (background.R * 299 + background.G * 587 + background.B * 114) / 1000;
            return brightness >= 145 ? Color.FromArgb(20, 24, 32) : Color.White;
        }
    }
}
