#nullable enable annotations

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace StandaloneBaseball
{
    public sealed class GameplayForm : Form
    {
        private const int DefaultTickIntervalMilliseconds = 16;
        private const int SeniorStarterPitchLimit = 100;
        private const int ReliefPitcherMaxOuts = 6;
        private const int MidInningReliefBoostPercent = 25;
        private const int OneInningForcedRemovalRuns = 5;
        private const int TwoInningForcedRemovalRuns = 6;
        private const int ThreeInningForcedRemovalRuns = 7;
        private const int FieldingPenaltyPerError = 1;
        private const int ErrorFreeFieldingChancesForRecovery = 10;

        private enum PitcherBaserunnerSource
        {
            FatigueEligible,
            FieldingError
        }

        private enum PitcherRunCharge
        {
            Earned,
            UnearnedError
        }

        private enum LiveHitType
        {
            Single,
            Double,
            Triple,
            HomeRun
        }

        private enum OffensiveStrategyCall
        {
            Normal,
            SacrificeBunt,
            HitAndRun,
            Steal,
            Safe,
            Bunt,
            DoubleSteal
        }

        private enum DefensiveAlignmentCall
        {
            Normal,
            InfieldIn,
            DoublePlay,
            OutfieldIn,
            NoDoubles,
            WheelPlay
        }

        private readonly GameplayRenderingSurface _surface;
        private readonly GameplayRenderingGameState _state;
        private readonly RankingGameModifier _rankingModifier;
        private readonly GameplayInput _input = new GameplayInput();
        private readonly Random _rng = new Random();
        private readonly System.Windows.Forms.Timer _timer;
        private readonly LaunchSoundPlayer _playBallSound = new LaunchSoundPlayer();
        private readonly LaunchSoundPlayer _topHalfMusic = new LaunchSoundPlayer();
        private readonly LaunchSoundPlayer _bottomHalfMusic = new LaunchSoundPlayer();
        private readonly LaunchSoundPlayer _changeSideSound = new LaunchSoundPlayer();
        private readonly LaunchSoundPlayer _homeRunnersSound = new LaunchSoundPlayer();
        private readonly LaunchSoundPlayer _visitorRunnersSound = new LaunchSoundPlayer();
        private readonly LaunchSoundPlayer _runnerOnThirdSound = new LaunchSoundPlayer();
        private readonly LaunchSoundPlayer _scoredRunSound = new LaunchSoundPlayer();
        private readonly LaunchSoundPlayer _pitcherChangeSound = new LaunchSoundPlayer();
        private readonly LaunchSoundPlayer _pitchThrowSound = new LaunchSoundPlayer();
        private readonly LaunchSoundPlayer _batHitBallSound = new LaunchSoundPlayer();
        private readonly LaunchSoundPlayer _playEventSound = new LaunchSoundPlayer();
        private readonly LaunchSoundPlayer _takeYourBaseSound = new LaunchSoundPlayer();
        private readonly LaunchSoundPlayer _safeCallSound = new LaunchSoundPlayer();
        private readonly LaunchSoundPlayer _chanceBgmSound = new LaunchSoundPlayer();
        private readonly LaunchSoundPlayer _homeRunSound = new LaunchSoundPlayer();
        private readonly LaunchSoundPlayer _outCallSound = new LaunchSoundPlayer();
        private readonly LaunchSoundPlayer _visitorOutCheerSound = new LaunchSoundPlayer();
        private readonly LaunchSoundPlayer _gameOverSound = new LaunchSoundPlayer();
        private readonly LaunchSoundPlayer _topThirdSound = new LaunchSoundPlayer();
        private readonly LaunchSoundPlayer _topFourthSound = new LaunchSoundPlayer();
        private readonly LaunchSoundPlayer _topSeventhSound = new LaunchSoundPlayer();
        private readonly LaunchSoundPlayer _topFinalSound = new LaunchSoundPlayer();
        private readonly System.Windows.Forms.Timer _visitorOutCheerTimer = new System.Windows.Forms.Timer();
        private readonly System.Windows.Forms.Timer _gameOverTimer = new System.Windows.Forms.Timer();
        private readonly System.Windows.Forms.Timer _topThirdTimer = new System.Windows.Forms.Timer();
        private readonly System.Windows.Forms.Timer _topFourthTimer = new System.Windows.Forms.Timer();
        private readonly System.Windows.Forms.Timer _topSeventhTimer = new System.Windows.Forms.Timer();
        private readonly System.Windows.Forms.Timer _topFinalTimer = new System.Windows.Forms.Timer();
        private readonly Dictionary<Guid, ReliefPitcherFatigueState> _reliefPitcherFatigue = new Dictionary<Guid, ReliefPitcherFatigueState>();
        private readonly Dictionary<Guid, PitcherRunRuleState> _pitcherRunRules = new Dictionary<Guid, PitcherRunRuleState>();
        private readonly HashSet<Guid> _pitchersRemovedByRunRule = new HashSet<Guid>();
        private readonly List<PlayerGameLine> _liveLines = new List<PlayerGameLine>();
        private IReadOnlyList<GameplayScoredRunner> _pendingScoredRunners = Array.Empty<GameplayScoredRunner>();
        private readonly Dictionary<OffensiveStrategyCall, Button> _offensiveStrategyButtons = new Dictionary<OffensiveStrategyCall, Button>();
        private readonly Dictionary<DefensiveAlignmentCall, Button> _defensiveStrategyButtons = new Dictionary<DefensiveAlignmentCall, Button>();
        private readonly Dictionary<DefensiveStealCall, Button> _stealDefenseButtons = new Dictionary<DefensiveStealCall, Button>();
        private readonly List<int> _awayRunsByInning = new List<int>();
        private readonly List<int> _homeRunsByInning = new List<int>();
        private readonly List<GamePlayByPlayEntry> _playByPlay = new List<GamePlayByPlayEntry>();
        private readonly List<HalfInningSnapshot> _completedHalfInnings = new List<HalfInningSnapshot>();
        private readonly HashSet<string> _recordedHalfInnings = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Label _strategyStatusLabel = new Label();
        private readonly List<CutsceneDefinition> _leagueCutscenes = new List<CutsceneDefinition>();
        private readonly List<CutsceneDefinition> _awayCutscenes = new List<CutsceneDefinition>();
        private readonly List<CutsceneDefinition> _homeCutscenes = new List<CutsceneDefinition>();
        private NationalAnthemCutsceneDefault _nationalAnthemCutsceneDefault = NationalAnthemCutsceneDefault.CurrentGameSettings;
        private Button _intentionalWalkButton;
        private Button _moundVisitButton;
        private Button _saveGameButton;
        private ComboBox _inputModeCombo;
        private GameMode _mode = GameMode.UserVsCpu;
        private bool _paused;
        private bool _controllerWasConnected;
        private bool _playedPlayBall;
        private bool _pregameCeremonyStarted;
        private bool _topHalfMusicPlaying;
        private bool _bottomHalfMusicPlaying;
        private bool _ballInPlayMusicActive;
        private bool _pausedTopHalfMusicForBallInPlay;
        private bool _pausedBottomHalfMusicForBallInPlay;
        private bool _topThirdThemePlayed;
        private bool _topThirdThemePlaying;
        private bool _topFourthThemePlayed;
        private bool _topFourthThemePlaying;
        private bool _topSeventhThemePlayed;
        private bool _topSeventhThemePlaying;
        private bool _topFinalThemePlayed;
        private bool _topFinalThemePlaying;
        private bool _gameComplete;
        private bool _skipPregameCeremony;
        private readonly HashSet<string> _allStarPitchingAppliedKeys = new HashSet<string>();
        private List<string> _awayNationalAnthemImages = new List<string>();
        private List<string> _homeNationalAnthemImages = new List<string>();
        private string _awayLineupLogoPath = "";
        private string _homeLineupLogoPath = "";
        private int _watchTick;
        private Guid _gameplaySaveId = Guid.NewGuid();
        private PointF _ballStart;
        private PointF _ballTarget;
        private float _ballProgress;
        private int _pitchBreakSign = 1;
        private double _knuckleWobbleSeed;
        private Guid _awayStarterPitcherId;
        private Guid _homeStarterPitcherId;
        private Guid? _winningPitcherCandidateId;
        private Guid? _losingPitcherCandidateId;
        private bool _endedByMercyRule;
        private int _playableAwayLeftOnBase;
        private int _playableHomeLeftOnBase;
        private int _awayStarterPitchCount;
        private int _homeStarterPitchCount;
        private int _awayStarterPostLimitBaserunnersThisInning;
        private int _homeStarterPostLimitBaserunnersThisInning;
        private int _awayMoundVisitsThisInning;
        private int _homeMoundVisitsThisInning;
        private bool _awayCoachVisitBoostActive;
        private bool _homeCoachVisitBoostActive;
        private Guid _userControlledTeamId;
        private Guid _keyboardControlledTeamId;
        private Guid _controllerControlledTeamId;
        private GameplayPitchType _currentPitchType = GameplayPitchType.Fastball;
        private SharedBattedBallResultType? _pendingBattedBallResult;
        private GameplayInputSource? _activeInputSource;
        private DefensiveStealCall _defensiveStealCall = DefensiveStealCall.Normal;
        private int _pickoffAttemptsThisPlateAppearance;
        private OffensiveStrategyCall _offensiveStrategyCall = OffensiveStrategyCall.Normal;
        private DefensiveAlignmentCall _defensiveAlignmentCall = DefensiveAlignmentCall.Normal;

        private sealed class ReliefPitcherFatigueState
        {
            public int OutsRecorded { get; set; }
            public int PostLimitBaserunnersThisInning { get; set; }
            public bool FirstBatterBoostAvailable { get; set; }
            public bool FirstBatterFaced { get; set; }
            public bool AppearanceInitialized { get; set; }
            public bool EnteredInSaveSituation { get; set; }
            public bool EnteredWithThreeRunLead { get; set; }
            public bool EnteredWithTyingRunThreat { get; set; }
            public bool LeadPreserved { get; set; } = true;
        }

        private sealed class PitcherRunRuleState
        {
            public Dictionary<int, int> RunsAllowedByInning { get; set; } = new Dictionary<int, int>();
            public Dictionary<int, int> EarnedRunsAllowedByInning { get; set; } = new Dictionary<int, int>();
            public HashSet<int> FinalizedInnings { get; set; } = new HashSet<int>();
            public int ConsecutiveScorelessInnings { get; set; }
            public int AdvancementBoostPercent { get; set; }
            public bool EarnedRunReductionImmune { get; set; }
        }

        public GameResult? FinalResult { get; private set; }
        public Func<GameplayState, bool>? SaveRequested { get; set; }

        public GameplayForm()
            : this(null, null)
        {
        }

        public GameplayForm(Team? awayTeam, Team? homeTeam, RankingGameModifier? rankingModifier = null)
        {
            Text = "Gameplay";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(960, 720);
            MinimumSize = new Size(720, 560);
            KeyPreview = true;

            _state = new GameplayRenderingGameState();
            _rankingModifier = rankingModifier ?? RankingGameModifier.None;
            _userControlledTeamId = awayTeam?.Id ?? Guid.Empty;
            _keyboardControlledTeamId = awayTeam?.Id ?? Guid.Empty;
            _controllerControlledTeamId = homeTeam?.Id ?? Guid.Empty;
            _surface = new GameplayRenderingSurface { Dock = DockStyle.Fill };
            BuildGameplayLayout();

            _timer = new System.Windows.Forms.Timer { Interval = DefaultTickIntervalMilliseconds };
            _timer.Tick += (s, e) => FixedTick();
            _visitorOutCheerTimer.Tick += (s, e) =>
            {
                _visitorOutCheerTimer.Stop();
                _visitorOutCheerSound.PlayOnce(LaunchSoundPlayer.FindVisitorOutCrowdCheer());
            };
            _gameOverTimer.Tick += (s, e) =>
            {
                _gameOverTimer.Stop();
                _gameOverSound.PlayOnce(LaunchSoundPlayer.FindGameOver());
            };
            _topThirdTimer.Tick += (s, e) =>
            {
                _topThirdTimer.Stop();
                _topThirdThemePlaying = false;
                UpdateInningMusic();
            };
            _topFourthTimer.Tick += (s, e) =>
            {
                _topFourthTimer.Stop();
                _topFourthThemePlaying = false;
                UpdateInningMusic();
            };
            _topSeventhTimer.Tick += (s, e) =>
            {
                _topSeventhTimer.Stop();
                _topSeventhThemePlaying = false;
                UpdateInningMusic();
            };
            _topFinalTimer.Tick += (s, e) =>
            {
                _topFinalTimer.Stop();
                _topFinalThemePlaying = false;
                UpdateInningMusic();
            };

            LoadMatch(awayTeam, homeTeam);
            _timer.Start();
        }

        public void SetUserControlledTeam(Team? team)
        {
            if (team != null && (team.Id == _state.AwayTeam?.Id || team.Id == _state.HomeTeam?.Id))
            {
                _userControlledTeamId = team.Id;
                RefreshInputModeComboSelection();
            }
        }

        public void SetCutscenes(IEnumerable<CutsceneDefinition>? cutscenes)
            => SetCutscenes(cutscenes, null, null);

        public void SetNationalAnthemCutsceneDefault(NationalAnthemCutsceneDefault defaultMode)
        {
            _nationalAnthemCutsceneDefault = Enum.IsDefined(typeof(NationalAnthemCutsceneDefault), defaultMode)
                ? defaultMode
                : NationalAnthemCutsceneDefault.CurrentGameSettings;
        }

        public void SetCutscenes(IEnumerable<CutsceneDefinition>? leagueCutscenes, IEnumerable<CutsceneDefinition>? awayCutscenes, IEnumerable<CutsceneDefinition>? homeCutscenes)
        {
            _leagueCutscenes.Clear();
            _awayCutscenes.Clear();
            _homeCutscenes.Clear();
            if (leagueCutscenes != null)
                _leagueCutscenes.AddRange(leagueCutscenes.Where(c => c != null));
            if (awayCutscenes != null)
                _awayCutscenes.AddRange(awayCutscenes.Where(c => c != null));
            if (homeCutscenes != null)
                _homeCutscenes.AddRange(homeCutscenes.Where(c => c != null));
        }

        private void TriggerCutscene(CutsceneTrigger trigger)
            => TriggerCutscene(trigger, null);

        private void TriggerCutscene(CutsceneTrigger trigger, Team? primaryTeam)
        {
            var cutscenes = CutscenesForTrigger(trigger, primaryTeam).ToList();
            if (cutscenes.Count == 0)
                return;

            bool timerWasEnabled = _timer.Enabled;
            if (timerWasEnabled)
                _timer.Stop();
            try
            {
                CutscenePlaybackForm.PlayFirst(this, cutscenes, trigger);
            }
            finally
            {
                if (timerWasEnabled && !_gameComplete)
                    _timer.Start();
            }
        }

        private IEnumerable<CutsceneDefinition> CutscenesForTrigger(CutsceneTrigger trigger, Team? primaryTeam)
        {
            if (primaryTeam != null)
            {
                if (_state?.AwayTeam?.Id == primaryTeam.Id)
                {
                    foreach (var cutscene in TeamCutscenesForUniform(_awayCutscenes, homeUniform: false, _state.UniformForTeam(_state.AwayTeam)?.Category)) yield return cutscene;
                }
                else if (_state?.HomeTeam?.Id == primaryTeam.Id)
                {
                    foreach (var cutscene in TeamCutscenesForUniform(_homeCutscenes, homeUniform: true, _state.UniformForTeam(_state.HomeTeam)?.Category)) yield return cutscene;
                }
            }

            if (CutsceneCatalog.IsTeamOnly(trigger))
                yield break;
            foreach (var cutscene in _leagueCutscenes)
                yield return cutscene;
        }

        private static IEnumerable<CutsceneDefinition> TeamCutscenesForUniform(IEnumerable<CutsceneDefinition> cutscenes, bool homeUniform, TeamUniformCategory? selectedCategory = null)
        {
            if (cutscenes == null)
                yield break;

            var order = CutsceneUniformOrder(homeUniform, selectedCategory);
            foreach (var folder in order)
            {
                foreach (var cutscene in cutscenes.Where(c => c != null && c.UniformFolder == folder))
                    yield return cutscene;
            }
        }

        private static TeamCutsceneUniformFolder[] CutsceneUniformOrder(bool homeUniform, TeamUniformCategory? selectedCategory)
        {
            if (selectedCategory.HasValue)
            {
                var selected = selectedCategory.Value switch
                {
                    TeamUniformCategory.Home => TeamCutsceneUniformFolder.Home,
                    TeamUniformCategory.HomeAlternate => TeamCutsceneUniformFolder.HomeAlternate,
                    TeamUniformCategory.Visitor => TeamCutsceneUniformFolder.Visitor,
                    TeamUniformCategory.VisitorAlternate => TeamCutsceneUniformFolder.VisitorAlternate,
                    _ => TeamCutsceneUniformFolder.Any
                };
                var fallback = homeUniform
                    ? selected == TeamCutsceneUniformFolder.Home ? TeamCutsceneUniformFolder.HomeAlternate : TeamCutsceneUniformFolder.Home
                    : selected == TeamCutsceneUniformFolder.Visitor ? TeamCutsceneUniformFolder.VisitorAlternate : TeamCutsceneUniformFolder.Visitor;
                return new[] { selected, fallback, TeamCutsceneUniformFolder.Any };
            }

            return homeUniform
                ? new[] { TeamCutsceneUniformFolder.Home, TeamCutsceneUniformFolder.HomeAlternate, TeamCutsceneUniformFolder.Any }
                : new[] { TeamCutsceneUniformFolder.Visitor, TeamCutsceneUniformFolder.VisitorAlternate, TeamCutsceneUniformFolder.Any };
        }

        public void SetPlayerVsPlayerInputAssignments(Team keyboardTeam, Team controllerTeam)
        {
            if (keyboardTeam != null && (keyboardTeam.Id == _state.AwayTeam?.Id || keyboardTeam.Id == _state.HomeTeam?.Id))
                _keyboardControlledTeamId = keyboardTeam.Id;
            if (controllerTeam != null && (controllerTeam.Id == _state.AwayTeam?.Id || controllerTeam.Id == _state.HomeTeam?.Id))
                _controllerControlledTeamId = controllerTeam.Id;
            if (_keyboardControlledTeamId == _controllerControlledTeamId)
            {
                _controllerControlledTeamId = _keyboardControlledTeamId == _state.AwayTeam?.Id
                    ? _state.HomeTeam?.Id ?? Guid.Empty
                    : _state.AwayTeam?.Id ?? Guid.Empty;
            }
        }

        private void RefreshInputModeComboItems()
        {
            if (_inputModeCombo == null)
                return;

            _inputModeCombo.Items.Clear();
            _inputModeCombo.Items.Add(new LiveInputModeItem { Mode = GameMode.CpuVsCpuWatch, Text = "CPU vs CPU Watch" });
            if (_state.AwayTeam != null)
                _inputModeCombo.Items.Add(new LiveInputModeItem { Mode = GameMode.UserVsCpu, TeamId = _state.AwayTeam.Id, Text = "User vs CPU - " + _state.AwayTeam.ScoreboardName });
            if (_state.HomeTeam != null)
                _inputModeCombo.Items.Add(new LiveInputModeItem { Mode = GameMode.UserVsCpu, TeamId = _state.HomeTeam.Id, Text = "User vs CPU - " + _state.HomeTeam.ScoreboardName });
            if (_state.AwayTeam != null && _state.HomeTeam != null)
                _inputModeCombo.Items.Add(new LiveInputModeItem { Mode = GameMode.PlayerVsPlayer, Text = "Player vs Player" });
            _inputModeCombo.Items.Add(new LiveInputModeItem { Mode = GameMode.QuickSim, Text = "Sim To Finish" });
            RefreshInputModeComboSelection();
        }

        private void RefreshInputModeComboSelection()
        {
            if (_inputModeCombo == null)
                return;

            foreach (var item in _inputModeCombo.Items.OfType<LiveInputModeItem>())
            {
                bool selected = item.Mode == _mode &&
                    (item.Mode != GameMode.UserVsCpu || item.TeamId == _userControlledTeamId);
                if (selected)
                {
                    _inputModeCombo.SelectedItem = item;
                    return;
                }
            }

            if (_inputModeCombo.Items.Count > 0 && _inputModeCombo.SelectedIndex < 0)
                _inputModeCombo.SelectedIndex = 0;
        }

        private void ApplyLiveInputMode(LiveInputModeItem item)
        {
            if (item == null)
                return;

            _mode = item.Mode;
            if (item.Mode == GameMode.UserVsCpu && item.TeamId != Guid.Empty)
                _userControlledTeamId = item.TeamId;

            _timer.Interval = item.Mode == GameMode.QuickSim ? 1 : DefaultTickIntervalMilliseconds;
            _state.ModeLabel = item.Mode switch
            {
                GameMode.CpuVsCpuWatch => "CPU vs CPU watch",
                GameMode.QuickSim => "Sim to finish",
                GameMode.UserVsCpu => "User controls " + ((_state.AwayTeam?.Id == _userControlledTeamId ? _state.AwayTeam : _state.HomeTeam)?.ScoreboardName ?? "team"),
                _ => "Player vs Player"
            };
            UpdateStrategyMenuState();
            _surface.Invalidate();
        }

        private void BuildGameplayLayout()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 230));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.Controls.Add(_surface, 0, 0);

            var panel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = Color.FromArgb(18, 24, 32),
                Padding = new Padding(8)
            };

            _strategyStatusLabel.AutoSize = false;
            _strategyStatusLabel.Width = 198;
            _strategyStatusLabel.Height = 54;
            _strategyStatusLabel.ForeColor = Color.White;
            _strategyStatusLabel.Text = "Strategy";
            _strategyStatusLabel.TextAlign = ContentAlignment.MiddleLeft;
            panel.Controls.Add(_strategyStatusLabel);

            var offense = AddStrategyGroup(panel, "Offense");
            AddOffensiveStrategyButton(offense, OffensiveStrategyCall.Safe, "Safe");
            AddOffensiveStrategyButton(offense, OffensiveStrategyCall.HitAndRun, "Hit && Run");
            AddOffensiveStrategyButton(offense, OffensiveStrategyCall.Steal, "Steal");
            AddOffensiveStrategyButton(offense, OffensiveStrategyCall.DoubleSteal, "Double Steal");
            AddOffensiveStrategyButton(offense, OffensiveStrategyCall.Bunt, "Bunt");
            AddOffensiveStrategyButton(offense, OffensiveStrategyCall.SacrificeBunt, "Sac Bunt");

            var defense = AddStrategyGroup(panel, "Defense");
            AddDefensiveStrategyButton(defense, DefensiveAlignmentCall.Normal, "Normal");
            AddDefensiveStrategyButton(defense, DefensiveAlignmentCall.InfieldIn, "Infield In");
            AddDefensiveStrategyButton(defense, DefensiveAlignmentCall.DoublePlay, "Double Play");
            AddDefensiveStrategyButton(defense, DefensiveAlignmentCall.OutfieldIn, "Outfield In");
            AddDefensiveStrategyButton(defense, DefensiveAlignmentCall.NoDoubles, "No Doubles");
            AddDefensiveStrategyButton(defense, DefensiveAlignmentCall.WheelPlay, "Wheel Play");
            _intentionalWalkButton = AddStrategyButton(defense, "Intentional Walk", () => ResolveIntentionalWalk(), null);

            var stealDefense = AddStrategyGroup(panel, "Steal Defense");
            AddStealDefenseButton(stealDefense, DefensiveStealCall.HoldRunner, "Hold Runner");
            AddStealDefenseButton(stealDefense, DefensiveStealCall.SlideStep, "Slide Step");
            AddStealDefenseButton(stealDefense, DefensiveStealCall.Pitchout, "Pitchout");
            AddStealDefenseButton(stealDefense, DefensiveStealCall.Pickoff, "Pickoff");

            var coaching = AddStrategyGroup(panel, "Coaching");
            _moundVisitButton = AddStrategyButton(coaching, "Mound Visit", () => TryMoundVisit(), null);

            var gameControls = AddStrategyGroup(panel, "Game");
            _saveGameButton = AddStrategyButton(gameControls, "Save In-Game", RequestInGameSave, null);
            _inputModeCombo = new ComboBox
            {
                Width = 176,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Margin = new Padding(0, 4, 0, 0)
            };
            RefreshInputModeComboItems();
            _inputModeCombo.SelectedIndexChanged += (s, e) =>
            {
                if (_inputModeCombo.SelectedItem is LiveInputModeItem item)
                    ApplyLiveInputMode(item);
            };
            gameControls.Controls.Add(_inputModeCombo);

            root.Controls.Add(panel, 1, 0);
            Controls.Add(root);
        }

        private sealed class LiveInputModeItem
        {
            public GameMode Mode { get; set; }
            public Guid TeamId { get; set; }
            public string Text { get; set; } = "";
            public override string ToString() => Text;
        }

        private static FlowLayoutPanel AddStrategyGroup(Control parent, string title)
        {
            var box = new GroupBox
            {
                Text = title,
                ForeColor = Color.White,
                Width = 205,
                AutoSize = true,
                Padding = new Padding(8),
                Margin = new Padding(0, 8, 0, 0)
            };
            var layout = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                Dock = DockStyle.Fill
            };
            box.Controls.Add(layout);
            parent.Controls.Add(box);
            return layout;
        }

        private void AddOffensiveStrategyButton(Control parent, OffensiveStrategyCall call, string text)
        {
            _offensiveStrategyButtons[call] = AddStrategyButton(parent, text, () =>
            {
                SetOffensiveStrategy(call);
                if (call == OffensiveStrategyCall.Steal)
                    TryResolveStealAttempt(userInitiated: true);
                else if (call == OffensiveStrategyCall.DoubleSteal)
                    TryResolveDoubleStealAttempt(userInitiated: true);
            }, null);
        }

        private void AddDefensiveStrategyButton(Control parent, DefensiveAlignmentCall call, string text)
        {
            _defensiveStrategyButtons[call] = AddStrategyButton(parent, text, () => SetDefensiveAlignment(call), null);
        }

        private void AddStealDefenseButton(Control parent, DefensiveStealCall call, string text)
        {
            _stealDefenseButtons[call] = AddStrategyButton(parent, text, () =>
            {
                if (call == DefensiveStealCall.Pickoff)
                    TryResolvePickoffMove();
                else
                    SetDefensiveStealCall(call);
            }, null);
        }

        private Button AddStrategyButton(Control parent, string text, Action action, string? tooltip)
        {
            var button = new Button
            {
                Text = text,
                Width = 176,
                Height = 28,
                Margin = new Padding(0, 3, 0, 0),
                BackColor = SystemColors.Control,
                UseVisualStyleBackColor = true
            };
            button.Click += (s, e) =>
            {
                action?.Invoke();
                UpdateStrategyMenuState();
                _surface.Focus();
            };
            parent.Controls.Add(button);
            return button;
        }

        private void UpdateStrategyMenuState()
        {
            bool canChoose = CanChooseStrategy();
            bool hasAnyRunner = _state.Bases.Any(b => b.Occupied);
            bool hasThird = _state.Bases.Length >= 3 && _state.Bases[2].Occupied;
            bool hasFirst = _state.Bases.Length >= 1 && _state.Bases[0].Occupied;

            SetButtonState(_offensiveStrategyButtons, OffensiveStrategyCall.Safe, canChoose && IsOffensiveStrategyAvailable(OffensiveStrategyCall.Safe));
            SetButtonState(_offensiveStrategyButtons, OffensiveStrategyCall.HitAndRun, canChoose && IsOffensiveStrategyAvailable(OffensiveStrategyCall.HitAndRun));
            SetButtonState(_offensiveStrategyButtons, OffensiveStrategyCall.Steal, canChoose && IsOffensiveStrategyAvailable(OffensiveStrategyCall.Steal));
            SetButtonState(_offensiveStrategyButtons, OffensiveStrategyCall.DoubleSteal, canChoose && IsOffensiveStrategyAvailable(OffensiveStrategyCall.DoubleSteal));
            SetButtonState(_offensiveStrategyButtons, OffensiveStrategyCall.Bunt, canChoose && IsOffensiveStrategyAvailable(OffensiveStrategyCall.Bunt));
            SetButtonState(_offensiveStrategyButtons, OffensiveStrategyCall.SacrificeBunt, canChoose && IsOffensiveStrategyAvailable(OffensiveStrategyCall.SacrificeBunt));

            SetButtonState(_defensiveStrategyButtons, DefensiveAlignmentCall.Normal, canChoose && IsDefensiveAlignmentAvailable(DefensiveAlignmentCall.Normal));
            SetButtonState(_defensiveStrategyButtons, DefensiveAlignmentCall.InfieldIn, canChoose && IsDefensiveAlignmentAvailable(DefensiveAlignmentCall.InfieldIn));
            SetButtonState(_defensiveStrategyButtons, DefensiveAlignmentCall.DoublePlay, canChoose && IsDefensiveAlignmentAvailable(DefensiveAlignmentCall.DoublePlay));
            SetButtonState(_defensiveStrategyButtons, DefensiveAlignmentCall.OutfieldIn, canChoose && IsDefensiveAlignmentAvailable(DefensiveAlignmentCall.OutfieldIn));
            SetButtonState(_defensiveStrategyButtons, DefensiveAlignmentCall.NoDoubles, canChoose && IsDefensiveAlignmentAvailable(DefensiveAlignmentCall.NoDoubles));
            SetButtonState(_defensiveStrategyButtons, DefensiveAlignmentCall.WheelPlay, canChoose && IsDefensiveAlignmentAvailable(DefensiveAlignmentCall.WheelPlay));
            SetButtonState(_intentionalWalkButton, canChoose && _state.Phase != GameplayRenderingPhase.Pitching);

            SetButtonState(_stealDefenseButtons, DefensiveStealCall.HoldRunner, canChoose && HasStealDefenseRunner());
            SetButtonState(_stealDefenseButtons, DefensiveStealCall.SlideStep, canChoose && FindLeadStealCandidate().Runner != null);
            SetButtonState(_stealDefenseButtons, DefensiveStealCall.Pitchout, canChoose && FindLeadStealCandidate().Runner != null);
            SetButtonState(_stealDefenseButtons, DefensiveStealCall.Pickoff, canChoose && hasAnyRunner);
            SetButtonState(_moundVisitButton, canChoose && _state.Phase != GameplayRenderingPhase.Pitching && CurrentFieldingTeamMoundVisits() < 2);
            SetButtonState(_saveGameButton, !_gameComplete);

            HighlightButton(_offensiveStrategyButtons, _offensiveStrategyCall);
            HighlightButton(_defensiveStrategyButtons, _defensiveAlignmentCall);
            HighlightButton(_stealDefenseButtons, _defensiveStealCall);

            string owner = _mode == GameMode.CpuVsCpuWatch ? "CPU watch" :
                _mode == GameMode.QuickSim ? "Sim" : "Manual";
            _strategyStatusLabel.Text = owner + Environment.NewLine +
                "Off: " + OffensiveStrategyCallLabel(_offensiveStrategyCall) + Environment.NewLine +
                "Def: " + DefensiveAlignmentCallLabel(_defensiveAlignmentCall);
        }

        private bool CanChooseStrategy()
        {
            return !_gameComplete &&
                _mode != GameMode.CpuVsCpuWatch &&
                _mode != GameMode.QuickSim &&
                (_state.Phase == GameplayRenderingPhase.Ready ||
                    _state.Phase == GameplayRenderingPhase.DeadBall ||
                    _state.Phase == GameplayRenderingPhase.Pitching);
        }

        private bool IsOffensiveStrategyAvailable(OffensiveStrategyCall call)
        {
            bool hasAnyRunner = _state.Bases.Any(baseState => baseState.Occupied);
            return call switch
            {
                OffensiveStrategyCall.Safe => true,
                OffensiveStrategyCall.HitAndRun => HasHitAndRunRunner(),
                OffensiveStrategyCall.Steal => FindLeadStealCandidate().Runner != null,
                OffensiveStrategyCall.DoubleSteal => FindDoubleStealCandidates().Count >= 2,
                OffensiveStrategyCall.Bunt => true,
                OffensiveStrategyCall.SacrificeBunt => hasAnyRunner && _state.Outs < 2,
                _ => true
            };
        }

        private bool IsDefensiveAlignmentAvailable(DefensiveAlignmentCall call)
        {
            bool hasAnyRunner = _state.Bases.Any(baseState => baseState.Occupied);
            bool hasThird = _state.Bases.Length >= 3 && _state.Bases[2].Occupied;
            bool hasFirst = _state.Bases.Length >= 1 && _state.Bases[0].Occupied;
            return call switch
            {
                DefensiveAlignmentCall.InfieldIn => hasThird && _state.Outs < 2,
                DefensiveAlignmentCall.DoublePlay => hasFirst && _state.Outs < 2,
                DefensiveAlignmentCall.OutfieldIn => hasThird && _state.Outs < 2,
                DefensiveAlignmentCall.WheelPlay => hasAnyRunner && _state.Outs < 2,
                _ => true
            };
        }

        private bool HasHitAndRunRunner()
        {
            if (_state.Outs >= 2 || _state.Bases == null)
                return false;

            for (int i = 0; i < _state.Bases.Length; i++)
            {
                var baseState = _state.Bases[i];
                if (!baseState.Occupied || baseState.Player == null)
                    continue;

                int targetBase = i + 2;
                if (targetBase >= 4 || !_state.Bases[targetBase - 1].Occupied)
                    return true;
            }

            return false;
        }

        private bool HasStealDefenseRunner()
        {
            return _state.Bases.Any(baseState => baseState.Occupied && baseState.Player != null);
        }

        private static void SetButtonState<TKey>(Dictionary<TKey, Button> buttons, TKey key, bool enabled) where TKey : notnull
        {
            if (buttons.TryGetValue(key, out var button))
                SetButtonState(button, enabled);
        }

        private static void SetButtonState(Button? button, bool enabled)
        {
            if (button == null)
                return;
            button.Enabled = enabled;
        }

        private static void HighlightButton<TKey>(Dictionary<TKey, Button> buttons, TKey active) where TKey : notnull
        {
            foreach (var pair in buttons)
            {
                bool selected = EqualityComparer<TKey>.Default.Equals(pair.Key, active);
                pair.Value.UseVisualStyleBackColor = !selected;
                pair.Value.BackColor = selected ? Color.FromArgb(255, 216, 96) : SystemColors.Control;
            }
        }

        public Control GameSurface => _surface;
        public int TickIntervalMilliseconds => _timer.Interval;
        public bool IsGameLoopRunning => _timer.Enabled;
        public string ModeLabel => _state.ModeLabel;

        public void StartGameLoop()
        {
            if (!_timer.Enabled)
                _timer.Start();
        }

        public void StopGameLoop()
        {
            if (_timer.Enabled)
                _timer.Stop();
        }

        public void LoadMatch(Team? awayTeam, Team? homeTeam)
        {
            if (awayTeam != null && homeTeam != null)
            {
                _state.SetTeams(awayTeam, homeTeam);
            }
            else
            {
                _state.InitializeLineups();
                _state.SeedFielders();
                _state.ClearBases();
            }
            ResetGame();
            RefreshInputModeComboItems();
        }

        public void ResetGame()
        {
            _state.AwayScore = 0;
            _state.HomeScore = 0;
            FinalResult = null;
            _gameComplete = false;
            _endedByMercyRule = false;
            _winningPitcherCandidateId = null;
            _losingPitcherCandidateId = null;
            _liveLines.Clear();
            _awayRunsByInning.Clear();
            _homeRunsByInning.Clear();
            _playByPlay.Clear();
            _completedHalfInnings.Clear();
            _recordedHalfInnings.Clear();
            _playableAwayLeftOnBase = 0;
            _playableHomeLeftOnBase = 0;
            _state.InitializeLineups();
            _state.AwayPitcherIndex = GameplayRules.FindStartingPitcherIndex(_state.AwayTeam);
            _state.HomePitcherIndex = GameplayRules.FindStartingPitcherIndex(_state.HomeTeam);
            _state.AwayEmergencyPitcherId = null;
            _state.HomeEmergencyPitcherId = null;
            InitializeStarterFatigueTracking();
            _state.Inning = 1;
            _state.TopHalf = true;
            _state.ResetCount();
            _state.Outs = 0;
            _state.ClearBases();
            _state.SeedFielders();
            _defensiveStealCall = DefensiveStealCall.Normal;
            _pickoffAttemptsThisPlateAppearance = 0;
            _awayMoundVisitsThisInning = 0;
            _homeMoundVisitsThisInning = 0;
            _awayCoachVisitBoostActive = false;
            _homeCoachVisitBoostActive = false;
            _state.BallVisible = true;
            _state.BallTrail = 0f;
            _state.BallPosition = new PointF(0.5f, 0.62f);
            _state.Phase = GameplayRenderingPhase.Ready;
            _state.ModeLabel = "Ready";
            _surface.SetState(_state);
            UpdateStrategyMenuState();
        }

        public void SetModeLabel(string label)
        {
            _state.ModeLabel = string.IsNullOrWhiteSpace(label) ? "Ready" : label.Trim();
            _surface.Invalidate();
        }

        public void SetReplayPlaybackMode()
        {
            _skipPregameCeremony = true;
            _pregameCeremonyStarted = true;
            _paused = true;
            StopGameLoop();
            RefreshInputModeComboItems();
        }

        internal void ApplyExactReplayFrame(ReplayRenderFrame frame)
        {
            if (frame == null)
                return;

            _state.Phase = frame.Actors.Count > 0 ? GameplayRenderingPhase.BallInPlay : GameplayRenderingPhase.Pitching;
            _state.BallVisible = frame.BallVisible;
            _state.BallPosition = new PointF(Math.Clamp(frame.BallX, 0f, 1f), Math.Clamp(frame.BallY, 0f, 1f));
            _state.BallHeight = Math.Max(0f, frame.BallZ);
            _state.BallTrail = frame.BallVisible ? Math.Max(0f, 1f - frame.Progress) : 0f;
            _state.ReplayActors.Clear();

            foreach (ReplayRenderActor actor in frame.Actors)
            {
                if (!actor.Runner && !string.IsNullOrWhiteSpace(actor.DefensivePosition))
                {
                    int index = _state.Fielders.FindIndex(marker =>
                        string.Equals(marker.Label, actor.DefensivePosition, StringComparison.OrdinalIgnoreCase));
                    if (index >= 0)
                    {
                        _state.Fielders[index].Position = new PointF(actor.X, actor.Y);
                        if (actor.Player != null)
                            _state.Fielders[index].Player = actor.Player;
                        if (actor.Team != null)
                            _state.Fielders[index].Team = actor.Team;
                        if (actor.Highlighted)
                            _state.ActiveFielderIndex = index;
                        continue;
                    }
                }

                _state.ReplayActors.Add(new GameplayRenderingPlayerMarker
                {
                    Label = actor.Runner ? "R" : actor.DefensivePosition,
                    Detail = actor.Highlighted ? "highlight" : "",
                    Position = new PointF(actor.X, actor.Y),
                    Color = actor.Runner ? _state.OffenseColor : _state.DefenseColor,
                    Runner = actor.Runner,
                    Player = actor.Player,
                    Team = actor.Team ?? (actor.Runner ? _state.BattingTeam : _state.FieldingTeam)
                });
            }
            _surface.Invalidate();
        }

        internal void ApplyReplayFielderPositions(IEnumerable<ReplayRenderActor> actors)
        {
            foreach (ReplayRenderActor actor in actors ?? Enumerable.Empty<ReplayRenderActor>())
            {
                int index = _state.Fielders.FindIndex(marker =>
                    string.Equals(marker.Label, actor.DefensivePosition, StringComparison.OrdinalIgnoreCase));
                if (index < 0)
                    continue;
                _state.Fielders[index].Position = new PointF(Math.Clamp(actor.X, 0f, 1f), Math.Clamp(actor.Y, 0f, 1f));
                if (actor.Player != null)
                    _state.Fielders[index].Player = actor.Player;
                if (actor.Team != null)
                    _state.Fielders[index].Team = actor.Team;
            }
            _surface.Invalidate();
        }

        internal void ApplyReplayScore(ReplayScore? score)
        {
            if (score == null)
                return;
            _state.AwayScore = Math.Max(0, score.Away);
            _state.HomeScore = Math.Max(0, score.Home);
            _surface.Invalidate();
        }

        private void RequestInGameSave()
        {
            bool wasPaused = _paused;
            _paused = true;
            try
            {
                bool saved = SaveRequested?.Invoke(CreateGameplayStateSnapshot()) == true;
                _state.ModeLabel = saved ? "In-game save complete" : "In-game save canceled";
            }
            finally
            {
                _paused = wasPaused;
                UpdateStrategyMenuState();
                _surface.Invalidate();
            }
        }

        internal GameplayState CreateGameplayStateSnapshot()
        {
            var snapshot = new GameplayState
            {
                Id = _gameplaySaveId,
                Mode = _mode,
                UserControlledTeamId = _userControlledTeamId,
                KeyboardControlledTeamId = _keyboardControlledTeamId,
                ControllerControlledTeamId = _controllerControlledTeamId,
                AwayTeam = _state.AwayTeam,
                HomeTeam = _state.HomeTeam,
                FieldPresetId = _state.FieldPreset?.Id ?? BaseballFieldPresets.Default.Id,
                AwayUniformSetId = _state.AwayUniformSetId,
                HomeUniformSetId = _state.HomeUniformSetId,
                RegulationInnings = _state.RegulationInnings,
                AllowExtraInnings = _state.AllowExtraInnings,
                MercyRuleEnabled = _state.MercyRuleEnabled,
                MercyRuleRuns = _state.MercyRuleRuns,
                MercyRuleMinimumInning = _state.MercyRuleMinimumInning,
                EndedByMercyRule = _endedByMercyRule,
                ExtraInningRunnerOnSecond = _state.ExtraInningRunnerOnSecond,
                CourtesyRunnerForPitchersCatchers = _state.CourtesyRunnerForPitchersCatchers,
                Inning = _state.Inning,
                Half = _state.TopHalf ? HalfInning.Top : HalfInning.Bottom,
                Count = new CountState
                {
                    Balls = _state.Balls,
                    Strikes = _state.Strikes,
                    Outs = _state.Outs
                },
                Bases = new BaseState
                {
                    First = SnapshotBaseRunner(0),
                    Second = SnapshotBaseRunner(1),
                    Third = SnapshotBaseRunner(2)
                },
                AwayScore = _state.AwayScore,
                HomeScore = _state.HomeScore,
                AwayBatterIndex = _state.AwayBatterIndex,
                HomeBatterIndex = _state.HomeBatterIndex,
                AwayPitcherIndex = _state.AwayPitcherIndex,
                HomePitcherIndex = _state.HomePitcherIndex,
                IsComplete = _gameComplete,
                AwayDesignatedHitterId = _state.AwayDesignatedHitterId,
                HomeDesignatedHitterId = _state.HomeDesignatedHitterId,
                AwayDhActive = _state.AwayDhActive,
                HomeDhActive = _state.HomeDhActive,
                AwayRunsByInning = _awayRunsByInning.ToList(),
                HomeRunsByInning = _homeRunsByInning.ToList(),
                AwayLeftOnBase = _playableAwayLeftOnBase,
                HomeLeftOnBase = _playableHomeLeftOnBase,
                PlayByPlay = _playByPlay.Select(ClonePlayByPlayEntry).ToList(),
                CompletedHalfInnings = _completedHalfInnings.Select(CloneHalfInningSnapshot).ToList(),
                LiveRules = CreateLiveRulesSnapshot()
            };

            snapshot.AwayLineupPlayerIds.AddRange(_state.AwayLineupPlayerIds);
            snapshot.HomeLineupPlayerIds.AddRange(_state.HomeLineupPlayerIds);
            foreach (var item in _state.PinchUseCounts)
                snapshot.PinchUseCounts[item.Key] = item.Value;
            snapshot.RemovedPlayerIds.AddRange(_state.RemovedPlayerIds);
            snapshot.LiveLines.AddRange(_liveLines.Where(HasLiveStats).Select(CloneGameLine));
            return snapshot;
        }

        private GameplayLiveRulesState CreateLiveRulesSnapshot()
        {
            return new GameplayLiveRulesState
            {
                AwayStarterPitcherId = _awayStarterPitcherId,
                HomeStarterPitcherId = _homeStarterPitcherId,
                AwayEmergencyPitcherId = _state.AwayEmergencyPitcherId,
                HomeEmergencyPitcherId = _state.HomeEmergencyPitcherId,
                WinningPitcherCandidateId = _winningPitcherCandidateId,
                LosingPitcherCandidateId = _losingPitcherCandidateId,
                AwayStarterPitchCount = _awayStarterPitchCount,
                HomeStarterPitchCount = _homeStarterPitchCount,
                AwayStarterPostLimitBaserunnersThisInning = _awayStarterPostLimitBaserunnersThisInning,
                HomeStarterPostLimitBaserunnersThisInning = _homeStarterPostLimitBaserunnersThisInning,
                AwayMoundVisitsThisInning = _awayMoundVisitsThisInning,
                HomeMoundVisitsThisInning = _homeMoundVisitsThisInning,
                AwayCoachVisitBoostActive = _awayCoachVisitBoostActive,
                HomeCoachVisitBoostActive = _homeCoachVisitBoostActive,
                PitchersRemovedByRunRule = _pitchersRemovedByRunRule.ToList(),
                ReliefPitcherFatigue = _reliefPitcherFatigue.ToDictionary(
                    item => item.Key,
                    item => new GameplayReliefPitcherState
                    {
                        OutsRecorded = item.Value.OutsRecorded,
                        PostLimitBaserunnersThisInning = item.Value.PostLimitBaserunnersThisInning,
                        FirstBatterBoostAvailable = item.Value.FirstBatterBoostAvailable,
                        FirstBatterFaced = item.Value.FirstBatterFaced,
                        AppearanceInitialized = item.Value.AppearanceInitialized,
                        EnteredInSaveSituation = item.Value.EnteredInSaveSituation,
                        EnteredWithThreeRunLead = item.Value.EnteredWithThreeRunLead,
                        EnteredWithTyingRunThreat = item.Value.EnteredWithTyingRunThreat,
                        LeadPreserved = item.Value.LeadPreserved
                    }),
                PitcherRunRules = _pitcherRunRules.ToDictionary(
                    item => item.Key,
                    item => new GameplayPitcherRunRuleState
                    {
                        RunsAllowedByInning = new Dictionary<int, int>(item.Value.RunsAllowedByInning),
                        EarnedRunsAllowedByInning = new Dictionary<int, int>(item.Value.EarnedRunsAllowedByInning),
                        FinalizedInnings = new HashSet<int>(item.Value.FinalizedInnings),
                        ConsecutiveScorelessInnings = item.Value.ConsecutiveScorelessInnings,
                        AdvancementBoostPercent = item.Value.AdvancementBoostPercent,
                        EarnedRunReductionImmune = item.Value.EarnedRunReductionImmune
                    })
            };
        }

        private static GamePlayByPlayEntry ClonePlayByPlayEntry(GamePlayByPlayEntry entry)
        {
            if (entry == null)
                return null;

            return new GamePlayByPlayEntry
            {
                Sequence = entry.Sequence,
                Inning = entry.Inning,
                Half = entry.Half,
                Outs = entry.Outs,
                AwayScore = entry.AwayScore,
                HomeScore = entry.HomeScore,
                Bases = entry.Bases ?? "",
                Description = entry.Description ?? ""
            };
        }

        private static HalfInningSnapshot CloneHalfInningSnapshot(HalfInningSnapshot inning)
        {
            if (inning == null)
                return null;

            return new HalfInningSnapshot
            {
                Inning = inning.Inning,
                Half = inning.Half,
                BattingTeamId = inning.BattingTeamId,
                RunsScored = inning.RunsScored,
                AwayScore = inning.AwayScore,
                HomeScore = inning.HomeScore
            };
        }

        private BaseRunner SnapshotBaseRunner(int baseIndex)
        {
            if (baseIndex < 0 || baseIndex >= _state.Bases.Length)
                return null;

            var baseState = _state.Bases[baseIndex];
            if (!baseState.Occupied || baseState.Player == null)
                return null;

            return new BaseRunner
            {
                Player = baseState.Player,
                Team = baseState.Team,
                CourtesyForPlayer = baseState.CourtesyForPlayer,
                ResponsiblePitcherId = baseState.ResponsiblePitcherId,
                Earned = baseState.Earned
            };
        }

        private static PlayerGameLine CloneGameLine(PlayerGameLine line)
        {
            if (line == null)
                return null;

            return new PlayerGameLine
            {
                TeamId = line.TeamId,
                PlayerId = line.PlayerId,
                PlayerName = line.PlayerName,
                Pitcher = line.Pitcher,
                StartingPitcher = line.StartingPitcher,
                Classification = line.Classification,
                InitialClassification = line.InitialClassification,
                R = line.R,
                AB = line.AB,
                H = line.H,
                Doubles = line.Doubles,
                Triples = line.Triples,
                HR = line.HR,
                RBI = line.RBI,
                BB = line.BB,
                IBB = line.IBB,
                SO = line.SO,
                SB = line.SB,
                CS = line.CS,
                HBP = line.HBP,
                SH = line.SH,
                SF = line.SF,
                FlyOuts = line.FlyOuts,
                GroundOuts = line.GroundOuts,
                PopOuts = line.PopOuts,
                GroundedIntoDoublePlays = line.GroundedIntoDoublePlays,
                ReachedOnError = line.ReachedOnError,
                IPOuts = line.IPOuts,
                ER = line.ER,
                RunsAllowed = line.RunsAllowed,
                K = line.K,
                HitsAllowed = line.HitsAllowed,
                DoublesAllowed = line.DoublesAllowed,
                TriplesAllowed = line.TriplesAllowed,
                WalksAllowed = line.WalksAllowed,
                IntentionalWalksAllowed = line.IntentionalWalksAllowed,
                HomeRunsAllowed = line.HomeRunsAllowed,
                HitBatters = line.HitBatters,
                WildPitches = line.WildPitches,
                Balks = line.Balks,
                BattersFaced = line.BattersFaced,
                PitchCount = line.PitchCount,
                Wins = line.Wins,
                Losses = line.Losses,
                Saves = line.Saves,
                Holds = line.Holds,
                BlownSaves = line.BlownSaves,
                CompleteGames = line.CompleteGames,
                Shutouts = line.Shutouts,
                Putouts = line.Putouts,
                Assists = line.Assists,
                Errors = line.Errors,
                DefensiveOuts = line.DefensiveOuts,
                DefensiveDoublePlays = line.DefensiveDoublePlays,
                TeamDoublePlaysTurned = line.TeamDoublePlaysTurned,
                PassedBalls = line.PassedBalls,
                StolenBasesAllowed = line.StolenBasesAllowed,
                CatcherCaughtStealing = line.CatcherCaughtStealing,
                GamesMissedInjury = line.GamesMissedInjury
            };
        }

        public void SetNationalAnthemImages(IEnumerable<string> awayImagePaths, IEnumerable<string> homeImagePaths)
        {
            _awayNationalAnthemImages = (awayImagePaths ?? Enumerable.Empty<string>()).ToList();
            _homeNationalAnthemImages = (homeImagePaths ?? Enumerable.Empty<string>()).ToList();
        }

        public void SetPregameLineupLogos(string awayLogoPath, string homeLogoPath)
        {
            _awayLineupLogoPath = awayLogoPath ?? "";
            _homeLineupLogoPath = homeLogoPath ?? "";
            _state.SetTeamLogos(_awayLineupLogoPath, _homeLineupLogoPath);
            _surface.Invalidate();
        }

        public void SetFieldPreset(BaseballFieldPreset preset)
        {
            _state.FieldPreset = preset ?? BaseballFieldPresets.Default;
            _surface.Invalidate();
        }

        public void SetScore(int awayScore, int homeScore)
        {
            _state.AwayScore = Math.Max(0, awayScore);
            _state.HomeScore = Math.Max(0, homeScore);
            _surface.Invalidate();
        }

        public void SetCount(int balls, int strikes, int outs)
        {
            _state.Balls = Math.Clamp(balls, 0, 4);
            _state.Strikes = Math.Clamp(strikes, 0, 3);
            _state.Outs = Math.Clamp(outs, 0, 3);
            _surface.Invalidate();
        }

        public void SetInning(int inning, bool topHalf)
        {
            _state.Inning = Math.Max(1, inning);
            _state.TopHalf = topHalf;
            ResetCurrentDefensivePitcherInningBaserunners();
            _state.SeedFielders();
            _surface.Invalidate();
        }

        public void SetBallPosition(float normalizedX, float normalizedY, bool visible = true)
        {
            _state.BallPosition = new PointF(Clamp01(normalizedX), Clamp01(normalizedY));
            _state.BallVisible = visible;
            _surface.Invalidate();
        }

        public void SetBaseRunner(int baseNumber, Player player, Team? team = null)
        {
            _state.SetBaseRunner(baseNumber, player, team ?? _state.BattingTeam);
            _surface.Invalidate();
        }

        public void SelectFielder(int fielderIndex)
        {
            if (_state.Fielders.Count == 0)
                return;

            _state.ActiveFielderIndex = Math.Clamp(fielderIndex, 0, _state.Fielders.Count - 1);
            _surface.Invalidate();
        }

        public bool ChangePitcher(int pitcherIndex)
        {
            if (_gameComplete ||
                (_state.Phase != GameplayRenderingPhase.Ready && _state.Phase != GameplayRenderingPhase.DeadBall))
            {
                return false;
            }

            int pitcherCount = CountPitchingOptions(_state.FieldingTeam);
            if (pitcherCount <= 0)
                return false;

            int normalizedIndex = FindNextAvailablePitcherIndex(_state.FieldingTeam, pitcherIndex);
            if (normalizedIndex < 0)
                return false;
            if (normalizedIndex == PositiveModulo(_state.CurrentPitcherIndex, pitcherCount))
                return false;

            Player outgoingPitcher = CurrentPitcher();
            FinalizeCurrentPitcherInning();
            _state.CurrentPitcherIndex = normalizedIndex;
            _state.CurrentEmergencyPitcherId = null;
            SetCurrentFieldingTeamCoachVisitBoost(false);

            Player pitcher = CurrentPitcher();
            bool pitcherEnteredOrder = _state.EnsurePitcherBatsForFieldingTeam(pitcher, outgoingPitcher);
            _state.SeedFielders();
            RegisterRelieverEntryIfNeeded(pitcher);
            MarkCurrentPitcherAppeared();
            _state.ModeLabel = "Pitcher change: " + (pitcher?.Name ?? "Pitcher") + (pitcherEnteredOrder ? " (batting order updated)" : "");
            PlayPitcherChangeSound();
            TriggerCutscene(CutsceneTrigger.PitcherChange, _state.FieldingTeam);
            _surface.Invalidate();
            return true;
        }

        public void ApplyGameplayState(object gameplayState)
        {
            if (gameplayState == null)
                return;

            if (gameplayState is GameplayState typedState)
            {
                ApplyGameplayState(typedState);
                return;
            }

            Team? away = ReadProperty<Team?>(gameplayState, "AwayTeam", null);
            Team? home = ReadProperty<Team?>(gameplayState, "HomeTeam", null);
            if (away != null || home != null)
                _state.SetTeams(away ?? _state.AwayTeam, home ?? _state.HomeTeam);

            _state.AwayScore = Math.Max(0, ReadProperty(gameplayState, "AwayScore", _state.AwayScore));
            _state.HomeScore = Math.Max(0, ReadProperty(gameplayState, "HomeScore", _state.HomeScore));
            _state.Inning = Math.Max(1, ReadProperty(gameplayState, "Inning", _state.Inning));
            _state.TopHalf = ReadProperty(gameplayState, "TopHalf", _state.TopHalf);
            _state.Balls = Math.Clamp(ReadProperty(gameplayState, "Balls", _state.Balls), 0, 4);
            _state.Strikes = Math.Clamp(ReadProperty(gameplayState, "Strikes", _state.Strikes), 0, 3);
            _state.Outs = Math.Clamp(ReadProperty(gameplayState, "Outs", _state.Outs), 0, 3);
            _state.ModeLabel = ReadProperty(gameplayState, "ModeLabel", _state.ModeLabel) ?? _state.ModeLabel;
            _state.AwayPitcherIndex = ReadProperty(gameplayState, "AwayPitcherIndex", _state.AwayPitcherIndex);
            _state.HomePitcherIndex = ReadProperty(gameplayState, "HomePitcherIndex", _state.HomePitcherIndex);
            _state.AwayEmergencyPitcherId = null;
            _state.HomeEmergencyPitcherId = null;

            PointF ball = ReadProperty(gameplayState, "BallPosition", _state.BallPosition);
            _state.BallPosition = new PointF(Clamp01(ball.X), Clamp01(ball.Y));
            _state.BallVisible = ReadProperty(gameplayState, "BallVisible", _state.BallVisible);
            InitializeStarterFatigueTracking();
            _state.SeedFielders();
            _surface.Invalidate();
        }

        public void ApplyGameplayState(GameplayState gameplayState)
        {
            if (gameplayState == null)
                return;

            _mode = gameplayState.Mode;
            if (gameplayState.Id != Guid.Empty)
                _gameplaySaveId = gameplayState.Id;
            _timer.Interval = _mode == GameMode.QuickSim ? 1 : DefaultTickIntervalMilliseconds;
            _state.SetTeams(gameplayState.AwayTeam ?? _state.AwayTeam, gameplayState.HomeTeam ?? _state.HomeTeam);
            RestoreControlAssignments(gameplayState);
            _state.AwayScore = Math.Max(0, gameplayState.AwayScore);
            _state.HomeScore = Math.Max(0, gameplayState.HomeScore);
            _gameComplete = gameplayState.IsComplete;
            _endedByMercyRule = gameplayState.EndedByMercyRule;
            _state.Inning = Math.Max(1, gameplayState.Inning);
            _state.TopHalf = gameplayState.Half == HalfInning.Top;
            _state.RegulationInnings = Math.Max(1, gameplayState.RegulationInnings);
            _state.AwayUniformSetId = gameplayState.AwayUniformSetId;
            _state.HomeUniformSetId = gameplayState.HomeUniformSetId;
            _state.AllowExtraInnings = gameplayState.AllowExtraInnings;
            _state.MercyRuleEnabled = gameplayState.MercyRuleEnabled;
            _state.MercyRuleRuns = Math.Max(1, gameplayState.MercyRuleRuns);
            _state.MercyRuleMinimumInning = Math.Max(1, gameplayState.MercyRuleMinimumInning);
            _state.ExtraInningRunnerOnSecond = gameplayState.ExtraInningRunnerOnSecond;
            _state.CourtesyRunnerForPitchersCatchers = gameplayState.CourtesyRunnerForPitchersCatchers;
            _state.AwayBatterIndex = gameplayState.AwayBatterIndex;
            _state.HomeBatterIndex = gameplayState.HomeBatterIndex;
            _state.AwayPitcherIndex = gameplayState.AwayPitcherIndex;
            _state.HomePitcherIndex = gameplayState.HomePitcherIndex;
            CopyLineupIds(gameplayState.AwayLineupPlayerIds, _state.AwayLineupPlayerIds);
            CopyLineupIds(gameplayState.HomeLineupPlayerIds, _state.HomeLineupPlayerIds);
            _state.AwayDesignatedHitterId = gameplayState.AwayDesignatedHitterId;
            _state.HomeDesignatedHitterId = gameplayState.HomeDesignatedHitterId;
            _state.AwayDhActive = gameplayState.AwayDhActive;
            _state.HomeDhActive = gameplayState.HomeDhActive;
            _liveLines.Clear();
            if (gameplayState.LiveLines != null)
                _liveLines.AddRange(gameplayState.LiveLines.Where(l => l != null).Select(CloneGameLine));
            RestoreGameHistory(gameplayState);
            RestoreLiveRulesState(gameplayState.LiveRules);
            _state.PinchUseCounts.Clear();
            if (gameplayState.PinchUseCounts != null)
            {
                foreach (var item in gameplayState.PinchUseCounts)
                    _state.PinchUseCounts[item.Key] = item.Value;
            }
            _state.RemovedPlayerIds.Clear();
            if (gameplayState.RemovedPlayerIds != null)
                _state.RemovedPlayerIds.AddRange(gameplayState.RemovedPlayerIds);
            _state.Balls = Math.Clamp(gameplayState.Count?.Balls ?? 0, 0, 4);
            _state.Strikes = Math.Clamp(gameplayState.Count?.Strikes ?? 0, 0, 3);
            _state.Outs = Math.Clamp(gameplayState.Count?.Outs ?? 0, 0, 3);
            _state.ModeLabel = gameplayState.IsComplete
                ? (gameplayState.EndedByMercyRule ? "Final - Mercy Rule" : "Final")
                : (_state.TopHalf ? "Top " : "Bottom ") + _state.Inning;
            _state.FieldPreset = BaseballFieldPresets.Find(gameplayState.FieldPresetId);
            _state.SetUniforms(gameplayState.AwayUniformSetId, gameplayState.HomeUniformSetId);

            _state.SeedFielders();
            _state.ClearBases();
            ApplyBaseRunner(1, gameplayState.Bases?.First);
            ApplyBaseRunner(2, gameplayState.Bases?.Second);
            ApplyBaseRunner(3, gameplayState.Bases?.Third);
            _skipPregameCeremony = IsGameplayStateInProgress(gameplayState);
            RefreshInputModeComboItems();
            _surface.Invalidate();
        }

        private void RestoreControlAssignments(GameplayState gameplayState)
        {
            Guid awayId = _state.AwayTeam?.Id ?? Guid.Empty;
            Guid homeId = _state.HomeTeam?.Id ?? Guid.Empty;

            _userControlledTeamId = IsMatchTeamId(gameplayState.UserControlledTeamId, awayId, homeId)
                ? gameplayState.UserControlledTeamId
                : awayId;
            _keyboardControlledTeamId = IsMatchTeamId(gameplayState.KeyboardControlledTeamId, awayId, homeId)
                ? gameplayState.KeyboardControlledTeamId
                : awayId;
            _controllerControlledTeamId = IsMatchTeamId(gameplayState.ControllerControlledTeamId, awayId, homeId)
                ? gameplayState.ControllerControlledTeamId
                : homeId;

            if (_controllerControlledTeamId == _keyboardControlledTeamId)
                _controllerControlledTeamId = _keyboardControlledTeamId == awayId ? homeId : awayId;
        }

        private static bool IsMatchTeamId(Guid teamId, Guid awayId, Guid homeId)
            => teamId != Guid.Empty && (teamId == awayId || teamId == homeId);

        private void RestoreGameHistory(GameplayState gameplayState)
        {
            _awayRunsByInning.Clear();
            _homeRunsByInning.Clear();
            _playByPlay.Clear();
            _completedHalfInnings.Clear();
            _recordedHalfInnings.Clear();

            if (gameplayState.AwayRunsByInning != null)
                _awayRunsByInning.AddRange(gameplayState.AwayRunsByInning.Select(r => Math.Max(0, r)));
            if (gameplayState.HomeRunsByInning != null)
                _homeRunsByInning.AddRange(gameplayState.HomeRunsByInning.Select(r => Math.Max(0, r)));
            if (gameplayState.PlayByPlay != null)
                _playByPlay.AddRange(gameplayState.PlayByPlay.Where(p => p != null).Select(ClonePlayByPlayEntry));

            if (gameplayState.CompletedHalfInnings != null)
            {
                foreach (var completed in gameplayState.CompletedHalfInnings.Where(i => i != null && i.Inning > 0))
                {
                    string key = HalfInningKey(completed.Inning, completed.Half);
                    if (_recordedHalfInnings.Add(key))
                        _completedHalfInnings.Add(CloneHalfInningSnapshot(completed));
                }
            }

            for (int inning = 1; inning <= _awayRunsByInning.Count; inning++)
                _recordedHalfInnings.Add(HalfInningKey(inning, HalfInning.Top));
            for (int inning = 1; inning <= _homeRunsByInning.Count; inning++)
                _recordedHalfInnings.Add(HalfInningKey(inning, HalfInning.Bottom));

            _playableAwayLeftOnBase = Math.Max(0, gameplayState.AwayLeftOnBase);
            _playableHomeLeftOnBase = Math.Max(0, gameplayState.HomeLeftOnBase);
        }

        private static string HalfInningKey(int inning, HalfInning half)
            => Math.Max(1, inning) + ":" + (half == HalfInning.Top ? "T" : "B");

        protected override bool IsInputKey(Keys keyData)
        {
            switch (keyData)
            {
                case Keys.Left:
                case Keys.Right:
                case Keys.Up:
                case Keys.Down:
                case Keys.Tab:
                    return true;
            }

            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (_input.HandleKeyDown(e.KeyCode))
                e.Handled = true;

            base.OnKeyDown(e);
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            if (_input.HandleKeyUp(e.KeyCode))
                e.Handled = true;

            base.OnKeyUp(e);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (_skipPregameCeremony)
                return;
            BeginInvoke(new Action(PlayPregameCeremony));
        }

        private void PlayPregameCeremony()
        {
            if (_pregameCeremonyStarted)
                return;

            _pregameCeremonyStarted = true;
            bool wasRunning = _timer.Enabled;
            StopGameLoop();

            using (var homeLineup = new StartingLineupForm(_state.HomeTeam, _homeLineupLogoPath, homeTeam: true, _state.UniformForTeam(_state.HomeTeam)))
                homeLineup.ShowDialog(this);
            using (var awayLineup = new StartingLineupForm(_state.AwayTeam, _awayLineupLogoPath, homeTeam: false, _state.UniformForTeam(_state.AwayTeam)))
                awayLineup.ShowDialog(this);

            if (!TryPlayNationalAnthemCutscene())
            {
                string anthem = LaunchSoundPlayer.FindRandomNationalAnthem(_rng);
                if (!string.IsNullOrWhiteSpace(anthem))
                {
                    using var ceremony = new NationalAnthemForm(
                        _state.AwayTeam,
                        _state.HomeTeam,
                        _awayNationalAnthemImages,
                        _homeNationalAnthemImages,
                        anthem);
                    ceremony.ShowDialog(this);
                }
            }

            if (wasRunning)
                StartGameLoop();
            PlayFirstPitchReadySound();
            UpdateInningMusic();
        }

        private bool TryPlayNationalAnthemCutscene()
        {
            if (_nationalAnthemCutsceneDefault == NationalAnthemCutsceneDefault.CurrentGameSettings)
                return false;

            if (_nationalAnthemCutsceneDefault == NationalAnthemCutsceneDefault.LeagueCutscene)
                return CutscenePlaybackForm.PlayFirst(this, _leagueCutscenes, CutsceneTrigger.NationalAnthem);

            return CutscenePlaybackForm.PlayFirst(
                this,
                TeamCutscenesForUniform(_homeCutscenes, homeUniform: true, _state.UniformForTeam(_state.HomeTeam)?.Category)
                    .Concat(TeamCutscenesForUniform(_awayCutscenes, homeUniform: false, _state.UniformForTeam(_state.AwayTeam)?.Category)),
                CutsceneTrigger.NationalAnthem);
        }

        private void PlayFirstPitchReadySound()
        {
            if (_playedPlayBall || _state.Phase != GameplayRenderingPhase.Ready)
                return;

            _playedPlayBall = true;
            _playBallSound.PlayOnce(LaunchSoundPlayer.FindPlayBall());
        }

        private void UpdateInningMusic()
        {
            bool canPlay = _pregameCeremonyStarted &&
                _playedPlayBall &&
                !_paused &&
                _state.Outs < 3;

            if (TryStartTopThirdTheme(canPlay))
                return;
            if (TryStartTopFourthTheme(canPlay))
                return;
            if (TryStartTopFinalTheme(canPlay))
                return;
            if (TryStartTopSeventhTheme(canPlay))
                return;

            bool specialThemeBlocking = _topThirdThemePlaying || _topFourthThemePlaying || _topSeventhThemePlaying || _topFinalThemePlaying;
            bool shouldPlayTop = canPlay && _state.TopHalf;
            bool shouldPlayBottom = canPlay && !_state.TopHalf;

            if (shouldPlayTop && !specialThemeBlocking && !_topHalfMusicPlaying)
            {
                _topHalfMusic.PlayLoop(LaunchSoundPlayer.FindTopHalfMatchupLoop());
                _topHalfMusicPlaying = true;
            }

            if ((!shouldPlayTop || specialThemeBlocking) && _topHalfMusicPlaying)
            {
                _topHalfMusic.Stop();
                _topHalfMusicPlaying = false;
            }

            if (shouldPlayBottom && !_bottomHalfMusicPlaying)
            {
                _bottomHalfMusic.PlayLoop(LaunchSoundPlayer.FindBottomHalfThemeLoop());
                _bottomHalfMusicPlaying = true;
            }

            if (!shouldPlayBottom && _bottomHalfMusicPlaying)
            {
                _bottomHalfMusic.Stop();
                _bottomHalfMusicPlaying = false;
            }
        }

        private bool TryStartTopThirdTheme(bool canPlay)
        {
            if (!canPlay || _topThirdThemePlayed || _topThirdThemePlaying || !_state.TopHalf || _state.Inning != 3)
                return _topThirdThemePlaying;

            string path = LaunchSoundPlayer.FindTopThirdTheme();
            if (string.IsNullOrWhiteSpace(path))
            {
                _topThirdThemePlayed = true;
                return false;
            }

            _topThirdThemePlayed = true;
            _topThirdThemePlaying = true;
            if (_topHalfMusicPlaying)
            {
                _topHalfMusic.Stop();
                _topHalfMusicPlaying = false;
            }
            _topThirdSound.PlayOnce(path);
            _topThirdTimer.Interval = Math.Clamp(LaunchSoundPlayer.GetDurationMilliseconds(path, 12000) + 150, 1000, 600000);
            _topThirdTimer.Start();
            return true;
        }

        private bool TryStartTopFourthTheme(bool canPlay)
        {
            if (!canPlay || _topFourthThemePlayed || _topFourthThemePlaying || !_state.TopHalf || _state.Inning != 4)
                return _topFourthThemePlaying;

            string path = LaunchSoundPlayer.FindTopFourthTheme();
            if (string.IsNullOrWhiteSpace(path))
            {
                _topFourthThemePlayed = true;
                return false;
            }

            _topFourthThemePlayed = true;
            _topFourthThemePlaying = true;
            if (_topHalfMusicPlaying)
            {
                _topHalfMusic.Stop();
                _topHalfMusicPlaying = false;
            }
            _topFourthSound.PlayOnce(path);
            _topFourthTimer.Interval = Math.Clamp(LaunchSoundPlayer.GetDurationMilliseconds(path, 12000) + 150, 1000, 600000);
            _topFourthTimer.Start();
            return true;
        }

        private bool TryStartTopSeventhTheme(bool canPlay)
        {
            if (!canPlay || _topSeventhThemePlayed || _topSeventhThemePlaying || !_state.TopHalf || _state.Inning != 7)
                return _topSeventhThemePlaying;

            string path = LaunchSoundPlayer.FindTopSeventhTheme();
            if (string.IsNullOrWhiteSpace(path))
            {
                _topSeventhThemePlayed = true;
                return false;
            }

            _topSeventhThemePlayed = true;
            _topSeventhThemePlaying = true;
            if (_topHalfMusicPlaying)
            {
                _topHalfMusic.Stop();
                _topHalfMusicPlaying = false;
            }
            _topSeventhSound.PlayOnce(path);
            _topSeventhTimer.Interval = Math.Clamp(LaunchSoundPlayer.GetDurationMilliseconds(path, 12000) + 150, 1000, 600000);
            _topSeventhTimer.Start();
            return true;
        }

        private bool TryStartTopFinalTheme(bool canPlay)
        {
            int finalInning = Math.Max(1, _state.RegulationInnings);
            if (!canPlay || _topFinalThemePlayed || _topFinalThemePlaying || !_state.TopHalf || _state.Inning != finalInning)
                return _topFinalThemePlaying;

            string path = LaunchSoundPlayer.FindTopFinalTheme();
            if (string.IsNullOrWhiteSpace(path))
            {
                _topFinalThemePlayed = true;
                return false;
            }

            _topFinalThemePlayed = true;
            _topFinalThemePlaying = true;
            if (_topHalfMusicPlaying)
            {
                _topHalfMusic.Stop();
                _topHalfMusicPlaying = false;
            }
            _topFinalSound.PlayOnce(path);
            _topFinalTimer.Interval = Math.Clamp(LaunchSoundPlayer.GetDurationMilliseconds(path, 12000) + 150, 1000, 600000);
            _topFinalTimer.Start();
            return true;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _timer?.Stop();
                _timer?.Dispose();
                _visitorOutCheerTimer?.Stop();
                _visitorOutCheerTimer?.Dispose();
                _gameOverTimer?.Stop();
                _gameOverTimer?.Dispose();
                _topThirdTimer?.Stop();
                _topThirdTimer?.Dispose();
                _topFourthTimer?.Stop();
                _topFourthTimer?.Dispose();
                _topSeventhTimer?.Stop();
                _topSeventhTimer?.Dispose();
                _topFinalTimer?.Stop();
                _topFinalTimer?.Dispose();
                _playBallSound.Dispose();
                _topHalfMusic.Dispose();
                _bottomHalfMusic.Dispose();
                _changeSideSound.Dispose();
                _homeRunnersSound.Dispose();
                _visitorRunnersSound.Dispose();
                _runnerOnThirdSound.Dispose();
                _scoredRunSound.Dispose();
                _pitcherChangeSound.Dispose();
                _pitchThrowSound.Dispose();
                _batHitBallSound.Dispose();
                _playEventSound.Dispose();
                _takeYourBaseSound.Dispose();
                _safeCallSound.Dispose();
                _chanceBgmSound.Dispose();
                _homeRunSound.Dispose();
                _outCallSound.Dispose();
                _visitorOutCheerSound.Dispose();
                _gameOverSound.Dispose();
                _topThirdSound.Dispose();
                _topFourthSound.Dispose();
                _topSeventhSound.Dispose();
                _topFinalSound.Dispose();
            }

            base.Dispose(disposing);
        }

        private void FixedTick()
        {
            if (_gameComplete)
            {
                UpdateInningMusic();
                UpdateStrategyMenuState();
                _surface.Invalidate();
                return;
            }

            ApplyAllStarPitchingRuleIfNeeded();
            if (_gameComplete)
            {
                UpdateInningMusic();
                UpdateStrategyMenuState();
                _surface.Invalidate();
                return;
            }

            bool controllerConnected = _input.PollController();
            if (controllerConnected != _controllerWasConnected)
            {
                _controllerWasConnected = controllerConnected;
                if (controllerConnected)
                    _state.ModeLabel = "Controller " + (_input.ConnectedControllerIndex.GetValueOrDefault() + 1);
            }

            foreach (var inputEvent in _input.DrainCommandEvents())
            {
                _activeInputSource = inputEvent.Source;
                ExecuteInputCommand(inputEvent.Command);
            }
            _activeInputSource = null;

            ApplyContinuousInput(CurrentFieldingInputSnapshot());

            if (_paused)
            {
                UpdateInningMusic();
                UpdateStrategyMenuState();
                _surface.Invalidate();
                return;
            }

            if (_mode == GameMode.CpuVsCpuWatch || _mode == GameMode.QuickSim)
                TickWatchMode();
            else if (_mode == GameMode.UserVsCpu)
                TickPlayerVsCpuMode();

            switch (_state.Phase)
            {
                case GameplayRenderingPhase.Pitching:
                    TickPitch();
                    break;
                case GameplayRenderingPhase.BallInPlay:
                    TickBallInPlay();
                    break;
            }

            UpdateInningMusic();
            UpdateStrategyMenuState();
            _surface.Invalidate();
        }

        private void ExecuteInputCommand(GameplayInputCommand command)
        {
            switch (command)
            {
                case GameplayInputCommand.BatSwing:
                    if (HumanControlsBattingTeam() && _state.Phase == GameplayRenderingPhase.Pitching)
                        ResolveSwing(SharedSwingType.Normal);
                    break;
                case GameplayInputCommand.BatContactSwing:
                    if (HumanControlsBattingTeam() && _state.Phase == GameplayRenderingPhase.Pitching)
                        ResolveSwing(SharedSwingType.Contact);
                    break;
                case GameplayInputCommand.BatPowerSwing:
                    if (HumanControlsBattingTeam() && _state.Phase == GameplayRenderingPhase.Pitching)
                        ResolveSwing(SharedSwingType.Power);
                    break;
                case GameplayInputCommand.PitchRelease:
                    if (HumanControlsFieldingTeam() &&
                        (_state.Phase == GameplayRenderingPhase.Ready || _state.Phase == GameplayRenderingPhase.DeadBall))
                    {
                        StartPitch();
                    }
                    break;
                case GameplayInputCommand.ThrowHome:
                    if (!HumanControlsFieldingTeam()) break;
                    SetDefensiveStealCall(DefensiveStealCall.Pitchout);
                    break;
                case GameplayInputCommand.ThrowFirst:
                    if (!HumanControlsFieldingTeam()) break;
                    SetDefensiveStealCall(DefensiveStealCall.HoldRunner);
                    break;
                case GameplayInputCommand.ThrowSecond:
                    if (!HumanControlsFieldingTeam()) break;
                    SetDefensiveStealCall(DefensiveStealCall.SlideStep);
                    break;
                case GameplayInputCommand.ThrowThird:
                    if (!HumanControlsFieldingTeam()) break;
                    TryResolvePickoffMove();
                    break;
                case GameplayInputCommand.AdvanceRunners:
                {
                    if (!HumanControlsBattingTeam()) break;
                    TryResolveStealAttempt(userInitiated: true);
                    break;
                }
                case GameplayInputCommand.RetreatRunners:
                    if (!HumanControlsBattingTeam()) break;
                    _state.ModeLabel = "Runners hold";
                    break;
                case GameplayInputCommand.CallSacrificeBunt:
                    if (!HumanControlsBattingTeam()) break;
                    SetOffensiveStrategy(OffensiveStrategyCall.SacrificeBunt);
                    break;
                case GameplayInputCommand.CallHitAndRun:
                    if (!HumanControlsBattingTeam()) break;
                    SetOffensiveStrategy(OffensiveStrategyCall.HitAndRun);
                    break;
                case GameplayInputCommand.CallSteal:
                    if (!HumanControlsBattingTeam()) break;
                    if (IsOffensiveStrategyAvailable(OffensiveStrategyCall.Steal))
                    {
                        SetOffensiveStrategy(OffensiveStrategyCall.Steal);
                        TryResolveStealAttempt(userInitiated: true);
                    }
                    else
                        _state.ModeLabel = "Steal: no eligible runner";
                    break;
                case GameplayInputCommand.CallSafe:
                    if (!HumanControlsBattingTeam()) break;
                    SetOffensiveStrategy(OffensiveStrategyCall.Safe);
                    break;
                case GameplayInputCommand.CallBunt:
                    if (!HumanControlsBattingTeam()) break;
                    SetOffensiveStrategy(OffensiveStrategyCall.Bunt);
                    break;
                case GameplayInputCommand.CallDoubleSteal:
                    if (!HumanControlsBattingTeam()) break;
                    if (IsOffensiveStrategyAvailable(OffensiveStrategyCall.DoubleSteal))
                    {
                        SetOffensiveStrategy(OffensiveStrategyCall.DoubleSteal);
                        TryResolveDoubleStealAttempt(userInitiated: true);
                    }
                    else
                        _state.ModeLabel = "Double steal: need two eligible runners";
                    break;
                case GameplayInputCommand.CallNormalDefense:
                    if (!HumanControlsFieldingTeam()) break;
                    SetDefensiveAlignment(DefensiveAlignmentCall.Normal);
                    break;
                case GameplayInputCommand.CallInfieldIn:
                    if (!HumanControlsFieldingTeam()) break;
                    SetDefensiveAlignment(DefensiveAlignmentCall.InfieldIn);
                    break;
                case GameplayInputCommand.CallDoublePlay:
                    if (!HumanControlsFieldingTeam()) break;
                    SetDefensiveAlignment(DefensiveAlignmentCall.DoublePlay);
                    break;
                case GameplayInputCommand.CallOutfieldIn:
                    if (!HumanControlsFieldingTeam()) break;
                    SetDefensiveAlignment(DefensiveAlignmentCall.OutfieldIn);
                    break;
                case GameplayInputCommand.CallNoDoubles:
                    if (!HumanControlsFieldingTeam()) break;
                    SetDefensiveAlignment(DefensiveAlignmentCall.NoDoubles);
                    break;
                case GameplayInputCommand.CallWheelPlay:
                    if (!HumanControlsFieldingTeam()) break;
                    SetDefensiveAlignment(DefensiveAlignmentCall.WheelPlay);
                    break;
                case GameplayInputCommand.CallIntentionalWalk:
                    if (!HumanControlsFieldingTeam()) break;
                    ResolveIntentionalWalk();
                    break;
                case GameplayInputCommand.CallMoundVisit:
                    if (!HumanControlsFieldingTeam()) break;
                    TryMoundVisit();
                    break;
                case GameplayInputCommand.ChangePitcher:
                    if (!HumanControlsFieldingTeam()) break;
                    TryCyclePitcher();
                    break;
                case GameplayInputCommand.SelectFastball:
                case GameplayInputCommand.SelectCurveball:
                case GameplayInputCommand.SelectSlider:
                case GameplayInputCommand.SelectChangeup:
                case GameplayInputCommand.SelectSplitter:
                case GameplayInputCommand.SelectForkball:
                case GameplayInputCommand.SelectKnuckleball:
                    if (HumanControlsFieldingTeam())
                    {
                        _currentPitchType = SelectedPitchTypeForFieldingTeam();
                        _state.ModeLabel = "Pitch: " + _currentPitchType;
                    }
                    break;
                case GameplayInputCommand.TogglePause:
                    _paused = !_paused;
                    _state.ModeLabel = _paused ? "Paused" : "Ready";
                    break;
                case GameplayInputCommand.ToggleCamera:
                    SelectFielder((_state.ActiveFielderIndex + 1) % Math.Max(1, _state.Fielders.Count));
                    _state.ModeLabel = "Fielder " + (_state.ActiveFielderIndex + 1);
                    break;
                case GameplayInputCommand.ToggleWatch:
                    _mode = _mode == GameMode.CpuVsCpuWatch ? GameMode.UserVsCpu : GameMode.CpuVsCpuWatch;
                    _timer.Interval = DefaultTickIntervalMilliseconds;
                    _state.ModeLabel = _mode == GameMode.CpuVsCpuWatch ? "CPU watch" : "Player vs CPU";
                    RefreshInputModeComboSelection();
                    break;
            }
        }

        private void SetOffensiveStrategy(OffensiveStrategyCall call)
        {
            if (_gameComplete ||
                (_state.Phase != GameplayRenderingPhase.Ready && _state.Phase != GameplayRenderingPhase.DeadBall && _state.Phase != GameplayRenderingPhase.Pitching))
            {
                return;
            }
            if (!IsOffensiveStrategyAvailable(call))
            {
                _state.ModeLabel = "Strategy unavailable";
                return;
            }

            _offensiveStrategyCall = call;
            _state.ModeLabel = "Strategy: " + OffensiveStrategyCallLabel(call);
        }

        private void SetDefensiveAlignment(DefensiveAlignmentCall call)
        {
            if (_gameComplete ||
                (_state.Phase != GameplayRenderingPhase.Ready && _state.Phase != GameplayRenderingPhase.DeadBall && _state.Phase != GameplayRenderingPhase.Pitching))
            {
                return;
            }
            if (!IsDefensiveAlignmentAvailable(call))
            {
                _state.ModeLabel = "Defense unavailable";
                return;
            }

            _defensiveAlignmentCall = call;
            _state.ModeLabel = "Defense: " + DefensiveAlignmentCallLabel(call);
        }

        private void ResetStrategyCalls()
        {
            _offensiveStrategyCall = OffensiveStrategyCall.Normal;
            _defensiveAlignmentCall = DefensiveAlignmentCall.Normal;
        }

        private static string OffensiveStrategyCallLabel(OffensiveStrategyCall call)
        {
            return call switch
            {
                OffensiveStrategyCall.SacrificeBunt => "sacrifice bunt",
                OffensiveStrategyCall.HitAndRun => "hit and run",
                OffensiveStrategyCall.Steal => "steal",
                OffensiveStrategyCall.Safe => "safe",
                OffensiveStrategyCall.Bunt => "bunt",
                OffensiveStrategyCall.DoubleSteal => "double steal",
                _ => "normal"
            };
        }

        private static string DefensiveAlignmentCallLabel(DefensiveAlignmentCall call)
        {
            return call switch
            {
                DefensiveAlignmentCall.InfieldIn => "infield in",
                DefensiveAlignmentCall.DoublePlay => "double play",
                DefensiveAlignmentCall.OutfieldIn => "outfield in",
                DefensiveAlignmentCall.NoDoubles => "no doubles",
                DefensiveAlignmentCall.WheelPlay => "wheel play",
                _ => "normal"
            };
        }

        private void ApplyContinuousInput(GameplayInputSnapshot snapshot)
        {
            if (_mode == GameMode.CpuVsCpuWatch || _mode == GameMode.QuickSim)
                return;

            if (HumanControlsFieldingTeam() && !snapshot.FielderMove.IsNeutral)
                MoveActiveFielder(snapshot.FielderMove.X * 0.0125f, snapshot.FielderMove.Y * 0.0125f);

            if (HumanControlsFieldingTeam() && _state.Phase == GameplayRenderingPhase.Pitching && !snapshot.PitchAim.IsNeutral)
            {
                _ballTarget = new PointF(
                    Clamp01(_ballTarget.X + snapshot.PitchAim.X * 0.0025f),
                    Clamp01(_ballTarget.Y + snapshot.PitchAim.Y * 0.0025f));
            }
        }

        private void TryCyclePitcher()
        {
            if (_mode == GameMode.CpuVsCpuWatch || _mode == GameMode.QuickSim)
                return;

            int pitcherCount = CountPitchingOptions(_state.FieldingTeam);
            if (pitcherCount <= 1)
                return;

            ChangePitcher(_state.CurrentPitcherIndex + 1);
        }

        private void TryMoundVisit()
        {
            if (_gameComplete ||
                _mode == GameMode.CpuVsCpuWatch ||
                _mode == GameMode.QuickSim ||
                (_state.Phase != GameplayRenderingPhase.Ready && _state.Phase != GameplayRenderingPhase.DeadBall))
            {
                return;
            }

            Player pitcher = CurrentPitcher();
            if (pitcher == null)
            {
                _state.ModeLabel = "Mound visit: no pitcher";
                return;
            }

            int visits = CurrentFieldingTeamMoundVisits();
            if (visits <= 0)
            {
                SetCurrentFieldingTeamMoundVisits(1);
                SetCurrentFieldingTeamCoachVisitBoost(true);
                var coach = CurrentFieldingHeadCoach();
                _state.ModeLabel = "Mound visit: " + CoachDisplayLabel(coach) + " settles " + pitcher.Name;
                return;
            }

            int pitcherCount = CountPitchingOptions(_state.FieldingTeam);
            if (pitcherCount <= 1)
            {
                _state.ModeLabel = "Second mound visit: pitcher must be replaced, no replacement available";
                return;
            }

            SetCurrentFieldingTeamMoundVisits(visits + 1);
            SetCurrentFieldingTeamCoachVisitBoost(false);
            if (!ChangePitcher(_state.CurrentPitcherIndex + 1))
                _state.ModeLabel = "Second mound visit: pitcher must be replaced";
        }

        private int CurrentFieldingTeamMoundVisits()
            => _state.TopHalf ? _homeMoundVisitsThisInning : _awayMoundVisitsThisInning;

        private void SetCurrentFieldingTeamMoundVisits(int visits)
        {
            if (_state.TopHalf)
                _homeMoundVisitsThisInning = visits;
            else
                _awayMoundVisitsThisInning = visits;
        }

        private void SetCurrentFieldingTeamCoachVisitBoost(bool active)
        {
            if (_state.TopHalf)
                _homeCoachVisitBoostActive = active;
            else
                _awayCoachVisitBoostActive = active;
        }

        private bool CurrentFieldingTeamCoachVisitBoostActive()
            => _state.TopHalf ? _homeCoachVisitBoostActive : _awayCoachVisitBoostActive;

        private Coach CurrentFieldingHeadCoach()
        {
            return HeadCoachForTeam(_state.FieldingTeam);
        }

        private Coach CurrentBattingHeadCoach()
        {
            return HeadCoachForTeam(_state.BattingTeam);
        }

        private static Coach HeadCoachForTeam(Team team)
        {
            if (team == null)
                return null;
            team.NormalizeText();
            return team.Coaches?.FirstOrDefault(c => c.Id == team.CoachId)
                ?? team.Coaches?.FirstOrDefault(c => string.Equals(c.Role, "Head Coach", StringComparison.OrdinalIgnoreCase))
                ?? team.Coaches?.FirstOrDefault();
        }

        private static int CoachStrategyChanceAdjustment(Coach coach, int safeAdjustment, int aggressiveAdjustment)
        {
            return coach?.Strategy switch
            {
                CoachStrategy.Safe => safeAdjustment,
                CoachStrategy.Aggressive => aggressiveAdjustment,
                _ => 0
            };
        }

        private static string CoachDisplayLabel(Coach coach)
            => coach == null ? "Coach" : coach.Name + " (" + CoachStyleLabel(coach.Style) + ", " + coach.Strategy + ")";

        private static string CoachStyleLabel(CoachStyle style)
        {
            return style switch
            {
                CoachStyle.BelowAverage => "Below Average",
                CoachStyle.AboveAverage => "Above Average",
                CoachStyle.Championship => "Championship",
                _ => "Average"
            };
        }

        private bool IsGameOnLineForBattingTeam()
        {
            int differential = BattingTeamScoreDifferential();
            int regulation = Math.Max(1, _state.RegulationInnings);
            bool late = _state.Inning >= Math.Max(1, regulation - 1);
            bool extra = _state.Inning > regulation;
            bool close = Math.Abs(differential) <= 1;
            bool comeback = differential < 0 && differential >= -3;
            return extra || close || (late && comeback);
        }

        private bool HasScoringOpportunity()
        {
            return _state.Bases.Any(baseState => baseState.Occupied) &&
                (_state.Bases.Length >= 3 && _state.Bases[2].Occupied ||
                 _state.Outs < 2 && _state.Bases.Any(baseState => baseState.Occupied));
        }

        private bool IsStealScoringOpportunity(StealCandidate candidate)
        {
            if (candidate.Runner == null)
                return false;
            return candidate.TargetBase >= 3 || IsGameOnLineForBattingTeam();
        }

        private bool IsOffensiveCallSound(OffensiveStrategyCall call)
        {
            int differential = BattingTeamScoreDifferential();
            bool gameOnLine = IsGameOnLineForBattingTeam();
            bool runnerOnThird = _state.Bases.Length >= 3 && _state.Bases[2].Occupied;
            return call switch
            {
                OffensiveStrategyCall.Safe => differential >= 0 || !gameOnLine,
                OffensiveStrategyCall.SacrificeBunt => _state.Outs < 2 && _state.Bases.Any(baseState => baseState.Occupied) && (runnerOnThird || gameOnLine),
                OffensiveStrategyCall.Bunt => _state.Outs < 2 && (runnerOnThird || gameOnLine),
                OffensiveStrategyCall.HitAndRun => _state.Outs < 2 && HasHitAndRunRunner() && gameOnLine,
                OffensiveStrategyCall.Steal => FindLeadStealCandidate().Runner != null && gameOnLine,
                OffensiveStrategyCall.DoubleSteal => FindDoubleStealCandidates().Count >= 2 && gameOnLine,
                _ => true
            };
        }

        private int CurrentOffensiveStrategyExecutionModifier(OffensiveStrategyCall call)
        {
            if (call == OffensiveStrategyCall.Normal)
                return 0;

            return CoachDecisionEngine.StrategyExecutionModifier(CurrentBattingHeadCoach(), IsOffensiveCallSound(call));
        }

        private int CurrentBuntDefenseCoachModifier()
        {
            bool runThreat = _state.Bases.Any(baseState => baseState.Occupied) && _state.Outs < 2;
            bool rightCall = _defensiveAlignmentCall == DefensiveAlignmentCall.WheelPlay ||
                _defensiveAlignmentCall == DefensiveAlignmentCall.InfieldIn ||
                _defensiveAlignmentCall == DefensiveAlignmentCall.DoublePlay;
            return CoachDecisionEngine.StrategyExecutionModifier(CurrentFieldingHeadCoach(), rightCall && runThreat);
        }

        private void SetDefensiveStealCall(DefensiveStealCall call)
        {
            if (_gameComplete ||
                (_state.Phase != GameplayRenderingPhase.Ready && _state.Phase != GameplayRenderingPhase.DeadBall))
            {
                return;
            }

            _defensiveStealCall = call;
            _state.ModeLabel = "Steal defense: " + DefensiveStealCallLabel(call);
        }

        private void TryResolvePickoffMove()
        {
            if (_gameComplete ||
                (_state.Phase != GameplayRenderingPhase.Ready && _state.Phase != GameplayRenderingPhase.DeadBall))
            {
                return;
            }

            var candidate = FindLeadStealCandidate();
            if (candidate.Runner == null)
            {
                _state.ModeLabel = "Pickoff: no runner";
                return;
            }

            _defensiveStealCall = DefensiveStealCall.Pickoff;
            _pickoffAttemptsThisPlateAppearance++;
            Player pitcher = CurrentPitcher();
            if (TryResolveBalk(DefensiveStealCall.Pickoff, _pickoffAttemptsThisPlateAppearance))
            {
                _defensiveStealCall = DefensiveStealCall.Normal;
                _pickoffAttemptsThisPlateAppearance = 0;
                return;
            }

            Player tagFielder = TagFielderForTarget(candidate.FromBase);
            int pickoffScore = PlayerRating(pitcher, p => p.Pickoff, 50) +
                PlayerRating(pitcher, p => p.HoldRunner, 50) +
                PlayerRating(tagFielder, p => p.TagRating, 50) / 2 +
                _rng.Next(-18, 19) -
                PlayerRating(candidate.Runner, p => p.Speed, 50) -
                PlayerRating(candidate.Runner, p => p.BaseRunning, 50) / 2;

            if (pickoffScore >= 32)
            {
                var result = new StealAttemptResult
                {
                    Outcome = StealAttemptOutcome.PickedOff,
                    FromBase = candidate.FromBase,
                    TargetBase = candidate.FromBase,
                    FinalBase = 0,
                    Detail = "Picked off"
                };
                ClearBaseSlot(candidate.FromBase);
                RecordLiveStealStats(candidate.Runner, FindDefensivePlayer("C"), tagFielder, pitcher, result);
                bool visitorBatting = _state.TopHalf;
                _state.Outs++;
                CreditDefensiveOuts(1);
                bool thirdOut = _state.Outs >= 3;
                bool finalOut = thirdOut && IsFinalOutAfterThirdOut(visitorBatting);
                RegisterCurrentRelieverOut();
                _state.ModeLabel = "Picked off: " + candidate.Runner.Name;
                LogPlay(_state.ModeLabel);
                PlayOutCall(thirdOut, visitorBatting, finalOut);
                if (finalOut)
                {
                    CompleteGame();
                    return;
                }

                if (thirdOut)
                {
                    _pickoffAttemptsThisPlateAppearance = 0;
                    FinalizeCurrentPitcherInning();
                    AdvanceToNextHalfInning();
                }
            }
            else
            {
                _state.ModeLabel = "Pickoff: back safely";
                LogPlay(_state.ModeLabel);
            }

            _defensiveStealCall = DefensiveStealCall.Normal;
        }

        private bool TryResolveStealAttempt(bool userInitiated)
        {
            if (_gameComplete ||
                (_state.Phase != GameplayRenderingPhase.Ready && _state.Phase != GameplayRenderingPhase.DeadBall))
            {
                return false;
            }

            var candidate = FindLeadStealCandidate();
            if (candidate.Runner == null)
            {
                if (userInitiated)
                    _state.ModeLabel = "Steal: no eligible runner";
                return false;
            }

            ResolveSteal(candidate, _defensiveStealCall);
            _offensiveStrategyCall = OffensiveStrategyCall.Normal;
            return true;
        }

        private bool TryResolveDoubleStealAttempt(bool userInitiated)
        {
            if (_gameComplete ||
                (_state.Phase != GameplayRenderingPhase.Ready && _state.Phase != GameplayRenderingPhase.DeadBall))
            {
                return false;
            }

            var candidates = FindDoubleStealCandidates();
            if (candidates.Count < 2)
            {
                if (userInitiated)
                    _state.ModeLabel = "Double steal: need two eligible runners";
                return false;
            }

            foreach (var candidate in candidates.OrderByDescending(c => c.FromBase).ToList())
            {
                if (_state.Outs >= 3 || _gameComplete)
                    break;
                if (candidate.Runner == null || !BaseHasRunner(candidate.FromBase, candidate.Runner))
                    continue;
                ResolveSteal(candidate, _defensiveStealCall);
            }

            if (!_gameComplete && _state.Outs < 3)
                _state.ModeLabel = "Double steal resolved";
            _offensiveStrategyCall = OffensiveStrategyCall.Normal;
            return true;
        }

        private StealAttemptResult ResolveSteal(StealCandidate candidate, DefensiveStealCall call)
        {
            Player pitcher = CurrentPitcher();
            Player? catcher = FindDefensivePlayer("C") ?? _state.FieldingTeam?.Roster?.FirstOrDefault(IsPitcherOrCatcherPosition);
            Player tagFielder = TagFielderForTarget(candidate.TargetBase);
            int scoreDifferential = _state.TopHalf
                ? _state.AwayScore - _state.HomeScore
                : _state.HomeScore - _state.AwayScore;
            var result = StealEngine.Resolve(
                _rng,
                candidate.Runner,
                pitcher,
                catcher,
                tagFielder,
                candidate.FromBase,
                _state.Outs,
                _state.Balls,
                _state.Strikes,
                scoreDifferential,
                call);

            ApplyStealResult(candidate, result, catcher, tagFielder, pitcher);
            _defensiveStealCall = DefensiveStealCall.Normal;
            return result;
        }

        private void ApplyStealResult(StealCandidate candidate, StealAttemptResult result, Player? catcher, Player tagFielder, Player pitcher)
        {
            bool visitorBatting = _state.TopHalf;
            var runnerSnapshot = SnapshotBase(candidate.FromBase);
            Player? runner = runnerSnapshot.Player;
            if (runner == null)
                return;

            ClearBaseSlot(candidate.FromBase);
            RecordLiveStealStats(candidate.Runner, catcher, tagFielder, pitcher, result);

            if (result.RunnerOut)
            {
                _state.Outs++;
                CreditDefensiveOuts(1);
                bool thirdOut = _state.Outs >= 3;
                bool finalOut = thirdOut && IsFinalOutAfterThirdOut(visitorBatting);
                RegisterCurrentRelieverOut();
                _state.ModeLabel = result.Detail + ": " + candidate.Runner.Name;
                LogPlay(_state.ModeLabel);
                PlayOutCall(thirdOut, visitorBatting, finalOut);
                if (finalOut)
                {
                    CompleteGame();
                    return;
                }

                if (thirdOut)
                {
                    FinalizeCurrentPitcherInning();
                    AdvanceToNextHalfInning();
                }
                return;
            }

            int finalBase = result.FinalBase;
            if (finalBase >= 4)
            {
                int runsScored = CaptureRunsScoredByBattingTeam(() =>
                    _state.ScoreRunner(runner, runnerSnapshot.ResponsiblePitcherId, runnerSnapshot.Earned));
                RegisterCurrentPitcherRunsAllowed(runsScored);
            }
            else
            {
                if (finalBase < 1)
                    finalBase = result.TargetBase;
                if (finalBase <= 3 && _state.Bases[finalBase - 1].Occupied)
                    finalBase = result.TargetBase;
                RestoreBase(finalBase, runnerSnapshot);
            }

            _state.ModeLabel = result.Detail + ": " + candidate.Runner.Name + " to " + BaseLabel(Math.Min(result.FinalBase, 4));
            LogPlay(_state.ModeLabel);
            if (result.SuccessfulSteal)
                PlaySafeCallSound();
        }

        private bool TryResolvePitchEscape(int pitcherAdjustment)
        {
            if (_state.Bases.All(baseState => !baseState.Occupied))
                return false;

            Player pitcher = CurrentPitcher();
            Player? catcher = FindDefensivePlayer("C") ?? _state.FieldingTeam?.Roster?.FirstOrDefault(IsPitcherOrCatcherPosition);
            if (catcher == null)
                return false;

            int catcherBlock = CatcherBlockingRating(catcher);
            var kind = PitchEscapeEngine.Roll(
                _rng,
                pitcher,
                catcher,
                _currentPitchType,
                PitchZoneX(),
                PitchZoneY(),
                pitcherAdjustment,
                catcherBlock);
            if (kind == PitchEscapeKind.None)
                return false;

            RecordLivePitchEscapeStats(kind, pitcher, catcher);
            string eventLabel = kind == PitchEscapeKind.WildPitch ? "Wild pitch" : "Passed ball";
            bool anyAdvance = false;

            for (int baseNumber = 3; baseNumber >= 1; baseNumber--)
            {
                if (_gameComplete || _state.Outs >= 3)
                    break;
                if (!_state.Bases[baseNumber - 1].Occupied)
                    continue;

                var snapshot = SnapshotBase(baseNumber);
                if (snapshot.Player == null)
                    continue;

                int targetBase = Math.Min(4, baseNumber + 1);
                if (targetBase <= 3 && _state.Bases[targetBase - 1].Occupied)
                    continue;

                var advance = PitchEscapeEngine.ResolveAdvance(
                    _rng,
                    snapshot.Player,
                    baseNumber,
                    _state.Outs,
                    BattingTeamScoreDifferential(),
                    catcher,
                    TagFielderForTarget(targetBase),
                    kind);
                if (!advance.Attempt)
                    continue;

                Player targetFielder = TagFielderForTarget(targetBase);
                RegisterParticipation(snapshot.Player, _state.BattingTeam, InjuryExposureType.Baserunning);
                RegisterParticipation(catcher, _state.FieldingTeam, InjuryExposureType.FieldingPlay);
                RegisterParticipation(targetFielder, _state.FieldingTeam, InjuryExposureType.FieldingPlay);
                anyAdvance = true;
                ClearBaseSlot(baseNumber);
                if (advance.RunnerOut)
                {
                    RecordPitchEscapeOutStats(catcher, targetFielder, pitcher);
                    bool visitorBatting = _state.TopHalf;
                    _state.Outs++;
                    CreditDefensiveOuts(1);
                    bool thirdOut = _state.Outs >= 3;
                    bool finalOut = thirdOut && IsFinalOutAfterThirdOut(visitorBatting);
                    RegisterCurrentRelieverOut();
                    _state.ModeLabel = eventLabel + ": " + snapshot.Player.Name + " out trying for " + BaseLabel(targetBase);
                    LogPlay(_state.ModeLabel);
                    PlayOutCall(thirdOut, visitorBatting, finalOut);
                    if (finalOut)
                    {
                        CompleteGame();
                        return true;
                    }

                    if (thirdOut)
                    {
                        FinalizeCurrentPitcherInning();
                        AdvanceToNextHalfInning();
                        return true;
                    }
                }
                else if (targetBase >= 4)
                {
                    int runsScored = CaptureRunsScoredByBattingTeam(() =>
                        _state.ScoreRunner(snapshot.Player, snapshot.ResponsiblePitcherId, kind == PitchEscapeKind.WildPitch && snapshot.Earned));
                    var runnerLine = LiveLine(snapshot.Player, _state.BattingTeam, pitcher: false);
                    if (runnerLine != null)
                        runnerLine.R += runsScored;
                    RegisterCurrentPitcherRunsAllowed(runsScored, kind == PitchEscapeKind.PassedBall ? PitcherRunCharge.UnearnedError : PitcherRunCharge.Earned);
                    _state.ModeLabel = eventLabel + ": " + snapshot.Player.Name + " scores";
                    LogPlay(_state.ModeLabel);
                    PlaySafeCallSound();
                }
                else
                {
                    RestoreBase(targetBase, snapshot);
                    _state.ModeLabel = eventLabel + ": " + snapshot.Player.Name + " to " + BaseLabel(targetBase);
                    LogPlay(_state.ModeLabel);
                    PlaySafeCallSound();
                }
            }

            if (!anyAdvance)
            {
                _state.ModeLabel = eventLabel + ": runners hold";
                LogPlay(_state.ModeLabel);
            }
            return true;
        }

        private void RecordLivePitchEscapeStats(PitchEscapeKind kind, Player pitcher, Player catcher)
        {
            if (kind == PitchEscapeKind.WildPitch)
            {
                var pitcherLine = LiveLine(pitcher, _state.FieldingTeam, pitcher: true);
                if (pitcherLine != null)
                    pitcherLine.WildPitches++;
                return;
            }

            var catcherLine = LiveLine(catcher, _state.FieldingTeam, pitcher: false);
            if (catcherLine != null)
                catcherLine.PassedBalls++;
        }

        private void RecordPitchEscapeOutStats(Player catcher, Player tagFielder, Player pitcher)
        {
            var pitcherLine = LiveLine(pitcher, _state.FieldingTeam, pitcher: true);
            if (pitcherLine != null)
                pitcherLine.IPOuts++;
            var catcherLine = LiveLine(catcher, _state.FieldingTeam, pitcher: false);
            if (catcherLine != null)
                catcherLine.Assists++;
            var tagLine = LiveLine(tagFielder, _state.FieldingTeam, pitcher: false);
            if (tagLine != null)
                tagLine.Putouts++;
        }

        private int CatcherBlockingRating(Player? catcher)
        {
            if (catcher == null)
                return 45;

            return (PositionFieldingRating(catcher, "C") +
                PlayerRating(catcher, p => p.Accuracy, 50) +
                PlayerRating(catcher, p => p.PopTime, 50) +
                PlayerRating(catcher, p => p.TagRating, 50) / 2) / 3;
        }

        private void RecordLiveStealStats(Player runner, Player? catcher, Player tagFielder, Player pitcher, StealAttemptResult result)
        {
            RegisterParticipation(runner, _state.BattingTeam, InjuryExposureType.StealOrSlide);
            RegisterParticipation(catcher, _state.FieldingTeam, InjuryExposureType.FieldingPlay);
            RegisterParticipation(tagFielder, _state.FieldingTeam, InjuryExposureType.FieldingPlay);
            int defenseScore = result.ThrowScore + result.TagScore / 2;
            if (result.Outcome != StealAttemptOutcome.PickedOff && Math.Abs(result.JumpScore - defenseScore) <= 10)
            {
                RegisterParticipation(runner, _state.BattingTeam, InjuryExposureType.Collision);
                RegisterParticipation(tagFielder, _state.FieldingTeam, InjuryExposureType.Collision);
            }

            var runnerLine = LiveLine(runner, _state.BattingTeam, pitcher: false);
            if (runnerLine != null)
            {
                if (result.SuccessfulSteal)
                    runnerLine.SB++;
                else
                    runnerLine.CS++;
                if (result.RunsScored > 0)
                    runnerLine.R += result.RunsScored;
            }

            var catcherStealLine = LiveLine(catcher, _state.FieldingTeam, pitcher: false);
            if (catcherStealLine != null)
            {
                if (result.Outcome == StealAttemptOutcome.CaughtStealing)
                    catcherStealLine.CatcherCaughtStealing++;
                else if (result.SuccessfulSteal)
                    catcherStealLine.StolenBasesAllowed++;
            }

            if (result.RunnerOut)
            {
                var pitcherLine = LiveLine(pitcher, _state.FieldingTeam, pitcher: true);
                if (pitcherLine != null)
                    pitcherLine.IPOuts++;

                var catcherLine = LiveLine(catcher, _state.FieldingTeam, pitcher: false);
                if (catcherLine != null)
                    catcherLine.Assists++;

                var tagLine = LiveLine(tagFielder, _state.FieldingTeam, pitcher: false);
                if (tagLine != null)
                    tagLine.Putouts++;
            }
            else if (result.Outcome == StealAttemptOutcome.ThrowingError)
            {
                var catcherLine = LiveLine(catcher, _state.FieldingTeam, pitcher: false);
                if (catcherLine != null)
                    catcherLine.Errors++;
            }
        }

        private StealCandidate FindLeadStealCandidate()
        {
            for (int baseNumber = 3; baseNumber >= 1; baseNumber--)
            {
                var baseState = _state.Bases[baseNumber - 1];
                if (!baseState.Occupied || baseState.Player == null)
                    continue;

                int targetBase = baseNumber + 1;
                if (targetBase <= 3 && _state.Bases[targetBase - 1].Occupied)
                    continue;

                return new StealCandidate(baseNumber, targetBase, baseState.Player);
            }

            return default;
        }

        private List<StealCandidate> FindDoubleStealCandidates()
        {
            var candidates = new List<StealCandidate>();
            for (int baseNumber = 3; baseNumber >= 1; baseNumber--)
            {
                var baseState = _state.Bases[baseNumber - 1];
                if (!baseState.Occupied || baseState.Player == null)
                    continue;

                int targetBase = baseNumber + 1;
                bool nextBaseOccupiedByRunnerAlsoStealing = targetBase <= 3 &&
                    _state.Bases[targetBase - 1].Occupied &&
                    candidates.Any(c => c.FromBase == targetBase);
                if (targetBase <= 3 && _state.Bases[targetBase - 1].Occupied && !nextBaseOccupiedByRunnerAlsoStealing)
                    continue;

                candidates.Add(new StealCandidate(baseNumber, targetBase, baseState.Player));
            }

            return candidates;
        }

        private bool BaseHasRunner(int baseNumber, Player runner)
        {
            if (baseNumber < 1 || baseNumber > 3 || runner == null)
                return false;
            var baseState = _state.Bases[baseNumber - 1];
            return baseState.Occupied && baseState.Player != null && baseState.Player.Id == runner.Id;
        }

        private BaseRunnerSnapshot SnapshotBase(int baseNumber)
        {
            var baseState = _state.Bases[baseNumber - 1];
            return new BaseRunnerSnapshot(
                baseState.Label,
                baseState.RunnerColor,
                baseState.Player,
                baseState.Team,
                baseState.CourtesyForPlayer,
                baseState.ResponsiblePitcherId,
                baseState.Earned);
        }

        private void RestoreBase(int baseNumber, BaseRunnerSnapshot snapshot)
        {
            if (baseNumber < 1 || baseNumber > 3 || snapshot.Player == null)
                return;

            var baseState = _state.Bases[baseNumber - 1];
            baseState.Occupied = true;
            baseState.Label = snapshot.Label;
            baseState.RunnerColor = snapshot.RunnerColor;
            baseState.Player = snapshot.Player;
            baseState.Team = snapshot.Team;
            baseState.CourtesyForPlayer = snapshot.CourtesyForPlayer;
            baseState.ResponsiblePitcherId = snapshot.ResponsiblePitcherId;
            baseState.Earned = snapshot.Earned;
        }

        private void ClearBaseSlot(int baseNumber)
        {
            if (baseNumber < 1 || baseNumber > 3)
                return;

            var baseState = _state.Bases[baseNumber - 1];
            baseState.Occupied = false;
            baseState.Label = "";
            baseState.RunnerColor = _state.OffenseColor;
            baseState.Player = null;
            baseState.Team = null;
            baseState.CourtesyForPlayer = null;
            baseState.ResponsiblePitcherId = Guid.Empty;
            baseState.Earned = true;
        }

        private Player FindDefensivePlayer(string label)
            => _state.Fielders.FirstOrDefault(f => string.Equals(f.Label, label, StringComparison.OrdinalIgnoreCase))?.Player;

        private Player TagFielderForTarget(int targetBase)
        {
            if (targetBase >= 4)
                return FindDefensivePlayer("C");
            if (targetBase == 3)
                return FindDefensivePlayer("3B");
            if (targetBase == 2)
                return FindDefensivePlayer("SS") ?? FindDefensivePlayer("2B");
            return FindDefensivePlayer("1B");
        }

        private static bool IsPitcherOrCatcherPosition(Player player)
            => (player?.Positions ?? "")
                .Split(new[] { '/', ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Any(p => string.Equals(p.Trim(), "C", StringComparison.OrdinalIgnoreCase));

        private static string DefensiveStealCallLabel(DefensiveStealCall call)
        {
            return call switch
            {
                DefensiveStealCall.HoldRunner => "Hold Runner",
                DefensiveStealCall.SlideStep => "Slide Step",
                DefensiveStealCall.Pitchout => "Pitchout",
                DefensiveStealCall.Pickoff => "Pickoff",
                _ => "Normal"
            };
        }

        private static string BaseLabel(int baseNumber)
        {
            return baseNumber switch
            {
                1 => "1B",
                2 => "2B",
                3 => "3B",
                4 => "Home",
                _ => "base"
            };
        }

        private static bool IsFlyOutLabel(string? label)
        {
            if (string.IsNullOrWhiteSpace(label))
                return false;

            return label.IndexOf("Fly", StringComparison.OrdinalIgnoreCase) >= 0 ||
                label.IndexOf("LF", StringComparison.OrdinalIgnoreCase) >= 0 ||
                label.IndexOf("CF", StringComparison.OrdinalIgnoreCase) >= 0 ||
                label.IndexOf("RF", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsPopOutLabel(string? label)
        {
            if (string.IsNullOrWhiteSpace(label))
                return false;

            return label.IndexOf("Pop", StringComparison.OrdinalIgnoreCase) >= 0 ||
                label.IndexOf("Out: C", StringComparison.OrdinalIgnoreCase) >= 0 ||
                label.IndexOf("Out: P", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private int PlayerRating(Player? player, Func<Player, int> selector, int fallback)
        {
            if (player == null)
                return fallback;
            int rating = InjuryEngine.EffectiveRating(player, selector(player));
            return RankingGameModifier.Apply(rating, _rankingModifier.BoostForTeam(TeamForPlayer(player)));
        }

        private Team? TeamForPlayer(Player? player)
        {
            if (player == null)
                return null;
            if (_state?.AwayTeam?.Roster?.Any(p => p != null && p.Id == player.Id) == true)
                return _state.AwayTeam;
            if (_state?.HomeTeam?.Roster?.Any(p => p != null && p.Id == player.Id) == true)
                return _state.HomeTeam;
            return null;
        }

        private void RegisterParticipation(Player? player, Team? team, InjuryExposureType exposure)
            => InjuryEngine.TryParticipationInjury(player, team, _rng, exposure);

        private void PlaySafeCallSound()
        {
            _safeCallSound.PlayOnce(LaunchSoundPlayer.FindSafeCall());
        }

        private readonly struct StealCandidate
        {
            public StealCandidate(int fromBase, int targetBase, Player runner)
            {
                FromBase = fromBase;
                TargetBase = targetBase;
                Runner = runner;
            }

            public int FromBase { get; }
            public int TargetBase { get; }
            public Player Runner { get; }
        }

        private readonly struct LiveContestedBasePlay
        {
            public LiveContestedBasePlay(Player runner, int fromBase, int targetBase, bool batterRunner, bool creditsHit)
            {
                Runner = runner;
                FromBase = fromBase;
                TargetBase = targetBase;
                BatterRunner = batterRunner;
                CreditsHit = creditsHit;
            }

            public Player Runner { get; }
            public int FromBase { get; }
            public int TargetBase { get; }
            public bool BatterRunner { get; }
            public bool CreditsHit { get; }
        }

        private readonly struct BaseRunnerSnapshot
        {
            public BaseRunnerSnapshot(string label, Color runnerColor, Player? player, Team? team, Player? courtesyForPlayer, Guid responsiblePitcherId, bool earned)
            {
                Label = label;
                RunnerColor = runnerColor;
                Player = player;
                Team = team;
                CourtesyForPlayer = courtesyForPlayer;
                ResponsiblePitcherId = responsiblePitcherId;
                Earned = earned;
            }

            public string Label { get; }
            public Color RunnerColor { get; }
            public Player? Player { get; }
            public Team? Team { get; }
            public Player? CourtesyForPlayer { get; }
            public Guid ResponsiblePitcherId { get; }
            public bool Earned { get; }
        }

        private void PlayPitcherChangeSound()
        {
            string path = LaunchSoundPlayer.FindRandomPitcherChange(_rng);
            if (!string.IsNullOrWhiteSpace(path))
                _pitcherChangeSound.PlayOnce(path);
        }

        private void PlayPitchThrowSound()
        {
            string path = LaunchSoundPlayer.FindRandomPitchThrow(_rng);
            if (!string.IsNullOrWhiteSpace(path))
                _pitchThrowSound.PlayOnce(path);
        }

        private static int CountPitchingOptions(Team team)
        {
            return LineupEngine.GetPitchingStaff(team).Count;
        }

        private Player CurrentPitcher()
            => _state.CurrentPitcherPlayer();

        private void ApplyAllStarPitchingRuleIfNeeded()
        {
            if (_state == null || _gameComplete)
                return;
            if (_state.Phase != GameplayRenderingPhase.Ready && _state.Phase != GameplayRenderingPhase.DeadBall)
                return;

            Team team = _state.FieldingTeam;
            if (team?.PitchingPlan?.UseAllStarPitchingRules != true)
                return;

            string key = team.Id + "|" + _state.Inning + "|" + (_state.TopHalf ? "T" : "B");
            if (_allStarPitchingAppliedKeys.Contains(key))
                return;

            int pitcherIndex = PitchingRotationEngine.AllStarPitcherIndexForInning(team, _state.Inning);
            if (pitcherIndex < 0)
            {
                if (_state.Inning > _state.RegulationInnings && _state.AwayScore == _state.HomeScore)
                {
                    _state.ModeLabel = "All-Star Game ends tied: " + team.ScoreboardName + " has no remaining pitcher.";
                    CompleteGame();
                }
                _allStarPitchingAppliedKeys.Add(key);
                return;
            }

            int pitcherCount = CountPitchingOptions(team);
            int normalizedCurrent = PositiveModulo(_state.CurrentPitcherIndex, Math.Max(1, pitcherCount));
            if (pitcherIndex != normalizedCurrent)
            {
                Player oldPitcher = CurrentPitcher();
                FinalizeCurrentPitcherInning();
                _state.CurrentPitcherIndex = pitcherIndex;
                _state.CurrentEmergencyPitcherId = null;
                Player newPitcher = CurrentPitcher();
                _state.SeedFielders();
                RegisterRelieverEntryIfNeeded(newPitcher);
                MarkCurrentPitcherAppeared();
                _state.ModeLabel = "All-Star pitcher: " + (newPitcher?.Name ?? "Pitcher") + " for inning " + _state.Inning;
                if (oldPitcher?.Id != newPitcher?.Id)
                {
                    PlayPitcherChangeSound();
                    TriggerCutscene(CutsceneTrigger.PitcherChange, team);
                }
            }

            _allStarPitchingAppliedKeys.Add(key);
        }

        private PlayerGameLine? LiveLine(Player? player, Team? team, bool pitcher)
        {
            if (player == null || team == null)
                return null;

            var line = _liveLines.FirstOrDefault(l => l.PlayerId == player.Id && l.TeamId == team.Id && l.Pitcher == pitcher);
            if (line != null)
                return line;

            line = new PlayerGameLine
            {
                TeamId = team.Id,
                PlayerId = player.Id,
                PlayerName = player.Name,
                Pitcher = pitcher,
                StartingPitcher = pitcher && (player.Id == _awayStarterPitcherId || player.Id == _homeStarterPitcherId),
                Classification = player.Classification,
                InitialClassification = player.InitialClassification == PlayerClassification.Unassigned ? player.Classification : player.InitialClassification
            };
            _liveLines.Add(line);
            return line;
        }

        private static bool HasLiveStats(PlayerGameLine line)
        {
            if (line == null)
                return false;

            return line.R != 0 ||
                line.AB != 0 ||
                line.H != 0 ||
                line.Doubles != 0 ||
                line.Triples != 0 ||
                line.HR != 0 ||
                line.RBI != 0 ||
                line.BB != 0 ||
                line.IBB != 0 ||
                line.SO != 0 ||
                line.SB != 0 ||
                line.CS != 0 ||
                line.HBP != 0 ||
                line.SH != 0 ||
                line.SF != 0 ||
                line.FlyOuts != 0 ||
                line.GroundOuts != 0 ||
                line.PopOuts != 0 ||
                line.GroundedIntoDoublePlays != 0 ||
                line.ReachedOnError != 0 ||
                line.IPOuts != 0 ||
                line.ER != 0 ||
                line.RunsAllowed != 0 ||
                line.K != 0 ||
                line.HitsAllowed != 0 ||
                line.DoublesAllowed != 0 ||
                line.TriplesAllowed != 0 ||
                line.WalksAllowed != 0 ||
                line.IntentionalWalksAllowed != 0 ||
                line.HomeRunsAllowed != 0 ||
                line.HitBatters != 0 ||
                line.WildPitches != 0 ||
                line.Balks != 0 ||
                line.BattersFaced != 0 ||
                line.PitchCount != 0 ||
                line.Wins != 0 ||
                line.Losses != 0 ||
                line.Saves != 0 ||
                line.Holds != 0 ||
                line.BlownSaves != 0 ||
                line.CompleteGames != 0 ||
                line.Shutouts != 0 ||
                line.Putouts != 0 ||
                line.Assists != 0 ||
                line.Errors != 0 ||
                line.DefensiveOuts != 0 ||
                line.DefensiveDoublePlays != 0 ||
                line.TeamDoublePlaysTurned != 0 ||
                line.PassedBalls != 0 ||
                line.StolenBasesAllowed != 0 ||
                line.CatcherCaughtStealing != 0 ||
                line.GamesMissedInjury != 0;
        }

        private void RecordLiveHitStats(Player? batter, Player pitcher, LiveHitType hitType, int runsScored)
        {
            var batterLine = LiveLine(batter, _state.BattingTeam, pitcher: false);
            if (batterLine != null)
            {
                batterLine.AB++;
                batterLine.H++;
                batterLine.RBI += runsScored;
                if (hitType == LiveHitType.Double)
                    batterLine.Doubles++;
                else if (hitType == LiveHitType.Triple)
                    batterLine.Triples++;
                else if (hitType == LiveHitType.HomeRun)
                {
                    batterLine.HR++;
                    batterLine.R++;
                }
            }

            var pitcherLine = LiveLine(pitcher, _state.FieldingTeam, pitcher: true);
            if (pitcherLine != null)
            {
                pitcherLine.BattersFaced++;
                pitcherLine.HitsAllowed++;
                if (hitType == LiveHitType.Double)
                    pitcherLine.DoublesAllowed++;
                else if (hitType == LiveHitType.Triple)
                    pitcherLine.TriplesAllowed++;
                else if (hitType == LiveHitType.HomeRun)
                    pitcherLine.HomeRunsAllowed++;
            }
        }

        private void RecordLiveWalkStats(Player? batter, Player pitcher, int runsScored, bool intentional = false)
        {
            var batterLine = LiveLine(batter, _state.BattingTeam, pitcher: false);
            if (batterLine != null)
            {
                batterLine.BB++;
                if (intentional)
                    batterLine.IBB++;
                batterLine.RBI += runsScored;
            }

            var pitcherLine = LiveLine(pitcher, _state.FieldingTeam, pitcher: true);
            if (pitcherLine != null)
            {
                pitcherLine.BattersFaced++;
                pitcherLine.WalksAllowed++;
                if (intentional)
                    pitcherLine.IntentionalWalksAllowed++;
            }
        }

        private void RecordLiveOutStats(Player? batter, Player pitcher, string label)
        {
            bool strikeout = label != null && label.IndexOf("Strikeout", StringComparison.OrdinalIgnoreCase) >= 0;
            bool flyOut = IsFlyOutLabel(label);
            bool popOut = IsPopOutLabel(label);
            bool groundOut = !strikeout && !flyOut && !popOut;
            var batterLine = LiveLine(batter, _state.BattingTeam, pitcher: false);
            if (batterLine != null)
            {
                batterLine.AB++;
                if (strikeout)
                    batterLine.SO++;
                else if (flyOut)
                    batterLine.FlyOuts++;
                else if (popOut)
                    batterLine.PopOuts++;
                else if (groundOut)
                    batterLine.GroundOuts++;
            }

            var pitcherLine = LiveLine(pitcher, _state.FieldingTeam, pitcher: true);
            if (pitcherLine != null)
            {
                pitcherLine.BattersFaced++;
                pitcherLine.IPOuts++;
                if (strikeout)
                    pitcherLine.K++;
            }
        }

        private void RecordLiveSacrificeBuntStats(Player? batter, Player pitcher, int runsScored)
        {
            var batterLine = LiveLine(batter, _state.BattingTeam, pitcher: false);
            if (batterLine != null)
            {
                batterLine.SH++;
                batterLine.RBI += runsScored;
            }

            var pitcherLine = LiveLine(pitcher, _state.FieldingTeam, pitcher: true);
            if (pitcherLine != null)
            {
                pitcherLine.BattersFaced++;
                pitcherLine.IPOuts++;
            }
        }

        private void RecordLiveSacrificeFlyStats(Player? batter, Player pitcher, int runsScored)
        {
            var batterLine = LiveLine(batter, _state.BattingTeam, pitcher: false);
            if (batterLine != null)
            {
                batterLine.SF++;
                batterLine.FlyOuts++;
                batterLine.RBI += runsScored;
            }

            var pitcherLine = LiveLine(pitcher, _state.FieldingTeam, pitcher: true);
            if (pitcherLine != null)
            {
                pitcherLine.BattersFaced++;
                pitcherLine.IPOuts++;
            }
        }

        private void RecordLiveErrorStats(Player? batter, Player pitcher, GameplayRenderingPlayerMarker fielder)
        {
            var batterLine = LiveLine(batter, _state.BattingTeam, pitcher: false);
            if (batterLine != null)
            {
                batterLine.AB++;
                batterLine.ReachedOnError++;
            }

            var pitcherLine = LiveLine(pitcher, _state.FieldingTeam, pitcher: true);
            if (pitcherLine != null)
                pitcherLine.BattersFaced++;

            var fielderLine = LiveLine(fielder?.Player, _state.FieldingTeam, pitcher: false);
            if (fielderLine != null)
                fielderLine.Errors++;
        }

        private int FindNextAvailablePitcherIndex(Team team, int startIndex)
        {
            int pitcherCount = CountPitchingOptions(team);
            if (pitcherCount <= 0)
                return -1;

            for (int offset = 0; offset < pitcherCount; offset++)
            {
                int index = PositiveModulo(startIndex + offset, pitcherCount);
                Player pitcher = GameplayRules.GetPitcher(team, index);
                bool blockedStarter = PitchingRotationEngine.IsStarterBlockedFromRelief(team, pitcher);
                if (pitcher != null && !blockedStarter && !_pitchersRemovedByRunRule.Contains(pitcher.Id))
                    return index;
            }

            return -1;
        }

        private int ScoreForBattingTeam()
            => _state.TopHalf ? _state.AwayScore : _state.HomeScore;

        private int CaptureRunsScoredByBattingTeam(Action scoringAction)
        {
            _state.DrainScoredRunners();
            int beforeDiff = _state.AwayScore - _state.HomeScore;
            int before = ScoreForBattingTeam();
            scoringAction?.Invoke();
            int runs = Math.Max(0, ScoreForBattingTeam() - before);
            _pendingScoredRunners = _state.DrainScoredRunners();
            UpdatePitcherDecisionCandidates(beforeDiff, _state.AwayScore - _state.HomeScore, _pendingScoredRunners);
            PlayRunsScoredSound(runs);
            if (runs > 0)
                TriggerCutscene(CutsceneTrigger.RunScored, _state.BattingTeam);
            return runs;
        }

        private void UpdatePitcherDecisionCandidates(int beforeDiff, int afterDiff, IReadOnlyList<GameplayScoredRunner> scoredRunners)
        {
            if (afterDiff == 0)
            {
                _winningPitcherCandidateId = null;
                _losingPitcherCandidateId = null;
                return;
            }
            if (beforeDiff != 0 && Math.Sign(beforeDiff) == Math.Sign(afterDiff))
                return;

            bool awayLeading = afterDiff > 0;
            Team winningTeam = awayLeading ? _state.AwayTeam : _state.HomeTeam;
            _winningPitcherCandidateId = PitcherForTeam(winningTeam)?.Id;
            var goAheadRunner = scoredRunners?.FirstOrDefault();
            _losingPitcherCandidateId = goAheadRunner != null && goAheadRunner.ResponsiblePitcherId != Guid.Empty
                ? goAheadRunner.ResponsiblePitcherId
                : CurrentPitcher()?.Id;
        }

        private Player? PitcherForTeam(Team? team)
        {
            if (team == null) return null;
            Guid? emergencyId = team.Id == _state.AwayTeam?.Id ? _state.AwayEmergencyPitcherId : _state.HomeEmergencyPitcherId;
            var emergency = team.Roster?.FirstOrDefault(p => p.Id == emergencyId);
            if (emergency != null) return emergency;
            int index = team.Id == _state.AwayTeam?.Id ? _state.AwayPitcherIndex : _state.HomePitcherIndex;
            return GameplayRules.GetPitcher(team, index);
        }

        private void PlayRunsScoredSound(int runs)
        {
            if (runs <= 0)
                return;

            string path = LaunchSoundPlayer.FindScoredRunCall();
            if (!string.IsNullOrWhiteSpace(path))
                _scoredRunSound.PlayOnce(path);
        }

        private void RegisterCurrentPitcherRunsAllowed(int runs, PitcherRunCharge charge = PitcherRunCharge.Earned)
        {
            if (runs <= 0)
                return;

            var charges = _pendingScoredRunners?.Take(runs).ToList() ?? new List<GameplayScoredRunner>();
            if (charges.Count == 0)
                charges = _state.DrainScoredRunners().Take(runs).ToList();
            _pendingScoredRunners = Array.Empty<GameplayScoredRunner>();
            while (charges.Count < runs)
            {
                charges.Add(new GameplayScoredRunner
                {
                    ResponsiblePitcherId = CurrentPitcher()?.Id ?? Guid.Empty,
                    Earned = charge == PitcherRunCharge.Earned
                });
            }

            foreach (var scored in charges)
            {
                Player pitcher = PitcherById(scored.ResponsiblePitcherId) ?? CurrentPitcher();
                if (pitcher == null)
                    continue;
                bool earned = charge == PitcherRunCharge.Earned && scored.Earned;
                RegisterPitcherRunAllowed(pitcher, earned);
                var pitcherLine = LiveLine(pitcher, TeamForPlayer(pitcher), pitcher: true);
                if (pitcherLine != null)
                {
                    pitcherLine.RunsAllowed++;
                    if (earned)
                        pitcherLine.ER++;
                }
                if (_reliefPitcherFatigue.TryGetValue(pitcher.Id, out ReliefPitcherFatigueState reliefState) &&
                    reliefState.EnteredInSaveSituation && !TeamHasLead(TeamForPlayer(pitcher)))
                {
                    reliefState.LeadPreserved = false;
                }
            }
        }

        private bool TeamHasLead(Team? team)
        {
            if (team == null)
                return false;
            int teamScore = team.Id == _state.AwayTeam?.Id ? _state.AwayScore : _state.HomeScore;
            int opponentScore = team.Id == _state.AwayTeam?.Id ? _state.HomeScore : _state.AwayScore;
            return teamScore > opponentScore;
        }

        private void CreditDefensiveOuts(int outs)
        {
            if (outs <= 0)
                return;
            foreach (Player player in _state.Fielders
                .Select(marker => marker?.Player)
                .OfType<Player>()
                .GroupBy(player => player.Id)
                .Select(group => group.First()))
            {
                PlayerGameLine line = LiveLine(player, _state.FieldingTeam, pitcher: false);
                if (line != null)
                    line.DefensiveOuts += outs;
            }
        }

        private Player? PitcherById(Guid pitcherId)
            => pitcherId == Guid.Empty ? null :
                (_state.AwayTeam?.Roster?.FirstOrDefault(p => p.Id == pitcherId) ??
                 _state.HomeTeam?.Roster?.FirstOrDefault(p => p.Id == pitcherId));

        private void RegisterPitcherRunAllowed(Player pitcher, bool earned)
        {
            if (pitcher == null || _pitchersRemovedByRunRule.Contains(pitcher.Id))
                return;

            var ruleState = GetPitcherRunRuleState(pitcher);

            int inning = Math.Max(1, _state.Inning);
            ruleState.RunsAllowedByInning.TryGetValue(inning, out int existing);
            ruleState.RunsAllowedByInning[inning] = existing + 1;
            if (earned)
            {
                ruleState.EarnedRunsAllowedByInning.TryGetValue(inning, out int existingEarned);
                ruleState.EarnedRunsAllowedByInning[inning] = existingEarned + 1;
            }
            EnforcePitcherRunRuleIfNeeded(pitcher, ruleState);
        }

        private void MarkCurrentPitcherAppeared()
        {
            Player pitcher = CurrentPitcher();
            if (pitcher == null)
                return;

            var ruleState = GetPitcherRunRuleState(pitcher);
            int inning = Math.Max(1, _state.Inning);
            if (!ruleState.RunsAllowedByInning.ContainsKey(inning))
                ruleState.RunsAllowedByInning[inning] = 0;
            if (!ruleState.EarnedRunsAllowedByInning.ContainsKey(inning))
                ruleState.EarnedRunsAllowedByInning[inning] = 0;
        }

        private PitcherRunRuleState GetPitcherRunRuleState(Player pitcher)
        {
            if (!_pitcherRunRules.TryGetValue(pitcher.Id, out var ruleState))
            {
                ruleState = new PitcherRunRuleState();
                _pitcherRunRules[pitcher.Id] = ruleState;
            }

            return ruleState;
        }

        private void EnforcePitcherRunRuleIfNeeded(Player pitcher, PitcherRunRuleState ruleState)
        {
            if (pitcher == null || ruleState == null || _pitchersRemovedByRunRule.Contains(pitcher.Id))
                return;

            string reason = "";
            int inning = Math.Max(1, _state.Inning);
            int oneInningRuns = RunsAllowedAcrossInnings(ruleState, inning, 1);
            int twoInningRuns = RunsAllowedAcrossInnings(ruleState, inning, 2);
            int threeInningRuns = RunsAllowedAcrossInnings(ruleState, inning, 3);

            if (oneInningRuns >= OneInningForcedRemovalRuns)
                reason = "5 runs in an inning";
            else if (twoInningRuns >= TwoInningForcedRemovalRuns)
                reason = "6 runs across two innings";
            else if (threeInningRuns >= ThreeInningForcedRemovalRuns)
                reason = "7 runs across three innings";

            if (!string.IsNullOrWhiteSpace(reason))
                ForceCurrentPitcherRemoval(pitcher, reason);
        }

        private static int RunsAllowedAcrossInnings(PitcherRunRuleState ruleState, int endingInning, int inningCount)
        {
            int total = 0;
            int firstInning = Math.Max(1, endingInning - inningCount + 1);
            for (int inning = firstInning; inning <= endingInning; inning++)
            {
                if (ruleState.RunsAllowedByInning.TryGetValue(inning, out int runs))
                    total += runs;
            }

            return total;
        }

        private void FinalizeCurrentPitcherInning()
        {
            Player pitcher = CurrentPitcher();
            if (pitcher == null || !_pitcherRunRules.TryGetValue(pitcher.Id, out var ruleState))
                return;

            int inning = Math.Max(1, _state.Inning);
            if (!ruleState.RunsAllowedByInning.ContainsKey(inning) &&
                !ruleState.EarnedRunsAllowedByInning.ContainsKey(inning))
            {
                return;
            }

            if (!ruleState.FinalizedInnings.Add(inning))
                return;

            int earnedRuns = ruleState.EarnedRunsAllowedByInning.TryGetValue(inning, out int er) ? er : 0;
            if (earnedRuns <= 0)
            {
                ruleState.ConsecutiveScorelessInnings++;
                int earnedBoost = Math.Clamp(ruleState.ConsecutiveScorelessInnings - 4, 0, 4) * 10;
                if (earnedBoost > ruleState.AdvancementBoostPercent)
                    ruleState.AdvancementBoostPercent = earnedBoost;
                if (ruleState.ConsecutiveScorelessInnings >= 8)
                    ruleState.EarnedRunReductionImmune = true;
                return;
            }

            ruleState.ConsecutiveScorelessInnings = 0;
        }

        private int CurrentPitcherEarnedRunReductionPercent()
        {
            Player pitcher = CurrentPitcher();
            if (pitcher == null || !_pitcherRunRules.TryGetValue(pitcher.Id, out var ruleState))
                return 0;
            if (ruleState.EarnedRunReductionImmune)
                return 0;

            int inning = Math.Max(1, _state.Inning);
            int earnedRuns = EarnedRunsAllowedAcrossInnings(ruleState, inning, 3);
            return Math.Max(0, earnedRuns / 5) * 10;
        }

        private int CurrentPitcherAdvancementBoostPercent()
        {
            Player pitcher = CurrentPitcher();
            if (pitcher == null || !_pitcherRunRules.TryGetValue(pitcher.Id, out var ruleState))
                return 0;

            return ruleState.AdvancementBoostPercent;
        }

        private static int EarnedRunsAllowedAcrossInnings(PitcherRunRuleState ruleState, int endingInning, int inningCount)
        {
            int total = 0;
            int firstInning = Math.Max(1, endingInning - inningCount + 1);
            for (int inning = firstInning; inning <= endingInning; inning++)
            {
                if (ruleState.EarnedRunsAllowedByInning.TryGetValue(inning, out int runs))
                    total += runs;
            }

            return total;
        }

        private void ForceCurrentPitcherRemoval(Player pitcher, string reason)
        {
            if (pitcher == null)
                return;

            FinalizeCurrentPitcherInning();
            _pitchersRemovedByRunRule.Add(pitcher.Id);
            int nextIndex = FindNextAvailablePitcherIndex(_state.FieldingTeam, _state.CurrentPitcherIndex + 1);
            if (nextIndex < 0)
            {
                Player emergencyPitcher = FindEmergencyPositionPlayer(_state.FieldingTeam);
                if (emergencyPitcher == null)
                {
                    _state.ModeLabel = pitcher.Name + " must be removed: " + reason + " (no position player available)";
                    return;
                }

                ApplyEmergencyPitchingStats(emergencyPitcher, _state.FieldingTeam);
                if (!PitchProfileEngine.IsPitcherClassified(emergencyPitcher))
                    PitchProfileEngine.AssignEmergencyPitchArsenal(emergencyPitcher, _rng);
                _state.CurrentEmergencyPitcherId = emergencyPitcher.Id;
                bool lostDh = _state.LoseDesignatedHitterForFieldingTeam(emergencyPitcher);
                _state.SeedFielders();
                RegisterRelieverEntryIfNeeded(emergencyPitcher);
                MarkCurrentPitcherAppeared();
                _state.ModeLabel = pitcher.Name + " removed: " + reason + ". Emergency pitcher: " + emergencyPitcher.Name + (lostDh ? " (DH lost)" : "");
                PlayPitcherChangeSound();
                TriggerCutscene(CutsceneTrigger.PitcherChange, _state.FieldingTeam);
                return;
            }

            _state.CurrentPitcherIndex = nextIndex;
            _state.CurrentEmergencyPitcherId = null;
            Player replacement = CurrentPitcher();
            bool pitcherEnteredOrder = _state.EnsurePitcherBatsForFieldingTeam(replacement, pitcher);
            _state.SeedFielders();
            RegisterRelieverEntryIfNeeded(replacement);
            MarkCurrentPitcherAppeared();
            _state.ModeLabel = pitcher.Name + " removed: " + reason + ". New pitcher: " + (replacement?.Name ?? "Pitcher") + (pitcherEnteredOrder ? " (batting order updated)" : "");
            PlayPitcherChangeSound();
            TriggerCutscene(CutsceneTrigger.PitcherChange, _state.FieldingTeam);
        }

        private Player FindEmergencyPositionPlayer(Team team)
        {
            if (team?.Roster == null)
                return null;

            var occupiedRunnerIds = _state.Bases
                .Where(b => b.Occupied && b.Player != null)
                .Select(b => b.Player)
                .OfType<Player>()
                .Select(player => player.Id)
                .ToHashSet();

            return team.Roster
                .Where(p => p != null)
                .Where(p => p.Role != PlayerRole.Pitcher)
                .Where(p => !_pitchersRemovedByRunRule.Contains(p.Id))
                .Where(p => !occupiedRunnerIds.Contains(p.Id))
                .Where(InjuryEngine.IsAvailable)
                .OrderByDescending(p => p.Overall)
                .FirstOrDefault()
                ?? team.Roster
                    .Where(p => p != null)
                    .Where(p => p.Role != PlayerRole.Pitcher)
                    .Where(p => !_pitchersRemovedByRunRule.Contains(p.Id))
                    .OrderByDescending(p => p.Overall)
                    .FirstOrDefault();
        }

        private static void ApplyEmergencyPitchingStats(Player emergencyPitcher, Team team)
        {
            if (emergencyPitcher == null)
                return;

            Player topStarter = team?.Roster?
                .Where(p => p.Role == PlayerRole.Pitcher)
                .OrderByDescending(p => p.Pitching + p.Stamina)
                .FirstOrDefault();
            if (topStarter == null)
                return;

            emergencyPitcher.Pitching = Math.Max(emergencyPitcher.Pitching, Math.Max(1, (int)Math.Round(topStarter.Pitching * 0.25)));
            emergencyPitcher.Stamina = Math.Max(emergencyPitcher.Stamina, Math.Max(1, (int)Math.Round(topStarter.Stamina * 0.25)));
        }

        private void RestoreLiveRulesState(GameplayLiveRulesState liveRules)
        {
            if (liveRules == null)
            {
                InitializeStarterFatigueTracking();
                return;
            }

            Player awayStarter = GameplayRules.GetPitcher(_state.AwayTeam, _state.AwayPitcherIndex);
            Player homeStarter = GameplayRules.GetPitcher(_state.HomeTeam, _state.HomePitcherIndex);
            _awayStarterPitcherId = liveRules.AwayStarterPitcherId != Guid.Empty
                ? liveRules.AwayStarterPitcherId
                : awayStarter?.Id ?? Guid.Empty;
            _homeStarterPitcherId = liveRules.HomeStarterPitcherId != Guid.Empty
                ? liveRules.HomeStarterPitcherId
                : homeStarter?.Id ?? Guid.Empty;
            _state.AwayEmergencyPitcherId = liveRules.AwayEmergencyPitcherId;
            _state.HomeEmergencyPitcherId = liveRules.HomeEmergencyPitcherId;
            _winningPitcherCandidateId = liveRules.WinningPitcherCandidateId;
            _losingPitcherCandidateId = liveRules.LosingPitcherCandidateId;
            _awayStarterPitchCount = Math.Max(0, liveRules.AwayStarterPitchCount);
            _homeStarterPitchCount = Math.Max(0, liveRules.HomeStarterPitchCount);
            _awayStarterPostLimitBaserunnersThisInning = Math.Max(0, liveRules.AwayStarterPostLimitBaserunnersThisInning);
            _homeStarterPostLimitBaserunnersThisInning = Math.Max(0, liveRules.HomeStarterPostLimitBaserunnersThisInning);
            _awayMoundVisitsThisInning = Math.Max(0, liveRules.AwayMoundVisitsThisInning);
            _homeMoundVisitsThisInning = Math.Max(0, liveRules.HomeMoundVisitsThisInning);
            _awayCoachVisitBoostActive = liveRules.AwayCoachVisitBoostActive;
            _homeCoachVisitBoostActive = liveRules.HomeCoachVisitBoostActive;

            _pitchersRemovedByRunRule.Clear();
            foreach (Guid pitcherId in liveRules.PitchersRemovedByRunRule ?? new List<Guid>())
            {
                if (pitcherId != Guid.Empty)
                    _pitchersRemovedByRunRule.Add(pitcherId);
            }

            _reliefPitcherFatigue.Clear();
            foreach (var item in liveRules.ReliefPitcherFatigue ?? new Dictionary<Guid, GameplayReliefPitcherState>())
            {
                if (item.Key == Guid.Empty || item.Value == null)
                    continue;

                _reliefPitcherFatigue[item.Key] = new ReliefPitcherFatigueState
                {
                    OutsRecorded = Math.Max(0, item.Value.OutsRecorded),
                    PostLimitBaserunnersThisInning = Math.Max(0, item.Value.PostLimitBaserunnersThisInning),
                    FirstBatterBoostAvailable = item.Value.FirstBatterBoostAvailable,
                    FirstBatterFaced = item.Value.FirstBatterFaced,
                    AppearanceInitialized = item.Value.AppearanceInitialized,
                    EnteredInSaveSituation = item.Value.EnteredInSaveSituation,
                    EnteredWithThreeRunLead = item.Value.EnteredWithThreeRunLead,
                    EnteredWithTyingRunThreat = item.Value.EnteredWithTyingRunThreat,
                    LeadPreserved = item.Value.LeadPreserved
                };
            }

            _pitcherRunRules.Clear();
            foreach (var item in liveRules.PitcherRunRules ?? new Dictionary<Guid, GameplayPitcherRunRuleState>())
            {
                if (item.Key == Guid.Empty || item.Value == null)
                    continue;

                _pitcherRunRules[item.Key] = new PitcherRunRuleState
                {
                    RunsAllowedByInning = CopyNonNegativeInningDictionary(item.Value.RunsAllowedByInning),
                    EarnedRunsAllowedByInning = CopyNonNegativeInningDictionary(item.Value.EarnedRunsAllowedByInning),
                    FinalizedInnings = new HashSet<int>((item.Value.FinalizedInnings ?? new HashSet<int>()).Where(i => i > 0)),
                    ConsecutiveScorelessInnings = Math.Max(0, item.Value.ConsecutiveScorelessInnings),
                    AdvancementBoostPercent = Math.Clamp(item.Value.AdvancementBoostPercent, 0, 40),
                    EarnedRunReductionImmune = item.Value.EarnedRunReductionImmune
                };
            }
        }

        private static Dictionary<int, int> CopyNonNegativeInningDictionary(Dictionary<int, int> source)
        {
            var result = new Dictionary<int, int>();
            foreach (var item in source ?? new Dictionary<int, int>())
            {
                if (item.Key > 0)
                    result[item.Key] = Math.Max(0, item.Value);
            }

            return result;
        }

        private void InitializeStarterFatigueTracking()
        {
            Player awayStarter = GameplayRules.GetPitcher(_state.AwayTeam, _state.AwayPitcherIndex);
            Player homeStarter = GameplayRules.GetPitcher(_state.HomeTeam, _state.HomePitcherIndex);
            _awayStarterPitcherId = awayStarter?.Id ?? Guid.Empty;
            _homeStarterPitcherId = homeStarter?.Id ?? Guid.Empty;
            _awayStarterPitchCount = 0;
            _homeStarterPitchCount = 0;
            _awayStarterPostLimitBaserunnersThisInning = 0;
            _homeStarterPostLimitBaserunnersThisInning = 0;
            _reliefPitcherFatigue.Clear();
            _pitcherRunRules.Clear();
            _pitchersRemovedByRunRule.Clear();
        }

        private void CountPitchForCurrentPitcher()
        {
            Player? currentPitcher = CurrentPitcher();
            if (currentPitcher == null)
                return;

            var pitcherLine = LiveLine(currentPitcher, _state.FieldingTeam, pitcher: true);
            if (pitcherLine != null)
                pitcherLine.PitchCount++;
            RegisterParticipation(currentPitcher, _state.FieldingTeam, InjuryExposureType.PitchThrown);

            if (!TryGetCurrentStarter(out Player starter, out bool awayStarter))
                return;

            if (awayStarter)
                _awayStarterPitchCount++;
            else
                _homeStarterPitchCount++;

            int pitchCount = awayStarter ? _awayStarterPitchCount : _homeStarterPitchCount;
            Team team = awayStarter ? _state.AwayTeam : _state.HomeTeam;
            if (pitchCount > StarterPitchLimit(starter, team))
                InjuryEngine.TryEventInjury(starter, _rng, 12);
        }

        private void RegisterCurrentPitcherBaserunnerAllowed(PitcherBaserunnerSource source)
        {
            if (source == PitcherBaserunnerSource.FieldingError)
                return;

            if (TryGetCurrentStarter(out Player starter, out bool awayStarter))
            {
                int pitchCount = awayStarter ? _awayStarterPitchCount : _homeStarterPitchCount;
                if (pitchCount < StarterPitchLimit(starter, awayStarter ? _state.AwayTeam : _state.HomeTeam))
                    return;

                if (awayStarter)
                    _awayStarterPostLimitBaserunnersThisInning++;
                else
                    _homeStarterPostLimitBaserunnersThisInning++;
                return;
            }

            if (TryGetCurrentRelieverState(out var reliefState) && reliefState.OutsRecorded >= ReliefPitcherMaxOuts)
                reliefState.PostLimitBaserunnersThisInning++;
        }

        private int CurrentPitcherPerformanceAdjustmentPercent()
        {
            int earnedRunPenalty = CurrentPitcherEarnedRunReductionPercent();
            int advancementBoost = CurrentPitcherAdvancementBoostPercent();
            int coachVisitBoost = CurrentCoachVisitBoostPercent();
            if (!TryGetCurrentStarter(out Player starter, out bool awayStarter))
            {
                int reliefPenalty = CurrentRelieverFatiguePenaltyPercent();
                return reliefPenalty + earnedRunPenalty - advancementBoost - CurrentRelieverFirstBatterBoostPercent() - coachVisitBoost;
            }

            int pitchCount = awayStarter ? _awayStarterPitchCount : _homeStarterPitchCount;
            if (pitchCount < StarterPitchLimit(starter, awayStarter ? _state.AwayTeam : _state.HomeTeam))
                return earnedRunPenalty - advancementBoost - coachVisitBoost;

            int baserunners = awayStarter
                ? _awayStarterPostLimitBaserunnersThisInning
                : _homeStarterPostLimitBaserunnersThisInning;

            int starterFatiguePenalty = 0;
            if (baserunners >= 3)
                starterFatiguePenalty = 20;
            else if (baserunners >= 2)
                starterFatiguePenalty = 10;

            return starterFatiguePenalty + earnedRunPenalty - advancementBoost - coachVisitBoost;
        }

        private int CurrentCoachVisitBoostPercent()
        {
            if (!CurrentFieldingTeamCoachVisitBoostActive())
                return 0;

            var coach = CurrentFieldingHeadCoach();
            return coach?.Style switch
            {
                CoachStyle.BelowAverage => 10,
                CoachStyle.AboveAverage => 20,
                CoachStyle.Championship => 25,
                _ => 15
            };
        }

        private bool CurrentPitcherStrikeoutsBecomeSingles()
        {
            if (!TryGetCurrentStarter(out Player starter, out bool awayStarter))
                return TryGetCurrentRelieverState(out var reliefState) &&
                    reliefState.OutsRecorded >= ReliefPitcherMaxOuts &&
                    reliefState.PostLimitBaserunnersThisInning >= 4;

            int pitchCount = awayStarter ? _awayStarterPitchCount : _homeStarterPitchCount;
            if (pitchCount < StarterPitchLimit(starter, awayStarter ? _state.AwayTeam : _state.HomeTeam))
                return false;

            int baserunners = awayStarter
                ? _awayStarterPostLimitBaserunnersThisInning
                : _homeStarterPostLimitBaserunnersThisInning;
            return baserunners >= 4;
        }

        private void RecordFatiguedStrikeoutSingle()
        {
            Player? batter = _state.CurrentBatterPlayer();
            Player pitcher = CurrentPitcher();
            _state.ResetCount();
            int runsScored = CaptureRunsScoredByBattingTeam(() => _state.AdvanceRunnersTwoBasesWithSingle());
            _state.ApplyCourtesyRunners(PickCourtesyRunner);
            RecordLiveHitStats(batter, pitcher, LiveHitType.Single, runsScored);
            _state.AdvanceBatter();
            RegisterCurrentPitcherBaserunnerAllowed(PitcherBaserunnerSource.FatigueEligible);
            CompletePlateAppearanceForCurrentReliever(batter);
            _state.ModeLabel = "Pitcher fatigue: strikeout becomes single";
            LogPlay(_state.ModeLabel);
            RegisterCurrentPitcherRunsAllowed(runsScored);
        }

        private bool TryGetCurrentStarter(out Player starter, out bool awayStarter)
        {
            starter = CurrentPitcher();
            awayStarter = !_state.TopHalf;
            if (starter == null)
                return false;

            Guid trackedStarterId = awayStarter ? _awayStarterPitcherId : _homeStarterPitcherId;
            return trackedStarterId != Guid.Empty && starter.Id == trackedStarterId;
        }

        private void RegisterRelieverEntryIfNeeded(Player pitcher)
        {
            if (pitcher == null || IsTrackedStarter(pitcher))
                return;

            var state = GetRelieverState(pitcher);
            if (!state.AppearanceInitialized)
            {
                int lead = CurrentFieldingTeamScore() - CurrentBattingTeamScore();
                int tyingRunDistance = _state.Bases.Count(baseState => baseState.Occupied) + 2;
                state.AppearanceInitialized = true;
                state.EnteredWithThreeRunLead = lead > 0 && lead <= 3;
                state.EnteredWithTyingRunThreat = lead > 0 && tyingRunDistance >= lead;
                state.EnteredInSaveSituation = state.EnteredWithThreeRunLead || state.EnteredWithTyingRunThreat;
                state.LeadPreserved = true;
            }
            state.PostLimitBaserunnersThisInning = 0;
            state.FirstBatterBoostAvailable = _state.Outs > 0 ||
                _state.Balls > 0 ||
                _state.Strikes > 0 ||
                _state.Bases.Any(b => b.Occupied);
            state.FirstBatterFaced = false;
        }

        private int CurrentRelieverFatiguePenaltyPercent()
        {
            Player pitcher = CurrentPitcher();
            int backToBackPenalty = Math.Max(0, pitcher?.ConsecutiveReliefGames ?? 0) * 10;
            if (!TryGetCurrentRelieverState(out var reliefState) || reliefState.OutsRecorded < ReliefPitcherMaxOuts)
                return backToBackPenalty;

            if (reliefState.PostLimitBaserunnersThisInning >= 3)
                return backToBackPenalty + 20;
            if (reliefState.PostLimitBaserunnersThisInning >= 2)
                return backToBackPenalty + 10;
            return backToBackPenalty;
        }

        private int CurrentRelieverFirstBatterBoostPercent()
        {
            if (!TryGetCurrentRelieverState(out var reliefState) ||
                reliefState.FirstBatterFaced ||
                !reliefState.FirstBatterBoostAvailable)
            {
                return 0;
            }

            Player pitcher = CurrentPitcher();
            Player? batter = _state.CurrentBatterPlayer();
            return SameSideMatchup(pitcher, batter) ? MidInningReliefBoostPercent : 0;
        }

        private void CompletePlateAppearanceForCurrentReliever(Player? completedBatter)
        {
            RegisterParticipation(completedBatter, _state.BattingTeam, InjuryExposureType.PlateAppearance);
            if (TryGetCurrentRelieverState(out var reliefState))
                reliefState.FirstBatterFaced = true;
            SetCurrentFieldingTeamCoachVisitBoost(false);
        }

        private void RegisterCurrentRelieverOut()
        {
            if (TryGetCurrentRelieverState(out var reliefState))
                reliefState.OutsRecorded++;
        }

        private bool TryGetCurrentRelieverState([NotNullWhen(true)] out ReliefPitcherFatigueState? reliefState)
        {
            reliefState = null;
            Player pitcher = CurrentPitcher();
            if (pitcher == null || IsTrackedStarter(pitcher))
                return false;

            reliefState = GetRelieverState(pitcher);
            return true;
        }

        private ReliefPitcherFatigueState GetRelieverState(Player pitcher)
        {
            if (!_reliefPitcherFatigue.TryGetValue(pitcher.Id, out var state))
            {
                state = new ReliefPitcherFatigueState();
                _reliefPitcherFatigue[pitcher.Id] = state;
            }

            return state;
        }

        private int CurrentFieldingTeamScore() => _state.TopHalf ? _state.HomeScore : _state.AwayScore;
        private int CurrentBattingTeamScore() => _state.TopHalf ? _state.AwayScore : _state.HomeScore;

        private bool IsTrackedStarter(Player pitcher)
            => pitcher != null && (pitcher.Id == _awayStarterPitcherId || pitcher.Id == _homeStarterPitcherId);

        private static bool SameSideMatchup(Player pitcher, Player? batter)
        {
            string throws = EffectiveThrowSide(pitcher);
            string bats = EffectiveBatSide(batter);
            return (throws == "L" || throws == "R") && throws == bats;
        }

        private static string EffectiveBatSide(Player? player)
        {
            string side = NormalizeSide(player?.Bats);
            if (!string.IsNullOrEmpty(side))
                return side;

            int bucket = StablePlayerBucket(player);
            if (bucket < 45) return "R";
            if (bucket < 80) return "L";
            return "S";
        }

        private static string EffectiveThrowSide(Player player)
        {
            string side = NormalizeSide(player?.Throws);
            if (!string.IsNullOrEmpty(side) && side != "S")
                return side;

            return StablePlayerBucket(player) < 25 ? "L" : "R";
        }

        private static string NormalizeSide(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "";

            string normalized = value.Trim().ToUpperInvariant();
            if (normalized.StartsWith("L")) return "L";
            if (normalized.StartsWith("R")) return "R";
            if (normalized.StartsWith("S")) return "S";
            return "";
        }

        private static int StablePlayerBucket(Player? player)
        {
            if (player == null)
                return 0;

            byte[] bytes = player.Id.ToByteArray();
            int value = 0;
            for (int i = 0; i < bytes.Length; i++)
                value = ((value * 31) + bytes[i]) & 0x7fffffff;
            return value % 100;
        }

        private void ResetCurrentDefensivePitcherInningBaserunners()
        {
            if (_state.TopHalf)
            {
                _homeStarterPostLimitBaserunnersThisInning = 0;
                _homeMoundVisitsThisInning = 0;
                _homeCoachVisitBoostActive = false;
            }
            else
            {
                _awayStarterPostLimitBaserunnersThisInning = 0;
                _awayMoundVisitsThisInning = 0;
                _awayCoachVisitBoostActive = false;
            }

            if (TryGetCurrentRelieverState(out var reliefState))
                reliefState.PostLimitBaserunnersThisInning = 0;
        }

        private static int StarterPitchLimit(Player? pitcher, Team? team)
        {
            int basePitchCount = pitcher?.CareerPitchCount > 0
                ? pitcher.CareerPitchCount
                : SeniorStarterPitchLimit;

            double multiplier = pitcher?.Classification switch
            {
                PlayerClassification.Freshman => 0.70,
                PlayerClassification.Sophomore => 0.80,
                PlayerClassification.Junior => 0.90,
                PlayerClassification.Senior => 1.00,
                _ => 1.00
            };

            int classifiedLimit = Math.Max(1, (int)Math.Round(basePitchCount * multiplier));
            if (pitcher == null || team == null)
                return classifiedLimit;
            return PitchingRotationEngine.ApplyStarterPitchCountPenalty(pitcher, team, classifiedLimit);
        }

        private static int AdjustedChance(int baseChance, int adjustmentPercent)
            => Math.Clamp(baseChance + adjustmentPercent, 0, 95);

        private void TickWatchMode()
        {
            _watchTick++;
            if (_state.Phase == GameplayRenderingPhase.Ready ||
                _state.Phase == GameplayRenderingPhase.DeadBall)
            {
                if (_watchTick % 42 == 0)
                {
                    if (TryCpuStealBeforePitch())
                        return;
                    ChooseCpuStrategiesBeforePitch();
                    StartPitch();
                }
                return;
            }

            if (_state.Phase == GameplayRenderingPhase.Pitching &&
                _ballProgress > 0.55f &&
                _watchTick % 18 == 0 &&
                _rng.Next(100) < 48)
            {
                ResolveCpuSwingOrTake();
            }
        }

        private void TickPlayerVsCpuMode()
        {
            _watchTick++;
            if ((_state.Phase == GameplayRenderingPhase.Ready || _state.Phase == GameplayRenderingPhase.DeadBall) &&
                !HumanControlsFieldingTeam() && _watchTick % 42 == 0)
            {
                ChooseCpuStrategiesBeforePitch();
                StartPitch();
                return;
            }

            if (_state.Phase == GameplayRenderingPhase.Pitching && !HumanControlsBattingTeam() &&
                _ballProgress > 0.55f && _watchTick % 12 == 0)
            {
                ResolveCpuSwingOrTake();
            }
        }

        private bool HumanControlsBattingTeam()
            => (_mode == GameMode.PlayerVsPlayer && InputSourceOwnsTeam(_state.BattingTeam)) ||
               (_mode == GameMode.UserVsCpu && _state.BattingTeam?.Id == _userControlledTeamId);

        private bool HumanControlsFieldingTeam()
            => (_mode == GameMode.PlayerVsPlayer && InputSourceOwnsTeam(_state.FieldingTeam)) ||
               (_mode == GameMode.UserVsCpu && _state.FieldingTeam?.Id == _userControlledTeamId);

        private bool InputSourceOwnsTeam(Team team)
        {
            if (team == null)
                return false;
            if (!_activeInputSource.HasValue)
                return true;
            return _activeInputSource.Value == GameplayInputSource.Keyboard
                ? team.Id == _keyboardControlledTeamId
                : team.Id == _controllerControlledTeamId;
        }

        private GameplayInputSnapshot CurrentFieldingInputSnapshot()
        {
            if (_mode != GameMode.PlayerVsPlayer)
                return _input.Snapshot;
            return _state.FieldingTeam?.Id == _keyboardControlledTeamId
                ? _input.KeyboardSnapshot
                : _input.ControllerSnapshot;
        }

        private GameplayPitchType SelectedPitchTypeForFieldingTeam()
        {
            if (_mode != GameMode.PlayerVsPlayer)
                return ValidateSelectedPitchType(_input.SelectedPitchType);
            var selected = _state.FieldingTeam?.Id == _keyboardControlledTeamId
                ? _input.KeyboardSelectedPitchType
                : _input.ControllerSnapshot.SelectedPitchType;
            return ValidateSelectedPitchType(selected);
        }

        private GameplayPitchType ValidateSelectedPitchType(GameplayPitchType selected)
        {
            var pitcher = CurrentPitcher();
            PitchProfileEngine.NormalizePlayerPitchProfiles(pitcher, _rng);
            if (PitchProfileEngine.CanThrow(pitcher, selected))
                return selected;

            var fallback = PitchProfileEngine.BestPitch(pitcher);
            _state.ModeLabel = pitcher == null
                ? "Pitch: " + fallback
                : pitcher.Name + " does not throw " + selected + ". Pitch: " + fallback;
            return fallback;
        }

        private void PrepareCpuPitch()
        {
            Player pitcher = CurrentPitcher();
            Player? batter = _state.CurrentBatterPlayer();
            if (batter == null)
                return;

            var decision = GameplayCpu.ChoosePitch(
                _rng,
                pitcher,
                batter,
                _state.Balls,
                _state.Strikes,
                _state.Bases[0].Occupied,
                _state.Bases[1].Occupied,
                _state.Bases[2].Occupied,
                _state.Outs,
                CurrentPitchCount(),
                (_mode == GameMode.CpuVsCpuWatch || _mode == GameMode.QuickSim) ? GameplayCpu.CpuMode.CpuVsCpuWatch : GameplayCpu.CpuMode.UserVsCpu);
            _currentPitchType = MapCpuPitchType(decision.PitchType);
            _ballTarget = new PointF(
                0.5f + (float)decision.AimX * 0.08f,
                0.84f + (float)decision.AimY * 0.08f);
        }

        private void ResolveCpuSwingOrTake()
        {
            Player? batter = _state.CurrentBatterPlayer();
            if (batter == null)
                return;

            var decision = GameplayCpu.DecideSwing(
                _rng,
                batter,
                CurrentPitcher(),
                MapGameplayPitchType(_currentPitchType),
                PitchZoneX(),
                PitchZoneY(),
                _state.Balls,
                _state.Strikes,
                Math.Max(0, (int)Math.Round((1f - _ballProgress) * 360)),
                _offensiveStrategyCall == OffensiveStrategyCall.HitAndRun,
                _offensiveStrategyCall == OffensiveStrategyCall.Bunt || _offensiveStrategyCall == OffensiveStrategyCall.SacrificeBunt);
            if (!decision.ShouldSwing)
                return;

            ResolveSwing(decision.SwingType == GameplayCpu.SwingType.Power
                ? SharedSwingType.Power
                : decision.SwingType == GameplayCpu.SwingType.Contact
                    ? SharedSwingType.Contact
                    : SharedSwingType.Normal,
                Math.Clamp(1.0 - Math.Abs(decision.TimingOffsetMs) / 120.0, 0.05, 1.0));
        }

        private int CurrentPitchCount()
        {
            var line = LiveLine(CurrentPitcher(), _state.FieldingTeam, pitcher: true);
            return line?.PitchCount ?? 0;
        }

        private static GameplayPitchType MapCpuPitchType(GameplayCpu.PitchType pitchType)
            => pitchType switch
            {
                GameplayCpu.PitchType.Curveball => GameplayPitchType.Curveball,
                GameplayCpu.PitchType.Slider => GameplayPitchType.Slider,
                GameplayCpu.PitchType.Changeup => GameplayPitchType.Changeup,
                GameplayCpu.PitchType.Splitter => GameplayPitchType.Splitter,
                GameplayCpu.PitchType.Forkball => GameplayPitchType.Forkball,
                GameplayCpu.PitchType.Knuckleball => GameplayPitchType.Knuckleball,
                _ => GameplayPitchType.Fastball
            };

        private static GameplayCpu.PitchType MapGameplayPitchType(GameplayPitchType pitchType)
            => pitchType switch
            {
                GameplayPitchType.Curveball => GameplayCpu.PitchType.Curveball,
                GameplayPitchType.Slider => GameplayCpu.PitchType.Slider,
                GameplayPitchType.Changeup => GameplayCpu.PitchType.Changeup,
                GameplayPitchType.Splitter => GameplayCpu.PitchType.Splitter,
                GameplayPitchType.Forkball => GameplayCpu.PitchType.Forkball,
                GameplayPitchType.Knuckleball => GameplayCpu.PitchType.Knuckleball,
                _ => GameplayCpu.PitchType.Fastball
            };

        private bool TryCpuStealBeforePitch()
        {
            var candidate = FindLeadStealCandidate();
            if (candidate.Runner == null)
                return false;

            Player pitcher = CurrentPitcher();
            Player? catcher = FindDefensivePlayer("C") ?? _state.FieldingTeam?.Roster?.FirstOrDefault(IsPitcherOrCatcherPosition);
            int scoreDifferential = _state.TopHalf
                ? _state.AwayScore - _state.HomeScore
                : _state.HomeScore - _state.AwayScore;
            bool nextBaseOccupied = candidate.TargetBase <= 3 && _state.Bases[candidate.TargetBase - 1].Occupied;
            if (nextBaseOccupied)
            {
                _defensiveStealCall = DefensiveStealCall.Normal;
                return false;
            }

            bool rightStealCall = StealEngine.ShouldCpuAttemptSteal(
                _rng,
                candidate.Runner,
                pitcher,
                catcher,
                candidate.FromBase,
                _state.Outs,
                _state.Balls,
                _state.Strikes,
                scoreDifferential,
                nextBaseOccupied);
            if (!CoachDecisionEngine.ShouldCallRiskyOffense(
                    _rng,
                    CurrentBattingHeadCoach(),
                    rightStealCall,
                    IsGameOnLineForBattingTeam(),
                    IsStealScoringOpportunity(candidate)))
            {
                _defensiveStealCall = DefensiveStealCall.Normal;
                return false;
            }

            _defensiveStealCall = catcher == null
                ? DefensiveStealCall.Normal
                : ChooseCpuStealDefense(pitcher, catcher, candidate.Runner, candidate.FromBase);
            ResolveSteal(candidate, _defensiveStealCall);
            return true;
        }

        private void ChooseCpuStrategiesBeforePitch()
        {
            ResetStrategyCalls();

            if (ShouldCpuCallSacrificeBunt())
            {
                _offensiveStrategyCall = OffensiveStrategyCall.SacrificeBunt;
                _defensiveAlignmentCall = ChooseCpuBuntDefense();
                _state.ModeLabel = "CPU strategy: sacrifice bunt";
                return;
            }

            if (ShouldCpuCallHitAndRun())
            {
                _offensiveStrategyCall = OffensiveStrategyCall.HitAndRun;
                _state.ModeLabel = "CPU strategy: hit and run";
            }
        }

        private bool ShouldCpuCallSacrificeBunt()
        {
            if (_state.Outs >= 2 || !_state.Bases.Any(baseState => baseState.Occupied))
                return false;

            int differential = BattingTeamScoreDifferential();
            bool closeNeedRun = differential <= 0 && differential >= -3;
            bool runnerOnThird = _state.Bases.Length >= 3 && _state.Bases[2].Occupied;
            bool runnerOnFirstOrSecond = _state.Bases[0].Occupied || _state.Bases[1].Occupied;
            bool rightCall = runnerOnThird && closeNeedRun ||
                runnerOnFirstOrSecond && _state.Outs == 0 && Math.Abs(differential) <= 2;
            return CoachDecisionEngine.ShouldCallSafeOffense(
                _rng,
                CurrentBattingHeadCoach(),
                rightCall,
                IsGameOnLineForBattingTeam(),
                HasScoringOpportunity());
        }

        private bool ShouldCpuCallHitAndRun()
        {
            if (_state.Outs >= 2 || !HasHitAndRunRunner())
                return false;

            int differential = BattingTeamScoreDifferential();
            Player batter = _state.CurrentBatterPlayer();
            Player runner = BestHitAndRunRunner();
            int runnerSpeed = PlayerRating(runner, p => p.Speed, 50) + PlayerRating(runner, p => p.BaseRunning, 50);
            int contact = PlayerRating(batter, p => p.Contact, 50);
            bool rightCall = differential >= -3 && differential <= 2 && runnerSpeed >= 100 && contact >= 48;
            return CoachDecisionEngine.ShouldCallRiskyOffense(
                _rng,
                CurrentBattingHeadCoach(),
                rightCall,
                IsGameOnLineForBattingTeam(),
                HasScoringOpportunity());
        }

        private Player BestHitAndRunRunner()
        {
            for (int i = Math.Min(2, _state.Bases.Length - 1); i >= 0; i--)
            {
                var baseState = _state.Bases[i];
                if (!baseState.Occupied || baseState.Player == null)
                    continue;

                int targetBase = i + 2;
                if (targetBase >= 4 || !_state.Bases[targetBase - 1].Occupied)
                    return baseState.Player;
            }

            return null;
        }

        private DefensiveAlignmentCall ChooseCpuBuntDefense()
        {
            bool runnerOnThird = _state.Bases.Length >= 3 && _state.Bases[2].Occupied;
            bool runThreat = _state.Bases.Any(baseState => baseState.Occupied) && _state.Outs < 2;
            bool preventCall = CoachDecisionEngine.ShouldCallPreventDefense(
                _rng,
                CurrentFieldingHeadCoach(),
                runThreat,
                IsGameOnLineForBattingTeam(),
                runThreat);

            if (!preventCall)
                return DefensiveAlignmentCall.Normal;

            if (runnerOnThird)
                return _rng.Next(100) < 72 ? DefensiveAlignmentCall.WheelPlay : DefensiveAlignmentCall.InfieldIn;

            return _rng.Next(100) < 58 ? DefensiveAlignmentCall.InfieldIn : DefensiveAlignmentCall.WheelPlay;
        }

        private DefensiveStealCall ChooseCpuStealDefense(Player pitcher, Player catcher, Player runner, int fromBase)
        {
            var coach = CurrentFieldingHeadCoach();
            DefensiveStealCall bestCall = StealEngine.ChooseCpuDefense(_rng, pitcher, catcher, runner, fromBase);
            bool runThreat = fromBase >= 2 || IsGameOnLineForBattingTeam();
            bool correctPreventCall = bestCall != DefensiveStealCall.Normal;
            if (!CoachDecisionEngine.ShouldCallPreventDefense(_rng, coach, correctPreventCall, IsGameOnLineForBattingTeam(), runThreat))
                return coach?.Strategy == CoachStrategy.Safe && runThreat ? DefensiveStealCall.HoldRunner : DefensiveStealCall.Normal;

            return bestCall;
        }

        private int BattingTeamScoreDifferential()
            => _state.TopHalf ? _state.AwayScore - _state.HomeScore : _state.HomeScore - _state.AwayScore;


        private void PrimaryAction()
        {
            if (_state.Phase == GameplayRenderingPhase.Pitching)
            {
                if (HumanControlsBattingTeam())
                    ResolveSwing(SharedSwingType.Normal);
                return;
            }

            if (HumanControlsFieldingTeam())
                StartPitch();
        }

        private void StartPitch()
        {
            if (_gameComplete)
                return;

            DefensiveStealCall stealCall = _defensiveStealCall;
            if (TryResolveBalk(stealCall, _pickoffAttemptsThisPlateAppearance))
            {
                _defensiveStealCall = DefensiveStealCall.Normal;
                _pickoffAttemptsThisPlateAppearance = 0;
                return;
            }

            MarkCurrentPitcherAppeared();
            CountPitchForCurrentPitcher();
            _pickoffAttemptsThisPlateAppearance = 0;
            _state.Phase = GameplayRenderingPhase.Pitching;
            _state.ModeLabel = "Pitch";
            PlayPitchThrowSound();
            _defensiveStealCall = DefensiveStealCall.Normal;
            _state.BallVisible = true;
            _state.BallTrail = 1f;
            _ballStart = new PointF(0.5f, 0.62f);
            if (HumanControlsFieldingTeam())
            {
                _currentPitchType = SelectedPitchTypeForFieldingTeam();
                _ballTarget = new PointF(0.5f + (_rng.Next(-5, 6) * 0.008f), 0.84f);
            }
            else
            {
                PrepareCpuPitch();
            }
            _ballProgress = 0f;
            _pitchBreakSign = _rng.Next(2) == 0 ? -1 : 1;
            _knuckleWobbleSeed = _rng.NextDouble() * Math.PI * 2.0;
            _state.BallPosition = _ballStart;
        }

        private bool TryResolveBalk(DefensiveStealCall stealCall, int repeatedPickoffAttempts)
        {
            if (_state.Bases.All(baseState => !baseState.Occupied))
                return false;

            Player pitcher = CurrentPitcher();
            Player leadRunner = FindLeadStealCandidate().Runner;
            bool stealThreat = leadRunner != null &&
                (PlayerRating(leadRunner, p => p.Speed, 50) + PlayerRating(leadRunner, p => p.StealAggression, 50)) / 2 >= 62;
            bool highPressure = _state.Inning >= Math.Max(1, _state.RegulationInnings - 1) &&
                Math.Abs(_state.AwayScore - _state.HomeScore) <= 2;
            BalkResult result = BalkEngine.Roll(
                _rng,
                pitcher,
                CurrentPitcherPerformanceAdjustmentPercent(),
                stealCall,
                repeatedPickoffAttempts,
                _state.Bases[2].Occupied,
                stealThreat,
                highPressure);
            if (!result.IsBalk)
                return false;

            PlayerGameLine pitcherLine = LiveLine(pitcher, _state.FieldingTeam, pitcher: true);
            if (pitcherLine != null)
                pitcherLine.Balks++;

            Player scoringRunner = _state.Bases[2].Occupied ? _state.Bases[2].Player : null;
            int runsScored = CaptureRunsScoredByBattingTeam(() => _state.AdvanceRunners(false, _rng));
            if (runsScored > 0 && scoringRunner != null)
            {
                PlayerGameLine runnerLine = LiveLine(scoringRunner, _state.BattingTeam, pitcher: false);
                if (runnerLine != null)
                    runnerLine.R += runsScored;
            }
            RegisterCurrentPitcherRunsAllowed(runsScored);

            _state.ModeLabel = "Balk: " + (pitcher?.Name ?? "Pitcher") + " - " + result.Reason;
            LogPlay(_state.ModeLabel);
            _state.Phase = GameplayRenderingPhase.DeadBall;
            _state.BallVisible = false;
            _state.BallTrail = 0f;
            ResetStrategyCalls();

            if (!_state.TopHalf &&
                _state.Inning >= Math.Max(1, _state.RegulationInnings) &&
                _state.HomeScore > _state.AwayScore)
            {
                CompleteGame();
            }
            return true;
        }

        private void TickPitch()
        {
            _ballProgress = Math.Min(1f, _ballProgress + 0.045f);
            _state.BallPosition = ApplyPitchMovement(Lerp(_ballStart, _ballTarget, EaseOut(_ballProgress)), _ballProgress);
            _state.BallTrail = Math.Max(0f, 1f - _ballProgress);

            if (_ballProgress >= 1f)
                TakePitch();
        }

        private PointF ApplyPitchMovement(PointF basePosition, float progress)
        {
            if (progress <= 0.55f)
                return basePosition;

            float late = Math.Clamp((progress - 0.55f) / 0.45f, 0f, 1f);
            var pitcher = CurrentPitcher();
            int accuracy = PlayerRating(pitcher, p => p.Accuracy, 50);
            float controlMiss = Math.Clamp((99 - accuracy) / 99f, 0f, 1f);

            if (_currentPitchType == GameplayPitchType.Forkball)
            {
                float dive = late * late;
                float side = (0.018f + controlMiss * 0.035f) * _pitchBreakSign * dive;
                return new PointF(Clamp01(basePosition.X + side), Clamp01(basePosition.Y + 0.038f * dive));
            }

            if (_currentPitchType == GameplayPitchType.Knuckleball)
            {
                float wobble = (float)Math.Sin(_knuckleWobbleSeed + progress * 22.0) * (0.009f + controlMiss * 0.012f);
                float floatDrop = (float)Math.Sin(_knuckleWobbleSeed * 0.7 + progress * 15.0) * 0.006f;
                return new PointF(Clamp01(basePosition.X + wobble), Clamp01(basePosition.Y + floatDrop));
            }

            return basePosition;
        }

        private void TakePitch()
        {
            int adjustment = CurrentPitcherPerformanceAdjustmentPercent();
            var resolution = SharedGameEngine.ResolvePitch(_rng, BuildSharedPitchRequest(SharedSwingType.Take, 1.0, adjustment));
            if (resolution.ResultType == SharedPitchResultType.HitByPitch)
            {
                ResolveHitByPitch();
                _state.Phase = GameplayRenderingPhase.DeadBall;
                _state.BallTrail = 0f;
                ResetStrategyCalls();
                PlayAfterPitchRunnerPromptIfNeeded();
                return;
            }

            bool ball = resolution.ResultType == SharedPitchResultType.Ball;
            if (ball)
            {
                _state.Balls++;
                _state.ModeLabel = "Ball";
                LogPlay(_state.ModeLabel);
                PlayBallCallSound();
                if (_state.Balls >= 4)
                {
                    Player? batter = _state.CurrentBatterPlayer();
                    Player pitcher = CurrentPitcher();
                    int runsScored = CaptureRunsScoredByBattingTeam(() => _state.AdvanceRunners(true, _rng));
                    _state.ApplyCourtesyRunners(PickCourtesyRunner);
                    RecordLiveWalkStats(batter, pitcher, runsScored);
                    _state.AdvanceBatter();
                    _state.ResetCount();
                    RegisterCurrentPitcherBaserunnerAllowed(PitcherBaserunnerSource.FatigueEligible);
                    CompletePlateAppearanceForCurrentReliever(batter);
                    _state.ModeLabel = "Walk";
                    LogPlay(_state.ModeLabel);
                    PlayTakeYourBaseSound();
                    RegisterCurrentPitcherRunsAllowed(runsScored);
                }
                else
                {
                    TryResolvePitchEscape(adjustment);
                }
            }
            else
            {
                _state.Strikes++;
                _state.ModeLabel = "Strike";
                LogPlay(_state.ModeLabel);
                PlayStrikeCallSound();
                if (_state.Strikes >= 3)
                {
                    if (CurrentPitcherStrikeoutsBecomeSingles())
                        RecordFatiguedStrikeoutSingle();
                    else
                        RecordOut("Strikeout");
                }
                else
                {
                    TryResolvePitchEscape(adjustment);
                }
            }

            _state.Phase = GameplayRenderingPhase.DeadBall;
            _state.BallTrail = 0f;
            ResetStrategyCalls();
            PlayAfterPitchRunnerPromptIfNeeded();
        }

        private void ResolveSwing(SharedSwingType swingType, double? timingQuality = null)
        {
            if (_offensiveStrategyCall == OffensiveStrategyCall.SacrificeBunt ||
                _offensiveStrategyCall == OffensiveStrategyCall.Bunt)
            {
                ResolveSacrificeBuntSwing();
                return;
            }

            int adjustment = CurrentPitcherPerformanceAdjustmentPercent();
            double timing = timingQuality ?? Math.Clamp(1.0 - Math.Abs(_ballProgress - 0.72f) / 0.55f, 0.05, 1.0);
            var resolution = SharedGameEngine.ResolvePitch(_rng, BuildSharedPitchRequest(swingType, timing, adjustment));

            if (resolution.ResultType == SharedPitchResultType.InPlay)
            {
                _state.ResetCount();
                _state.ModeLabel = _offensiveStrategyCall == OffensiveStrategyCall.HitAndRun
                    ? "Hit and run: in play"
                    : _offensiveStrategyCall == OffensiveStrategyCall.Safe
                        ? "Safe: in play"
                        : "In play";
                PlayBatHitBallSound();
                PlayFlyBallSound();
                StartBallInPlayMusic();
                _state.Phase = GameplayRenderingPhase.BallInPlay;
                _ballStart = _state.BallPosition;
                _ballTarget = new PointF(_rng.Next(24, 77) / 100f, _rng.Next(24, 55) / 100f);
                _ballProgress = 0f;
                _state.BallTrail = 1f;
                _pendingBattedBallResult = SharedGameEngine.ResolveBattedBall(_rng, new SharedBattedBallRequest
                {
                    Batter = _state.CurrentBatterPlayer(),
                    Pitcher = CurrentPitcher(),
                    PitchType = _currentPitchType,
                    ContactQuality = resolution.ContactQuality,
                    PitcherAdjustmentPercent = adjustment,
                    BatterBoostPercent = _rankingModifier.BoostForTeam(_state.BattingTeam),
                    PitcherBoostPercent = _rankingModifier.BoostForTeam(_state.FieldingTeam),
                    DefenseFieldingRating = TeamFieldingRating(_state.FieldingTeam),
                    SafeApproach = _offensiveStrategyCall == OffensiveStrategyCall.Safe,
                    NoDoublesDefense = _defensiveAlignmentCall == DefensiveAlignmentCall.NoDoubles,
                    OutfieldIn = _defensiveAlignmentCall == DefensiveAlignmentCall.OutfieldIn
                });
                return;
            }

            if (resolution.ResultType == SharedPitchResultType.SwingingStrike)
            {
                _state.Strikes++;
                _state.ModeLabel = "Swinging strike";
                PlayStrikeCallSound();
                if (_state.Strikes >= 3)
                {
                    if (CurrentPitcherStrikeoutsBecomeSingles())
                        RecordFatiguedStrikeoutSingle();
                    else
                        RecordOut("Strikeout");
                }
                else
                {
                    TryResolvePitchEscape(adjustment);
                }
            }
            else if (resolution.ResultType == SharedPitchResultType.Foul)
            {
                bool foulFly = _rng.Next(100) < 45;
                _state.ModeLabel = foulFly ? "Foul fly" : "Foul";
                PlayBatHitBallSound();
                if (foulFly)
                    PlayFoulFlyBallSound();
                else
                    PlayFoulBallSound();
                if (_state.Strikes < 2)
                    _state.Strikes++;
            }

            StopBallInPlayMusic();
            _state.Phase = GameplayRenderingPhase.DeadBall;
            _state.BallTrail = 0f;
            ResetStrategyCalls();
            PlayAfterPitchRunnerPromptIfNeeded();
        }

        private SharedPitchRequest BuildSharedPitchRequest(SharedSwingType swingType, double timingQuality, int adjustment)
        {
            return new SharedPitchRequest
            {
                Batter = _state.CurrentBatterPlayer(),
                Pitcher = CurrentPitcher(),
                PitchType = _currentPitchType,
                SwingType = swingType,
                PitchX = PitchZoneX(),
                PitchY = PitchZoneY(),
                TimingQuality = timingQuality,
                Balls = _state.Balls,
                Strikes = _state.Strikes,
                PitcherAdjustmentPercent = adjustment,
                OffensiveStrategyModifier = CurrentOffensiveStrategyExecutionModifier(_offensiveStrategyCall),
                BatterBoostPercent = _rankingModifier.BoostForTeam(_state.BattingTeam),
                PitcherBoostPercent = _rankingModifier.BoostForTeam(_state.FieldingTeam)
            };
        }

        private double PitchZoneX() => (_ballTarget.X - 0.5f) / 0.08f;

        private double PitchZoneY() => (_ballTarget.Y - 0.84f) / 0.08f;

        private int TeamFieldingRating(Team team)
        {
            var card = LineupEngine.BuildLineupCard(team);
            var ratings = (card.DefensiveAssignments ?? new Dictionary<string, Player>(StringComparer.OrdinalIgnoreCase))
                .Where(pair => pair.Value != null)
                .Select(pair => PositionFieldingRating(pair.Value, pair.Key))
                .ToList();
            int rating = ratings.Count == 0 ? 50 : (int)Math.Round(ratings.Average());
            return _rankingModifier.Apply(team, rating);
        }

        private void ResolveSacrificeBuntSwing()
        {
            Player? batter = _state.CurrentBatterPlayer();
            Player pitcher = CurrentPitcher();
            PlayBatHitBallSound();

            int foulChance = _state.Strikes >= 2 ? 18 : 24;
            if (_rng.Next(100) < foulChance)
            {
                if (_state.Strikes >= 2)
                {
                    _state.ModeLabel = "Foul bunt strikeout";
                    RecordOut("Strikeout");
                }
                else
                {
                    _state.Strikes++;
                    _state.ModeLabel = "Bunt foul";
                    PlayFoulBallSound();
                }

                StopBallInPlayMusic();
                _state.Phase = GameplayRenderingPhase.DeadBall;
                _state.BallTrail = 0f;
                ResetStrategyCalls();
                PlayAfterPitchRunnerPromptIfNeeded();
                return;
            }

            GameplayRenderingPlayerMarker fielder = PickBuntFielder();
            int defenseScore = BuntDefenseScore(fielder);
            int buntScore = PlayerRating(batter, p => p.Contact, 50) +
                PlayerRating(batter, p => p.Speed, 50) / 2 +
                PlayerRating(batter, p => p.BaseRunning, 50) / 2 +
                CurrentOffensiveStrategyExecutionModifier(_offensiveStrategyCall) +
                _rng.Next(-18, 19);

            bool runnerOnThird = _state.Bases.Length >= 3 && _state.Bases[2].Occupied;
            if (runnerOnThird && _state.Outs < 2)
            {
                var scoringRunner = _state.Bases[2].Player;
                if (scoringRunner != null && defenseScore >= buntScore + 8)
                {
                    var play = new LiveContestedBasePlay(scoringRunner, 3, 4, batterRunner: false, creditsHit: false);
                    ResolveContestedBaseOut(play, fielder, batter, pitcher, LiveHitType.Single);
                    StopBallInPlayMusic();
                    _state.Phase = GameplayRenderingPhase.DeadBall;
                    _state.BallTrail = 0f;
                    ResetStrategyCalls();
                    PlayAfterPitchRunnerPromptIfNeeded();
                    return;
                }
            }
            else
            {
                var leadRunner = FindLeadForceBuntRunner();
                if (leadRunner.Runner != null && defenseScore >= buntScore + 14)
                {
                    var play = new LiveContestedBasePlay(leadRunner.Runner, leadRunner.FromBase, leadRunner.TargetBase, batterRunner: false, creditsHit: false);
                    ResolveContestedBaseOut(play, fielder, batter, pitcher, LiveHitType.Single);
                    StopBallInPlayMusic();
                    _state.Phase = GameplayRenderingPhase.DeadBall;
                    _state.BallTrail = 0f;
                    ResetStrategyCalls();
                    PlayAfterPitchRunnerPromptIfNeeded();
                    return;
                }
            }

            int runsScored = CaptureRunsScoredByBattingTeam(() => _state.AdvanceRunners(false, _rng));
            _state.ApplyCourtesyRunners(PickCourtesyRunner);
            RecordLiveSacrificeBuntStats(batter, pitcher, runsScored);
            RegisterCurrentPitcherRunsAllowed(runsScored);
            RecordDefensiveAssistAndPutout(fielder?.Player, TagFielderForTarget(1));
            CompleteRecordedOut(runsScored > 0 ? "Sacrifice bunt: run scores" : "Sacrifice bunt");
            StopBallInPlayMusic();
            _state.Phase = GameplayRenderingPhase.DeadBall;
            _state.BallTrail = 0f;
            ResetStrategyCalls();
            PlayAfterPitchRunnerPromptIfNeeded();
        }

        private GameplayRenderingPlayerMarker PickBuntFielder()
        {
            string[] labels = _defensiveAlignmentCall switch
            {
                DefensiveAlignmentCall.WheelPlay => new[] { "3B", "1B", "P", "C" },
                DefensiveAlignmentCall.DoublePlay => new[] { "P", "SS", "2B", "3B", "1B", "C" },
                _ => new[] { "P", "3B", "1B", "C" }
            };

            foreach (string label in labels.OrderBy(_ => _rng.Next()))
            {
                var fielder = _state.Fielders.FirstOrDefault(f => string.Equals(f.Label, label, StringComparison.OrdinalIgnoreCase));
                if (fielder?.Player != null)
                    return fielder;
            }

            return PickBallTargetFielder();
        }

        private int BuntDefenseScore(GameplayRenderingPlayerMarker? fielder)
        {
            Player? player = fielder?.Player;
            int score = PositionFieldingRating(player, fielder?.Label) +
                PlayerRating(player, p => p.ArmStrength, 50) / 2 +
                PlayerRating(player, p => p.Accuracy, 50) / 2 +
                _rng.Next(-18, 19);

            if (_defensiveAlignmentCall == DefensiveAlignmentCall.InfieldIn)
                score += 16;
            else if (_defensiveAlignmentCall == DefensiveAlignmentCall.DoublePlay)
                score += 12;
            else if (_defensiveAlignmentCall == DefensiveAlignmentCall.WheelPlay)
                score += 26;

            return score + CurrentBuntDefenseCoachModifier();
        }

        private StealCandidate FindLeadForceBuntRunner()
        {
            for (int baseNumber = 3; baseNumber >= 1; baseNumber--)
            {
                var baseState = _state.Bases[baseNumber - 1];
                if (!baseState.Occupied || baseState.Player == null)
                    continue;

                int targetBase = baseNumber + 1;
                if (targetBase <= 3 && _state.Bases[targetBase - 1].Occupied)
                    continue;

                return new StealCandidate(baseNumber, targetBase, baseState.Player);
            }

            return default;
        }

        private void PlayStrikeCallSound()
        {
            _playEventSound.PlayOnce(LaunchSoundPlayer.FindRandomStrikeCall(_rng));
        }

        private void PlayBallCallSound()
        {
            _playEventSound.PlayOnce(LaunchSoundPlayer.FindBallCall());
        }

        private void PlayTakeYourBaseSound()
        {
            _takeYourBaseSound.PlayOnce(LaunchSoundPlayer.FindTakeYourBaseCall());
        }

        private void PlayFoulBallSound()
        {
            _playEventSound.PlayOnce(LaunchSoundPlayer.FindFoulBallCall());
        }

        private void PlayFoulFlyBallSound()
        {
            _playEventSound.PlayOnce(LaunchSoundPlayer.FindFoulFlyBallCall());
        }

        private void PlayBatHitBallSound()
        {
            _batHitBallSound.PlayOnce(LaunchSoundPlayer.FindBatHitsBallCall());
        }

        private void PlayFlyBallSound()
        {
            _playEventSound.PlayOnce(LaunchSoundPlayer.FindFlyBallCall());
        }

        private void PlayUghImpactSound()
        {
            _playEventSound.PlayOnce(LaunchSoundPlayer.FindUghImpactCall());
        }

        private bool ShouldHitBatter(int pitcherAdjustment)
        {
            int chance = Math.Clamp(2 + pitcherAdjustment / 20, 1, 5);
            return _rng.Next(100) < chance;
        }

        private void ResolveHitByPitch()
        {
            Player? batter = _state.CurrentBatterPlayer();
            Player pitcher = CurrentPitcher();
            int runsScored = CaptureRunsScoredByBattingTeam(() => _state.AdvanceRunners(true, _rng));
            _state.ApplyCourtesyRunners(PickCourtesyRunner);
            var batterLine = LiveLine(batter, _state.BattingTeam, pitcher: false);
            if (batterLine != null)
            {
                batterLine.HBP++;
                batterLine.RBI += runsScored;
            }

            var pitcherLine = LiveLine(pitcher, _state.FieldingTeam, pitcher: true);
            if (pitcherLine != null)
            {
                pitcherLine.HitBatters++;
                pitcherLine.BattersFaced++;
            }
            if (batter != null)
                InjuryEngine.TryEventInjury(batter, _rng, 28);

            _state.AdvanceBatter();
            _state.ResetCount();
            RegisterCurrentPitcherBaserunnerAllowed(PitcherBaserunnerSource.FatigueEligible);
            CompletePlateAppearanceForCurrentReliever(batter);
            _state.ModeLabel = "Hit by pitch";
            LogPlay(_state.ModeLabel);
            PlayUghImpactSound();
            RegisterCurrentPitcherRunsAllowed(runsScored);
        }

        private void ResolveIntentionalWalk()
        {
            if (_gameComplete ||
                (_state.Phase != GameplayRenderingPhase.Ready && _state.Phase != GameplayRenderingPhase.DeadBall))
            {
                return;
            }

            Player? batter = _state.CurrentBatterPlayer();
            Player pitcher = CurrentPitcher();
            int runsScored = CaptureRunsScoredByBattingTeam(() => _state.AdvanceRunners(true, _rng));
            _state.ApplyCourtesyRunners(PickCourtesyRunner);
            RecordLiveWalkStats(batter, pitcher, runsScored, intentional: true);
            _state.AdvanceBatter();
            _state.ResetCount();
            RegisterCurrentPitcherBaserunnerAllowed(PitcherBaserunnerSource.FatigueEligible);
            CompletePlateAppearanceForCurrentReliever(batter);
            RegisterCurrentPitcherRunsAllowed(runsScored);
            ResetStrategyCalls();
            _defensiveStealCall = DefensiveStealCall.Normal;
            _state.Phase = GameplayRenderingPhase.DeadBall;
            _state.BallTrail = 0f;
            _state.ModeLabel = "Intentional walk";
            LogPlay(_state.ModeLabel);
            PlayTakeYourBaseSound();
            PlayAfterPitchRunnerPromptIfNeeded();
        }

        private void StartBallInPlayMusic()
        {
            if (_ballInPlayMusicActive)
                return;

            string path = LaunchSoundPlayer.FindChanceBgm();
            if (string.IsNullOrWhiteSpace(path))
                return;

            _ballInPlayMusicActive = true;
            _pausedTopHalfMusicForBallInPlay = _topHalfMusicPlaying;
            _pausedBottomHalfMusicForBallInPlay = _bottomHalfMusicPlaying;
            if (_pausedTopHalfMusicForBallInPlay)
                _topHalfMusic.Pause();
            if (_pausedBottomHalfMusicForBallInPlay)
                _bottomHalfMusic.Pause();

            _chanceBgmSound.PlayLoop(path);
        }

        private void StopBallInPlayMusic()
        {
            if (!_ballInPlayMusicActive)
                return;

            _chanceBgmSound.Stop();
            _ballInPlayMusicActive = false;
            if (!_gameComplete)
            {
                if (_pausedTopHalfMusicForBallInPlay && _topHalfMusicPlaying)
                    _topHalfMusic.Resume();
                if (_pausedBottomHalfMusicForBallInPlay && _bottomHalfMusicPlaying)
                    _bottomHalfMusic.Resume();
            }

            _pausedTopHalfMusicForBallInPlay = false;
            _pausedBottomHalfMusicForBallInPlay = false;
        }

        private static string LiveHitLabel(LiveHitType hitType, bool grandSlam)
        {
            return hitType switch
            {
                LiveHitType.Double => "Double",
                LiveHitType.Triple => "Triple",
                LiveHitType.HomeRun => grandSlam ? "Grand slam" : "Home run",
                _ => "Single"
            };
        }

        private LiveContestedBasePlay PickContestedBasePlay(LiveHitType hitType, GameplayRenderingPlayerMarker? fielder)
        {
            if (hitType == LiveHitType.HomeRun || fielder?.Player == null)
                return default;

            int basesAwarded = BasesAwardedForHit(hitType);
            for (int baseNumber = 3; baseNumber >= 1; baseNumber--)
            {
                var runnerBase = _state.Bases[baseNumber - 1];
                if (!runnerBase.Occupied || runnerBase.Player == null)
                    continue;

                int targetBase = Math.Min(4, baseNumber + basesAwarded);
                if (targetBase <= 3 && _state.Bases[targetBase - 1].Occupied)
                    continue;

                int throwChance = targetBase switch
                {
                    4 => 72,
                    3 => 58,
                    2 => baseNumber == 1 ? 70 : 48,
                    _ => 40
                };
                if (_defensiveAlignmentCall == DefensiveAlignmentCall.DoublePlay && baseNumber == 1 && targetBase == 2)
                    throwChance += 18;
                if (_defensiveAlignmentCall == DefensiveAlignmentCall.OutfieldIn && targetBase >= 4)
                    throwChance += 16;
                if (_defensiveAlignmentCall == DefensiveAlignmentCall.NoDoubles && hitType == LiveHitType.Double)
                    throwChance -= 14;
                throwChance += _state.Outs >= 2 ? 8 : 0;
                throwChance -= Math.Max(0, PlayerRating(runnerBase.Player, p => p.Speed, 50) - 65) / 3;
                if (_rng.Next(100) < Math.Clamp(throwChance, 18, 86))
                {
                    bool forceOut = baseNumber == 1 && targetBase == 2;
                    return new LiveContestedBasePlay(
                        runnerBase.Player,
                        fromBase: baseNumber,
                        targetBase: targetBase,
                        batterRunner: false,
                        creditsHit: !forceOut);
                }
            }

            if (hitType == LiveHitType.Single)
            {
                var batter = _state.CurrentBatterPlayer();
                if (batter == null)
                    return default;

                return new LiveContestedBasePlay(
                    batter,
                    fromBase: 0,
                    targetBase: 1,
                    batterRunner: true,
                    creditsHit: false);
            }

            return default;
        }

        private bool DefenseWinsContestedBasePlay(
            LiveContestedBasePlay play,
            GameplayRenderingPlayerMarker? fielder,
            LiveHitType hitType)
        {
            if (play.Runner == null || fielder?.Player == null)
                return false;

            Player targetFielder = TagFielderForTarget(play.TargetBase);
            int runnerScore =
                PlayerRating(play.Runner, p => p.Speed, 50) +
                PlayerRating(play.Runner, p => p.BaseRunning, 50) +
                _rng.Next(-16, 17);
            if (play.TargetBase >= 3)
                runnerScore += 8;
            if (hitType == LiveHitType.Double)
                runnerScore += 18;
            else if (hitType == LiveHitType.Triple)
                runnerScore += 30;

            int throwDistancePenalty = play.TargetBase switch
            {
                4 => 16,
                3 => 10,
                2 => 6,
                _ => 0
            };
            int defenseScore =
                PositionFieldingRating(fielder.Player, fielder.Label) / 2 +
                PlayerRating(fielder.Player, p => p.ArmStrength, PlayerRating(fielder.Player, p => p.Fielding, 50)) / 2 +
                PlayerRating(fielder.Player, p => p.Accuracy, PlayerRating(fielder.Player, p => p.Fielding, 50)) / 3 +
                PositionFieldingRating(targetFielder, TargetPositionForBase(play.TargetBase)) / 3 +
                PlayerRating(targetFielder, p => p.TagRating, PlayerRating(targetFielder, p => p.Fielding, 50)) / 3 +
                _rng.Next(-16, 17) -
                throwDistancePenalty;
            if (play.TargetBase == 1)
                defenseScore += 18;
            if (_defensiveAlignmentCall == DefensiveAlignmentCall.InfieldIn && play.TargetBase >= 3)
                defenseScore += 12;
            if (_defensiveAlignmentCall == DefensiveAlignmentCall.DoublePlay && play.TargetBase == 2)
                defenseScore += 18;
            if (_defensiveAlignmentCall == DefensiveAlignmentCall.OutfieldIn && play.TargetBase >= 4)
                defenseScore += 16;
            if (_defensiveAlignmentCall == DefensiveAlignmentCall.NoDoubles && play.TargetBase >= 4)
                defenseScore -= 12;
            if (_defensiveAlignmentCall == DefensiveAlignmentCall.WheelPlay)
                defenseScore += play.TargetBase >= 3 ? 18 : 10;

            return defenseScore >= runnerScore;
        }

        private void ResolveContestedBaseOut(
            LiveContestedBasePlay play,
            GameplayRenderingPlayerMarker? fielder,
            Player? batter,
            Player pitcher,
            LiveHitType hitType)
        {
            Player targetFielder = TagFielderForTarget(play.TargetBase);
            RegisterParticipation(play.Runner, _state.BattingTeam, InjuryExposureType.Collision);
            RegisterParticipation(targetFielder, _state.FieldingTeam, InjuryExposureType.Collision);
            RecordDefensiveAssistAndPutout(fielder?.Player, targetFielder);

            if (play.BatterRunner)
            {
                RecordOut("Out at " + BaseLabel(play.TargetBase) + ": " + ContestedThrowLabel(fielder, targetFielder));
                return;
            }

            int basesAwarded = play.CreditsHit ? BasesAwardedForHit(hitType) : 1;
            int runsScored = CaptureRunsScoredByBattingTeam(() =>
                ApplyBaserunningForHit(hitType, fielder, play.Runner.Id, basesAwarded));
            _state.ApplyCourtesyRunners(PickCourtesyRunner);
            if (play.CreditsHit)
                RecordLiveHitStats(batter, pitcher, hitType, runsScored);
            else
                RecordLiveFielderChoiceStats(batter, pitcher);

            RecordLiveBaserunnerOutStats(pitcher);
            RegisterCurrentPitcherRunsAllowed(runsScored);
            RegisterCurrentRelieverOut();
            _state.Outs++;
            CreditDefensiveOuts(1);
            bool visitorBatting = _state.TopHalf;
            bool thirdOut = _state.Outs >= 3;
            bool finalOut = thirdOut && IsFinalOutAfterThirdOut(visitorBatting);
            _state.AdvanceBatter();
            RegisterCurrentPitcherBaserunnerAllowed(PitcherBaserunnerSource.FatigueEligible);
            CompletePlateAppearanceForCurrentReliever(batter);
            _state.ModeLabel = "Out at " + BaseLabel(play.TargetBase) + ": " + ContestedThrowLabel(fielder, targetFielder);
            LogPlay(_state.ModeLabel);
            PlayOutCall(thirdOut, visitorBatting, finalOut);
            if (finalOut)
            {
                CompleteGame();
                return;
            }

            if (thirdOut)
            {
                FinalizeCurrentPitcherInning();
                AdvanceToNextHalfInning();
            }
        }

        private void RecordLiveFielderChoiceStats(Player? batter, Player pitcher)
        {
            var batterLine = LiveLine(batter, _state.BattingTeam, pitcher: false);
            if (batterLine != null)
                batterLine.AB++;

            var pitcherLine = LiveLine(pitcher, _state.FieldingTeam, pitcher: true);
            if (pitcherLine != null)
                pitcherLine.BattersFaced++;
        }

        private void RecordLiveBaserunnerOutStats(Player pitcher)
        {
            var pitcherLine = LiveLine(pitcher, _state.FieldingTeam, pitcher: true);
            if (pitcherLine != null)
                pitcherLine.IPOuts++;
        }

        private void CompleteRecordedOut(string label)
        {
            bool visitorBatting = _state.TopHalf;
            Player? batter = _state.CurrentBatterPlayer();
            _state.ResetCount();
            _state.Outs++;
            CreditDefensiveOuts(1);
            bool thirdOut = _state.Outs >= 3;
            bool finalOut = thirdOut && IsFinalOutAfterThirdOut(visitorBatting);
            RegisterCurrentRelieverOut();
            _state.AdvanceBatter();
            CompletePlateAppearanceForCurrentReliever(batter);
            _state.ModeLabel = label;
            LogPlay(_state.ModeLabel);
            PlayOutCall(thirdOut, visitorBatting, finalOut);
            if (label?.IndexOf("Strikeout", StringComparison.OrdinalIgnoreCase) >= 0)
                TriggerCutscene(CutsceneTrigger.Strikeout, _state.FieldingTeam);
            if (finalOut)
            {
                CompleteGame();
                return;
            }

            if (thirdOut)
            {
                FinalizeCurrentPitcherInning();
                AdvanceToNextHalfInning();
            }
        }

        private void RecordDefensiveAssistAndPutout(Player? fielder, Player? targetFielder)
        {
            RegisterParticipation(fielder, _state.FieldingTeam, InjuryExposureType.FieldingPlay);
            if (targetFielder?.Id != fielder?.Id)
                RegisterParticipation(targetFielder, _state.FieldingTeam, InjuryExposureType.FieldingPlay);
            var fielderLine = LiveLine(fielder, _state.FieldingTeam, pitcher: false);
            if (fielderLine != null)
                fielderLine.Assists++;

            var putoutLine = LiveLine(targetFielder, _state.FieldingTeam, pitcher: false);
            if (putoutLine != null)
                putoutLine.Putouts++;
        }

        private static int BasesAwardedForHit(LiveHitType hitType)
        {
            return hitType switch
            {
                LiveHitType.Double => 2,
                LiveHitType.Triple => 3,
                LiveHitType.HomeRun => 4,
                _ => 1
            };
        }

        private void ApplyBaserunningForHit(
            LiveHitType hitType,
            GameplayRenderingPlayerMarker? fielder,
            Guid? excludedRunnerId,
            int batterBasesAwarded)
        {
            int batterBase = Math.Clamp(batterBasesAwarded, 1, 3);
            var ballLocation = BallLocationForHit(hitType, fielder);
            int ballDepth = BallDepthForHit(hitType, fielder);
            int fielderArm = PlayerRating(fielder?.Player, p => p.ArmStrength, 50);
            int scoreDifferential = BattingTeamScoreDifferential();
            var runners = _state.Bases
                .Select((baseState, index) => new
                {
                    BaseNumber = index + 1,
                    baseState.Occupied,
                    baseState.Player,
                    Snapshot = new BaseRunnerSnapshot(
                        baseState.Label,
                        baseState.RunnerColor,
                        baseState.Player,
                        baseState.Team,
                        baseState.CourtesyForPlayer,
                        baseState.ResponsiblePitcherId,
                        baseState.Earned)
                })
                .Where(runner => runner.Occupied && runner.Player != null)
                .Where(runner => runner.Player == null || !excludedRunnerId.HasValue || runner.Player.Id != excludedRunnerId.Value)
                .OrderByDescending(runner => runner.BaseNumber)
                .ToList();
            var originalOccupiedBases = runners.Select(runner => runner.BaseNumber).ToHashSet();

            _state.ClearBases();
            var occupiedTargets = new HashSet<int>();
            foreach (var runner in runners)
            {
                if (runner.Player == null)
                    continue;

                RegisterParticipation(runner.Player, _state.BattingTeam, InjuryExposureType.Baserunning);

                int minimumTarget = MinimumTargetBaseForHitRunner(runner.BaseNumber, batterBase, originalOccupiedBases);
                bool runnerAheadOccupied = occupiedTargets.Any(target => target > runner.BaseNumber && target <= 3);
                bool forced = minimumTarget > runner.BaseNumber;
                var decision = GameplayCpu.DecideBaserunning(
                    _rng,
                    runner.Player,
                    runner.BaseNumber,
                    _state.Outs,
                    ballLocation,
                    ballDepth,
                    fielderArm,
                    scoreDifferential,
                    forced,
                    runnerAheadOccupied);
                if (_offensiveStrategyCall == OffensiveStrategyCall.Safe && !forced)
                    decision = new GameplayCpu.BaserunningDecision(GameplayCpu.BaserunningAction.Hold, runner.BaseNumber, 0.95);

                int target = ResolveRunnerTargetBase(runner.BaseNumber, minimumTarget, decision);
                if (_offensiveStrategyCall == OffensiveStrategyCall.HitAndRun)
                    target = Math.Max(target, Math.Min(4, runner.BaseNumber + 1));
                if (target <= 3 && occupiedTargets.Contains(target) &&
                    runner.BaseNumber > batterBase &&
                    !occupiedTargets.Contains(runner.BaseNumber))
                {
                    target = runner.BaseNumber;
                }

                while (target <= 3 && occupiedTargets.Contains(target))
                    target++;

                if (target >= 4)
                {
                    _state.ScoreRunner(runner.Player, runner.Snapshot.ResponsiblePitcherId, runner.Snapshot.Earned);
                    var runnerLine = LiveLine(runner.Player, _state.BattingTeam, pitcher: false);
                    if (runnerLine != null)
                        runnerLine.R++;
                    continue;
                }

                RestoreBase(target, runner.Snapshot);
                occupiedTargets.Add(target);
            }

            if (batterBase <= 3)
            {
                Player? batter = _state.CurrentBatterPlayer();
                RegisterParticipation(batter, _state.BattingTeam, InjuryExposureType.Baserunning);
                while (batterBase <= 3 && occupiedTargets.Contains(batterBase))
                    batterBase++;

                if (batterBase <= 3)
                    _state.SetBaseRunner(batterBase, batter, _state.BattingTeam);
                else if (batter != null)
                    _state.ScoreRunner(batter, CurrentPitcher()?.Id ?? Guid.Empty, earned: true);
            }
        }

        private static int MinimumTargetBaseForHitRunner(int currentBase, int batterBase, HashSet<int> originalOccupiedBases)
        {
            if (currentBase <= batterBase)
                return Math.Min(4, batterBase + 1);
            bool forcedByTrailingRunners = Enumerable.Range(1, currentBase - 1).All(originalOccupiedBases.Contains);
            if (forcedByTrailingRunners)
                return Math.Min(4, currentBase + 1);
            return currentBase;
        }

        private static int ResolveRunnerTargetBase(
            int currentBase,
            int minimumTarget,
            GameplayCpu.BaserunningDecision decision)
        {
            int target = decision.Action switch
            {
                GameplayCpu.BaserunningAction.Hold => currentBase,
                GameplayCpu.BaserunningAction.Retreat => Math.Max(1, currentBase - 1),
                GameplayCpu.BaserunningAction.TagUp => decision.TargetBase,
                GameplayCpu.BaserunningAction.TakeExtraBase => decision.TargetBase,
                GameplayCpu.BaserunningAction.Advance => decision.TargetBase,
                _ => decision.TargetBase
            };

            return Math.Clamp(Math.Max(target, minimumTarget), 1, 4);
        }

        private static GameplayCpu.BallLocation BallLocationForHit(LiveHitType hitType, GameplayRenderingPlayerMarker? fielder)
        {
            if (hitType == LiveHitType.Triple)
                return GameplayCpu.BallLocation.Wall;
            if (hitType == LiveHitType.Double)
                return GameplayCpu.BallLocation.Gap;

            string label = fielder?.Label ?? "";
            if (string.Equals(label, "LF", StringComparison.OrdinalIgnoreCase))
                return GameplayCpu.BallLocation.OutfieldLeft;
            if (string.Equals(label, "CF", StringComparison.OrdinalIgnoreCase))
                return GameplayCpu.BallLocation.OutfieldCenter;
            if (string.Equals(label, "RF", StringComparison.OrdinalIgnoreCase))
                return GameplayCpu.BallLocation.OutfieldRight;
            if (string.Equals(label, "3B", StringComparison.OrdinalIgnoreCase) || string.Equals(label, "SS", StringComparison.OrdinalIgnoreCase))
                return GameplayCpu.BallLocation.InfieldLeft;
            if (string.Equals(label, "2B", StringComparison.OrdinalIgnoreCase) || string.Equals(label, "1B", StringComparison.OrdinalIgnoreCase))
                return GameplayCpu.BallLocation.InfieldRight;
            return GameplayCpu.BallLocation.InfieldMiddle;
        }

        private int BallDepthForHit(LiveHitType hitType, GameplayRenderingPlayerMarker? fielder)
        {
            if (hitType == LiveHitType.Triple)
                return 92;
            if (hitType == LiveHitType.Double)
                return 78;

            string label = fielder?.Label ?? "";
            bool outfield = string.Equals(label, "LF", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(label, "CF", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(label, "RF", StringComparison.OrdinalIgnoreCase);
            int depth = outfield ? 58 : 34;
            if (outfield && _defensiveAlignmentCall == DefensiveAlignmentCall.OutfieldIn)
                depth = Math.Max(38, depth - 16);
            else if (outfield && _defensiveAlignmentCall == DefensiveAlignmentCall.NoDoubles)
                depth = Math.Min(82, depth + 16);
            return depth;
        }

        private static string ContestedThrowLabel(GameplayRenderingPlayerMarker? fielder, Player? targetFielder)
        {
            string from = string.IsNullOrWhiteSpace(fielder?.Label) ? "Fielder" : fielder.Label;
            string to = targetFielder?.Name ?? "base";
            return from + " to " + to;
        }

        private void PlayHomeRunSound()
        {
            _homeRunSound.PlayOnce(LaunchSoundPlayer.FindRandomHomeRunCall(_rng));
        }

        private void PlayGrandSlamSound()
        {
            _homeRunSound.PlayOnce(LaunchSoundPlayer.FindGrandSlamCall());
        }

        private void TickBallInPlay()
        {
            _ballProgress = Math.Min(1f, _ballProgress + 0.028f);
            _state.BallPosition = Lerp(_ballStart, _ballTarget, EaseOut(_ballProgress));
            _state.BallTrail = Math.Max(0f, 1f - _ballProgress);

            if (_ballProgress < 1f)
                return;

            int adjustment = CurrentPitcherPerformanceAdjustmentPercent();
            SharedBattedBallResultType outcome = _pendingBattedBallResult ??
                SharedGameEngine.ResolveBattedBall(_rng, new SharedBattedBallRequest
                {
                    Batter = _state.CurrentBatterPlayer(),
                    Pitcher = CurrentPitcher(),
                    PitchType = _currentPitchType,
                    ContactQuality = 0.5,
                    PitcherAdjustmentPercent = adjustment,
                    BatterBoostPercent = _rankingModifier.BoostForTeam(_state.BattingTeam),
                    PitcherBoostPercent = _rankingModifier.BoostForTeam(_state.FieldingTeam),
                    DefenseFieldingRating = TeamFieldingRating(_state.FieldingTeam)
                });
            _pendingBattedBallResult = null;
            bool hit = outcome == SharedBattedBallResultType.Single ||
                outcome == SharedBattedBallResultType.Double ||
                outcome == SharedBattedBallResultType.Triple ||
                outcome == SharedBattedBallResultType.HomeRun;
            if (hit)
            {
                Player? batter = _state.CurrentBatterPlayer();
                Player pitcher = CurrentPitcher();
                LiveHitType hitType = outcome switch
                {
                    SharedBattedBallResultType.Double => LiveHitType.Double,
                    SharedBattedBallResultType.Triple => LiveHitType.Triple,
                    SharedBattedBallResultType.HomeRun => LiveHitType.HomeRun,
                    _ => LiveHitType.Single
                };
                bool homeRun = hitType == LiveHitType.HomeRun;
                bool grandSlam = homeRun && _state.OccupiedBaseCount() == 3;
                GameplayRenderingPlayerMarker fielder = PickBallTargetFielder();
                LiveContestedBasePlay contestedPlay = homeRun ? default : PickContestedBasePlay(hitType, fielder);
                if (!homeRun && contestedPlay.Runner != null && DefenseWinsContestedBasePlay(contestedPlay, fielder, hitType))
                {
                    ResolveContestedBaseOut(contestedPlay, fielder, batter, pitcher, hitType);
                    StopBallInPlayMusic();
                    _state.Phase = GameplayRenderingPhase.DeadBall;
                    _state.BallTrail = 0f;
                    ResetStrategyCalls();
                    PlayAfterPitchRunnerPromptIfNeeded();
                    return;
                }

                RegisterParticipation(fielder?.Player, _state.FieldingTeam, InjuryExposureType.FieldingPlay);

                int runsScored = CaptureRunsScoredByBattingTeam(() =>
                {
                    switch (hitType)
                    {
                        case LiveHitType.HomeRun:
                            foreach (var baseState in _state.Bases.Where(baseState => baseState.Occupied))
                                RegisterParticipation(baseState.Player, _state.BattingTeam, InjuryExposureType.Baserunning);
                            RegisterParticipation(batter, _state.BattingTeam, InjuryExposureType.Baserunning);
                            _state.AdvanceHomeRun();
                            break;
                        default:
                            ApplyBaserunningForHit(hitType, fielder, excludedRunnerId: null, batterBasesAwarded: BasesAwardedForHit(hitType));
                            break;
                    }
                });
                if (!homeRun)
                    _state.ApplyCourtesyRunners(PickCourtesyRunner);
                RecordLiveHitStats(batter, pitcher, hitType, runsScored);
                _state.AdvanceBatter();
                RegisterCurrentPitcherBaserunnerAllowed(PitcherBaserunnerSource.FatigueEligible);
                CompletePlateAppearanceForCurrentReliever(batter);
                _state.ModeLabel = LiveHitLabel(hitType, grandSlam);
                LogPlay(_state.ModeLabel);
                if (contestedPlay.Runner != null)
                    PlaySafeCallSound();
                if (homeRun)
                {
                    if (grandSlam)
                    {
                        PlayGrandSlamSound();
                        TriggerCutscene(CutsceneTrigger.GrandSlam, _state.BattingTeam);
                    }
                    else
                    {
                        PlayHomeRunSound();
                        TriggerCutscene(CutsceneTrigger.HomeRun, _state.BattingTeam);
                    }
                }
                RegisterCurrentPitcherRunsAllowed(runsScored);
            }
            else
            {
                var fielder = PickBallTargetFielder();
                if (!TryResolveFieldingError(fielder, outcome == SharedBattedBallResultType.Error))
                {
                    RegisterParticipation(fielder?.Player, _state.FieldingTeam, InjuryExposureType.FieldingPlay);
                    string recoveryNote = RegisterErrorFreeFieldingChance(fielder?.Player);
                    string outLabel = fielder == null ? "Out" : "Out: " + fielder.Label;
                    if (TryResolveLiveDoublePlay(fielder))
                    {
                        StopBallInPlayMusic();
                        _state.Phase = GameplayRenderingPhase.DeadBall;
                        _state.BallTrail = 0f;
                        ResetStrategyCalls();
                        PlayAfterPitchRunnerPromptIfNeeded();
                        return;
                    }
                    if (TryResolveSacrificeFlyOut(fielder, outLabel + recoveryNote))
                    {
                        StopBallInPlayMusic();
                        _state.Phase = GameplayRenderingPhase.DeadBall;
                        _state.BallTrail = 0f;
                        ResetStrategyCalls();
                        PlayAfterPitchRunnerPromptIfNeeded();
                        return;
                    }

                    RecordOut(outLabel + recoveryNote);
                }
            }

            StopBallInPlayMusic();
            _state.Phase = GameplayRenderingPhase.DeadBall;
            _state.BallTrail = 0f;
            ResetStrategyCalls();
            PlayAfterPitchRunnerPromptIfNeeded();
        }

        private bool TryResolveLiveDoublePlay(GameplayRenderingPlayerMarker? fielder)
        {
            if (_state.Outs >= 2 || !_state.Bases[0].Occupied || fielder?.Player == null)
                return false;

            string position = fielder.Label ?? "";
            bool infieldGrounder = position == "P" || position == "1B" || position == "2B" || position == "SS" || position == "3B";
            if (!infieldGrounder)
                return false;

            Player? batter = _state.CurrentBatterPlayer();
            Player runner = _state.Bases[0].Player;
            int defense = PositionFieldingRating(fielder.Player, position) +
                PlayerRating(fielder.Player, p => p.Accuracy, 50) / 2 +
                PlayerRating(fielder.Player, p => p.ArmStrength, 50) / 2;
            int offense = PlayerRating(runner, p => p.Speed, 50) +
                PlayerRating(runner, p => p.BaseRunning, 50) / 2 +
                PlayerRating(batter, p => p.Speed, 50) / 2;
            int chance = 18 + (defense - offense) / 5;
            if (_defensiveAlignmentCall == DefensiveAlignmentCall.DoublePlay)
                chance += 28;
            if (_offensiveStrategyCall == OffensiveStrategyCall.HitAndRun)
                chance -= 16;
            if (_rng.Next(100) >= Math.Clamp(chance, 6, 72))
                return false;

            Player pitcher = CurrentPitcher();
            ClearBaseSlot(1);
            PlayerGameLine batterLine = LiveLine(batter, _state.BattingTeam, pitcher: false);
            if (batterLine != null)
            {
                batterLine.AB++;
                batterLine.GroundOuts++;
                batterLine.GroundedIntoDoublePlays++;
            }

            PlayerGameLine pitcherLine = LiveLine(pitcher, _state.FieldingTeam, pitcher: true);
            if (pitcherLine != null)
            {
                pitcherLine.BattersFaced++;
                pitcherLine.IPOuts += 2;
            }

            Player pivot = position == "SS" ? FindDefensivePlayer("2B") : FindDefensivePlayer("SS");
            Player first = FindDefensivePlayer("1B");
            RegisterParticipation(runner, _state.BattingTeam, InjuryExposureType.Collision);
            RegisterParticipation(pivot, _state.FieldingTeam, InjuryExposureType.Collision);
            CreditDoublePlayFielder(fielder.Player, assist: position != "1B", putout: position == "1B", teamCredit: true);
            if (pivot?.Id != fielder.Player.Id)
                CreditDoublePlayFielder(pivot, assist: true, putout: true, teamCredit: false);
            if (first?.Id != fielder.Player.Id && first?.Id != pivot?.Id)
                CreditDoublePlayFielder(first, assist: false, putout: true, teamCredit: false);

            bool visitorBatting = _state.TopHalf;
            _state.ResetCount();
            _state.Outs += 2;
            CreditDefensiveOuts(2);
            RegisterCurrentRelieverOut();
            RegisterCurrentRelieverOut();
            _state.AdvanceBatter();
            CompletePlateAppearanceForCurrentReliever(batter);
            bool thirdOut = _state.Outs >= 3;
            bool finalOut = thirdOut && IsFinalOutAfterThirdOut(visitorBatting);
            _state.ModeLabel = "Grounded into double play: " + (batter?.Name ?? "Batter");
            LogPlay(_state.ModeLabel);
            PlayOutCall(thirdOut, visitorBatting, finalOut);
            if (finalOut)
            {
                CompleteGame();
                return true;
            }
            if (thirdOut)
            {
                FinalizeCurrentPitcherInning();
                AdvanceToNextHalfInning();
            }
            return true;
        }

        private void CreditDoublePlayFielder(Player? player, bool assist, bool putout, bool teamCredit)
        {
            if (player == null)
                return;
            RegisterParticipation(player, _state.FieldingTeam, InjuryExposureType.FieldingPlay);
            PlayerGameLine line = LiveLine(player, _state.FieldingTeam, pitcher: false);
            if (line == null)
                return;
            if (assist)
                line.Assists++;
            if (putout)
                line.Putouts++;
            line.DefensiveDoublePlays++;
            if (teamCredit)
                line.TeamDoublePlaysTurned++;
            FieldingDevelopmentEngine.RegisterCleanChance(player);
        }

        private bool TryResolveSacrificeFlyOut(GameplayRenderingPlayerMarker? fielder, string outLabel)
        {
            if (_state.Outs >= 2 || fielder == null || !IsFlyOutLabel(outLabel) || _state.Bases.Length < 3 || !_state.Bases[2].Occupied)
                return false;

            Player runner = _state.Bases[2].Player;
            Player? batter = _state.CurrentBatterPlayer();
            Player pitcher = CurrentPitcher();
            int runnerScore = PlayerRating(runner, p => p.Speed, 50) +
                PlayerRating(runner, p => p.BaseRunning, 50) +
                _rng.Next(-14, 15);
            int defenseScore = PlayerRating(fielder.Player, p => p.ArmStrength, 50) +
                PlayerRating(fielder.Player, p => p.Accuracy, 50) / 2 +
                PlayerRating(TagFielderForTarget(4), p => p.TagRating, 50) / 2 +
                _rng.Next(-14, 15);

            if (_defensiveAlignmentCall == DefensiveAlignmentCall.InfieldIn)
                defenseScore += 8;
            if (_defensiveAlignmentCall == DefensiveAlignmentCall.OutfieldIn)
                defenseScore += 18;
            if (_defensiveAlignmentCall == DefensiveAlignmentCall.NoDoubles)
                defenseScore -= 10;

            if (runnerScore <= defenseScore)
                return false;

            int runsScored = CaptureRunsScoredByBattingTeam(() =>
            {
                RegisterParticipation(runner, _state.BattingTeam, InjuryExposureType.Baserunning);
                _state.ScoreRunnerFromBase(3);
            });
            RecordLiveSacrificeFlyStats(batter, pitcher, runsScored);
            RegisterCurrentPitcherRunsAllowed(runsScored);
            var fielderLine = LiveLine(fielder.Player, _state.FieldingTeam, pitcher: false);
            if (fielderLine != null)
                fielderLine.Putouts++;
            CompleteRecordedOut("Sacrifice fly: " + fielder.Label);
            return true;
        }

        private bool TryResolveFieldingError(GameplayRenderingPlayerMarker? fielder, bool force = false)
        {
            if (fielder?.Player == null)
                return false;

            int effectiveFielding = EffectiveFieldingRating(fielder.Player, fielder.Label);
            int errorChance = Math.Clamp(18 - effectiveFielding / 7, 2, 18);
            if (!force && _rng.Next(100) >= errorChance)
                return false;

            RegisterParticipation(fielder.Player, _state.FieldingTeam, InjuryExposureType.FieldingPlay);
            Player batter = _state.CurrentBatterPlayer();
            Player pitcher = CurrentPitcher();
            foreach (var baseState in _state.Bases.Where(baseState => baseState.Occupied))
                RegisterParticipation(baseState.Player, _state.BattingTeam, InjuryExposureType.Baserunning);
            RegisterParticipation(batter, _state.BattingTeam, InjuryExposureType.Baserunning);
            int runsScored = CaptureRunsScoredByBattingTeam(() => _state.AdvanceRunners(true, _rng, batterEarned: false));
            _state.ApplyCourtesyRunners(PickCourtesyRunner);
            RecordLiveErrorStats(batter, pitcher, fielder);
            _state.AdvanceBatter();
            RegisterCurrentPitcherBaserunnerAllowed(PitcherBaserunnerSource.FieldingError);
            CompletePlateAppearanceForCurrentReliever(batter);
            int fieldingBefore = fielder.Player.Fielding;
            ApplyFieldingErrorPenalty(fielder.Player);
            _state.ModeLabel = "Error: " + fielder.Label + " Fielding " + fieldingBefore + "->" + fielder.Player.Fielding;
            LogPlay(_state.ModeLabel);
            PlayUghImpactSound();
            RegisterCurrentPitcherRunsAllowed(runsScored, PitcherRunCharge.UnearnedError);
            return true;
        }

        private static void ApplyFieldingErrorPenalty(Player player)
        {
            FieldingDevelopmentEngine.ApplyError(player);
        }

        private static string RegisterErrorFreeFieldingChance(Player? player)
        {
            if (player == null)
                return "";

            int before = player.Fielding;
            return FieldingDevelopmentEngine.RegisterCleanChance(player)
                ? " Fielding " + before + "->" + player.Fielding
                : "";
        }

        private GameplayRenderingPlayerMarker? PickBallTargetFielder()
        {
            if (_state.Fielders.Count == 0)
                return null;

            var best = _state.Fielders
                .Select((fielder, index) => new
                {
                    Fielder = fielder,
                    Index = index,
                    Score = DistanceSquared(fielder.Position, _ballTarget) + (float)(_rng.NextDouble() * 0.01)
                })
                .Where(item => item.Fielder.Player != null)
                .OrderBy(item => item.Score)
                .FirstOrDefault();

            if (best == null)
                return null;

            _state.ActiveFielderIndex = best.Index;
            return best.Fielder;
        }

        private int EffectiveFieldingRating(Player? player, string? assignedPosition)
        {
            if (player == null)
                return 50;

            int rating = PositionFieldingRating(player, assignedPosition);
            return RankingGameModifier.Apply(rating, _rankingModifier.BoostForTeam(TeamForPlayer(player)));
        }

        private static int PositionFieldingRating(Player? player, string? assignedPosition)
        {
            if (player == null)
                return 50;

            int rating = FieldingDevelopmentEngine.EffectiveRating(player);
            return LineupEngine.ApplyPositionFieldingPenalty(player, assignedPosition ?? "", rating);
        }

        private static string TargetPositionForBase(int baseNumber)
        {
            return baseNumber switch
            {
                2 => "2B",
                3 => "3B",
                4 => "C",
                _ => "1B"
            };
        }

        private static float DistanceSquared(PointF a, PointF b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            return dx * dx + dy * dy;
        }

        private void RecordOut(string label)
        {
            bool visitorBatting = _state.TopHalf;
            Player? batter = _state.CurrentBatterPlayer();
            Player pitcher = CurrentPitcher();
            _state.ResetCount();
            _state.Outs++;
            CreditDefensiveOuts(1);
            bool thirdOut = _state.Outs >= 3;
            bool finalOut = thirdOut && IsFinalOutAfterThirdOut(visitorBatting);
            RecordLiveOutStats(batter, pitcher, label);
            RegisterCurrentRelieverOut();
            _state.AdvanceBatter();
            CompletePlateAppearanceForCurrentReliever(batter);
            _state.ModeLabel = label;
            LogPlay(_state.ModeLabel);
            PlayOutCall(thirdOut, visitorBatting, finalOut);
            if (finalOut)
            {
                CompleteGame();
                return;
            }

            if (thirdOut)
            {
                FinalizeCurrentPitcherInning();
                AdvanceToNextHalfInning();
            }
        }

        private bool IsFinalOutAfterThirdOut(bool visitorBatting)
        {
            int regulation = Math.Max(1, _state.RegulationInnings);

            if (IsMercyRuleComplete(visitorBatting))
            {
                _endedByMercyRule = true;
                return true;
            }

            if (_state.Inning < regulation)
                return false;

            if (visitorBatting)
                return _state.HomeScore > _state.AwayScore;

            return _state.AwayScore != _state.HomeScore || !_state.AllowExtraInnings;
        }

        private bool IsMercyRuleComplete(bool completedTopHalf)
        {
            if (!_state.MercyRuleEnabled || _state.Inning < Math.Max(1, _state.MercyRuleMinimumInning))
                return false;

            int lead = Math.Abs(_state.HomeScore - _state.AwayScore);
            if (lead < Math.Max(1, _state.MercyRuleRuns))
                return false;

            return completedTopHalf
                ? _state.HomeScore > _state.AwayScore
                : _state.HomeScore != _state.AwayScore;
        }

        private Team WinningTeam()
        {
            if (_state == null)
                return null;
            if (_state.AwayScore > _state.HomeScore)
                return _state.AwayTeam;
            if (_state.HomeScore > _state.AwayScore)
                return _state.HomeTeam;
            return null;
        }

        private void AdvanceToNextHalfInning()
        {
            RecordCompletedHalfInning();
            _state.NextHalfInning(_rng, PickExtraInningRunner);
            ResetCurrentDefensivePitcherInningBaserunners();
            UpdateInningMusic();
            PlayChangeSideSoundIfNeeded();
        }

        private void RecordCompletedHalfInning()
        {
            if (_state == null)
                return;

            int inning = Math.Max(1, _state.Inning);
            HalfInning half = _state.TopHalf ? HalfInning.Top : HalfInning.Bottom;
            string key = HalfInningKey(inning, half);
            if (!_recordedHalfInnings.Add(key))
                return;

            RegisterParticipation(FindDefensivePlayer("C"), _state.FieldingTeam, InjuryExposureType.CatcherInning);

            int runsScored;
            if (_state.TopHalf)
            {
                EnsureInningSlots(_awayRunsByInning, inning);
                runsScored = Math.Max(0, _state.AwayScore - _awayRunsByInning.Sum());
                _awayRunsByInning[inning - 1] = runsScored;
                _playableAwayLeftOnBase += CountOccupiedBases();
            }
            else
            {
                EnsureInningSlots(_homeRunsByInning, inning);
                runsScored = Math.Max(0, _state.HomeScore - _homeRunsByInning.Sum());
                _homeRunsByInning[inning - 1] = runsScored;
                _playableHomeLeftOnBase += CountOccupiedBases();
            }

            _completedHalfInnings.Add(new HalfInningSnapshot
            {
                Inning = inning,
                Half = half,
                BattingTeamId = _state.BattingTeam?.Id ?? Guid.Empty,
                RunsScored = runsScored,
                AwayScore = _state.AwayScore,
                HomeScore = _state.HomeScore
            });

            LogPlay("End " + (_state.TopHalf ? "top " : "bottom ") + inning + ".");
        }

        private static void EnsureInningSlots(List<int> values, int inning)
        {
            while (values.Count < inning)
                values.Add(0);
        }

        private int CountOccupiedBases()
        {
            return _state?.Bases?.Count(b => b.Occupied) ?? 0;
        }

        private string CurrentBaseStateText()
        {
            if (_state?.Bases == null || _state.Bases.Length == 0)
                return "Bases empty";

            var occupied = new List<string>();
            if (_state.Bases.Length > 0 && _state.Bases[0].Occupied) occupied.Add("1B");
            if (_state.Bases.Length > 1 && _state.Bases[1].Occupied) occupied.Add("2B");
            if (_state.Bases.Length > 2 && _state.Bases[2].Occupied) occupied.Add("3B");
            return occupied.Count == 0 ? "Bases empty" : string.Join(", ", occupied);
        }

        private void LogPlay(string description)
        {
            if (_state == null || string.IsNullOrWhiteSpace(description))
                return;

            _playByPlay.Add(new GamePlayByPlayEntry
            {
                Sequence = _playByPlay.Count + 1,
                Inning = Math.Max(1, _state.Inning),
                Half = _state.TopHalf ? HalfInning.Top : HalfInning.Bottom,
                Outs = Math.Clamp(_state.Outs, 0, 3),
                AwayScore = _state.AwayScore,
                HomeScore = _state.HomeScore,
                Bases = CurrentBaseStateText(),
                Description = description.Trim()
            });
        }

        private static void PopulateGameResultTotals(GameResult result)
        {
            if (result == null)
                return;

            result.Lines ??= new List<PlayerGameLine>();
            result.AwayRunsByInning ??= new List<int>();
            result.HomeRunsByInning ??= new List<int>();
            result.PlayByPlay ??= new List<GamePlayByPlayEntry>();

            result.AwayHits = result.Lines.Where(line => line.TeamId == result.AwayTeamId).Sum(line => line.H);
            result.HomeHits = result.Lines.Where(line => line.TeamId == result.HomeTeamId).Sum(line => line.H);
            result.AwayErrors = result.Lines.Where(line => line.TeamId == result.AwayTeamId).Sum(line => line.Errors);
            result.HomeErrors = result.Lines.Where(line => line.TeamId == result.HomeTeamId).Sum(line => line.Errors);

            var winLine = result.Lines.FirstOrDefault(line => line.Wins > 0);
            if (winLine != null)
            {
                result.WinningPitcherId = winLine.PlayerId;
                result.WinningPitcherName = winLine.PlayerName ?? "";
            }

            var lossLine = result.Lines.FirstOrDefault(line => line.Losses > 0);
            if (lossLine != null)
            {
                result.LosingPitcherId = lossLine.PlayerId;
                result.LosingPitcherName = lossLine.PlayerName ?? "";
            }

            var saveLine = result.Lines.FirstOrDefault(line => line.Saves > 0);
            if (saveLine != null)
            {
                result.SavePitcherId = saveLine.PlayerId;
                result.SavePitcherName = saveLine.PlayerName ?? "";
            }

            if (result.GameLengthInnings <= 0)
                result.GameLengthInnings = Math.Max(result.AwayRunsByInning.Count, result.HomeRunsByInning.Count);
        }

        private void CompleteGame()
        {
            FinalizeCurrentPitcherInning();
            RecordCompletedHalfInning();
            AssignPlayablePitcherDecisions();
            _gameComplete = true;
            _state.Phase = GameplayRenderingPhase.DeadBall;
            _state.BallTrail = 0f;
            _state.ModeLabel = _endedByMercyRule ? "Final - Mercy Rule" : "Final";
            LogPlay(_endedByMercyRule ? "Final by mercy rule." : "Final.");
            var resultLines = _liveLines.Where(HasLiveStats).ToList();
            FinalResult = new GameResult
            {
                PlayedAt = DateTime.Now,
                AwayTeamId = _state.AwayTeam?.Id ?? Guid.Empty,
                HomeTeamId = _state.HomeTeam?.Id ?? Guid.Empty,
                AwayCoachId = _state.AwayTeam?.CoachId ?? Guid.Empty,
                HomeCoachId = _state.HomeTeam?.CoachId ?? Guid.Empty,
                GameMode = _mode.ToString(),
                StadiumId = _state.FieldPreset?.Id ?? "",
                StadiumName = _state.FieldPreset?.Name ?? "",
                AwayUniformSetId = _state.AwayUniformSetId,
                HomeUniformSetId = _state.HomeUniformSetId,
                AwayUniformName = _state.UniformForTeam(_state.AwayTeam)?.Name ?? "",
                HomeUniformName = _state.UniformForTeam(_state.HomeTeam)?.Name ?? "",
                RegulationInnings = _state.RegulationInnings,
                ExtraInningsEnabled = _state.AllowExtraInnings,
                ExtraInningRunnerOnSecond = _state.ExtraInningRunnerOnSecond,
                MercyRuleEnabled = _state.MercyRuleEnabled,
                MercyRuleRuns = _state.MercyRuleRuns,
                MercyRuleMinimumInning = _state.MercyRuleMinimumInning,
                EndedByMercyRule = _endedByMercyRule,
                GameLengthInnings = _state.Inning,
                GameLengthOuts = _state.Outs,
                AwayScore = _state.AwayScore,
                HomeScore = _state.HomeScore,
                AwayRunsByInning = _awayRunsByInning.ToList(),
                HomeRunsByInning = _homeRunsByInning.ToList(),
                AwayLeftOnBase = _playableAwayLeftOnBase,
                HomeLeftOnBase = _playableHomeLeftOnBase,
                Lines = resultLines,
                PlayByPlay = _playByPlay.ToList()
            };
            PopulateGameResultTotals(FinalResult);
            UpdateInningMusic();
            TriggerCutscene(CutsceneTrigger.FinalOut, WinningTeam());
        }

        private void AssignPlayablePitcherDecisions()
        {
            PitcherDecisionEngine.Apply(new PitcherDecisionRequest
            {
                AwayTeamId = _state.AwayTeam?.Id ?? Guid.Empty,
                HomeTeamId = _state.HomeTeam?.Id ?? Guid.Empty,
                AwayScore = _state.AwayScore,
                HomeScore = _state.HomeScore,
                RegulationInnings = Math.Clamp(_state.RegulationInnings, 5, 9),
                WinningPitcherCandidateId = _winningPitcherCandidateId,
                LosingPitcherCandidateId = _losingPitcherCandidateId,
                Appearances = BuildPlayableDecisionAppearances(_state.AwayTeam)
                    .Concat(BuildPlayableDecisionAppearances(_state.HomeTeam))
                    .ToList()
            });
        }

        private List<PitcherDecisionAppearance> BuildPlayableDecisionAppearances(Team? team)
        {
            if (team == null)
                return new List<PitcherDecisionAppearance>();

            var lines = _liveLines
                .Where(line => line.TeamId == team.Id && line.Pitcher)
                .ToList();
            Guid finishingPitcherId = PitcherForTeam(team)?.Id ?? Guid.Empty;
            var orderedIds = new List<Guid>();
            PlayerGameLine? starter = lines.FirstOrDefault(line => line.StartingPitcher);
            if (starter != null)
                orderedIds.Add(starter.PlayerId);
            orderedIds.AddRange(_reliefPitcherFatigue.Keys.Where(id =>
                lines.Any(line => line.PlayerId == id) && !orderedIds.Contains(id)));
            orderedIds.AddRange(lines.Select(line => line.PlayerId).Where(id => !orderedIds.Contains(id)));

            return orderedIds.Select((playerId, index) =>
            {
                PlayerGameLine line = lines.First(item => item.PlayerId == playerId);
                _reliefPitcherFatigue.TryGetValue(playerId, out ReliefPitcherFatigueState? state);
                return new PitcherDecisionAppearance
                {
                    TeamId = team.Id,
                    PlayerId = playerId,
                    PlayerName = line.PlayerName ?? "",
                    Starter = line.StartingPitcher,
                    FinishedGame = playerId == finishingPitcherId,
                    AppearanceOrder = index,
                    EnteredInSaveSituation = state?.EnteredInSaveSituation == true,
                    EnteredWithThreeRunLead = state?.EnteredWithThreeRunLead == true,
                    EnteredWithTyingRunThreat = state?.EnteredWithTyingRunThreat == true,
                    LeadPreserved = state?.LeadPreserved ?? true,
                    Line = line
                };
            }).ToList();
        }

        private void PlayOutCall(bool thirdOut, bool visitorBatting, bool finalOut)
        {
            string outPath = thirdOut
                ? LaunchSoundPlayer.FindYoureOutCall()
                : LaunchSoundPlayer.FindOutCall();
            _outCallSound.PlayOnce(outPath);

            if (visitorBatting)
                ScheduleVisitorOutCrowdCheer(outPath);
            if (finalOut)
                ScheduleGameOverSound(outPath, visitorBatting);
        }

        private void ScheduleVisitorOutCrowdCheer(string outPath)
        {
            _visitorOutCheerTimer.Stop();
            int delay = LaunchSoundPlayer.GetDurationMilliseconds(outPath, 700) + 80;
            _visitorOutCheerTimer.Interval = Math.Clamp(delay, 120, 5000);
            _visitorOutCheerTimer.Start();
        }

        private void ScheduleGameOverSound(string outPath, bool visitorBatting)
        {
            _gameOverTimer.Stop();
            int delay = LaunchSoundPlayer.GetDurationMilliseconds(outPath, 700) + 90;
            if (visitorBatting)
            {
                string cheer = LaunchSoundPlayer.FindVisitorOutCrowdCheer();
                delay += LaunchSoundPlayer.GetDurationMilliseconds(cheer, 1200) + 90;
            }

            _gameOverTimer.Interval = Math.Clamp(delay, 180, 10000);
            _gameOverTimer.Start();
        }

        private void PlayChangeSideSoundIfNeeded()
        {
            int stretchInning = _state.RegulationInnings < 7
                ? Math.Max(1, _state.RegulationInnings)
                : 7;

            if (_state.Inning == stretchInning && !_state.TopHalf)
            {
                _changeSideSound.PlayOnce(LaunchSoundPlayer.FindRandomSeventhInningStretch(_rng));
                TriggerCutscene(CutsceneTrigger.SeventhInningStretch, _state.HomeTeam);
                return;
            }

            _changeSideSound.PlayOnce(LaunchSoundPlayer.FindChangeSide());
        }

        private void PlayAfterPitchRunnerPromptIfNeeded()
        {
            if (_state.Outs >= 3)
                return;

            if (_state.Bases.Length >= 3 && _state.Bases[2].Occupied)
            {
                string path = LaunchSoundPlayer.FindRunnerOnThirdPrompt();
                if (!string.IsNullOrWhiteSpace(path))
                {
                    _runnerOnThirdSound.PlayOnce(path);
                    return;
                }
            }

            PlayHomeRunnersPromptIfNeeded();
            PlayVisitorRunnersPromptIfNeeded();
        }

        private void PlayHomeRunnersPromptIfNeeded()
        {
            if (_state.TopHalf || _state.Outs >= 3)
                return;
            if (!_state.Bases.Any(b => b.Occupied))
                return;

            _homeRunnersSound.PlayOnce(LaunchSoundPlayer.FindRandomHomeRunnersPrompt(_rng));
        }

        private void PlayVisitorRunnersPromptIfNeeded()
        {
            if (!_state.TopHalf || _state.Outs >= 3)
                return;
            if (!_state.Bases.Any(b => b.Occupied))
                return;

            _visitorRunnersSound.PlayOnce(LaunchSoundPlayer.FindVisitorRunnersPrompt());
        }

        private Player PickExtraInningRunner(Team team, IReadOnlyList<Player> candidates, IReadOnlyDictionary<Guid, int> pinchUses)
        {
            if (candidates == null || candidates.Count == 0)
                return null;

            StopGameLoop();
            try
            {
                using var dlg = new ExtraInningRunnerPickerDialog(team, candidates, pinchUses);
                return dlg.ShowDialog(this) == DialogResult.OK
                    ? dlg.SelectedPlayer
                    : candidates.FirstOrDefault();
            }
            finally
            {
                StartGameLoop();
            }
        }

        private Player PickCourtesyRunner(Team team, Player protectedPlayer, IReadOnlyList<Player> candidates, IReadOnlyDictionary<Guid, int> pinchUses)
        {
            if (candidates == null || candidates.Count == 0)
                return null;

            StopGameLoop();
            try
            {
                string name = protectedPlayer?.Name ?? "the catcher/pitcher";
                using var dlg = new ExtraInningRunnerPickerDialog(
                    team,
                    candidates,
                    pinchUses,
                    "Choose Courtesy Runner",
                    "Courtesy runner for " + name + ": next 8 scheduled batters are ineligible.");
                return dlg.ShowDialog(this) == DialogResult.OK
                    ? dlg.SelectedPlayer
                    : candidates.FirstOrDefault();
            }
            finally
            {
                StartGameLoop();
            }
        }

        private void MoveActiveFielder(float dx, float dy)
        {
            if (_state.Fielders.Count == 0)
                return;

            int index = Math.Clamp(_state.ActiveFielderIndex, 0, _state.Fielders.Count - 1);
            GameplayRenderingPlayerMarker marker = _state.Fielders[index];
            marker.Position = new PointF(Clamp01(marker.Position.X + dx), Clamp01(marker.Position.Y + dy));
            _surface.Invalidate();
        }

        private void ApplyBaseRunner(int baseNumber, BaseRunner? runner)
        {
            if (baseNumber < 1 || baseNumber > _state.Bases.Length)
                return;

            if (runner == null)
            {
                var baseState = _state.Bases[baseNumber - 1];
                baseState.Label = "";
                baseState.Occupied = false;
                baseState.Player = null;
                baseState.Team = null;
                baseState.CourtesyForPlayer = null;
                baseState.ResponsiblePitcherId = Guid.Empty;
                baseState.Earned = true;
                return;
            }

            _state.SetBaseRunner(baseNumber, runner.Player, runner.Team);
            _state.Bases[baseNumber - 1].CourtesyForPlayer = runner.CourtesyForPlayer;
            _state.Bases[baseNumber - 1].ResponsiblePitcherId = runner.ResponsiblePitcherId;
            _state.Bases[baseNumber - 1].Earned = runner.Earned;
        }

        private static bool IsGameplayStateInProgress(GameplayState state)
        {
            if (state == null)
                return false;
            return state.Inning > 1 ||
                state.AwayScore > 0 ||
                state.HomeScore > 0 ||
                (state.Count?.Balls ?? 0) > 0 ||
                (state.Count?.Strikes ?? 0) > 0 ||
                (state.Count?.Outs ?? 0) > 0 ||
                state.Bases?.First != null ||
                state.Bases?.Second != null ||
                state.Bases?.Third != null ||
                (state.LiveLines?.Count ?? 0) > 0;
        }

        private static void CopyLineupIds(IEnumerable<Guid>? source, List<Guid>? target)
        {
            if (target == null)
                return;

            target.Clear();
            if (source == null)
                return;

            target.AddRange(source.Where(id => id != Guid.Empty));
        }

        private static PointF Lerp(PointF a, PointF b, float t)
            => new PointF(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);

        private static float EaseOut(float value)
            => 1f - (1f - value) * (1f - value);

        private static float Clamp01(float value)
            => value < 0f ? 0f : value > 1f ? 1f : value;

        private static int PositiveModulo(int value, int modulus)
            => modulus <= 0 ? 0 : ((value % modulus) + modulus) % modulus;

        private static T ReadProperty<T>(object source, string name, T fallback)
        {
            PropertyInfo? property = source.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property == null)
                return fallback;

            object? value = property.GetValue(source);
            if (value is T typed)
                return typed;

            try
            {
                if (value != null)
                    return (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return fallback;
            }

            return fallback;
        }
    }
}
