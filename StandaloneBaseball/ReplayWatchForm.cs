using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

#nullable enable annotations

namespace StandaloneBaseball
{
    public sealed class ReplayWatchForm : Form
    {
        private readonly ReplayFile _replay;
        private readonly Team _awayTeam;
        private readonly Team _homeTeam;
        private readonly Dictionary<string, Player> _playersByReplayId = new Dictionary<string, Player>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Player> _playersBySourceId = new Dictionary<string, Player>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Guid> _idsByReplayKey = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        private readonly System.Windows.Forms.Timer _timer;
        private readonly GameplayForm _gameplay;
        private readonly Label _stateLabel;
        private readonly Label _scoreLabel;
        private readonly Label _basesLabel;
        private readonly TextBox _playText;
        private readonly Button _playButton;
        private readonly Button _previousInningButton;
        private readonly Button _previousEventButton;
        private readonly Button _nextEventButton;
        private readonly Button _nextInningButton;
        private readonly ComboBox _speedCombo;
        private readonly Stopwatch _playbackClock = new Stopwatch();
        private readonly List<LaunchSoundPlayer> _replayAudioPlayers = new List<LaunchSoundPlayer>();
        private readonly HashSet<string> _playedCueKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private int _index = -1;
        private ReplayEvent? _activeExactEvent;
        private int _nextExactEventIndex;
        private long _replayTimeMs;
        private long _lastClockMilliseconds;
        private long _lastExactFrameTimeMs;
        private long _playbackScaleRemainder;
        private ReplayPlaybackSpeed _playbackSpeed = ReplayWatchNavigation.NormalSpeed;

