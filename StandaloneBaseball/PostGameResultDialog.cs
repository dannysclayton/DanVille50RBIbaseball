#nullable enable annotations

using System;
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
            ClientSize = new Size(760, 560);
            MinimumSize = new Size(680, 500);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ControlBox = false;

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 4,
                ColumnCount = 1,
                Padding = new Padding(18),
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
            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect
            };
            grid.Columns.Add("team", "Team");
            grid.Columns.Add("line", "Line");
            grid.Columns.Add("runs", "R");
            grid.Columns.Add("hits", "H");
            grid.Columns.Add("errors", "E");
            grid.Columns.Add("lob", "LOB");
            grid.Columns.Add("wildPitches", "WP");
            grid.Columns.Add("passedBalls", "PB");
            grid.Columns.Add("balks", "BK");
            grid.Columns.Add("doublePlays", "DP");
            grid.Columns.Add("result", "Result");
            grid.Columns.Add("record", "Projected Record");
            bool tied = _result.AwayScore == _result.HomeScore;
            AddBoxScoreRow(grid, _away, _result.AwayScore, tied ? "Tie" : _result.AwayScore > _result.HomeScore ? "Win" : "Loss");
            AddBoxScoreRow(grid, _home, _result.HomeScore, tied ? "Tie" : _result.HomeScore > _result.AwayScore ? "Win" : "Loss");
            return grid;
        }

        private void AddBoxScoreRow(DataGridView grid, Team team, int runs, string result)
        {
            var lines = (_result.Lines ?? new System.Collections.Generic.List<PlayerGameLine>())
                .Where(line => line.TeamId == team?.Id)
                .ToList();
            grid.Rows.Add(
                team?.DisplayName ?? "Team",
                LineScoreText(team?.Id ?? Guid.Empty),
                runs,
                TeamHits(team?.Id ?? Guid.Empty, lines),
                TeamErrors(team?.Id ?? Guid.Empty, lines),
                TeamLeftOnBase(team?.Id ?? Guid.Empty),
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
