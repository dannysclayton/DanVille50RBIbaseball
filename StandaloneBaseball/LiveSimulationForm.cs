#nullable enable annotations

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace StandaloneBaseball
{
    internal sealed class LiveSimulationForm : Form
    {
        private readonly SimulatedGameEngine.SimulatedGameRun _run;
        private readonly Team _away;
        private readonly Team _home;
        private readonly string _homeLogoPath;
        private readonly System.Windows.Forms.Timer _timer = new System.Windows.Forms.Timer();
        private readonly Label _inningLabel = new Label();
        private readonly Label _scoreLabel = new Label();
        private readonly Label _stateLabel = new Label();
        private readonly ListBox _playByPlay = new ListBox();
        private readonly Button _commitButton = new Button();
        private readonly Button _dismissButton = new Button();
        private readonly PlaylistSoundPlayer _music = new PlaylistSoundPlayer();
        private Image _templateImage;
        private int _eventIndex;

        public LiveSimulationForm(SimulatedGameEngine.SimulatedGameRun run, Team away, Team home, string scoreboardTemplatePath = "", string homeLogoPath = "")
        {
            _run = run ?? throw new ArgumentNullException(nameof(run));
            _away = away;
            _home = home;
            _homeLogoPath = homeLogoPath ?? "";

            Text = "Live Simulation";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(980, 680);
            MinimumSize = new Size(760, 540);

            if (!string.IsNullOrWhiteSpace(scoreboardTemplatePath) && File.Exists(scoreboardTemplatePath))
                _templateImage = Image.FromFile(scoreboardTemplatePath);

            BuildLayout();

            _timer.Interval = 450;
            _timer.Tick += (s, e) => ShowNextEvent();
            Shown += (s, e) =>
            {
                _music.PlayPlaylistLoop(LaunchSoundPlayer.ResolveTeamMusicPlaylist(_home));
                _timer.Start();
            };
            FormClosed += (s, e) =>
            {
                _timer.Stop();
                _music.Dispose();
                _templateImage?.Dispose();
            };
        }

        public GameResult Result => _run.Result;

        public bool CommitRequested { get; private set; }

        private void BuildLayout()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 4,
                ColumnCount = 1,
                Padding = new Padding(12),
                BackColor = Color.FromArgb(16, 24, 32)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 110));
            if (UseHomeScoreboardTemplate())
                root.RowStyles[0] = new RowStyle(SizeType.Absolute, 190);
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            Controls.Add(root);

            var scoreboard = new ScoreboardPanel(this) { Dock = DockStyle.Fill };
            if (!UseHomeScoreboardTemplate())
            {
                scoreboard.Controls.Add(_scoreLabel);
                scoreboard.Controls.Add(_inningLabel);
            }
            root.Controls.Add(scoreboard, 0, 0);

            _scoreLabel.AutoSize = false;
            _scoreLabel.Dock = DockStyle.Top;
            _scoreLabel.Height = 64;
            _scoreLabel.Font = new Font(FontFamily.GenericSansSerif, 28, FontStyle.Bold);
            _scoreLabel.ForeColor = Color.White;
            _scoreLabel.BackColor = Color.Transparent;
            _scoreLabel.TextAlign = ContentAlignment.MiddleCenter;
            _scoreLabel.Text = TeamName(_away) + " 0   -   " + TeamName(_home) + " 0";

            _inningLabel.AutoSize = false;
            _inningLabel.Dock = DockStyle.Bottom;
            _inningLabel.Height = 34;
            _inningLabel.Font = new Font(FontFamily.GenericSansSerif, 13, FontStyle.Bold);
            _inningLabel.ForeColor = Color.FromArgb(255, 218, 94);
            _inningLabel.BackColor = Color.Transparent;
            _inningLabel.TextAlign = ContentAlignment.MiddleCenter;
            _inningLabel.Text = "Top 1";

            _stateLabel.Dock = DockStyle.Fill;
            _stateLabel.Font = new Font(FontFamily.GenericSansSerif, 15, FontStyle.Bold);
            _stateLabel.ForeColor = Color.White;
            _stateLabel.BackColor = Color.FromArgb(26, 38, 48);
            _stateLabel.Padding = new Padding(12);
            _stateLabel.TextAlign = ContentAlignment.MiddleCenter;
            _stateLabel.Text = "Bases empty | 0 outs";
            root.Controls.Add(_stateLabel, 0, 1);

            _playByPlay.Dock = DockStyle.Fill;
            _playByPlay.BackColor = Color.FromArgb(8, 12, 16);
            _playByPlay.ForeColor = Color.White;
            _playByPlay.Font = new Font("Consolas", 12, FontStyle.Regular);
            _playByPlay.BorderStyle = BorderStyle.FixedSingle;
            root.Controls.Add(_playByPlay, 0, 2);

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            _dismissButton.Text = "Dismiss";
            _dismissButton.Width = 110;
            _dismissButton.Height = 34;
            _dismissButton.Click += (s, e) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };
            _commitButton.Text = "Commit Result";
            _commitButton.Width = 130;
            _commitButton.Height = 34;
            _commitButton.Enabled = false;
            _commitButton.Click += (s, e) =>
            {
                CommitRequested = true;
                DialogResult = DialogResult.OK;
                Close();
            };
            buttons.Controls.Add(_dismissButton);
            buttons.Controls.Add(_commitButton);
            root.Controls.Add(buttons, 0, 3);
        }

        private void ShowNextEvent()
        {
            var events = _run.Events;
            if (events == null || _eventIndex >= events.Count)
            {
                _timer.Stop();
                _commitButton.Enabled = true;
                return;
            }

            var item = events[_eventIndex++];
            _scoreLabel.Text = TeamName(_away) + " " + item.AwayScore + "   -   " + TeamName(_home) + " " + item.HomeScore;
            _inningLabel.Text = (item.TopHalf ? "Top " : "Bottom ") + item.Inning;
            _stateLabel.Text = item.Bases + " | " + item.Outs + " out" + (item.Outs == 1 ? "" : "s");
            string prefix = (item.TopHalf ? "T" : "B") + item.Inning + " [" + item.AwayScore + "-" + item.HomeScore + "] ";
            _playByPlay.Items.Add(prefix + item.Narration);
            _playByPlay.TopIndex = Math.Max(0, _playByPlay.Items.Count - 1);
            Invalidate();
        }

        private static string TeamName(Team team)
            => string.IsNullOrWhiteSpace(team?.ScoreboardName) ? team?.DisplayName ?? "Team" : team.ScoreboardName;

        private bool UseHomeScoreboardTemplate()
            => _home?.ScoreboardTemplate?.Enabled == true;

        private sealed class ScoreboardPanel : Panel
        {
            private readonly LiveSimulationForm _owner;

            public ScoreboardPanel(LiveSimulationForm owner)
            {
                _owner = owner;
                DoubleBuffered = true;
                BackColor = Color.FromArgb(13, 41, 77);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                if (_owner.UseHomeScoreboardTemplate())
                {
                    ScoreboardTemplateRenderer.Draw(
                        e.Graphics,
                        ClientRectangle,
                        _owner._home,
                        _owner._homeLogoPath,
                        _owner._scoreLabel.Text,
                        _owner._inningLabel.Text);
                    return;
                }

                if (_owner._templateImage != null)
                {
                    e.Graphics.DrawImage(_owner._templateImage, ClientRectangle);
                    using var overlay = new SolidBrush(Color.FromArgb(135, 0, 0, 0));
                    e.Graphics.FillRectangle(overlay, ClientRectangle);
                    return;
                }

                using var brush = new LinearGradientBrush(ClientRectangle, Color.FromArgb(10, 44, 92), Color.FromArgb(138, 20, 28), 0f);
                e.Graphics.FillRectangle(brush, ClientRectangle);
            }
        }
    }
}