        public ReplayWatchForm(
            ReplayFile replay,
            Team? currentAwayTeam = null,
            Team? currentHomeTeam = null,
            string currentAwayLogoPath = "",
            string currentHomeLogoPath = "")
        {
            _replay = replay ?? new ReplayFile();
            NormalizeReplay();
            _awayTeam = BuildTeam(_replay.Teams.Away, away: true);
            _homeTeam = BuildTeam(_replay.Teams.Home, away: false);
            ReplayScoreboardPresentation.Apply(_replay, _replay.Teams.Away, _awayTeam, currentAwayTeam, homeTeam: false);
            ReplayScoreboardPresentation.Apply(_replay, _replay.Teams.Home, _homeTeam, currentHomeTeam, homeTeam: true);
            string awayLogoPath = ReplayScoreboardPresentation.ResolveLogo(_replay, _replay.Teams.Away, currentAwayLogoPath);
            string homeLogoPath = ReplayScoreboardPresentation.ResolveLogo(_replay, _replay.Teams.Home, currentHomeLogoPath);

            Text = "Dan's RBI Baseball 2026 Replay - " + _replay.PlaybackQuality;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(1120, 780);
            MinimumSize = new Size(900, 640);

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 4,
                ColumnCount = 1,
                Padding = new Padding(10)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 68));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 32));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            Controls.Add(root);

            var state = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
            state.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
            state.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
            state.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
            _stateLabel = ReplayLabel("Ready");
            _scoreLabel = ReplayLabel(ScoreText(new ReplayScore()));
            _basesLabel = ReplayLabel("Bases empty");
            state.Controls.Add(_stateLabel, 0, 0);
            state.Controls.Add(_scoreLabel, 1, 0);
            state.Controls.Add(_basesLabel, 2, 0);
            root.Controls.Add(state, 0, 0);

            _gameplay = new GameplayForm(_awayTeam, _homeTeam);
            _gameplay.SetPregameLineupLogos(awayLogoPath, homeLogoPath);
            _gameplay.SetReplayPlaybackMode();
            _gameplay.TopLevel = false;
            _gameplay.FormBorderStyle = FormBorderStyle.None;
            _gameplay.Dock = DockStyle.Fill;
            root.Controls.Add(_gameplay, 0, 1);
            _gameplay.Show();
            ApplyReplayField();

            _playText = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 10.5f),
                BackColor = Color.FromArgb(18, 24, 28),
                ForeColor = Color.White
            };
            root.Controls.Add(_playText, 0, 2);

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };
            buttons.Controls.Add(new Label
            {
                Text = "Speed",
                AutoSize = true,
                Margin = new Padding(3, 9, 3, 0)
            });
            _speedCombo = new ComboBox
            {
                Name = "ReplaySpeedComboBox",
                AccessibleName = "Replay playback speed",
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 68,
                TabIndex = 0
            };
            foreach (ReplayPlaybackSpeed speed in ReplayWatchNavigation.PlaybackSpeeds)
                _speedCombo.Items.Add(speed);
            _speedCombo.SelectedItem = _playbackSpeed;
            _speedCombo.SelectedIndexChanged += (s, e) => ChangePlaybackSpeed();
            buttons.Controls.Add(_speedCombo);

            _previousInningButton = ReplayButton("ReplayPreviousInningButton", "Previous Inning", "Jump to previous inning", 1, JumpToPreviousInning);
            _previousEventButton = ReplayButton("ReplayPreviousEventButton", "Previous Event", "Jump to previous event", 2, JumpToPreviousEvent);
            _nextEventButton = ReplayButton("ReplayNextEventButton", "Next Event", "Jump to next event", 3, JumpToNextEvent);
            _nextInningButton = ReplayButton("ReplayNextInningButton", "Next Inning", "Jump to next inning", 4, JumpToNextInning);
            _playButton = new Button
            {
                Name = "ReplayPlayButton",
                AccessibleName = "Play or pause replay",
                Text = "Play",
                AutoSize = true,
                TabIndex = 5
            };
            _playButton.Click += (s, e) => TogglePlay();
            var step = new Button { Text = "Step", AutoSize = true, TabIndex = 6, AccessibleName = "Step replay" };
            step.Click += (s, e) => Step();
            var reset = new Button { Text = "Reset", AutoSize = true, TabIndex = 7, AccessibleName = "Reset replay" };
            reset.Click += (s, e) => ResetReplay();
            var close = new Button { Text = "Close", AutoSize = true, TabIndex = 8, AccessibleName = "Close replay" };
            close.Click += (s, e) => Close();
            buttons.Controls.Add(_previousInningButton);
            buttons.Controls.Add(_previousEventButton);
            buttons.Controls.Add(_nextEventButton);
            buttons.Controls.Add(_nextInningButton);
            buttons.Controls.Add(_playButton);
            buttons.Controls.Add(step);
            buttons.Controls.Add(reset);
            buttons.Controls.Add(close);
            root.Controls.Add(buttons, 0, 3);

            _timer = new System.Windows.Forms.Timer
            {
                Interval = _replay.UsesTimedPlayback ? 15 : ReplayWatchNavigation.SnapshotInterval(_playbackSpeed)
            };
            _timer.Tick += (s, e) => PlaybackTick();

            ResetReplay();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _timer?.Dispose();
                foreach (LaunchSoundPlayer player in _replayAudioPlayers)
                    player.Dispose();
                _replayAudioPlayers.Clear();
                _gameplay?.Dispose();
            }

            base.Dispose(disposing);
        }

        private void NormalizeReplay()
        {
            _replay.Teams ??= new ReplayTeams();
            _replay.Teams.Away ??= new ReplayTeam();
            _replay.Teams.Home ??= new ReplayTeam();
            _replay.Game ??= new ReplayGameInfo();
            _replay.Rules ??= new ReplayRules();
            _replay.Assets ??= new ReplayAssets();
            _replay.Game.FinalScore ??= new ReplayScore();
            _replay.Game.LineScore ??= new ReplayLineScore();
            _replay.Events ??= new List<ReplayEvent>();
            _replay.PlayLog ??= new List<string>();
            _replay.DetailedPlayLog ??= new List<string>();
            _replay.ReplayIssues ??= new List<string>();
            _replay.Teams.Away.Lineup ??= new List<ReplayLineupSlot>();
            _replay.Teams.Home.Lineup ??= new List<ReplayLineupSlot>();
            _replay.Teams.Away.Bench ??= new List<ReplayPlayer>();
            _replay.Teams.Home.Bench ??= new List<ReplayPlayer>();
            _replay.Teams.Away.PitchingStaff ??= new List<ReplayPlayer>();
            _replay.Teams.Home.PitchingStaff ??= new List<ReplayPlayer>();
            foreach (var replayEvent in _replay.Events)
            {
                if (replayEvent == null)
                    continue;
                replayEvent.Score ??= new ReplayScore();
                replayEvent.Bases ??= new ReplayBases();
                replayEvent.RunnersAdvanced ??= new List<string>();
                replayEvent.Audio ??= new List<ReplayAudioCue>();
                replayEvent.Cutscenes ??= new List<ReplayCutsceneCue>();
                replayEvent.RunnerAdvancements ??= new List<ReplayRunnerAdvancement>();
            }
        }

        private void ApplyReplayField()
        {
            string background = ReplayStore.ResolveReplayPath(_replay, _replay.Assets?.StadiumBackground);
            BaseballFieldPreset source = BaseballFieldPresets.Find(_replay.Game?.StadiumId);
            if (string.IsNullOrWhiteSpace(background))
            {
                _gameplay.SetFieldPreset(source);
                return;
            }

            _gameplay.SetFieldPreset(new BaseballFieldPreset
            {
                Id = string.IsNullOrWhiteSpace(_replay.Game?.StadiumId) ? "exact-replay-field" : _replay.Game.StadiumId,
                Name = string.IsNullOrWhiteSpace(_replay.Game?.StadiumName) ? "Exact Replay Field" : _replay.Game.StadiumName,
                TeamLabel = source.TeamLabel,
                OpenedYear = source.OpenedYear,
                GrassColor = source.GrassColor,
                DarkGrassColor = source.DarkGrassColor,
                InfieldColor = source.InfieldColor,
                ClayColor = source.ClayColor,
                WallColor = source.WallColor,
                SeatColor = source.SeatColor,
                StructureColor = source.StructureColor,
                AccentColor = source.AccentColor,
                BackgroundAssetPath = background,
                Variant = source.Variant,
                FenceTopOffset = source.FenceTopOffset,
                FenceStartAngle = source.FenceStartAngle,
                FenceSweepAngle = source.FenceSweepAngle,
                UserCreated = true
            });
        }

        private Team BuildTeam(ReplayTeam replayTeam, bool away)
        {
            var team = new Team
            {
                Id = ReplayGuid("team", replayTeam.TeamId, away ? "away" : "home"),
                City = TeamName(replayTeam.TeamName, away ? "Away" : "Home"),
                Nickname = TeamName(replayTeam.Mascot, "Team"),
                ScoreboardAbbreviation = LimitScoreboardName(replayTeam.ScoreboardAbbreviation, away ? "AWAY" : "HOME"),
                PrimaryArgb = ParseColor(replayTeam.PrimaryColor, away ? Color.FromArgb(40, 90, 180) : Color.FromArgb(200, 60, 55)).ToArgb(),
                SecondaryArgb = ParseColor(replayTeam.SecondaryColor, Color.White).ToArgb(),
                BaseLineup = new TeamBaseLineup(),
                PitchingPlan = new TeamPitchingPlan()
            };
            team.EnsureDefaultUniformSets();

            foreach (var player in ReplayPlayersForTeam(replayTeam).Where(p => p != null))
                EnsurePlayer(team, player);

            FillReplayRoster(team);
            BuildReplayLineup(team, replayTeam);
            team.NormalizeText();
            PitchingRotationEngine.NormalizePitchingPlan(team);
            return team;
        }

        private IEnumerable<ReplayPlayer> ReplayPlayersForTeam(ReplayTeam team)
        {
            foreach (var slot in team.Lineup ?? new List<ReplayLineupSlot>())
            {
                if (slot?.Player != null)
                    yield return slot.Player;
            }

            foreach (ReplayPlayer player in team.Bench ?? new List<ReplayPlayer>())
                if (player != null) yield return player;
            foreach (ReplayPlayer player in team.PitchingStaff ?? new List<ReplayPlayer>())
                if (player != null) yield return player;

            foreach (var replayEvent in _replay.Events ?? new List<ReplayEvent>())
            {
                foreach (var player in PlayersFromEvent(replayEvent))
                {
                    if (player != null && SameReplayTeam(player, team))
                        yield return player;
                }
            }
        }

        private IEnumerable<ReplayPlayer> PlayersFromEvent(ReplayEvent replayEvent)
        {
            if (replayEvent == null)
                yield break;
            if (replayEvent.Bases?.First != null) yield return replayEvent.Bases.First;
            if (replayEvent.Bases?.Second != null) yield return replayEvent.Bases.Second;
            if (replayEvent.Bases?.Third != null) yield return replayEvent.Bases.Third;
            if (replayEvent.Result?.Batter != null) yield return replayEvent.Result.Batter;
            if (replayEvent.Result?.Pitcher != null) yield return replayEvent.Result.Pitcher;
        }

        private static bool SameReplayTeam(ReplayPlayer player, ReplayTeam team)
        {
            if (player == null || team == null)
                return false;
            if (string.IsNullOrWhiteSpace(player.TeamId))
                return false;
            return string.Equals(player.TeamId, team.TeamId, StringComparison.OrdinalIgnoreCase);
        }

        private Player EnsurePlayer(Team team, ReplayPlayer replayPlayer)
        {
            team.Roster ??= new List<Player>();
            string replayId = ReplayPlayerKey(team, replayPlayer);
            if (_playersByReplayId.TryGetValue(replayId, out Player? existing))
                return existing;

            var player = new Player
            {
                Id = ReplayGuid("player", replayPlayer.PlayerId, replayId),
                Name = string.IsNullOrWhiteSpace(replayPlayer.Name) ? "Player" : replayPlayer.Name.Trim(),
                Role = IsReplayPitcher(replayPlayer) ? PlayerRole.Pitcher : PlayerRole.Batter,
                Classification = ParseClassification(replayPlayer.Classification),
                InitialClassification = ParseClassification(replayPlayer.Classification),
                Positions = NormalizePositions(replayPlayer),
                Bats = string.IsNullOrWhiteSpace(replayPlayer.Bats) ? replayPlayer.Handedness : replayPlayer.Bats,
                Throws = string.IsNullOrWhiteSpace(replayPlayer.Throws) ? replayPlayer.Handedness : replayPlayer.Throws,
                AvatarPath = replayPlayer.Photo,
                SpriteSheetPath = replayPlayer.SpriteSheet,
                Contact = replayPlayer.Ratings?.Contact ?? 55,
                Power = replayPlayer.Ratings?.Power ?? 55,
                Speed = replayPlayer.Ratings?.Speed ?? 55,
                BaseRunning = replayPlayer.Ratings?.BaseRunning ?? 55,
                Pitching = replayPlayer.Ratings?.Pitching ?? (IsReplayPitcher(replayPlayer) ? 60 : 25),
                Stamina = replayPlayer.Ratings?.Stamina ?? (IsReplayPitcher(replayPlayer) ? 60 : 30),
                Fielding = replayPlayer.Ratings?.Fielding ?? 55,
                ArmStrength = replayPlayer.Ratings?.CatcherArm > 0
                    ? replayPlayer.Ratings.CatcherArm
                    : replayPlayer.Ratings?.Throwing ?? 55,
                Accuracy = replayPlayer.Ratings?.CatcherBlocking > 0
                    ? replayPlayer.Ratings.CatcherBlocking
                    : replayPlayer.Ratings?.Throwing ?? 55,
                PopTime = replayPlayer.Ratings?.CatcherBlocking > 0
                    ? replayPlayer.Ratings.CatcherBlocking
                    : 50
            };
            team.Roster.Add(player);
            _playersByReplayId[replayId] = player;
            if (!string.IsNullOrWhiteSpace(replayPlayer.PlayerId))
                _playersBySourceId[replayPlayer.PlayerId] = player;
            return player;
        }

        private void FillReplayRoster(Team team)
        {
            List<Player> roster = team.Roster ??= new List<Player>();
            string[] positions = { "C", "P", "1B", "2B", "3B", "SS", "LF", "CF", "RF" };
            foreach (string position in positions)
            {
                if (roster.Any(p => PositionAssignmentEngine.CanAssign(p, position)))
                    continue;

                var player = new Player
                {
                    Id = ReplayGuid("placeholder", team.Id.ToString("N"), position),
                    Name = position + " Replay",
                    Role = position == "P" ? PlayerRole.Pitcher : PlayerRole.Batter,
                    Positions = position,
                    Contact = 45,
                    Power = 45,
                    Speed = 45,
                    Pitching = position == "P" ? 55 : 20,
                    Stamina = position == "P" ? 55 : 30,
                    Fielding = 50
                };
                roster.Add(player);
            }

            while (roster.Count < 9)
            {
                int number = roster.Count + 1;
                roster.Add(new Player
                {
                    Id = ReplayGuid("placeholder", team.Id.ToString("N"), "bench" + number),
                    Name = "Replay Player " + number,
                    Role = PlayerRole.Batter,
                    Positions = "OF",
                    Contact = 45,
                    Power = 45,
                    Speed = 45,
                    Fielding = 45
                });
            }
        }

        private void BuildReplayLineup(Team team, ReplayTeam replayTeam)
        {
            var defensive = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
            var batting = new List<TeamBaseLineupSlot>();

            foreach (var slot in (replayTeam.Lineup ?? new List<ReplayLineupSlot>()).OrderBy(s => s?.Order ?? 0))
            {
                if (slot?.Player == null)
                    continue;
                Player player = EnsurePlayer(team, slot.Player);
                string position = NormalizePosition(slot.Position);
                if (string.IsNullOrWhiteSpace(position))
                    position = FirstPosition(player);
                bool dh = string.Equals(position, "DH", StringComparison.OrdinalIgnoreCase);
                batting.Add(new TeamBaseLineupSlot
                {
                    BattingOrder = batting.Count + 1,
                    PlayerId = player.Id,
                    PlayerName = player.Name,
                    DefensivePosition = dh ? "DH" : position,
                    DesignatedHitter = dh
                });
                if (!dh && !string.IsNullOrWhiteSpace(position) && !defensive.ContainsKey(position))
                    defensive[position] = player.Id;
            }

            string[] mandatory = { "C", "P", "1B", "2B", "3B", "SS", "LF", "CF", "RF" };
            foreach (string position in mandatory)
            {
                if (defensive.ContainsKey(position))
                    continue;
                Player? player = team.Roster.FirstOrDefault(p => PositionAssignmentEngine.CanAssign(p, position)) ?? team.Roster.FirstOrDefault();
                if (player != null)
                    defensive[position] = player.Id;
            }

            foreach (var player in team.Roster.Where(p => batting.All(b => b.PlayerId != p.Id)).Take(9 - batting.Count))
            {
                batting.Add(new TeamBaseLineupSlot
                {
                    BattingOrder = batting.Count + 1,
                    PlayerId = player.Id,
                    PlayerName = player.Name,
                    DefensivePosition = FirstPosition(player)
                });
            }

            Player? pitcher = defensive.TryGetValue("P", out Guid pitcherId)
                ? team.Roster.FirstOrDefault(p => p.Id == pitcherId)
                : team.Roster.FirstOrDefault(p => p.Role == PlayerRole.Pitcher);
            if (pitcher != null)
                pitcher.Role = PlayerRole.Pitcher;

            team.BaseLineup = new TeamBaseLineup
            {
                LastCalculatedAt = DateTime.Now,
                HasDesignatedHitter = batting.Any(b => b.DesignatedHitter),
                StartingPitcherId = pitcher?.Id,
                DesignatedHitterId = batting.FirstOrDefault(b => b.DesignatedHitter)?.PlayerId,
                DefensiveAssignments = defensive,
                BattingOrder = batting.Take(9).Select((slot, index) =>
                {
                    slot.BattingOrder = index + 1;
                    return slot;
                }).ToList(),
                Status = "Replay lineup"
            };

            var pitchers = team.Roster.Where(p => p.Role == PlayerRole.Pitcher).ToList();
            if (pitchers.Count == 0 && pitcher != null)
                pitchers.Add(pitcher);
            team.PitchingPlan = new TeamPitchingPlan
            {
                RotationSize = Math.Clamp(Math.Max(3, pitchers.Count), 3, 5),
                StarterRotationIds = pitchers.Select(p => p.Id).Take(5).ToList(),
                Status = "Replay pitching staff"
            };
        }

        private void TogglePlay()
        {
            if (_timer.Enabled)
            {
                _timer.Stop();
                _playbackClock.Stop();
                _playButton.Text = "Play";
                return;
            }

            if (_replay.UsesTimedPlayback)
            {
                _lastClockMilliseconds = 0;
                _playbackScaleRemainder = 0;
                _playbackClock.Restart();
            }
            _timer.Start();
            _playButton.Text = "Pause";
        }

        private void PlaybackTick()
        {
            if (!_replay.UsesTimedPlayback)
            {
                Step();
                return;
            }

            long elapsed = _playbackClock.ElapsedMilliseconds;
            long delta = Math.Max(0, elapsed - _lastClockMilliseconds);
            _lastClockMilliseconds = elapsed;
            long scaledDelta = ReplayWatchNavigation.ScaleElapsed(delta, _playbackSpeed, ref _playbackScaleRemainder);
            AdvanceExactPlayback(_replayTimeMs + scaledDelta);
        }

        private void Step()
        {
            if (_replay.UsesTimedPlayback)
            {
                _timer.Stop();
                _playbackClock.Stop();
                _playButton.Text = "Play";
                if (_activeExactEvent != null)
                    AdvanceExactPlayback(_activeExactEvent.TimeMs + Math.Max(0, _activeExactEvent.DurationMs));
                else if (_nextExactEventIndex < _replay.Events.Count)
                {
                    ReplayEvent next = _replay.Events[_nextExactEventIndex];
                    AdvanceExactPlayback(next.TimeMs + Math.Max(0, next.DurationMs));
                }
                return;
            }

            if (_replay.Events.Count == 0)
            {
                _timer.Stop();
                _playButton.Text = "Play";
                return;
            }

            if (_index >= _replay.Events.Count - 1)
            {
                _timer.Stop();
                _playButton.Text = "Play";
                return;
            }

            _index++;
            DisplayEvent(_replay.Events[_index], isFinalEvent: _index >= _replay.Events.Count - 1);
        }

        private void ResetReplay()
        {
            _timer.Stop();
            _playbackClock.Reset();
            _playButton.Text = "Play";
            _index = -1;
            _activeExactEvent = null;
            _nextExactEventIndex = 0;
            _replayTimeMs = Math.Max(0, _replay.StartingState?.TimeMs ?? 0);
            _lastExactFrameTimeMs = _replayTimeMs - 1;
            _lastClockMilliseconds = 0;
            _playbackScaleRemainder = 0;
            _playedCueKeys.Clear();
            foreach (LaunchSoundPlayer player in _replayAudioPlayers)
                player.Dispose();
            _replayAudioPlayers.Clear();
            _playText.Clear();
            if (_replay.UsesTimedPlayback)
            {
                ApplyExactState(_replay.StartingState, null, isFinalEvent: false);
                _stateLabel.Text = _replay.IsBestEffort
                    ? "Best Effort - " + _replay.ReplayIssues.Count + " approximation" + (_replay.ReplayIssues.Count == 1 ? "" : "s")
                    : "Exact replay ready";
            }
            else
            {
                ApplyReplayState(null, isFinalEvent: false);
                _stateLabel.Text = "Snapshot replay ready";
                _scoreLabel.Text = ScoreText(new ReplayScore());
                _basesLabel.Text = "Bases empty";
            }

            if (_replay.IsBestEffort)
            {
                _playText.AppendText("BEST-EFFORT REPLAY\r\n");
                _playText.AppendText("Available timing, movement, state, audio, and cutscene data will be reproduced. Missing data will be approximated.\r\n");
                foreach (string issue in _replay.ReplayIssues)
                    _playText.AppendText("- " + issue + "\r\n");
                _playText.AppendText("\r\n");
            }

            if (_replay.Events.Count == 0 && _replay.PlayLog.Count > 0)
                _playText.Text = string.Join(Environment.NewLine, _replay.PlayLog);

            UpdateNavigationControls();
        }

        private void AdvanceExactPlayback(long targetTimeMs)
        {
            if (!_replay.UsesTimedPlayback || _replay.Events.Count == 0)
            {
                StopExactPlayback();
                return;
            }

            targetTimeMs = Math.Max(_replayTimeMs, targetTimeMs);
            while (true)
            {
                if (_activeExactEvent == null)
                {
                    if (_nextExactEventIndex >= _replay.Events.Count)
                    {
                        ApplyFinalExactState(preserveRenderFrame: true);
                        StopExactPlayback();
                        break;
                    }

                    ReplayEvent next = _replay.Events[_nextExactEventIndex];
                    if (targetTimeMs < next.TimeMs)
                        break;
                    _activeExactEvent = next;
                    _index = _nextExactEventIndex;
                    _nextExactEventIndex++;
                    _lastExactFrameTimeMs = next.TimeMs - 1;
                    ApplyExactState(next.Before, next, isFinalEvent: false);
                    AppendEventText(next);
                }

                long eventEnd = _activeExactEvent.TimeMs + Math.Max(0, _activeExactEvent.DurationMs);
                long frameTime = Math.Min(targetTimeMs, eventEnd);
                ProcessExactCues(_activeExactEvent, _lastExactFrameTimeMs, frameTime);
                ReplayRenderFrame frame = ReplayExactEngine.CreateFrame(_activeExactEvent, frameTime);
                HydrateReplayActors(frame);
                _gameplay.ApplyExactReplayFrame(frame);
                ApplyTimedScoreboardUpdate(_activeExactEvent, frameTime);
                _lastExactFrameTimeMs = frameTime;

                if (targetTimeMs < eventEnd)
                    break;

                ApplyExactState(_activeExactEvent.After, _activeExactEvent,
                    isFinalEvent: _nextExactEventIndex >= _replay.Events.Count,
                    preserveRenderFrame: true);
                _activeExactEvent = null;
            }

            _replayTimeMs = targetTimeMs;
            UpdateNavigationControls();
        }

        private void ApplyFinalExactState(bool preserveRenderFrame = false)
        {
            ApplyExactState(_replay.FinalState, null, isFinalEvent: true, preserveRenderFrame: preserveRenderFrame);
        }

        private void StopExactPlayback()
        {
            _timer.Stop();
            _playbackClock.Stop();
            _playButton.Text = "Play";
        }

        private void ApplyTimedScoreboardUpdate(ReplayEvent? replayEvent, long frameTimeMs)
        {
            if (replayEvent == null)
                return;

            long? updateAt = replayEvent.Animation?.ScoreboardUpdatesAtMs?.OrderBy(value => value).FirstOrDefault();
            if (updateAt.HasValue && updateAt.Value > 0 && frameTimeMs >= updateAt.Value)
                _gameplay.ApplyReplayScore(replayEvent.After?.Score);
        }

        private void ProcessExactCues(ReplayEvent replayEvent, long previousTimeMs, long frameTimeMs)
        {
            var audioCues = replayEvent.Audio ?? new List<ReplayAudioCue>();
            for (int index = 0; index < audioCues.Count; index++)
            {
                ReplayAudioCue cue = audioCues[index];
                string key = replayEvent.EventId + ":audio:" + index;
                if (cue == null || _playedCueKeys.Contains(key) || cue.StartTimeMs <= previousTimeMs || cue.StartTimeMs > frameTimeMs)
                    continue;
                string path = ReplayStore.ResolveAudioCuePath(_replay, cue);
                if (!string.IsNullOrWhiteSpace(path))
                {
                    var player = new LaunchSoundPlayer();
                    if (cue.Loop) player.PlayLoop(path); else player.PlayOnce(path);
                    _replayAudioPlayers.Add(player);
                }
                _playedCueKeys.Add(key);
            }

            var cutsceneCues = replayEvent.Cutscenes ?? new List<ReplayCutsceneCue>();
            for (int index = 0; index < cutsceneCues.Count; index++)
            {
                ReplayCutsceneCue cue = cutsceneCues[index];
                string key = replayEvent.EventId + ":cutscene:" + index;
                if (cue == null || _playedCueKeys.Contains(key) || cue.StartTimeMs <= previousTimeMs || cue.StartTimeMs > frameTimeMs)
                    continue;
                string path = ReplayStore.ResolveCutsceneCuePath(_replay, cue);
                if (!string.IsNullOrWhiteSpace(path))
                {
                    if (cue.Blocking)
                    {
                        _playbackClock.Stop();
                        CutscenePlaybackForm.PlayPath(this, path, cue.Trigger, cue.DurationMs, blocking: true);
                        _lastClockMilliseconds = 0;
                        _playbackClock.Restart();
                    }
                    else
                    {
                        CutscenePlaybackForm.PlayPath(this, path, cue.Trigger, cue.DurationMs, blocking: false);
                    }
                }
                _playedCueKeys.Add(key);
            }
        }

        private void DisplayEvent(ReplayEvent replayEvent, bool isFinalEvent)
        {
            ApplyReplayState(replayEvent, isFinalEvent);
            _stateLabel.Text = StateText(replayEvent);
            _scoreLabel.Text = ScoreText(replayEvent.Score);
            _basesLabel.Text = BaseText(replayEvent.Bases);

            AppendEventText(replayEvent);
            UpdateNavigationControls();
        }

        private Button ReplayButton(string name, string text, string accessibleName, int tabIndex, Action action)
        {
            var button = new Button
            {
                Name = name,
                Text = text,
                AutoSize = true,
                AccessibleName = accessibleName,
                TabIndex = tabIndex
            };
            button.Click += (s, e) => action();
            return button;
        }

        private void ChangePlaybackSpeed()
        {
            if (_speedCombo.SelectedItem is not ReplayPlaybackSpeed speed)
                return;

            if (_replay.UsesTimedPlayback && _timer.Enabled)
                PlaybackTick();

            _playbackSpeed = speed;
            _playbackScaleRemainder = 0;
            if (!_replay.UsesTimedPlayback)
                _timer.Interval = ReplayWatchNavigation.SnapshotInterval(speed);
        }

        private void JumpToPreviousEvent()
            => SeekToEvent(ReplayWatchNavigation.PreviousEventIndex(_index, _replay.Events.Count));

        private void JumpToNextEvent()
            => SeekToEvent(ReplayWatchNavigation.NextEventIndex(_index, _replay.Events.Count));

        private void JumpToPreviousInning()
            => SeekToEvent(ReplayWatchNavigation.PreviousInningIndex(_replay.Events, _index));

        private void JumpToNextInning()
            => SeekToEvent(ReplayWatchNavigation.NextInningIndex(_replay.Events, _index));

        private void SeekToEvent(int targetIndex)
        {
            if (targetIndex < -1 || targetIndex >= _replay.Events.Count || targetIndex == _index)
                return;

            ResetReplay();
            if (targetIndex < 0)
                return;

            if (_replay.UsesTimedPlayback)
                RebuildTimedReplay(targetIndex);
            else
                RebuildSnapshotReplay(targetIndex);
            UpdateNavigationControls();
        }

        private void RebuildTimedReplay(int targetIndex)
        {
            foreach (int eventIndex in ReplayWatchNavigation.RebuildEventIndexes(targetIndex, _replay.Events.Count))
            {
                ReplayEvent replayEvent = _replay.Events[eventIndex];
                _index = eventIndex;
                _nextExactEventIndex = eventIndex + 1;
                AppendEventText(replayEvent);
                if (eventIndex < targetIndex)
                    ApplyExactState(replayEvent.After, replayEvent, isFinalEvent: false);
            }

            ReplayEvent target = _replay.Events[targetIndex];
            _activeExactEvent = null;
            long frameTimeMs = target.TimeMs + Math.Max(0, target.DurationMs);
            _replayTimeMs = Math.Max(frameTimeMs, target.After?.TimeMs ?? 0);
            _lastExactFrameTimeMs = frameTimeMs;

            _gameplay.ClearExactReplayFrame();
            ReplayRenderFrame frame = ReplayExactEngine.CreateFrame(target, frameTimeMs);
            HydrateReplayActors(frame);
            _gameplay.ApplyExactReplayFrame(frame);

            bool isTerminalEvent = ReplayWatchNavigation.IsTerminalEvent(targetIndex, _replay.Events.Count);
            ApplyExactState(
                isTerminalEvent ? _replay.FinalState : target.After,
                isTerminalEvent ? null : target,
                isFinalEvent: isTerminalEvent,
                preserveRenderFrame: true);
        }

        private void RebuildSnapshotReplay(int targetIndex)
        {
            foreach (int eventIndex in ReplayWatchNavigation.RebuildEventIndexes(targetIndex, _replay.Events.Count))
            {
                _index = eventIndex;
                DisplayEvent(_replay.Events[eventIndex],
                    isFinalEvent: ReplayWatchNavigation.IsTerminalEvent(eventIndex, _replay.Events.Count));
            }
        }

        private void UpdateNavigationControls()
        {
            if (_previousEventButton == null)
                return;

            _previousEventButton.Enabled = ReplayWatchNavigation.PreviousEventIndex(_index, _replay.Events.Count) != _index;
            _nextEventButton.Enabled = ReplayWatchNavigation.NextEventIndex(_index, _replay.Events.Count) != _index;
            _previousInningButton.Enabled = ReplayWatchNavigation.PreviousInningIndex(_replay.Events, _index) >= 0;
            _nextInningButton.Enabled = ReplayWatchNavigation.NextInningIndex(_replay.Events, _index) >= 0;
        }

        private void AppendEventText(ReplayEvent replayEvent)
        {
            if (replayEvent == null)
                return;
            var line = replayEvent.Description;
            if (string.IsNullOrWhiteSpace(line) && replayEvent.Result != null)
                line = replayEvent.Result.Description;
            if (string.IsNullOrWhiteSpace(line))
                line = replayEvent.EventType;

            var prefix = "#" + replayEvent.Sequence.ToString("000") + " " + HalfText(replayEvent.Half) + " " + replayEvent.Inning;
            _playText.AppendText(prefix + " | " + line + Environment.NewLine);
            if (replayEvent.Result != null && !string.IsNullOrWhiteSpace(replayEvent.Result.NarrationText))
                _playText.AppendText("      " + replayEvent.Result.NarrationText + Environment.NewLine);
            foreach (var advanced in replayEvent.RunnersAdvanced.Where(a => !string.IsNullOrWhiteSpace(a)))
                _playText.AppendText("      -> " + advanced + Environment.NewLine);
        }

        private void ApplyExactState(
            ReplayGameState? replayState,
            ReplayEvent? replayEvent,
            bool isFinalEvent,
            bool preserveRenderFrame = false)
        {
            replayState ??= new ReplayGameState();
            if (!preserveRenderFrame)
                _gameplay.ClearExactReplayFrame();
            var state = new GameplayState
            {
                Id = Guid.NewGuid(),
                Mode = GameMode.CpuVsCpuWatch,
                AwayTeam = _awayTeam,
                HomeTeam = _homeTeam,
                RegulationInnings = Math.Clamp(_replay.Game.Innings <= 0 ? 9 : _replay.Game.Innings, 5, 9),
                AllowExtraInnings = _replay.Rules?.ExtraInningsEnabled ?? true,
                MercyRuleEnabled = _replay.Rules?.MercyRuleEnabled ?? false,
                MercyRuleRuns = Math.Max(1, _replay.Rules?.MercyRuleRuns ?? 10),
                MercyRuleMinimumInning = Math.Max(1, _replay.Rules?.MercyRuleMinimumInning ?? 5),
                ExtraInningRunnerOnSecond = _replay.Rules?.ExtraInningRunnerOnSecond ?? false,
                CourtesyRunnerForPitchersCatchers = _replay.Rules?.CourtesyRunnerForPitchersCatchers ?? false,
                Inning = Math.Max(1, replayState.Inning),
                Half = ParseHalf(replayState.Half),
                Count = new CountState
                {
                    Balls = Math.Clamp(replayState.Balls, 0, 4),
                    Strikes = Math.Clamp(replayState.Strikes, 0, 3),
                    Outs = Math.Clamp(replayState.Outs, 0, 3)
                },
                Bases = new BaseState(),
                AwayScore = Math.Max(0, replayState.Score?.Away ?? 0),
                HomeScore = Math.Max(0, replayState.Score?.Home ?? 0),
                AwayBatterIndex = Math.Max(0, replayState.AwayBatterIndex),
                HomeBatterIndex = Math.Max(0, replayState.HomeBatterIndex),
                AwayPitcherIndex = Math.Max(0, replayState.AwayPitcherIndex),
                HomePitcherIndex = Math.Max(0, replayState.HomePitcherIndex),
                IsComplete = isFinalEvent
            };
            state.AwayLineupPlayerIds.AddRange(_awayTeam.BaseLineup.BattingOrder.OrderBy(slot => slot.BattingOrder).Select(slot => slot.PlayerId));
            state.HomeLineupPlayerIds.AddRange(_homeTeam.BaseLineup.BattingOrder.OrderBy(slot => slot.BattingOrder).Select(slot => slot.PlayerId));
            ApplyDhState(state, replayState.DhState);
            state.Bases.First = ExactBaseRunner(replayState.Bases?.First);
            state.Bases.Second = ExactBaseRunner(replayState.Bases?.Second);
            state.Bases.Third = ExactBaseRunner(replayState.Bases?.Third);

            _gameplay.ApplyGameplayState(state);
            _gameplay.SetReplayPlaybackMode();
            ApplyStateFielderPositions(replayState);
            string quality = _replay.IsBestEffort ? "Best-effort" : "Exact";
            _gameplay.SetModeLabel(replayEvent == null
                ? (isFinalEvent ? quality + " replay complete" : quality + " replay ready")
                : quality + " - " + ReplayModeLabel(replayEvent));
            _stateLabel.Text = ExactStateText(replayState);
            _scoreLabel.Text = ScoreText(replayState.Score);
            _basesLabel.Text = ExactBaseText(replayState.Bases);
        }

        private void ApplyDhState(GameplayState state, ReplayDhState? dhState)
        {
            if (state == null || dhState == null)
                return;
            state.AwayDhActive = dhState.AwayDhActive;
            state.HomeDhActive = dhState.HomeDhActive;
            if (_playersBySourceId.TryGetValue(dhState.AwayDhPlayerId ?? "", out Player? awayDh))
                state.AwayDesignatedHitterId = awayDh.Id;
            if (_playersBySourceId.TryGetValue(dhState.HomeDhPlayerId ?? "", out Player? homeDh))
                state.HomeDesignatedHitterId = homeDh.Id;
        }

        private void ApplyStateFielderPositions(ReplayGameState? replayState)
        {
            var actors = new List<ReplayRenderActor>();
            foreach (ReplayStateFielder fielder in replayState?.Fielders ?? new List<ReplayStateFielder>())
            {
                actors.Add(new ReplayRenderActor
                {
                    PlayerId = fielder.PlayerId,
                    DefensivePosition = fielder.Position,
                    X = Math.Clamp(fielder.X, 0f, 1f),
                    Y = Math.Clamp(fielder.Y, 0f, 1f)
                });
            }
            HydrateReplayActors(actors);
            _gameplay.ApplyReplayFielderPositions(actors);
        }

        private void HydrateReplayActors(ReplayRenderFrame? frame)
        {
            if (frame != null)
                HydrateReplayActors(frame.Actors);
        }

        private void HydrateReplayActors(IEnumerable<ReplayRenderActor>? actors)
        {
            foreach (ReplayRenderActor actor in actors ?? Enumerable.Empty<ReplayRenderActor>())
            {
                if (!_playersBySourceId.TryGetValue(actor.PlayerId ?? "", out Player? player))
                    continue;
                actor.Player = player;
                actor.Team = _awayTeam.Roster.Any(item => item.Id == player.Id) ? _awayTeam : _homeTeam;
            }
        }

        private BaseRunner? ExactBaseRunner(ReplayBaseOccupant? occupant)
        {
            if (occupant == null || string.IsNullOrWhiteSpace(occupant.PlayerId) ||
                !_playersBySourceId.TryGetValue(occupant.PlayerId, out Player? player))
                return null;
            Team team = _awayTeam.Roster.Any(item => item.Id == player.Id) ? _awayTeam : _homeTeam;
            Guid responsiblePitcherId = Guid.Empty;
            if (!string.IsNullOrWhiteSpace(occupant.ResponsiblePitcherId) &&
                _playersBySourceId.TryGetValue(occupant.ResponsiblePitcherId, out Player? pitcher))
                responsiblePitcherId = pitcher.Id;
            return new BaseRunner
            {
                Player = player,
                Team = team,
                ResponsiblePitcherId = responsiblePitcherId,
                Earned = occupant.Earned
            };
        }

        private static string ExactStateText(ReplayGameState? state)
        {
            if (state == null)
                return "Ready";
            return HalfText(state.Half) + " " + state.Inning + "   " + state.Outs + " out" + (state.Outs == 1 ? "" : "s") +
                "   " + state.Balls + "-" + state.Strikes;
        }

        private string ExactBaseText(ReplayExactBases? bases)
        {
            string Name(ReplayBaseOccupant? occupant)
                => occupant != null && _playersBySourceId.TryGetValue(occupant.PlayerId ?? "", out Player? player)
                    ? player.Name
                    : "-";
            return "1B: " + Name(bases?.First) + "   2B: " + Name(bases?.Second) + "   3B: " + Name(bases?.Third);
        }

        private void ApplyReplayState(ReplayEvent? replayEvent, bool isFinalEvent)
        {
            var state = new GameplayState
            {
                Id = Guid.NewGuid(),
                Mode = GameMode.CpuVsCpuWatch,
                AwayTeam = _awayTeam,
                HomeTeam = _homeTeam,
                RegulationInnings = Math.Clamp(_replay.Game.Innings <= 0 ? 9 : _replay.Game.Innings, 5, 9),
                AllowExtraInnings = true,
                MercyRuleEnabled = false,
                ExtraInningRunnerOnSecond = false,
                CourtesyRunnerForPitchersCatchers = false,
                Inning = Math.Max(1, replayEvent?.Inning ?? 1),
                Half = ParseHalf(replayEvent?.Half),
                Count = new CountState { Outs = Math.Clamp(replayEvent?.Outs ?? 0, 0, 3) },
                Bases = new BaseState(),
                AwayScore = Math.Max(0, replayEvent?.Score?.Away ?? 0),
                HomeScore = Math.Max(0, replayEvent?.Score?.Home ?? 0),
                IsComplete = isFinalEvent
            };

            state.AwayLineupPlayerIds.AddRange(_awayTeam.BaseLineup.BattingOrder.OrderBy(s => s.BattingOrder).Select(s => s.PlayerId));
            state.HomeLineupPlayerIds.AddRange(_homeTeam.BaseLineup.BattingOrder.OrderBy(s => s.BattingOrder).Select(s => s.PlayerId));
            state.AwayPitcherIndex = PitcherIndexFor(_awayTeam, replayEvent?.Result?.Pitcher);
            state.HomePitcherIndex = PitcherIndexFor(_homeTeam, replayEvent?.Result?.Pitcher);
            state.AwayBatterIndex = BatterIndexFor(_awayTeam, replayEvent?.Result?.Batter);
            state.HomeBatterIndex = BatterIndexFor(_homeTeam, replayEvent?.Result?.Batter);
            state.Bases.First = ReplayBaseRunner(replayEvent?.Bases?.First);
            state.Bases.Second = ReplayBaseRunner(replayEvent?.Bases?.Second);
            state.Bases.Third = ReplayBaseRunner(replayEvent?.Bases?.Third);

            _gameplay.ApplyGameplayState(state);
            _gameplay.SetReplayPlaybackMode();
            _gameplay.SetModeLabel(replayEvent == null ? "Replay ready" : ReplayModeLabel(replayEvent));
        }

        private BaseRunner? ReplayBaseRunner(ReplayPlayer? replayPlayer)
        {
            if (replayPlayer == null)
                return null;
            if (!_playersByReplayId.TryGetValue(ReplayPlayerKey(replayPlayer), out Player? player))
                return null;
            Team team = _awayTeam.Roster.Any(p => p.Id == player.Id) ? _awayTeam : _homeTeam;
            return new BaseRunner
            {
                Player = player,
                Team = team,
                ResponsiblePitcherId = Guid.Empty,
                Earned = true
            };
        }

        private int BatterIndexFor(Team team, ReplayPlayer? replayPlayer)
        {
            if (team == null || replayPlayer == null)
                return 0;
            if (!_playersByReplayId.TryGetValue(ReplayPlayerKey(team, replayPlayer), out Player? player))
                return 0;
            var order = team.BaseLineup.BattingOrder.OrderBy(s => s.BattingOrder).ToList();
            int index = order.FindIndex(s => s.PlayerId == player.Id);
            return Math.Max(0, index);
        }

        private int PitcherIndexFor(Team team, ReplayPlayer? replayPlayer)
        {
            if (team == null || replayPlayer == null)
                return 0;
            if (!_playersByReplayId.TryGetValue(ReplayPlayerKey(team, replayPlayer), out Player? player))
                return 0;
            var staff = LineupEngine.GetPitchingStaff(team).ToList();
            int index = staff.FindIndex(p => p.Id == player.Id);
            return Math.Max(0, index);
        }

        private string ReplayModeLabel(ReplayEvent? replayEvent)
        {
            string? result = replayEvent?.Result?.Outcome;
            if (string.IsNullOrWhiteSpace(result))
                result = replayEvent?.EventType;
            return StateText(replayEvent) + (string.IsNullOrWhiteSpace(result) ? "" : " - " + result);
        }

        private string StateText(ReplayEvent? replayEvent)
        {
            if (replayEvent == null)
                return "Ready";
            return HalfText(replayEvent.Half) + " " + replayEvent.Inning + "   " + replayEvent.Outs + " out" + (replayEvent.Outs == 1 ? "" : "s");
        }

        private string ScoreText(ReplayScore? score)
        {
            score ??= new ReplayScore();
            var away = string.IsNullOrWhiteSpace(_replay.Teams.Away.ScoreboardAbbreviation)
                ? "AWAY"
                : _replay.Teams.Away.ScoreboardAbbreviation;
            var home = string.IsNullOrWhiteSpace(_replay.Teams.Home.ScoreboardAbbreviation)
                ? "HOME"
                : _replay.Teams.Home.ScoreboardAbbreviation;
            return away + " " + score.Away + "  " + home + " " + score.Home;
        }

        private static string BaseText(ReplayBases? bases)
        {
            if (bases == null)
                return "Bases empty";
            return "1B: " + RunnerName(bases.First) + "   2B: " + RunnerName(bases.Second) + "   3B: " + RunnerName(bases.Third);
        }

        private static string RunnerName(ReplayPlayer? player) => player == null ? "-" : player.Name;

        private static string HalfText(string? half)
        {
            if (string.Equals(half, "top", StringComparison.OrdinalIgnoreCase))
                return "Top";
            if (string.Equals(half, "bottom", StringComparison.OrdinalIgnoreCase))
                return "Bot";
            return string.IsNullOrWhiteSpace(half) ? "Game" : half;
        }

        private static HalfInning ParseHalf(string? half)
            => string.Equals(half, "bottom", StringComparison.OrdinalIgnoreCase) ? HalfInning.Bottom : HalfInning.Top;

        private static Label ReplayLabel(string text)
        {
            return new Label
            {
                Text = text,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font(SystemFonts.MessageBoxFont?.FontFamily ?? FontFamily.GenericSansSerif, 12f, FontStyle.Bold)
            };
        }

        private static string ReplayPlayerKey(ReplayPlayer? player)
            => ReplayPlayerKey(null, player);

        private static string ReplayPlayerKey(Team? team, ReplayPlayer? player)
        {
            if (player == null)
                return "";
            string teamKey = !string.IsNullOrWhiteSpace(player.TeamId)
                ? player.TeamId
                : team?.Id.ToString("N") ?? "";
            string playerKey = !string.IsNullOrWhiteSpace(player.PlayerId)
                ? player.PlayerId
                : player.Name;
            return teamKey + ":" + playerKey;
        }

        private Guid ReplayGuid(string scope, string primary, string fallback)
        {
            string key = scope + ":" + (string.IsNullOrWhiteSpace(primary) ? fallback : primary);
            if (!_idsByReplayKey.TryGetValue(key, out Guid id))
            {
                id = Guid.NewGuid();
                _idsByReplayKey[key] = id;
            }

            return id;
        }

        private static string TeamName(string value, string fallback)
            => (string.IsNullOrWhiteSpace(value) ? fallback : value).Trim();

        private static string LimitScoreboardName(string value, string fallback)
            => Team.Limit(string.IsNullOrWhiteSpace(value) ? fallback : value, Team.MaxScoreboardAbbreviationLength).ToUpperInvariant();

        private static Color ParseColor(string hex, Color fallback)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(hex))
                    return fallback;
                return ColorTranslator.FromHtml(hex);
            }
            catch
            {
                return fallback;
            }
        }

        private static PlayerClassification ParseClassification(string value)
        {
            return Enum.TryParse(value, ignoreCase: true, out PlayerClassification classification)
                ? classification
                : PlayerClassification.Unassigned;
        }

        private static bool IsReplayPitcher(ReplayPlayer? player)
        {
            string text = ((player?.Position ?? "") + " " + (player?.PlayerType ?? "") + " " + string.Join(" ", player?.EligiblePositions ?? new List<string>())).ToUpperInvariant();
            return text.Contains("PITCH") || text.Split(new[] { ' ', '/', ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Any(p => p == "P");
        }

        private static string NormalizePositions(ReplayPlayer player)
        {
            var values = new List<string>();
            if (!string.IsNullOrWhiteSpace(player?.Position))
                values.Add(player.Position);
            if (player?.EligiblePositions != null)
                values.AddRange(player.EligiblePositions);
            string joined = string.Join("/", values.Select(NormalizePosition).Where(p => !string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(joined))
                return joined;
            return IsReplayPitcher(player) ? "P" : "OF";
        }

        private static string NormalizePosition(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "";
            string normalized = value.Trim().ToUpperInvariant().Replace(" ", "");
            return normalized switch
            {
                "PITCHER" => "P",
                "CATCHER" => "C",
                "FIRST" or "FIRSTBASE" => "1B",
                "SECOND" or "SECONDBASE" => "2B",
                "THIRD" or "THIRDBASE" => "3B",
                "SHORTSTOP" => "SS",
                "LEFTFIELD" => "LF",
                "CENTERFIELD" => "CF",
                "RIGHTFIELD" => "RF",
                "OUTFIELD" => "OF",
                "DESIGNATEDHITTER" => "DH",
                _ => normalized
            };
        }

        private static string FirstPosition(Player player)
        {
            string value = player?.Positions ?? "";
            string? first = value.Split(new[] { '/', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(NormalizePosition)
                .FirstOrDefault(p => !string.IsNullOrWhiteSpace(p));
            return string.IsNullOrWhiteSpace(first) ? "OF" : first;
        }
    }

    internal sealed class ReplayPlaybackSpeed
    {
        public ReplayPlaybackSpeed(string label, int numerator, int denominator)
        {
            Label = label;
            Numerator = numerator;
            Denominator = denominator;
        }

        public string Label { get; }
        public int Numerator { get; }
        public int Denominator { get; }

        public override string ToString() => Label;
    }

    internal static class ReplayWatchNavigation
    {
        private const int SnapshotBaseIntervalMs = 1200;

        public static IReadOnlyList<ReplayPlaybackSpeed> PlaybackSpeeds { get; } = new[]
        {
            new ReplayPlaybackSpeed("0.5x", 1, 2),
            new ReplayPlaybackSpeed("1x", 1, 1),
            new ReplayPlaybackSpeed("2x", 2, 1),
            new ReplayPlaybackSpeed("4x", 4, 1)
        };

        public static ReplayPlaybackSpeed NormalSpeed => PlaybackSpeeds[1];

        public static long ScaleElapsed(long elapsedMs, ReplayPlaybackSpeed speed, ref long remainder)
        {
            if (elapsedMs <= 0)
                return 0;

            speed ??= NormalSpeed;
            long scaled = elapsedMs * speed.Numerator + remainder;
            long result = scaled / speed.Denominator;
            remainder = scaled % speed.Denominator;
            return result;
        }

        public static int SnapshotInterval(ReplayPlaybackSpeed speed)
        {
            speed ??= NormalSpeed;
            return Math.Max(15, SnapshotBaseIntervalMs * speed.Denominator / speed.Numerator);
        }

        public static int PreviousEventIndex(int currentIndex, int eventCount)
        {
            if (eventCount <= 0)
                return -1;
            return Math.Max(-1, Math.Min(currentIndex, eventCount - 1) - 1);
        }

        public static int NextEventIndex(int currentIndex, int eventCount)
        {
            if (eventCount <= 0)
                return -1;
            return Math.Min(eventCount - 1, Math.Max(-1, currentIndex) + 1);
        }

        public static int PreviousInningIndex(IReadOnlyList<ReplayEvent> events, int currentIndex)
        {
            if (events == null || events.Count == 0 || currentIndex <= 0)
                return -1;

            currentIndex = Math.Min(currentIndex, events.Count - 1);
            int currentInning = events[currentIndex]?.Inning ?? 0;
            int previous = currentIndex - 1;
            while (previous >= 0 && (events[previous]?.Inning ?? 0) == currentInning)
                previous--;
            if (previous < 0)
                return -1;

            int previousInning = events[previous]?.Inning ?? 0;
            while (previous > 0 && (events[previous - 1]?.Inning ?? 0) == previousInning)
                previous--;
            return previous;
        }

        public static int NextInningIndex(IReadOnlyList<ReplayEvent> events, int currentIndex)
        {
            if (events == null || events.Count == 0)
                return -1;
            if (currentIndex < 0)
                return 0;

            currentIndex = Math.Min(currentIndex, events.Count - 1);
            int currentInning = events[currentIndex]?.Inning ?? 0;
            for (int index = currentIndex + 1; index < events.Count; index++)
            {
                if ((events[index]?.Inning ?? 0) != currentInning)
                    return index;
            }
            return -1;
        }

        public static bool IsTerminalEvent(int eventIndex, int eventCount)
            => eventCount > 0 && eventIndex == eventCount - 1;

        public static IReadOnlyList<int> RebuildEventIndexes(int targetIndex, int eventCount)
        {
            if (eventCount <= 0 || targetIndex < 0 || targetIndex >= eventCount)
                return Array.Empty<int>();
            return Enumerable.Range(0, targetIndex + 1).ToArray();
        }
    }
}
