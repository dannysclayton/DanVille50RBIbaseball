#nullable enable annotations

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;

namespace StandaloneBaseball
{
    public sealed partial class MainForm : Form
    {
        private static readonly JsonSerializerOptions TeamFileJsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        private sealed class TeamItem
        {
            public Team Team { get; set; }
            public string Text { get; set; }
            public override string ToString() => Text ?? Team?.DisplayName ?? "";
        }

        private sealed class SeasonItem
        {
            public Season Season { get; set; }
            public string Text { get; set; }
            public override string ToString() => Text ?? (Season == null ? "" : Season.Year + " - " + Season.Name);
        }

        private sealed class ScheduledGameItem
        {
            public ScheduledGame Game { get; set; }
            public string Text { get; set; }
            public override string ToString() => Text ?? "";
        }

        private sealed class RankingPollItem
        {
            public SeasonRankingPoll Poll { get; set; }
            public string Text { get; set; }
            public override string ToString() => Text ?? "";
        }

        private sealed class RecordsBookEntityItem
        {
            public Guid? Id { get; set; }
            public string Text { get; set; }
            public override string ToString() => Text ?? "";
        }

        private sealed class ControlTeamItem
        {
            public Guid? TeamId { get; set; }
            public bool Auto { get; set; }
            public bool WatchOnly { get; set; }
            public string Text { get; set; }
            public override string ToString() => Text ?? "";
        }

        private sealed class PvpInputAssignmentItem
        {
            public bool AwayUsesKeyboard { get; set; }
            public string Text { get; set; }
            public override string ToString() => Text ?? "";
        }

        private sealed class UniformChoiceItem
        {
            public bool Auto { get; set; }
            public TeamUniformCategory? AutoCategory { get; set; }
            public Guid? UniformId { get; set; }
            public TeamUniformSet Uniform { get; set; }
            public string Text { get; set; }
            public override string ToString() => Text ?? Uniform?.Name ?? "";
        }

        private sealed class TeamMutationSnapshot
        {
            private readonly Dictionary<Guid, string> _teams = new Dictionary<Guid, string>();

            public static TeamMutationSnapshot Capture(LeagueFile league, params Team[] teams)
            {
                var snapshot = new TeamMutationSnapshot();
                var leagueTeamIds = (league?.Teams ?? new List<Team>()).Select(t => t.Id).ToHashSet();
                foreach (var team in teams?.Where(t => t != null).GroupBy(t => t.Id).Select(g => g.First()) ?? Enumerable.Empty<Team>())
                {
                    if (leagueTeamIds.Contains(team.Id))
                        snapshot._teams[team.Id] = JsonSerializer.Serialize(team, TeamFileJsonOptions);
                }
                return snapshot;
            }

            public void Restore(LeagueFile league)
            {
                if (league?.Teams == null)
                    return;

                foreach (var pair in _teams)
                {
                    int index = league.Teams.FindIndex(t => t.Id == pair.Key);
                    if (index < 0)
                        continue;
                    var restored = JsonSerializer.Deserialize<Team>(pair.Value, TeamFileJsonOptions);
                    if (restored != null)
                        league.Teams[index] = restored;
                }
            }
        }

        private sealed class TeamPlacement
        {
            public Conference Conference { get; set; }
            public Region Region { get; set; }
            public District District { get; set; }
        }

        private sealed class TeamSeasonStatLine
        {
            public Guid TeamId { get; set; }
            public string TeamName { get; set; }
            public string SeasonName { get; set; }
            public int SeasonNumber { get; set; }
            public bool Champion { get; set; }
            public int Games => Wins + Losses + Ties;
            public int Wins { get; set; }
            public int Losses { get; set; }
            public int Ties { get; set; }
            public int RunsFor { get; set; }
            public int RunsAgainst { get; set; }
            public int AB { get; set; }
            public int R { get; set; }
            public int H { get; set; }
            public int Doubles { get; set; }
            public int Triples { get; set; }
            public int HR { get; set; }
            public int RBI { get; set; }
            public int BB { get; set; }
            public int IBB { get; set; }
            public int SO { get; set; }
            public int SB { get; set; }
            public int CS { get; set; }
            public int HBP { get; set; }
            public int SH { get; set; }
            public int SF { get; set; }
            public int FlyOuts { get; set; }
            public int GroundOuts { get; set; }
            public int PopOuts { get; set; }
            public int GroundedIntoDoublePlays { get; set; }
            public int ReachedOnError { get; set; }
            public int IPOuts { get; set; }
            public int ER { get; set; }
            public int RunsAllowed { get; set; }
            public int PitchingK { get; set; }
            public int PitchingBB { get; set; }
            public int PitchingIBB { get; set; }
            public int HitsAllowed { get; set; }
            public int DoublesAllowed { get; set; }
            public int TriplesAllowed { get; set; }
            public int HomeRunsAllowed { get; set; }
            public int HitBatters { get; set; }
            public int WildPitches { get; set; }
            public int Balks { get; set; }
            public int Holds { get; set; }
            public int BlownSaves { get; set; }
            public int CompleteGames { get; set; }
            public int Shutouts { get; set; }
            public int Putouts { get; set; }
            public int Assists { get; set; }
            public int Errors { get; set; }
            public int DefensiveOuts { get; set; }
            public int DoublePlaysTurned { get; set; }
            public int PassedBalls { get; set; }
            public int StolenBasesAllowed { get; set; }
            public int CatcherCaughtStealing { get; set; }
            public int InjuryGamesMissed { get; set; }
            public int TotalBases => H + Doubles + Triples * 2 + HR * 3;
            public int PlateAppearances => AB + BB + HBP + SH + SF;
            public int ExtraBaseHits => Doubles + Triples + HR;
            public int TotalChances => Putouts + Assists + Errors;
            public int CatcherStealAttempts => StolenBasesAllowed + CatcherCaughtStealing;
            public double CatcherCaughtStealingPercentage => CatcherStealAttempts <= 0 ? 0.0 : CatcherCaughtStealing / (double)CatcherStealAttempts;
            public int RunDiff => RunsFor - RunsAgainst;
        }

        private sealed class PlayerSeasonStatLine
        {
            public Guid PlayerId { get; set; }
            public string PlayerName { get; set; }
            public bool Pitcher { get; set; }
            public PlayerClassification Classification { get; set; }
            public string Positions { get; set; }
            public string Injury { get; set; }
            public bool MedicalTag { get; set; }
            public bool MedicalEligible { get; set; }
            public bool Redshirt { get; set; }
            public int VarsitySeasons { get; set; }
            public int CallUpSeason { get; set; }
            public int Games { get; set; }
            public int R { get; set; }
            public int AB { get; set; }
            public int H { get; set; }
            public int Doubles { get; set; }
            public int Triples { get; set; }
            public int HR { get; set; }
            public int RBI { get; set; }
            public int BB { get; set; }
            public int IBB { get; set; }
            public int SO { get; set; }
            public int SB { get; set; }
            public int CS { get; set; }
            public int HBP { get; set; }
            public int SH { get; set; }
            public int SF { get; set; }
            public int FlyOuts { get; set; }
            public int GroundOuts { get; set; }
            public int PopOuts { get; set; }
            public int GroundedIntoDoublePlays { get; set; }
            public int ReachedOnError { get; set; }
            public int IPOuts { get; set; }
            public int ER { get; set; }
            public int RunsAllowed { get; set; }
            public int K { get; set; }
            public int HitsAllowed { get; set; }
            public int DoublesAllowed { get; set; }
            public int TriplesAllowed { get; set; }
            public int WalksAllowed { get; set; }
            public int IntentionalWalksAllowed { get; set; }
            public int HomeRunsAllowed { get; set; }
            public int HitBatters { get; set; }
            public int WildPitches { get; set; }
            public int Balks { get; set; }
            public int BattersFaced { get; set; }
            public int PitchCount { get; set; }
            public int PitchingWins { get; set; }
            public int PitchingLosses { get; set; }
            public int Saves { get; set; }
            public int Holds { get; set; }
            public int BlownSaves { get; set; }
            public int CompleteGames { get; set; }
            public int Shutouts { get; set; }
            public int Putouts { get; set; }
            public int Assists { get; set; }
            public int Errors { get; set; }
            public int DefensiveOuts { get; set; }
            public int DefensiveDoublePlays { get; set; }
            public int PassedBalls { get; set; }
            public int StolenBasesAllowed { get; set; }
            public int CatcherCaughtStealing { get; set; }
            public int GamesMissedInjury { get; set; }
            public int TotalBases => H + Doubles + Triples * 2 + HR * 3;
            public int PlateAppearances => AB + BB + HBP + SH + SF;
            public int ExtraBaseHits => Doubles + Triples + HR;
            public int TotalChances => Putouts + Assists + Errors;
            public int CatcherStealAttempts => StolenBasesAllowed + CatcherCaughtStealing;
            public double CatcherCaughtStealingPercentage => CatcherStealAttempts <= 0 ? 0.0 : CatcherCaughtStealing / (double)CatcherStealAttempts;
        }

        private sealed class HallOfFameCandidate
        {
            public Guid PlayerId { get; set; }
            public Guid TeamId { get; set; }
            public string PlayerName { get; set; }
            public string TeamName { get; set; }
            public PlayerRole Role { get; set; }
            public PlayerClassification Classification { get; set; }
            public PlayerClassification InitialClassification { get; set; }
            public int HallScore { get; set; }
            public string Recommendation { get; set; }
            public string Reason { get; set; }
            public PlayerSeasonStatLine Stats { get; set; }
            public PlayerSeasonStatLine PlayoffStats { get; set; }
            public int Championships { get; set; }
            public int LeaderBonus { get; set; }
            public string LeaderBonusReason { get; set; } = "";
            public int PlayoffBonus { get; set; }
            public string PlayoffBonusReason { get; set; } = "";
            public HashSet<Guid> StatSeasonIds { get; } = new HashSet<Guid>();
            public string ExtrapolationReason { get; set; } = "";
        }

        private sealed class CoachRecord
        {
            public Guid CoachId { get; set; }
            public Guid TeamId { get; set; }
            public string CoachName { get; set; } = "";
            public string TeamName { get; set; } = "";
            public int Wins { get; set; }
            public int Losses { get; set; }
            public int Ties { get; set; }
            public int PlayoffWins { get; set; }
            public int PlayoffLosses { get; set; }
            public int ChampionshipWins { get; set; }
            public int DistrictWins { get; set; }
            public int RegionWins { get; set; }
            public int ConferenceWins { get; set; }
            public int HallScore { get; set; }
            public string Recommendation { get; set; } = "";
            public string Reason { get; set; } = "";
        }

        private sealed class AllStarCandidate
        {
            public Guid PlayerId { get; set; }
            public Guid TeamId { get; set; }
            public string PlayerName { get; set; }
            public string TeamName { get; set; }
            public PlayerRole Role { get; set; }
            public string Positions { get; set; }
            public string AllStarTeam { get; set; }
            public int Score { get; set; }
            public PlayerSeasonStatLine Stats { get; set; }
        }

        private sealed class AllStarStatRow
        {
            public string AllStarTeam { get; set; } = "";
            public string PlayerName { get; set; } = "";
            public string OriginalTeam { get; set; } = "";
            public string Role { get; set; } = "";
            public PlayerGameLine Line { get; set; } = new PlayerGameLine();
        }

        private sealed class TeamHallSeasonSummary
        {
            public int SeasonNumber { get; set; }
            public string SeasonName { get; set; } = "";
            public int Wins { get; set; }
            public int Losses { get; set; }
            public int Ties { get; set; }
            public string Finish { get; set; } = "";
        }

        private sealed class AwardCandidate
        {
            public string AwardName { get; set; } = "";
            public string Category { get; set; } = "";
            public string Position { get; set; } = "";
            public Guid PlayerId { get; set; }
            public Guid TeamId { get; set; }
            public string PlayerName { get; set; } = "";
            public string TeamName { get; set; } = "";
            public PlayerRole Role { get; set; }
            public double Score { get; set; }
            public int Rank { get; set; }
            public bool Winner => Rank == 1;
            public string KeyStats { get; set; } = "";
            public PlayerSeasonStatLine Stats { get; set; }
        }

        private enum StatsScope
        {
            Season,
            Playoffs,
            Career,
            AllTime
        }

        private enum AllStarStatsScope
        {
            SelectedGame,
            PlayerCareer,
            TeamSideHistory,
            AllTimeLeaders
        }

        private enum ExportFormat
        {
            Excel,
            Word
        }

        private readonly Random _rng = new Random();
        private LeagueFile _league;
        private string? _path;
        private bool _dirty;
        private bool _suppress;
        private bool _refreshingUniformCombos;

        private ListBox _teamList;
        private TextBox _cityBox, _nicknameBox, _abbrBox, _coachBox;
        private Panel _primaryPanel, _secondaryPanel, _fieldPanel;
        private FlowLayoutPanel _teamBadgesPanel;
        private PictureBox _playerAvatarBox;
        private Label _playerAvatarLabel;
        private Image _playerAvatarImage;
        private Panel _teamHallPagePanel;
        private PictureBox _teamHallPicture;
        private Image? _teamHallImage;
        private DataGridView _rosterGrid, _gamesGrid, _playoffGrid, _rankingGrid, _championshipGrid, _teamStatsGrid, _playerStatsGrid, _recordsBookGrid, _hofDynastyGrid, _hofTeamGrid, _hofCandidatesGrid, _hofCoachCandidatesGrid, _hofRecordsGrid, _allStarCandidatesGrid, _allStarSelectionsGrid, _allStarGameStatsGrid, _awardRacesGrid, _positionAwardsGrid, _goldGloveGrid, _silverBatGrid, _awardFinalistsGrid, _awardHistoryGrid, _inboxGrid;
        private TextBox _inboxBodyBox;
        private TreeView _structureTree;
        private TabControl _allStarTabs, _awardTabs, _hofTabs;
        private ComboBox _awayCombo, _homeCombo, _seasonCombo, _commitSeasonCombo, _rankingSeasonCombo, _rankingPollCombo, _allStarSeasonCombo, _allStarStatsScopeCombo, _awardSeasonCombo, _teamStatsTeamCombo, _teamStatsSeasonCombo, _teamStatsScopeCombo, _playerStatsTeamCombo, _playerStatsSeasonCombo, _playerStatsScopeCombo, _recordsBookLevelCombo, _recordsBookEntityCombo, _recordsBookScopeCombo, _hofTeamCombo, _inningsCombo, _scheduledGameCombo, _fieldPresetCombo, _controlTeamCombo, _pvpInputCombo, _awayUniformCombo, _homeUniformCombo, _inboxFilterCombo;
        private ToolStripStatusLabel _status;
        private Label _simResult, _championLabel, _rankingSummaryLabel, _allStarSummaryLabel, _awardSummaryLabel, _teamLeadersLabel, _playerLeadersLabel, _hofSummaryLabel, _inboxSummaryLabel;
        private CheckBox _mercyRuleBox, _extraInningsBox, _extraRunnerBox;
        private TabControl _tabs;
        private ToolTip _tips;
        private GameResult? _lastGame;
        private readonly MenuAction? _startupAction;
        private readonly LaunchSoundPlayer _worldSeriesChampionsSound = new LaunchSoundPlayer();
        private readonly PlaylistSoundPlayer _teamContextMusic = new PlaylistSoundPlayer();
        private string _currentTeamContextMusicPath = "";
        private System.Windows.Forms.Timer _scoreboardPhotoTimer;
        private readonly List<string> _scoreboardPhotoPaths = new List<string>();
        private readonly List<Image> _dynastyLogoImages = new List<Image>();
        private readonly List<Image> _teamBadgeImages = new List<Image>();
        private int _scoreboardPhotoIndex;

        public MainForm(MenuAction? startupAction = null)
        {
            _startupAction = startupAction;
            Text = "Dan's RBI Baseball 2026";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1120, 720);
            MinimumSize = new Size(960, 620);
            _tips = new ToolTip();

            _league = CreateStarterLeague();
            AssetPathResolver.ClearLeagueFilePath();
            BuildUi();
            _scoreboardPhotoTimer = new System.Windows.Forms.Timer { Interval = 2500 };
            _scoreboardPhotoTimer.Tick += (s, e) =>
            {
                if (_scoreboardPhotoPaths.Count > 1)
                {
                    _scoreboardPhotoIndex = (_scoreboardPhotoIndex + 1) % _scoreboardPhotoPaths.Count;
                    _fieldPanel?.Invalidate();
                }
            };
            RefreshAll();
            if (_startupAction.HasValue)
                BeginInvoke(new Action(() => ApplyMenuAction(_startupAction.Value)));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ClearDynastyLogoImages();
                ClearTeamBadgeImages();
                ClearTeamHallImage();
                SetPlayerAvatarImage(null);
                _scoreboardPhotoTimer?.Dispose();
                _worldSeriesChampionsSound.Dispose();
                _teamContextMusic.Dispose();
                _tips?.Dispose();
            }

            base.Dispose(disposing);
        }

        private void BuildUi()
        {
            var menu = new MenuStrip();
            var file = new ToolStripMenuItem("&File");
            file.DropDownItems.Add("New League", null, (s, e) => NewLeague());
            file.DropDownItems.Add("Open...", null, (s, e) => OpenLeague());
            file.DropDownItems.Add("Restore Backup...", null, (s, e) => RestoreLeagueBackup());
            file.DropDownItems.Add("Save", null, (s, e) => SaveLeague(false));
            file.DropDownItems.Add("Save As...", null, (s, e) => SaveLeague(true));
            file.DropDownItems.Add(new ToolStripSeparator());
            file.DropDownItems.Add("Set Asset Library...", null, (s, e) => SetAssetLibrary());
            file.DropDownItems.Add("Open Asset Library", null, (s, e) => OpenAssetLibrary());
            file.DropDownItems.Add(new ToolStripSeparator());
            file.DropDownItems.Add("Import ROM Snapshot...", null, (s, e) => ImportRomSnapshot());
            file.DropDownItems.Add(new ToolStripSeparator());
            file.DropDownItems.Add("Exit", null, (s, e) => Close());
            var teamsMenu = new ToolStripMenuItem("&Teams");
            teamsMenu.DropDownItems.Add("Create Team From Schools CSV...", null, (s, e) => AddSchoolTeamFromCsv());
            teamsMenu.DropDownItems.Add("Update Schools CSV...", null, (s, e) => UpdateSchoolsCsv());
            teamsMenu.DropDownItems.Add("Set User Controlled Teams...", null, (s, e) => SetUserControlledTeams());
            menu.Items.Add(file);
            menu.Items.Add(teamsMenu);
            Controls.Add(menu);
            MainMenuStrip = menu;

            var tabs = new TabControl { Dock = DockStyle.Fill };
            _tabs = tabs;
            var teams = new TabPage("Teams");
            var structure = new TabPage("Structure");
            var sim = new TabPage("Game");
            var seasons = new TabPage("Seasons");
            var rankings = new TabPage("Rankings");
            var allStars = new TabPage("All-Stars");
            var awards = new TabPage("Awards");
            var dynasty = new TabPage("Dynasty");
            var hallOfFame = new TabPage("Hall Of Fame");
            var statistics = new TabPage("Statistics");
            var teamStats = new TabPage("Team Stats");
            var playerStats = new TabPage("Player Stats");
            var recordsBook = new TabPage("Records Book");
            var inbox = new TabPage("Inbox");
            BuildTeamsTab(teams);
            BuildStructureTab(structure);
            BuildSimTab(sim);
            BuildSeasonsTab(seasons);
            BuildRankingsTab(rankings);
            BuildAllStarsTab(allStars);
            BuildAwardsTab(awards);
            BuildDynastyTab(dynasty);
            BuildHallOfFameTab(hallOfFame);
            BuildHierarchyStatisticsTab(statistics);
            BuildTeamStatsTab(teamStats);
            BuildPlayerStatsTab(playerStats);
            BuildRecordsBookTab(recordsBook);
            BuildInboxTab(inbox);
            tabs.TabPages.AddRange(new[] { teams, structure, sim, seasons, rankings, allStars, awards, dynasty, hallOfFame, statistics, teamStats, playerStats, recordsBook, inbox });
            Controls.Add(tabs);

            var strip = new StatusStrip();
            _status = new ToolStripStatusLabel("Ready.");
            strip.Items.Add(_status);
            Controls.Add(strip);

            MainMenuStrip.BringToFront();
            FormClosing += (s, e) => { if (!ConfirmDiscard()) e.Cancel = true; };
        }

        private void BuildTeamsTab(TabPage tab)
        {
            var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 260, FixedPanel = FixedPanel.Panel1 };
            tab.Controls.Add(split);

            var left = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Padding = new Padding(8) };
            left.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            left.RowStyles.Add(new RowStyle(SizeType.Absolute, 132));
            _teamList = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };
            _teamList.SelectedIndexChanged += (s, e) => LoadSelectedTeam();
            left.Controls.Add(_teamList, 0, 0);
            var teamButtons = new FlowLayoutPanel { Dock = DockStyle.Fill };
            AddButton(teamButtons, "Add", (s, e) => AddTeam());
            AddButton(teamButtons, "Add School...", (s, e) => AddSchoolTeamFromCsv());
            AddButton(teamButtons, "Update Schools", (s, e) => UpdateSchoolsCsv());
            AddButton(teamButtons, "Remove", (s, e) => RemoveTeam());
            AddButton(teamButtons, "Random Roster", (s, e) => RandomRoster());
            AddButton(teamButtons, "Import Roster...", (s, e) => ImportSelectedTeamRosterFromLibrary());
            AddButton(teamButtons, "Base Lineup...", (s, e) => ManageBaseLineup());
            AddButton(teamButtons, "Pitching Plan...", (s, e) => ManagePitchingPlan());
            AddButton(teamButtons, "JV Pool...", (s, e) => ManageJvPool());
            AddButton(teamButtons, "Injured Reserve...", (s, e) => ManageInjuredReserve());
            AddButton(teamButtons, "Coaches...", (s, e) => ManageTeamCoaches());
            AddButton(teamButtons, "Home Field...", (s, e) => ManageTeamHomeField());
            AddButton(teamButtons, "Field Editor...", (s, e) => OpenFieldEditor());
            AddButton(teamButtons, "Scoreboard...", (s, e) => ManageTeamScoreboardTemplate());
            AddButton(teamButtons, "Uniforms...", (s, e) => ManageTeamUniforms());
            AddButton(teamButtons, "Logo...", (s, e) => ManageTeamLogo());
            AddButton(teamButtons, "Photos...", (s, e) => ManageTeamPhotos());
            AddButton(teamButtons, "Music Playlist...", (s, e) => ManageTeamMusic());
            AddButton(teamButtons, "Clear Music", (s, e) => ClearTeamMusic());
            AddButton(teamButtons, "Anthem Images...", (s, e) => ManageTeamNationalAnthemImages());
            AddButton(teamButtons, "Sprites...", (s, e) => ManageTeamSprites());
            AddButton(teamButtons, "Team Cutscenes...", (s, e) => ManageTeamCutscenes());
            left.Controls.Add(teamButtons, 0, 1);
            split.Panel1.Controls.Add(left);

            var right = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1, Padding = new Padding(8) };
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
            right.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, 114));
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));

            var fields = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 10, RowCount = 2 };
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 76));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 56));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 78));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 66));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 44));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 82));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 44));
            fields.Controls.Add(new Label { Text = "Team Name", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
            _cityBox = new TextBox { Dock = DockStyle.Fill, MaxLength = 0 };
            _cityBox.TextChanged += (s, e) => UpdateTeamText();
            fields.Controls.Add(_cityBox, 1, 0);
            fields.Controls.Add(new Label { Text = "Mascot", AutoSize = true, Anchor = AnchorStyles.Left }, 2, 0);
            _nicknameBox = new TextBox { Dock = DockStyle.Fill, MaxLength = 0 };
            _nicknameBox.TextChanged += (s, e) => UpdateTeamText();
            fields.Controls.Add(_nicknameBox, 3, 0);
            fields.Controls.Add(new Label { Text = "Scoreboard", AutoSize = true, Anchor = AnchorStyles.Left }, 4, 0);
            _abbrBox = new TextBox { Dock = DockStyle.Fill, MaxLength = Team.MaxScoreboardAbbreviationLength, CharacterCasing = CharacterCasing.Upper };
            _abbrBox.TextChanged += (s, e) => UpdateTeamText();
            fields.Controls.Add(_abbrBox, 5, 0);
            fields.Controls.Add(new Label { Text = "Primary", AutoSize = true, Anchor = AnchorStyles.Left }, 6, 0);
            _primaryPanel = ColorPanel();
            _primaryPanel.Click += (s, e) => PickColor(true);
            fields.Controls.Add(_primaryPanel, 7, 0);
            fields.Controls.Add(new Label { Text = "Secondary", AutoSize = true, Anchor = AnchorStyles.Left }, 8, 0);
            _secondaryPanel = ColorPanel();
            _secondaryPanel.Click += (s, e) => PickColor(false);
            fields.Controls.Add(_secondaryPanel, 9, 0);
            fields.Controls.Add(new Label { Text = "Coach", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
            _coachBox = new TextBox { Dock = DockStyle.Fill, MaxLength = 40 };
            _coachBox.TextChanged += (s, e) => UpdateTeamText();
            fields.Controls.Add(_coachBox, 1, 1);
            fields.SetColumnSpan(_coachBox, 3);
            right.Controls.Add(fields, 0, 0);

            _rosterGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false
            };
            _rosterGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "name", HeaderText = "Name", Width = 180 });
            var role = new DataGridViewComboBoxColumn { Name = "role", HeaderText = "Role", Width = 90 };
            role.Items.AddRange(PlayerRole.Batter, PlayerRole.Pitcher);
            _rosterGrid.Columns.Add(role);
            var classification = new DataGridViewComboBoxColumn { Name = "classification", HeaderText = "Class", Width = 100 };
            classification.Items.AddRange(
                PlayerClassification.Freshman,
                PlayerClassification.Sophomore,
                PlayerClassification.Junior,
                PlayerClassification.Senior);
            _rosterGrid.Columns.Add(classification);
            _rosterGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "positions", HeaderText = "Positions", Width = 110 });
            _rosterGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "bats", HeaderText = "Bats", Width = 54 });
            _rosterGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "throws", HeaderText = "Throws", Width = 62 });
            _rosterGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "careerpitchcount", HeaderText = "Pitch Cap", Width = 76 });
            _rosterGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "potential", HeaderText = "Pot", Width = 58 });
            _rosterGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "workethic", HeaderText = "Work", Width = 58 });
            _rosterGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "durability", HeaderText = "Dur", Width = 58 });
            _rosterGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "regressionrisk", HeaderText = "Risk", Width = 58 });
            var injuryStatus = new DataGridViewComboBoxColumn { Name = "injurystatus", HeaderText = "Injury", Width = 86 };
            injuryStatus.Items.AddRange(PlayerInjuryStatus.Healthy, PlayerInjuryStatus.DayToDay, PlayerInjuryStatus.Out);
            _rosterGrid.Columns.Add(injuryStatus);
            _rosterGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "injuryname", HeaderText = "Injury Name", Width = 130 });
            _rosterGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "injurygames", HeaderText = "Out", Width = 46 });
            _rosterGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "injurymissed", HeaderText = "Missed", Width = 58 });
            _rosterGrid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "medicaltag", HeaderText = "Med", Width = 46 });
            _rosterGrid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "medicaleligible", HeaderText = "Med Elig", Width = 62, ReadOnly = true });
            _rosterGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "varsityseasons", HeaderText = "V Years", Width = 58, ReadOnly = true });
            _rosterGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "callupseason", HeaderText = "Call-Up", Width = 62, ReadOnly = true });
            _rosterGrid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "redshirtactive", HeaderText = "RS", Width = 42 });
            _rosterGrid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "redshirtused", HeaderText = "RS Used", Width = 62, ReadOnly = true });
            _rosterGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "allstar", HeaderText = "All-Star", Width = 70, ReadOnly = true });
            _rosterGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "awards", HeaderText = "Awards", Width = 150, ReadOnly = true });
            _rosterGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "jersey", HeaderText = "Jersey", Width = 78 });
            _rosterGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "pants", HeaderText = "Pants", Width = 78 });
            _rosterGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "caphelmet", HeaderText = "Cap/Helm", Width = 82 });
            foreach (var pitch in PitchProfileEngine.AllPitchTypes)
            {
                string key = PitchColumnKey(pitch);
                _rosterGrid.Columns.Add(new DataGridViewCheckBoxColumn { Name = key + "_enabled", HeaderText = PitchProfileEngine.ShortName(pitch), Width = 48 });
                _rosterGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = key + "_effectiveness", HeaderText = PitchProfileEngine.ShortName(pitch) + " Eff", Width = 64 });
            }
            _rosterGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "pitchstrengths", HeaderText = "Pitch Strengths", Width = 130 });
            _rosterGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "pitchweaknesses", HeaderText = "Pitch Weaknesses", Width = 140 });
            _rosterGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "pitchscout", HeaderText = "Pitch Scout", Width = 210, ReadOnly = true });
            foreach (var c in new[] { "Contact", "Power", "Speed", "StealAggression", "BaseRunning", "Fielding", "HoldRunner", "Pickoff", "DeliveryTime", "ArmStrength", "PopTime", "Accuracy", "TagRating", "Pitching", "Stamina" })
                _rosterGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = c.ToLowerInvariant(), HeaderText = c, Width = 82 });
            _rosterGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "overall", HeaderText = "Overall", ReadOnly = true, Width = 76 });
            _rosterGrid.CellEndEdit += (s, e) => SaveRosterCell(e);
            _rosterGrid.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (_rosterGrid.IsCurrentCellDirty)
                    _rosterGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
            _rosterGrid.CellValueChanged += (s, e) =>
            {
                if (!_suppress && e.RowIndex >= 0 && e.ColumnIndex >= 0 &&
                    _rosterGrid.Columns[e.ColumnIndex] is DataGridViewCheckBoxColumn)
                {
                    SaveRosterCell(e);
                }
            };
            _rosterGrid.SelectionChanged += (s, e) => RefreshSelectedPlayerAvatar();
            _rosterGrid.DataError += (s, e) => { e.ThrowException = false; };
            right.Controls.Add(_rosterGrid, 0, 1);

            var teamPagePanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            teamPagePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));
            teamPagePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            var avatarPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Padding = new Padding(4) };
            avatarPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104));
            avatarPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _playerAvatarBox = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White
            };
            _playerAvatarLabel = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                Text = "Select a player to show avatar."
            };
            avatarPanel.Controls.Add(_playerAvatarBox, 0, 0);
            avatarPanel.Controls.Add(_playerAvatarLabel, 1, 0);
            teamPagePanel.Controls.Add(avatarPanel, 0, 0);

            _teamBadgesPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                WrapContents = false,
                Padding = new Padding(4),
                BackColor = Color.FromArgb(248, 248, 248)
            };
            teamPagePanel.Controls.Add(_teamBadgesPanel, 1, 0);
            right.Controls.Add(teamPagePanel, 0, 2);

            var playerButtons = new FlowLayoutPanel { Dock = DockStyle.Fill };
            AddButton(playerButtons, "Add Player", (s, e) => AddPlayer());
            AddButton(playerButtons, "Remove Player", (s, e) => RemovePlayer());
            AddButton(playerButtons, "Player Photo...", (s, e) => ManagePlayerAvatar());
            AddButton(playerButtons, "Player Sprite...", (s, e) => ManageTeamSprites());
            AddButton(playerButtons, "Export Badges...", (s, e) => ExportTeamBadgeStrip());
            AddButton(playerButtons, "Roster Excel", (s, e) => ExportGrid(_rosterGrid, RosterExportTitle(), ExportFormat.Excel));
            AddButton(playerButtons, "Roster Word", (s, e) => ExportGrid(_rosterGrid, RosterExportTitle(), ExportFormat.Word));
            right.Controls.Add(playerButtons, 0, 3);
            split.Panel2.Controls.Add(right);
        }

        private void BuildSimTab(TabPage tab)
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1, Padding = new Padding(10) };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));

            var bar = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
            bar.Controls.Add(new Label { Text = "Away", AutoSize = true, Margin = new Padding(0, 8, 4, 0) });
            _awayCombo = new ComboBox { Width = 220, DropDownStyle = ComboBoxStyle.DropDownList };
            _awayCombo.SelectedIndexChanged += (s, e) =>
            {
                RefreshControlTeamCombo();
                RefreshGameUniformCombos();
            };
            bar.Controls.Add(_awayCombo);
            bar.Controls.Add(new Label { Text = "Home", AutoSize = true, Margin = new Padding(12, 8, 4, 0) });
            _homeCombo = new ComboBox { Width = 220, DropDownStyle = ComboBoxStyle.DropDownList };
            _homeCombo.SelectedIndexChanged += (s, e) =>
            {
                ApplyHomeTeamFieldSelection();
                RefreshControlTeamCombo();
                RefreshGameUniformCombos();
            };
            bar.Controls.Add(_homeCombo);
            AddButton(bar, "Player vs CPU", (s, e) => LaunchPlayableGame(GameMode.UserVsCpu));
            AddButton(bar, "Player vs Player", (s, e) => LaunchPlayableGame(GameMode.PlayerVsPlayer));
            AddButton(bar, "Watch CPU", (s, e) => LaunchPlayableGame(GameMode.CpuVsCpuWatch));
            AddButton(bar, "Resume Saved", (s, e) => ResumeSavedGame());
            AddButton(bar, "Replay Library...", (s, e) => WatchReplay());
            AddButton(bar, "Quick Sim", (s, e) => SimulateGame());
            AddButton(bar, "Sim Season", (s, e) => SimSeason());
            bar.Controls.Add(new Label { Text = "Commit to", AutoSize = true, Margin = new Padding(12, 8, 4, 0) });
            _commitSeasonCombo = new ComboBox { Width = 190, DropDownStyle = ComboBoxStyle.DropDownList };
            _commitSeasonCombo.SelectedIndexChanged += (s, e) => RefreshScheduleCombo();
            bar.Controls.Add(_commitSeasonCombo);
            root.Controls.Add(bar, 0, 0);

            var rulesBar = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
            rulesBar.Controls.Add(new Label { Text = "Scheduled", AutoSize = true, Margin = new Padding(0, 8, 4, 0) });
            _scheduledGameCombo = new ComboBox { Width = 260, DropDownStyle = ComboBoxStyle.DropDownList };
            _scheduledGameCombo.SelectedIndexChanged += (s, e) => LoadScheduledGameSelection();
            rulesBar.Controls.Add(_scheduledGameCombo);
            rulesBar.Controls.Add(new Label { Text = "Away Uniform", AutoSize = true, Margin = new Padding(12, 8, 4, 0) });
            _awayUniformCombo = new ComboBox { Width = 170, DropDownStyle = ComboBoxStyle.DropDownList };
            _awayUniformCombo.SelectedIndexChanged += (s, e) => ApplyGameUniformSelection();
            rulesBar.Controls.Add(_awayUniformCombo);
            rulesBar.Controls.Add(new Label { Text = "Home Uniform", AutoSize = true, Margin = new Padding(12, 8, 4, 0) });
            _homeUniformCombo = new ComboBox { Width = 170, DropDownStyle = ComboBoxStyle.DropDownList };
            _homeUniformCombo.SelectedIndexChanged += (s, e) => ApplyGameUniformSelection();
            rulesBar.Controls.Add(_homeUniformCombo);
            rulesBar.Controls.Add(new Label { Text = "User Controls", AutoSize = true, Margin = new Padding(12, 8, 4, 0) });
            _controlTeamCombo = new ComboBox { Width = 220, DropDownStyle = ComboBoxStyle.DropDownList };
            rulesBar.Controls.Add(_controlTeamCombo);
            rulesBar.Controls.Add(new Label { Text = "PVP Inputs", AutoSize = true, Margin = new Padding(12, 8, 4, 0) });
            _pvpInputCombo = new ComboBox { Width = 235, DropDownStyle = ComboBoxStyle.DropDownList };
            _pvpInputCombo.Items.Add(new PvpInputAssignmentItem { AwayUsesKeyboard = true, Text = "Away Keyboard / Home Controller" });
            _pvpInputCombo.Items.Add(new PvpInputAssignmentItem { AwayUsesKeyboard = false, Text = "Away Controller / Home Keyboard" });
            _pvpInputCombo.SelectedIndex = 0;
            rulesBar.Controls.Add(_pvpInputCombo);
            rulesBar.Controls.Add(new Label { Text = "Field", AutoSize = true, Margin = new Padding(12, 8, 4, 0) });
            _fieldPresetCombo = new ComboBox { Width = 250, DropDownStyle = ComboBoxStyle.DropDownList };
            _fieldPresetCombo.SelectedIndexChanged += (s, e) => _fieldPanel?.Invalidate();
            RefreshFieldPresetCombo();
            rulesBar.Controls.Add(_fieldPresetCombo);
            AddButton(rulesBar, "Field Editor...", (s, e) => OpenFieldEditor());
            rulesBar.Controls.Add(new Label { Text = "Game Length", AutoSize = true, Margin = new Padding(0, 8, 4, 0) });
            _inningsCombo = new ComboBox { Width = 74, DropDownStyle = ComboBoxStyle.DropDownList };
            for (int innings = 5; innings <= 9; innings++)
                _inningsCombo.Items.Add(innings);
            _inningsCombo.SelectedIndexChanged += (s, e) => UpdateGameInnings();
            rulesBar.Controls.Add(_inningsCombo);
            rulesBar.Controls.Add(new Label { Text = "innings", AutoSize = true, Margin = new Padding(4, 8, 4, 0) });
            _mercyRuleBox = new CheckBox { Text = "Mercy", AutoSize = true, Margin = new Padding(18, 7, 4, 0) };
            _mercyRuleBox.CheckedChanged += (s, e) => UpdateGameRules();
            rulesBar.Controls.Add(_mercyRuleBox);
            _extraInningsBox = new CheckBox { Text = "Extras", AutoSize = true, Margin = new Padding(12, 7, 4, 0) };
            _extraInningsBox.CheckedChanged += (s, e) => UpdateGameRules();
            rulesBar.Controls.Add(_extraInningsBox);
            _extraRunnerBox = new CheckBox { Text = "Runner on 2B", AutoSize = true, Margin = new Padding(12, 7, 4, 0) };
            _extraRunnerBox.CheckedChanged += (s, e) => UpdateGameRules();
            rulesBar.Controls.Add(_extraRunnerBox);
            root.Controls.Add(rulesBar, 0, 1);

            _fieldPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(40, 115, 64) };
            _fieldPanel.Paint += PaintField;
            root.Controls.Add(_fieldPanel, 0, 2);

            _simResult = new Label
            {
                Dock = DockStyle.Fill,
                Font = new Font(Font.FontFamily, 16, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter
            };
            root.Controls.Add(_simResult, 0, 3);
            tab.Controls.Add(root);
        }

        private void BuildStructureTab(TabPage tab)
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Padding = new Padding(8) };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var bar = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
            AddButton(bar, "Add Conference", (s, e) => AddConference());
            AddButton(bar, "Remove Conference", (s, e) => RemoveSelectedConference());
            AddButton(bar, "Normalize Structure", (s, e) => NormalizeStructure());
            root.Controls.Add(bar, 0, 0);

            _structureTree = new TreeView { Dock = DockStyle.Fill, HideSelection = false };
            root.Controls.Add(_structureTree, 0, 1);
            tab.Controls.Add(root);
        }

        private void BuildSeasonsTab(TabPage tab)
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, Padding = new Padding(8) };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var bar = new FlowLayoutPanel { Dock = DockStyle.Fill };
            AddButton(bar, "Add Season", (s, e) => AddSeason());
            AddButton(bar, "Generate Schedule", (s, e) => GenerateScheduleForSelectedSeason());
            AddButton(bar, "Generate Playoffs", (s, e) => GeneratePlayoffs());
            AddButton(bar, "Sim Playoff Series", (s, e) => SimPlayoffSeries());
            AddButton(bar, "Advance Offseason", (s, e) => AdvanceOffseason());
            bar.Controls.Add(new Label { Text = "View", AutoSize = true, Margin = new Padding(12, 8, 4, 0) });
            _seasonCombo = new ComboBox { Width = 220, DropDownStyle = ComboBoxStyle.DropDownList };
            _seasonCombo.SelectedIndexChanged += (s, e) => RefreshSeasonViews();
            bar.Controls.Add(_seasonCombo);
            root.Controls.Add(bar, 0, 0);

            _championLabel = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font(Font.FontFamily, 14, FontStyle.Bold),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(242, 244, 248),
                ForeColor = Color.FromArgb(31, 41, 55)
            };
            root.Controls.Add(_championLabel, 0, 1);

            var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 260 };
            _gamesGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None
            };
            _gamesGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Status", Width = 90 });
            _gamesGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Type", Width = 110 });
            _gamesGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Date", Width = 130 });
            _gamesGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Away", Width = 210 });
            _gamesGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "R", Width = 48 });
            _gamesGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Home", Width = 210 });
            _gamesGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "R", Width = 48 });
            _gamesGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Winner", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            split.Panel1.Controls.Add(_gamesGrid);

            _playoffGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None
            };
            _playoffGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Round", Width = 54 });
            _playoffGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Series", Width = 150 });
            _playoffGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Best", Width = 54 });
            _playoffGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Group", Width = 220 });
            _playoffGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Team A", Width = 180 });
            _playoffGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Team B", Width = 180 });
            _playoffGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Home Adv", Width = 180 });
            _playoffGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Result", Width = 80 });
            _playoffGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Notes", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            split.Panel2.Controls.Add(_playoffGrid);
            root.Controls.Add(split, 0, 2);
            tab.Controls.Add(root);
        }

        private void BuildRankingsTab(TabPage tab)
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, Padding = new Padding(8) };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var bar = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
            bar.Controls.Add(new Label { Text = "Season", AutoSize = true, Margin = new Padding(4, 10, 4, 0) });
            _rankingSeasonCombo = new ComboBox { Width = 230, DropDownStyle = ComboBoxStyle.DropDownList };
            _rankingSeasonCombo.SelectedIndexChanged += (s, e) =>
            {
                RefreshRankingPollCombo();
                RefreshRankingGrid();
            };
            bar.Controls.Add(_rankingSeasonCombo);
            bar.Controls.Add(new Label { Text = "Poll", AutoSize = true, Margin = new Padding(12, 10, 4, 0) });
            _rankingPollCombo = new ComboBox { Width = 230, DropDownStyle = ComboBoxStyle.DropDownList };
            _rankingPollCombo.SelectedIndexChanged += (s, e) => RefreshRankingGrid();
            bar.Controls.Add(_rankingPollCombo);
            AddButton(bar, "Pre-Season Poll", (s, e) => GenerateRankingPoll(RankingPollType.PreSeason));
            AddButton(bar, "Current Week Poll", (s, e) => GenerateRankingPoll(RankingPollType.Weekly));
            AddButton(bar, "Final Poll", (s, e) => GenerateRankingPoll(RankingPollType.Final));
            AddButton(bar, "Export Poll Excel", (s, e) => ExportSelectedRankingPoll(ExportFormat.Excel));
            AddButton(bar, "Export Poll Word", (s, e) => ExportSelectedRankingPoll(ExportFormat.Word));
            AddButton(bar, "Export All Polls", (s, e) => ExportAllRankingPolls());
            root.Controls.Add(bar, 0, 0);

            _rankingSummaryLabel = new Label
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(8),
                TextAlign = ContentAlignment.MiddleLeft
            };
            root.Controls.Add(_rankingSummaryLabel, 0, 1);

            _rankingGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false
            };
            AddGridColumn(_rankingGrid, "Rank", 54);
            AddGridColumn(_rankingGrid, "Prev", 54);
            AddGridColumn(_rankingGrid, "Team", 210);
            AddGridColumn(_rankingGrid, "W", 42);
            AddGridColumn(_rankingGrid, "L", 42);
            AddGridColumn(_rankingGrid, "T", 42);
            AddGridColumn(_rankingGrid, "Score", 72);
            AddGridColumn(_rankingGrid, "Poll", 68);
            AddGridColumn(_rankingGrid, "Computer", 78);
            AddGridColumn(_rankingGrid, "Ranked W", 78);
            AddGridColumn(_rankingGrid, "SOS", 62);
            AddGridColumn(_rankingGrid, "Diff", 58);
            AddGridColumn(_rankingGrid, "Notes", 360);
            root.Controls.Add(_rankingGrid, 0, 2);
            tab.Controls.Add(root);
        }

        private void BuildAllStarsTab(TabPage tab)
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, Padding = new Padding(8) };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var bar = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
            bar.Controls.Add(new Label { Text = "Season", AutoSize = true, Margin = new Padding(4, 10, 4, 0) });
            _allStarSeasonCombo = new ComboBox { Width = 230, DropDownStyle = ComboBoxStyle.DropDownList };
            _allStarSeasonCombo.SelectedIndexChanged += (s, e) => RefreshAllStarViews();
            bar.Controls.Add(_allStarSeasonCombo);
            bar.Controls.Add(new Label { Text = "Stats", AutoSize = true, Margin = new Padding(12, 10, 4, 0) });
            _allStarStatsScopeCombo = new ComboBox { Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
            _allStarStatsScopeCombo.Items.AddRange(new object[] { "Selected Game", "Player Career", "Team/Side History", "All-Time Leaders" });
            _allStarStatsScopeCombo.SelectedIndex = 0;
            _allStarStatsScopeCombo.SelectedIndexChanged += (s, e) => RefreshAllStarViews();
            bar.Controls.Add(_allStarStatsScopeCombo);
            AddButton(bar, "Generate Candidates", (s, e) => RefreshAllStarViews());
            AddButton(bar, "Add Candidate", (s, e) => AddSelectedAllStarCandidate());
            AddButton(bar, "Remove Selection", (s, e) => RemoveSelectedAllStarSelection());
            AddButton(bar, "Auto Finalize Teams", (s, e) => AutoFinalizeAllStars());
            AddButton(bar, "Play vs CPU", (s, e) => PlayAllStarGameVsCpu());
            AddButton(bar, "Sim All-Star Game", (s, e) => SimAllStarGame());
            AddButton(bar, "Watch All-Star Game", (s, e) => WatchAllStarGame());
            AddButton(bar, "Export Excel", (s, e) => ExportGrid(CurrentAllStarGrid(), CurrentAllStarExportTitle(), ExportFormat.Excel));
            AddButton(bar, "Export Word", (s, e) => ExportGrid(CurrentAllStarGrid(), CurrentAllStarExportTitle(), ExportFormat.Word));
            root.Controls.Add(bar, 0, 0);

            _allStarSummaryLabel = new Label
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(8),
                TextAlign = ContentAlignment.MiddleLeft
            };
            root.Controls.Add(_allStarSummaryLabel, 0, 1);

            var tabs = new TabControl { Dock = DockStyle.Fill };
            _allStarTabs = tabs;
            var candidatesTab = new TabPage("Candidates");
            var selectionsTab = new TabPage("Selections");
            var gameStatsTab = new TabPage("Game Stats");
            _allStarCandidatesGrid = CreateReadOnlyGrid();
            _allStarSelectionsGrid = CreateReadOnlyGrid();
            _allStarGameStatsGrid = CreateReadOnlyGrid();
            AddAllStarColumns(_allStarCandidatesGrid, includeSelected: false);
            AddAllStarColumns(_allStarSelectionsGrid, includeSelected: true);
            AddAllStarGameStatsColumns(_allStarGameStatsGrid);
            candidatesTab.Controls.Add(_allStarCandidatesGrid);
            selectionsTab.Controls.Add(_allStarSelectionsGrid);
            gameStatsTab.Controls.Add(_allStarGameStatsGrid);
            tabs.TabPages.AddRange(new[] { candidatesTab, selectionsTab, gameStatsTab });
            root.Controls.Add(tabs, 0, 2);
            tab.Controls.Add(root);
        }

        private static void AddAllStarColumns(DataGridView grid, bool includeSelected)
        {
            AddGridColumn(grid, includeSelected ? "All-Star Team" : "Projected Team", 118);
            AddGridColumn(grid, "Score", 60);
            AddGridColumn(grid, "Player", 180);
            AddGridColumn(grid, "Team", 180);
            AddGridColumn(grid, "Role", 74);
            AddGridColumn(grid, "Pos", 80);
            AddGridColumn(grid, "G", 48);
            AddGridColumn(grid, "H", 52);
            AddGridColumn(grid, "HR", 52);
            AddGridColumn(grid, "RBI", 56);
            AddGridColumn(grid, "SB", 52);
            AddGridColumn(grid, "W", 48);
            AddGridColumn(grid, "SV", 48);
            AddGridColumn(grid, "K", 56);
            AddGridColumn(grid, "AVG", 62);
            AddGridColumn(grid, "OPS", 62);
            AddGridColumn(grid, "ERA", 62);
        }

        private static void AddAllStarGameStatsColumns(DataGridView grid)
        {
            AddGridColumn(grid, "All-Star Team", 118);
            AddGridColumn(grid, "Player", 180);
            AddGridColumn(grid, "Original Team", 180);
            AddGridColumn(grid, "Role", 74);
            AddGridColumn(grid, "R", 44);
            AddGridColumn(grid, "PA", 52);
            AddGridColumn(grid, "XBH", 50);
            AddGridColumn(grid, "AB", 52);
            AddGridColumn(grid, "H", 48);
            AddGridColumn(grid, "2B", 44);
            AddGridColumn(grid, "3B", 44);
            AddGridColumn(grid, "HR", 48);
            AddGridColumn(grid, "RBI", 52);
            AddGridColumn(grid, "BB", 48);
            AddGridColumn(grid, "IBB", 48);
            AddGridColumn(grid, "SO", 48);
            AddGridColumn(grid, "SB", 48);
            AddGridColumn(grid, "GIDP", 52);
            AddGridColumn(grid, "ROE", 48);
            AddGridColumn(grid, "IP", 56);
            AddGridColumn(grid, "HLD", 48);
            AddGridColumn(grid, "BS", 44);
            AddGridColumn(grid, "CG", 44);
            AddGridColumn(grid, "SHO", 48);
            AddGridColumn(grid, "K", 48);
            AddGridColumn(grid, "ER", 48);
            AddGridColumn(grid, "RA", 48);
            AddGridColumn(grid, "ERA", 62);
            AddGridColumn(grid, "WHIP", 64);
            AddGridColumn(grid, "H-A", 54);
            AddGridColumn(grid, "2B-A", 54);
            AddGridColumn(grid, "3B-A", 54);
            AddGridColumn(grid, "BB-A", 56);
            AddGridColumn(grid, "IBB-A", 58);
            AddGridColumn(grid, "WP", 48);
            AddGridColumn(grid, "BK", 48);
            AddGridColumn(grid, "PB", 48);
            AddGridColumn(grid, "E", 44);
            AddGridColumn(grid, "DP", 44);
            AddGridColumn(grid, "Def IP", 58);
            AddGridColumn(grid, "TC", 48);
            AddGridColumn(grid, "SBA-C", 56);
            AddGridColumn(grid, "CS-C", 52);
            AddGridColumn(grid, "CS%", 58);
            AddGridColumn(grid, "PC", 48);
        }

        private void BuildAwardsTab(TabPage tab)
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, Padding = new Padding(8) };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var bar = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
            bar.Controls.Add(new Label { Text = "Season", AutoSize = true, Margin = new Padding(4, 10, 4, 0) });
            _awardSeasonCombo = new ComboBox { Width = 230, DropDownStyle = ComboBoxStyle.DropDownList };
            _awardSeasonCombo.SelectedIndexChanged += (s, e) => RefreshAwardViews();
            bar.Controls.Add(_awardSeasonCombo);
            AddButton(bar, "Refresh Races", (s, e) => RefreshAwardViews());
            AddButton(bar, "Finalize Awards", (s, e) => FinalizeSeasonAwards());
            AddButton(bar, "Export Excel", (s, e) => ExportGrid(CurrentAwardGrid(), CurrentAwardExportTitle(), ExportFormat.Excel));
            AddButton(bar, "Export Word", (s, e) => ExportGrid(CurrentAwardGrid(), CurrentAwardExportTitle(), ExportFormat.Word));
            root.Controls.Add(bar, 0, 0);

            _awardSummaryLabel = new Label
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(8),
                TextAlign = ContentAlignment.MiddleLeft
            };
            root.Controls.Add(_awardSummaryLabel, 0, 1);

            _awardTabs = new TabControl { Dock = DockStyle.Fill };
            var races = new TabPage("Award Races");
            var position = new TabPage("Position Awards");
            var glove = new TabPage("Gold Glove");
            var bat = new TabPage("Silver Bat");
            var finalists = new TabPage("Finalists");
            var history = new TabPage("Award History");

            _awardRacesGrid = CreateReadOnlyGrid();
            _positionAwardsGrid = CreateReadOnlyGrid();
            _goldGloveGrid = CreateReadOnlyGrid();
            _silverBatGrid = CreateReadOnlyGrid();
            _awardFinalistsGrid = CreateReadOnlyGrid();
            _awardHistoryGrid = CreateReadOnlyGrid();
            AddAwardColumns(_awardRacesGrid);
            AddAwardColumns(_positionAwardsGrid);
            AddAwardColumns(_goldGloveGrid);
            AddAwardColumns(_silverBatGrid);
            AddAwardColumns(_awardFinalistsGrid);
            AddAwardColumns(_awardHistoryGrid);

            races.Controls.Add(_awardRacesGrid);
            position.Controls.Add(_positionAwardsGrid);
            glove.Controls.Add(_goldGloveGrid);
            bat.Controls.Add(_silverBatGrid);
            finalists.Controls.Add(_awardFinalistsGrid);
            history.Controls.Add(_awardHistoryGrid);
            _awardTabs.TabPages.AddRange(new[] { races, position, glove, bat, finalists, history });
            root.Controls.Add(_awardTabs, 0, 2);
            tab.Controls.Add(root);
        }

        private static void AddAwardColumns(DataGridView grid)
        {
            AddGridColumn(grid, "Award", 170);
            AddGridColumn(grid, "Category", 110);
            AddGridColumn(grid, "Rank", 54);
            AddGridColumn(grid, "Status", 74);
            AddGridColumn(grid, "Player", 180);
            AddGridColumn(grid, "Team", 180);
            AddGridColumn(grid, "Pos", 70);
            AddGridColumn(grid, "Score", 70);
            AddGridColumn(grid, "Key Stats", 280);
        }

        private void BuildDynastyTab(TabPage tab)
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Padding = new Padding(8) };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var bar = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
            bar.Controls.Add(new Label
            {
                Text = "Championship history starts with Season 1.",
                AutoSize = true,
                Margin = new Padding(4, 10, 4, 0)
            });
            AddButton(bar, "Export Excel", (s, e) => ExportGrid(_championshipGrid, "Championship History", ExportFormat.Excel));
            AddButton(bar, "Export Word", (s, e) => ExportGrid(_championshipGrid, "Championship History", ExportFormat.Word));
            root.Controls.Add(bar, 0, 0);

            _championshipGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                RowTemplate = { Height = 72 }
            };
            _championshipGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Season", Width = 86 });
            _championshipGrid.Columns.Add(new DataGridViewImageColumn
            {
                HeaderText = "Logo",
                Width = 92,
                ImageLayout = DataGridViewImageCellLayout.Zoom
            });
            _championshipGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Team", Width = 260 });
            _championshipGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Record", Width = 92 });
            _championshipGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Season Name", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            root.Controls.Add(_championshipGrid, 0, 1);
            tab.Controls.Add(root);
        }

        private void BuildHallOfFameTab(TabPage tab)
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, Padding = new Padding(8) };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var bar = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
            bar.Controls.Add(new Label { Text = "Team Hall", AutoSize = true, Margin = new Padding(4, 10, 4, 0) });
            _hofTeamCombo = new ComboBox { Width = 240, DropDownStyle = ComboBoxStyle.DropDownList };
            _hofTeamCombo.SelectedIndexChanged += (s, e) => RefreshHallOfFameViews();
            bar.Controls.Add(_hofTeamCombo);
            var induct = new Button { Text = "Induct Selected Candidate", AutoSize = true, Margin = new Padding(14, 4, 4, 4) };
            induct.Click += (s, e) => InductSelectedHallOfFameCandidate();
            bar.Controls.Add(induct);
            var remove = new Button { Text = "Remove Selected Inductee", AutoSize = true, Margin = new Padding(4) };
            remove.Click += (s, e) => RemoveSelectedHallOfFameEntry();
            bar.Controls.Add(remove);
            AddButton(bar, "Export Excel", (s, e) => ExportGrid(CurrentHallOfFameGrid(), CurrentHallOfFameExportTitle(), ExportFormat.Excel));
            AddButton(bar, "Export Word", (s, e) => ExportGrid(CurrentHallOfFameGrid(), CurrentHallOfFameExportTitle(), ExportFormat.Word));
            AddButton(bar, "Export Full Hall...", (s, e) => ExportFullHallOfFame());
            AddButton(bar, "Export Team Hall Page...", (s, e) => ExportTeamHallOfFamePage());
            root.Controls.Add(bar, 0, 0);

            _hofSummaryLabel = new Label
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(8),
                TextAlign = ContentAlignment.MiddleLeft
            };
            root.Controls.Add(_hofSummaryLabel, 0, 1);

            var tabs = new TabControl { Dock = DockStyle.Fill };
            _hofTabs = tabs;
            var dynasty = new TabPage("Dynasty Hall");
            var team = new TabPage("Team Inductees");
            var teamPage = new TabPage("Team Hall");
            var candidates = new TabPage("Candidates");
            var coachCandidates = new TabPage("Coach Candidates");
            var records = new TabPage("All-Time Records");
            _hofDynastyGrid = CreateReadOnlyGrid();
            _hofTeamGrid = CreateReadOnlyGrid();
            _hofCandidatesGrid = CreateReadOnlyGrid();
            _hofCoachCandidatesGrid = CreateReadOnlyGrid();
            _hofRecordsGrid = CreateReadOnlyGrid();

            AddHallEntryColumns(_hofDynastyGrid);
            AddHallEntryColumns(_hofTeamGrid);
            AddHallCandidateColumns(_hofCandidatesGrid);
            AddCoachHallCandidateColumns(_hofCoachCandidatesGrid);
            AddGridColumn(_hofRecordsGrid, "Record", 150);
            AddGridColumn(_hofRecordsGrid, "Player", 180);
            AddGridColumn(_hofRecordsGrid, "Team", 180);
            AddGridColumn(_hofRecordsGrid, "Value", 90);
            AddGridColumn(_hofRecordsGrid, "Detail", 260);

            dynasty.Controls.Add(_hofDynastyGrid);
            team.Controls.Add(_hofTeamGrid);
            _teamHallPagePanel = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.FromArgb(232, 236, 242) };
            _teamHallPicture = new PictureBox { SizeMode = PictureBoxSizeMode.Normal, Location = new Point(12, 12) };
            _teamHallPagePanel.Controls.Add(_teamHallPicture);
            teamPage.Controls.Add(_teamHallPagePanel);
            candidates.Controls.Add(_hofCandidatesGrid);
            coachCandidates.Controls.Add(_hofCoachCandidatesGrid);
            records.Controls.Add(_hofRecordsGrid);
            tabs.TabPages.AddRange(new[] { dynasty, team, teamPage, candidates, coachCandidates, records });
            root.Controls.Add(tabs, 0, 2);
            tab.Controls.Add(root);
        }

        private void BuildInboxTab(TabPage tab)
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, Padding = new Padding(8) };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var bar = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
            bar.Controls.Add(new Label { Text = "Filter", AutoSize = true, Margin = new Padding(4, 10, 4, 0) });
            _inboxFilterCombo = new ComboBox { Width = 160, DropDownStyle = ComboBoxStyle.DropDownList };
            _inboxFilterCombo.Items.AddRange(new object[] { "All", "Unread", "Scouting Reports", "Game Reports", "Important" });
            _inboxFilterCombo.SelectedIndex = 0;
            _inboxFilterCombo.SelectedIndexChanged += (s, e) => RefreshInboxGrid();
            bar.Controls.Add(_inboxFilterCombo);
            AddButton(bar, "Mark Read", (s, e) => MarkSelectedInboxRead());
            AddButton(bar, "Mark All Read", (s, e) => MarkAllInboxRead());
            AddButton(bar, "Delete", (s, e) => DeleteSelectedInboxMessage());
            root.Controls.Add(bar, 0, 0);

            _inboxSummaryLabel = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(62, 68, 78)
            };
            root.Controls.Add(_inboxSummaryLabel, 0, 1);

            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 260
            };
            _inboxGrid = CreateReadOnlyGrid();
            _inboxGrid.SelectionChanged += (s, e) => ShowSelectedInboxMessage();
            AddGridColumn(_inboxGrid, "Status", 72);
            AddGridColumn(_inboxGrid, "Date", 128);
            AddGridColumn(_inboxGrid, "To", 170);
            AddGridColumn(_inboxGrid, "Team", 170);
            AddGridColumn(_inboxGrid, "Category", 110);
            AddGridColumn(_inboxGrid, "Subject", 390);
            split.Panel1.Controls.Add(_inboxGrid);

            _inboxBodyBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font(FontFamily.GenericMonospace, 10f),
                BackColor = Color.White
            };
            split.Panel2.Controls.Add(_inboxBodyBox);
            root.Controls.Add(split, 0, 2);
            tab.Controls.Add(root);
        }

        private static DataGridView CreateReadOnlyGrid()
        {
            return new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false
            };
        }

        private static void AddHallEntryColumns(DataGridView grid)
        {
            AddGridColumn(grid, "Season", 82);
            AddGridColumn(grid, "Name", 180);
            AddGridColumn(grid, "Team", 180);
            AddGridColumn(grid, "Role", 74);
            AddGridColumn(grid, "Score", 60);
            AddGridColumn(grid, "G", 48);
            AddGridColumn(grid, "PA", 52);
            AddGridColumn(grid, "H", 54);
            AddGridColumn(grid, "XBH", 52);
            AddGridColumn(grid, "HR", 52);
            AddGridColumn(grid, "RBI", 56);
            AddGridColumn(grid, "SB", 52);
            AddGridColumn(grid, "ROE", 50);
            AddGridColumn(grid, "W", 48);
            AddGridColumn(grid, "SV", 48);
            AddGridColumn(grid, "HLD", 48);
            AddGridColumn(grid, "BS", 44);
            AddGridColumn(grid, "CG", 44);
            AddGridColumn(grid, "SHO", 48);
            AddGridColumn(grid, "K", 56);
            AddGridColumn(grid, "RA", 48);
            AddGridColumn(grid, "2B-A", 52);
            AddGridColumn(grid, "3B-A", 52);
            AddGridColumn(grid, "Def IP", 56);
            AddGridColumn(grid, "TC", 48);
            AddGridColumn(grid, "CS%", 58);
            AddGridColumn(grid, "AVG", 62);
            AddGridColumn(grid, "OPS", 62);
            AddGridColumn(grid, "ERA", 62);
            AddGridColumn(grid, "Titles", 62);
            AddGridColumn(grid, "Reason", 260);
        }

        private static void AddHallCandidateColumns(DataGridView grid)
        {
            AddGridColumn(grid, "Recommendation", 118);
            AddGridColumn(grid, "Score", 60);
            AddGridColumn(grid, "Player", 180);
            AddGridColumn(grid, "Team", 180);
            AddGridColumn(grid, "Role", 74);
            AddGridColumn(grid, "G", 48);
            AddGridColumn(grid, "PA", 52);
            AddGridColumn(grid, "H", 54);
            AddGridColumn(grid, "XBH", 52);
            AddGridColumn(grid, "HR", 52);
            AddGridColumn(grid, "RBI", 56);
            AddGridColumn(grid, "SB", 52);
            AddGridColumn(grid, "ROE", 50);
            AddGridColumn(grid, "W", 48);
            AddGridColumn(grid, "SV", 48);
            AddGridColumn(grid, "HLD", 48);
            AddGridColumn(grid, "BS", 44);
            AddGridColumn(grid, "CG", 44);
            AddGridColumn(grid, "SHO", 48);
            AddGridColumn(grid, "K", 56);
            AddGridColumn(grid, "RA", 48);
            AddGridColumn(grid, "2B-A", 52);
            AddGridColumn(grid, "3B-A", 52);
            AddGridColumn(grid, "Def IP", 56);
            AddGridColumn(grid, "TC", 48);
            AddGridColumn(grid, "CS%", 58);
            AddGridColumn(grid, "AVG", 62);
            AddGridColumn(grid, "OPS", 62);
            AddGridColumn(grid, "ERA", 62);
            AddGridColumn(grid, "Titles", 62);
            AddGridColumn(grid, "Reason", 300);
        }

        private static void AddCoachHallCandidateColumns(DataGridView grid)
        {
            AddGridColumn(grid, "Recommendation", 118);
            AddGridColumn(grid, "Score", 60);
            AddGridColumn(grid, "Coach", 180);
            AddGridColumn(grid, "Team", 180);
            AddGridColumn(grid, "W", 52);
            AddGridColumn(grid, "L", 52);
            AddGridColumn(grid, "T", 52);
            AddGridColumn(grid, "Titles", 62);
            AddGridColumn(grid, "Playoff W", 82);
            AddGridColumn(grid, "District W", 82);
            AddGridColumn(grid, "Region W", 78);
            AddGridColumn(grid, "Conference W", 104);
            AddGridColumn(grid, "Reason", 360);
        }

        private void BuildTeamStatsTab(TabPage tab)
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, Padding = new Padding(8) };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var bar = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
            bar.Controls.Add(new Label { Text = "Scope", AutoSize = true, Margin = new Padding(4, 10, 4, 0) });
            _teamStatsScopeCombo = new ComboBox { Width = 110, DropDownStyle = ComboBoxStyle.DropDownList };
            _teamStatsScopeCombo.Items.AddRange(new object[] { "Season", "Playoffs", "Career", "All-Time" });
            _teamStatsScopeCombo.SelectedIndex = 0;
            _teamStatsScopeCombo.SelectedIndexChanged += (s, e) => RefreshTeamStatsGrid();
            bar.Controls.Add(_teamStatsScopeCombo);
            bar.Controls.Add(new Label { Text = "Team", AutoSize = true, Margin = new Padding(4, 10, 4, 0) });
            _teamStatsTeamCombo = new ComboBox { Width = 230, DropDownStyle = ComboBoxStyle.DropDownList };
            _teamStatsTeamCombo.SelectedIndexChanged += (s, e) =>
            {
                RefreshTeamStatsGrid();
                PlayTeamContextMusic(SelectedTeam(_teamStatsTeamCombo));
            };
            bar.Controls.Add(_teamStatsTeamCombo);
            bar.Controls.Add(new Label { Text = "Season", AutoSize = true, Margin = new Padding(14, 10, 4, 0) });
            _teamStatsSeasonCombo = new ComboBox { Width = 210, DropDownStyle = ComboBoxStyle.DropDownList };
            _teamStatsSeasonCombo.SelectedIndexChanged += (s, e) => RefreshTeamStatsGrid();
            bar.Controls.Add(_teamStatsSeasonCombo);
            AddButton(bar, "Export Excel", (s, e) => ExportGrid(_teamStatsGrid, "Team Stats", ExportFormat.Excel));
            AddButton(bar, "Export Word", (s, e) => ExportGrid(_teamStatsGrid, "Team Stats", ExportFormat.Word));
            root.Controls.Add(bar, 0, 0);

            _teamLeadersLabel = new Label
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(8),
                TextAlign = ContentAlignment.MiddleLeft
            };
            root.Controls.Add(_teamLeadersLabel, 0, 1);

            _teamStatsGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false
            };
            AddGridColumn(_teamStatsGrid, "Season", 82);
            AddGridColumn(_teamStatsGrid, "Team", 190);
            AddGridColumn(_teamStatsGrid, "G", 45);
            AddGridColumn(_teamStatsGrid, "W", 45);
            AddGridColumn(_teamStatsGrid, "L", 45);
            AddGridColumn(_teamStatsGrid, "T", 45);
            AddGridColumn(_teamStatsGrid, "Pct", 60);
            AddGridColumn(_teamStatsGrid, "RS", 52);
            AddGridColumn(_teamStatsGrid, "RA", 52);
            AddGridColumn(_teamStatsGrid, "Diff", 54);
            AddGridColumn(_teamStatsGrid, "AVG", 60);
            AddGridColumn(_teamStatsGrid, "OBP", 60);
            AddGridColumn(_teamStatsGrid, "SLG", 60);
            AddGridColumn(_teamStatsGrid, "OPS", 60);
            AddGridColumn(_teamStatsGrid, "PA", 58);
            AddGridColumn(_teamStatsGrid, "XBH", 52);
            AddGridColumn(_teamStatsGrid, "AB", 58);
            AddGridColumn(_teamStatsGrid, "R", 50);
            AddGridColumn(_teamStatsGrid, "H", 50);
            AddGridColumn(_teamStatsGrid, "2B", 46);
            AddGridColumn(_teamStatsGrid, "3B", 46);
            AddGridColumn(_teamStatsGrid, "HR", 48);
            AddGridColumn(_teamStatsGrid, "RBI", 52);
            AddGridColumn(_teamStatsGrid, "BB", 48);
            AddGridColumn(_teamStatsGrid, "IBB", 48);
            AddGridColumn(_teamStatsGrid, "SO", 48);
            AddGridColumn(_teamStatsGrid, "SB", 48);
            AddGridColumn(_teamStatsGrid, "CS", 48);
            AddGridColumn(_teamStatsGrid, "HBP", 52);
            AddGridColumn(_teamStatsGrid, "SH", 48);
            AddGridColumn(_teamStatsGrid, "SF", 48);
            AddGridColumn(_teamStatsGrid, "GO", 48);
            AddGridColumn(_teamStatsGrid, "FO", 48);
            AddGridColumn(_teamStatsGrid, "POp", 50);
            AddGridColumn(_teamStatsGrid, "GIDP", 54);
            AddGridColumn(_teamStatsGrid, "ROE", 50);
            AddGridColumn(_teamStatsGrid, "IP", 56);
            AddGridColumn(_teamStatsGrid, "ERA", 62);
            AddGridColumn(_teamStatsGrid, "WHIP", 64);
            AddGridColumn(_teamStatsGrid, "RA-P", 54);
            AddGridColumn(_teamStatsGrid, "K", 48);
            AddGridColumn(_teamStatsGrid, "PBB", 52);
            AddGridColumn(_teamStatsGrid, "PIBB", 56);
            AddGridColumn(_teamStatsGrid, "H-A", 54);
            AddGridColumn(_teamStatsGrid, "2B-A", 54);
            AddGridColumn(_teamStatsGrid, "3B-A", 54);
            AddGridColumn(_teamStatsGrid, "HR-A", 56);
            AddGridColumn(_teamStatsGrid, "HB", 48);
            AddGridColumn(_teamStatsGrid, "WP", 48);
            AddGridColumn(_teamStatsGrid, "BK", 48);
            AddGridColumn(_teamStatsGrid, "HLD", 50);
            AddGridColumn(_teamStatsGrid, "BS", 46);
            AddGridColumn(_teamStatsGrid, "CG", 46);
            AddGridColumn(_teamStatsGrid, "SHO", 50);
            AddGridColumn(_teamStatsGrid, "FPCT", 64);
            AddGridColumn(_teamStatsGrid, "Def IP", 58);
            AddGridColumn(_teamStatsGrid, "TC", 50);
            AddGridColumn(_teamStatsGrid, "E", 44);
            AddGridColumn(_teamStatsGrid, "DP", 44);
            AddGridColumn(_teamStatsGrid, "PB", 44);
            AddGridColumn(_teamStatsGrid, "SBA-C", 56);
            AddGridColumn(_teamStatsGrid, "CS-C", 52);
            AddGridColumn(_teamStatsGrid, "CS%", 58);
            AddGridColumn(_teamStatsGrid, "INJ-G", 58);
            AddGridColumn(_teamStatsGrid, "Champion", 86);
            AddGridColumn(_teamStatsGrid, "Season Name", 170);
            root.Controls.Add(_teamStatsGrid, 0, 2);
            tab.Controls.Add(root);
        }

        private void BuildPlayerStatsTab(TabPage tab)
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, Padding = new Padding(8) };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var bar = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
            bar.Controls.Add(new Label { Text = "Scope", AutoSize = true, Margin = new Padding(4, 10, 4, 0) });
            _playerStatsScopeCombo = new ComboBox { Width = 110, DropDownStyle = ComboBoxStyle.DropDownList };
            _playerStatsScopeCombo.Items.AddRange(new object[] { "Season", "Playoffs", "Career", "All-Time" });
            _playerStatsScopeCombo.SelectedIndex = 0;
            _playerStatsScopeCombo.SelectedIndexChanged += (s, e) => RefreshPlayerStatsGrid();
            bar.Controls.Add(_playerStatsScopeCombo);
            bar.Controls.Add(new Label { Text = "Team", AutoSize = true, Margin = new Padding(4, 10, 4, 0) });
            _playerStatsTeamCombo = new ComboBox { Width = 230, DropDownStyle = ComboBoxStyle.DropDownList };
            _playerStatsTeamCombo.SelectedIndexChanged += (s, e) =>
            {
                RefreshPlayerStatsGrid();
                PlayTeamContextMusic(SelectedTeam(_playerStatsTeamCombo));
            };
            bar.Controls.Add(_playerStatsTeamCombo);
            bar.Controls.Add(new Label { Text = "Season", AutoSize = true, Margin = new Padding(14, 10, 4, 0) });
            _playerStatsSeasonCombo = new ComboBox { Width = 210, DropDownStyle = ComboBoxStyle.DropDownList };
            _playerStatsSeasonCombo.SelectedIndexChanged += (s, e) => RefreshPlayerStatsGrid();
            bar.Controls.Add(_playerStatsSeasonCombo);
            AddButton(bar, "Export Excel", (s, e) => ExportGrid(_playerStatsGrid, "Player Stats", ExportFormat.Excel));
            AddButton(bar, "Export Word", (s, e) => ExportGrid(_playerStatsGrid, "Player Stats", ExportFormat.Word));
            root.Controls.Add(bar, 0, 0);

            _playerLeadersLabel = new Label
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(8),
                TextAlign = ContentAlignment.MiddleLeft
            };
            root.Controls.Add(_playerLeadersLabel, 0, 1);

            _playerStatsGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false
            };
            AddGridColumn(_playerStatsGrid, "Player", 170);
            AddGridColumn(_playerStatsGrid, "Role", 70);
            AddGridColumn(_playerStatsGrid, "Class", 94);
            AddGridColumn(_playerStatsGrid, "Pos", 80);
            AddGridColumn(_playerStatsGrid, "Injury", 130);
            AddGridColumn(_playerStatsGrid, "V Years", 58);
            AddGridColumn(_playerStatsGrid, "Call-Up", 62);
            AddGridColumn(_playerStatsGrid, "INJ-G", 58);
            AddGridColumn(_playerStatsGrid, "G", 44);
            AddGridColumn(_playerStatsGrid, "R", 44);
            AddGridColumn(_playerStatsGrid, "PA", 52);
            AddGridColumn(_playerStatsGrid, "XBH", 50);
            AddGridColumn(_playerStatsGrid, "AB", 52);
            AddGridColumn(_playerStatsGrid, "H", 48);
            AddGridColumn(_playerStatsGrid, "2B", 44);
            AddGridColumn(_playerStatsGrid, "3B", 44);
            AddGridColumn(_playerStatsGrid, "HR", 48);
            AddGridColumn(_playerStatsGrid, "RBI", 52);
            AddGridColumn(_playerStatsGrid, "BB", 48);
            AddGridColumn(_playerStatsGrid, "IBB", 48);
            AddGridColumn(_playerStatsGrid, "SO", 48);
            AddGridColumn(_playerStatsGrid, "SB", 48);
            AddGridColumn(_playerStatsGrid, "CS", 48);
            AddGridColumn(_playerStatsGrid, "HBP", 52);
            AddGridColumn(_playerStatsGrid, "SH", 48);
            AddGridColumn(_playerStatsGrid, "SF", 48);
            AddGridColumn(_playerStatsGrid, "GO", 48);
            AddGridColumn(_playerStatsGrid, "FO", 48);
            AddGridColumn(_playerStatsGrid, "POp", 50);
            AddGridColumn(_playerStatsGrid, "GIDP", 54);
            AddGridColumn(_playerStatsGrid, "ROE", 50);
            AddGridColumn(_playerStatsGrid, "AVG", 62);
            AddGridColumn(_playerStatsGrid, "OBP", 62);
            AddGridColumn(_playerStatsGrid, "SLG", 62);
            AddGridColumn(_playerStatsGrid, "OPS", 62);
            AddGridColumn(_playerStatsGrid, "TB", 48);
            AddGridColumn(_playerStatsGrid, "IP", 56);
            AddGridColumn(_playerStatsGrid, "W", 44);
            AddGridColumn(_playerStatsGrid, "L", 44);
            AddGridColumn(_playerStatsGrid, "SV", 44);
            AddGridColumn(_playerStatsGrid, "HLD", 48);
            AddGridColumn(_playerStatsGrid, "BS", 44);
            AddGridColumn(_playerStatsGrid, "CG", 44);
            AddGridColumn(_playerStatsGrid, "SHO", 48);
            AddGridColumn(_playerStatsGrid, "K", 48);
            AddGridColumn(_playerStatsGrid, "ER", 48);
            AddGridColumn(_playerStatsGrid, "RA", 48);
            AddGridColumn(_playerStatsGrid, "ERA", 62);
            AddGridColumn(_playerStatsGrid, "WHIP", 64);
            AddGridColumn(_playerStatsGrid, "H-A", 54);
            AddGridColumn(_playerStatsGrid, "2B-A", 54);
            AddGridColumn(_playerStatsGrid, "3B-A", 54);
            AddGridColumn(_playerStatsGrid, "BB-A", 56);
            AddGridColumn(_playerStatsGrid, "IBB-A", 58);
            AddGridColumn(_playerStatsGrid, "HR-A", 56);
            AddGridColumn(_playerStatsGrid, "HB", 48);
            AddGridColumn(_playerStatsGrid, "WP", 48);
            AddGridColumn(_playerStatsGrid, "BK", 48);
            AddGridColumn(_playerStatsGrid, "BF", 48);
            AddGridColumn(_playerStatsGrid, "PC", 48);
            AddGridColumn(_playerStatsGrid, "FPCT", 64);
            AddGridColumn(_playerStatsGrid, "Def IP", 58);
            AddGridColumn(_playerStatsGrid, "TC", 48);
            AddGridColumn(_playerStatsGrid, "PO", 48);
            AddGridColumn(_playerStatsGrid, "A", 44);
            AddGridColumn(_playerStatsGrid, "E", 44);
            AddGridColumn(_playerStatsGrid, "DP", 44);
            AddGridColumn(_playerStatsGrid, "PB", 44);
            AddGridColumn(_playerStatsGrid, "SBA-C", 56);
            AddGridColumn(_playerStatsGrid, "CS-C", 52);
            AddGridColumn(_playerStatsGrid, "CS%", 58);
            AddGridColumn(_playerStatsGrid, "Season", 160);
            root.Controls.Add(_playerStatsGrid, 0, 2);
            tab.Controls.Add(root);
        }

        private void BuildRecordsBookTab(TabPage tab)
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, Padding = new Padding(8) };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var bar = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
            bar.Controls.Add(new Label { Text = "Level", AutoSize = true, Margin = new Padding(4, 10, 4, 0) });
            _recordsBookLevelCombo = new ComboBox { Width = 130, DropDownStyle = ComboBoxStyle.DropDownList };
            _recordsBookLevelCombo.Items.AddRange(new object[] { "League", "Conference", "Region", "District", "Team" });
            _recordsBookLevelCombo.SelectedIndex = 0;
            _recordsBookLevelCombo.SelectedIndexChanged += (s, e) => RefreshRecordsBookEntityCombo();
            bar.Controls.Add(_recordsBookLevelCombo);

            bar.Controls.Add(new Label { Text = "Book", AutoSize = true, Margin = new Padding(12, 10, 4, 0) });
            _recordsBookEntityCombo = new ComboBox { Width = 260, DropDownStyle = ComboBoxStyle.DropDownList };
            _recordsBookEntityCombo.SelectedIndexChanged += (s, e) => RefreshRecordsBookGrid();
            bar.Controls.Add(_recordsBookEntityCombo);

            bar.Controls.Add(new Label { Text = "Scope", AutoSize = true, Margin = new Padding(12, 10, 4, 0) });
            _recordsBookScopeCombo = new ComboBox { Width = 110, DropDownStyle = ComboBoxStyle.DropDownList };
            _recordsBookScopeCombo.Items.AddRange(new object[] { "All", "Game", "Season", "Career" });
            _recordsBookScopeCombo.SelectedIndex = 0;
            _recordsBookScopeCombo.SelectedIndexChanged += (s, e) => RefreshRecordsBookGrid();
            bar.Controls.Add(_recordsBookScopeCombo);

            AddButton(bar, "Refresh", (s, e) => RefreshRecordsBookGrid());
            AddButton(bar, "Export Excel", (s, e) => ExportGrid(_recordsBookGrid, "Records Book", ExportFormat.Excel));
            AddButton(bar, "Export Word", (s, e) => ExportGrid(_recordsBookGrid, "Records Book", ExportFormat.Word));
            root.Controls.Add(bar, 0, 0);

            root.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = "Records are derived from committed game results. Ties are listed as shared records.",
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(62, 68, 78)
            }, 0, 1);

            _recordsBookGrid = CreateReadOnlyGrid();
            AddGridColumn(_recordsBookGrid, "Level", 92);
            AddGridColumn(_recordsBookGrid, "Book", 190);
            AddGridColumn(_recordsBookGrid, "Scope", 78);
            AddGridColumn(_recordsBookGrid, "Category", 118);
            AddGridColumn(_recordsBookGrid, "Record", 150);
            AddGridColumn(_recordsBookGrid, "Holder", 180);
            AddGridColumn(_recordsBookGrid, "Team", 180);
            AddGridColumn(_recordsBookGrid, "Value", 76);
            AddGridColumn(_recordsBookGrid, "Season", 72);
            AddGridColumn(_recordsBookGrid, "Season Name", 150);
            AddGridColumn(_recordsBookGrid, "Opponent", 170);
            AddGridColumn(_recordsBookGrid, "Date", 122);
            AddGridColumn(_recordsBookGrid, "Game", 230);
            AddGridColumn(_recordsBookGrid, "Detail", 220);
            root.Controls.Add(_recordsBookGrid, 0, 2);
            tab.Controls.Add(root);
        }

        private static DataGridViewTextBoxColumn AddGridColumn(DataGridView grid, string header, int width)
        {
            var column = new DataGridViewTextBoxColumn
            {
                HeaderText = header,
                Width = width,
                SortMode = DataGridViewColumnSortMode.Automatic
            };
            grid.Columns.Add(column);
            return column;
        }

        private static Button AddButton(Control host, string text, EventHandler click)
        {
            var b = new Button { Text = text, AutoSize = true, Margin = new Padding(4) };
            b.Click += click;
            host.Controls.Add(b);
            return b;
        }

        private static Panel ColorPanel()
            => new Panel { Width = 34, Height = 24, BorderStyle = BorderStyle.FixedSingle, Margin = new Padding(4) };

        private void ExportGrid(DataGridView grid, string title, ExportFormat format)
        {
            if (grid == null || grid.Columns.Count == 0)
            {
                MessageBox.Show(this, "There is no table to export.");
                return;
            }

            string ext = format == ExportFormat.Excel ? "xlsx" : "docx";
            string filter = format == ExportFormat.Excel
                ? "Excel workbook (*.xlsx)|*.xlsx"
                : "Word document (*.docx)|*.docx";
            using var dlg = new SaveFileDialog
            {
                Title = "Export " + title,
                Filter = filter + "|HTML table (*.html)|*.html|All files (*.*)|*.*",
                FileName = SanitizeFileName(title) + "." + ext,
                AddExtension = true,
                DefaultExt = ext
            };
            if (dlg.ShowDialog(this) != DialogResult.OK)
                return;

            var sections = new[] { BuildGridExportSection(title, grid) };
            string outputPath = WriteExport(dlg.FileName, title, format, sections, () => BuildGridExportHtml(grid, title, format));
            _status.Text = "Exported " + title + " to " + outputPath;
        }

        private void ExportFullHallOfFame()
        {
            if (_hofDynastyGrid == null)
            {
                MessageBox.Show(this, "There is no Hall of Fame to export.");
                return;
            }

            RefreshHallOfFameViews();
            using var dlg = new SaveFileDialog
            {
                Title = "Export full Hall of Fame",
                Filter = "Word document (*.docx)|*.docx|Excel workbook (*.xlsx)|*.xlsx|HTML report (*.html)|*.html|All files (*.*)|*.*",
                FileName = SanitizeFileName((_league?.Name ?? "Dynasty") + "_Full_Hall_Of_Fame") + ".docx",
                AddExtension = true,
                DefaultExt = "docx"
            };
            if (dlg.ShowDialog(this) != DialogResult.OK)
                return;

            ExportFormat format = dlg.FilterIndex == 2 ? ExportFormat.Excel : ExportFormat.Word;
            var sections = BuildFullHallOfFameExportSections();
            string outputPath = WriteExport(dlg.FileName, (_league?.Name ?? "Dynasty") + " Hall of Fame", format, sections, () => BuildFullHallOfFameExportHtml(format));
            _status.Text = "Exported full Hall of Fame to " + outputPath;
        }

        private static string WriteExport(
            string requestedPath,
            string title,
            ExportFormat requestedFormat,
            IEnumerable<ExportSection> sections,
            Func<string> htmlFactory)
        {
            string outputPath = NormalizeExportPath(requestedPath, requestedFormat);
            if (IsHtmlExport(outputPath))
            {
                File.WriteAllText(outputPath, htmlFactory(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
                return outputPath;
            }

            if (File.Exists(outputPath))
                File.Delete(outputPath);

            ExportFormat format = FormatForPath(outputPath, requestedFormat);
            if (format == ExportFormat.Excel)
                NativeDocumentExporter.WriteXlsx(outputPath, title, sections);
            else
                NativeDocumentExporter.WriteDocx(outputPath, title, sections);
            return outputPath;
        }

        private static string NormalizeExportPath(string path, ExportFormat requestedFormat)
        {
            if (string.IsNullOrWhiteSpace(path))
                return path;

            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".html" || ext == ".htm" || ext == ".xlsx" || ext == ".docx")
                return path;
            if (ext == ".xls")
                return Path.ChangeExtension(path, ".xlsx");
            if (ext == ".doc")
                return Path.ChangeExtension(path, ".docx");

            return Path.ChangeExtension(path, requestedFormat == ExportFormat.Excel ? ".xlsx" : ".docx");
        }

        private static bool IsHtmlExport(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            return ext == ".html" || ext == ".htm";
        }

        private static ExportFormat FormatForPath(string path, ExportFormat fallback)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".xlsx")
                return ExportFormat.Excel;
            if (ext == ".docx")
                return ExportFormat.Word;
            return fallback;
        }

        private static ExportSection BuildGridExportSection(string title, DataGridView grid)
        {
            var section = new ExportSection { Title = title };
            if (grid == null)
                return section;

            var columns = grid.Columns.Cast<DataGridViewColumn>()
                .Where(column => column.Visible)
                .OrderBy(column => column.DisplayIndex)
                .ToList();
            section.Headers = columns.Select(column => column.HeaderText ?? "").ToList();
            section.Rows = grid.Rows.Cast<DataGridViewRow>()
                .Where(row => !row.IsNewRow && row.Visible)
                .Select(row => columns.Select(column => ExportCellText(row.Cells[column.Index].FormattedValue)).ToList())
                .ToList();
            return section;
        }

        private List<ExportSection> BuildFullHallOfFameExportSections()
        {
            string selectedTeam = SelectedTeam(_hofTeamCombo)?.DisplayName ?? "All Teams";
            return new List<ExportSection>
            {
                new ExportSection
                {
                    Title = "Export Details",
                    Headers = new List<string> { "Field", "Value" },
                    Rows = new List<List<string>>
                    {
                        new List<string> { "Dynasty", _league?.Name ?? "Dynasty" },
                        new List<string> { "Selected Team View", selectedTeam }
                    }
                },
                BuildGridExportSection("Dynasty Hall Inductees", _hofDynastyGrid),
                BuildGridExportSection("Selected Team Inductees", _hofTeamGrid),
                BuildGridExportSection("Player Candidates", _hofCandidatesGrid),
                BuildGridExportSection("Coach Candidates", _hofCoachCandidatesGrid),
                BuildGridExportSection("All-Time Records", _hofRecordsGrid)
            };
        }

        private static List<ExportSection> BuildRankingPollsExportSections(IEnumerable<SeasonRankingPoll> polls)
        {
            return (polls ?? Enumerable.Empty<SeasonRankingPoll>())
                .Where(poll => poll != null)
                .Select(poll => new ExportSection
                {
                    Title = poll.Name,
                    Headers = new List<string>
                    {
                        "Rank", "Prev", "Team", "W", "L", "T", "Score", "Poll", "Computer", "Ranked W", "SOS", "Diff", "Notes"
                    },
                    Rows = (poll.Rankings ?? new List<SeasonRankingEntry>())
                        .OrderBy(entry => entry.Rank)
                        .ThenBy(entry => entry.TeamName)
                        .Select(entry => new List<string>
                        {
                            entry.Rank.ToString(),
                            entry.PreviousRank <= 0 ? "NR" : entry.PreviousRank.ToString(),
                            entry.TeamName ?? "",
                            entry.Wins.ToString(),
                            entry.Losses.ToString(),
                            entry.Ties.ToString(),
                            entry.Score.ToString("0.00"),
                            entry.PollScore.ToString("0.00"),
                            entry.ComputerScore.ToString("0.00"),
                            entry.RankedWins.ToString(),
                            entry.StrengthOfSchedule.ToString("0.000"),
                            entry.RunDifferential.ToString(),
                            entry.Notes ?? ""
                        })
                        .ToList()
                })
                .ToList();
        }

        private static string ExportCellText(object value)
            => value is Image ? "[image]" : Convert.ToString(value) ?? "";

        private static string BuildGridExportHtml(DataGridView grid, string title, ExportFormat format)
        {
            var html = new StringBuilder();
            html.AppendLine("<html>");
            html.AppendLine("<head>");
            html.AppendLine("<meta http-equiv=\"Content-Type\" content=\"text/html; charset=utf-8\" />");
            html.AppendLine("<style>");
            html.AppendLine("body{font-family:Segoe UI,Arial,sans-serif;font-size:10pt;}");
            html.AppendLine("h1{font-size:18pt;margin-bottom:14px;}");
            html.AppendLine("table{border-collapse:collapse;}");
            html.AppendLine("th{background:#173f8a;color:white;font-weight:bold;}");
            html.AppendLine("th,td{border:1px solid #999;padding:4px 7px;white-space:nowrap;}");
            html.AppendLine("td.num{text-align:right;}");
            html.AppendLine("</style>");
            html.AppendLine("</head>");
            html.AppendLine("<body>");
            html.AppendLine("<h1>" + Html(title) + "</h1>");
            AppendGridExportTable(html, grid);
            html.AppendLine("</body>");
            html.AppendLine("</html>");
            return html.ToString();
        }

        private void ExportSelectedRankingPoll(ExportFormat format)
        {
            var season = SelectedSeason(_rankingSeasonCombo);
            var poll = SelectedRankingPoll();
            if (season == null || poll == null)
            {
                MessageBox.Show(this, "Select or generate a poll first.");
                return;
            }

            ExportRankingPolls(new[] { poll }, season.Name + " " + poll.Name, format, allPolls: false);
        }

        private void ExportAllRankingPolls()
        {
            var season = SelectedSeason(_rankingSeasonCombo);
            var polls = (season?.RankingPolls ?? new List<SeasonRankingPoll>())
                .OrderBy(p => RankingPollSortOrder(p.Type))
                .ThenBy(p => p.Week)
                .ThenBy(p => p.CreatedAt)
                .ToList();
            if (season == null || polls.Count == 0)
            {
                MessageBox.Show(this, "There are no saved polls to export.");
                return;
            }

            using var dlg = new SaveFileDialog
            {
                Title = "Export all polls",
                Filter = "Excel workbook (*.xlsx)|*.xlsx|Word document (*.docx)|*.docx|HTML report (*.html)|*.html|All files (*.*)|*.*",
                FileName = SanitizeFileName(season.Name + "_All_Polls") + ".xlsx",
                AddExtension = true,
                DefaultExt = "xlsx"
            };
            if (dlg.ShowDialog(this) != DialogResult.OK)
                return;

            ExportFormat format = dlg.FilterIndex == 2 ? ExportFormat.Word : ExportFormat.Excel;
            string title = season.Name + " All Polls";
            string outputPath = WriteExport(dlg.FileName, title, format, BuildRankingPollsExportSections(polls), () => BuildRankingPollsExportHtml(polls, title, format));
            _status.Text = "Exported all polls to " + outputPath;
        }

        private void ExportRankingPolls(IEnumerable<SeasonRankingPoll> polls, string title, ExportFormat format, bool allPolls)
        {
            var list = polls?.Where(p => p != null).ToList() ?? new List<SeasonRankingPoll>();
            if (list.Count == 0)
            {
                MessageBox.Show(this, "There are no polls to export.");
                return;
            }

            string ext = format == ExportFormat.Excel ? "xlsx" : "docx";
            string filter = format == ExportFormat.Excel
                ? "Excel workbook (*.xlsx)|*.xlsx"
                : "Word document (*.docx)|*.docx";
            using var dlg = new SaveFileDialog
            {
                Title = "Export " + title,
                Filter = filter + "|HTML report (*.html)|*.html|All files (*.*)|*.*",
                FileName = SanitizeFileName(title) + "." + ext,
                AddExtension = true,
                DefaultExt = ext
            };
            if (dlg.ShowDialog(this) != DialogResult.OK)
                return;

            string outputPath = WriteExport(dlg.FileName, title, format, BuildRankingPollsExportSections(list), () => BuildRankingPollsExportHtml(list, title, format));
            _status.Text = "Exported " + (allPolls ? "all polls" : title) + " to " + outputPath;
        }

        private static string BuildRankingPollsExportHtml(IEnumerable<SeasonRankingPoll> polls, string title, ExportFormat format)
        {
            var html = new StringBuilder();
            html.AppendLine("<html>");
            html.AppendLine("<head>");
            html.AppendLine("<meta http-equiv=\"Content-Type\" content=\"text/html; charset=utf-8\" />");
            html.AppendLine("<style>");
            html.AppendLine("body{font-family:Segoe UI,Arial,sans-serif;font-size:10pt;color:#1f2933;}");
            html.AppendLine("h1{font-size:20pt;margin-bottom:8px;}");
            html.AppendLine("h2{font-size:15pt;margin:20px 0 8px 0;color:#173f8a;}");
            html.AppendLine("table{border-collapse:collapse;margin-bottom:18px;}");
            html.AppendLine("th{background:#173f8a;color:white;font-weight:bold;}");
            html.AppendLine("th,td{border:1px solid #999;padding:4px 7px;white-space:nowrap;}");
            html.AppendLine("td.num{text-align:right;}");
            html.AppendLine("</style>");
            html.AppendLine("</head>");
            html.AppendLine("<body>");
            html.AppendLine("<h1>" + Html(title) + "</h1>");
            foreach (var poll in polls)
            {
                html.AppendLine("<h2>" + Html(poll.Name) + "</h2>");
                html.AppendLine("<table>");
                html.AppendLine("<tr><th>Rank</th><th>Prev</th><th>Team</th><th>W</th><th>L</th><th>T</th><th>Score</th><th>Poll</th><th>Computer</th><th>Ranked W</th><th>SOS</th><th>Diff</th><th>Notes</th></tr>");
                foreach (var entry in (poll.Rankings ?? new List<SeasonRankingEntry>()).OrderBy(r => r.Rank).ThenBy(r => r.TeamName))
                {
                    html.AppendLine("<tr>" +
                        "<td class=\"num\">" + entry.Rank + "</td>" +
                        "<td class=\"num\">" + (entry.PreviousRank <= 0 ? "NR" : entry.PreviousRank.ToString()) + "</td>" +
                        "<td>" + Html(entry.TeamName) + "</td>" +
                        "<td class=\"num\">" + entry.Wins + "</td>" +
                        "<td class=\"num\">" + entry.Losses + "</td>" +
                        "<td class=\"num\">" + entry.Ties + "</td>" +
                        "<td class=\"num\">" + entry.Score.ToString("0.00") + "</td>" +
                        "<td class=\"num\">" + entry.PollScore.ToString("0.00") + "</td>" +
                        "<td class=\"num\">" + entry.ComputerScore.ToString("0.00") + "</td>" +
                        "<td class=\"num\">" + entry.RankedWins + "</td>" +
                        "<td class=\"num\">" + entry.StrengthOfSchedule.ToString("0.000") + "</td>" +
                        "<td class=\"num\">" + entry.RunDifferential + "</td>" +
                        "<td>" + Html(entry.Notes) + "</td>" +
                        "</tr>");
                }
                html.AppendLine("</table>");
            }
            html.AppendLine("</body>");
            html.AppendLine("</html>");
            return html.ToString();
        }

        private string BuildFullHallOfFameExportHtml(ExportFormat format)
        {
            var html = new StringBuilder();
            string selectedTeam = SelectedTeam(_hofTeamCombo)?.DisplayName ?? "All Teams";
            html.AppendLine("<html>");
            html.AppendLine("<head>");
            html.AppendLine("<meta http-equiv=\"Content-Type\" content=\"text/html; charset=utf-8\" />");
            html.AppendLine("<style>");
            html.AppendLine("body{font-family:Segoe UI,Arial,sans-serif;font-size:10pt;color:#1f2933;}");
            html.AppendLine("h1{font-size:22pt;margin-bottom:4px;}");
            html.AppendLine("h2{font-size:15pt;margin:22px 0 8px 0;color:#173f8a;}");
            html.AppendLine("p.meta{margin:0 0 16px 0;color:#4b5563;}");
            html.AppendLine("table{border-collapse:collapse;margin-bottom:18px;}");
            html.AppendLine("th{background:#173f8a;color:white;font-weight:bold;}");
            html.AppendLine("th,td{border:1px solid #999;padding:4px 7px;white-space:nowrap;}");
            html.AppendLine("td.num{text-align:right;}");
            html.AppendLine("</style>");
            html.AppendLine("</head>");
            html.AppendLine("<body>");
            html.AppendLine("<h1>" + Html((_league?.Name ?? "Dynasty") + " Hall of Fame") + "</h1>");
            html.AppendLine("<p class=\"meta\">Selected team view: " + Html(selectedTeam) + "</p>");
            AppendGridExportSection(html, "Dynasty Hall Inductees", _hofDynastyGrid);
            AppendGridExportSection(html, "Selected Team Inductees", _hofTeamGrid);
            AppendGridExportSection(html, "Player Candidates", _hofCandidatesGrid);
            AppendGridExportSection(html, "Coach Candidates", _hofCoachCandidatesGrid);
            AppendGridExportSection(html, "All-Time Records", _hofRecordsGrid);
            html.AppendLine("</body>");
            html.AppendLine("</html>");
            return html.ToString();
        }

        private static void AppendGridExportSection(StringBuilder html, string title, DataGridView grid)
        {
            html.AppendLine("<h2>" + Html(title) + "</h2>");
            AppendGridExportTable(html, grid);
        }

        private static void AppendGridExportTable(StringBuilder html, DataGridView grid)
        {
            if (html == null || grid == null)
                return;

            html.AppendLine("<table>");
            html.AppendLine("<tr>");
            foreach (DataGridViewColumn column in grid.Columns.Cast<DataGridViewColumn>().Where(c => c.Visible))
                html.Append("<th>").Append(Html(column.HeaderText)).AppendLine("</th>");
            html.AppendLine("</tr>");

            foreach (DataGridViewRow row in grid.Rows.Cast<DataGridViewRow>().Where(r => !r.IsNewRow && r.Visible))
            {
                html.AppendLine("<tr>");
                foreach (DataGridViewColumn column in grid.Columns.Cast<DataGridViewColumn>().Where(c => c.Visible))
                {
                    object value = row.Cells[column.Index].FormattedValue;
                    string text = value is Image ? "[image]" : Convert.ToString(value) ?? "";
                    bool numeric = double.TryParse(text, out _);
                    html.Append(numeric ? "<td class=\"num\">" : "<td>")
                        .Append(Html(text))
                        .AppendLine("</td>");
                }
                html.AppendLine("</tr>");
            }

            html.AppendLine("</table>");
        }

        private static string Html(string value)
            => System.Net.WebUtility.HtmlEncode(value ?? "");

        private string RosterExportTitle()
        {
            var team = SelectedTeam();
            return (team?.DisplayName ?? "Team") + " Roster";
        }

        private DataGridView CurrentAllStarGrid()
            => CurrentGridFromTabs(_allStarTabs) ?? _allStarGameStatsGrid;

        private string CurrentAllStarExportTitle()
            => "All-Star " + (_allStarTabs?.SelectedTab?.Text ?? "Stats");

        private DataGridView CurrentAwardGrid()
            => CurrentGridFromTabs(_awardTabs) ?? _awardRacesGrid;

        private string CurrentAwardExportTitle()
            => "Awards " + (_awardTabs?.SelectedTab?.Text ?? "Award Races");

        private DataGridView CurrentHallOfFameGrid()
            => CurrentGridFromTabs(_hofTabs) ?? _hofDynastyGrid;

        private string CurrentHallOfFameExportTitle()
            => "Hall Of Fame " + (_hofTabs?.SelectedTab?.Text ?? "Dynasty Hall");

        private static DataGridView CurrentGridFromTabs(TabControl tabs)
            => tabs?.SelectedTab?.Controls.OfType<DataGridView>().FirstOrDefault();

        private LeagueFile CreateStarterLeague(LeagueRules? rules = null, string? name = null, string? ownerFullName = null)
        {
            var league = new LeagueFile
            {
                Name = string.IsNullOrWhiteSpace(name) ? "New Baseball Universe" : name.Trim(),
                OwnerFullName = (ownerFullName ?? "").Trim(),
                Rules = rules ?? new LeagueRules()
            };
            for (int i = 1; i <= 4; i++)
            {
                var team = new Team { City = "City " + i, Nickname = "Club " + i, ScoreboardAbbreviation = "C" + i };
                team.PrimaryArgb = Color.FromArgb(255, 40 + i * 30, 80 + i * 25, 170 - i * 20).ToArgb();
                team.SecondaryArgb = Color.FromArgb(255, 230 - i * 15, 190, 70 + i * 20).ToArgb();
                Simulator.FillRandomRoster(team, _rng);
                EnsureTeamBaseLineup(team, recalculate: true);
                league.Teams.Add(team);
            }
            var season = new Season { Year = DateTime.Now.Year, Name = DateTime.Now.Year + " Season" };
            league.Seasons.Add(season);
            PlayoffEngine.EnsureDefaultStructure(league);
            if (league.Rules?.Schedule?.HasAnyGames == true)
            {
                season.Schedule = ScheduleGenerator.Generate(league, league.Rules.Schedule, out string error);
                if (error != null)
                    season.Schedule.Clear();
            }
            return league;
        }

        private void RefreshAll()
        {
            if (_lastGame == null)
            {
                _scoreboardPhotoTimer.Enabled = false;
                _scoreboardPhotoPaths.Clear();
                _scoreboardPhotoIndex = 0;
            }
            RefreshTeamList();
            RefreshTeamCombos();
            RefreshFieldPresetCombo();
            RefreshSeasonCombos();
            RefreshScheduleCombo();
            RefreshControlTeamCombo();
            RefreshGameUniformCombos();
            RefreshRulesControls();
            RefreshStructureTree();
            RefreshSeasonViews();
            RefreshRankingPollCombo();
            RefreshRankingGrid();
            RefreshHallOfFameViews();
            RefreshDynastyGrid();
            RefreshInboxGrid();
            RefreshRecordsBookEntityCombo();
            RefreshHierarchyStatistics();
            Text = "Dan's RBI Baseball 2026" + (_dirty ? " *" : "");
        }

        private void RefreshRulesControls()
        {
            if (_inningsCombo == null) return;

            _league.Rules ??= new LeagueRules();
            int innings = ClampGameInnings(_league.Rules.Innings);
            _suppress = true;
            _inningsCombo.SelectedItem = innings;
            if (_mercyRuleBox != null)
                _mercyRuleBox.Checked = _league.Rules.MercyRuleEnabled;
            if (_extraInningsBox != null)
                _extraInningsBox.Checked = _league.Rules.ExtraInnings;
            if (_extraRunnerBox != null)
            {
                _extraRunnerBox.Checked = _league.Rules.ExtraInningRunnerOnSecond;
                _extraRunnerBox.Enabled = _league.Rules.ExtraInnings;
            }
            _suppress = false;
        }

        private void RefreshInboxGrid()
        {
            if (_inboxGrid == null)
                return;

            EnsureInbox();
            Guid? selectedId = (_inboxGrid.CurrentRow?.Tag as CoachInboxMessage)?.Id;
            _inboxGrid.Rows.Clear();

            var messages = FilteredInboxMessages()
                .OrderBy(m => m.IsRead)
                .ThenByDescending(m => m.CreatedAt)
                .ToList();

            foreach (var message in messages)
            {
                var team = TeamById(message.TeamId);
                int row = _inboxGrid.Rows.Add(
                    message.IsRead ? "Read" : "Unread",
                    message.CreatedAt.ToString("g"),
                    string.IsNullOrWhiteSpace(message.To) ? "Coach" : message.To,
                    team?.DisplayName ?? "",
                    message.Category,
                    message.Subject);
                _inboxGrid.Rows[row].Tag = message;
                if (!message.IsRead)
                    _inboxGrid.Rows[row].DefaultCellStyle.Font = new Font(_inboxGrid.Font, FontStyle.Bold);
                if (message.Important)
                    _inboxGrid.Rows[row].DefaultCellStyle.ForeColor = Color.FromArgb(148, 58, 24);
            }

            if (selectedId.HasValue)
            {
                foreach (DataGridViewRow row in _inboxGrid.Rows)
                {
                    if ((row.Tag as CoachInboxMessage)?.Id == selectedId.Value)
                    {
                        row.Selected = true;
                        _inboxGrid.CurrentCell = row.Cells[0];
                        break;
                    }
                }
            }

            ShowSelectedInboxMessage();
            UpdateInboxSummary();
        }

        private IEnumerable<CoachInboxMessage> FilteredInboxMessages()
        {
            EnsureInbox();
            IEnumerable<CoachInboxMessage> messages = _league.InboxMessages;
            string filter = Convert.ToString(_inboxFilterCombo?.SelectedItem) ?? "All";
            if (filter.Equals("Unread", StringComparison.OrdinalIgnoreCase))
                messages = messages.Where(m => !m.IsRead);
            else if (filter.Equals("Scouting Reports", StringComparison.OrdinalIgnoreCase))
                messages = messages.Where(m => string.Equals(m.Category, "Scouting Report", StringComparison.OrdinalIgnoreCase));
            else if (filter.Equals("Game Reports", StringComparison.OrdinalIgnoreCase))
                messages = messages.Where(m => string.Equals(m.Category, "Game Report", StringComparison.OrdinalIgnoreCase));
            else if (filter.Equals("Important", StringComparison.OrdinalIgnoreCase))
                messages = messages.Where(m => m.Important);
            return messages;
        }

        private void ShowSelectedInboxMessage()
        {
            if (_inboxBodyBox == null)
                return;
            var message = SelectedInboxMessage();
            if (message == null)
            {
                _inboxBodyBox.Text = "";
                return;
            }

            var body = new StringBuilder();
            body.AppendLine("From: " + message.From);
            body.AppendLine("To: " + message.To);
            body.AppendLine("Date: " + message.CreatedAt.ToString("f"));
            body.AppendLine("Category: " + message.Category);
            body.AppendLine("Subject: " + message.Subject);
            body.AppendLine(new string('-', 72));
            body.AppendLine(message.Body ?? "");
            _inboxBodyBox.Text = body.ToString();
        }

        private CoachInboxMessage SelectedInboxMessage()
            => _inboxGrid?.CurrentRow?.Tag as CoachInboxMessage;

        private void MarkSelectedInboxRead()
        {
            var message = SelectedInboxMessage();
            if (message == null)
                return;
            if (!message.IsRead)
            {
                message.IsRead = true;
                MarkDirty();
            }
            RefreshInboxGrid();
        }

        private void MarkAllInboxRead()
        {
            EnsureInbox();
            bool changed = false;
            foreach (var message in _league.InboxMessages)
            {
                if (message.IsRead)
                    continue;
                message.IsRead = true;
                changed = true;
            }
            if (changed)
                MarkDirty();
            RefreshInboxGrid();
        }

        private void DeleteSelectedInboxMessage()
        {
            var message = SelectedInboxMessage();
            if (message == null)
                return;
            if (MessageBox.Show(this, "Delete this inbox message?", "Delete message", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            _league.InboxMessages.RemoveAll(m => m.Id == message.Id);
            MarkDirty();
            RefreshInboxGrid();
        }

        private void UpdateInboxSummary()
        {
            EnsureInbox();
            int total = _league.InboxMessages.Count;
            int unread = _league.InboxMessages.Count(m => !m.IsRead);
            if (_inboxSummaryLabel != null)
                _inboxSummaryLabel.Text = unread + " unread message(s), " + total + " total. Game reports are generated when results are committed.";
            if (_tabs != null)
            {
                foreach (TabPage page in _tabs.TabPages)
                {
                    if (page.Text.StartsWith("Inbox", StringComparison.OrdinalIgnoreCase))
                    {
                        page.Text = unread > 0 ? "Inbox (" + unread + ")" : "Inbox";
                        break;
                    }
                }
            }
        }

        private void EnsureInbox()
        {
            if (_league != null)
                _league.InboxMessages ??= new List<CoachInboxMessage>();
        }

        private void RefreshRecordsBookEntityCombo()
        {
            if (_recordsBookEntityCombo == null || _recordsBookLevelCombo == null)
                return;

            var previous = (_recordsBookEntityCombo.SelectedItem as RecordsBookEntityItem)?.Id;
            string level = Convert.ToString(_recordsBookLevelCombo.SelectedItem) ?? "League";
            _recordsBookEntityCombo.Items.Clear();
            foreach (var entity in RecordsBookEngine.EntitiesForLevel(_league, level))
            {
                _recordsBookEntityCombo.Items.Add(new RecordsBookEntityItem
                {
                    Id = entity.Id,
                    Text = entity.Name
                });
            }

            if (_recordsBookEntityCombo.Items.Count == 0)
            {
                RefreshRecordsBookGrid();
                return;
            }

            int index = 0;
            if (previous.HasValue)
            {
                for (int i = 0; i < _recordsBookEntityCombo.Items.Count; i++)
                {
                    if ((_recordsBookEntityCombo.Items[i] as RecordsBookEntityItem)?.Id == previous.Value)
                    {
                        index = i;
                        break;
                    }
                }
            }
            _recordsBookEntityCombo.SelectedIndex = index;
            RefreshRecordsBookGrid();
        }

        private void RefreshRecordsBookGrid()
        {
            if (_recordsBookGrid == null)
                return;

            _recordsBookGrid.Rows.Clear();
            string level = Convert.ToString(_recordsBookLevelCombo?.SelectedItem) ?? "League";
            string scope = Convert.ToString(_recordsBookScopeCombo?.SelectedItem) ?? "All";
            Guid? entityId = (_recordsBookEntityCombo?.SelectedItem as RecordsBookEntityItem)?.Id;
            var entries = RecordsBookEngine.Build(_league, level, entityId, scope);
            foreach (var entry in entries)
            {
                _recordsBookGrid.Rows.Add(
                    entry.Level,
                    entry.LevelName,
                    entry.Scope,
                    entry.Category,
                    entry.Record,
                    entry.Holder,
                    entry.Team,
                    entry.Value,
                    entry.SeasonNumber <= 0 ? "" : entry.SeasonNumber.ToString(),
                    entry.SeasonName,
                    entry.Opponent,
                    entry.Date.HasValue ? entry.Date.Value.ToString("g") : "",
                    entry.Game,
                    entry.Detail);
            }
        }

        private void UpdateGameInnings()
        {
            if (_suppress || _inningsCombo?.SelectedItem == null) return;

            _league.Rules ??= new LeagueRules();
            int innings = ClampGameInnings(Convert.ToInt32(_inningsCombo.SelectedItem));
            if (_league.Rules.Innings == innings) return;

            _league.Rules.Innings = innings;
            MarkDirty();
            _status.Text = "Game length set to " + innings + " innings.";
        }

        private void UpdateGameRules()
        {
            if (_suppress) return;

            _league.Rules ??= new LeagueRules();
            bool mercy = _mercyRuleBox?.Checked ?? _league.Rules.MercyRuleEnabled;
            bool extras = _extraInningsBox?.Checked ?? _league.Rules.ExtraInnings;
            bool runner = extras && (_extraRunnerBox?.Checked ?? _league.Rules.ExtraInningRunnerOnSecond);

            if (_extraRunnerBox != null)
                _extraRunnerBox.Enabled = extras;
            if (!extras && _extraRunnerBox?.Checked == true)
            {
                _suppress = true;
                _extraRunnerBox.Checked = false;
                _suppress = false;
            }

            if (_league.Rules.MercyRuleEnabled == mercy
                && _league.Rules.ExtraInnings == extras
                && _league.Rules.ExtraInningRunnerOnSecond == runner)
            {
                return;
            }

            _league.Rules.MercyRuleEnabled = mercy;
            _league.Rules.ExtraInnings = extras;
            _league.Rules.ExtraInningRunnerOnSecond = runner;
            MarkDirty();
            _status.Text = "Game rules updated.";
        }

        private static int ClampGameInnings(int innings)
            => Math.Clamp(innings, 5, 9);

        private void RefreshStructureTree()
        {
            if (_structureTree == null) return;
            _structureTree.BeginUpdate();
            _structureTree.Nodes.Clear();
            PlayoffEngine.EnsureDefaultStructure(_league);

            foreach (var conf in _league.Structure.Conferences)
            {
                var confNode = new TreeNode(conf.Name) { Tag = conf };
                foreach (var region in conf.Regions)
                {
                    var regionNode = new TreeNode(region.Name) { Tag = region };
                    foreach (var district in region.Districts)
                    {
                        var teams = district.TeamIds
                            .Select(TeamById)
                            .Where(t => t != null)
                            .Select(t => t.ScoreboardName)
                            .ToList();
                        var districtNode = new TreeNode(district.Name + "  (" + teams.Count + " teams)")
                        {
                            Tag = district
                        };
                        if (teams.Count > 0)
                            districtNode.Nodes.Add(new TreeNode(string.Join(", ", teams)));
                        regionNode.Nodes.Add(districtNode);
                    }
                    confNode.Nodes.Add(regionNode);
                }
                _structureTree.Nodes.Add(confNode);
            }

            _structureTree.ExpandAll();
            _structureTree.EndUpdate();
        }

        private void AddConference()
        {
            _league.Structure ??= new LeagueStructure();
            _league.Structure.Conferences ??= new List<Conference>();
            _league.Structure.Conferences.Add(PlayoffEngine.CreateConference(_league.Structure.Conferences.Count + 1));
            PlayoffEngine.EnsureDefaultStructure(_league);
            MarkDirty();
            RefreshAll();
        }

        private void RemoveSelectedConference()
        {
            var conf = _structureTree?.SelectedNode?.Tag as Conference;
            if (conf == null)
            {
                MessageBox.Show(this, "Select a conference node first.");
                return;
            }
            if (MessageBox.Show(this, "Remove " + conf.Name + "? Its teams will be reassigned across the remaining districts.",
                    "Remove conference", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK) return;

            _league.Structure.Conferences.Remove(conf);
            PlayoffEngine.EnsureDefaultStructure(_league);
            MarkDirty();
            RefreshAll();
        }

        private void NormalizeStructure()
        {
            PlayoffEngine.EnsureDefaultStructure(_league);
            MarkDirty();
            RefreshAll();
        }

        private void RefreshTeamList()
        {
            var selected = SelectedTeam()?.Id;
            _teamList.BeginUpdate();
            _teamList.Items.Clear();
            foreach (var team in _league.Teams)
                _teamList.Items.Add(new TeamItem { Team = team });
            _teamList.EndUpdate();
            int idx = selected.HasValue ? _league.Teams.FindIndex(t => t.Id == selected.Value) : 0;
            if (_teamList.Items.Count > 0) _teamList.SelectedIndex = Math.Max(0, idx);
        }

        private void RefreshTeamCombos()
        {
            FillTeamCombo(_awayCombo);
            FillTeamCombo(_homeCombo);
            FillTeamCombo(_teamStatsTeamCombo, true);
            FillTeamCombo(_playerStatsTeamCombo, true);
            FillTeamCombo(_hofTeamCombo, true);
            if (_homeCombo.Items.Count > 1) _homeCombo.SelectedIndex = 1;
            RefreshControlTeamCombo();
        }

        private void FillTeamCombo(ComboBox combo, bool includeAll = false)
        {
            if (combo == null) return;
            combo.Items.Clear();
            if (includeAll)
                combo.Items.Add(new TeamItem { Text = "All Teams" });
            foreach (var team in _league.Teams)
                combo.Items.Add(new TeamItem { Team = team });
            if (combo.Items.Count > 0) combo.SelectedIndex = 0;
        }

        private void RefreshSeasonCombos()
        {
            FillSeasonCombo(_seasonCombo);
            FillSeasonCombo(_commitSeasonCombo);
            FillSeasonCombo(_rankingSeasonCombo);
            FillSeasonCombo(_allStarSeasonCombo);
            FillSeasonCombo(_awardSeasonCombo);
            FillSeasonCombo(_teamStatsSeasonCombo, true);
            FillSeasonCombo(_playerStatsSeasonCombo, true);
        }

        private void FillSeasonCombo(ComboBox combo, bool includeAll = false)
        {
            if (combo == null) return;
            combo.Items.Clear();
            if (includeAll)
                combo.Items.Add(new SeasonItem { Text = "All Seasons" });
            foreach (var season in _league.Seasons)
                combo.Items.Add(new SeasonItem { Season = season });
            if (combo.Items.Count > 0) combo.SelectedIndex = 0;
        }

        private void RefreshRankingPollCombo()
        {
            if (_rankingPollCombo == null)
                return;

            var selectedId = (_rankingPollCombo.SelectedItem as RankingPollItem)?.Poll?.Id;
            _rankingPollCombo.Items.Clear();
            var season = SelectedSeason(_rankingSeasonCombo);
            foreach (var poll in (season?.RankingPolls ?? new List<SeasonRankingPoll>())
                .OrderBy(p => RankingPollSortOrder(p.Type))
                .ThenBy(p => p.Week)
                .ThenBy(p => p.CreatedAt))
            {
                _rankingPollCombo.Items.Add(new RankingPollItem
                {
                    Poll = poll,
                    Text = poll.Name + " (" + poll.CreatedAt.ToString("g") + ")"
                });
            }

            if (_rankingPollCombo.Items.Count == 0)
                return;

            int index = 0;
            if (selectedId.HasValue)
            {
                for (int i = 0; i < _rankingPollCombo.Items.Count; i++)
                {
                    if (_rankingPollCombo.Items[i] is RankingPollItem item && item.Poll?.Id == selectedId.Value)
                    {
                        index = i;
                        break;
                    }
                }
            }
            _rankingPollCombo.SelectedIndex = index;
        }

        private static int RankingPollSortOrder(RankingPollType type)
        {
            return type switch
            {
                RankingPollType.PreSeason => 0,
                RankingPollType.Weekly => 1,
                RankingPollType.Final => 2,
                _ => 0
            };
        }

        private void GenerateRankingPoll(RankingPollType type)
        {
            var season = SelectedSeason(_rankingSeasonCombo) ?? SelectedSeason(_seasonCombo);
            if (season == null)
            {
                MessageBox.Show(this, "Select a season first.");
                return;
            }

            int week = type == RankingPollType.Weekly ? CurrentCompletedWeek(season) : 0;
            var poll = RankingEngine.GeneratePoll(_league, season, type, week);
            RankingEngine.SavePoll(season, poll);
            MarkDirty();
            RefreshRankingPollCombo();
            SelectRankingPoll(poll.Id);
            RefreshRankingGrid();
            _status.Text = "Generated " + poll.Name + ".";
        }

        private int CurrentCompletedWeek(Season season)
        {
            var scheduleById = (season?.Schedule ?? new List<ScheduledGame>()).ToDictionary(g => g.Id);
            int week = (season?.Games ?? new List<GameResult>())
                .Where(g => !g.IsPlayoff &&
                    g.ScheduledGameId.HasValue &&
                    scheduleById.TryGetValue(g.ScheduledGameId.Value, out _))
                .Select(g => scheduleById[g.ScheduledGameId.GetValueOrDefault()].Week)
                .DefaultIfEmpty(1)
                .Max();
            return Math.Max(1, week);
        }

        private void SelectRankingPoll(Guid pollId)
        {
            if (_rankingPollCombo == null)
                return;
            for (int i = 0; i < _rankingPollCombo.Items.Count; i++)
            {
                if ((_rankingPollCombo.Items[i] as RankingPollItem)?.Poll?.Id == pollId)
                {
                    _rankingPollCombo.SelectedIndex = i;
                    return;
                }
            }
        }

        private SeasonRankingPoll SelectedRankingPoll()
            => (_rankingPollCombo?.SelectedItem as RankingPollItem)?.Poll ?? RankingEngine.LatestPoll(SelectedSeason(_rankingSeasonCombo));

        private void RefreshRankingGrid()
        {
            if (_rankingGrid == null)
                return;

            _rankingGrid.Rows.Clear();
            if (_rankingSummaryLabel != null)
                _rankingSummaryLabel.Text = "";

            var season = SelectedSeason(_rankingSeasonCombo);
            var poll = SelectedRankingPoll();
            if (season == null || poll == null)
            {
                if (_rankingSummaryLabel != null)
                    _rankingSummaryLabel.Text = "Generate a Pre-Season, Weekly, or Final Poll to create the Official Top 25.";
                return;
            }

            int officialCount = RankingEngine.OfficialCount(_league);
            foreach (var entry in (poll.Rankings ?? new List<SeasonRankingEntry>())
                .OrderBy(r => r.Rank)
                .Where(r => r.Rank <= officialCount))
            {
                string previous = entry.PreviousRank <= 0 ? "NR" : entry.PreviousRank.ToString();
                _rankingGrid.Rows.Add(
                    entry.Rank,
                    previous,
                    entry.TeamName,
                    entry.Wins,
                    entry.Losses,
                    entry.Ties,
                    entry.Score.ToString("0.00"),
                    entry.PollScore.ToString("0.00"),
                    entry.ComputerScore.ToString("0.00"),
                    entry.RankedWins,
                    entry.StrengthOfSchedule.ToString("0.000"),
                    entry.RunDifferential,
                    entry.Notes);
            }

            if (_rankingSummaryLabel != null)
                _rankingSummaryLabel.Text = poll.Name + ": Official Top " + officialCount + " shown; all " +
                    (poll.Rankings?.Count ?? 0) + " teams are ranked and saved for playoff seeding.";
        }

        private void RefreshScheduleCombo()
        {
            if (_scheduledGameCombo == null) return;

            var selectedId = (_scheduledGameCombo.SelectedItem as ScheduledGameItem)?.Game?.Id;
            _scheduledGameCombo.Items.Clear();
            var season = SelectedSeason(_commitSeasonCombo) ?? SelectedSeason(_seasonCombo);
            if (season?.Schedule != null)
            {
                foreach (var game in season.Schedule.Where(g => !g.PlayedGameId.HasValue).OrderBy(g => g.GameNumber).ThenBy(g => g.Week))
                {
                    _scheduledGameCombo.Items.Add(new ScheduledGameItem
                    {
                        Game = game,
                        Text = ScheduleLabel(game)
                    });
                }
            }

            if (_scheduledGameCombo.Items.Count == 0)
                return;

            int idx = 0;
            if (selectedId.HasValue)
            {
                for (int i = 0; i < _scheduledGameCombo.Items.Count; i++)
                {
                    if (_scheduledGameCombo.Items[i] is ScheduledGameItem item && item.Game?.Id == selectedId.Value)
                    {
                        idx = i;
                        break;
                    }
                }
            }
            _scheduledGameCombo.SelectedIndex = idx;
            RefreshControlTeamCombo();
            RefreshGameUniformCombos();
        }

        private void LoadScheduledGameSelection()
        {
            var scheduled = SelectedScheduledGame();
            if (scheduled == null) return;
            SelectTeamCombo(_awayCombo, scheduled.AwayTeamId);
            SelectTeamCombo(_homeCombo, scheduled.HomeTeamId);
            ApplyHomeTeamFieldSelection();
            RefreshControlTeamCombo();
            RefreshGameUniformCombos();
        }

        private ScheduledGame SelectedScheduledGame()
            => (_scheduledGameCombo?.SelectedItem as ScheduledGameItem)?.Game;

        private void RefreshGameUniformCombos()
        {
            if (_awayUniformCombo == null || _homeUniformCombo == null)
                return;

            var scheduled = SelectedScheduledGame();
            var away = scheduled == null ? SelectedTeam(_awayCombo) : TeamById(scheduled.AwayTeamId);
            var home = scheduled == null ? SelectedTeam(_homeCombo) : TeamById(scheduled.HomeTeamId);

            _refreshingUniformCombos = true;
            try
            {
                var schedule = SelectedUniformRotationSchedule();
                bool rotate = _league?.Rules?.RotateSavedUniforms ?? true;
                FillUniformCombo(_awayUniformCombo, away, homeRole: false, scheduled?.AwayUniformSetId, scheduled?.AwayUniformAutoCategory, scheduled, schedule, rotate);
                FillUniformCombo(_homeUniformCombo, home, homeRole: true, scheduled?.HomeUniformSetId, scheduled?.HomeUniformAutoCategory, scheduled, schedule, rotate);
            }
            finally
            {
                _refreshingUniformCombos = false;
            }
        }

        private static void FillUniformCombo(ComboBox combo, Team team, bool homeRole, Guid? selectedUniformId, TeamUniformCategory? selectedAutoCategory, ScheduledGame? scheduled, IEnumerable<ScheduledGame> schedule, bool rotateSavedUniforms)
        {
            combo.Items.Clear();
            if (team == null)
                return;

            var autoCategories = homeRole
                ? new[] { TeamUniformCategory.Home, TeamUniformCategory.HomeAlternate }
                : new[] { TeamUniformCategory.Visitor, TeamUniformCategory.VisitorAlternate };
            var activeAutoCategory = GameUniformResolver.ValidAutoCategory(homeRole, selectedAutoCategory)
                ?? GameUniformResolver.DefaultCategory(homeRole);

            foreach (var category in autoCategories)
            {
                var automatic = GameUniformResolver.ResolveUniform(team, homeRole, null, scheduled, scheduled?.GameNumber ?? 1, schedule, rotateSavedUniforms, category);
                combo.Items.Add(new UniformChoiceItem
                {
                    Auto = true,
                    AutoCategory = category,
                    Text = "Auto " + TeamUniformSet.CategoryLabel(category) + " - " + (automatic?.Name ?? "Default")
                });
            }

            foreach (var uniform in GameUniformResolver.UniformChoicesForRole(team, homeRole))
            {
                combo.Items.Add(new UniformChoiceItem
                {
                    Uniform = uniform,
                    UniformId = uniform.Id,
                    Text = TeamUniformSet.CategoryLabel(uniform.Category) + " - " + uniform.Name
                });
            }

            int index = 0;
            if (selectedUniformId.HasValue)
            {
                for (int i = 1; i < combo.Items.Count; i++)
                {
                    if ((combo.Items[i] as UniformChoiceItem)?.UniformId == selectedUniformId.Value)
                    {
                        index = i;
                        break;
                    }
                }
            }
            else
            {
                for (int i = 0; i < combo.Items.Count; i++)
                {
                    if ((combo.Items[i] as UniformChoiceItem)?.AutoCategory == activeAutoCategory)
                    {
                        index = i;
                        break;
                    }
                }
            }
            combo.SelectedIndex = combo.Items.Count == 0 ? -1 : index;
        }

        private void ApplyGameUniformSelection()
        {
            if (_refreshingUniformCombos)
                return;

            var scheduled = SelectedScheduledGame();
            if (scheduled != null)
            {
                scheduled.AwayUniformSetId = SelectedUniformChoiceId(_awayUniformCombo);
                scheduled.HomeUniformSetId = SelectedUniformChoiceId(_homeUniformCombo);
                scheduled.AwayUniformAutoCategory = SelectedUniformAutoCategory(_awayUniformCombo);
                scheduled.HomeUniformAutoCategory = SelectedUniformAutoCategory(_homeUniformCombo);
                MarkDirty();
            }

            _fieldPanel?.Invalidate();
        }

        private static Guid? SelectedUniformChoiceId(ComboBox combo)
        {
            var item = combo?.SelectedItem as UniformChoiceItem;
            return item?.Auto == true ? null : item?.UniformId;
        }

        private static TeamUniformCategory? SelectedUniformAutoCategory(ComboBox combo)
        {
            var item = combo?.SelectedItem as UniformChoiceItem;
            return item?.Auto == true ? item.AutoCategory : null;
        }

        private TeamUniformSet SelectedAwayUniform(Team away, ScheduledGame? scheduled)
            => GameUniformResolver.ResolveUniform(away, homeRole: false, SelectedUniformChoiceId(_awayUniformCombo), scheduled, scheduled?.GameNumber ?? 1, SelectedUniformRotationSchedule(), _league?.Rules?.RotateSavedUniforms ?? true, SelectedUniformAutoCategory(_awayUniformCombo));

        private TeamUniformSet SelectedHomeUniform(Team home, ScheduledGame? scheduled)
            => GameUniformResolver.ResolveUniform(home, homeRole: true, SelectedUniformChoiceId(_homeUniformCombo), scheduled, scheduled?.GameNumber ?? 1, SelectedUniformRotationSchedule(), _league?.Rules?.RotateSavedUniforms ?? true, SelectedUniformAutoCategory(_homeUniformCombo));

        private IEnumerable<ScheduledGame> SelectedUniformRotationSchedule()
            => (SelectedSeason(_commitSeasonCombo) ?? SelectedSeason(_seasonCombo))?.Schedule ?? Enumerable.Empty<ScheduledGame>();

        private string ScheduleLabel(ScheduledGame game)
        {
            if (game == null)
                return "";
            string day = string.IsNullOrWhiteSpace(game.DayLabel) ? "Game Day" : game.DayLabel;
            string number = game.GameNumber > 0 ? "Game #" + game.GameNumber + " - " : "";
            return "Week " + game.Week + " " + day + " - " + number + game.Type + ": " +
                FindTeam(game.AwayTeamId) + " at " + FindTeam(game.HomeTeamId);
        }

        private void RefreshControlTeamCombo()
        {
            if (_controlTeamCombo == null)
                return;

            var selected = _controlTeamCombo.SelectedItem as ControlTeamItem;
            Guid? selectedId = selected?.TeamId;
            bool selectedWatch = selected?.WatchOnly == true;
            bool selectedAuto = selected == null || selected.Auto;

            _controlTeamCombo.Items.Clear();
            var away = SelectedScheduledGame() == null ? SelectedTeam(_awayCombo) : TeamById(SelectedScheduledGame().AwayTeamId);
            var home = SelectedScheduledGame() == null ? SelectedTeam(_homeCombo) : TeamById(SelectedScheduledGame().HomeTeamId);
            var controlled = DefaultControlledTeamsForGame(away, home);
            string autoText = controlled.Count switch
            {
                0 => "Auto - Watch CPU",
                1 => "Auto - " + controlled[0].ScoreboardName,
                _ => "Auto - Player vs Player"
            };
            _controlTeamCombo.Items.Add(new ControlTeamItem { Auto = true, Text = autoText });
            if (away != null)
                _controlTeamCombo.Items.Add(new ControlTeamItem { TeamId = away.Id, Text = "User controls " + away.ScoreboardName });
            if (home != null)
                _controlTeamCombo.Items.Add(new ControlTeamItem { TeamId = home.Id, Text = "User controls " + home.ScoreboardName });
            _controlTeamCombo.Items.Add(new ControlTeamItem { WatchOnly = true, Text = "Watch CPU vs CPU" });

            int index = 0;
            for (int i = 0; i < _controlTeamCombo.Items.Count; i++)
            {
                var item = _controlTeamCombo.Items[i] as ControlTeamItem;
                if (item == null)
                    continue;
                if ((selectedWatch && item.WatchOnly)
                    || (selectedAuto && item.Auto)
                    || (selectedId.HasValue && item.TeamId == selectedId.Value))
                {
                    index = i;
                    break;
                }
            }
            _controlTeamCombo.SelectedIndex = _controlTeamCombo.Items.Count == 0 ? -1 : index;
        }

        private List<Team> DefaultControlledTeamsForGame(Team away, Team home)
        {
            _league.UserControlledTeamIds ??= new List<Guid>();
            var result = new List<Team>();
            if (away != null && _league.UserControlledTeamIds.Contains(away.Id))
                result.Add(away);
            if (home != null && _league.UserControlledTeamIds.Contains(home.Id))
                result.Add(home);
            return result;
        }

        private BaseballFieldPreset SelectedFieldPreset()
            => _fieldPresetCombo?.SelectedItem as BaseballFieldPreset ?? BaseballFieldPresets.Default;

        private IEnumerable<BaseballFieldPreset> AllFieldPresets()
        {
            foreach (var preset in BaseballFieldPresets.All)
                yield return preset;

            foreach (var field in _league?.CustomFields ?? Enumerable.Empty<CustomBaseballField>())
                yield return BaseballFieldPresets.FromCustom(field);
        }

        private BaseballFieldPreset FindFieldPreset(string id)
        {
            if (!string.IsNullOrWhiteSpace(id))
            {
                var custom = (_league?.CustomFields ?? new List<CustomBaseballField>())
                    .FirstOrDefault(f => string.Equals(f.Id, id, StringComparison.OrdinalIgnoreCase));
                if (custom != null)
                    return BaseballFieldPresets.FromCustom(custom);
            }

            return BaseballFieldPresets.Find(id);
        }

        private void RefreshFieldPresetCombo()
        {
            if (_fieldPresetCombo == null)
                return;

            string selectedId = (_fieldPresetCombo.SelectedItem as BaseballFieldPreset)?.Id;
            if (string.IsNullOrWhiteSpace(selectedId))
                selectedId = SelectedTeam(_homeCombo)?.HomeFieldPresetId;
            if (string.IsNullOrWhiteSpace(selectedId))
                selectedId = BaseballFieldPresets.Default.Id;

            _fieldPresetCombo.Items.Clear();
            foreach (var preset in AllFieldPresets())
                _fieldPresetCombo.Items.Add(preset);

            SelectFieldPreset(selectedId);
            if (_fieldPresetCombo.SelectedIndex < 0 && _fieldPresetCombo.Items.Count > 0)
                _fieldPresetCombo.SelectedIndex = 0;
        }

        private void ApplyHomeTeamFieldSelection()
        {
            var home = SelectedTeam(_homeCombo);
            if (home == null || _fieldPresetCombo == null)
                return;
            string id = string.IsNullOrWhiteSpace(home.HomeFieldPresetId)
                ? BaseballFieldPresets.Default.Id
                : home.HomeFieldPresetId;
            SelectFieldPreset(id);
        }

        private void SelectFieldPreset(string id)
        {
            if (_fieldPresetCombo == null)
                return;
            var preset = FindFieldPreset(id);
            for (int i = 0; i < _fieldPresetCombo.Items.Count; i++)
            {
                if (_fieldPresetCombo.Items[i] is BaseballFieldPreset item &&
                    string.Equals(item.Id, preset.Id, StringComparison.OrdinalIgnoreCase))
                {
                    _fieldPresetCombo.SelectedIndex = i;
                    return;
                }
            }
        }

        private void SelectTeamCombo(ComboBox combo, Guid teamId)
        {
            if (combo == null) return;
            for (int i = 0; i < combo.Items.Count; i++)
            {
                if ((combo.Items[i] as TeamItem)?.Team?.Id == teamId)
                {
                    combo.SelectedIndex = i;
                    return;
                }
            }
        }

        private Team SelectedTeam() => (_teamList?.SelectedItem as TeamItem)?.Team;
        private Season SelectedSeason(ComboBox combo) => (combo?.SelectedItem as SeasonItem)?.Season;
        private Team SelectedTeam(ComboBox combo) => (combo?.SelectedItem as TeamItem)?.Team;

        private static StatsScope SelectedStatsScope(ComboBox combo)
        {
            string value = Convert.ToString(combo?.SelectedItem) ?? "";
            if (value.Equals("Playoffs", StringComparison.OrdinalIgnoreCase)) return StatsScope.Playoffs;
            if (value.Equals("Career", StringComparison.OrdinalIgnoreCase)) return StatsScope.Career;
            if (value.Equals("All-Time", StringComparison.OrdinalIgnoreCase)) return StatsScope.AllTime;
            return StatsScope.Season;
        }

        private void LoadSelectedTeam()
        {
            var team = SelectedTeam();
            if (team != null)
                EnsureTeamCoaches(team);
            PlayTeamContextMusic(team);
            _suppress = true;
            _cityBox.Text = team?.City ?? "";
            _nicknameBox.Text = team?.Nickname ?? "";
            _abbrBox.Text = team?.ScoreboardAbbreviation ?? "";
            _coachBox.Text = team?.CoachName ?? "";
            _primaryPanel.BackColor = team == null ? SystemColors.Control : Color.FromArgb(team.PrimaryArgb);
            _secondaryPanel.BackColor = team == null ? SystemColors.Control : Color.FromArgb(team.SecondaryArgb);
            _tips.SetToolTip(_primaryPanel, team == null ? "" : "Primary " + ToHex(Color.FromArgb(team.PrimaryArgb)));
            _tips.SetToolTip(_secondaryPanel, team == null ? "" : "Secondary " + ToHex(Color.FromArgb(team.SecondaryArgb)));
            _rosterGrid.Rows.Clear();
            if (team != null)
            {
                foreach (var p in team.Roster)
                {
                    if (p.Classification == PlayerClassification.Unassigned)
                        p.Classification = Simulator.RandomClassification(_rng);
                    if (string.IsNullOrWhiteSpace(p.Positions))
                        p.Positions = Simulator.RandomPositions(_rng, p.Role);
                    EnsureDevelopmentFields(p);
                    EnsureHandednessFields(p);
                    EnsurePitchCountFields(p);
                    EnsureStealDefenseFields(p);
                    PitchProfileEngine.NormalizePlayerPitchProfiles(p, _rng);
                    var rowValues = new List<object>
                    {
                        p.Name,
                        p.Role,
                        p.Classification,
                        p.Positions,
                        p.Bats,
                        p.Throws,
                        p.CareerPitchCount,
                        p.Potential,
                        p.WorkEthic,
                        p.Durability,
                        p.RegressionRisk,
                        p.InjuryStatus,
                        p.InjuryName,
                        p.InjuryGamesRemaining,
                        p.InjuryMissedGamesThisSeason,
                        p.MedicalTag,
                        p.MedicalTagEligible,
                        p.VarsitySeasonsPlayed,
                        p.VarsityCallUpSeasonNumber <= 0 ? "" : p.VarsityCallUpSeasonNumber.ToString(),
                        p.RedshirtActive,
                        p.RedshirtUsed,
                        AllStarTagText(p),
                        AwardTagText(p),
                        NullableColorHex(p.JerseyArgbOverride),
                        NullableColorHex(p.PantsArgbOverride),
                        NullableColorHex(p.CapHelmetArgbOverride)
                    };
                    foreach (var pitch in PitchProfileEngine.AllPitchTypes)
                    {
                        var profile = p.PitchArsenal.FirstOrDefault(x => x.PitchType == pitch);
                        rowValues.Add(profile?.Enabled == true);
                        rowValues.Add(profile == null ? 0 : Math.Clamp(profile.Effectiveness, 0, 99));
                    }
                    rowValues.Add(PitchProfileEngine.PitchListText(p.PitchStrengths));
                    rowValues.Add(PitchProfileEngine.PitchListText(p.PitchWeaknesses));
                    rowValues.Add(PitchProfileEngine.ArsenalScoutSummary(p));
                    rowValues.AddRange(new object[]
                    {
                        p.Contact,
                        p.Power,
                        p.Speed,
                        p.StealAggression,
                        p.BaseRunning,
                        p.Fielding,
                        p.HoldRunner,
                        p.Pickoff,
                        p.DeliveryTime,
                        p.ArmStrength,
                        p.PopTime,
                        p.Accuracy,
                        p.TagRating,
                        p.Pitching,
                        p.Stamina,
                        p.Overall
                    });
                    int row = _rosterGrid.Rows.Add(rowValues.ToArray());
                    _rosterGrid.Rows[row].Tag = p;
                    _rosterGrid.Rows[row].Cells["jersey"].ToolTipText = "Blank uses team primary color";
                    _rosterGrid.Rows[row].Cells["pants"].ToolTipText = "Blank uses white pants";
                    _rosterGrid.Rows[row].Cells["caphelmet"].ToolTipText = "Blank uses team secondary color";
                    _rosterGrid.Rows[row].Cells["pitchstrengths"].ToolTipText = "Examples: FB, SL, FORK";
                    _rosterGrid.Rows[row].Cells["pitchweaknesses"].ToolTipText = "Examples: CB, KN";
                }
            }
            _suppress = false;
            RefreshTeamBadges(team);
            RefreshSelectedPlayerAvatar();
        }

        private void RefreshSelectedPlayerAvatar()
        {
            Player player = _rosterGrid?.CurrentRow?.Tag as Player;
            SetPlayerAvatarImage(null);

            if (_playerAvatarLabel == null || _playerAvatarBox == null)
                return;

            if (player == null)
            {
                _playerAvatarLabel.Text = "Select a player to show avatar.";
                return;
            }

            string path = ResolvePlayerAvatarPath(player);
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                SetPlayerAvatarImage(LoadImageCopy(path));
                _playerAvatarLabel.Text = player.Name + Environment.NewLine + "Avatar saved";
                _tips.SetToolTip(_playerAvatarBox, path);
            }
            else
            {
                _playerAvatarLabel.Text = player.Name + Environment.NewLine + "No avatar photo";
                _tips.SetToolTip(_playerAvatarBox, "Use Player Photo... to add an avatar.");
            }
        }

        private void SetPlayerAvatarImage(Image? image)
        {
            if (_playerAvatarBox == null)
                return;

            var old = _playerAvatarImage;
            _playerAvatarImage = image;
            _playerAvatarBox.Image = image;
            old?.Dispose();
        }

        private static Image LoadImageCopy(string path)
        {
            using var stream = File.OpenRead(path);
            using var image = Image.FromStream(stream);
            return new Bitmap(image);
        }

        private void PlayTeamContextMusic(Team? team)
        {
            string[] tracks = LaunchSoundPlayer.ResolveAssignedTeamMusicPlaylist(team);
            string key = string.Join("|", tracks);
            if (tracks.Length == 0)
            {
                _teamContextMusic.Stop();
                _currentTeamContextMusicPath = "";
                return;
            }
            if (string.Equals(key, _currentTeamContextMusicPath, StringComparison.OrdinalIgnoreCase))
                return;

            _teamContextMusic.PlayPlaylistLoop(tracks);
            _currentTeamContextMusicPath = key;
        }

        private void UpdateTeamText()
        {
            if (_suppress) return;
            var team = SelectedTeam();
            if (team == null) return;
            team.City = (_cityBox.Text ?? "").Trim();
            team.Nickname = string.IsNullOrWhiteSpace(_nicknameBox.Text) ? "Team" : _nicknameBox.Text.Trim();
            team.ScoreboardAbbreviation = Team.Limit(_abbrBox.Text, Team.MaxScoreboardAbbreviationLength).ToUpperInvariant();
            team.CoachName = string.IsNullOrWhiteSpace(_coachBox.Text) ? "Head Coach" : Team.Limit(_coachBox.Text, 40);
            var headCoach = HeadCoach(team);
            if (headCoach != null)
            {
                headCoach.Name = team.CoachName;
                headCoach.Role = "Head Coach";
                headCoach.Active = true;
            }
            MarkDirty();
            RefreshAll();
        }

        private void PickColor(bool primary)
        {
            var team = SelectedTeam();
            if (team == null) return;
            using var dlg = new TeamColorDialog(
                Color.FromArgb(primary ? team.PrimaryArgb : team.SecondaryArgb),
                primary ? "Primary Team Color" : "Secondary Team Color");
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            if (primary) team.PrimaryArgb = dlg.SelectedColor.ToArgb();
            else team.SecondaryArgb = dlg.SelectedColor.ToArgb();
            MarkDirty();
            LoadSelectedTeam();
        }

        private void ManageTeamCoaches()
        {
            var team = SelectedTeam();
            if (team == null)
            {
                MessageBox.Show(this, "Select a team first.");
                return;
            }

            EnsureTeamCoaches(team);
            using var form = new Form
            {
                Text = "Coaches - " + team.DisplayName,
                StartPosition = FormStartPosition.CenterParent,
                Size = new Size(1080, 430),
                MinimizeBox = false,
                MaximizeBox = false
            };

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Padding = new Padding(10) };
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            form.Controls.Add(root);

            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false
            };
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "name", HeaderText = "Name", Width = 180 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "role", HeaderText = "Role", Width = 120 });
            var styleColumn = new DataGridViewComboBoxColumn { Name = "style", HeaderText = "Style", Width = 118 };
            styleColumn.Items.AddRange("Below Average", "Average", "Above Average", "Championship");
            grid.Columns.Add(styleColumn);
            var strategyColumn = new DataGridViewComboBoxColumn { Name = "strategy", HeaderText = "Strategy", Width = 112 };
            strategyColumn.Items.AddRange("Safe", "Conservative", "Aggressive");
            grid.Columns.Add(strategyColumn);
            grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "head", HeaderText = "Head", Width = 58 });
            grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "active", HeaderText = "Active", Width = 62 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "wins", HeaderText = "W", Width = 58, ReadOnly = true });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "losses", HeaderText = "L", Width = 58, ReadOnly = true });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "playoffwins", HeaderText = "PO W", Width = 66, ReadOnly = true });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "playofflosses", HeaderText = "PO L", Width = 66, ReadOnly = true });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "titles", HeaderText = "Titles", Width = 66, ReadOnly = true });
            grid.CellEndEdit += (s, e) =>
            {
                if (grid.Rows[e.RowIndex].Tag is Coach coach)
                    SaveCoachGridRow(team, coach, grid.Rows[e.RowIndex]);
            };
            grid.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (grid.IsCurrentCellDirty)
                    grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
            grid.CellValueChanged += (s, e) =>
            {
                if (e.RowIndex < 0 || grid.Rows[e.RowIndex].Tag is not Coach coach)
                    return;
                SaveCoachGridRow(team, coach, grid.Rows[e.RowIndex]);
                if (grid.Columns[e.ColumnIndex].Name == "head" && Convert.ToBoolean(grid.Rows[e.RowIndex].Cells["head"].Value))
                    RefreshCoachGrid(team, grid);
            };
            root.Controls.Add(grid, 0, 0);

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            AddButton(buttons, "Close", (s, e) => form.Close());
            AddButton(buttons, "Remove Coach", (s, e) =>
            {
                if (grid.CurrentRow?.Tag is not Coach coach)
                    return;
                if (team.Coaches.Count <= 1)
                {
                    MessageBox.Show(form, "A team must keep at least one coach.");
                    return;
                }
                if (MessageBox.Show(form, "Remove " + coach.Name + "?", "Remove coach", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK)
                    return;
                team.Coaches.Remove(coach);
                if (team.CoachId == coach.Id)
                    SetHeadCoach(team, team.Coaches.First());
                MarkDirty();
                RefreshCoachGrid(team, grid);
                LoadSelectedTeam();
            });
            AddButton(buttons, "Add Coach", (s, e) =>
            {
                var coach = new Coach { Name = "New Coach", Role = "Assistant Coach", Active = true };
                team.Coaches.Add(coach);
                MarkDirty();
                RefreshCoachGrid(team, grid);
            });
            root.Controls.Add(buttons, 0, 1);

            RefreshCoachGrid(team, grid);
            form.ShowDialog(this);
            EnsureTeamCoaches(team);
            LoadSelectedTeam();
            RefreshHallOfFameViews();
        }

        private void RefreshCoachGrid(Team team, DataGridView grid)
        {
            EnsureTeamCoaches(team);
            grid.Rows.Clear();
            foreach (var coach in team.Coaches)
            {
                var record = BuildCoachRecord(team, coach);
                int row = grid.Rows.Add(
                    coach.Name,
                    coach.Role,
                    CoachStyleLabel(coach.Style),
                    coach.Strategy.ToString(),
                    coach.Id == team.CoachId,
                    coach.Active,
                    record.Wins,
                    record.Losses,
                    record.PlayoffWins,
                    record.PlayoffLosses,
                    record.ChampionshipWins);
                grid.Rows[row].Tag = coach;
            }
        }

        private void SaveCoachGridRow(Team team, Coach coach, DataGridViewRow row)
        {
            coach.Name = Team.Limit(Convert.ToString(row.Cells["name"].Value), 40);
            if (string.IsNullOrWhiteSpace(coach.Name))
                coach.Name = "Coach";
            coach.Role = Team.Limit(Convert.ToString(row.Cells["role"].Value), 32);
            if (string.IsNullOrWhiteSpace(coach.Role))
                coach.Role = "Assistant Coach";
            coach.Style = ParseEnumCell(row.Cells["style"].Value, CoachStyle.Average);
            coach.Strategy = ParseEnumCell(row.Cells["strategy"].Value, CoachStrategy.Conservative);
            coach.Active = Convert.ToBoolean(row.Cells["active"].Value ?? true);

            if (Convert.ToBoolean(row.Cells["head"].Value ?? false))
                SetHeadCoach(team, coach);
            else if (coach.Id == team.CoachId)
                row.Cells["head"].Value = true;

            MarkDirty();
            LoadSelectedTeam();
        }

        private static TEnum ParseEnumCell<TEnum>(object value, TEnum fallback) where TEnum : struct
        {
            string text = Convert.ToString(value) ?? "";
            return Enum.TryParse(text.Replace(" ", ""), out TEnum parsed) ? parsed : fallback;
        }

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

        private void EnsureTeamCoaches(Team team)
        {
            if (team == null)
                return;
            team.NormalizeText();
            if (team.Coaches.All(c => c.Id != team.CoachId))
                team.Coaches.Insert(0, new Coach { Id = team.CoachId, Name = team.CoachName, Role = "Head Coach", Active = true });
        }

        private Coach HeadCoach(Team team)
        {
            EnsureTeamCoaches(team);
            return team?.Coaches?.FirstOrDefault(c => c.Id == team.CoachId);
        }

        private static void SetHeadCoach(Team team, Coach coach)
        {
            if (team == null || coach == null)
                return;
            foreach (var item in team.Coaches ?? Enumerable.Empty<Coach>())
            {
                if (item.Id == coach.Id)
                    item.Role = "Head Coach";
                else if (string.Equals(item.Role, "Head Coach", StringComparison.OrdinalIgnoreCase))
                    item.Role = "Assistant Coach";
            }
            coach.Active = true;
            team.CoachId = coach.Id;
            team.CoachName = coach.Name;
        }

        private CoachRecord BuildCoachRecord(Team team, Coach coach)
        {
            var record = new CoachRecord
            {
                CoachId = coach?.Id ?? Guid.Empty,
                TeamId = team?.Id ?? Guid.Empty,
                CoachName = CoachDisplayName(team, coach),
                TeamName = team?.DisplayName ?? ""
            };
            if (team == null || coach == null || _league?.Seasons == null)
                return record;

            foreach (var season in _league.Seasons)
            {
                foreach (var game in season.Games ?? Enumerable.Empty<GameResult>())
                {
                    bool awayCoach = IsGameForCoach(game, team.Id, coach.Id, away: true);
                    bool homeCoach = IsGameForCoach(game, team.Id, coach.Id, away: false);
                    if (!awayCoach && !homeCoach)
                        continue;

                    bool won = awayCoach
                        ? game.AwayScore > game.HomeScore
                        : game.HomeScore > game.AwayScore;
                    bool tied = game.AwayScore == game.HomeScore;
                    if (won) record.Wins++;
                    else if (tied) record.Ties++;
                    else record.Losses++;

                    if (won)
                    {
                        var scheduled = (season.Schedule ?? new List<ScheduledGame>())
                            .FirstOrDefault(s => game.ScheduledGameId.HasValue && s.Id == game.ScheduledGameId.Value);
                        if (scheduled?.Type == ScheduledGameType.District) record.DistrictWins++;
                        else if (scheduled?.Type == ScheduledGameType.Region) record.RegionWins++;
                        else if (scheduled?.Type == ScheduledGameType.Conference) record.ConferenceWins++;
                    }
                }

                foreach (var series in season.Playoffs ?? Enumerable.Empty<PlayoffSeries>())
                {
                    bool teamA = series.TeamAId == team.Id && (series.TeamACoachId == coach.Id ||
                        (series.TeamACoachId == Guid.Empty && coach.Id == team.CoachId));
                    bool teamB = series.TeamBId == team.Id && (series.TeamBCoachId == coach.Id ||
                        (series.TeamBCoachId == Guid.Empty && coach.Id == team.CoachId));
                    if (!teamA && !teamB)
                        continue;

                    record.PlayoffWins += teamA ? series.TeamAWins : series.TeamBWins;
                    record.PlayoffLosses += teamA ? series.TeamBWins : series.TeamAWins;
                    if (series.WinnerTeamId == team.Id && IsFinalChampionshipSeries(series) &&
                        (series.WinnerCoachId == coach.Id || !series.WinnerCoachId.HasValue || series.WinnerCoachId == Guid.Empty))
                        record.ChampionshipWins++;
                }
            }

            record.HallScore = CalculateCoachHallScore(record);
            record.Recommendation = CoachHallRecommendation(record.HallScore);
            record.Reason = BuildCoachHallReason(record);
            return record;
        }

        private bool IsGameForCoach(GameResult game, Guid teamId, Guid coachId, bool away)
        {
            if (game == null || coachId == Guid.Empty)
                return false;

            Guid gameTeamId = away ? game.AwayTeamId : game.HomeTeamId;
            Guid gameCoachId = away ? game.AwayCoachId : game.HomeCoachId;
            if (gameTeamId != teamId)
                return false;
            if (gameCoachId == coachId)
                return true;

            var team = TeamById(teamId);
            return gameCoachId == Guid.Empty && team?.CoachId == coachId;
        }

        private static int CalculateCoachHallScore(CoachRecord record)
        {
            if (record == null)
                return 0;
            return record.ChampionshipWins * 100 +
                record.PlayoffWins * 12 +
                record.DistrictWins * 5 +
                record.RegionWins * 3 +
                record.ConferenceWins * 2;
        }

        private static string CoachHallRecommendation(int score)
        {
            if (score >= 150) return "Must Induct";
            if (score >= 100) return "Recommended";
            if (score >= 60) return "Watch List";
            return "Developing";
        }

        private static string BuildCoachHallReason(CoachRecord record)
        {
            var parts = new List<string>();
            if (record.ChampionshipWins > 0) parts.Add(record.ChampionshipWins + " championship(s)");
            if (record.PlayoffWins > 0) parts.Add(record.PlayoffWins + " playoff win(s)");
            if (record.DistrictWins > 0) parts.Add(record.DistrictWins + " district win(s)");
            if (record.RegionWins > 0) parts.Add(record.RegionWins + " region win(s)");
            if (record.ConferenceWins > 0) parts.Add(record.ConferenceWins + " conference win(s)");
            if (parts.Count == 0) parts.Add(FormatRecord(record.Wins, record.Losses, record.Ties) + " career record");
            return string.Join(", ", parts);
        }

        private static string ToHex(Color c) => "#" + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2");

        private static string NullableColorHex(int? argb)
        {
            return argb.HasValue ? ToHex(Color.FromArgb(argb.Value)) : "";
        }

        private bool TrySaveUniformOverride(DataGridViewRow row, string cellName, Action<int?> assign)
        {
            string value = Convert.ToString(row.Cells[cellName].Value);
            if (string.IsNullOrWhiteSpace(value))
            {
                assign(null);
                return true;
            }

            if (TryParseHexColor(value, out var color))
            {
                assign(color.ToArgb());
                row.Cells[cellName].Value = ToHex(color);
                return true;
            }

            MessageBox.Show(this,
                "Enter uniform colors as #RRGGBB or leave blank to use the team default.",
                "Invalid uniform color", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        private static bool TryParseHexColor(string text, out Color color)
        {
            color = Color.Black;
            string value = (text ?? "").Trim();
            if (value.StartsWith("#")) value = value.Substring(1);
            if (value.Length != 6) return false;
            if (!int.TryParse(value.Substring(0, 2), System.Globalization.NumberStyles.HexNumber, null, out int r)) return false;
            if (!int.TryParse(value.Substring(2, 2), System.Globalization.NumberStyles.HexNumber, null, out int g)) return false;
            if (!int.TryParse(value.Substring(4, 2), System.Globalization.NumberStyles.HexNumber, null, out int b)) return false;
            color = Color.FromArgb(255, r, g, b);
            return true;
        }

        private string AssetLibraryPath()
        {
            return string.IsNullOrWhiteSpace(_league?.AssetLibraryPath)
                ? LeagueFile.DefaultAssetLibraryPath
                : _league.AssetLibraryPath;
        }

        private string ExistingAssetLibraryPath()
        {
            string path = AssetLibraryPath();
            return Directory.Exists(path) ? path : null;
        }

        public void ApplyMenuAction(MenuAction action)
        {
            switch (action)
            {
                case MenuAction.StartDynasty:
                    NewLeague();
                    break;
                case MenuAction.ContinueDynasty:
                    OpenLeague();
                    break;
                case MenuAction.Game:
                    SelectTab("Game");
                    break;
                case MenuAction.Teams:
                    SelectTab("Teams");
                    break;
                case MenuAction.Seasons:
                    SelectTab("Seasons");
                    break;
                case MenuAction.Replays:
                    SelectTab("Game");
                    WatchReplay();
                    break;
                case MenuAction.Settings:
                    ShowSettingsMenu();
                    break;
            }
        }

        private void SelectTab(string text)
        {
            if (_tabs == null)
                return;
            foreach (TabPage page in _tabs.TabPages)
            {
                if (string.Equals(page.Text, text, StringComparison.OrdinalIgnoreCase))
                {
                    _tabs.SelectedTab = page;
                    return;
                }
            }
        }

        private void ShowSettingsMenu()
        {
            using var dialog = new Form
            {
                Text = "Dan's RBI Baseball 2026 Settings",
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MinimizeBox = false,
                MaximizeBox = false,
                ClientSize = new Size(560, 294)
            };

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 6,
                ColumnCount = 1,
                Padding = new Padding(14)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            dialog.Controls.Add(root);

            root.Controls.Add(new Label
            {
                Text = "Choose a settings action.",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font(Font.FontFamily, 11f, FontStyle.Bold)
            }, 0, 0);

            var row1 = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            var setLibrary = new Button { Text = "Set Asset Library...", AutoSize = true };
            setLibrary.Click += (s, e) => { dialog.Close(); SetAssetLibrary(); };
            var openLibrary = new Button { Text = "Open Asset Library", AutoSize = true };
            openLibrary.Click += (s, e) => { dialog.Close(); OpenAssetLibrary(); };
            row1.Controls.Add(openLibrary);
            row1.Controls.Add(setLibrary);
            root.Controls.Add(row1, 0, 1);

            var row2 = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            var cutscenes = new Button { Text = "League Cutscenes...", AutoSize = true };
            cutscenes.Click += (s, e) => { dialog.Close(); ShowCutsceneEditor(); };
            row2.Controls.Add(cutscenes);
            root.Controls.Add(row2, 0, 2);

            var row3 = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            var anthemDefault = new ComboBox { Width = 190, DropDownStyle = ComboBoxStyle.DropDownList };
            anthemDefault.Items.AddRange(new object[] { "Current Game Settings", "League Cut Scene", "Team Cut Scene" });
            anthemDefault.SelectedIndex = _league.NationalAnthemCutsceneDefault switch
            {
                NationalAnthemCutsceneDefault.LeagueCutscene => 1,
                NationalAnthemCutsceneDefault.TeamCutscene => 2,
                _ => 0
            };
            anthemDefault.SelectedIndexChanged += (s, e) =>
            {
                _league.NationalAnthemCutsceneDefault = anthemDefault.SelectedIndex switch
                {
                    1 => NationalAnthemCutsceneDefault.LeagueCutscene,
                    2 => NationalAnthemCutsceneDefault.TeamCutscene,
                    _ => NationalAnthemCutsceneDefault.CurrentGameSettings
                };
                MarkDirty();
            };
            row3.Controls.Add(anthemDefault);
            row3.Controls.Add(new Label { Text = "National Anthem Default", AutoSize = true, Padding = new Padding(0, 8, 8, 0) });
            root.Controls.Add(row3, 0, 3);

            _league.Rules ??= new LeagueRules();
            var row4 = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            var rotateUniforms = new CheckBox
            {
                Text = "Auto-rotate saved uniforms by home/visitor game sequence",
                Checked = _league.Rules.RotateSavedUniforms,
                AutoSize = true,
                Padding = new Padding(0, 8, 8, 0)
            };
            rotateUniforms.CheckedChanged += (s, e) =>
            {
                _league.Rules ??= new LeagueRules();
                _league.Rules.RotateSavedUniforms = rotateUniforms.Checked;
                MarkDirty();
                RefreshGameUniformCombos();
            };
            row4.Controls.Add(rotateUniforms);
            root.Controls.Add(row4, 0, 4);

            var row5 = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            var close = new Button { Text = "Close", AutoSize = true };
            close.Click += (s, e) => dialog.Close();
            row5.Controls.Add(close);
            root.Controls.Add(row5, 0, 5);

            dialog.ShowDialog(this);
        }

        private void ShowCutsceneEditor()
        {
            _league.Cutscenes ??= new List<CutsceneDefinition>();
            string assetDir = UserDataPaths.EnsureLeagueCutsceneDirectory();
            using var dialog = new CutsceneEditorDialog(_league.Cutscenes, assetDir, ExistingAssetLibraryPath(), "League Cutscenes", CutsceneCatalog.LeagueTriggers);
            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;

            _league.Cutscenes = dialog.Cutscenes.Select(c => new CutsceneDefinition
            {
                Id = c.Id == Guid.Empty ? Guid.NewGuid() : c.Id,
                Name = c.Name,
                Trigger = c.Trigger,
                UniformFolder = TeamCutsceneUniformFolder.Any,
                MediaPath = c.MediaPath,
                Enabled = c.Enabled,
                DurationSeconds = c.DurationSeconds
            }).ToList();
            MarkDirty();
            _status.Text = "League cutscene settings updated.";
        }

        private void ManageTeamCutscenes()
        {
            var team = SelectedTeam();
            if (team == null) return;
            if (!EnsureLeagueSavedForAssets()) return;

            team.Cutscenes ??= new List<CutsceneDefinition>();
            string assetDir = GetTeamCutsceneDir(team, true);
            using var dialog = new CutsceneEditorDialog(
                team.Cutscenes,
                assetDir,
                ExistingAssetLibraryPath(),
                team.DisplayName + " Cutscenes",
                CutsceneCatalog.TeamTriggers,
                TeamCutsceneUniformFolders());
            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;

            team.Cutscenes = dialog.Cutscenes.Select(c => new CutsceneDefinition
            {
                Id = c.Id == Guid.Empty ? Guid.NewGuid() : c.Id,
                Name = c.Name,
                Trigger = c.Trigger,
                UniformFolder = c.UniformFolder,
                MediaPath = c.MediaPath,
                Enabled = c.Enabled,
                DurationSeconds = c.DurationSeconds
            }).ToList();
            MarkDirty();
            _status.Text = "Team cutscene settings updated for " + team.DisplayName + ".";
        }

        private void SetAssetLibrary()
        {
            using var dlg = new AssetLibrarySetupDialog(AssetLibraryPath());
            if (dlg.ShowDialog(this) != DialogResult.OK)
                return;

            _league.AssetLibraryPath = dlg.SelectedPath;
            MarkDirty();
            _status.Text = "Asset library set to " + dlg.SelectedPath;
        }

        private void OpenAssetLibrary()
        {
            string path = AssetLibraryPath();
            if (string.IsNullOrWhiteSpace(path))
            {
                MessageBox.Show(this,
                    "No shared asset library is configured. Use Settings > Set Asset Library to choose one.",
                    "Asset library not configured", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (!Directory.Exists(path))
            {
                MessageBox.Show(this,
                    "The asset library folder does not exist:\n\n" + path,
                    "Asset library not found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }

        private void AddTeam()
        {
            var team = new Team { City = "New", Nickname = "Team", ScoreboardAbbreviation = "NEW" };
            Simulator.FillRandomRoster(team, _rng);
            EnsureTeamBaseLineup(team, recalculate: true);
            SaveTeamBaseLineupFile(team);
            _league.Teams.Add(team);
            PlayoffEngine.EnsureDefaultStructure(_league);
            MarkDirty();
            RefreshAll();
            _teamList.SelectedIndex = _teamList.Items.Count - 1;
        }

        private void AddSchoolTeamFromCsv()
        {
            string csvPath = ChooseSchoolsCsvPath();
            if (string.IsNullOrWhiteSpace(csvPath)) return;

            List<SchoolTeamRecord> schools;
            try
            {
                schools = SchoolTeamImporter.Load(csvPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Could not load schools CSV", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (schools.Count == 0)
            {
                MessageBox.Show(this, "No schools were found in:\n\n" + csvPath, "No schools", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var dlg = new SchoolTeamPickerDialog(schools);
            if (dlg.ShowDialog(this) != DialogResult.OK || dlg.SelectedSchool == null) return;

            var school = dlg.SelectedSchool;
            var team = new Team
            {
                City = (school.Name ?? "").Trim(),
                Nickname = string.IsNullOrWhiteSpace(school.Mascot)
                    ? "Team"
                    : school.Mascot.Trim(),
                CatalogSchoolName = school.Name,
                CatalogMascot = school.Mascot,
                ScoreboardAbbreviation = BuildScoreboardAbbreviation(school)
            };

            if (TryParseHexColor(school.PrimaryColor, out var primary))
                team.PrimaryArgb = primary.ToArgb();
            if (TryParseHexColor(school.SecondaryColor, out var secondary))
                team.SecondaryArgb = secondary.ToArgb();

            Simulator.FillRandomRoster(team, _rng);
            EnsureTeamBaseLineup(team, recalculate: true);
            SaveTeamBaseLineupFile(team);
            _league.Teams.Add(team);
            PlayoffEngine.EnsureDefaultStructure(_league);
            MarkDirty();
            RefreshAll();
            SelectTeam(team.Id);
            string rosterStatus = TryImportRosterFromAssetLibrary(team, showMessages: false, confirmReplace: false)
                ? " Roster imported from library."
                : "";

            string logoStatus = "";
            if (!string.IsNullOrWhiteSpace(school.LogoPath))
            {
                if (TryCopyTeamLogo(team, school.LogoPath, out string copyMessage))
                    logoStatus = " Logo copied.";
                else if (!string.IsNullOrWhiteSpace(copyMessage))
                    logoStatus = " " + copyMessage;
            }

            int uniformCount = ImportSchoolUniformImages(team, school);
            string uniformStatus = uniformCount > 0 ? " " + uniformCount + " uniform image(s) imported." : "";

            _status.Text = "Created " + team.DisplayName + " from schools CSV." + logoStatus + uniformStatus + rosterStatus;
            RefreshDynastyGrid();
        }

        private int ImportSchoolUniformImages(Team team, SchoolTeamRecord school)
        {
            if (team == null || school == null)
                return 0;

            int imported = 0;
            imported += TryAddSchoolUniform(team, TeamUniformCategory.Home, "Home", school.HomeUniformImagePath) ? 1 : 0;
            imported += TryAddSchoolUniform(team, TeamUniformCategory.Visitor, "Visitor", school.AwayUniformImagePath) ? 1 : 0;
            imported += TryAddSchoolUniform(team, TeamUniformCategory.HomeAlternate, "Home Alternate", school.AlternateHomeUniformImagePath) ? 1 : 0;
            imported += TryAddSchoolUniform(team, TeamUniformCategory.VisitorAlternate, "Visitor Alternate", school.AlternateAwayUniformImagePath) ? 1 : 0;
            if (imported > 0)
                MarkDirty();
            return imported;
        }

        private bool TryAddSchoolUniform(Team team, TeamUniformCategory category, string name, string sourcePath)
        {
            if (team == null || string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath) || !IsImageFile(sourcePath))
                return false;
            if (!EnsureLeagueSavedForAssets())
                return false;

            try
            {
                team.EnsureDefaultUniformSets();
                string dir = Path.Combine(GetTeamUniformDir(team, true), TeamUniformSet.CategoryLabel(category).ToUpperInvariant(), Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(dir);
                string ext = Path.GetExtension(sourcePath);
                if (string.IsNullOrWhiteSpace(ext))
                    ext = ".png";
                string dest = Path.Combine(dir, "uniform" + ext.ToLowerInvariant());
                File.Copy(sourcePath, dest, overwrite: true);

                foreach (var uniform in team.UniformSets.Where(u => u.Category == category))
                    uniform.Active = false;
                team.UniformSets.Add(new TeamUniformSet
                {
                    Category = category,
                    Name = name,
                    JerseyArgb = category == TeamUniformCategory.Home || category == TeamUniformCategory.Visitor ? team.PrimaryArgb : team.SecondaryArgb,
                    PantsArgb = category == TeamUniformCategory.Home || category == TeamUniformCategory.HomeAlternate ? Color.White.ToArgb() : Color.LightGray.ToArgb(),
                    CapHelmetArgb = team.SecondaryArgb,
                    ImagePath = AssetPathResolver.ToPortablePath(dest),
                    Active = true
                });
                return true;
            }
            catch
            {
                return false;
            }
        }

        private string ChooseSchoolsCsvPath()
        {
            string savedCsvPath = SchoolTeamCsvCatalog.PreferredSchoolsCsvPath;
            if (File.Exists(savedCsvPath))
                return savedCsvPath;

            if (MessageBox.Show(this,
                    "No saved schools.csv was found in your local application data.\n\nChoose a schools.csv file now to save as your editable source of truth?",
                    "Schools CSV Missing",
                    MessageBoxButtons.OKCancel,
                    MessageBoxIcon.Information) != DialogResult.OK)
                return null;

            return UpdateSchoolsCsvFromPicker();
        }

        private void UpdateSchoolsCsv()
        {
            string savedPath = UpdateSchoolsCsvFromPicker();
            if (!string.IsNullOrWhiteSpace(savedPath))
                _status.Text = "Updated schools CSV source: " + savedPath;
        }

        private string UpdateSchoolsCsvFromPicker()
        {
            using var dlg = new OpenFileDialog
            {
                Title = "Choose updated schools.csv",
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                FileName = "schools.csv",
                InitialDirectory = SchoolTeamCsvCatalog.PreferredInitialDirectory()
            };
            if (dlg.ShowDialog(this) != DialogResult.OK)
                return null;

            try
            {
                var result = SchoolTeamCsvCatalog.InstallFrom(dlg.FileName);
                string updated = string.Join("\n", result.UpdatedPaths());
                MessageBox.Show(this,
                    "Updated saved schools.csv with " + result.SchoolCount + " school record(s). " +
                    "Only school identity and color fields were imported; logos and uniforms remain user-provided team assets.\n\n" + updated,
                    "Schools CSV Updated",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return result.RuntimePath;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Could not update schools CSV", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return null;
            }
        }

        private static string BuildScoreboardAbbreviation(SchoolTeamRecord school)
        {
            string source = !string.IsNullOrWhiteSpace(school.Name) ? school.Name : school.Mascot;
            string compact = new string((source ?? "").Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(compact))
                compact = "TEAM";
            return Team.Limit(compact, Team.MaxScoreboardAbbreviationLength);
        }

        private void SelectTeam(Guid teamId)
        {
            int index = _league.Teams.FindIndex(t => t.Id == teamId);
            if (index >= 0 && index < _teamList.Items.Count)
                _teamList.SelectedIndex = index;
        }

        private void RemoveTeam()
        {
            var team = SelectedTeam();
            if (team == null) return;
            if (MessageBox.Show(this, "Remove " + team.DisplayName + "?", "Remove team", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK) return;
            string photoDir = GetTeamPhotoDir(team, false);
            _league.Teams.Remove(team);
            foreach (var season in _league.Seasons)
                season.Games.RemoveAll(g => g.AwayTeamId == team.Id || g.HomeTeamId == team.Id);
            TryDeleteDirectory(photoDir);
            MarkDirty();
            RefreshAll();
        }

        private void RandomRoster()
        {
            var team = SelectedTeam();
            if (team == null) return;
            team.InjuredReserve?.Clear();
            Simulator.FillRandomRoster(team, _rng);
            EnsureTeamBaseLineup(team, recalculate: true);
            SaveTeamBaseLineupFile(team);
            MarkDirty();
            LoadSelectedTeam();
        }

        private void ImportSelectedTeamRosterFromLibrary()
        {
            var team = SelectedTeam();
            if (team == null)
            {
                MessageBox.Show(this, "Select a team first.");
                return;
            }

            TryImportRosterFromAssetLibrary(team, showMessages: true, confirmReplace: true);
        }

        private bool TryImportRosterFromAssetLibrary(Team team, bool showMessages, bool confirmReplace)
        {
            string rosterPath = FindLibraryRosterFile(team);
            if (string.IsNullOrWhiteSpace(rosterPath))
            {
                if (showMessages)
                    MessageBox.Show(this,
                        "No .xlsx roster file was found for " + team.DisplayName + " in the asset library.",
                        "Roster not found", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            if (confirmReplace && team.Roster?.Count > 0)
            {
                var confirm = MessageBox.Show(this,
                    "Replace the current roster for " + team.DisplayName + " with players from:\n\n" + rosterPath,
                    "Import roster", MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
                if (confirm != DialogResult.OK)
                    return false;
            }

            try
            {
                var result = RosterSpreadsheetImporter.Import(rosterPath, _rng);
                if (result.Players.Count == 0)
                {
                    if (showMessages)
                        MessageBox.Show(this, result.Message, "Roster import", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }

                team.Roster = result.Players;
                team.JvPool = result.JvPlayers;
                team.InjuredReserve = new List<Player>();
                EnsureTeamBaseLineup(team, recalculate: true);
                SaveTeamBaseLineupFile(team);
                MarkDirty();
                LoadSelectedTeam();
                _status.Text = result.Message;
                if (showMessages)
                    MessageBox.Show(this, result.Message, "Roster imported", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return true;
            }
            catch (Exception ex)
            {
                if (showMessages)
                    MessageBox.Show(this,
                        "Could not import roster workbook.\n\n" + ex.Message,
                        "Roster import failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
        }

        private void ManageBaseLineup()
        {
            var team = SelectedTeam();
            if (team == null)
            {
                MessageBox.Show(this, "Select a team first.");
                return;
            }

            EnsureTeamBaseLineup(team, recalculate: false);
            var eligible = (team.Roster ?? new List<Player>())
                .Where(p => p != null && InjuryEngine.IsAvailable(p) && !p.RedshirtActive)
                .OrderBy(p => p.Name)
                .ToList();
            if (eligible.Count < 9)
            {
                MessageBox.Show(this, "This team needs at least 9 eligible, non-redshirt players before a base lineup can be edited.");
                return;
            }

            using var form = new Form
            {
                Text = "Base Lineup - " + team.DisplayName,
                StartPosition = FormStartPosition.CenterParent,
                Size = new Size(860, 500),
                MinimizeBox = false,
                MaximizeBox = false
            };

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, Padding = new Padding(10) };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            form.Controls.Add(root);

            var top = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
            top.Controls.Add(new Label { Text = "Starting pitcher", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(0, 7, 6, 0) });
            var pitcherCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 };
            foreach (var player in eligible
                .Where(p => LineupEngine.CanAssignPosition(p, "P"))
                .OrderBy(p => LineupEngine.IsPenalizedPositionAssignment(p, "P") ? 1 : 0)
                .ThenByDescending(p => p.Pitching + p.Stamina))
                pitcherCombo.Items.Add(new PlayerChoice(player));
            top.Controls.Add(pitcherCombo);
            root.Controls.Add(top, 0, 0);

            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "order", HeaderText = "#", ReadOnly = true, FillWeight = 30 });
            var playerColumn = new DataGridViewComboBoxColumn { Name = "player", HeaderText = "Player", DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton, FillWeight = 170 };
            foreach (var player in eligible)
                playerColumn.Items.Add(new PlayerChoice(player));
            grid.Columns.Add(playerColumn);
            var positionColumn = new DataGridViewComboBoxColumn { Name = "position", HeaderText = "Position", DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton, FillWeight = 80 };
            foreach (string position in new[] { "C", "P", "1B", "2B", "3B", "SS", "LF", "CF", "RF", "DH" })
                positionColumn.Items.Add(position);
            grid.Columns.Add(positionColumn);
            grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "dh", HeaderText = "DH", FillWeight = 40 });
            root.Controls.Add(grid, 0, 1);

            var card = LineupEngine.BuildLineupCard(team);
            for (int i = 0; i < 9; i++)
            {
                var slot = card.BattingOrder.ElementAtOrDefault(i);
                int row = grid.Rows.Add(i + 1, ChoiceFor(slot?.Player, eligible), slot?.DefensivePosition ?? "", slot?.DesignatedHitter ?? false);
                grid.Rows[row].Tag = slot;
            }
            var starterChoice = ChoiceFor(card.StartingPitcher, eligible);
            if (starterChoice != null)
                pitcherCombo.SelectedItem = starterChoice;
            else if (pitcherCombo.Items.Count > 0)
                pitcherCombo.SelectedIndex = 0;

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            AddButton(buttons, "Cancel", (s, e) => form.Close());
            AddButton(buttons, "Recalculate", (s, e) =>
            {
                team.BaseLineup = LineupEngine.CreateBaseLineup(team);
                SaveTeamBaseLineupFile(team);
                MarkDirty();
                form.DialogResult = DialogResult.Retry;
                form.Close();
            });
            AddButton(buttons, "Save", (s, e) =>
            {
                string error = TryBuildEditedBaseLineup(team, grid, pitcherCombo, out TeamBaseLineup edited);
                if (!string.IsNullOrWhiteSpace(error))
                {
                    MessageBox.Show(form, error, "Invalid lineup", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                team.BaseLineup = edited;
                SaveTeamBaseLineupFile(team);
                MarkDirty();
                form.DialogResult = DialogResult.OK;
                form.Close();
            });
            root.Controls.Add(buttons, 0, 2);

            var result = form.ShowDialog(this);
            if (result == DialogResult.Retry)
                ManageBaseLineup();
        }

        private void ManagePitchingPlan()
        {
            var team = SelectedTeam();
            if (team == null)
            {
                MessageBox.Show(this, "Select a team first.");
                return;
            }

            EnsureTeamPitchingPlan(team, recalculate: false);
            var pitchers = (team.Roster ?? new List<Player>())
                .Where(p => p != null && InjuryEngine.IsAvailable(p) && !p.RedshirtActive && (p.Role == PlayerRole.Pitcher || LineupEngine.HasPosition(p, "P")))
                .OrderByDescending(PitchingRotationEngine.StarterScore)
                .ToList();
            if (pitchers.Count < 3)
            {
                MessageBox.Show(this, "This team needs at least 3 available, non-redshirt pitchers before a pitching rotation can be edited.");
                return;
            }

            using var form = new Form
            {
                Text = "Pitching Plan - " + team.DisplayName,
                StartPosition = FormStartPosition.CenterParent,
                Size = new Size(820, 560),
                MinimizeBox = false,
                MaximizeBox = false
            };

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1, Padding = new Padding(10) };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            form.Controls.Add(root);

            var top = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
            top.Controls.Add(new Label { Text = "Rotation size", AutoSize = true, Padding = new Padding(0, 8, 6, 0) });
            var rotationSizeCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 90 };
            foreach (int size in new[] { 3, 4, 5 }.Where(n => n <= pitchers.Count))
                rotationSizeCombo.Items.Add(size);
            team.PitchingPlan ??= new TeamPitchingPlan();
            int selectedRotationSize = Math.Clamp(team.PitchingPlan.RotationSize, 3, Math.Min(5, pitchers.Count));
            rotationSizeCombo.SelectedItem = rotationSizeCombo.Items.Contains(selectedRotationSize) ? selectedRotationSize : rotationSizeCombo.Items[rotationSizeCombo.Items.Count - 1];
            var penaltyLabel = new Label { AutoSize = true, Padding = new Padding(12, 8, 0, 0) };
            top.Controls.Add(rotationSizeCombo);
            top.Controls.Add(penaltyLabel);
            root.Controls.Add(top, 0, 0);

            var starterGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };
            starterGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "slot", HeaderText = "Start #", ReadOnly = true, FillWeight = 45 });
            var starterColumn = new DataGridViewComboBoxColumn { Name = "player", HeaderText = "Starter", DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton, FillWeight = 180 };
            foreach (var pitcher in pitchers)
                starterColumn.Items.Add(new PlayerChoice(pitcher));
            starterGrid.Columns.Add(starterColumn);
            root.Controls.Add(starterGrid, 0, 1);

            var bullpenGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };
            bullpenGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "player", HeaderText = "Bullpen Pitcher", ReadOnly = true, FillWeight = 160 });
            var roleColumn = new DataGridViewComboBoxColumn { Name = "role", HeaderText = "Role", DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton, FillWeight = 95 };
            foreach (var role in Enum.GetValues(typeof(BullpenRole)).Cast<BullpenRole>())
                roleColumn.Items.Add(role);
            bullpenGrid.Columns.Add(roleColumn);
            root.Controls.Add(bullpenGrid, 0, 2);

            void Populate()
            {
                int size = rotationSizeCombo.SelectedItem is int n ? n : 5;
                team.PitchingPlan.RotationSize = size;
                PitchingRotationEngine.NormalizePitchingPlan(team);
                UpdatePitchingPenaltyLabel(penaltyLabel, team);
                starterGrid.Rows.Clear();
                for (int i = 0; i < size; i++)
                {
                    var selected = pitchers.FirstOrDefault(p => i < team.PitchingPlan.StarterRotationIds.Count && p.Id == team.PitchingPlan.StarterRotationIds[i])
                        ?? pitchers.ElementAtOrDefault(i);
                    starterGrid.Rows.Add(i + 1, ChoiceFor(selected, pitchers));
                }

                var starterIds = new HashSet<Guid>(starterGrid.Rows.Cast<DataGridViewRow>()
                    .Select(r => (r.Cells["player"].Value as PlayerChoice)?.Player?.Id ?? Guid.Empty)
                    .Where(id => id != Guid.Empty));
                bullpenGrid.Rows.Clear();
                var roleMap = (team.PitchingPlan.BullpenRoles ?? new List<BullpenRoleAssignment>())
                    .GroupBy(r => r.PlayerId)
                    .ToDictionary(g => g.Key, g => g.First().Role);
                foreach (var pitcher in pitchers.Where(p => !starterIds.Contains(p.Id)).OrderByDescending(PitchingRotationEngine.RelieverScore))
                {
                    var role = roleMap.TryGetValue(pitcher.Id, out var assigned) ? assigned : BullpenRole.MiddleRelief;
                    int row = bullpenGrid.Rows.Add(pitcher.Name + " (" + pitcher.Positions + ")", role);
                    bullpenGrid.Rows[row].Tag = pitcher;
                }
            }

            rotationSizeCombo.SelectedIndexChanged += (s, e) =>
            {
                team.PitchingPlan = PitchingRotationEngine.CreatePitchingPlan(team, rotationSizeCombo.SelectedItem as int?);
                Populate();
            };
            Populate();

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            AddButton(buttons, "Cancel", (s, e) => form.Close());
            AddButton(buttons, "Auto Assign", (s, e) =>
            {
                team.PitchingPlan = PitchingRotationEngine.CreatePitchingPlan(team, rotationSizeCombo.SelectedItem as int?);
                Populate();
            });
            AddButton(buttons, "Save", (s, e) =>
            {
                string error = TryBuildEditedPitchingPlan(team, starterGrid, bullpenGrid, rotationSizeCombo, out var edited);
                if (!string.IsNullOrWhiteSpace(error) || edited == null)
                {
                    MessageBox.Show(form,
                        string.IsNullOrWhiteSpace(error) ? "The pitching plan could not be built." : error,
                        "Invalid pitching plan", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                team.PitchingPlan = edited;
                SaveTeamPitchingPlanFile(team);
                MarkDirty();
                form.DialogResult = DialogResult.OK;
                form.Close();
            });
            root.Controls.Add(buttons, 0, 3);

            form.ShowDialog(this);
        }

        private static void UpdatePitchingPenaltyLabel(Label label, Team team)
        {
            int pitchPenalty = PitchingRotationEngine.RotationPitchCountPenaltyPercent(team);
            int injuryBonus = PitchingRotationEngine.RotationInjuryRiskBonusPercent(team);
            label.Text = pitchPenalty == 0 && injuryBonus == 0
                ? "No rotation penalty."
                : "-" + pitchPenalty + "% max pitch count, +" + injuryBonus + "% injury risk.";
        }

        private static string TryBuildEditedPitchingPlan(Team team, DataGridView starterGrid, DataGridView bullpenGrid, ComboBox rotationSizeCombo, out TeamPitchingPlan? plan)
        {
            plan = null;
            int size = rotationSizeCombo.SelectedItem is int n ? n : 5;
            var starters = new List<Player>();
            foreach (DataGridViewRow row in starterGrid.Rows)
            {
                var choice = row.Cells["player"].Value as PlayerChoice;
                if (choice?.Player == null)
                    return "Every rotation slot must have a pitcher.";
                if (starters.Any(p => p.Id == choice.Player.Id))
                    return "A pitcher can only appear once in the starter rotation.";
                starters.Add(choice.Player);
            }

            if (starters.Count != size)
                return "The starter rotation must have exactly " + size + " pitchers.";

            plan = new TeamPitchingPlan
            {
                RotationSize = size,
                NextStarterSlot = Math.Clamp(team.PitchingPlan?.NextStarterSlot ?? 0, 0, Math.Max(0, size - 1)),
                LastCalculatedAt = DateTime.Now,
                StarterRotationIds = starters.Select(p => p.Id).ToList(),
                Status = "User edited pitching plan."
            };

            foreach (DataGridViewRow row in bullpenGrid.Rows)
            {
                if (row.Tag is not Player player)
                    continue;
                var role = row.Cells["role"].Value is BullpenRole assigned ? assigned : BullpenRole.MiddleRelief;
                plan.BullpenRoles.Add(new BullpenRoleAssignment { PlayerId = player.Id, PlayerName = player.Name, Role = role });
            }

            return "";
        }

        private sealed class PlayerChoice
        {
            public PlayerChoice(Player player) { Player = player; }
            public Player Player { get; }
            public override string ToString() => Player == null ? "" : Player.Name + " (" + Player.Positions + ")";
            public override bool Equals(object obj) => obj is PlayerChoice other && other.Player?.Id == Player?.Id;
            public override int GetHashCode() => Player?.Id.GetHashCode() ?? 0;
        }

        private static PlayerChoice? ChoiceFor(Player? player, IEnumerable<Player> choices)
        {
            if (player == null)
                return null;
            return choices.Select(p => new PlayerChoice(p)).FirstOrDefault(c => c.Player.Id == player.Id);
        }

        private static string TryBuildEditedBaseLineup(Team team, DataGridView grid, ComboBox pitcherCombo, out TeamBaseLineup lineup)
        {
            lineup = new TeamBaseLineup { LastCalculatedAt = DateTime.Now, Status = "User edited base lineup" };
            Player startingPitcher = (pitcherCombo.SelectedItem as PlayerChoice)?.Player;
            var usedBatters = new HashSet<Guid>();
            bool hasDh = false;

            foreach (DataGridViewRow row in grid.Rows)
            {
                Player player = (row.Cells["player"].Value as PlayerChoice)?.Player;
                string position = Convert.ToString(row.Cells["position"].Value) ?? "";
                bool dh = Convert.ToBoolean(row.Cells["dh"].Value ?? false) || position.Equals("DH", StringComparison.OrdinalIgnoreCase);
                if (player == null)
                    return "Every batting-order slot must have a player.";
                if (!usedBatters.Add(player.Id))
                    return "A player can only appear once in the batting order.";
                if (!InjuryEngine.IsAvailable(player) || player.RedshirtActive)
                    return player.Name + " is not eligible for the base lineup.";
                if (dh)
                {
                    hasDh = true;
                    position = "DH";
                }
                else if (string.IsNullOrWhiteSpace(position) || !LineupEngine.CanAssignPosition(player, position))
                {
                    return player.Name + " is not eligible at " + position + ".";
                }

                int order = Convert.ToInt32(row.Cells["order"].Value);
                lineup.BattingOrder.Add(new TeamBaseLineupSlot
                {
                    BattingOrder = order,
                    PlayerId = player.Id,
                    PlayerName = player.Name,
                    DefensivePosition = position,
                    DesignatedHitter = dh
                });
                if (!dh)
                    lineup.DefensiveAssignments[position] = player.Id;
            }

            if (startingPitcher != null)
                lineup.DefensiveAssignments["P"] = startingPitcher.Id;

            foreach (string position in new[] { "C", "P", "1B", "2B", "3B", "SS", "LF", "CF", "RF" })
            {
                if (!lineup.DefensiveAssignments.TryGetValue(position, out Guid playerId))
                    return "Missing defensive position: " + position + ".";
                Player assigned = team.Roster.FirstOrDefault(p => p.Id == playerId);
                if (assigned == null || !LineupEngine.CanAssignPosition(assigned, position) || assigned.RedshirtActive || !InjuryEngine.IsAvailable(assigned))
                    return "Invalid defensive assignment at " + position + ".";
            }

            lineup.HasDesignatedHitter = hasDh;
            lineup.StartingPitcherId = lineup.DefensiveAssignments.TryGetValue("P", out Guid starterId) ? starterId : null;
            lineup.DesignatedHitterId = lineup.BattingOrder.FirstOrDefault(s => s.DesignatedHitter)?.PlayerId;
            Guid? startingPitcherId = lineup.StartingPitcherId;
            bool pitcherInOrder = startingPitcherId.HasValue && lineup.BattingOrder.Any(s => s.PlayerId == startingPitcherId.Value);
            if (hasDh && pitcherInOrder)
                return "When DH is active, the pitcher cannot also appear in the batting order.";
            if (!hasDh && startingPitcherId.HasValue && !pitcherInOrder)
                return "Without a DH, the pitcher must be in the batting order.";
            return "";
        }

        private void ManageJvPool()
        {
            var team = SelectedTeam();
            if (team == null)
            {
                MessageBox.Show(this, "Select a team first.");
                return;
            }

            team.JvPool ??= new List<Player>();
            using var form = new Form
            {
                Text = "JV Pool - " + team.DisplayName,
                StartPosition = FormStartPosition.CenterParent,
                Size = new Size(820, 430),
                MinimizeBox = false,
                MaximizeBox = false
            };

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Padding = new Padding(10) };
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            form.Controls.Add(root);

            var grid = CreateReadOnlyGrid();
            AddGridColumn(grid, "Name", 180);
            AddGridColumn(grid, "Role", 80);
            AddGridColumn(grid, "Class", 95);
            AddGridColumn(grid, "Positions", 110);
            AddGridColumn(grid, "Pot", 55);
            AddGridColumn(grid, "Contact", 70);
            AddGridColumn(grid, "Power", 70);
            AddGridColumn(grid, "Speed", 70);
            AddGridColumn(grid, "Fielding", 70);
            AddGridColumn(grid, "Pitch", 70);
            AddGridColumn(grid, "Stamina", 70);
            root.Controls.Add(grid, 0, 0);

            void refresh()
            {
                grid.Rows.Clear();
                foreach (var player in team.JvPool.OrderBy(p => p.Role).ThenBy(p => p.Positions).ThenBy(p => p.Name))
                {
                    int row = grid.Rows.Add(player.Name, player.Role, player.Classification, player.Positions,
                        player.Potential, player.Contact, player.Power, player.Speed, player.Fielding, player.Pitching, player.Stamina);
                    grid.Rows[row].Tag = player;
                }
            }

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            AddButton(buttons, "Close", (s, e) => form.Close());
            AddButton(buttons, "Remove", (s, e) =>
            {
                if (grid.CurrentRow?.Tag is not Player player)
                    return;
                team.JvPool.Remove(player);
                MarkDirty();
                refresh();
            });
            AddButton(buttons, "Promote", (s, e) =>
            {
                if (grid.CurrentRow?.Tag is not Player player)
                    return;
                if (team.Roster.Count >= PlayerProgressionEngine.TargetRosterSize)
                {
                    MessageBox.Show(form, "Varsity roster is already at 30 players.");
                    return;
                }
                team.JvPool.Remove(player);
                PlayerProgressionEngine.PrepareJvCallUp(player, CurrentRosterManagementSeasonNumber(), _rng);
                team.Roster.Add(player);
                EnsureTeamBaseLineup(team, recalculate: true);
                SaveTeamBaseLineupFile(team);
                MarkDirty();
                refresh();
                LoadSelectedTeam();
            });
            root.Controls.Add(buttons, 0, 1);
            refresh();
            form.ShowDialog(this);
            LoadSelectedTeam();
        }

        private void OpenFieldEditor()
        {
            _league.CustomFields ??= new List<CustomBaseballField>();
            using var form = new FieldEditorDialog(
                _league,
                ImportCustomFieldAsset,
                GetTeamLogoChoices,
                ExistingAssetLibraryPath());
            form.ShowDialog(this);
            if (!form.Modified)
                return;

            MarkDirty();
            RefreshFieldPresetCombo();
            ApplyHomeTeamFieldSelection();
            _fieldPanel?.Invalidate();
            _status.Text = "Custom fields updated.";
        }

        private List<FieldEditorDialog.TeamLogoChoice> GetTeamLogoChoices()
        {
            return (_league?.Teams ?? new List<Team>())
                .Select(team => new FieldEditorDialog.TeamLogoChoice { Team = team, LogoPath = GetTeamLogoPath(team) })
                .Where(choice => !string.IsNullOrWhiteSpace(choice.LogoPath) && File.Exists(choice.LogoPath))
                .OrderBy(choice => choice.Team.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private string ImportCustomFieldAsset(string sourcePath, string fieldId)
        {
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
                throw new FileNotFoundException("Image source was not found.", sourcePath);
            if (!IsImageFile(sourcePath))
                throw new InvalidOperationException("Only image files can be used for custom fields.");
            if (!EnsureLeagueSavedForAssets())
                throw new InvalidOperationException("Save the league before importing custom field images.");

            string dir = GetCustomFieldAssetDir(fieldId, true);
            string dest = UniquePhotoPath(dir, Path.GetFileName(sourcePath));
            File.Copy(sourcePath, dest);
            return AssetPathResolver.ToPortablePath(dest);
        }

        private string GetCustomFieldAssetDir(string fieldId, bool create)
        {
            string clean = SanitizeFileName(string.IsNullOrWhiteSpace(fieldId) ? "custom-field" : fieldId);
            string dir = Path.Combine(GetAssetsDir(), "custom_fields", clean);
            if (create)
                Directory.CreateDirectory(dir);
            return dir;
        }

        private void ManageTeamHomeField()
        {
            var team = SelectedTeam();
            if (team == null)
            {
                MessageBox.Show(this, "Select a team first.");
                return;
            }

            using var form = new Form
            {
                Text = "Home Field - " + team.DisplayName,
                StartPosition = FormStartPosition.CenterParent,
                Size = new Size(560, 190),
                MinimizeBox = false,
                MaximizeBox = false
            };

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, Padding = new Padding(12) };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            form.Controls.Add(root);

            root.Controls.Add(new Label
            {
                Text = "Choose the home field used when this team is home.",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 0);

            var combo = new ComboBox { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList };
            foreach (var preset in AllFieldPresets())
                combo.Items.Add(preset);
            string selectedId = string.IsNullOrWhiteSpace(team.HomeFieldPresetId) ? BaseballFieldPresets.Default.Id : team.HomeFieldPresetId;
            for (int i = 0; i < combo.Items.Count; i++)
            {
                if (combo.Items[i] is BaseballFieldPreset item &&
                    string.Equals(item.Id, selectedId, StringComparison.OrdinalIgnoreCase))
                {
                    combo.SelectedIndex = i;
                    break;
                }
            }
            if (combo.SelectedIndex < 0 && combo.Items.Count > 0)
                combo.SelectedIndex = 0;
            root.Controls.Add(combo, 0, 1);

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            AddButton(buttons, "Cancel", (s, e) => form.Close());
            AddButton(buttons, "Save", (s, e) =>
            {
                if (combo.SelectedItem is BaseballFieldPreset preset)
                {
                    team.HomeFieldPresetId = preset.Id;
                    MarkDirty();
                    if (SelectedTeam(_homeCombo)?.Id == team.Id)
                        SelectFieldPreset(preset.Id);
                    _status.Text = "Home field set to " + preset.Name + " for " + team.DisplayName + ".";
                }
                form.Close();
            });
            root.Controls.Add(buttons, 0, 2);

            form.ShowDialog(this);
        }

        private string FindLibraryRosterFile(Team team)
        {
            string teamDir = FindAssetLibraryTeamDir(team);
            if (string.IsNullOrWhiteSpace(teamDir) || !Directory.Exists(teamDir))
                return null;

            var files = Directory.EnumerateFiles(teamDir, "*.xlsx", SearchOption.AllDirectories)
                .Where(p => !Path.GetFileName(p).StartsWith("~$", StringComparison.OrdinalIgnoreCase))
                .Select(p => new
                {
                    Path = p,
                    Score = RosterFileScore(p),
                    LastWrite = File.GetLastWriteTime(p)
                })
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .ThenByDescending(x => x.LastWrite)
                .ToList();

            return files.FirstOrDefault()?.Path;
        }

        private string FindAssetLibraryTeamDir(Team team)
        {
            string root = ExistingAssetLibraryPath();
            if (team == null || string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
                return null;

            string displayKey = NormalizeMatchKey(team.DisplayName);
            string cityMascotKey = NormalizeMatchKey((team.City + " " + team.Nickname).Trim());
            string nicknameKey = NormalizeMatchKey(team.Nickname);
            string cityKey = NormalizeMatchKey(team.City);
            var teamSearchRoots = new[] { root, Path.Combine(root, "Teams") }
                .Where(Directory.Exists)
                .Distinct(StringComparer.OrdinalIgnoreCase);
            var candidates = teamSearchRoots
                .SelectMany(Directory.EnumerateDirectories)
                .Select(dir =>
                {
                    string folderKey = NormalizeMatchKey(Path.GetFileName(dir));
                    int score = 0;
                    if (folderKey == displayKey || folderKey == cityMascotKey) score = 100;
                    else if (folderKey.Contains(cityMascotKey) || cityMascotKey.Contains(folderKey)) score = 85;
                    else if (!string.IsNullOrWhiteSpace(cityKey) && !string.IsNullOrWhiteSpace(nicknameKey) &&
                             folderKey.Contains(cityKey) && folderKey.Contains(nicknameKey)) score = 80;
                    else if (!string.IsNullOrWhiteSpace(cityKey) && folderKey.Contains(cityKey)) score = 45;
                    return new { Dir = dir, Score = score };
                })
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.Dir, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return candidates.FirstOrDefault()?.Dir;
        }

        private static int RosterFileScore(string path)
        {
            string key = NormalizeMatchKey(path);
            if (!key.Contains("roster") && !key.Contains("players"))
                return 0;
            int score = 10;
            if (key.Contains("baseball")) score += 50;
            if (key.Contains("roster")) score += 25;
            if (key.Contains("maxpreps")) score += 10;
            if (key.Contains("football")) score -= 50;
            return score;
        }

        private static string NormalizeMatchKey(string value)
            => new string((value ?? "").Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

        private void ManageTeamLogo()
        {
            var team = SelectedTeam();
            if (team == null) return;
            if (!EnsureLeagueSavedForAssets()) return;

            string dir = GetTeamLogoDir(team, true);
            using var dlg = new OpenFileDialog
            {
                Title = "Choose logo for " + team.DisplayName,
                Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All files (*.*)|*.*",
                Multiselect = false,
                InitialDirectory = ExistingAssetLibraryPath()
            };

            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                foreach (string existing in Directory.GetFiles(dir).Where(IsImageFile))
                    File.Delete(existing);

                string ext = Path.GetExtension(dlg.FileName);
                if (string.IsNullOrWhiteSpace(ext)) ext = ".png";
                string dest = Path.Combine(dir, "logo" + ext.ToLowerInvariant());
                File.Copy(dlg.FileName, dest, true);
                MarkDirty();
                _status.Text = SynchronizeSchoolCatalogLogo(team, dest, out string catalogMessage)
                    ? "Set logo for " + team.DisplayName + " and updated schools.csv."
                    : "Set logo for " + team.DisplayName + ". " + catalogMessage;
                RefreshDynastyGrid();
            }

            if (MessageBox.Show(this,
                    "Open this team's logo folder?\n\n" + dir,
                    "Team logo", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = dir,
                    UseShellExecute = true
                });
            }
        }

        private void ManageTeamUniforms()
        {
            var team = SelectedTeam();
            if (team == null) return;
            if (!EnsureLeagueSavedForAssets()) return;

            team.EnsureDefaultUniformSets();
            using var dlg = new TeamUniformEditorDialog(team, GetTeamUniformDir(team, true), ExistingAssetLibraryPath());
            if (dlg.ShowDialog(this) != DialogResult.OK)
                return;

            team.UniformSets = dlg.Uniforms.ToList();
            foreach (var uniform in team.UniformSets)
                uniform.ImagePath = NormalizeExistingAssetPath(uniform.ImagePath);
            MarkDirty();
            LoadSelectedTeam();
            _status.Text = "Saved " + team.UniformSets.Count + " uniform set(s) for " + team.DisplayName + ".";
        }

        private void ManageTeamScoreboardTemplate()
        {
            var team = SelectedTeam();
            if (team == null) return;

            team.ScoreboardTemplate ??= new TeamScoreboardTemplate();
            team.ScoreboardTemplate.Normalize(team);
            using var dlg = new TeamScoreboardTemplateDialog(team, GetTeamLogoPath(team) ?? "");
            dlg.ShowDialog(this);
            if (!dlg.Modified)
                return;

            MarkDirty();
            _fieldPanel?.Invalidate();
            _status.Text = "Home scoreboard template updated for " + team.DisplayName + ".";
        }

        private bool TryCopyTeamLogo(Team team, string sourcePath, out string message)
        {
            message = "";
            if (team == null || string.IsNullOrWhiteSpace(sourcePath))
                return false;

            if (!File.Exists(sourcePath))
            {
                message = "Logo source was not found.";
                return false;
            }

            if (!IsImageFile(sourcePath))
            {
                message = "Logo source is not a supported image file.";
                return false;
            }

            if (!EnsureLeagueSavedForAssets())
            {
                message = "Logo not copied because the league was not saved.";
                return false;
            }

            try
            {
                string dir = GetTeamLogoDir(team, true);
                foreach (string existing in Directory.GetFiles(dir).Where(IsImageFile))
                    File.Delete(existing);

                string ext = Path.GetExtension(sourcePath);
                if (string.IsNullOrWhiteSpace(ext)) ext = ".png";
                string dest = Path.Combine(dir, "logo" + ext.ToLowerInvariant());
                File.Copy(sourcePath, dest, true);
                SynchronizeSchoolCatalogLogo(team, dest, out _);
                return true;
            }
            catch (Exception ex)
            {
                message = "Logo could not be copied: " + ex.Message;
                return false;
            }
        }

        private bool SynchronizeSchoolCatalogLogo(Team team, string logoPath, out string message)
        {
            message = "";
            if (team == null || string.IsNullOrWhiteSpace(logoPath) || !File.Exists(logoPath))
            {
                message = "The team logo was not available for the school catalog.";
                return false;
            }

            try
            {
                if (string.IsNullOrWhiteSpace(team.CatalogSchoolName))
                    team.CatalogSchoolName = team.City;
                if (string.IsNullOrWhiteSpace(team.CatalogMascot))
                    team.CatalogMascot = team.Nickname;

                SchoolTeamCsvCatalog.UpdateTeamLogos(new[] { BuildSchoolLogoCatalogEntry(team, logoPath) });
                return true;
            }
            catch (Exception ex)
            {
                message = "schools.csv could not be updated: " + ex.Message;
                return false;
            }
        }

        private void SynchronizeAllSchoolCatalogLogos()
        {
            var entries = (_league?.Teams ?? new List<Team>())
                .Select(team => new { Team = team, LogoPath = GetTeamLogoPath(team) ?? "" })
                .Where(item => !string.IsNullOrWhiteSpace(item.LogoPath) && File.Exists(item.LogoPath))
                .Select(item =>
                {
                    if (string.IsNullOrWhiteSpace(item.Team.CatalogSchoolName))
                        item.Team.CatalogSchoolName = item.Team.City;
                    if (string.IsNullOrWhiteSpace(item.Team.CatalogMascot))
                        item.Team.CatalogMascot = item.Team.Nickname;
                    return BuildSchoolLogoCatalogEntry(item.Team, item.LogoPath);
                })
                .ToList();
            if (entries.Count > 0)
                SchoolTeamCsvCatalog.UpdateTeamLogos(entries);
        }

        private static SchoolLogoCatalogEntry BuildSchoolLogoCatalogEntry(Team team, string logoPath)
        {
            Color primary = Color.FromArgb(team.PrimaryArgb);
            Color secondary = Color.FromArgb(team.SecondaryArgb);
            return new SchoolLogoCatalogEntry
            {
                SchoolName = string.IsNullOrWhiteSpace(team.CatalogSchoolName) ? team.City : team.CatalogSchoolName,
                Mascot = string.IsNullOrWhiteSpace(team.CatalogMascot) ? team.Nickname : team.CatalogMascot,
                PrimaryColor = $"#{primary.R:X2}{primary.G:X2}{primary.B:X2}",
                SecondaryColor = $"#{secondary.R:X2}{secondary.G:X2}{secondary.B:X2}",
                SourceLogoPath = logoPath
            };
        }

        private void ManageTeamPhotos()
        {
            var team = SelectedTeam();
            if (team == null) return;
            if (!EnsureLeagueSavedForAssets()) return;

            string dir = GetTeamPhotoDir(team, true);
            using var dlg = new OpenFileDialog
            {
                Title = "Add scoreboard photos for " + team.DisplayName,
                Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All files (*.*)|*.*",
                Multiselect = true,
                InitialDirectory = ExistingAssetLibraryPath()
            };
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                int copied = 0;
                foreach (string source in dlg.FileNames)
                {
                    string dest = UniquePhotoPath(dir, Path.GetFileName(source));
                    File.Copy(source, dest);
                    copied++;
                }
                _status.Text = "Added " + copied + " photo(s) to " + dir;
            }

            if (MessageBox.Show(this,
                    "Open this team's photo folder?\n\n" + dir,
                    "Team photos", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = dir,
                    UseShellExecute = true
                });
            }
        }

        private void ManageTeamMusic()
        {
            var team = SelectedTeam();
            if (team == null) return;
            if (!EnsureLeagueSavedForAssets()) return;

            string playlistDir = GetTeamMusicDir(team, true);
            var selectedTracks = (team.TeamMusicPlaylist ?? new List<string>())
                .Select(AssetPathResolver.ResolveExistingFile)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .ToList();
            using var dlg = new TeamMusicPickerDialog(
                team.DisplayName,
                playlistDir,
                selectedTracks,
                ExistingAssetLibraryPath());
            if (dlg.ShowDialog(this) != DialogResult.OK)
                return;

            team.TeamMusicPlaylist = dlg.SelectedTracks
                .Select(AssetPathResolver.ResolveExistingFile)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(AssetPathResolver.ToPortablePath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            team.TeamMusicPath = team.TeamMusicPlaylist.FirstOrDefault() ?? "";
            MarkDirty();
            _currentTeamContextMusicPath = "";
            PlayTeamContextMusic(team);
            _status.Text = "Set " + team.TeamMusicPlaylist.Count + " team music track(s) for " + team.DisplayName + ".";
        }

        private void ClearTeamMusic()
        {
            var team = SelectedTeam();
            if (team == null) return;
            team.TeamMusicPath = "";
            team.TeamMusicPlaylist ??= new List<string>();
            team.TeamMusicPlaylist.Clear();
            MarkDirty();
            _currentTeamContextMusicPath = "";
            PlayTeamContextMusic(team);
            _status.Text = "Cleared team music for " + team.DisplayName + ". Default simulated-game music will be used.";
        }

        private void ManageTeamNationalAnthemImages()
        {
            var team = SelectedTeam();
            if (team == null) return;
            if (!EnsureLeagueSavedForAssets()) return;

            string dir = GetTeamNationalAnthemDir(team, true);
            using var dlg = new OpenFileDialog
            {
                Title = "Add national anthem images for " + team.DisplayName,
                Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All files (*.*)|*.*",
                Multiselect = true,
                InitialDirectory = ExistingAssetLibraryPath()
            };
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                int copied = 0;
                foreach (string source in dlg.FileNames)
                {
                    string dest = UniquePhotoPath(dir, Path.GetFileName(source));
                    File.Copy(source, dest);
                    copied++;
                }
                _status.Text = "Added " + copied + " national anthem image(s) to " + dir;
            }

            if (MessageBox.Show(this,
                    "Open this team's National Anthem folder?\n\n" + dir,
                    "National Anthem images", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = dir,
                    UseShellExecute = true
                });
            }
        }

        private void ManageTeamSprites()
        {
            var team = SelectedTeam();
            if (team == null) return;
            if (!EnsureLeagueSavedForAssets()) return;

            Player? player = _rosterGrid.CurrentRow?.Tag as Player;
            string dir = GetTeamSpriteDir(team, true);
            // The dialog uses a missing player as its team-only target mode.
            using var dlg = new SpriteCreatorDialog(team, player!, dir, ExistingAssetLibraryPath());
            if (dlg.ShowDialog(this) != DialogResult.OK || string.IsNullOrWhiteSpace(dlg.SavedSpriteSheetPath))
                return;

            if (dlg.SavedForPlayer && player != null)
            {
                player.SpriteSheetPath = AssetPathResolver.ToPortablePath(dlg.SavedSpriteSheetPath);
                _status.Text = "Saved sprite page for " + player.Name + ".";
            }
            else
            {
                team.SpriteSheetPath = AssetPathResolver.ToPortablePath(dlg.SavedSpriteSheetPath);
                _status.Text = "Saved team sprite page for " + team.DisplayName + ".";
            }

            MarkDirty();
            LoadSelectedTeam();

            if (MessageBox.Show(this,
                    "Open this team's sprite folder?\n\n" + dir,
                    "Team sprites", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = dir,
                    UseShellExecute = true
                });
            }
        }

        private void ManagePlayerAvatar()
        {
            var team = SelectedTeam();
            if (team == null) return;
            if (_rosterGrid.CurrentRow?.Tag is not Player player)
            {
                MessageBox.Show(this, "Select a player first.");
                return;
            }
            if (!EnsureLeagueSavedForAssets()) return;

            using var dlg = new OpenFileDialog
            {
                Title = "Choose avatar photo for " + player.Name,
                Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All files (*.*)|*.*",
                Multiselect = false,
                InitialDirectory = ExistingAssetLibraryPath()
            };
            if (dlg.ShowDialog(this) != DialogResult.OK)
                return;

            string dir = GetPlayerAvatarDir(team, player, true);
            foreach (string existing in Directory.GetFiles(dir).Where(IsImageFile))
                File.Delete(existing);

            string ext = Path.GetExtension(dlg.FileName);
            if (string.IsNullOrWhiteSpace(ext))
                ext = ".png";
            string dest = Path.Combine(dir, "avatar" + ext.ToLowerInvariant());
            File.Copy(dlg.FileName, dest, true);
            player.AvatarPath = AssetPathResolver.ToPortablePath(dest);
            MarkDirty();
            RefreshSelectedPlayerAvatar();
            _status.Text = "Saved avatar photo for " + player.Name + ".";
        }

        private bool EnsureLeagueSavedForAssets()
        {
            if (!string.IsNullOrEmpty(_path)) return true;
            MessageBox.Show(this,
                "Save the league first so team asset folders can live next to the league file.",
                "Save league first", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return SaveLeague(true);
        }

        private string GetAssetsDir()
        {
            return Path.Combine(
                Path.GetDirectoryName(_path) ?? ".",
                Path.GetFileNameWithoutExtension(_path) + ".assets");
        }

        private static string GetSharedTeamMusicPlaylistDir(bool create)
        {
            return create
                ? UserDataPaths.EnsureTeamMusicPlaylistDirectory()
                : UserDataPaths.TeamMusicPlaylistDirectory;
        }

        private string GetTeamPhotoDir(Team team, bool create)
        {
            if (string.IsNullOrEmpty(_path) || team == null) return null;
            string dir = Path.Combine(GetTeamAssetDir(team, create), "photos");
            if (create) Directory.CreateDirectory(dir);
            return dir;
        }

        private string GetTeamLogoDir(Team team, bool create)
        {
            if (string.IsNullOrEmpty(_path) || team == null) return null;
            string dir = Path.Combine(GetTeamAssetDir(team, create), "logo");
            if (create) Directory.CreateDirectory(dir);
            return dir;
        }

        private string GetTeamSpriteDir(Team team, bool create)
        {
            if (string.IsNullOrEmpty(_path) || team == null) return null;
            string dir = Path.Combine(GetTeamAssetDir(team, create), "sprites");
            if (create) Directory.CreateDirectory(dir);
            return dir;
        }

        private string GetTeamUniformDir(Team team, bool create)
        {
            if (string.IsNullOrEmpty(_path) || team == null) return null;
            string dir = Path.Combine(GetTeamAssetDir(team, create), "uniforms");
            if (create) Directory.CreateDirectory(dir);
            return dir;
        }

        private string GetPlayerAvatarDir(Team team, Player player, bool create)
        {
            if (string.IsNullOrEmpty(_path) || team == null || player == null) return null;
            string playerFolder = SanitizeFileName(player.Name) + "_" + player.Id.ToString("N").Substring(0, 8);
            string dir = Path.Combine(GetTeamAssetDir(team, create), "players", playerFolder, "avatar");
            if (create) Directory.CreateDirectory(dir);
            return dir;
        }

        private string ResolvePlayerAvatarPath(Player player)
        {
            if (player == null)
                return "";
            string storedAvatar = AssetPathResolver.ResolveExistingFile(player.AvatarPath);
            if (!string.IsNullOrWhiteSpace(storedAvatar))
                return storedAvatar;

            var team = SelectedTeam();
            string dir = GetPlayerAvatarDir(team, player, create: false);
            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
                return "";
            return Directory.GetFiles(dir).Where(IsImageFile).OrderBy(path => path).FirstOrDefault() ?? "";
        }

        private string GetTeamMusicDir(Team team, bool create)
        {
            if (team == null) return null;
            string dir = string.IsNullOrEmpty(_path)
                ? Path.Combine(UserDataPaths.UnsavedTeamAssetsDirectory, "Team Music", team.Id.ToString("N"))
                : Path.Combine(GetTeamAssetDir(team, create), "music");
            if (create) Directory.CreateDirectory(dir);
            return dir;
        }

        private string GetTeamNationalAnthemDir(Team team, bool create)
        {
            if (string.IsNullOrEmpty(_path) || team == null) return null;
            string dir = Path.Combine(GetTeamAssetDir(team, create), "National Anthem");
            if (create) Directory.CreateDirectory(dir);
            return dir;
        }

        private string GetTeamCutsceneDir(Team team, bool create)
        {
            if (string.IsNullOrEmpty(_path) || team == null) return null;
            string dir = Path.Combine(GetTeamAssetDir(team, create), "cutscenes");
            if (create)
            {
                Directory.CreateDirectory(dir);
                foreach (var folder in TeamCutsceneUniformFolders())
                    Directory.CreateDirectory(Path.Combine(dir, TeamCutsceneUniformFolderName(folder)));
            }
            return dir;
        }

        private static IReadOnlyList<TeamCutsceneUniformFolder> TeamCutsceneUniformFolders()
            => new[]
            {
                TeamCutsceneUniformFolder.Home,
                TeamCutsceneUniformFolder.HomeAlternate,
                TeamCutsceneUniformFolder.Visitor,
                TeamCutsceneUniformFolder.VisitorAlternate
            };

        private static string TeamCutsceneUniformFolderName(TeamCutsceneUniformFolder folder)
        {
            return folder switch
            {
                TeamCutsceneUniformFolder.Home => "HOME",
                TeamCutsceneUniformFolder.HomeAlternate => "HOME ALTERNATE",
                TeamCutsceneUniformFolder.Visitor => "VISITOR",
                TeamCutsceneUniformFolder.VisitorAlternate => "VISITOR ALTERNATE",
                _ => ""
            };
        }

        private string GetTeamBadgeDir(Team team, bool create)
        {
            if (string.IsNullOrEmpty(_path) || team == null) return null;
            string dir = Path.Combine(GetTeamAssetDir(team, create), "badges");
            if (create) Directory.CreateDirectory(dir);
            return dir;
        }

        private string GetTeamBadgeTemplateDir(Team team, bool create)
        {
            string badgeDir = GetTeamBadgeDir(team, create);
            if (string.IsNullOrEmpty(badgeDir)) return null;
            string dir = Path.Combine(badgeDir, "templates");
            if (create) Directory.CreateDirectory(dir);
            return dir;
        }

        private string GetTeamAssetDir(Team team, bool create)
        {
            string teamFolder = SanitizeFileName(team.ScoreboardName) + "_" + team.Id.ToString("N").Substring(0, 8);
            string dir = Path.Combine(GetAssetsDir(), "teams", teamFolder);
            if (create) Directory.CreateDirectory(dir);
            return dir;
        }

        private string GetTeamBaseLineupPath(Team team, bool create)
        {
            if (string.IsNullOrEmpty(_path) || team == null)
                return null;
            string dir = GetTeamAssetDir(team, create);
            return string.IsNullOrWhiteSpace(dir) ? null : Path.Combine(dir, "base_lineup.json");
        }

        private string GetTeamPitchingPlanPath(Team team, bool create)
        {
            if (string.IsNullOrEmpty(_path) || team == null)
                return null;
            string dir = GetTeamAssetDir(team, create);
            return string.IsNullOrWhiteSpace(dir) ? null : Path.Combine(dir, "pitching_rotation.json");
        }

        private void EnsureTeamBaseLineup(Team team, bool recalculate)
        {
            if (team == null)
                return;

            PrepareRedshirtsBeforeBaseLineup(team);
            team.BaseLineup ??= new TeamBaseLineup();
            team.BaseLineup.BattingOrder ??= new List<TeamBaseLineupSlot>();
            team.BaseLineup.DefensiveAssignments ??= new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
            if (recalculate || team.BaseLineup.BattingOrder.Count != 9 || team.BaseLineup.DefensiveAssignments.Count == 0)
                team.BaseLineup = LineupEngine.CreateBaseLineup(team);
        }

        private void EnsureTeamPitchingPlan(Team team, bool recalculate)
        {
            if (team == null)
                return;

            team.PitchingPlan ??= new TeamPitchingPlan();
            PitchingRotationEngine.NormalizePitchingPlan(team);
            int pitcherCount = (team.Roster ?? new List<Player>())
                .Count(p => p != null && InjuryEngine.IsAvailable(p) && !p.RedshirtActive && (p.Role == PlayerRole.Pitcher || LineupEngine.HasPosition(p, "P")));
            int required = Math.Min(Math.Clamp(team.PitchingPlan.RotationSize, 3, 5), pitcherCount);
            if (recalculate || pitcherCount >= 3 && team.PitchingPlan.StarterRotationIds.Count < required)
                team.PitchingPlan = PitchingRotationEngine.CreatePitchingPlan(team);
        }

        private static void PrepareRedshirtsBeforeBaseLineup(Team team)
        {
            team.Roster ??= new List<Player>();
            var active = team.Roster.Where(p => p != null && p.RedshirtActive).Take(5).ToHashSet();
            foreach (var player in team.Roster.Where(p => p != null))
            {
                if (player.RedshirtActive && !active.Contains(player))
                    player.RedshirtActive = false;
                if (player.RedshirtActive)
                    player.RedshirtUsed = true;
            }
        }

        private void SaveTeamBaseLineupFile(Team team)
        {
            string path = GetTeamBaseLineupPath(team, create: true);
            if (string.IsNullOrWhiteSpace(path))
                return;

            EnsureTeamBaseLineup(team, recalculate: false);
            File.WriteAllText(path, JsonSerializer.Serialize(team.BaseLineup, TeamFileJsonOptions));
        }

        private void SaveTeamPitchingPlanFile(Team team)
        {
            string path = GetTeamPitchingPlanPath(team, create: true);
            if (string.IsNullOrWhiteSpace(path))
                return;

            EnsureTeamPitchingPlan(team, recalculate: false);
            File.WriteAllText(path, JsonSerializer.Serialize(team.PitchingPlan, TeamFileJsonOptions));
        }

        private void SaveAllTeamBaseLineupFiles()
        {
            foreach (var team in _league?.Teams ?? Enumerable.Empty<Team>())
            {
                EnsureTeamBaseLineup(team, recalculate: false);
                SaveTeamBaseLineupFile(team);
            }
        }

        private void SaveAllTeamPitchingPlanFiles()
        {
            foreach (var team in _league?.Teams ?? Enumerable.Empty<Team>())
            {
                EnsureTeamPitchingPlan(team, recalculate: false);
                SaveTeamPitchingPlanFile(team);
            }
        }

        private void LoadAllTeamBaseLineupFiles()
        {
            foreach (var team in _league?.Teams ?? Enumerable.Empty<Team>())
            {
                string path = GetTeamBaseLineupPath(team, create: false);
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                {
                    try
                    {
                        team.BaseLineup = JsonSerializer.Deserialize<TeamBaseLineup>(File.ReadAllText(path), TeamFileJsonOptions) ?? new TeamBaseLineup();
                    }
                    catch
                    {
                        team.BaseLineup ??= new TeamBaseLineup();
                    }
                }
                EnsureTeamBaseLineup(team, recalculate: false);
            }
        }

        private void LoadAllTeamPitchingPlanFiles()
        {
            foreach (var team in _league?.Teams ?? Enumerable.Empty<Team>())
            {
                string path = GetTeamPitchingPlanPath(team, create: false);
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                {
                    try
                    {
                        team.PitchingPlan = JsonSerializer.Deserialize<TeamPitchingPlan>(File.ReadAllText(path), TeamFileJsonOptions) ?? new TeamPitchingPlan();
                    }
                    catch
                    {
                        team.PitchingPlan ??= new TeamPitchingPlan();
                    }
                }
                EnsureTeamPitchingPlan(team, recalculate: false);
            }
        }

        private void NormalizePortableAssetPaths()
        {
            if (string.IsNullOrWhiteSpace(_path) || _league == null)
                return;

            AssetPathResolver.SetLeagueFilePath(_path);

            foreach (var cutscene in _league.Cutscenes ?? new List<CutsceneDefinition>())
                cutscene.MediaPath = NormalizeExistingAssetPath(cutscene.MediaPath);

            foreach (var field in _league.CustomFields ?? new List<CustomBaseballField>())
            {
                field.BackgroundAssetPath = NormalizeExistingAssetPath(field.BackgroundAssetPath);
                foreach (var overlay in field.Overlays ?? new List<FieldImageOverlay>())
                    overlay.AssetPath = NormalizeExistingAssetPath(overlay.AssetPath);
            }

            foreach (var team in _league.Teams ?? new List<Team>())
            {
                team.SpriteSheetPath = NormalizeExistingAssetPath(team.SpriteSheetPath);
                team.ScoreboardTemplate ??= new TeamScoreboardTemplate();
                team.ScoreboardTemplate.BackgroundAssetPath = NormalizeExistingAssetPath(team.ScoreboardTemplate.BackgroundAssetPath);
                team.TeamMusicPlaylist = (team.TeamMusicPlaylist ?? new List<string>())
                    .Select(path => CopyTeamMusicToTeamAssets(team, path))
                    .Where(path => !string.IsNullOrWhiteSpace(path))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                team.TeamMusicPath = team.TeamMusicPlaylist.FirstOrDefault() ?? "";

                team.UniformSets ??= new List<TeamUniformSet>();
                foreach (var uniform in team.UniformSets)
                    uniform.ImagePath = NormalizeExistingAssetPath(uniform.ImagePath);

                foreach (var cutscene in team.Cutscenes ?? new List<CutsceneDefinition>())
                    cutscene.MediaPath = NormalizeExistingAssetPath(cutscene.MediaPath);

                foreach (var player in TeamPlayersIncludingPools(team))
                {
                    player.AvatarPath = NormalizeExistingAssetPath(player.AvatarPath);
                    player.SpriteSheetPath = NormalizeExistingAssetPath(player.SpriteSheetPath);
                }
            }
        }

        private string CopyTeamMusicToTeamAssets(Team team, string path)
        {
            string source = AssetPathResolver.ResolveExistingFile(path);
            if (team == null || string.IsNullOrWhiteSpace(source) || !IsAudioFile(source))
                return "";

            string dir = GetTeamMusicDir(team, true);
            if (string.IsNullOrWhiteSpace(dir))
                return AssetPathResolver.ToPortablePath(source);

            string fullDir = Path.GetFullPath(dir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string fullSource = Path.GetFullPath(source);
            if (fullSource.StartsWith(fullDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                fullSource.StartsWith(fullDir + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                return AssetPathResolver.ToPortablePath(fullSource);
            }

            string dest = UniquePhotoPath(dir, Path.GetFileName(source));
            File.Copy(source, dest);
            return AssetPathResolver.ToPortablePath(dest);
        }

        private static string NormalizeExistingAssetPath(string path)
        {
            string resolved = AssetPathResolver.ResolveExistingFile(path);
            return string.IsNullOrWhiteSpace(resolved)
                ? path ?? ""
                : AssetPathResolver.ToPortablePath(resolved);
        }

        private static IEnumerable<Player> TeamPlayersIncludingPools(Team team)
        {
            foreach (var player in team?.Roster ?? new List<Player>())
                if (player != null) yield return player;
            foreach (var player in team?.JvPool ?? new List<Player>())
                if (player != null) yield return player;
            foreach (var player in team?.InjuredReserve ?? new List<Player>())
                if (player != null) yield return player;
        }

        private static string UniquePhotoPath(string dir, string fileName)
        {
            string clean = SanitizeFileName(Path.GetFileNameWithoutExtension(fileName));
            string ext = Path.GetExtension(fileName);
            if (string.IsNullOrWhiteSpace(clean)) clean = "photo";
            string path = Path.Combine(dir, clean + ext);
            int n = 2;
            while (File.Exists(path))
                path = Path.Combine(dir, clean + "_" + n++ + ext);
            return path;
        }

        private static string SanitizeFileName(string value)
        {
            value = string.IsNullOrWhiteSpace(value) ? "team" : value.Trim();
            foreach (char c in Path.GetInvalidFileNameChars())
                value = value.Replace(c, '_');
            return value;
        }

        private static void TryDeleteDirectory(string dir)
        {
            try
            {
                if (string.IsNullOrEmpty(dir)) return;
                string parent = Directory.GetParent(dir)?.FullName;
                if (!string.IsNullOrEmpty(parent) && Directory.Exists(parent))
                    Directory.Delete(parent, true);
            }
            catch { }
        }

        private List<string> GetTeamPhotoPaths(Team team)
        {
            string dir = GetTeamPhotoDir(team, false);
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return new List<string>();
            return Directory.GetFiles(dir)
                .Where(IsImageFile)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private List<string> GetTeamNationalAnthemPaths(Team team)
        {
            string dir = GetTeamNationalAnthemDir(team, false);
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return new List<string>();
            return Directory.GetFiles(dir)
                .Where(IsImageFile)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private List<string> GetTeamBadgePaths(Team team)
        {
            string dir = GetTeamBadgeDir(team, false);
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return new List<string>();
            return Directory.GetFiles(dir, "*.png")
                .Where(path => !path.Contains(Path.DirectorySeparatorChar + "templates" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                .Where(IsImageFile)
                .OrderByDescending(p => p, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private string? GetTeamLogoPath(Team? team)
        {
            if (team == null)
                return null;
            string dir = GetTeamLogoDir(team, false);
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return null;
            return Directory.GetFiles(dir)
                .Where(IsImageFile)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
        }

        private void RefreshTeamBadges(Team? team)
        {
            if (_teamBadgesPanel == null)
                return;

            ClearTeamBadgeImages();
            _teamBadgesPanel.Controls.Clear();

            var paths = team == null ? new List<string>() : GetTeamBadgePaths(team);
            if (team == null || paths.Count == 0)
            {
                _teamBadgesPanel.Controls.Add(new Label
                {
                    Text = team == null ? "Select a team to view series badges." : "No series championship badges yet.",
                    AutoSize = false,
                    Width = 360,
                    Height = 90,
                    TextAlign = ContentAlignment.MiddleLeft,
                    ForeColor = Color.FromArgb(80, 84, 92)
                });
                return;
            }

            foreach (string path in paths)
            {
                try
                {
                    var image = Image.FromFile(path);
                    _teamBadgeImages.Add(image);
                    var box = new PictureBox
                    {
                        Image = image,
                        SizeMode = PictureBoxSizeMode.Zoom,
                        Width = 92,
                        Height = 92,
                        Margin = new Padding(4),
                        Cursor = Cursors.Hand,
                        Tag = path
                    };
                    _tips.SetToolTip(box, Path.GetFileNameWithoutExtension(path).Replace('_', ' '));
                    box.DoubleClick += (s, e) =>
                    {
                        string selectedPath = Convert.ToString((s as PictureBox)?.Tag);
                        if (!string.IsNullOrWhiteSpace(selectedPath) && File.Exists(selectedPath))
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = selectedPath, UseShellExecute = true });
                    };
                    _teamBadgesPanel.Controls.Add(box);
                }
                catch { }
            }
        }

        private void ClearTeamBadgeImages()
        {
            foreach (var image in _teamBadgeImages)
                image.Dispose();
            _teamBadgeImages.Clear();
        }

        private void ExportTeamBadgeStrip()
        {
            var team = SelectedTeam();
            if (team == null)
            {
                MessageBox.Show(this, "Select a team first.");
                return;
            }

            var badgePaths = GetTeamBadgePaths(team);
            if (badgePaths.Count == 0)
            {
                MessageBox.Show(this, team.DisplayName + " does not have any series badges to export yet.");
                return;
            }

            string defaultDir = GetTeamBadgeDir(team, true) ?? Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            using var dlg = new SaveFileDialog
            {
                Title = "Export badge strip for " + team.DisplayName,
                Filter = "PNG image (*.png)|*.png",
                InitialDirectory = Directory.Exists(defaultDir) ? defaultDir : Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                FileName = SanitizeFileName(team.DisplayName) + "_series_badges.png"
            };

            if (dlg.ShowDialog(this) != DialogResult.OK)
                return;

            try
            {
                using var page = RenderTeamBadgeStripPage(team, badgePaths);
                page.Save(dlg.FileName, ImageFormat.Png);
                _status.Text = "Exported badge strip page for " + team.DisplayName + ".";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Badge strip could not be exported: " + ex.Message, "Export badges", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private Bitmap RenderTeamBadgeStripPage(Team team, IReadOnlyList<string> badgePaths)
        {
            const int width = 1600;
            const int margin = 70;
            const int badgeSize = 250;
            const int labelHeight = 44;
            const int gap = 34;
            const int columns = 5;
            int rows = Math.Max(1, (int)Math.Ceiling(badgePaths.Count / (double)columns));
            int headerHeight = 190;
            int height = headerHeight + rows * (badgeSize + labelHeight + gap) + margin;

            var bitmap = new Bitmap(width, height);
            using var g = Graphics.FromImage(bitmap);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.White);

            Color primary = Color.FromArgb(team.PrimaryArgb);
            Color secondary = Color.FromArgb(team.SecondaryArgb);
            using (var headerBrush = new SolidBrush(primary))
                g.FillRectangle(headerBrush, 0, 0, width, 146);
            using (var accentBrush = new SolidBrush(secondary))
                g.FillRectangle(accentBrush, 0, 146, width, 12);

            DrawCenteredText(g, team.DisplayName.ToUpperInvariant(), new Rectangle(margin, 22, width - margin * 2, 62), primary, FontStyle.Bold, 52, 24);
            DrawCenteredText(g, "SERIES CHAMPIONSHIP BADGES", new Rectangle(margin, 84, width - margin * 2, 48), primary, FontStyle.Bold, 36, 18);

            using var dividerPen = new Pen(Color.FromArgb(220, 224, 230), 2);
            g.DrawLine(dividerPen, margin, headerHeight - 18, width - margin, headerHeight - 18);

            int startX = (width - (columns * badgeSize + (columns - 1) * gap)) / 2;
            for (int i = 0; i < badgePaths.Count; i++)
            {
                int row = i / columns;
                int col = i % columns;
                int x = startX + col * (badgeSize + gap);
                int y = headerHeight + row * (badgeSize + labelHeight + gap);
                var bounds = new Rectangle(x, y, badgeSize, badgeSize);

                using (var shadow = new SolidBrush(Color.FromArgb(28, Color.Black)))
                    g.FillRectangle(shadow, bounds.X + 5, bounds.Y + 7, bounds.Width, bounds.Height);
                using (var bg = new SolidBrush(Color.White))
                    g.FillRectangle(bg, bounds);
                using (var border = new Pen(Color.FromArgb(218, 222, 230), 2))
                    g.DrawRectangle(border, bounds);

                try
                {
                    using var badge = Image.FromFile(badgePaths[i]);
                    g.DrawImage(badge, FitImage(badge.Size, Rectangle.Inflate(bounds, -6, -6)));
                }
                catch
                {
                    using var errorBrush = new SolidBrush(Color.FromArgb(240, 240, 240));
                    g.FillRectangle(errorBrush, bounds);
                }

                string label = Path.GetFileNameWithoutExtension(badgePaths[i]).Replace('_', ' ');
                using var labelFont = new Font(Font.FontFamily, 9, FontStyle.Bold);
                TextRenderer.DrawText(
                    g,
                    label,
                    labelFont,
                    new Rectangle(x - 6, y + badgeSize + 8, badgeSize + 12, labelHeight - 8),
                    Color.FromArgb(44, 48, 56),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }

            return bitmap;
        }

        private void RefreshTeamHallOfFamePage(Team team)
        {
            if (_teamHallPicture == null)
                return;

            ClearTeamHallImage();
            if (team == null)
            {
                _teamHallPicture.Size = new Size(640, 90);
                _teamHallImage = RenderEmptyTeamHallMessage("Select a team to view its Hall of Fame page.");
                _teamHallPicture.Image = _teamHallImage;
                return;
            }

            var hallImage = RenderTeamHallOfFamePage(team);
            _teamHallImage = hallImage;
            _teamHallPicture.Image = hallImage;
            _teamHallPicture.Size = hallImage.Size;
        }

        private void ClearTeamHallImage()
        {
            if (_teamHallPicture != null)
                _teamHallPicture.Image = null;
            _teamHallImage?.Dispose();
            _teamHallImage = null;
        }

        private Bitmap RenderEmptyTeamHallMessage(string message)
        {
            var bitmap = new Bitmap(640, 90);
            using var g = Graphics.FromImage(bitmap);
            g.Clear(Color.White);
            using var font = new Font(Font.FontFamily, 11, FontStyle.Bold);
            TextRenderer.DrawText(g, message ?? "", font, new Rectangle(16, 16, 608, 58), Color.FromArgb(62, 68, 78), TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
            return bitmap;
        }

        private void ExportTeamHallOfFamePage()
        {
            var team = SelectedTeam(_hofTeamCombo) ?? SelectedTeam();
            if (team == null)
            {
                MessageBox.Show(this, "Select a team first.");
                return;
            }

            string defaultDir = GetTeamAssetDir(team, true) ?? Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            using var dlg = new SaveFileDialog
            {
                Title = "Export team Hall of Fame page for " + team.DisplayName,
                Filter = "PNG image (*.png)|*.png",
                InitialDirectory = Directory.Exists(defaultDir) ? defaultDir : Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                FileName = SanitizeFileName(team.DisplayName) + "_team_hall_of_fame.png"
            };

            if (dlg.ShowDialog(this) != DialogResult.OK)
                return;

            try
            {
                using var page = RenderTeamHallOfFamePage(team);
                page.Save(dlg.FileName, ImageFormat.Png);
                _status.Text = "Exported team Hall of Fame page for " + team.DisplayName + ".";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Team Hall of Fame page could not be exported: " + ex.Message, "Export team Hall", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private Bitmap RenderTeamHallOfFamePage(Team team)
        {
            const int width = 1500;
            const int margin = 58;
            var summaries = BuildTeamHallSeasonSummaries(team);
            var worldSeriesBadges = GetTeamBadgePaths(team)
                .Where(path => Path.GetFileNameWithoutExtension(path).Replace('_', ' ').IndexOf("world series", StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();
            var awardLines = BuildTeamHallAwardLines(team);
            var allStarLines = BuildTeamHallAllStarLines(team);
            var leaderLines = BuildTeamHallLeaderLines(team);

            int badgeRows = Math.Max(1, (int)Math.Ceiling(worldSeriesBadges.Count / 5.0));
            int seasonRows = Math.Max(1, summaries.Count);
            int awardRows = Math.Max(1, awardLines.Count);
            int allStarRows = Math.Max(1, allStarLines.Count);
            int leaderRows = Math.Max(1, leaderLines.Count);
            int height = 280 + 78 + badgeRows * 190 + 78 + seasonRows * 38 + 78 + awardRows * 30 + 78 + allStarRows * 30 + 78 + leaderRows * 30 + 90;

            var bitmap = new Bitmap(width, Math.Max(1040, height));
            using var g = Graphics.FromImage(bitmap);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.White);

            Color primary = Color.FromArgb(team.PrimaryArgb);
            Color secondary = Color.FromArgb(team.SecondaryArgb);
            Color textOnPrimary = ReadableTextColor(primary);
            Color bodyText = Color.FromArgb(40, 45, 54);
            using var primaryBrush = new SolidBrush(primary);
            using var secondaryBrush = new SolidBrush(secondary);
            using var lightBrush = new SolidBrush(Color.FromArgb(246, 248, 252));
            using var borderPen = new Pen(Color.FromArgb(214, 220, 230), 2);

            g.FillRectangle(primaryBrush, 0, 0, width, 196);
            g.FillRectangle(secondaryBrush, 0, 196, width, 16);
            DrawTeamHallLogo(g, team, new Rectangle(margin, 32, 132, 132), primary, secondary);
            using (var titleFont = new Font("Impact", 48, FontStyle.Regular, GraphicsUnit.Pixel))
                TextRenderer.DrawText(g, team.DisplayName.ToUpperInvariant(), titleFont, new Rectangle(214, 44, width - 310, 58), textOnPrimary, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            using (var subtitleFont = new Font(Font.FontFamily, 18, FontStyle.Bold))
                TextRenderer.DrawText(g, "TEAM HALL OF FAME", subtitleFont, new Rectangle(218, 104, width - 310, 34), textOnPrimary, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            using (var smallFont = new Font(Font.FontFamily, 11, FontStyle.Regular))
                TextRenderer.DrawText(g, "World Series badges, season history, player awards, All-Star selections, and yearly top-five team leaders.", smallFont, new Rectangle(220, 142, width - 310, 28), textOnPrimary, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            int y = 248;
            y = DrawTeamHallSectionHeader(g, "World Series Badges", y, margin, width, primary, secondary);
            var badgeArea = new Rectangle(margin, y, width - margin * 2, badgeRows * 190);
            g.FillRectangle(lightBrush, badgeArea);
            g.DrawRectangle(borderPen, badgeArea);
            if (worldSeriesBadges.Count == 0)
            {
                DrawTeamHallText(g, "No World Series badges yet.", new Rectangle(badgeArea.Left + 18, badgeArea.Top + 18, badgeArea.Width - 36, 38), bodyText, true);
            }
            else
            {
                const int badgeSize = 156;
                int gap = (badgeArea.Width - 5 * badgeSize) / 6;
                for (int i = 0; i < worldSeriesBadges.Count; i++)
                {
                    int row = i / 5;
                    int col = i % 5;
                    int x = badgeArea.Left + gap + col * (badgeSize + gap);
                    int by = badgeArea.Top + 18 + row * 190;
                    var bounds = new Rectangle(x, by, badgeSize, badgeSize);
                    using (var white = new SolidBrush(Color.White))
                        g.FillRectangle(white, bounds);
                    g.DrawRectangle(borderPen, bounds);
                    try
                    {
                        using var badge = Image.FromFile(worldSeriesBadges[i]);
                        g.DrawImage(badge, FitImage(badge.Size, Rectangle.Inflate(bounds, -4, -4)));
                    }
                    catch { }
                    string label = Path.GetFileNameWithoutExtension(worldSeriesBadges[i]).Replace('_', ' ');
                    DrawTeamHallText(g, label, new Rectangle(x - 12, by + badgeSize + 4, badgeSize + 24, 24), bodyText, false, TextFormatFlags.HorizontalCenter | TextFormatFlags.EndEllipsis);
                }
            }
            y += badgeArea.Height + 34;

            y = DrawTeamHallSectionHeader(g, "Season Results", y, margin, width, primary, secondary);
            var seasonArea = new Rectangle(margin, y, width - margin * 2, seasonRows * 38 + 12);
            g.FillRectangle(lightBrush, seasonArea);
            g.DrawRectangle(borderPen, seasonArea);
            if (summaries.Count == 0)
            {
                DrawTeamHallText(g, "No seasons created yet.", new Rectangle(seasonArea.Left + 18, seasonArea.Top + 10, seasonArea.Width - 36, 30), bodyText, true);
            }
            else
            {
                int rowY = seasonArea.Top + 8;
                foreach (var row in summaries)
                {
                    string text = "Season " + row.SeasonNumber + "    " + FormatRecord(row.Wins, row.Losses, row.Ties) + "    " + row.Finish;
                    if (!string.IsNullOrWhiteSpace(row.SeasonName))
                        text += "    " + row.SeasonName;
                    DrawTeamHallText(g, text, new Rectangle(seasonArea.Left + 18, rowY, seasonArea.Width - 36, 30), bodyText, true);
                    rowY += 38;
                }
            }
            y += seasonArea.Height + 34;

            y = DrawTeamHallLineSection(g, "Player Awards By Season", awardLines, y, margin, width, primary, secondary, bodyText, lightBrush, borderPen);
            y = DrawTeamHallLineSection(g, "All-Star Selections By Season", allStarLines, y + 8, margin, width, primary, secondary, bodyText, lightBrush, borderPen);
            DrawTeamHallLineSection(g, "Yearly Team Top-Five Stat Leaders", leaderLines, y + 8, margin, width, primary, secondary, bodyText, lightBrush, borderPen);

            return bitmap;
        }

        private void DrawTeamHallLogo(Graphics g, Team team, Rectangle bounds, Color primary, Color secondary)
        {
            using var white = new SolidBrush(Color.White);
            g.FillEllipse(white, bounds);
            using var ring = new Pen(secondary, 7);
            g.DrawEllipse(ring, bounds);

            string logoPath = GetTeamLogoPath(team);
            if (!string.IsNullOrWhiteSpace(logoPath) && File.Exists(logoPath))
            {
                try
                {
                    using var logo = Image.FromFile(logoPath);
                    g.DrawImage(logo, FitImage(logo.Size, Rectangle.Inflate(bounds, -12, -12)));
                    return;
                }
                catch { }
            }

            using var fallback = new SolidBrush(primary);
            g.FillEllipse(fallback, Rectangle.Inflate(bounds, -16, -16));
            DrawCenteredText(g, team.ScoreboardName, Rectangle.Inflate(bounds, -18, -18), primary, FontStyle.Bold, 46, 18, ReadableTextColor(primary));
        }

        private int DrawTeamHallSectionHeader(Graphics g, string title, int y, int margin, int width, Color primary, Color secondary)
        {
            using var font = new Font(Font.FontFamily, 17, FontStyle.Bold);
            using var brush = new SolidBrush(primary);
            g.FillRectangle(brush, margin, y, width - margin * 2, 44);
            using var accent = new SolidBrush(secondary);
            g.FillRectangle(accent, margin, y + 38, width - margin * 2, 6);
            TextRenderer.DrawText(g, title ?? "", font, new Rectangle(margin + 16, y + 4, width - margin * 2 - 32, 34), ReadableTextColor(primary), TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            return y + 52;
        }

        private int DrawTeamHallLineSection(Graphics g, string title, List<string> lines, int y, int margin, int width, Color primary, Color secondary, Color textColor, Brush background, Pen borderPen)
        {
            y = DrawTeamHallSectionHeader(g, title, y, margin, width, primary, secondary);
            int rows = Math.Max(1, lines.Count);
            var area = new Rectangle(margin, y, width - margin * 2, rows * 30 + 18);
            g.FillRectangle(background, area);
            g.DrawRectangle(borderPen, area);
            if (lines.Count == 0)
            {
                DrawTeamHallText(g, "No entries recorded yet.", new Rectangle(area.Left + 18, area.Top + 9, area.Width - 36, 30), textColor, true);
            }
            else
            {
                int rowY = area.Top + 9;
                foreach (string line in lines)
                {
                    DrawTeamHallText(g, line, new Rectangle(area.Left + 18, rowY, area.Width - 36, 26), textColor, false);
                    rowY += 30;
                }
            }
            return y + area.Height + 26;
        }

        private void DrawTeamHallText(Graphics g, string text, Rectangle bounds, Color color, bool bold, TextFormatFlags flags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis)
        {
            using var font = new Font(Font.FontFamily, bold ? 10.5f : 9.5f, bold ? FontStyle.Bold : FontStyle.Regular);
            TextRenderer.DrawText(g, text ?? "", font, bounds, color, flags);
        }

        private List<TeamHallSeasonSummary> BuildTeamHallSeasonSummaries(Team team)
        {
            var rows = new List<TeamHallSeasonSummary>();
            if (team == null || _league?.Seasons == null)
                return rows;

            for (int i = 0; i < _league.Seasons.Count; i++)
            {
                var season = _league.Seasons[i];
                var stats = TeamSeasonStats(season, team.Id);
                rows.Add(new TeamHallSeasonSummary
                {
                    SeasonNumber = i + 1,
                    SeasonName = season.Name,
                    Wins = stats.Wins,
                    Losses = stats.Losses,
                    Ties = stats.Ties,
                    Finish = TeamSeasonFinish(season, team)
                });
            }

            return rows;
        }

        private string TeamSeasonFinish(Season season, Team team)
        {
            if (season == null || team == null)
                return "";
            if (season.ChampionTeamId == team.Id)
                return "World Series Champions";

            var playedSeries = (season.Playoffs ?? new List<PlayoffSeries>())
                .Where(s => (s.TeamAId == team.Id || s.TeamBId == team.Id) && s.WinnerTeamId.HasValue)
                .OrderByDescending(s => s.Round)
                .ToList();
            var lostSeries = playedSeries.FirstOrDefault(s => s.WinnerTeamId.GetValueOrDefault() != team.Id);
            if (lostSeries != null)
                return PlayoffFinishText(lostSeries.RoundName);

            var wonSeries = playedSeries.FirstOrDefault(s => s.WinnerTeamId.GetValueOrDefault() == team.Id);
            if (wonSeries != null)
                return "Advanced through " + (string.IsNullOrWhiteSpace(wonSeries.RoundName) ? "Playoffs" : wonSeries.RoundName);

            bool hadGames = season.Games?.Any(g => g.AwayTeamId == team.Id || g.HomeTeamId == team.Id) == true;
            return hadGames ? "Regular Season" : "Not Started";
        }

        private static string PlayoffFinishText(string roundName)
        {
            string name = string.IsNullOrWhiteSpace(roundName) ? "Playoff" : roundName.Trim();
            if (string.Equals(name, "World Series", StringComparison.OrdinalIgnoreCase))
                return "World Series Finalists";
            if (name.EndsWith("Semi-Finals", StringComparison.OrdinalIgnoreCase))
                return name.Substring(0, name.Length - "Semi-Finals".Length) + "Semi-Finalists";
            if (name.EndsWith("Semi-Final", StringComparison.OrdinalIgnoreCase))
                return name.Substring(0, name.Length - "Semi-Final".Length) + "Semi-Finalists";
            if (name.EndsWith("Finals", StringComparison.OrdinalIgnoreCase))
                return name.Substring(0, name.Length - "Finals".Length) + "Finalists";
            if (name.EndsWith("Final", StringComparison.OrdinalIgnoreCase))
                return name.Substring(0, name.Length - "Final".Length) + "Finalists";
            if (name.IndexOf("Bi-District", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Bi-District Qualifier";
            return name + " Finalists";
        }

        private List<string> BuildTeamHallAwardLines(Team team)
        {
            var lines = new List<string>();
            if (team == null || _league?.Seasons == null)
                return lines;

            for (int i = 0; i < _league.Seasons.Count; i++)
            {
                var season = _league.Seasons[i];
                foreach (var award in (season.Awards ?? new List<SeasonAwardSelection>())
                    .Where(a => a.TeamId == team.Id)
                    .OrderBy(a => a.AwardName)
                    .ThenBy(a => a.Rank))
                {
                    string status = award.Winner ? "Winner" : "Finalist #" + award.Rank;
                    string detail = string.IsNullOrWhiteSpace(award.KeyStats) ? "" : " - " + award.KeyStats;
                    lines.Add("Season " + (i + 1) + ": " + award.AwardName + " - " + award.PlayerName + " (" + status + ")" + detail);
                }
            }

            return lines;
        }

        private List<string> BuildTeamHallAllStarLines(Team team)
        {
            var lines = new List<string>();
            if (team == null || _league?.Seasons == null)
                return lines;

            for (int i = 0; i < _league.Seasons.Count; i++)
            {
                var season = _league.Seasons[i];
                foreach (var selection in (season.AllStarSelections ?? new List<SeasonAllStarSelection>())
                    .Where(a => a.TeamId == team.Id)
                    .OrderByDescending(a => a.Starter)
                    .ThenBy(a => a.PlayerName))
                {
                    string starter = selection.Starter ? "Starter" : "Reserve";
                    string positions = string.IsNullOrWhiteSpace(selection.Positions) ? selection.Role.ToString() : selection.Positions;
                    lines.Add("Season " + (i + 1) + ": " + selection.PlayerName + " - " + starter + ", " + positions + ", " + selection.AllStarTeam);
                }
            }

            return lines;
        }

        private List<string> BuildTeamHallLeaderLines(Team team)
        {
            var lines = new List<string>();
            if (team == null || _league?.Seasons == null)
                return lines;

            for (int i = 0; i < _league.Seasons.Count; i++)
            {
                var season = _league.Seasons[i];
                var stats = PlayerSeasonStats(season, team);
                AddTeamHallLeaderLine(lines, i + 1, "Plate Appearances", stats.Where(s => s.PlateAppearances > 0), s => s.PlateAppearances, s => s.PlateAppearances.ToString());
                AddTeamHallLeaderLine(lines, i + 1, "Hits", stats.Where(s => s.H > 0), s => s.H, s => s.H.ToString());
                AddTeamHallLeaderLine(lines, i + 1, "Extra-Base Hits", stats.Where(s => s.ExtraBaseHits > 0), s => s.ExtraBaseHits, s => s.ExtraBaseHits.ToString());
                AddTeamHallLeaderLine(lines, i + 1, "Reached on Error", stats.Where(s => s.ReachedOnError > 0), s => s.ReachedOnError, s => s.ReachedOnError.ToString());
                AddTeamHallLeaderLine(lines, i + 1, "Home Runs", stats.Where(s => s.HR > 0), s => s.HR, s => s.HR.ToString());
                AddTeamHallLeaderLine(lines, i + 1, "RBI", stats.Where(s => s.RBI > 0), s => s.RBI, s => s.RBI.ToString());
                AddTeamHallLeaderLine(lines, i + 1, "Stolen Bases", stats.Where(s => s.SB > 0), s => s.SB, s => s.SB.ToString());
                AddTeamHallLeaderLine(lines, i + 1, "Strikeouts", stats.Where(s => s.K > 0), s => s.K, s => s.K.ToString());
                AddTeamHallLeaderLine(lines, i + 1, "Holds", stats.Where(s => s.Holds > 0), s => s.Holds, s => s.Holds.ToString());
                AddTeamHallLeaderLine(lines, i + 1, "Complete Games", stats.Where(s => s.CompleteGames > 0), s => s.CompleteGames, s => s.CompleteGames.ToString());
                AddTeamHallLeaderLine(lines, i + 1, "Shutouts", stats.Where(s => s.Shutouts > 0), s => s.Shutouts, s => s.Shutouts.ToString());
                AddTeamHallLeaderLine(lines, i + 1, "Defensive Total Chances", stats.Where(s => s.TotalChances > 0), s => s.TotalChances, s => s.TotalChances.ToString());
                AddTeamHallLeaderLine(lines, i + 1, "Catcher CS%", stats.Where(s => s.CatcherStealAttempts >= 5), s => s.CatcherCaughtStealingPercentage, s => s.CatcherCaughtStealingPercentage.ToString("0.0%"));
            }

            return lines;
        }

        private static void AddTeamHallLeaderLine(List<string> lines, int seasonNumber, string label, IEnumerable<PlayerSeasonStatLine> source, Func<PlayerSeasonStatLine, double> rank, Func<PlayerSeasonStatLine, string> value)
        {
            var top = source
                .OrderByDescending(rank)
                .ThenBy(s => s.PlayerName)
                .Take(5)
                .ToList();
            if (top.Count == 0)
                return;

            string list = string.Join("; ", top.Select((s, index) => (index + 1) + ". " + s.PlayerName + " (" + value(s) + ")"));
            lines.Add("Season " + seasonNumber + " " + label + " Top 5: " + list);
        }

        private void AwardSeriesChampionBadge(Season season, PlayoffSeries series)
        {
            if (season == null || series == null || !series.WinnerTeamId.HasValue || string.IsNullOrEmpty(_path))
                return;

            Team team = TeamById(series.WinnerTeamId.Value);
            if (team == null)
                return;

            int seasonNumber = CurrentSeasonNumber(season);
            string seriesName = string.IsNullOrWhiteSpace(series.RoundName) ? "Series" : series.RoundName;
            try
            {
                string badgeDir = GetTeamBadgeDir(team, true);
                string templateDir = GetTeamBadgeTemplateDir(team, true);
                if (string.IsNullOrWhiteSpace(badgeDir) || string.IsNullOrWhiteSpace(templateDir))
                    return;

                string fileBase = "season_" + seasonNumber.ToString("00") + "_" + SanitizeFileName(seriesName).ToLowerInvariant() + "_champions.png";
                string badgePath = Path.Combine(badgeDir, fileBase);
                string templatePath = Path.Combine(templateDir, SanitizeFileName(seriesName).ToLowerInvariant() + "_template.png");
                RenderSeriesChampionBadge(team, seriesName, seasonNumber, badgePath);
                if (!File.Exists(templatePath))
                    RenderSeriesChampionBadge(team, seriesName, seasonNumber, templatePath);
                if (SelectedTeam()?.Id == team.Id)
                    RefreshTeamBadges(team);
            }
            catch
            {
                // Badge generation should never block recording playoff results.
            }
        }

        private void RenderSeriesChampionBadge(Team team, string seriesName, int seasonNumber, string path)
        {
            const int size = 1200;
            using var bitmap = new Bitmap(size, size);
            using var g = Graphics.FromImage(bitmap);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.White);

            Color primary = Color.FromArgb(team.PrimaryArgb);
            Color secondary = Color.FromArgb(team.SecondaryArgb);
            Color lightBorder = Color.FromArgb(210, 214, 222);
            Color textOnPrimary = ReadableTextColor(primary);

            PointF center = new PointF(size / 2f, 560);
            PointF[] star = StarPoints(center, 585, 390, 5, -90);
            PointF[] starBorder = StarPoints(center, 625, 430, 5, -90);
            using (var borderBrush = new SolidBrush(lightBorder))
                g.FillPolygon(borderBrush, starBorder);
            using (var primaryBrush = new SolidBrush(primary))
                g.FillPolygon(primaryBrush, star);

            var body = new Rectangle(170, 330, 860, 520);
            using (var bodyBrush = new SolidBrush(primary))
                g.FillRoundedRectangle(bodyBrush, body, 36);
            using (var bodyPen = new Pen(lightBorder, 22))
                g.DrawRoundedRectangle(bodyPen, body, 36);

            var topBar = new Rectangle(300, 345, 600, 82);
            using (var barBrush = new SolidBrush(secondary))
                g.FillRoundedRectangle(barBrush, topBar, 10);
            DrawCenteredText(g, seriesName.ToUpperInvariant(), topBar, secondary, FontStyle.Bold, 54, 20);

            DrawCenteredText(g, "CHAMPIONS", new Rectangle(125, 420, 950, 285), primary, FontStyle.Bold, 150, 56, Color.White);

            var ribbon = new Rectangle(300, 680, 600, 110);
            using (var ribbonBrush = new SolidBrush(Color.White))
                g.FillRoundedRectangle(ribbonBrush, ribbon, 28);
            using (var ribbonPen = new Pen(secondary, 7))
                g.DrawRoundedRectangle(ribbonPen, ribbon, 28);
            string schoolLine = (team.City + " " + team.Nickname).Trim().ToUpperInvariant();
            DrawCenteredText(g, schoolLine, ribbon, Color.White, FontStyle.Bold, 48, 20, primary);

            var logoBounds = new Rectangle(472, 755, 256, 256);
            DrawBadgeLogo(g, team, logoBounds, primary, secondary);

            var seasonRect = new Rectangle(320, 1012, 560, 120);
            using (var shadowPen = new Pen(lightBorder, 12) { LineJoin = System.Drawing.Drawing2D.LineJoin.Round })
                DrawOutlinedText(g, "SEASON " + seasonNumber, seasonRect, FontStyle.Bold, 92, Color.FromArgb(214, 32, 32), shadowPen);
            using (var outlinePen = new Pen(Color.White, 5) { LineJoin = System.Drawing.Drawing2D.LineJoin.Round })
                DrawOutlinedText(g, "SEASON " + seasonNumber, seasonRect, FontStyle.Bold, 92, Color.FromArgb(214, 32, 32), outlinePen);

            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);
            bitmap.Save(path, ImageFormat.Png);
        }

        private void DrawBadgeLogo(Graphics g, Team team, Rectangle bounds, Color primary, Color secondary)
        {
            using var shadow = new SolidBrush(Color.FromArgb(70, Color.Black));
            g.FillEllipse(shadow, bounds.Left + 8, bounds.Top + 10, bounds.Width, bounds.Height);
            using var white = new SolidBrush(Color.White);
            g.FillEllipse(white, bounds);
            using var ring = new Pen(secondary, 12);
            g.DrawEllipse(ring, bounds);

            string logoPath = GetTeamLogoPath(team);
            if (!string.IsNullOrWhiteSpace(logoPath) && File.Exists(logoPath))
            {
                try
                {
                    using var logo = Image.FromFile(logoPath);
                    Rectangle dest = FitImage(logo.Size, Rectangle.Inflate(bounds, -20, -20));
                    using var clipPath = new System.Drawing.Drawing2D.GraphicsPath();
                    clipPath.AddEllipse(Rectangle.Inflate(bounds, -14, -14));
                    var previousClip = g.Clip;
                    g.SetClip(clipPath);
                    g.DrawImage(logo, dest);
                    g.Clip = previousClip;
                    previousClip.Dispose();
                    return;
                }
                catch { }
            }

            using var logoBrush = new SolidBrush(primary);
            g.FillEllipse(logoBrush, Rectangle.Inflate(bounds, -28, -28));
            DrawCenteredText(g, team.ScoreboardName, Rectangle.Inflate(bounds, -32, -32), primary, FontStyle.Bold, 60, 20, ReadableTextColor(primary));
        }

        private static PointF[] StarPoints(PointF center, float outerRadius, float innerRadius, int points, float startAngleDegrees)
        {
            var result = new PointF[points * 2];
            double angle = startAngleDegrees * Math.PI / 180.0;
            double step = Math.PI / points;
            for (int i = 0; i < result.Length; i++)
            {
                double radius = i % 2 == 0 ? outerRadius : innerRadius;
                result[i] = new PointF(
                    center.X + (float)(Math.Cos(angle) * radius),
                    center.Y + (float)(Math.Sin(angle) * radius));
                angle += step;
            }
            return result;
        }

        private void DrawCenteredText(Graphics g, string text, Rectangle bounds, Color background, FontStyle style, int maxSize, int minSize)
            => DrawCenteredText(g, text, bounds, background, style, maxSize, minSize, ReadableTextColor(background));

        private void DrawCenteredText(Graphics g, string text, Rectangle bounds, Color background, FontStyle style, int maxSize, int minSize, Color foreColor)
        {
            using var brush = new SolidBrush(foreColor);
            using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter };
            using var font = FitFont(g, text, bounds, style, maxSize, minSize);
            g.DrawString(text ?? "", font, brush, bounds, format);
        }

        private void DrawOutlinedText(Graphics g, string text, Rectangle bounds, FontStyle style, int maxSize, Color fill, Pen outline)
        {
            using var font = FitFont(g, text, bounds, style, maxSize, 28);
            using var path = new System.Drawing.Drawing2D.GraphicsPath();
            using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            path.AddString(text ?? "", font.FontFamily, (int)style, g.DpiY * font.Size / 72f, bounds, format);
            g.DrawPath(outline, path);
            using var brush = new SolidBrush(fill);
            g.FillPath(brush, path);
        }

        private Font FitFont(Graphics g, string text, Rectangle bounds, FontStyle style, int maxSize, int minSize)
        {
            string value = string.IsNullOrWhiteSpace(text) ? " " : text;
            for (int size = maxSize; size >= minSize; size -= 2)
            {
                var font = new Font("Impact", size, style, GraphicsUnit.Pixel);
                SizeF measured = g.MeasureString(value, font, bounds.Width);
                if (measured.Width <= bounds.Width * 0.98f && measured.Height <= bounds.Height * 0.98f)
                    return font;
                font.Dispose();
            }
            return new Font(Font.FontFamily, minSize, style, GraphicsUnit.Pixel);
        }

        private Image CreateDynastyLogoImage(Team team)
        {
            const int width = 72;
            const int height = 54;
            var bitmap = new Bitmap(width, height);
            using var g = Graphics.FromImage(bitmap);
            g.Clear(Color.FromArgb(24, 28, 36));
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            string logoPath = GetTeamLogoPath(team);
            if (!string.IsNullOrWhiteSpace(logoPath) && File.Exists(logoPath))
            {
                try
                {
                    using var source = Image.FromFile(logoPath);
                    var dest = FitImage(source.Size, new Rectangle(4, 4, width - 8, height - 8));
                    g.DrawImage(source, dest);
                    return bitmap;
                }
                catch
                {
                    g.Clear(Color.FromArgb(24, 28, 36));
                }
            }

            Color primary = Color.FromArgb(team.PrimaryArgb);
            Color secondary = Color.FromArgb(team.SecondaryArgb);
            using var primaryBrush = new SolidBrush(primary);
            using var secondaryPen = new Pen(secondary, 4);
            g.FillRectangle(primaryBrush, 0, 0, width, height);
            g.DrawRectangle(secondaryPen, 2, 2, width - 4, height - 4);
            using var font = new Font(Font.FontFamily, 15, FontStyle.Bold);
            TextRenderer.DrawText(g, team.ScoreboardName, font, new Rectangle(0, 0, width, height), ReadableTextColor(primary),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            return bitmap;
        }

        private static bool IsImageFile(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            return ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp" || ext == ".gif";
        }

        private static bool IsAudioFile(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            return ext == ".mp3" || ext == ".wav" || ext == ".wma";
        }

        private void AddPlayer()
        {
            var team = SelectedTeam();
            if (team == null) return;
            team.Roster.Add(Simulator.RandomPlayer(_rng, PlayerRole.Batter, "New Player"));
            EnsureTeamBaseLineup(team, recalculate: true);
            SaveTeamBaseLineupFile(team);
            MarkDirty();
            LoadSelectedTeam();
        }

        private void RemovePlayer()
        {
            var team = SelectedTeam();
            if (team == null || _rosterGrid.CurrentRow?.Tag is not Player p) return;
            team.Roster.Remove(p);
            EnsureTeamBaseLineup(team, recalculate: true);
            SaveTeamBaseLineupFile(team);
            MarkDirty();
            LoadSelectedTeam();
        }

        private void SaveRosterCell(DataGridViewCellEventArgs e)
        {
            if (_suppress || e.RowIndex < 0 || _rosterGrid.Rows[e.RowIndex].Tag is not Player p) return;
            var row = _rosterGrid.Rows[e.RowIndex];
            string editedColumn = e.ColumnIndex >= 0 && e.ColumnIndex < _rosterGrid.Columns.Count
                ? _rosterGrid.Columns[e.ColumnIndex].Name
                : "";
            bool lineupEligibilityChanged = editedColumn == "role" ||
                editedColumn == "positions" ||
                editedColumn == "injurystatus" ||
                editedColumn == "injurygames" ||
                editedColumn == "redshirtactive";
            p.Name = Convert.ToString(row.Cells["name"].Value) ?? "";
            if (Enum.TryParse(Convert.ToString(row.Cells["role"].Value), out PlayerRole role)) p.Role = role;
            if (Enum.TryParse(Convert.ToString(row.Cells["classification"].Value), out PlayerClassification classification) &&
                classification != PlayerClassification.Unassigned)
            {
                p.Classification = classification;
                if (p.InitialClassification == PlayerClassification.Unassigned)
                    p.InitialClassification = classification;
            }
            p.Positions = NormalizePositions(Convert.ToString(row.Cells["positions"].Value), p.Role);
            p.Bats = NormalizeHandedness(Convert.ToString(row.Cells["bats"].Value), allowSwitch: true, fallback: "R");
            p.Throws = NormalizeHandedness(Convert.ToString(row.Cells["throws"].Value), allowSwitch: false, fallback: "R");
            p.CareerPitchCount = p.Role == PlayerRole.Pitcher
                ? Math.Max(1, ClampPitchCountCell(row.Cells["careerpitchcount"].Value))
                : Math.Max(0, ClampPitchCountCell(row.Cells["careerpitchcount"].Value));
            p.Potential = ClampCell(row.Cells["potential"].Value);
            p.WorkEthic = ClampCell(row.Cells["workethic"].Value);
            p.Durability = ClampCell(row.Cells["durability"].Value);
            p.RegressionRisk = ClampCell(row.Cells["regressionrisk"].Value);
            if (Enum.TryParse(Convert.ToString(row.Cells["injurystatus"].Value), out PlayerInjuryStatus injuryStatus))
                p.InjuryStatus = injuryStatus;
            p.InjuryName = Convert.ToString(row.Cells["injuryname"].Value) ?? "";
            p.InjuryGamesRemaining = ClampCell(row.Cells["injurygames"].Value);
            p.InjuryMissedGamesThisSeason = ClampCell(row.Cells["injurymissed"].Value);
            p.MedicalTag = Convert.ToBoolean(row.Cells["medicaltag"].Value ?? false);
            bool redshirtRequested = Convert.ToBoolean(row.Cells["redshirtactive"].Value ?? false);
            if (!ApplyRedshirtSelection(p, redshirtRequested))
            {
                row.Cells["redshirtactive"].Value = p.RedshirtActive;
                return;
            }
            row.Cells["redshirtused"].Value = p.RedshirtUsed;
            if (!TrySaveUniformOverride(row, "jersey", value => p.JerseyArgbOverride = value) ||
                !TrySaveUniformOverride(row, "pants", value => p.PantsArgbOverride = value) ||
                !TrySaveUniformOverride(row, "caphelmet", value => p.CapHelmetArgbOverride = value))
            {
                row.Cells["jersey"].Value = NullableColorHex(p.JerseyArgbOverride);
                row.Cells["pants"].Value = NullableColorHex(p.PantsArgbOverride);
                row.Cells["caphelmet"].Value = NullableColorHex(p.CapHelmetArgbOverride);
                return;
            }
            if (p.InjuryStatus == PlayerInjuryStatus.Healthy)
            {
                p.InjuryName = "";
                p.InjuryGamesRemaining = 0;
                p.InjurySeverity = 0;
            }
            else if (p.InjurySeverity <= 0)
            {
                p.InjurySeverity = p.InjuryStatus == PlayerInjuryStatus.DayToDay ? 1 : 2;
            }
            SavePitchProfileCells(p, row);
            p.Contact = ClampCell(row.Cells["contact"].Value);
            p.Power = ClampCell(row.Cells["power"].Value);
            p.Speed = ClampCell(row.Cells["speed"].Value);
            p.StealAggression = ClampCell(row.Cells["stealaggression"].Value);
            p.BaseRunning = ClampCell(row.Cells["baserunning"].Value);
            p.Fielding = ClampCell(row.Cells["fielding"].Value);
            p.HoldRunner = ClampCell(row.Cells["holdrunner"].Value);
            p.Pickoff = ClampCell(row.Cells["pickoff"].Value);
            p.DeliveryTime = ClampCell(row.Cells["deliverytime"].Value);
            p.ArmStrength = ClampCell(row.Cells["armstrength"].Value);
            p.PopTime = ClampCell(row.Cells["poptime"].Value);
            p.Accuracy = ClampCell(row.Cells["accuracy"].Value);
            p.TagRating = ClampCell(row.Cells["tagrating"].Value);
            p.Pitching = ClampCell(row.Cells["pitching"].Value);
            p.Stamina = ClampCell(row.Cells["stamina"].Value);
            row.Cells["overall"].Value = p.Overall;
            if (lineupEligibilityChanged)
            {
                var team = SelectedTeam();
                EnsureTeamBaseLineup(team, recalculate: true);
                SaveTeamBaseLineupFile(team);
            }
            MarkDirty();
        }

        private static void SavePitchProfileCells(Player player, DataGridViewRow row)
        {
            PitchProfileEngine.NormalizePlayerPitchProfiles(player);
            foreach (var pitch in PitchProfileEngine.AllPitchTypes)
            {
                string key = PitchColumnKey(pitch);
                var profile = player.PitchArsenal.First(p => p.PitchType == pitch);
                profile.Enabled = Convert.ToBoolean(row.Cells[key + "_enabled"].Value ?? false);
                profile.Effectiveness = ClampCell(row.Cells[key + "_effectiveness"].Value);
            }

            if (!player.PitchArsenal.Any(p => p.Enabled))
            {
                if (PitchProfileEngine.IsPitcherClassified(player))
                {
                    var fastball = player.PitchArsenal.First(p => p.PitchType == GameplayPitchType.Fastball);
                    fastball.Enabled = true;
                    row.Cells[PitchColumnKey(GameplayPitchType.Fastball) + "_enabled"].Value = true;
                }
            }
            PitchProfileEngine.EnsurePitcherMinimumArsenal(player, new Random());
            foreach (var pitch in PitchProfileEngine.AllPitchTypes)
            {
                string key = PitchColumnKey(pitch);
                var profile = player.PitchArsenal.First(p => p.PitchType == pitch);
                row.Cells[key + "_enabled"].Value = profile.Enabled;
                row.Cells[key + "_effectiveness"].Value = profile.Effectiveness;
            }

            player.PitchStrengths = PitchProfileEngine.ParsePitchList(Convert.ToString(row.Cells["pitchstrengths"].Value));
            player.PitchWeaknesses = PitchProfileEngine.ParsePitchList(Convert.ToString(row.Cells["pitchweaknesses"].Value))
                .Where(p => !player.PitchStrengths.Contains(p))
                .ToList();
            row.Cells["pitchstrengths"].Value = PitchProfileEngine.PitchListText(player.PitchStrengths);
            row.Cells["pitchweaknesses"].Value = PitchProfileEngine.PitchListText(player.PitchWeaknesses);
            row.Cells["pitchscout"].Value = PitchProfileEngine.ArsenalScoutSummary(player);
        }

        private static string PitchColumnKey(GameplayPitchType pitch)
            => "pitch_" + PitchProfileEngine.ShortName(pitch).ToLowerInvariant();

        private static int ClampCell(object value)
        {
            int.TryParse(Convert.ToString(value), out int n);
            return n < 0 ? 0 : n > 99 ? 99 : n;
        }

        private static int ClampPitchCountCell(object value)
        {
            int.TryParse(Convert.ToString(value), out int n);
            return Math.Clamp(n, 0, 200);
        }

        private static string NormalizePositions(string? value, PlayerRole role)
        {
            value = (value ?? "").Trim().ToUpperInvariant().Replace(",", "/").Replace(" ", "");
            while (value.Contains("//"))
                value = value.Replace("//", "/");
            value = value.Trim('/');
            return string.IsNullOrWhiteSpace(value)
                ? (role == PlayerRole.Pitcher ? "P" : "DH")
                : value;
        }

        private bool ApplyRedshirtSelection(Player player, bool requested)
        {
            if (!requested)
            {
                player.RedshirtActive = false;
                return true;
            }

            if (player.RedshirtUsed && !player.RedshirtActive)
            {
                MessageBox.Show(this, "This player has already used a redshirt season.");
                return false;
            }

            if (!player.RedshirtActive &&
                player.VarsityCallUpSeasonNumber > 0 &&
                player.VarsityCallUpSeasonNumber == CurrentRosterManagementSeasonNumber())
            {
                MessageBox.Show(this, "A player called up during the current season can be redshirted in a later season, but not during the call-up season.");
                return false;
            }

            var team = SelectedTeam();
            int activeCount = team?.Roster.Count(p => p.RedshirtActive && p.Id != player.Id) ?? 0;
            if (!player.RedshirtActive && activeCount >= 5)
            {
                MessageBox.Show(this, "Only 5 players can be redshirted for a team in a season.");
                return false;
            }

            player.RedshirtActive = true;
            player.RedshirtUsed = true;
            return true;
        }

        private void EnsureDevelopmentFields(Player player)
        {
            if (player.Potential <= 0) player.Potential = Simulator.RandomDevelopmentRating(_rng, 40, 95);
            if (player.WorkEthic <= 0) player.WorkEthic = Simulator.RandomDevelopmentRating(_rng, 30, 95);
            if (player.Durability <= 0) player.Durability = Simulator.RandomDevelopmentRating(_rng, 35, 95);
            if (player.RegressionRisk <= 0) player.RegressionRisk = Simulator.RandomDevelopmentRating(_rng, 5, 55);
            if (player.Fielding <= 0) player.Fielding = Simulator.RandomDevelopmentRating(_rng, 35, 95);
        }

        private void EnsureStealDefenseFields(Player player)
        {
            if (player.StealAggression <= 0) player.StealAggression = Simulator.RandomDevelopmentRating(_rng, 20, 90);
            if (player.BaseRunning <= 0) player.BaseRunning = Simulator.RandomDevelopmentRating(_rng, 30, 95);
            if (player.HoldRunner <= 0) player.HoldRunner = Simulator.RandomDevelopmentRating(_rng, player.Role == PlayerRole.Pitcher ? 30 : 10, player.Role == PlayerRole.Pitcher ? 95 : 55);
            if (player.Pickoff <= 0) player.Pickoff = Simulator.RandomDevelopmentRating(_rng, player.Role == PlayerRole.Pitcher ? 25 : 10, player.Role == PlayerRole.Pitcher ? 90 : 45);
            if (player.DeliveryTime <= 0) player.DeliveryTime = Simulator.RandomDevelopmentRating(_rng, player.Role == PlayerRole.Pitcher ? 30 : 10, player.Role == PlayerRole.Pitcher ? 95 : 50);
            if (player.ArmStrength <= 0) player.ArmStrength = Simulator.RandomDevelopmentRating(_rng, 30, 95);
            if (player.PopTime <= 0) player.PopTime = Simulator.RandomDevelopmentRating(_rng, 30, 95);
            if (player.Accuracy <= 0) player.Accuracy = Simulator.RandomDevelopmentRating(_rng, 30, 95);
            if (player.TagRating <= 0) player.TagRating = Simulator.RandomDevelopmentRating(_rng, 30, 95);
        }

        private void EnsureHandednessFields(Player player)
        {
            if (string.IsNullOrWhiteSpace(player.Bats))
                player.Bats = Simulator.RandomBatSide(_rng);
            if (string.IsNullOrWhiteSpace(player.Throws))
                player.Throws = Simulator.RandomThrowSide(_rng, player.Role);
        }

        private void EnsurePitchCountFields(Player player)
        {
            if (player.Role == PlayerRole.Pitcher && player.CareerPitchCount <= 0)
                player.CareerPitchCount = Simulator.RandomCareerPitchCount(_rng);
        }

        private static string NormalizeHandedness(string? value, bool allowSwitch, string fallback)
        {
            string normalized = (value ?? "").Trim().ToUpperInvariant();
            if (normalized.StartsWith("L")) return "L";
            if (normalized.StartsWith("R")) return "R";
            if (allowSwitch && normalized.StartsWith("S")) return "S";
            return fallback;
        }

        private void AddSeason()
        {
            int year = _league.Seasons.Count == 0 ? DateTime.Now.Year : _league.Seasons.Max(s => s.Year) + 1;
            var season = new Season { Year = year, Name = year + " Season" };
            GenerateSchedule(season, showErrors: false);
            _league.Seasons.Add(season);
            MarkDirty();
            RefreshSeasonCombos();
            RefreshSeasonViews();
            RefreshScheduleCombo();
        }

        private void GenerateScheduleForSelectedSeason()
        {
            var season = SelectedSeason(_seasonCombo);
            if (season == null)
            {
                MessageBox.Show(this, "Select a season first.");
                return;
            }

            if (season.Games.Count > 0)
            {
                var confirm = MessageBox.Show(this,
                    "Regenerating the schedule will keep completed results, but pending planned games will be replaced.\n\nContinue?",
                    "Generate schedule", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);
                if (confirm != DialogResult.OK)
                    return;
            }

            if (!GenerateSchedule(season, showErrors: true))
                return;

            MarkDirty();
            RefreshSeasonViews();
            RefreshScheduleCombo();
        }

        private bool GenerateSchedule(Season season, bool showErrors)
        {
            _league.Rules ??= new LeagueRules();
            _league.Rules.Schedule ??= new SeasonScheduleRules();
            season.Schedule = ScheduleGenerator.Generate(_league, _league.Rules.Schedule, out string error);
            if (error != null)
            {
                season.Schedule.Clear();
                if (showErrors)
                    MessageBox.Show(this, error, "Could not generate schedule", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            return true;
        }

        private RankingGameModifier RankingModifierForGame(Season? season, Team away, Team home)
            => RankingGameModifier.FromSeason(season, away, home);

        private bool ValidateGameStart(Season? season, ScheduledGame? scheduled, Team away, Team home, bool enforceScheduleOrder = true)
        {
            if (!LineupEngine.TryValidateForGame(away, out string awayError))
            {
                MessageBox.Show(this, awayError, "Invalid lineup", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            if (!LineupEngine.TryValidateForGame(home, out string homeError))
            {
                MessageBox.Show(this, homeError, "Invalid lineup", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            if (!enforceScheduleOrder || season == null || scheduled == null)
            {
                LineupEngine.RegisterPositionExperience(away, _rng);
                LineupEngine.RegisterPositionExperience(home, _rng);
                return true;
            }

            var next = (season.Schedule ?? new List<ScheduledGame>())
                .Where(g => !g.PlayedGameId.HasValue)
                .OrderBy(g => g.GameNumber)
                .ThenBy(g => g.Week)
                .FirstOrDefault();
            if (next == null || next.Id == scheduled.Id)
            {
                LineupEngine.RegisterPositionExperience(away, _rng);
                LineupEngine.RegisterPositionExperience(home, _rng);
                return true;
            }

            MessageBox.Show(this,
                "Scheduled games must be completed in order. The next game is " + ScheduleLabel(next) + ".",
                "Schedule order", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return false;
        }

        private void SimulateGame()
        {
            var scheduled = SelectedScheduledGame();
            var away = scheduled == null ? SelectedTeam(_awayCombo) : TeamById(scheduled.AwayTeamId);
            var home = scheduled == null ? SelectedTeam(_homeCombo) : TeamById(scheduled.HomeTeamId);
            if (away == null || home == null || away.Id == home.Id)
            {
                MessageBox.Show(this, "Pick two different teams.");
                return;
            }
            var season = SelectedSeason(_commitSeasonCombo);
            EnsureScoutingMessagesForSeries(season, scheduled);
            var mutationSnapshot = TeamMutationSnapshot.Capture(_league, away, home);
            InjuryEngine.ProcessGameInjuries(away, home, _rng);
            if (!ValidateGameStart(season, scheduled, away, home))
            {
                mutationSnapshot.Restore(_league);
                RefreshAll();
                return;
            }
            var awayUniform = SelectedAwayUniform(away, scheduled);
            var homeUniform = SelectedHomeUniform(home, scheduled);
            var run = SimulatedGameEngine.SimulateDetailed(_league, away, home, _rng, RankingModifierForGame(season, away, home));
            var result = run.Result;
            _lastGame = result;
            ApplyUniformSelectionToResult(result, awayUniform, homeUniform);
            _simResult.Text = ScoreboardLine(away, result.AwayScore, home, result.HomeScore);
            LoadScoreboardPhotos(away, home);
            _teamContextMusic.Stop();
            _currentTeamContextMusicPath = "";
            using var live = new LiveSimulationForm(run, away, home, home?.ScoreboardTemplate?.BackgroundAssetPath ?? "", GetTeamLogoPath(home) ?? "");
            live.ShowDialog(this);
            if (live.CommitRequested && season != null)
            {
                bool saved = CommitGameResult(season, scheduled, result);
                _status.Text = saved
                    ? "Committed and autosaved simulation: " + ScoreboardLine(away!, result.AwayScore, home!, result.HomeScore)
                    : "Simulation committed in memory, but autosave was canceled or failed. Use File > Save.";
            }
            else
            {
                mutationSnapshot.Restore(_league);
                RefreshAll();
                _status.Text = live.CommitRequested
                    ? "No season was selected; simulation changes were restored."
                    : "Dismissed simulation; team and player changes were restored.";
            }
            _fieldPanel.Invalidate();
        }

        private void SimSeason()
        {
            var season = SelectedSeason(_commitSeasonCombo) ?? SelectedSeason(_seasonCombo);
            if (season == null)
            {
                MessageBox.Show(this, "Select a season first.");
                return;
            }
            if (season.Schedule == null || season.Schedule.Count == 0)
            {
                MessageBox.Show(this, "Generate a season schedule first.");
                return;
            }

            var pending = season.Schedule
                .Where(g => !g.PlayedGameId.HasValue)
                .OrderBy(g => g.GameNumber)
                .ThenBy(g => g.Week)
                .ToList();
            if (pending.Count == 0)
            {
                MessageBox.Show(this, "There are no pending scheduled games.");
                return;
            }

            int completed = 0;
            foreach (var scheduled in pending)
            {
                var away = TeamById(scheduled.AwayTeamId);
                var home = TeamById(scheduled.HomeTeamId);
                if (away == null || home == null)
                    continue;
                InjuryEngine.ProcessGameInjuries(away, home, _rng);
                if (!ValidateGameStart(season, scheduled, away, home, enforceScheduleOrder: false))
                {
                    if (completed > 0)
                        AutosaveCommittedChanges("Season simulation");
                    return;
                }

                EnsureScoutingMessagesForSeries(season, scheduled);
                var result = Simulator.SimGame(_league, away, home, _rng, RankingModifierForGame(season, away, home));
                ApplyUniformSelectionToResult(
                    result,
                    GameUniformResolver.ResolveUniform(away, homeRole: false, scheduled.AwayUniformSetId, scheduled, scheduled.GameNumber, season.Schedule, _league?.Rules?.RotateSavedUniforms ?? true, scheduled.AwayUniformAutoCategory),
                    GameUniformResolver.ResolveUniform(home, homeRole: true, scheduled.HomeUniformSetId, scheduled, scheduled.GameNumber, season.Schedule, _league?.Rules?.RotateSavedUniforms ?? true, scheduled.HomeUniformAutoCategory));
                CommitGameResult(season, scheduled, result, refresh: false, showChampion: false, autoSave: false);
                completed++;
            }

            bool saved = completed == 0 || AutosaveCommittedChanges("Season simulation");
            _status.Text = saved
                ? "Simulated and autosaved " + completed + " scheduled game(s)."
                : "Simulated " + completed + " scheduled game(s), but autosave was canceled or failed. Use File > Save.";
            if (completed == 0)
                RefreshAll();
        }

        private bool CommitGameResult(
            Season? season,
            ScheduledGame? scheduled,
            GameResult? result,
            bool refresh = true,
            bool showChampion = true,
            bool autoSave = true)
        {
            if (season == null || result == null)
                return false;

            if (result.AwayCoachId == Guid.Empty)
            {
                var away = TeamById(result.AwayTeamId);
                if (away != null)
                {
                    EnsureTeamCoaches(away);
                    result.AwayCoachId = away.CoachId;
                }
            }
            if (result.HomeCoachId == Guid.Empty)
            {
                var home = TeamById(result.HomeTeamId);
                if (home != null)
                {
                    EnsureTeamCoaches(home);
                    result.HomeCoachId = home.CoachId;
                }
            }

            AppendInjuryMissedGameLines(result, TeamById(result.AwayTeamId), TeamById(result.HomeTeamId));

            if (scheduled != null)
            {
                result.ScheduledGameId = scheduled.Id;
                if (string.IsNullOrWhiteSpace(result.GameType))
                    result.GameType = scheduled.Type.ToString();
                if (string.IsNullOrWhiteSpace(result.GameMode))
                    result.GameMode = "Scheduled";
                scheduled.PlayedGameId = result.Id;
            }
            season.Games.Add(result);
            PitchingRotationEngine.UpdateSeasonPitcherUsage(season, TeamById(result.AwayTeamId), result);
            PitchingRotationEngine.UpdateSeasonPitcherUsage(season, TeamById(result.HomeTeamId), result);
            Team? newChampion = null;
            PlayoffSeries? championshipSeries = null;
            if (ApplyCommittedResultToPlayoffSeries(season, result, out var champion, out var series))
            {
                newChampion = champion;
                championshipSeries = series;
            }
            AdvancePlayoffBracket(season);
            AddGameInboxMessages(season, scheduled, result);
            MarkDirty();
            if (refresh)
            {
                RefreshSeasonViews();
                RefreshScheduleCombo();
                RefreshInboxGrid();
                RefreshRecordsBookGrid();
            }
            bool saved = !autoSave || AutosaveCommittedChanges("Game result");
            if (showChampion && newChampion != null && championshipSeries != null)
                ShowChampionshipDialog(season, newChampion, championshipSeries);
            return saved;
        }

        private bool AutosaveCommittedChanges(string operation)
        {
            if (!_dirty)
                return true;

            bool saved = SaveLeague(false);
            if (!saved)
                _status.Text = operation + " committed in memory, but autosave was canceled or failed. Use File > Save.";
            return saved;
        }

        private static void ApplyUniformSelectionToResult(GameResult result, TeamUniformSet? awayUniform, TeamUniformSet? homeUniform)
        {
            if (result == null)
                return;
            result.AwayUniformSetId = awayUniform?.Id;
            result.HomeUniformSetId = homeUniform?.Id;
            result.AwayUniformName = awayUniform?.Name ?? "";
            result.HomeUniformName = homeUniform?.Name ?? "";
        }

        private static void AppendInjuryMissedGameLines(GameResult result, params Team[] teams)
        {
            if (result == null)
                return;
            result.Lines ??= new List<PlayerGameLine>();
            var appeared = result.Lines.Select(line => line.PlayerId).ToHashSet();
            foreach (Team team in teams.Where(team => team != null))
            {
                var irIds = (team.InjuredReserve ?? new List<Player>()).Select(player => player.Id).ToHashSet();
                foreach (Player player in (team.Roster ?? Enumerable.Empty<Player>())
                    .Concat(team.InjuredReserve ?? Enumerable.Empty<Player>()))
                {
                    if (appeared.Contains(player.Id) ||
                        !irIds.Contains(player.Id) && (player.InjuryStatus != PlayerInjuryStatus.Out || player.InjuryGamesRemaining <= 0))
                        continue;
                    result.Lines.Add(new PlayerGameLine
                    {
                        TeamId = team.Id,
                        PlayerId = player.Id,
                        PlayerName = player.Name,
                        Pitcher = player.Role == PlayerRole.Pitcher,
                        Classification = player.Classification,
                        InitialClassification = player.InitialClassification == PlayerClassification.Unassigned
                            ? player.Classification
                            : player.InitialClassification,
                        GamesMissedInjury = 1
                    });
                }
            }
        }

        private void AddGameInboxMessages(Season? season, ScheduledGame? scheduled, GameResult? result)
        {
            if (_league == null || season == null || result == null)
                return;
            EnsureInbox();
            if (_league.InboxMessages.Any(m => m.GameResultId == result.Id && string.Equals(m.Category, "Game Report", StringComparison.OrdinalIgnoreCase)))
                return;

            var away = TeamById(result.AwayTeamId);
            var home = TeamById(result.HomeTeamId);
            if (away == null || home == null)
                return;

            EnsureTeamCoaches(away);
            EnsureTeamCoaches(home);
            int seasonNumber = CurrentSeasonNumber(season);
            string context = GameContextLabel(scheduled, result);
            var playerOfGame = PlayerOfGame(result);
            bool important = result.IsPlayoff || Math.Abs(result.AwayScore - result.HomeScore) <= 1;

            _league.InboxMessages.Add(CreateGameInboxMessage(season, seasonNumber, scheduled, result, away, home, away, result.AwayCoachId, context, playerOfGame, important));
            _league.InboxMessages.Add(CreateGameInboxMessage(season, seasonNumber, scheduled, result, away, home, home, result.HomeCoachId, context, playerOfGame, important));
        }

        private void EnsureScoutingMessagesForSeries(Season? season, ScheduledGame? scheduled)
        {
            if (_league == null || season == null || scheduled == null)
                return;
            var away = TeamById(scheduled.AwayTeamId);
            var home = TeamById(scheduled.HomeTeamId);
            if (away == null || home == null)
                return;

            EnsureInbox();
            EnsureTeamCoaches(away);
            EnsureTeamCoaches(home);
            EnsureTeamBaseLineup(away, recalculate: false);
            EnsureTeamBaseLineup(home, recalculate: false);
            EnsureTeamPitchingPlan(away, recalculate: false);
            EnsureTeamPitchingPlan(home, recalculate: false);

            bool created = false;
            created |= AddScoutingMessageIfMissing(season, scheduled, recipient: away, opponent: home);
            created |= AddScoutingMessageIfMissing(season, scheduled, recipient: home, opponent: away);
            if (created)
            {
                MarkDirty();
                RefreshInboxGrid();
            }
        }

        private bool AddScoutingMessageIfMissing(Season season, ScheduledGame scheduled, Team recipient, Team opponent)
        {
            string key = ScoutingReferenceKey(season, scheduled, recipient.Id);
            if (_league.InboxMessages.Any(m => string.Equals(m.ReferenceKey, key, StringComparison.OrdinalIgnoreCase)))
                return false;

            var coach = recipient.Coaches?.FirstOrDefault(c => c.Id == recipient.CoachId)
                ?? recipient.Coaches?.FirstOrDefault(c => c.Active)
                ?? recipient.Coaches?.FirstOrDefault();
            string coachName = coach?.Name ?? recipient.CoachName ?? "Coach";
            int gamesInSeries = ScheduledSeriesGames(season, scheduled).Count;
            string context = GameContextLabel(scheduled, null);
            string subject = "Scouting: " + opponent.ScoreboardName + " series - Week " + scheduled.Week;

            var body = new StringBuilder();
            body.AppendLine("Coach " + coachName + ",");
            body.AppendLine();
            body.AppendLine("Advance scouting report for the upcoming " + context + " series.");
            body.AppendLine("Opponent: " + opponent.DisplayName + " (" + opponent.ScoreboardName + ")");
            body.AppendLine("Series: " + gamesInSeries + " game(s), Week " + scheduled.Week + ", opens " + scheduled.DayLabel);
            body.AppendLine("Opponent record: " + TeamRecordText(season, opponent.Id));
            body.AppendLine("Opponent ranking: " + TeamRankingText(season, opponent.Id));
            body.AppendLine("Opponent coach: " + OpponentCoachText(opponent));
            body.AppendLine();
            body.AppendLine("Projected Opponent Lineup");
            foreach (var line in ProjectedLineupLines(opponent))
                body.AppendLine(line);
            body.AppendLine();
            body.AppendLine("Pitching Matchup");
            body.AppendLine("Our expected starter: " + PitcherScoutText(ExpectedStarter(recipient), recipient));
            body.AppendLine("Opponent expected starter: " + PitcherScoutText(ExpectedStarter(opponent), opponent));
            body.AppendLine();
            body.AppendLine("Potential Issues");
            foreach (var issue in ScoutingIssueLines(season, recipient, opponent))
                body.AppendLine("- " + issue);
            body.AppendLine();
            body.AppendLine("Top Opponent Threats");
            foreach (var threat in TopOpponentThreats(season, opponent))
                body.AppendLine("- " + threat);
            body.AppendLine();
            body.AppendLine("This scouting report is sent once before the series begins. It uses the saved base lineup, pitching rotation, current season stats, rankings, and coach tendencies.");

            _league.InboxMessages.Add(new CoachInboxMessage
            {
                SeasonId = season.Id,
                SeasonNumber = CurrentSeasonNumber(season),
                TeamId = recipient.Id,
                CoachId = recipient.CoachId,
                ReferenceKey = key,
                To = coachName + " (" + recipient.DisplayName + ")",
                Category = "Scouting Report",
                Subject = subject,
                Body = body.ToString(),
                Important = true,
                IsRead = false
            });
            return true;
        }

        private static string ScoutingReferenceKey(Season season, ScheduledGame scheduled, Guid recipientTeamId)
            => "scouting:" + season.Id.ToString("N") + ":" + scheduled.Week + ":" + ((int)scheduled.Type) + ":" +
               scheduled.AwayTeamId.ToString("N") + ":" + scheduled.HomeTeamId.ToString("N") + ":" + recipientTeamId.ToString("N");

        private List<ScheduledGame> ScheduledSeriesGames(Season season, ScheduledGame scheduled)
        {
            return (season?.Schedule ?? new List<ScheduledGame>())
                .Where(g => g.Week == scheduled.Week &&
                    g.Type == scheduled.Type &&
                    g.AwayTeamId == scheduled.AwayTeamId &&
                    g.HomeTeamId == scheduled.HomeTeamId)
                .OrderBy(g => g.GameNumber)
                .ToList();
        }

        private string TeamRankingText(Season season, Guid teamId)
        {
            var poll = RankingEngine.LatestPoll(season);
            var entry = poll?.Rankings?.FirstOrDefault(r => r.TeamId == teamId);
            return entry == null ? "Unranked/no poll" : "#" + entry.Rank + " in " + (poll?.Name ?? "poll");
        }

        private static string OpponentCoachText(Team opponent)
        {
            var coach = opponent?.Coaches?.FirstOrDefault(c => c.Id == opponent.CoachId)
                ?? opponent?.Coaches?.FirstOrDefault(c => c.Active)
                ?? opponent?.Coaches?.FirstOrDefault();
            return coach == null
                ? "No coach profile"
                : coach.Name + " - " + coach.Style + " / " + coach.Strategy;
        }

        private IEnumerable<string> ProjectedLineupLines(Team team)
        {
            if (team?.BaseLineup?.BattingOrder == null || team.BaseLineup.BattingOrder.Count == 0)
            {
                yield return "No saved lineup is available.";
                yield break;
            }

            foreach (var slot in team.BaseLineup.BattingOrder.OrderBy(s => s.BattingOrder))
            {
                var player = team.Roster?.FirstOrDefault(p => p.Id == slot.PlayerId);
                if (player == null)
                    continue;
                yield return slot.BattingOrder + ". " + player.Name + " " + slot.DefensivePosition +
                    " - CON " + player.Contact + ", PWR " + player.Power + ", SPD " + player.Speed +
                    ", BR " + player.BaseRunning + ", bats " + (string.IsNullOrWhiteSpace(player.Bats) ? "?" : player.Bats);
            }
        }

        private Player ExpectedStarter(Team team)
        {
            if (team == null)
                return null;
            var roster = team.Roster ?? new List<Player>();
            var plan = team.PitchingPlan;
            if (plan?.StarterRotationIds != null && plan.StarterRotationIds.Count > 0)
            {
                int index = Math.Clamp(plan.NextStarterSlot, 0, plan.StarterRotationIds.Count - 1);
                var pitcher = roster.FirstOrDefault(p => p.Id == plan.StarterRotationIds[index]);
                if (pitcher != null)
                    return pitcher;
            }
            if (team.BaseLineup?.StartingPitcherId.HasValue == true)
            {
                var pitcher = roster.FirstOrDefault(p => p.Id == team.BaseLineup.StartingPitcherId.Value);
                if (pitcher != null)
                    return pitcher;
            }
            return roster
                .Where(p => p.Role == PlayerRole.Pitcher || (p.Positions ?? "").Contains("P", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(p => p.Pitching * 2 + p.Stamina)
                .FirstOrDefault();
        }

        private string PitcherScoutText(Player pitcher, Team team)
        {
            if (pitcher == null)
                return "No eligible starter found";
            PitchProfileEngine.NormalizePlayerPitchProfiles(pitcher, _rng);
            return pitcher.Name + " (" + team.ScoreboardName + ") - " + pitcher.Classification +
                ", throws " + (string.IsNullOrWhiteSpace(pitcher.Throws) ? "?" : pitcher.Throws) +
                ", Pitch " + pitcher.Pitching + ", Stamina " + pitcher.Stamina +
                ", arsenal " + PitchProfileEngine.ArsenalScoutSummary(pitcher);
        }

        private IEnumerable<string> ScoutingIssueLines(Season season, Team recipient, Team opponent)
        {
            var issues = new List<string>();
            var ourStarter = ExpectedStarter(recipient);
            var theirStarter = ExpectedStarter(opponent);
            var opponentLineup = LineupPlayers(opponent).ToList();
            var ourLineup = LineupPlayers(recipient).ToList();

            if (ourStarter == null)
                issues.Add("No expected starter is set for your team; pitching plan should be reviewed before first pitch.");
            else
            {
                if (ourStarter.Pitching < 60)
                    issues.Add("Your expected starter has a below-average Pitching rating against this lineup.");
                if (ourStarter.Stamina < 60)
                    issues.Add("Your expected starter has limited stamina; bullpen may be needed early in the series.");
                var topOpponentPower = opponentLineup.OrderByDescending(p => p.Power).FirstOrDefault();
                if (topOpponentPower != null && topOpponentPower.Power >= 75)
                    issues.Add(topOpponentPower.Name + " is a major power threat and can punish missed locations.");
                var topOpponentSpeed = opponentLineup.OrderByDescending(p => p.Speed + p.BaseRunning).FirstOrDefault();
                var catcher = CatcherForTeam(recipient);
                if (topOpponentSpeed != null && topOpponentSpeed.Speed + topOpponentSpeed.BaseRunning >= 145)
                {
                    string catcherNote = catcher == null ? "no catcher profile available" :
                        "catcher arm/pop/accuracy " + ((catcher.ArmStrength + catcher.PopTime + catcher.Accuracy) / 3);
                    issues.Add(topOpponentSpeed.Name + " creates steal/extra-base pressure; " + catcherNote + ".");
                }
            }

            if (theirStarter != null)
            {
                if (theirStarter.Pitching >= 75)
                    issues.Add("Opponent starter " + theirStarter.Name + " grades as a high-end matchup problem.");
                var bestPitch = (theirStarter.PitchArsenal ?? new List<PlayerPitchProfile>())
                    .Where(p => p.Enabled)
                    .OrderByDescending(p => p.Effectiveness)
                    .FirstOrDefault();
                if (bestPitch != null)
                {
                    int weakCount = ourLineup.Count(p => p.PitchWeaknesses != null && p.PitchWeaknesses.Contains(bestPitch.PitchType));
                    if (weakCount >= 3)
                        issues.Add(weakCount + " projected hitters are weak against " + PitchProfileEngine.ShortName(bestPitch.PitchType) + ", the opponent starter's best pitch.");
                }
            }

            var oppStats = TeamSeasonStats(season, opponent.Id);
            if (oppStats.SB >= 10)
                issues.Add("Opponent has already stolen " + oppStats.SB + " bases this season; control the running game.");
            if (oppStats.HR >= 8)
                issues.Add("Opponent power profile is dangerous with " + oppStats.HR + " home runs this season.");
            if (issues.Count == 0)
                issues.Add("No major red flags found; normal strategy profile is recommended.");
            return issues;
        }

        private IEnumerable<Player> LineupPlayers(Team team)
        {
            var roster = team?.Roster ?? new List<Player>();
            foreach (var slot in team?.BaseLineup?.BattingOrder?.OrderBy(s => s.BattingOrder) ?? Enumerable.Empty<TeamBaseLineupSlot>())
            {
                var player = roster.FirstOrDefault(p => p.Id == slot.PlayerId);
                if (player != null)
                    yield return player;
            }
        }

        private Player CatcherForTeam(Team team)
        {
            var roster = team?.Roster ?? new List<Player>();
            if (team?.BaseLineup?.DefensiveAssignments != null &&
                team.BaseLineup.DefensiveAssignments.TryGetValue("C", out Guid catcherId))
                return roster.FirstOrDefault(p => p.Id == catcherId);
            return roster
                .Where(p => (p.Positions ?? "").Contains("C", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(p => p.ArmStrength + p.PopTime + p.Accuracy)
                .FirstOrDefault();
        }

        private IEnumerable<string> TopOpponentThreats(Season season, Team opponent)
        {
            var statsByPlayer = PlayerSeasonStats(season, opponent).ToDictionary(s => s.PlayerId);
            foreach (var player in (opponent?.Roster ?? new List<Player>())
                .Where(p => !p.RedshirtActive && p.InjuryStatus != PlayerInjuryStatus.Out)
                .OrderByDescending(p => p.Contact + p.Power + p.Speed + p.BaseRunning)
                .Take(5))
            {
                statsByPlayer.TryGetValue(player.Id, out var stats);
                string statText = stats == null || stats.Games <= 0
                    ? "no current stats"
                    : stats.H + " H, " + stats.HR + " HR, " + stats.RBI + " RBI, " + stats.SB + " SB";
                yield return player.Name + " - CON " + player.Contact + ", PWR " + player.Power +
                    ", SPD " + player.Speed + ", BR " + player.BaseRunning + " (" + statText + ")";
            }
        }

        private CoachInboxMessage CreateGameInboxMessage(
            Season season,
            int seasonNumber,
            ScheduledGame? scheduled,
            GameResult result,
            Team away,
            Team home,
            Team recipientTeam,
            Guid coachId,
            string context,
            PlayerGameLine playerOfGame,
            bool important)
        {
            var coach = recipientTeam.Coaches?.FirstOrDefault(c => c.Id == coachId)
                ?? recipientTeam.Coaches?.FirstOrDefault(c => c.Active)
                ?? recipientTeam.Coaches?.FirstOrDefault();
            string coachName = coach?.Name ?? recipientTeam.CoachName ?? "Coach";
            bool recipientWon = (result.AwayScore > result.HomeScore && recipientTeam.Id == result.AwayTeamId) ||
                (result.HomeScore > result.AwayScore && recipientTeam.Id == result.HomeTeamId);
            string outcome = result.AwayScore == result.HomeScore ? "Tie" : recipientWon ? "Win" : "Loss";
            string score = ScoreboardLine(away, result.AwayScore, home, result.HomeScore);
            string subject = context + ": " + outcome + " - " + score;

            var body = new StringBuilder();
            body.AppendLine("Coach " + coachName + ",");
            body.AppendLine();
            body.AppendLine(context + " is complete.");
            body.AppendLine("Final: " + score);
            body.AppendLine("Result for " + recipientTeam.DisplayName + ": " + outcome);
            body.AppendLine("Updated record: " + TeamRecordText(season, recipientTeam.Id));
            if (scheduled != null)
                body.AppendLine("Schedule slot: Week " + scheduled.Week + ", " + scheduled.DayLabel + ", Game #" + scheduled.GameNumber);
            if (result.IsPlayoff)
                body.AppendLine("Playoff round: " + (string.IsNullOrWhiteSpace(result.PlayoffRoundName) ? "Round " + result.PlayoffRound : result.PlayoffRoundName));
            body.AppendLine();
            body.AppendLine("Player of the Game");
            body.AppendLine(PlayerOfGameText(playerOfGame));
            body.AppendLine();
            body.AppendLine("Team Statistics");
            body.AppendLine(TeamGameSummary(result, away));
            body.AppendLine(TeamGameSummary(result, home));
            body.AppendLine();
            body.AppendLine("Top Performers");
            foreach (var line in TopPerformerLines(result, 5))
                body.AppendLine(line);
            body.AppendLine();
            body.AppendLine("This report was generated from the committed game result and is saved in the dynasty inbox.");

            return new CoachInboxMessage
            {
                SeasonId = season.Id,
                SeasonNumber = seasonNumber,
                GameResultId = result.Id,
                TeamId = recipientTeam.Id,
                CoachId = coachId,
                To = coachName + " (" + recipientTeam.DisplayName + ")",
                Category = "Game Report",
                Subject = subject,
                Body = body.ToString(),
                Important = important,
                IsRead = false
            };
        }

        private string GameContextLabel(ScheduledGame? scheduled, GameResult? result)
        {
            if (result?.IsPlayoff == true)
                return string.IsNullOrWhiteSpace(result.PlayoffRoundName) ? "Playoff Game" : result.PlayoffRoundName;
            if (scheduled == null)
                return "Game Report";
            return scheduled.Type switch
            {
                ScheduledGameType.District => "District Game",
                ScheduledGameType.Region => "Region Game",
                ScheduledGameType.Conference => "Conference Game",
                ScheduledGameType.NonConference => "Non-Conference Game",
                _ => "Scheduled Game"
            };
        }

        private static PlayerGameLine PlayerOfGame(GameResult result)
        {
            if (result?.Lines == null || result.Lines.Count == 0)
                return null;
            Guid winnerId = result.AwayScore == result.HomeScore
                ? Guid.Empty
                : result.AwayScore > result.HomeScore ? result.AwayTeamId : result.HomeTeamId;
            return result.Lines
                .OrderByDescending(l => GameLineScore(l, winnerId))
                .FirstOrDefault();
        }

        private static double GameLineScore(PlayerGameLine line, Guid winnerId)
        {
            if (line == null)
                return double.MinValue;
            double score =
                line.H * 2.0 +
                line.Doubles +
                line.Triples * 2.0 +
                line.HR * 4.0 +
                line.RBI * 2.0 +
                line.R +
                line.BB * 0.8 +
                line.HBP * 0.8 +
                line.SB * 1.4 -
                line.CS -
                line.SO * 0.25 +
                line.IPOuts * 0.75 +
                line.K * 1.2 +
                line.Wins * 4.0 +
                line.Saves * 3.0 -
                line.ER * 2.0 -
                line.HitsAllowed * 0.6 -
                line.WalksAllowed * 0.6 -
                line.HomeRunsAllowed * 2.0 +
                line.Putouts * 0.05 +
                line.Assists * 0.1 -
                line.Errors * 1.5;
            if (winnerId != Guid.Empty && line.TeamId == winnerId)
                score += 2.0;
            return score;
        }

        private string PlayerOfGameText(PlayerGameLine line)
        {
            if (line == null)
                return "No player stat line was available for this game.";
            var team = TeamById(line.TeamId);
            var details = new List<string>();
            if (line.H > 0 || line.AB > 0) details.Add(line.H + "-for-" + line.AB);
            if (line.Doubles > 0) details.Add(line.Doubles + " 2B");
            if (line.Triples > 0) details.Add(line.Triples + " 3B");
            if (line.HR > 0) details.Add(line.HR + " HR");
            if (line.RBI > 0) details.Add(line.RBI + " RBI");
            if (line.R > 0) details.Add(line.R + " R");
            if (line.SB > 0) details.Add(line.SB + " SB");
            if (line.IPOuts > 0) details.Add(FormatInnings(line.IPOuts) + " IP");
            if (line.K > 0) details.Add(line.K + " K");
            if (line.ER > 0 || line.IPOuts > 0) details.Add(line.ER + " ER");
            return line.PlayerName + " - " + (team?.DisplayName ?? "Team") + " - " + (details.Count == 0 ? "key contributor" : string.Join(", ", details));
        }

        private string TeamGameSummary(GameResult result, Team team)
        {
            if (result == null || team == null)
                return "";
            var lines = result.Lines?.Where(l => l.TeamId == team.Id).ToList() ?? new List<PlayerGameLine>();
            int runs = team.Id == result.AwayTeamId ? result.AwayScore : result.HomeScore;
            int hits = lines.Sum(l => l.H);
            int doubles = lines.Sum(l => l.Doubles);
            int triples = lines.Sum(l => l.Triples);
            int homers = lines.Sum(l => l.HR);
            int rbi = lines.Sum(l => l.RBI);
            int walks = lines.Sum(l => l.BB);
            int steals = lines.Sum(l => l.SB);
            int errors = lines.Sum(l => l.Errors);
            int pitchingK = lines.Sum(l => l.K);
            return team.ScoreboardName + ": R " + runs + ", H " + hits + ", 2B " + doubles + ", 3B " + triples +
                ", HR " + homers + ", RBI " + rbi + ", BB " + walks + ", SB " + steals + ", K " + pitchingK + ", E " + errors;
        }

        private IEnumerable<string> TopPerformerLines(GameResult result, int count)
        {
            if (result?.Lines == null)
                yield break;
            Guid winnerId = result.AwayScore == result.HomeScore
                ? Guid.Empty
                : result.AwayScore > result.HomeScore ? result.AwayTeamId : result.HomeTeamId;
            foreach (var line in result.Lines.OrderByDescending(l => GameLineScore(l, winnerId)).Take(count))
                yield return "- " + PlayerOfGameText(line);
        }

        private bool ApplyCommittedResultToPlayoffSeries(Season season, GameResult result, out Team? champion, out PlayoffSeries? championshipSeries)
        {
            champion = null;
            championshipSeries = null;
            if (season == null || result == null || result.AwayScore == result.HomeScore)
                return false;

            if (!result.PlayoffSeriesId.HasValue)
                return false;

            var series = season.Playoffs?.FirstOrDefault(s => s.Id == result.PlayoffSeriesId.Value && !s.WinnerTeamId.HasValue);
            if (series == null)
                return false;
            if (!((series.TeamAId == result.AwayTeamId && series.TeamBId == result.HomeTeamId) ||
                  (series.TeamAId == result.HomeTeamId && series.TeamBId == result.AwayTeamId)))
                return false;

            result.IsPlayoff = true;
            result.PlayoffRound = series.Round;
            result.PlayoffRoundName = string.IsNullOrWhiteSpace(series.RoundName)
                ? PlayoffEngine.RoundNameFor(series.Round, season.Playoffs?.Count > 0 ? season.Playoffs.Max(s => s.Round) : series.Round)
                : series.RoundName;
            result.GameType = string.IsNullOrWhiteSpace(result.PlayoffRoundName) ? "Playoff" : result.PlayoffRoundName;

            Guid winningTeamId = result.AwayScore > result.HomeScore ? result.AwayTeamId : result.HomeTeamId;
            if (winningTeamId == series.TeamAId)
                series.TeamAWins++;
            else if (winningTeamId == series.TeamBId)
                series.TeamBWins++;
            else
                return false;

            int winsNeeded = series.BestOf / 2 + 1;
            if (series.TeamAWins < winsNeeded && series.TeamBWins < winsNeeded)
                return false;

            series.WinnerTeamId = series.TeamAWins > series.TeamBWins ? series.TeamAId : series.TeamBId;
            series.WinnerCoachId = series.WinnerTeamId == series.TeamAId ? series.TeamACoachId : series.TeamBCoachId;
            AwardSeriesChampionBadge(season, series);
            if (!TryRecordChampion(season, series, out var recordedChampion))
                return false;

            champion = recordedChampion;
            championshipSeries = series;
            return true;
        }

        private void AdvancePlayoffBracket(Season season)
        {
            PlayoffEngine.AdvanceBracket(_league, season);
        }

        private void AdvanceOffseason()
        {
            var season = SelectedSeason(_seasonCombo);
            if (season == null)
            {
                MessageBox.Show(this, "Select a season first.");
                return;
            }

            if (season.OffseasonProcessed)
            {
                MessageBox.Show(this, "This season's offseason progression has already been processed.");
                return;
            }

            if (!season.ChampionTeamId.HasValue)
            {
                MessageBox.Show(this, "The championship must be completed before the All-Star Game and offseason.");
                return;
            }

            if (season.AllStarGame == null)
            {
                MessageBox.Show(this, "The All-Star Game is played after the championship and before the offseason. Complete the All-Star Game first.");
                return;
            }

            if (season.Awards == null || !season.Awards.Any(a => a.Winner))
            {
                MessageBox.Show(this, "Finalize season awards after the All-Star Game and before the offseason.");
                return;
            }

            var confirm = MessageBox.Show(this,
                "Advance offseason for " + season.Name + "?\n\nSeniors will graduate, underclassmen will advance, ratings will progress/regress, and new recruits will refill rosters.",
                "Advance offseason", MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
            if (confirm != DialogResult.OK)
                return;

            var result = PlayerProgressionEngine.ApplyOffseason(_league, season, _rng);
            MarkDirty();
            if (!string.IsNullOrEmpty(_path))
                SaveLeague(false);
            RefreshAll();

            MessageBox.Show(this,
                "Offseason complete.\n\n" +
                "Graduated seniors: " + result.GraduatedSeniors + "\n" +
                "Progressed players: " + result.ProgressedPlayers + "\n" +
                "Improved players: " + result.ImprovedPlayers + "\n" +
                "Regressed players: " + result.RegressedPlayers + "\n" +
                "Medical tags awarded: " + result.MedicalTagsAwarded + "\n" +
                "Redshirts processed: " + result.RedshirtsProcessed + "\n" +
                "Pitch count growth: +" + result.PitchCountIncreases + "\n" +
                "JV promotions: " + result.JvPromotions + "\n" +
                "Added recruits: " + result.AddedRecruits,
                "Offseason complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void LaunchPlayableGame(GameMode mode)
        {
            var scheduled = SelectedScheduledGame();
            var away = scheduled == null ? SelectedTeam(_awayCombo) : TeamById(scheduled.AwayTeamId);
            var home = scheduled == null ? SelectedTeam(_homeCombo) : TeamById(scheduled.HomeTeamId);
            if (away == null || home == null || away.Id == home.Id)
            {
                MessageBox.Show(this, "Pick two different teams.");
                return;
            }
            var season = SelectedSeason(_commitSeasonCombo) ?? SelectedSeason(_seasonCombo);
            var awayUniform = SelectedAwayUniform(away, scheduled);
            var homeUniform = SelectedHomeUniform(home, scheduled);
            EnsureScoutingMessagesForSeries(season, scheduled);
            var mutationSnapshot = TeamMutationSnapshot.Capture(_league, away, home);
            InjuryEngine.ProcessGameInjuries(away, home, _rng);
            if (!ValidateGameStart(season, scheduled, away, home))
            {
                mutationSnapshot.Restore(_league);
                RefreshAll();
                return;
            }

            Team? controlledTeam = null;
            if (mode == GameMode.UserVsCpu)
                mode = ResolveRequestedGameMode(away, home, out controlledTeam);

            var rules = _league.Rules ?? new LeagueRules();
            var state = GameplayState.Create(
                away,
                home,
                mode,
                rules.Innings,
                rules.ExtraInnings,
                rules.MercyRuleEnabled,
                rules.MercyRuleRuns,
                rules.MercyRuleMinimumInning,
                rules.ExtraInningRunnerOnSecond,
                rules.CourtesyRunnerForPitchersCatchers);
            state.FieldPresetId = SelectedFieldPreset().Id;
            state.AwayUniformSetId = awayUniform?.Id;
            state.HomeUniformSetId = homeUniform?.Id;
            TriggerCutscene(IsPlayoffGameForCutscene(away, home) ? CutsceneTrigger.PlayoffGameStart : CutsceneTrigger.GameStart, home, away, homeUniform?.Category, awayUniform?.Category);
            ShowGameLoadingScreen(away, home, mode);
            using var game = new GameplayForm(away, home, RankingModifierForGame(season, away, home));
            game.SetCutscenes(_league.Cutscenes, away.Cutscenes, home.Cutscenes);
            game.SetNationalAnthemCutsceneDefault(_league.NationalAnthemCutsceneDefault);
            game.SetFieldPreset(SelectedFieldPreset());
            game.SetNationalAnthemImages(GetTeamNationalAnthemPaths(away), GetTeamNationalAnthemPaths(home));
            game.SetPregameLineupLogos(GetTeamLogoPath(away) ?? "", GetTeamLogoPath(home) ?? "");
            game.ApplyGameplayState(state);
            game.SaveRequested += liveState => SaveInProgressGame(liveState, season, scheduled);
            if (controlledTeam != null)
                game.SetUserControlledTeam(controlledTeam);
            if (mode == GameMode.PlayerVsPlayer)
            {
                bool awayUsesKeyboard = SelectedPvpAwayUsesKeyboard();
                game.SetPlayerVsPlayerInputAssignments(
                    awayUsesKeyboard ? away : home,
                    awayUsesKeyboard ? home : away);
            }
            game.SetModeLabel(mode == GameMode.CpuVsCpuWatch
                ? "CPU watch - controller or Tab/V toggles user control"
                : mode == GameMode.PlayerVsPlayer
                    ? PlayerVsPlayerInputLabel(away, home)
                    : "Player vs CPU - keyboard or XInput controller");
            LoadScoreboardPhotos(away, home);
            _simResult.Text = mode == GameMode.CpuVsCpuWatch
                ? "Watching " + away.ScoreboardName + " at " + home.ScoreboardName
                : mode == GameMode.PlayerVsPlayer
                    ? "Player vs Player: " + away.ScoreboardName + " at " + home.ScoreboardName
                : "Playing " + away.ScoreboardName + " at " + home.ScoreboardName;
            _status.Text = "Keyboard is active. XInput controllers are read directly by Windows; PCSX2 is not required for controller input.";
            _fieldPanel.Invalidate();
            game.ShowDialog(this);
            HandlePlayableGameResult(game.FinalResult, away, home, scheduled, mutationSnapshot);
        }

        private bool SelectedPvpAwayUsesKeyboard()
            => !((_pvpInputCombo?.SelectedItem as PvpInputAssignmentItem)?.AwayUsesKeyboard == false);

        private string PlayerVsPlayerInputLabel(Team away, Team home)
        {
            bool awayUsesKeyboard = SelectedPvpAwayUsesKeyboard();
            string keyboard = awayUsesKeyboard ? away?.ScoreboardName : home?.ScoreboardName;
            string controller = awayUsesKeyboard ? home?.ScoreboardName : away?.ScoreboardName;
            return "Player vs Player - Keyboard: " + (keyboard ?? "Team") + ", Controller: " + (controller ?? "Team");
        }

        private void ResumeSavedGame()
        {
            var save = SelectedInProgressGameSave();
            if (save?.State == null)
            {
                MessageBox.Show(this, "No saved in-progress game is available for the selected scheduled game or matchup.", "Resume Saved Game", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var away = TeamById(save.AwayTeamId) ?? save.State.AwayTeam;
            var home = TeamById(save.HomeTeamId) ?? save.State.HomeTeam;
            if (away == null || home == null)
            {
                MessageBox.Show(this, "The saved game's teams could not be found.", "Resume Saved Game", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            save.State.AwayTeam = away;
            save.State.HomeTeam = home;

            var season = save.SeasonId.HasValue
                ? _league.Seasons.FirstOrDefault(s => s.Id == save.SeasonId.Value)
                : SelectedSeason(_commitSeasonCombo) ?? SelectedSeason(_seasonCombo);
            var scheduled = season?.Schedule?.FirstOrDefault(g => save.ScheduledGameId.HasValue && g.Id == save.ScheduledGameId.Value);
            var mutationSnapshot = TeamMutationSnapshot.Capture(_league, away, home);

            using var game = new GameplayForm(away, home, RankingModifierForGame(season, away, home));
            game.SetCutscenes(_league.Cutscenes, away.Cutscenes, home.Cutscenes);
            game.SetNationalAnthemCutsceneDefault(_league.NationalAnthemCutsceneDefault);
            game.SetFieldPreset(BaseballFieldPresets.Find(save.State.FieldPresetId));
            game.SetNationalAnthemImages(GetTeamNationalAnthemPaths(away), GetTeamNationalAnthemPaths(home));
            game.SetPregameLineupLogos(GetTeamLogoPath(away) ?? "", GetTeamLogoPath(home) ?? "");
            game.ApplyGameplayState(save.State);
            game.SaveRequested += liveState => SaveInProgressGame(liveState, season, scheduled, save.Id);
            game.SetModeLabel("Resumed saved game - " + away.ScoreboardName + " at " + home.ScoreboardName);
            LoadScoreboardPhotos(away, home);
            _simResult.Text = "Resumed saved game: " + away.ScoreboardName + " at " + home.ScoreboardName;
            game.ShowDialog(this);
            HandlePlayableGameResult(game.FinalResult, away, home, scheduled, mutationSnapshot);
        }

        private InProgressGameSave SelectedInProgressGameSave()
        {
            _league.InProgressGames ??= new List<InProgressGameSave>();
            var scheduled = SelectedScheduledGame();
            var season = SelectedSeason(_commitSeasonCombo) ?? SelectedSeason(_seasonCombo);
            if (scheduled != null)
            {
                return _league.InProgressGames
                    .Where(s => s.ScheduledGameId == scheduled.Id)
                    .OrderByDescending(s => s.SavedAt)
                    .FirstOrDefault();
            }

            var away = SelectedTeam(_awayCombo);
            var home = SelectedTeam(_homeCombo);
            if (away == null || home == null)
                return null;

            return _league.InProgressGames
                .Where(s => s.AwayTeamId == away.Id && s.HomeTeamId == home.Id &&
                    (!s.SeasonId.HasValue || season == null || s.SeasonId == season.Id))
                .OrderByDescending(s => s.SavedAt)
                .FirstOrDefault();
        }

        private bool SaveInProgressGame(GameplayState liveState, Season? season, ScheduledGame? scheduled, Guid? existingSaveId = null)
        {
            if (liveState == null || liveState.AwayTeam == null || liveState.HomeTeam == null)
                return false;

            _league.InProgressGames ??= new List<InProgressGameSave>();
            var save = existingSaveId.HasValue
                ? _league.InProgressGames.FirstOrDefault(s => s.Id == existingSaveId.Value)
                : null;
            save ??= scheduled == null
                ? _league.InProgressGames.FirstOrDefault(s => s.Id == liveState.Id)
                : _league.InProgressGames.FirstOrDefault(s => s.ScheduledGameId == scheduled.Id);

            if (save == null)
            {
                save = new InProgressGameSave { Id = liveState.Id };
                _league.InProgressGames.Add(save);
            }

            save.SavedAt = DateTime.Now;
            save.SeasonId = season?.Id;
            save.ScheduledGameId = scheduled?.Id;
            save.AwayTeamId = liveState.AwayTeam.Id;
            save.HomeTeamId = liveState.HomeTeam.Id;
            save.State = liveState;
            save.Label = BuildInProgressGameLabel(liveState, season, scheduled);

            MarkDirty();
            return SaveLeague(false);
        }

        private static string BuildInProgressGameLabel(GameplayState state, Season? season, ScheduledGame? scheduled)
        {
            string prefix = scheduled == null
                ? "Unscheduled"
                : "Game " + scheduled.GameNumber;
            string seasonName = season == null ? "" : season.Name + " ";
            string half = state.Half == HalfInning.Top ? "Top" : "Bottom";
            return (seasonName + prefix + ": " + state.AwayTeam.ScoreboardName + " " + state.AwayScore +
                ", " + state.HomeTeam.ScoreboardName + " " + state.HomeScore +
                " - " + half + " " + state.Inning).Trim();
        }

        private void RemoveInProgressGameSave(Season? season, ScheduledGame? scheduled, Team away, Team home)
        {
            if (_league?.InProgressGames == null || _league.InProgressGames.Count == 0)
                return;

            int removed = _league.InProgressGames.RemoveAll(save =>
                scheduled != null && save.ScheduledGameId == scheduled.Id ||
                scheduled == null && save.AwayTeamId == away?.Id && save.HomeTeamId == home?.Id &&
                    (!save.SeasonId.HasValue || season == null || save.SeasonId == season.Id));
            if (removed > 0)
                MarkDirty();
        }

        private GameMode ResolveRequestedGameMode(Team away, Team home, out Team? controlledTeam)
        {
            controlledTeam = null;
            var selected = _controlTeamCombo?.SelectedItem as ControlTeamItem;
            if (selected?.WatchOnly == true)
                return GameMode.CpuVsCpuWatch;

            if (selected?.TeamId.HasValue == true)
            {
                controlledTeam = selected.TeamId.Value == away?.Id ? away :
                    selected.TeamId.Value == home?.Id ? home : null;
                return controlledTeam == null ? GameMode.CpuVsCpuWatch : GameMode.UserVsCpu;
            }

            var controlled = DefaultControlledTeamsForGame(away, home);
            if (controlled.Count == 0)
                return GameMode.CpuVsCpuWatch;
            if (controlled.Count > 1)
                return GameMode.PlayerVsPlayer;

            controlledTeam = controlled[0];
            return GameMode.UserVsCpu;
        }

        private void HandlePlayableGameResult(GameResult? result, Team away, Team home, ScheduledGame? scheduled, TeamMutationSnapshot mutationSnapshot)
        {
            if (result == null || away == null || home == null)
            {
                mutationSnapshot?.Restore(_league);
                RefreshAll();
                _status.Text = "Game exited before completion; team and player changes were restored.";
                return;
            }

            var season = SelectedSeason(_commitSeasonCombo) ?? SelectedSeason(_seasonCombo);
            Team winner = result.AwayScore == result.HomeScore
                ? null
                : result.AwayScore > result.HomeScore ? away : home;
            string winnerRecord = winner == null ? "" : ProjectedTeamRecordText(season, winner.Id, result);
            bool canCommit = season != null && (scheduled == null || !scheduled.PlayedGameId.HasValue);
            string commitText = canCommit
                ? (scheduled == null ? "Commit to Season" : "Commit to Scheduled Game")
                : "No Season Selected";

            using var dialog = new PostGameResultDialog(
                away,
                home,
                result,
                winner == null ? null : GetTeamLogoPath(winner),
                winnerRecord,
                commitText,
                canCommit);

            dialog.ShowDialog(this);
            _lastGame = result;
            _simResult.Text = ScoreboardLine(away, result.AwayScore, home, result.HomeScore);
            LoadScoreboardPhotos(away, home);
            RemoveInProgressGameSave(season, scheduled, away, home);

            if (dialog.CommitRequested)
            {
                bool saved = CommitGameResult(season, scheduled, result);
                _status.Text = saved
                    ? "Committed and autosaved final: " + ScoreboardLine(away, result.AwayScore, home, result.HomeScore)
                    : "Final committed in memory, but autosave was canceled or failed. Use File > Save.";
            }
            else
            {
                mutationSnapshot?.Restore(_league);
                RefreshAll();
                _status.Text = "Dismissed final without committing: " + ScoreboardLine(away, result.AwayScore, home, result.HomeScore);
            }

            _fieldPanel.Invalidate();
        }

        private void WatchReplay()
        {
            try
            {
                using var library = new ReplayLibraryDialog(ReplayStore.DefaultReplayFolder);
                if (library.ShowDialog(this) != DialogResult.OK)
                    return;

                var replay = ReplayStore.Load(library.SelectedReplayPath);
                Team? awayTeam = FindReplayPresentationTeam(replay.Teams?.Away);
                Team? homeTeam = FindReplayPresentationTeam(replay.Teams?.Home);
                if (awayTeam == null || homeTeam == null)
                    throw new InvalidDataException("The replay teams could not be matched to teams in the current dynasty.");
                using var viewer = new ReplayWatchForm(
                    replay,
                    awayTeam,
                    homeTeam,
                    GetTeamLogoPath(awayTeam) ?? "",
                    GetTeamLogoPath(homeTeam) ?? "");
                viewer.ShowDialog(this);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this,
                    "Could not open replay file.\n\n" + ex.Message,
                    "Replay import failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private Team? FindReplayPresentationTeam(ReplayTeam? replayTeam)
        {
            if (replayTeam == null || _league?.Teams == null)
                return null;

            if (Guid.TryParse(replayTeam.TeamId, out Guid teamId))
            {
                Team byId = TeamById(teamId);
                if (byId != null)
                    return byId;
            }

            return _league.Teams.FirstOrDefault(team =>
                team != null &&
                ((!string.IsNullOrWhiteSpace(replayTeam.ScoreboardAbbreviation) &&
                  string.Equals(team.ScoreboardName, replayTeam.ScoreboardAbbreviation, StringComparison.OrdinalIgnoreCase)) ||
                 (string.Equals(team.City, replayTeam.TeamName, StringComparison.OrdinalIgnoreCase) &&
                  string.Equals(team.Nickname, replayTeam.Mascot, StringComparison.OrdinalIgnoreCase)) ||
                 string.Equals(team.DisplayName, replayTeam.TeamName, StringComparison.OrdinalIgnoreCase)));
        }

        private void ShowGameLoadingScreen(Team away, Team home, GameMode mode)
        {
            var season = SelectedSeason(_commitSeasonCombo) ?? SelectedSeason(_seasonCombo);
            string? awayLogo = GetTeamLogoPath(away) ?? GetTeamPhotoPaths(away).FirstOrDefault();
            string? homeLogo = GetTeamLogoPath(home) ?? GetTeamPhotoPaths(home).FirstOrDefault();
            var playoffSeries = FindMatchingPlayoffSeries(season, away.Id, home.Id);
            bool playoffGame = playoffSeries != null;
            string gameTitle = playoffSeries != null
                ? PlayoffGameTitle(season, playoffSeries)
                : RegularGameTitle(away.Id, home.Id);
            string modeLabel = ModeLabelFor(mode);

            using var loading = new GameLoadingForm(
                away,
                TeamRecordText(season, away.Id),
                awayLogo,
                home,
                TeamRecordText(season, home.Id),
                homeLogo,
                gameTitle,
                modeLabel,
                playoffGame);
            loading.ShowDialog(this);
        }

        private bool TriggerCutscene(CutsceneTrigger trigger)
            => TriggerCutscene(trigger, null, null);

        private bool TriggerCutscene(CutsceneTrigger trigger, Team? primaryTeam, Team? secondaryTeam = null)
            => TriggerCutscene(trigger, primaryTeam, secondaryTeam, null, null);

        private bool TriggerCutscene(CutsceneTrigger trigger, Team? primaryTeam, Team? secondaryTeam, TeamUniformCategory? primaryUniformCategory, TeamUniformCategory? secondaryUniformCategory)
            => CutscenePlaybackForm.PlayFirst(this, CutscenePriority(trigger, primaryTeam, secondaryTeam, primaryUniformCategory, secondaryUniformCategory), trigger);

        private IEnumerable<CutsceneDefinition> CutscenePriority(CutsceneTrigger trigger, Team? primaryTeam, Team? secondaryTeam, TeamUniformCategory? primaryUniformCategory, TeamUniformCategory? secondaryUniformCategory)
        {
            if (primaryTeam?.Cutscenes != null)
            {
                foreach (var cutscene in TeamCutscenesForUniform(primaryTeam.Cutscenes, homeUniform: true, primaryUniformCategory))
                    yield return cutscene;
            }
            if (secondaryTeam != null && secondaryTeam.Id != primaryTeam?.Id && secondaryTeam.Cutscenes != null)
            {
                foreach (var cutscene in TeamCutscenesForUniform(secondaryTeam.Cutscenes, homeUniform: false, secondaryUniformCategory))
                    yield return cutscene;
            }
            if (CutsceneCatalog.IsTeamOnly(trigger))
                yield break;
            foreach (var cutscene in _league?.Cutscenes ?? Enumerable.Empty<CutsceneDefinition>())
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

        private bool IsPlayoffGameForCutscene(Team away, Team home)
        {
            var season = SelectedSeason(_commitSeasonCombo) ?? SelectedSeason(_seasonCombo);
            return FindMatchingPlayoffSeries(season, away?.Id ?? Guid.Empty, home?.Id ?? Guid.Empty) != null;
        }

        private static string ModeLabelFor(GameMode mode)
        {
            return mode switch
            {
                GameMode.CpuVsCpuWatch => "CPU vs CPU Watch",
                GameMode.PlayerVsPlayer => "Player vs Player",
                GameMode.UserVsCpu => "Player vs CPU",
                _ => "Game"
            };
        }

        private static string TeamRecordText(Season season, Guid teamId)
        {
            if (season == null || season.Games == null)
                return "0-0";

            int wins = 0;
            int losses = 0;
            int ties = 0;
            foreach (var game in season.Games)
            {
                bool away = game.AwayTeamId == teamId;
                bool home = game.HomeTeamId == teamId;
                if (!away && !home)
                    continue;

                CountRecordGame(game, teamId, ref wins, ref losses, ref ties);
            }

            return FormatRecord(wins, losses, ties);
        }

        private static string ProjectedTeamRecordText(Season season, Guid teamId, GameResult pendingResult)
        {
            int wins = 0;
            int losses = 0;
            int ties = 0;
            if (season?.Games != null)
            {
                foreach (var game in season.Games)
                    CountRecordGame(game, teamId, ref wins, ref losses, ref ties);
            }

            CountRecordGame(pendingResult, teamId, ref wins, ref losses, ref ties);
            return FormatRecord(wins, losses, ties);
        }

        private static void CountRecordGame(GameResult game, Guid teamId, ref int wins, ref int losses, ref int ties)
        {
            if (game == null)
                return;

            bool away = game.AwayTeamId == teamId;
            bool home = game.HomeTeamId == teamId;
            if (!away && !home)
                return;

            if (game.AwayScore == game.HomeScore)
            {
                ties++;
                return;
            }

            bool teamWon = away ? game.AwayScore > game.HomeScore : game.HomeScore > game.AwayScore;
            if (teamWon)
                wins++;
            else
                losses++;
        }

        private static string FormatRecord(int wins, int losses, int ties)
            => ties > 0 ? wins + "-" + losses + "-" + ties : wins + "-" + losses;

        private static int CountTeamWins(Season season, Guid teamId)
        {
            if (season?.Games == null)
                return 0;

            int wins = 0;
            foreach (var game in season.Games)
            {
                if (game.AwayTeamId == teamId && game.AwayScore > game.HomeScore)
                    wins++;
                else if (game.HomeTeamId == teamId && game.HomeScore > game.AwayScore)
                    wins++;
            }
            return wins;
        }

        private static TeamSeasonStatLine TeamSeasonStats(Season? season, Guid teamId, bool playoffsOnly = false)
        {
            var stats = new TeamSeasonStatLine { TeamId = teamId };
            if (season?.Games == null)
                return stats;

            foreach (var game in season.Games)
            {
                if (playoffsOnly && !game.IsPlayoff)
                    continue;

                bool away = game.AwayTeamId == teamId;
                bool home = game.HomeTeamId == teamId;
                if (!away && !home)
                    continue;

                int runsFor = away ? game.AwayScore : game.HomeScore;
                int runsAgainst = away ? game.HomeScore : game.AwayScore;
                stats.RunsFor += runsFor;
                stats.RunsAgainst += runsAgainst;
                if (runsFor > runsAgainst)
                    stats.Wins++;
                else if (runsAgainst > runsFor)
                    stats.Losses++;
                else
                    stats.Ties++;

                foreach (var line in game.Lines?.Where(l => l.TeamId == teamId) ?? Enumerable.Empty<PlayerGameLine>())
                {
                    stats.AB += line.AB;
                    stats.R += line.R;
                    stats.H += line.H;
                    stats.Doubles += line.Doubles;
                    stats.Triples += line.Triples;
                    stats.HR += line.HR;
                    stats.RBI += line.RBI;
                    stats.BB += line.BB;
                    stats.IBB += line.IBB;
                    stats.SO += line.SO;
                    stats.SB += line.SB;
                    stats.CS += line.CS;
                    stats.HBP += line.HBP;
                    stats.SH += line.SH;
                    stats.SF += line.SF;
                    stats.FlyOuts += line.FlyOuts;
                    stats.GroundOuts += line.GroundOuts;
                    stats.PopOuts += line.PopOuts;
                    stats.GroundedIntoDoublePlays += line.GroundedIntoDoublePlays;
                    stats.ReachedOnError += line.ReachedOnError;
                    stats.IPOuts += line.IPOuts;
                    stats.ER += line.ER;
                    stats.RunsAllowed += line.RunsAllowed;
                    stats.PitchingK += line.K;
                    stats.PitchingBB += line.WalksAllowed;
                    stats.PitchingIBB += line.IntentionalWalksAllowed;
                    stats.HitsAllowed += line.HitsAllowed;
                    stats.DoublesAllowed += line.DoublesAllowed;
                    stats.TriplesAllowed += line.TriplesAllowed;
                    stats.HomeRunsAllowed += line.HomeRunsAllowed;
                    stats.HitBatters += line.HitBatters;
                    stats.WildPitches += line.WildPitches;
                    stats.Balks += line.Balks;
                    stats.Holds += line.Holds;
                    stats.BlownSaves += line.BlownSaves;
                    stats.CompleteGames += line.CompleteGames;
                    stats.Shutouts += line.Shutouts;
                    stats.Putouts += line.Putouts;
                    stats.Assists += line.Assists;
                    stats.Errors += line.Errors;
                    stats.DefensiveOuts += line.DefensiveOuts;
                    stats.DoublePlaysTurned += line.TeamDoublePlaysTurned;
                    stats.PassedBalls += line.PassedBalls;
                    stats.StolenBasesAllowed += line.StolenBasesAllowed;
                    stats.CatcherCaughtStealing += line.CatcherCaughtStealing;
                    stats.InjuryGamesMissed += line.GamesMissedInjury;
                }
            }

            return stats;
        }

        private TeamSeasonStatLine TeamCareerStats(Team team, IEnumerable<Season> seasons, bool playoffsOnly = false)
        {
            var career = new TeamSeasonStatLine
            {
                TeamId = team.Id,
                TeamName = team.DisplayName,
                SeasonName = "Career"
            };

            foreach (var season in seasons ?? Enumerable.Empty<Season>())
            {
                var seasonStats = TeamSeasonStats(season, team.Id, playoffsOnly);
                AccumulateTeamStats(career, seasonStats);
                career.Champion = career.Champion || season.ChampionTeamId == team.Id;
            }

            return career;
        }

        private static void AccumulateTeamStats(TeamSeasonStatLine target, TeamSeasonStatLine source)
        {
            target.Wins += source.Wins;
            target.Losses += source.Losses;
            target.Ties += source.Ties;
            target.RunsFor += source.RunsFor;
            target.RunsAgainst += source.RunsAgainst;
            target.AB += source.AB;
            target.R += source.R;
            target.H += source.H;
            target.Doubles += source.Doubles;
            target.Triples += source.Triples;
            target.HR += source.HR;
            target.RBI += source.RBI;
            target.BB += source.BB;
            target.IBB += source.IBB;
            target.SO += source.SO;
            target.SB += source.SB;
            target.CS += source.CS;
            target.HBP += source.HBP;
            target.SH += source.SH;
            target.SF += source.SF;
            target.FlyOuts += source.FlyOuts;
            target.GroundOuts += source.GroundOuts;
            target.PopOuts += source.PopOuts;
            target.GroundedIntoDoublePlays += source.GroundedIntoDoublePlays;
            target.ReachedOnError += source.ReachedOnError;
            target.IPOuts += source.IPOuts;
            target.ER += source.ER;
            target.RunsAllowed += source.RunsAllowed;
            target.PitchingK += source.PitchingK;
            target.PitchingBB += source.PitchingBB;
            target.PitchingIBB += source.PitchingIBB;
            target.HitsAllowed += source.HitsAllowed;
            target.DoublesAllowed += source.DoublesAllowed;
            target.TriplesAllowed += source.TriplesAllowed;
            target.HomeRunsAllowed += source.HomeRunsAllowed;
            target.HitBatters += source.HitBatters;
            target.WildPitches += source.WildPitches;
            target.Balks += source.Balks;
            target.Holds += source.Holds;
            target.BlownSaves += source.BlownSaves;
            target.CompleteGames += source.CompleteGames;
            target.Shutouts += source.Shutouts;
            target.Putouts += source.Putouts;
            target.Assists += source.Assists;
            target.Errors += source.Errors;
            target.DefensiveOuts += source.DefensiveOuts;
            target.DoublePlaysTurned += source.DoublePlaysTurned;
            target.PassedBalls += source.PassedBalls;
            target.StolenBasesAllowed += source.StolenBasesAllowed;
            target.CatcherCaughtStealing += source.CatcherCaughtStealing;
            target.InjuryGamesMissed += source.InjuryGamesMissed;
        }

        private static List<PlayerSeasonStatLine> PlayerSeasonStats(Season? season, Team? team, bool playoffsOnly = false)
        {
            var map = new Dictionary<Guid, PlayerSeasonStatLine>();
            if (team != null)
            {
                foreach (var player in (team.Roster ?? new List<Player>())
                    .Concat(team.InjuredReserve ?? new List<Player>())
                    .Concat((team.JvPool ?? new List<Player>()).Where(player => player.VarsitySeasonsPlayed > 0)))
                {
                    map[player.Id] = new PlayerSeasonStatLine
                    {
                        PlayerId = player.Id,
                        PlayerName = player.Name,
                        Pitcher = player.Role == PlayerRole.Pitcher,
                        Classification = player.Classification,
                        Positions = player.Positions,
                        Injury = InjuryEngine.InjurySummary(player),
                        MedicalTag = player.MedicalTag,
                        MedicalEligible = player.MedicalTagEligible,
                        Redshirt = player.RedshirtActive,
                        VarsitySeasons = player.VarsitySeasonsPlayed,
                        CallUpSeason = player.VarsityCallUpSeasonNumber
                    };
                }
            }

            if (season?.Games != null)
            {
                foreach (var game in season.Games)
                {
                    if (playoffsOnly && !game.IsPlayoff)
                        continue;

                    var appearedThisGame = new HashSet<Guid>();
                    foreach (var line in (game.Lines ?? Enumerable.Empty<PlayerGameLine>())
                        .Where(l => l != null && (team == null || l.TeamId == team.Id)))
                    {
                        if (!map.TryGetValue(line.PlayerId, out var stats))
                        {
                            stats = new PlayerSeasonStatLine
                            {
                                PlayerId = line.PlayerId,
                                PlayerName = line.PlayerName,
                                Pitcher = line.Pitcher
                            };
                            map[line.PlayerId] = stats;
                        }

                        stats.PlayerName = string.IsNullOrWhiteSpace(stats.PlayerName) ? line.PlayerName : stats.PlayerName;
                        stats.Pitcher = stats.Pitcher || line.Pitcher;
                        if (HasPlayerAppearance(line) && appearedThisGame.Add(line.PlayerId))
                            stats.Games++;
                        stats.R += line.R;
                        stats.AB += line.AB;
                        stats.H += line.H;
                        stats.Doubles += line.Doubles;
                        stats.Triples += line.Triples;
                        stats.HR += line.HR;
                        stats.RBI += line.RBI;
                        stats.BB += line.BB;
                        stats.IBB += line.IBB;
                        stats.SO += line.SO;
                        stats.SB += line.SB;
                        stats.CS += line.CS;
                        stats.HBP += line.HBP;
                        stats.SH += line.SH;
                        stats.SF += line.SF;
                        stats.FlyOuts += line.FlyOuts;
                        stats.GroundOuts += line.GroundOuts;
                        stats.PopOuts += line.PopOuts;
                        stats.GroundedIntoDoublePlays += line.GroundedIntoDoublePlays;
                        stats.ReachedOnError += line.ReachedOnError;
                        stats.IPOuts += line.IPOuts;
                        stats.ER += line.ER;
                        stats.RunsAllowed += line.RunsAllowed;
                        stats.K += line.K;
                        stats.HitsAllowed += line.HitsAllowed;
                        stats.DoublesAllowed += line.DoublesAllowed;
                        stats.TriplesAllowed += line.TriplesAllowed;
                        stats.WalksAllowed += line.WalksAllowed;
                        stats.IntentionalWalksAllowed += line.IntentionalWalksAllowed;
                        stats.HomeRunsAllowed += line.HomeRunsAllowed;
                        stats.HitBatters += line.HitBatters;
                        stats.WildPitches += line.WildPitches;
                        stats.Balks += line.Balks;
                        stats.BattersFaced += line.BattersFaced;
                        stats.PitchCount += line.PitchCount;
                        stats.PitchingWins += line.Wins;
                        stats.PitchingLosses += line.Losses;
                        stats.Saves += line.Saves;
                        stats.Holds += line.Holds;
                        stats.BlownSaves += line.BlownSaves;
                        stats.CompleteGames += line.CompleteGames;
                        stats.Shutouts += line.Shutouts;
                        stats.Putouts += line.Putouts;
                        stats.Assists += line.Assists;
                        stats.Errors += line.Errors;
                        stats.DefensiveOuts += line.DefensiveOuts;
                        stats.DefensiveDoublePlays += line.DefensiveDoublePlays;
                        stats.PassedBalls += line.PassedBalls;
                        stats.StolenBasesAllowed += line.StolenBasesAllowed;
                        stats.CatcherCaughtStealing += line.CatcherCaughtStealing;
                        stats.GamesMissedInjury += line.GamesMissedInjury;
                    }
                }
            }

            return map.Values.ToList();
        }

        private List<PlayerSeasonStatLine> PlayerCareerStats(IEnumerable<Season>? seasons, Team? team, bool playoffsOnly = false)
        {
            var map = new Dictionary<Guid, PlayerSeasonStatLine>();
            IEnumerable<Team> teams = team == null
                ? _league.Teams ?? Enumerable.Empty<Team>()
                : new[] { team };

            foreach (var rosterTeam in teams)
            {
                foreach (var player in (rosterTeam.Roster ?? Enumerable.Empty<Player>())
                    .Concat(rosterTeam.InjuredReserve ?? Enumerable.Empty<Player>())
                    .Concat((rosterTeam.JvPool ?? new List<Player>()).Where(player => player.VarsitySeasonsPlayed > 0)))
                {
                    if (!map.ContainsKey(player.Id))
                    {
                        map[player.Id] = new PlayerSeasonStatLine
                        {
                            PlayerId = player.Id,
                            PlayerName = player.Name,
                            Pitcher = player.Role == PlayerRole.Pitcher,
                            Classification = player.Classification,
                            Positions = player.Positions,
                            Injury = InjuryEngine.InjurySummary(player),
                            MedicalTag = player.MedicalTag,
                            MedicalEligible = player.MedicalTagEligible,
                            Redshirt = player.RedshirtActive,
                            VarsitySeasons = player.VarsitySeasonsPlayed,
                            CallUpSeason = player.VarsityCallUpSeasonNumber
                        };
                    }
                }
            }

            foreach (var season in seasons ?? Enumerable.Empty<Season>())
            {
                foreach (var game in season.Games ?? Enumerable.Empty<GameResult>())
                {
                    if (playoffsOnly && !game.IsPlayoff)
                        continue;

                    var appearedThisGame = new HashSet<Guid>();
                    foreach (var line in game.Lines ?? Enumerable.Empty<PlayerGameLine>())
                    {
                        if (team != null && line.TeamId != team.Id)
                            continue;

                        if (!map.TryGetValue(line.PlayerId, out var stats))
                        {
                            stats = new PlayerSeasonStatLine
                            {
                                PlayerId = line.PlayerId,
                                PlayerName = line.PlayerName,
                                Pitcher = line.Pitcher
                            };
                            map[line.PlayerId] = stats;
                        }

                        AccumulatePlayerLine(stats, line, HasPlayerAppearance(line) && appearedThisGame.Add(line.PlayerId));
                    }
                }
            }

            return map.Values.ToList();
        }

        private static string FormatPct(int wins, int losses)
        {
            int total = wins + losses;
            if (total == 0) return ".000";
            return ((double)wins / total).ToString(".000");
        }

        private static string FormatAverage(int hits, int atBats)
        {
            if (atBats <= 0) return ".000";
            return ((double)hits / atBats).ToString(".000");
        }

        private static double AverageValue(int hits, int atBats)
            => atBats <= 0 ? 0.0 : hits / (double)atBats;

        private static double ObpValue(int hits, int walks, int hbp, int atBats, int sf)
        {
            int plateBase = atBats + walks + hbp + sf;
            return plateBase <= 0 ? 0.0 : (hits + walks + hbp) / (double)plateBase;
        }

        private static double SlgValue(int totalBases, int atBats)
            => atBats <= 0 ? 0.0 : totalBases / (double)atBats;

        private static double EraValue(int earnedRuns, int outs)
            => outs <= 0 ? 0.0 : earnedRuns * 27.0 / outs;

        private static double WhipValue(int walksAllowed, int hitsAllowed, int outs)
            => outs <= 0 ? 0.0 : (walksAllowed + hitsAllowed) / (outs / 3.0);

        private static double FieldingPctValue(int putouts, int assists, int errors)
        {
            int chances = putouts + assists + errors;
            return chances <= 0 ? 0.0 : (putouts + assists) / (double)chances;
        }

        private static string FormatInnings(int outs)
        {
            if (outs <= 0) return "0.0";
            int innings = outs / 3;
            int remainder = outs % 3;
            return innings + "." + remainder;
        }

        private static string FormatEra(int earnedRuns, int outs)
        {
            if (outs <= 0) return "0.00";
            return (earnedRuns * 27.0 / outs).ToString("0.00");
        }

        private string LoadingGameTitle(Season season, Team away, Team home)
        {
            var playoffSeries = FindMatchingPlayoffSeries(season, away.Id, home.Id);
            if (playoffSeries != null)
                return PlayoffGameTitle(season, playoffSeries);

            return RegularGameTitle(away.Id, home.Id);
        }

        private PlayoffSeries? FindMatchingPlayoffSeries(Season? season, Guid awayId, Guid homeId)
        {
            if (season == null || season.Playoffs == null)
                return null;

            return season.Playoffs
                .Where(s => !s.WinnerTeamId.HasValue &&
                    s.TeamAId != Guid.Empty &&
                    s.TeamBId != Guid.Empty &&
                    ((s.TeamAId == awayId && s.TeamBId == homeId) ||
                     (s.TeamAId == homeId && s.TeamBId == awayId)))
                .OrderByDescending(s => s.Round)
                .FirstOrDefault();
        }

        private string RegularGameTitle(Guid awayId, Guid homeId)
        {
            var away = FindTeamPlacement(awayId);
            var home = FindTeamPlacement(homeId);
            if (away == null || home == null)
                return "Regular Season Game";

            if (away.District?.Id == home.District?.Id)
                return "District Game";
            if (away.Region?.Id == home.Region?.Id)
                return "Region Game";
            if (away.Conference?.Id == home.Conference?.Id)
                return "Conference Game";

            return "Non-Conference Game";
        }

        private TeamPlacement FindTeamPlacement(Guid teamId)
        {
            if (_league?.Structure?.Conferences == null)
                return null;

            foreach (var conference in _league.Structure.Conferences)
            {
                foreach (var region in conference.Regions ?? Enumerable.Empty<Region>())
                {
                    foreach (var district in region.Districts ?? Enumerable.Empty<District>())
                    {
                        if (district.TeamIds != null && district.TeamIds.Contains(teamId))
                        {
                            return new TeamPlacement
                            {
                                Conference = conference,
                                Region = region,
                                District = district
                            };
                        }
                    }
                }
            }

            return null;
        }

        private string PlayoffGameTitle(Season season, PlayoffSeries series)
        {
            int maxRound = season?.Playoffs?.Count > 0 ? season.Playoffs.Max(s => s.Round) : series.Round;
            string name = string.IsNullOrWhiteSpace(series.RoundName)
                ? PlayoffEngine.RoundNameFor(series.Round, maxRound)
                : series.RoundName;
            return string.Equals(name, "World Series", StringComparison.OrdinalIgnoreCase)
                ? "World Series"
                : name + " Play-Off Game";
        }

        private void RefreshSeasonViews()
        {
            RefreshChampionBanner();
            RefreshGamesGrid();
            RefreshPlayoffGrid();
            RefreshDynastyGrid();
            RefreshAllStarViews();
            RefreshAwardViews();
            RefreshTeamStatsGrid();
            RefreshPlayerStatsGrid();
            RefreshHierarchyStatistics();
        }

        private void RefreshTeamStatsGrid()
        {
            if (_teamStatsGrid == null) return;
            _teamStatsGrid.Rows.Clear();
            if (_teamLeadersLabel != null) _teamLeadersLabel.Text = "";
            if (_league?.Seasons == null || _league.Teams == null) return;

            StatsScope scope = SelectedStatsScope(_teamStatsScopeCombo);
            var selectedTeam = SelectedTeam(_teamStatsTeamCombo);
            var selectedSeason = SelectedSeason(_teamStatsSeasonCombo);
            var statLines = new List<TeamSeasonStatLine>();

            if (scope == StatsScope.Career || scope == StatsScope.AllTime)
            {
                IEnumerable<Team> teams = _league.Teams.OrderBy(t => t.DisplayName);
                if (scope == StatsScope.Career && selectedTeam != null)
                    teams = teams.Where(t => t.Id == selectedTeam.Id);

                foreach (var team in teams)
                {
                    var stats = TeamCareerStats(team, _league.Seasons);
                    stats.TeamName = team.DisplayName;
                    stats.SeasonName = scope == StatsScope.AllTime ? "All-Time" : "Career";
                    statLines.Add(stats);
                    AddTeamStatsRow(stats, stats.SeasonName);
                }

                if (_teamLeadersLabel != null)
                    _teamLeadersLabel.Text = BuildTeamLeadersText(statLines);
                return;
            }

            for (int i = 0; i < _league.Seasons.Count; i++)
            {
                var season = _league.Seasons[i];
                if (selectedSeason != null && season != selectedSeason)
                    continue;

                foreach (var team in _league.Teams.OrderBy(t => t.DisplayName))
                {
                    if (selectedTeam != null && team.Id != selectedTeam.Id)
                        continue;

                    bool playoffsOnly = scope == StatsScope.Playoffs;
                    var stats = TeamSeasonStats(season, team.Id, playoffsOnly);
                    stats.TeamName = team.DisplayName;
                    stats.SeasonName = playoffsOnly ? season.Name + " Playoffs" : season.Name;
                    stats.SeasonNumber = i + 1;
                    stats.Champion = season.ChampionTeamId == team.Id;
                    statLines.Add(stats);
                    AddTeamStatsRow(stats, (playoffsOnly ? "Playoffs " : "Season ") + (i + 1));
                }
            }

            if (_teamLeadersLabel != null)
                _teamLeadersLabel.Text = BuildTeamLeadersText(statLines);
        }

        private void RefreshPlayerStatsGrid()
        {
            if (_playerStatsGrid == null) return;
            _playerStatsGrid.Rows.Clear();
            if (_playerLeadersLabel != null) _playerLeadersLabel.Text = "";

            StatsScope scope = SelectedStatsScope(_playerStatsScopeCombo);
            var team = SelectedTeam(_playerStatsTeamCombo);
            var season = SelectedSeason(_playerStatsSeasonCombo);
            if (scope == StatsScope.AllTime)
            {
                team = null;
                season = null;
            }

            List<PlayerSeasonStatLine> stats;
            string scopeLabel;
            if (scope == StatsScope.Playoffs)
            {
                if (season == null)
                {
                    stats = PlayerCareerStats(_league.Seasons, team, playoffsOnly: true);
                    scopeLabel = "Playoffs";
                }
                else if (team == null)
                {
                    stats = PlayerCareerStats(new[] { season }, null, playoffsOnly: true);
                    scopeLabel = season.Name + " Playoffs";
                }
                else
                {
                    stats = PlayerSeasonStats(season, team, playoffsOnly: true);
                    scopeLabel = season.Name + " Playoffs";
                }
            }
            else if (scope == StatsScope.Career || season == null)
            {
                stats = PlayerCareerStats(_league.Seasons, team);
                scopeLabel = scope == StatsScope.AllTime ? "All-Time" : "Career";
            }
            else
            {
                if (team == null)
                    stats = PlayerCareerStats(new[] { season }, null);
                else
                    stats = PlayerSeasonStats(season, team);
                scopeLabel = season.Name;
            }

            stats = stats
                .OrderByDescending(s => s.Pitcher)
                .ThenBy(s => s.PlayerName)
                .ToList();

            foreach (var line in stats)
                AddPlayerStatsRow(line, scopeLabel);

            if (_playerLeadersLabel != null)
                _playerLeadersLabel.Text = BuildPlayerLeadersText(stats);
        }

        private void AddTeamStatsRow(TeamSeasonStatLine stats, string scopeLabel)
        {
            _teamStatsGrid.Rows.Add(
                scopeLabel,
                stats.TeamName,
                stats.Games,
                stats.Wins,
                stats.Losses,
                stats.Ties,
                Math.Round(stats.Games <= 0 ? 0.0 : (stats.Wins + stats.Ties * 0.5) / stats.Games, 3),
                stats.RunsFor,
                stats.RunsAgainst,
                stats.RunDiff,
                Math.Round(AverageValue(stats.H, stats.AB), 3),
                Math.Round(ObpValue(stats.H, stats.BB, stats.HBP, stats.AB, stats.SF), 3),
                Math.Round(SlgValue(stats.TotalBases, stats.AB), 3),
                Math.Round(ObpValue(stats.H, stats.BB, stats.HBP, stats.AB, stats.SF) + SlgValue(stats.TotalBases, stats.AB), 3),
                stats.PlateAppearances,
                stats.ExtraBaseHits,
                stats.AB,
                stats.R > 0 ? stats.R : stats.RunsFor,
                stats.H,
                stats.Doubles,
                stats.Triples,
                stats.HR,
                stats.RBI,
                stats.BB,
                stats.IBB,
                stats.SO,
                stats.SB,
                stats.CS,
                stats.HBP,
                stats.SH,
                stats.SF,
                stats.GroundOuts,
                stats.FlyOuts,
                stats.PopOuts,
                stats.GroundedIntoDoublePlays,
                stats.ReachedOnError,
                FormatInnings(stats.IPOuts),
                Math.Round(EraValue(stats.ER, stats.IPOuts), 2),
                Math.Round(WhipValue(stats.PitchingBB, stats.HitsAllowed, stats.IPOuts), 2),
                stats.RunsAllowed,
                stats.PitchingK,
                stats.PitchingBB,
                stats.PitchingIBB,
                stats.HitsAllowed,
                stats.DoublesAllowed,
                stats.TriplesAllowed,
                stats.HomeRunsAllowed,
                stats.HitBatters,
                stats.WildPitches,
                stats.Balks,
                stats.Holds,
                stats.BlownSaves,
                stats.CompleteGames,
                stats.Shutouts,
                Math.Round(FieldingPctValue(stats.Putouts, stats.Assists, stats.Errors), 3),
                FormatInnings(stats.DefensiveOuts),
                stats.TotalChances,
                stats.Errors,
                stats.DoublePlaysTurned,
                stats.PassedBalls,
                stats.StolenBasesAllowed,
                stats.CatcherCaughtStealing,
                stats.CatcherStealAttempts <= 0 ? "" : stats.CatcherCaughtStealingPercentage.ToString("0.0%"),
                stats.InjuryGamesMissed,
                stats.Champion ? "Yes" : "",
                stats.SeasonName);
        }

        private void AddPlayerStatsRow(PlayerSeasonStatLine line, string scopeLabel)
        {
            _playerStatsGrid.Rows.Add(
                line.PlayerName,
                line.Pitcher ? "Pitcher" : "Batter",
                line.Classification == PlayerClassification.Unassigned ? "" : line.Classification,
                line.Positions,
                line.Injury,
                line.VarsitySeasons,
                line.CallUpSeason <= 0 ? "" : line.CallUpSeason.ToString(),
                line.GamesMissedInjury,
                line.Games,
                line.R,
                line.PlateAppearances,
                line.ExtraBaseHits,
                line.AB,
                line.H,
                line.Doubles,
                line.Triples,
                line.HR,
                line.RBI,
                line.BB,
                line.IBB,
                line.SO,
                line.SB,
                line.CS,
                line.HBP,
                line.SH,
                line.SF,
                line.GroundOuts,
                line.FlyOuts,
                line.PopOuts,
                line.GroundedIntoDoublePlays,
                line.ReachedOnError,
                Math.Round(AverageValue(line.H, line.AB), 3),
                Math.Round(ObpValue(line.H, line.BB, line.HBP, line.AB, line.SF), 3),
                Math.Round(SlgValue(line.TotalBases, line.AB), 3),
                Math.Round(ObpValue(line.H, line.BB, line.HBP, line.AB, line.SF) + SlgValue(line.TotalBases, line.AB), 3),
                line.TotalBases,
                FormatInnings(line.IPOuts),
                line.PitchingWins,
                line.PitchingLosses,
                line.Saves,
                line.Holds,
                line.BlownSaves,
                line.CompleteGames,
                line.Shutouts,
                line.K,
                line.ER,
                line.RunsAllowed,
                Math.Round(EraValue(line.ER, line.IPOuts), 2),
                Math.Round(WhipValue(line.WalksAllowed, line.HitsAllowed, line.IPOuts), 2),
                line.HitsAllowed,
                line.DoublesAllowed,
                line.TriplesAllowed,
                line.WalksAllowed,
                line.IntentionalWalksAllowed,
                line.HomeRunsAllowed,
                line.HitBatters,
                line.WildPitches,
                line.Balks,
                line.BattersFaced,
                line.PitchCount,
                Math.Round(FieldingPctValue(line.Putouts, line.Assists, line.Errors), 3),
                FormatInnings(line.DefensiveOuts),
                line.TotalChances,
                line.Putouts,
                line.Assists,
                line.Errors,
                line.DefensiveDoublePlays,
                line.PassedBalls,
                line.StolenBasesAllowed,
                line.CatcherCaughtStealing,
                line.CatcherStealAttempts <= 0 ? "" : line.CatcherCaughtStealingPercentage.ToString("0.0%"),
                scopeLabel);
        }

        private void RefreshAllStarViews()
        {
            if (_allStarCandidatesGrid == null) return;
            _allStarCandidatesGrid.Rows.Clear();
            _allStarSelectionsGrid.Rows.Clear();
            _allStarGameStatsGrid.Rows.Clear();

            var season = SelectedSeason(_allStarSeasonCombo);
            if (season == null)
            {
                if (_allStarSummaryLabel != null)
                    _allStarSummaryLabel.Text = "Select a season.";
                return;
            }

            season.AllStarSelections ??= new List<SeasonAllStarSelection>();
            var candidates = BuildAllStarCandidates(season);
            foreach (var candidate in candidates.Where(c => !season.AllStarSelections.Any(s => s.PlayerId == c.PlayerId)))
                AddAllStarCandidateRow(candidate);

            foreach (var selection in season.AllStarSelections
                .OrderBy(s => s.AllStarTeam)
                .ThenByDescending(s => s.SelectionScore)
                .ThenBy(s => s.PlayerName))
            {
                AddAllStarSelectionRow(selection);
            }

            RefreshAllStarGameStatsGrid(season);

            if (_allStarSummaryLabel != null)
            {
                string status = season.ChampionTeamId.HasValue ? "Championship complete" : "Complete championship before All-Star Game";
                string game = season.AllStarGame == null
                    ? "All-Star Game not played"
                    : season.AllStarGame.AwayName + " " + season.AllStarGame.AwayScore + ", " + season.AllStarGame.HomeName + " " + season.AllStarGame.HomeScore;
                int blue = season.AllStarSelections.Count(s => s.AllStarTeam == "Blue All-Stars");
                int red = season.AllStarSelections.Count(s => s.AllStarTeam == "Red All-Stars");
                _allStarSummaryLabel.Text = status + "    |    Blue: " + blue + "    Red: " + red + "    |    " + game;
            }
        }

        private void AddAllStarCandidateRow(AllStarCandidate candidate)
        {
            int row = _allStarCandidatesGrid.Rows.Add(
                candidate.AllStarTeam,
                candidate.Score,
                candidate.PlayerName,
                candidate.TeamName,
                candidate.Role,
                candidate.Positions,
                candidate.Stats.Games,
                candidate.Stats.H,
                candidate.Stats.HR,
                candidate.Stats.RBI,
                candidate.Stats.SB,
                candidate.Stats.PitchingWins,
                candidate.Stats.Saves,
                candidate.Stats.K,
                candidate.Stats.AB <= 0 ? "" : AverageValue(candidate.Stats.H, candidate.Stats.AB).ToString("0.000"),
                candidate.Stats.AB <= 0 ? "" : (ObpValue(candidate.Stats.H, candidate.Stats.BB, candidate.Stats.HBP, candidate.Stats.AB, candidate.Stats.SF) + SlgValue(candidate.Stats.TotalBases, candidate.Stats.AB)).ToString("0.000"),
                candidate.Stats.IPOuts <= 0 ? "" : EraValue(candidate.Stats.ER, candidate.Stats.IPOuts).ToString("0.00"));
            _allStarCandidatesGrid.Rows[row].Tag = candidate;
        }

        private void AddAllStarSelectionRow(SeasonAllStarSelection selection)
        {
            var player = PlayerById(selection.PlayerId);
            var stats = PlayerSeasonStats(SelectedSeason(_allStarSeasonCombo), TeamById(selection.TeamId))
                .FirstOrDefault(s => s.PlayerId == selection.PlayerId) ?? new PlayerSeasonStatLine { PlayerName = selection.PlayerName };
            int row = _allStarSelectionsGrid.Rows.Add(
                selection.AllStarTeam,
                selection.SelectionScore,
                selection.PlayerName,
                selection.TeamName,
                selection.Role,
                selection.Positions,
                stats.Games,
                stats.H,
                stats.HR,
                stats.RBI,
                stats.SB,
                stats.PitchingWins,
                stats.Saves,
                stats.K,
                stats.AB <= 0 ? "" : AverageValue(stats.H, stats.AB).ToString("0.000"),
                stats.AB <= 0 ? "" : (ObpValue(stats.H, stats.BB, stats.HBP, stats.AB, stats.SF) + SlgValue(stats.TotalBases, stats.AB)).ToString("0.000"),
                stats.IPOuts <= 0 ? "" : EraValue(stats.ER, stats.IPOuts).ToString("0.00"));
            _allStarSelectionsGrid.Rows[row].Tag = selection;
        }

        private void RefreshAllStarGameStatsGrid(Season season)
        {
            if (_allStarGameStatsGrid == null)
                return;

            IEnumerable<AllStarStatRow> rows = SelectedAllStarStatsScope() switch
            {
                AllStarStatsScope.PlayerCareer => BuildAllStarPlayerCareerRows(),
                AllStarStatsScope.TeamSideHistory => BuildAllStarSideHistoryRows(),
                AllStarStatsScope.AllTimeLeaders => BuildAllStarLeaderRows(),
                _ => BuildSelectedAllStarGameRows(season)
            };

            foreach (var row in rows)
                AddAllStarGameStatsRow(row);
        }

        private string AllStarTeamNameForLine(Season season, PlayerGameLine line)
        {
            var selection = season?.AllStarSelections?.FirstOrDefault(s => s.PlayerId == line.PlayerId);
            if (!string.IsNullOrWhiteSpace(selection?.AllStarTeam))
                return selection.AllStarTeam;

            return "";
        }

        private AllStarStatsScope SelectedAllStarStatsScope()
        {
            string value = Convert.ToString(_allStarStatsScopeCombo?.SelectedItem) ?? "";
            if (value.Equals("Player Career", StringComparison.OrdinalIgnoreCase)) return AllStarStatsScope.PlayerCareer;
            if (value.Equals("Team/Side History", StringComparison.OrdinalIgnoreCase)) return AllStarStatsScope.TeamSideHistory;
            if (value.Equals("All-Time Leaders", StringComparison.OrdinalIgnoreCase)) return AllStarStatsScope.AllTimeLeaders;
            return AllStarStatsScope.SelectedGame;
        }

        private IEnumerable<AllStarStatRow> BuildSelectedAllStarGameRows(Season season)
        {
            if (season?.AllStarGame?.Lines == null)
                return Enumerable.Empty<AllStarStatRow>();

            var selections = AllStarSelectionsByPlayer(season);
            return season.AllStarGame.Lines
                .OrderBy(l => AllStarTeamNameForLine(season, l))
                .ThenByDescending(l => l.Pitcher)
                .ThenBy(l => l.PlayerName)
                .Select(line =>
                {
                    selections.TryGetValue(line.PlayerId, out var selection);
                    return new AllStarStatRow
                    {
                        AllStarTeam = AllStarTeamNameForLine(season, line),
                        PlayerName = line.PlayerName,
                        OriginalTeam = selection?.TeamName ?? "",
                        Role = line.Pitcher ? "Pitcher" : "Batter",
                        Line = line
                    };
                })
                .ToList();
        }

        private IEnumerable<AllStarStatRow> BuildAllStarPlayerCareerRows()
        {
            var rows = new Dictionary<Guid, AllStarStatRow>();
            foreach (var item in AllStarLineItems())
            {
                if (!rows.TryGetValue(item.Line.PlayerId, out var row))
                {
                    row = new AllStarStatRow
                    {
                        AllStarTeam = "Career",
                        PlayerName = item.Line.PlayerName,
                        OriginalTeam = item.Selection?.TeamName ?? "",
                        Role = item.Line.Pitcher ? "Pitcher" : "Batter",
                        Line = new PlayerGameLine { PlayerId = item.Line.PlayerId, PlayerName = item.Line.PlayerName }
                    };
                    rows[item.Line.PlayerId] = row;
                }

                if (item.Line.Pitcher)
                    row.Role = "Pitcher";
                AccumulateAllStarLine(row.Line, item.Line);
            }

            return rows.Values
                .OrderByDescending(r => AllStarTotalValue(r.Line))
                .ThenBy(r => r.PlayerName)
                .ToList();
        }

        private IEnumerable<AllStarStatRow> BuildAllStarSideHistoryRows()
        {
            var rows = new Dictionary<string, AllStarStatRow>(StringComparer.OrdinalIgnoreCase);
            var gamesBySide = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var season in _league?.Seasons ?? Enumerable.Empty<Season>())
            {
                if (season.AllStarGame != null)
                {
                    if (!string.IsNullOrWhiteSpace(season.AllStarGame.AwayName))
                        gamesBySide[season.AllStarGame.AwayName] = gamesBySide.TryGetValue(season.AllStarGame.AwayName, out int a) ? a + 1 : 1;
                    if (!string.IsNullOrWhiteSpace(season.AllStarGame.HomeName))
                        gamesBySide[season.AllStarGame.HomeName] = gamesBySide.TryGetValue(season.AllStarGame.HomeName, out int h) ? h + 1 : 1;
                }
            }

            foreach (var item in AllStarLineItems())
            {
                string side = item.Selection?.AllStarTeam ?? "";
                if (string.IsNullOrWhiteSpace(side))
                    continue;

                if (!rows.TryGetValue(side, out var row))
                {
                    row = new AllStarStatRow
                    {
                        AllStarTeam = side,
                        PlayerName = side,
                        OriginalTeam = "All-Star Side",
                        Role = "Team",
                        Line = new PlayerGameLine { PlayerName = side }
                    };
                    rows[side] = row;
                }

                AccumulateAllStarLine(row.Line, item.Line);
            }

            foreach (var row in rows.Values)
            {
                int games = gamesBySide.TryGetValue(row.AllStarTeam, out int count) ? count : 0;
                row.PlayerName = row.AllStarTeam + (games > 0 ? " (" + games + " games)" : "");
            }

            return rows.Values.OrderBy(r => r.AllStarTeam).ToList();
        }

        private IEnumerable<AllStarStatRow> BuildAllStarLeaderRows()
        {
            var career = BuildAllStarPlayerCareerRows().ToList();
            var leaders = new List<AllStarStatRow>();

            AddAllStarLeader(leaders, career, "Hits Leader", r => r.Line.H, r => r.Line.H > 0);
            AddAllStarLeader(leaders, career, "HR Leader", r => r.Line.HR, r => r.Line.HR > 0);
            AddAllStarLeader(leaders, career, "RBI Leader", r => r.Line.RBI, r => r.Line.RBI > 0);
            AddAllStarLeader(leaders, career, "SB Leader", r => r.Line.SB, r => r.Line.SB > 0);
            AddAllStarLeader(leaders, career, "Strikeout Leader", r => r.Line.K, r => r.Line.K > 0);
            AddAllStarLeader(leaders, career, "ERA Leader", r => -EraValue(r.Line.ER, r.Line.IPOuts), r => r.Line.IPOuts > 0);
            AddAllStarLeader(leaders, career, "WHIP Leader", r => -WhipValue(r.Line.WalksAllowed, r.Line.HitsAllowed, r.Line.IPOuts), r => r.Line.IPOuts > 0);

            return leaders;
        }

        private static void AddAllStarLeader(List<AllStarStatRow> leaders, List<AllStarStatRow> rows, string label, Func<AllStarStatRow, double> rank, Func<AllStarStatRow, bool> eligible)
        {
            var leader = rows.Where(eligible).OrderByDescending(rank).FirstOrDefault();
            if (leader == null)
                return;

            leaders.Add(new AllStarStatRow
            {
                AllStarTeam = label,
                PlayerName = leader.PlayerName,
                OriginalTeam = leader.OriginalTeam,
                Role = leader.Role,
                Line = leader.Line
            });
        }

        private IEnumerable<(Season Season, PlayerGameLine Line, SeasonAllStarSelection Selection)> AllStarLineItems()
        {
            foreach (var season in _league?.Seasons ?? Enumerable.Empty<Season>())
            {
                if (season.AllStarGame?.Lines == null)
                    continue;

                var selections = AllStarSelectionsByPlayer(season);
                foreach (var line in season.AllStarGame.Lines)
                {
                    selections.TryGetValue(line.PlayerId, out var selection);
                    yield return (season, line, selection);
                }
            }
        }

        private static Dictionary<Guid, SeasonAllStarSelection> AllStarSelectionsByPlayer(Season season)
        {
            return (season.AllStarSelections ?? new List<SeasonAllStarSelection>())
                .GroupBy(s => s.PlayerId)
                .ToDictionary(g => g.Key, g => g.First());
        }

        private void AddAllStarGameStatsRow(AllStarStatRow row)
        {
            PlayerGameLine line = row.Line ?? new PlayerGameLine();
            _allStarGameStatsGrid.Rows.Add(
                row.AllStarTeam,
                row.PlayerName,
                row.OriginalTeam,
                row.Role,
                line.R,
                line.PlateAppearances,
                line.ExtraBaseHits,
                line.AB,
                line.H,
                line.Doubles,
                line.Triples,
                line.HR,
                line.RBI,
                line.BB,
                line.IBB,
                line.SO,
                line.SB,
                line.GroundedIntoDoublePlays,
                line.ReachedOnError,
                FormatInnings(line.IPOuts),
                line.Holds,
                line.BlownSaves,
                line.CompleteGames,
                line.Shutouts,
                line.K,
                line.ER,
                line.RunsAllowed,
                line.IPOuts <= 0 ? "" : EraValue(line.ER, line.IPOuts).ToString("0.00"),
                line.IPOuts <= 0 ? "" : WhipValue(line.WalksAllowed, line.HitsAllowed, line.IPOuts).ToString("0.00"),
                line.HitsAllowed,
                line.DoublesAllowed,
                line.TriplesAllowed,
                line.WalksAllowed,
                line.IntentionalWalksAllowed,
                line.WildPitches,
                line.Balks,
                line.PassedBalls,
                line.Errors,
                line.DefensiveDoublePlays,
                FormatInnings(line.DefensiveOuts),
                line.TotalChances,
                line.StolenBasesAllowed,
                line.CatcherCaughtStealing,
                line.CatcherStealAttempts <= 0 ? "" : line.CatcherCaughtStealingPercentage.ToString("0.0%"),
                line.PitchCount);
        }

        private static void AccumulateAllStarLine(PlayerGameLine target, PlayerGameLine source)
        {
            target.Pitcher = target.Pitcher || source.Pitcher;
            target.R += source.R;
            target.AB += source.AB;
            target.H += source.H;
            target.Doubles += source.Doubles;
            target.Triples += source.Triples;
            target.HR += source.HR;
            target.RBI += source.RBI;
            target.BB += source.BB;
            target.IBB += source.IBB;
            target.SO += source.SO;
            target.SB += source.SB;
            target.CS += source.CS;
            target.HBP += source.HBP;
            target.SH += source.SH;
            target.SF += source.SF;
            target.FlyOuts += source.FlyOuts;
            target.GroundOuts += source.GroundOuts;
            target.PopOuts += source.PopOuts;
            target.GroundedIntoDoublePlays += source.GroundedIntoDoublePlays;
            target.ReachedOnError += source.ReachedOnError;
            target.IPOuts += source.IPOuts;
            target.ER += source.ER;
            target.RunsAllowed += source.RunsAllowed;
            target.K += source.K;
            target.HitsAllowed += source.HitsAllowed;
            target.DoublesAllowed += source.DoublesAllowed;
            target.TriplesAllowed += source.TriplesAllowed;
            target.WalksAllowed += source.WalksAllowed;
            target.IntentionalWalksAllowed += source.IntentionalWalksAllowed;
            target.HomeRunsAllowed += source.HomeRunsAllowed;
            target.HitBatters += source.HitBatters;
            target.WildPitches += source.WildPitches;
            target.Balks += source.Balks;
            target.BattersFaced += source.BattersFaced;
            target.PitchCount += source.PitchCount;
            target.Wins += source.Wins;
            target.Losses += source.Losses;
            target.Saves += source.Saves;
            target.Holds += source.Holds;
            target.BlownSaves += source.BlownSaves;
            target.CompleteGames += source.CompleteGames;
            target.Shutouts += source.Shutouts;
            target.Putouts += source.Putouts;
            target.Assists += source.Assists;
            target.Errors += source.Errors;
            target.DefensiveOuts += source.DefensiveOuts;
            target.DefensiveDoublePlays += source.DefensiveDoublePlays;
            target.PassedBalls += source.PassedBalls;
            target.StolenBasesAllowed += source.StolenBasesAllowed;
            target.CatcherCaughtStealing += source.CatcherCaughtStealing;
            target.GamesMissedInjury += source.GamesMissedInjury;
        }

        private static double AllStarTotalValue(PlayerGameLine line)
        {
            return line.H * 2.0 + line.HR * 6.0 + line.RBI * 2.0 + line.SB * 1.5 + line.K * 1.4 + line.IPOuts / 3.0;
        }

        private void RefreshAwardViews()
        {
            if (_awardRacesGrid == null) return;
            ClearAwardGrids();
            var season = SelectedSeason(_awardSeasonCombo);
            if (season == null)
            {
                if (_awardSummaryLabel != null)
                    _awardSummaryLabel.Text = "Select a season.";
                return;
            }

            season.Awards ??= new List<SeasonAwardSelection>();
            var candidates = BuildSeasonAwardCandidates(season);
            FillAwardGrid(_awardRacesGrid, candidates.Where(c => c.Category == "Award Race"));
            FillAwardGrid(_positionAwardsGrid, candidates.Where(c => c.Category == "Position Award"));
            FillAwardGrid(_goldGloveGrid, candidates.Where(c => c.Category == "Gold Glove"));
            FillAwardGrid(_silverBatGrid, candidates.Where(c => c.Category == "Silver Bat"));
            FillAwardGrid(_awardFinalistsGrid, candidates);
            FillAwardHistoryGrid(season);

            if (_awardSummaryLabel != null)
            {
                int winners = season.Awards.Count(a => a.Winner);
                _awardSummaryLabel.Text = season.Awards.Count == 0
                    ? "Live award races are active. Finalize after the All-Star Game and before offseason."
                    : "Awards finalized: " + winners + " winner(s), " + season.Awards.Count(a => !a.Winner) + " finalist record(s).";
            }
        }

        private void ClearAwardGrids()
        {
            foreach (var grid in new[] { _awardRacesGrid, _positionAwardsGrid, _goldGloveGrid, _silverBatGrid, _awardFinalistsGrid, _awardHistoryGrid })
                grid?.Rows.Clear();
        }

        private void FillAwardGrid(DataGridView grid, IEnumerable<AwardCandidate> candidates)
        {
            foreach (var c in candidates.OrderBy(c => c.AwardName).ThenBy(c => c.Rank))
                AddAwardCandidateRow(grid, c);
        }

        private void AddAwardCandidateRow(DataGridView grid, AwardCandidate c)
        {
            grid.Rows.Add(c.AwardName, c.Category, c.Rank, c.Winner ? "Winner" : "Finalist", c.PlayerName,
                c.TeamName, c.Position, Math.Round(c.Score, 1), c.KeyStats);
        }

        private void FillAwardHistoryGrid(Season season)
        {
            foreach (var award in (season.Awards ?? new List<SeasonAwardSelection>())
                .OrderBy(a => a.AwardName)
                .ThenBy(a => a.Rank))
            {
                _awardHistoryGrid.Rows.Add(award.AwardName, award.Category, award.Rank, award.Winner ? "Winner" : "Finalist",
                    award.PlayerName, award.TeamName, award.Position, Math.Round(award.Score, 1), award.KeyStats);
            }
        }

        private void FinalizeSeasonAwards()
        {
            var season = SelectedSeason(_awardSeasonCombo);
            if (season == null) return;
            if (season.AllStarGame == null)
            {
                MessageBox.Show(this, "Awards are finalized after the All-Star Game and before offseason.");
                return;
            }

            var candidates = BuildSeasonAwardCandidates(season);
            season.Awards = candidates.Select(c => new SeasonAwardSelection
            {
                PlayerId = c.PlayerId,
                TeamId = c.TeamId,
                PlayerName = c.PlayerName,
                TeamName = c.TeamName,
                AwardName = c.AwardName,
                Category = c.Category,
                Position = c.Position,
                Rank = c.Rank,
                Winner = c.Winner,
                Score = c.Score,
                KeyStats = c.KeyStats,
                FinalizedAt = DateTime.Now
            }).ToList();

            EnsureJohnnyOatesHallOfFameEntry(season);

            MarkDirty();
            RefreshAwardViews();
            LoadSelectedTeam();
        }

        private List<AwardCandidate> BuildSeasonAwardCandidates(Season season)
        {
            var playerRows = BuildAwardPlayerRows(season);
            var all = new List<AwardCandidate>();

            AddAwardTopFive(all, "Player of the Year", "Award Race", "", playerRows, r => r.OverallScore);
            AddAwardTopFive(all, "Pitcher of the Year", "Award Race", "P", playerRows.Where(r => r.Player.Role == PlayerRole.Pitcher), r => r.PitchingScore);
            AddAwardTopFive(all, "Cy Young Award", "Award Race", "P", playerRows.Where(r => r.Player.Role == PlayerRole.Pitcher), r => r.Stats.PitchingWins);
            AddAwardTopFive(all, "Nolan Ryan Award", "Award Race", "P", playerRows.Where(r => r.Player.Role == PlayerRole.Pitcher), r => r.Stats.K);
            AddAwardTopFive(all, "Babe Ruth Award", "Award Race", "HR", playerRows.Where(r => r.Player.Role != PlayerRole.Pitcher), r => r.Stats.HR);
            AddAwardTopFive(all, "Chuck Knoblauch Award", "Award Race", "2B", playerRows.Where(r => r.Player.Role != PlayerRole.Pitcher), r => r.Stats.Doubles);
            AddAwardTopFive(all, "John Wetteland Award", "Award Race", "SV", playerRows.Where(r => r.Player.Role == PlayerRole.Pitcher), r => r.Stats.Saves);
            AddAwardTopFive(all, "Freshman of the Year", "Award Race", "FR", playerRows.Where(r => r.Player.Classification == PlayerClassification.Freshman), r => r.OverallScore);
            AddJohnnyOatesAward(all, season);

            foreach (string pos in AwardPositions())
            {
                var eligible = playerRows.Where(r => IsEligibleForAwardPosition(r.Player, pos)).ToList();
                AddAwardTopFive(all, PositionAwardName(pos), "Position Award", pos, eligible, r => r.OverallScore);
                AddAwardTopFive(all, pos + " Gold Glove", "Gold Glove", pos, eligible, r => r.DefenseScore);
                AddAwardTopFive(all, pos + " Silver Bat", "Silver Bat", pos, eligible, r => r.OffenseScore);
            }

            return all;
        }

        private void AddJohnnyOatesAward(List<AwardCandidate> target, Season season)
        {
            if (season == null || !season.ChampionTeamId.HasValue)
                return;

            var champion = TeamById(season.ChampionTeamId.Value);
            if (champion == null)
                return;

            champion.NormalizeText();
            Guid winningCoachId = WinningChampionshipCoachId(season, champion);
            var coach = CoachById(champion, winningCoachId);
            string coachName = CoachDisplayName(champion, coach);
            target.Add(new AwardCandidate
            {
                AwardName = "Johnny Oates Award",
                Category = "Coach Award",
                Position = "Coach",
                PlayerId = winningCoachId,
                TeamId = champion.Id,
                PlayerName = coachName,
                TeamName = champion.DisplayName,
                Role = PlayerRole.Batter,
                Score = 1000 + CountTeamWins(season, champion.Id),
                Rank = 1,
                KeyStats = "World Series champion coach, " + TeamRecordText(season, champion.Id)
            });
        }

        private Guid WinningChampionshipCoachId(Season season, Team champion)
        {
            var series = (season?.Playoffs ?? Enumerable.Empty<PlayoffSeries>())
                .FirstOrDefault(s => s.WinnerTeamId == champion.Id && IsFinalChampionshipSeries(s));
            if (series?.WinnerCoachId.HasValue == true && series.WinnerCoachId.Value != Guid.Empty)
                return series.WinnerCoachId.Value;
            return champion?.CoachId ?? Guid.Empty;
        }

        private Coach CoachById(Team team, Guid coachId)
        {
            EnsureTeamCoaches(team);
            return team?.Coaches?.FirstOrDefault(c => c.Id == coachId)
                ?? team?.Coaches?.FirstOrDefault(c => c.Id == team.CoachId);
        }

        private sealed class AwardPlayerRow
        {
            public Player Player { get; set; }
            public Team Team { get; set; }
            public PlayerSeasonStatLine Stats { get; set; }
            public double OffenseScore { get; set; }
            public double PitchingScore { get; set; }
            public double DefenseScore { get; set; }
            public double OverallScore => Math.Max(OffenseScore, PitchingScore) + DefenseScore * 0.18;
        }

        private List<AwardPlayerRow> BuildAwardPlayerRows(Season season)
        {
            var rows = new List<AwardPlayerRow>();
            foreach (var team in _league?.Teams ?? Enumerable.Empty<Team>())
            {
                var statsByPlayer = PlayerSeasonStats(season, team).ToDictionary(s => s.PlayerId);
                foreach (var player in team.Roster ?? Enumerable.Empty<Player>())
                {
                    if (player == null || player.RedshirtActive || !InjuryEngine.IsAvailable(player))
                        continue;

                    statsByPlayer.TryGetValue(player.Id, out var stats);
                    stats ??= new PlayerSeasonStatLine
                    {
                        PlayerId = player.Id,
                        PlayerName = player.Name,
                        Pitcher = player.Role == PlayerRole.Pitcher,
                        Classification = player.Classification,
                        Positions = player.Positions
                    };

                    rows.Add(new AwardPlayerRow
                    {
                        Player = player,
                        Team = team,
                        Stats = stats,
                        OffenseScore = OffensiveAwardScore(player, stats),
                        PitchingScore = PitchingAwardScore(player, stats),
                        DefenseScore = DefensiveAwardScore(player, stats)
                    });
                }
            }
            return rows;
        }

        private void AddAwardTopFive(List<AwardCandidate> target, string awardName, string category, string position, IEnumerable<AwardPlayerRow> source, Func<AwardPlayerRow, double> score)
        {
            int rank = 1;
            foreach (var row in source
                .Select(r => new { Row = r, Score = score(r) })
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.Row.Player.Name)
                .Take(5))
            {
                target.Add(new AwardCandidate
                {
                    AwardName = awardName,
                    Category = category,
                    Position = position,
                    PlayerId = row.Row.Player.Id,
                    TeamId = row.Row.Team.Id,
                    PlayerName = row.Row.Player.Name,
                    TeamName = row.Row.Team.DisplayName,
                    Role = row.Row.Player.Role,
                    Score = row.Score,
                    Rank = rank++,
                    KeyStats = AwardKeyStats(row.Row.Stats)
                });
            }
        }

        private static double OffensiveAwardScore(Player player, PlayerSeasonStatLine s)
        {
            double ops = ObpValue(s.H, s.BB, s.HBP, s.AB, s.SF) + SlgValue(s.TotalBases, s.AB);
            return s.H * 1.7 + s.HR * 7.5 + s.RBI * 2.2 + s.R * 1.1 + s.SB * 1.3 + ops * 75.0 + player.Contact * 0.25 + player.Power * 0.35;
        }

        private static double PitchingAwardScore(Player player, PlayerSeasonStatLine s)
        {
            double eraBonus = s.IPOuts <= 0 ? 0 : Math.Max(0, 5.00 - EraValue(s.ER, s.IPOuts)) * 14.0;
            double whipBonus = s.IPOuts <= 0 ? 0 : Math.Max(0, 1.55 - WhipValue(s.WalksAllowed, s.HitsAllowed, s.IPOuts)) * 24.0;
            return s.K * 2.2 + s.PitchingWins * 9.0 + s.Saves * 6.5 + (s.IPOuts / 3.0) * 1.4 + eraBonus + whipBonus + player.Pitching * 0.45;
        }

        private static double DefensiveAwardScore(Player player, PlayerSeasonStatLine s)
        {
            return FieldingPctValue(s.Putouts, s.Assists, s.Errors) * 100.0 + s.Putouts * 0.25 + s.Assists * 0.5 - s.Errors * 8.0 + player.Fielding * 0.45 + player.Durability * 0.25;
        }

        private static string AwardKeyStats(PlayerSeasonStatLine s)
        {
            string avg = s.AB <= 0 ? ".000" : AverageValue(s.H, s.AB).ToString("0.000");
            string ops = s.AB <= 0 ? ".000" : (ObpValue(s.H, s.BB, s.HBP, s.AB, s.SF) + SlgValue(s.TotalBases, s.AB)).ToString("0.000");
            string era = s.IPOuts <= 0 ? "" : ", ERA " + EraValue(s.ER, s.IPOuts).ToString("0.00");
            return "AVG " + avg + ", OPS " + ops + ", HR " + s.HR + ", RBI " + s.RBI + ", K " + s.K + era;
        }

        private static string[] AwardPositions()
            => new[] { "C", "1B", "2B", "3B", "SS", "LF", "CF", "RF", "DH", "SP", "RP", "UTIL" };

        private static string PositionAwardName(string position)
        {
            return position switch
            {
                "C" => "Ivan Rodriguez Award",
                "UTIL" => "Rusty Greer Award",
                _ => "Best " + position
            };
        }

        private static bool IsEligibleForAwardPosition(Player player, string position)
        {
            string positions = (player.Positions ?? "").ToUpperInvariant();
            var parts = positions.Split(new[] { '/', ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (position == "SP" || position == "RP") return player.Role == PlayerRole.Pitcher;
            if (position == "UTIL") return parts.Length > 1 || positions.Contains("UTIL");
            if (position == "DH") return positions.Contains("DH") || player.Role != PlayerRole.Pitcher;
            if ((position == "LF" || position == "CF" || position == "RF") && parts.Contains("OF")) return true;
            return parts.Contains(position);
        }

        private List<AllStarCandidate> BuildAllStarCandidates(Season season)
        {
            var candidates = new List<AllStarCandidate>();
            if (season == null || _league?.Teams == null) return candidates;

            foreach (var team in _league.Teams)
            {
                var statsByPlayer = PlayerSeasonStats(season, team).ToDictionary(s => s.PlayerId);
                foreach (var player in (team.Roster ?? Enumerable.Empty<Player>())
                    .Concat(team.InjuredReserve ?? Enumerable.Empty<Player>())
                    .Concat((team.JvPool ?? new List<Player>()).Where(player => player.VarsitySeasonsPlayed > 0)))
                {
                    if (player == null || player.RedshirtActive)
                        continue;

                    statsByPlayer.TryGetValue(player.Id, out var stats);
                    stats ??= new PlayerSeasonStatLine
                    {
                        PlayerId = player.Id,
                        PlayerName = player.Name,
                        Pitcher = player.Role == PlayerRole.Pitcher,
                        Classification = player.Classification,
                        Positions = player.Positions,
                        Injury = InjuryEngine.InjurySummary(player),
                        MedicalTag = player.MedicalTag,
                        MedicalEligible = player.MedicalTagEligible,
                        Redshirt = player.RedshirtActive,
                        VarsitySeasons = player.VarsitySeasonsPlayed,
                        CallUpSeason = player.VarsityCallUpSeasonNumber
                    };

                    candidates.Add(new AllStarCandidate
                    {
                        PlayerId = player.Id,
                        TeamId = team.Id,
                        PlayerName = player.Name,
                        TeamName = team.DisplayName,
                        Role = player.Role,
                        Positions = player.Positions,
                        AllStarTeam = AllStarSideForTeam(team.Id),
                        Score = AllStarScore(player, stats),
                        Stats = stats
                    });
                }
            }

            return candidates
                .Where(c => c.Score > 0)
                .OrderBy(c => c.AllStarTeam)
                .ThenByDescending(c => c.Score)
                .ThenBy(c => c.PlayerName)
                .ToList();
        }

        private static int AllStarScore(Player player, PlayerSeasonStatLine stats)
        {
            double hitter = stats.H * 2.0 + stats.HR * 8.0 + stats.RBI * 2.5 + stats.SB * 2.0 +
                (ObpValue(stats.H, stats.BB, stats.HBP, stats.AB, stats.SF) + SlgValue(stats.TotalBases, stats.AB)) * 35.0 +
                player.Overall * 0.65;
            double pitcher = stats.PitchingWins * 10.0 + stats.Saves * 7.0 + stats.K * 2.0 + (stats.IPOuts / 3.0) * 1.5 +
                (stats.IPOuts > 0 ? Math.Max(0, 4.75 - EraValue(stats.ER, stats.IPOuts)) * 12.0 : 0) +
                player.Overall * 0.65;
            return (int)Math.Round(player.Role == PlayerRole.Pitcher ? Math.Max(pitcher, hitter * 0.45) : Math.Max(hitter, pitcher * 0.45));
        }

        private string AllStarSideForTeam(Guid teamId)
        {
            var placement = FindTeamPlacement(teamId);
            int index = placement?.Conference == null || _league?.Structure?.Conferences == null
                ? _league?.Teams?.FindIndex(t => t.Id == teamId) ?? -1
                : _league.Structure.Conferences.FindIndex(c => c.Id == placement.Conference.Id);
            return Math.Max(0, index) % 2 == 0 ? "Blue All-Stars" : "Red All-Stars";
        }

        private void AddSelectedAllStarCandidate()
        {
            var season = SelectedSeason(_allStarSeasonCombo);
            if (season == null || _allStarCandidatesGrid?.CurrentRow?.Tag is not AllStarCandidate candidate) return;
            season.AllStarSelections ??= new List<SeasonAllStarSelection>();
            if (season.AllStarSelections.Any(s => s.PlayerId == candidate.PlayerId)) return;
            season.AllStarSelections.Add(ToAllStarSelection(candidate, starter: false));
            ApplyAllStarTag(season, candidate.PlayerId);
            MarkDirty();
            RefreshAllStarViews();
            LoadSelectedTeam();
        }

        private void RemoveSelectedAllStarSelection()
        {
            var season = SelectedSeason(_allStarSeasonCombo);
            if (season == null || _allStarSelectionsGrid?.CurrentRow?.Tag is not SeasonAllStarSelection selection) return;
            season.AllStarSelections.Remove(selection);
            RemoveAllStarTag(season, selection.PlayerId);
            MarkDirty();
            RefreshAllStarViews();
            LoadSelectedTeam();
        }

        private void AutoFinalizeAllStars()
        {
            var season = SelectedSeason(_allStarSeasonCombo);
            if (season == null) return;
            var candidates = BuildAllStarCandidates(season);
            var selected = new List<SeasonAllStarSelection>();

            foreach (string side in new[] { "Blue All-Stars", "Red All-Stars" })
            {
                var sideCandidates = candidates.Where(c => c.AllStarTeam == side).ToList();
                selected.AddRange(BuildAllStarSelectionsForSide(sideCandidates));
            }

            if (selected.Count == 0)
            {
                MessageBox.Show(this, "No All-Star candidates are available yet. Play or simulate season games first.");
                return;
            }

            ClearAllStarTagsForSeason(season);
            season.AllStarSelections = selected
                .GroupBy(s => s.PlayerId)
                .Select(g => g.OrderByDescending(s => s.SelectionScore).First())
                .ToList();
            foreach (var selection in season.AllStarSelections)
                ApplyAllStarTag(season, selection.PlayerId);

            MarkDirty();
            RefreshAllStarViews();
            LoadSelectedTeam();
        }

        private static IEnumerable<SeasonAllStarSelection> BuildAllStarSelectionsForSide(List<AllStarCandidate> sideCandidates)
        {
            var selected = new List<SeasonAllStarSelection>();
            var selectedIds = new HashSet<Guid>();
            var selectedCandidates = new List<AllStarCandidate>();

            void AddCandidate(AllStarCandidate candidate, bool starter)
            {
                if (candidate == null || !selectedIds.Add(candidate.PlayerId))
                    return;
                selected.Add(ToAllStarSelection(candidate, starter));
                selectedCandidates.Add(candidate);
            }

            foreach (var candidate in sideCandidates
                .Where(c => c.Role == PlayerRole.Pitcher)
                .OrderByDescending(c => c.Score)
                .Take(12)
                .Select((candidate, index) => new { candidate, index }))
            {
                AddCandidate(candidate.candidate, candidate.index < 5);
            }

            foreach (string position in AllStarDepthPositions())
            {
                int positionCount = selectedCandidates.Count(c => CandidateHasPosition(c, position));
                foreach (var candidate in sideCandidates
                    .Where(c => !selectedIds.Contains(c.PlayerId) && CandidateHasPosition(c, position))
                    .OrderByDescending(c => c.Score)
                    .ThenBy(c => c.PlayerName))
                {
                    AddCandidate(candidate, positionCount == 0 && position != "DH");
                    positionCount++;
                    if (positionCount >= 3)
                        break;
                }
            }

            int dhCount = 0;
            foreach (var candidate in sideCandidates
                .Where(c => !selectedIds.Contains(c.PlayerId) && CandidateHasPosition(c, "DH"))
                .OrderByDescending(c => c.Score)
                .ThenBy(c => c.PlayerName))
            {
                AddCandidate(candidate, starter: false);
                dhCount++;
                if (dhCount >= 3)
                    break;
            }

            return selected;
        }

        private static IEnumerable<string> AllStarDepthPositions()
        {
            yield return "C";
            yield return "1B";
            yield return "2B";
            yield return "3B";
            yield return "SS";
            yield return "LF";
            yield return "CF";
            yield return "RF";
        }

        private static SeasonAllStarSelection ToAllStarSelection(AllStarCandidate candidate, bool starter)
        {
            return new SeasonAllStarSelection
            {
                PlayerId = candidate.PlayerId,
                TeamId = candidate.TeamId,
                PlayerName = candidate.PlayerName,
                TeamName = candidate.TeamName,
                Role = candidate.Role,
                Positions = candidate.Positions,
                AllStarTeam = candidate.AllStarTeam,
                Starter = starter,
                SelectionScore = candidate.Score
            };
        }

        private void SimAllStarGame()
        {
            var season = SelectedSeason(_allStarSeasonCombo);
            if (!CanPlayAllStarGame(season)) return;
            Team blue = BuildSyntheticAllStarTeam(season, "Blue All-Stars", Color.RoyalBlue, Color.White);
            Team red = BuildSyntheticAllStarTeam(season, "Red All-Stars", Color.Firebrick, Color.White);
            var result = Simulator.SimGame(_league, blue, red, _rng);
            SaveAllStarGameResult(season, blue, red, result);
        }

        private void SaveAllStarGameResult(Season season, Team blue, Team red, GameResult result)
        {
            if (season == null || blue == null || red == null || result == null)
                return;

            season.AllStarGame = new SeasonAllStarGame
            {
                PlayedAt = result.PlayedAt,
                AwayName = blue.DisplayName,
                HomeName = red.DisplayName,
                AwayScore = result.AwayScore,
                HomeScore = result.HomeScore,
                AwayBaseLineup = blue.BaseLineup,
                HomeBaseLineup = red.BaseLineup,
                Lines = result.Lines?.ToList() ?? new List<PlayerGameLine>()
            };
            MarkDirty();
            RefreshAllStarViews();
            MessageBox.Show(this,
                season.AllStarGame.AwayName + " " + season.AllStarGame.AwayScore + ", " +
                season.AllStarGame.HomeName + " " + season.AllStarGame.HomeScore,
                "All-Star Game Final", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void WatchAllStarGame()
        {
            LaunchAllStarGame(GameMode.CpuVsCpuWatch);
        }

        private void PlayAllStarGameVsCpu()
        {
            LaunchAllStarGame(GameMode.UserVsCpu);
        }

        private void LaunchAllStarGame(GameMode mode)
        {
            var season = SelectedSeason(_allStarSeasonCombo);
            if (!CanPlayAllStarGame(season)) return;
            Team blue = BuildSyntheticAllStarTeam(season, "Blue All-Stars", Color.RoyalBlue, Color.White);
            Team red = BuildSyntheticAllStarTeam(season, "Red All-Stars", Color.Firebrick, Color.White);
            var rules = _league.Rules ?? new LeagueRules();
            var state = GameplayState.Create(blue, red, mode, rules.Innings, rules.ExtraInnings, rules.MercyRuleEnabled,
                rules.MercyRuleRuns, rules.MercyRuleMinimumInning, rules.ExtraInningRunnerOnSecond, rules.CourtesyRunnerForPitchersCatchers);
            state.FieldPresetId = SelectedFieldPreset().Id;
            TriggerCutscene(CutsceneTrigger.AllStarGameStart);
            using (var loading = new GameLoadingForm(blue, "All-Stars", null, red, "All-Stars", null, "All-Star Game", ModeLabelFor(mode), previewImagePath: FindAllStarGamePreviewImage()))
                loading.ShowDialog(this);
            using var game = new GameplayForm(blue, red);
            game.SetCutscenes(_league.Cutscenes, null, null);
            game.SetNationalAnthemCutsceneDefault(_league.NationalAnthemCutsceneDefault);
            game.SetFieldPreset(SelectedFieldPreset());
            game.ApplyGameplayState(state);
            game.SetModeLabel("All-Star Game - " + ModeLabelFor(mode));
            game.ShowDialog(this);
            if (game.FinalResult == null)
            {
                _status.Text = "All-Star Game exited before a final result; nothing was recorded.";
                return;
            }

            SaveAllStarGameResult(season, blue, red, game.FinalResult);
        }

        private static string FindAllStarGamePreviewImage()
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Assets", "Loading Screens", "all_star_game_preview.png");
            return File.Exists(path) ? path : null;
        }

        private bool CanPlayAllStarGame(Season season)
        {
            if (season == null)
            {
                MessageBox.Show(this, "Select a season first.");
                return false;
            }
            if (!season.ChampionTeamId.HasValue)
            {
                MessageBox.Show(this, "The All-Star Game is played after the championship. Finish the championship first.");
                return false;
            }
            if ((season.AllStarSelections?.Count ?? 0) < 2)
            {
                MessageBox.Show(this, "Finalize All-Star selections first.");
                return false;
            }
            return true;
        }

        private Team BuildSyntheticAllStarTeam(Season season, string side, Color primary, Color secondary)
        {
            var team = new Team
            {
                City = side.Replace(" All-Stars", ""),
                Nickname = "All-Stars",
                ScoreboardAbbreviation = side.StartsWith("Blue") ? "BLUE" : "RED",
                PrimaryArgb = primary.ToArgb(),
                SecondaryArgb = secondary.ToArgb()
            };
            foreach (var selection in season.AllStarSelections.Where(s => s.AllStarTeam == side).OrderByDescending(s => s.Starter).ThenByDescending(s => s.SelectionScore))
            {
                var source = PlayerById(selection.PlayerId);
                if (source == null || source.RedshirtActive) continue;
                team.Roster.Add(CloneAllStarPlayer(source));
            }
            EnsureAllStarFullDepth(season, team, side);
            EnsureAllStarRosterCanFieldLineup(season, team, side);
            team.BaseLineup = LineupEngine.CreateBaseLineup(team);
            team.PitchingPlan = BuildAllStarPitchingPlan(team);
            return team;
        }

        private void EnsureAllStarFullDepth(Season season, Team team, string side)
        {
            var candidates = BuildAllStarCandidates(season)
                .Where(c => c.AllStarTeam == side)
                .OrderByDescending(c => c.Score)
                .ThenBy(c => c.PlayerName)
                .ToList();

            foreach (string position in AllStarDepthPositions())
            {
                while (team.Roster.Count(p => p != null && PlayerHasAllStarPosition(p, position)) < 3)
                {
                    var candidate = candidates.FirstOrDefault(c => !team.Roster.Any(p => p.Id == c.PlayerId) && CandidateHasPosition(c, position));
                    if (candidate == null)
                        break;

                    AddAllStarCandidateToSyntheticRoster(team, candidates, candidate);
                }
            }

            foreach (var candidate in candidates
                .Where(c => !team.Roster.Any(p => p.Id == c.PlayerId) && CandidateHasPosition(c, "DH"))
                .OrderByDescending(c => c.Score)
                .ThenBy(c => c.PlayerName)
                .Take(3)
                .ToList())
            {
                AddAllStarCandidateToSyntheticRoster(team, candidates, candidate);
            }

            while (team.Roster.Count(p => p != null && (p.Role == PlayerRole.Pitcher || LineupEngine.HasPosition(p, "P"))) < 12)
            {
                var candidate = candidates.FirstOrDefault(c => !team.Roster.Any(p => p.Id == c.PlayerId) &&
                    (c.Role == PlayerRole.Pitcher || CandidateHasPosition(c, "P")));
                if (candidate == null)
                    return;

                AddAllStarCandidateToSyntheticRoster(team, candidates, candidate);
            }
        }

        private void AddAllStarCandidateToSyntheticRoster(Team team, List<AllStarCandidate> candidates, AllStarCandidate candidate)
        {
            var source = PlayerById(candidate.PlayerId);
            if (source == null || source.RedshirtActive)
            {
                candidates.Remove(candidate);
                return;
            }

            team.Roster.Add(CloneAllStarPlayer(source));
        }

        private static bool PlayerHasAllStarPosition(Player player, string position)
        {
            if (player == null)
                return false;
            if (position == "DH")
                return LineupEngine.HasPosition(player, "DH") || player.Role != PlayerRole.Pitcher;
            return LineupEngine.HasPosition(player, position);
        }

        private static TeamPitchingPlan BuildAllStarPitchingPlan(Team team)
        {
            var pitchers = (team?.Roster ?? new List<Player>())
                .Where(p => p != null && (p.Role == PlayerRole.Pitcher || LineupEngine.HasPosition(p, "P")))
                .ToList();

            var primaryPitchers = pitchers
                .OrderByDescending(PitchingRotationEngine.StarterScore)
                .Take(12)
                .ToList();
            var extraPitchers = pitchers
                .Where(p => primaryPitchers.All(primary => primary.Id != p.Id))
                .OrderByDescending(PitchingRotationEngine.StarterScore)
                .ToList();

            var starters = primaryPitchers
                .Take(5)
                .ToList();

            var remaining = primaryPitchers.Where(p => starters.All(s => s.Id != p.Id)).ToList();
            var longRelievers = remaining
                .OrderByDescending(p => p.Stamina * 1.2 + p.Pitching * 0.9 + p.Accuracy * 0.35)
                .Take(2)
                .ToList();

            remaining = remaining.Where(p => longRelievers.All(l => l.Id != p.Id)).ToList();
            var closer = remaining.OrderByDescending(PitchingRotationEngine.RelieverScore).FirstOrDefault();
            var setup = remaining
                .Where(p => closer == null || p.Id != closer.Id)
                .OrderByDescending(PitchingRotationEngine.RelieverScore)
                .FirstOrDefault();

            var schedule = new List<Player>();
            schedule.AddRange(starters);
            schedule.AddRange(longRelievers);
            if (setup != null) schedule.Add(setup);
            if (closer != null) schedule.Add(closer);
            schedule.AddRange(primaryPitchers
                .Where(p => schedule.All(s => s.Id != p.Id))
                .OrderByDescending(PitchingRotationEngine.RelieverScore));
            schedule.AddRange(extraPitchers
                .Where(p => schedule.All(s => s.Id != p.Id))
                .OrderByDescending(PitchingRotationEngine.RelieverScore));

            var plan = new TeamPitchingPlan
            {
                RotationSize = Math.Min(5, Math.Max(3, starters.Count)),
                LastCalculatedAt = DateTime.Now,
                UseAllStarPitchingRules = true,
                StarterRotationIds = starters.Select(p => p.Id).ToList(),
                AllStarPitchingScheduleIds = schedule.Select(p => p.Id).ToList(),
                Status = "All-Star pitching plan: 5 starters, 2 long relief, setup, closer, remaining primary pitchers, then pitcher-position All-Stars for extra innings."
            };

            foreach (var player in longRelievers)
                plan.BullpenRoles.Add(new BullpenRoleAssignment { PlayerId = player.Id, PlayerName = player.Name, Role = BullpenRole.LongRelief });
            if (setup != null)
                plan.BullpenRoles.Add(new BullpenRoleAssignment { PlayerId = setup.Id, PlayerName = setup.Name, Role = BullpenRole.Setup });
            if (closer != null)
                plan.BullpenRoles.Add(new BullpenRoleAssignment { PlayerId = closer.Id, PlayerName = closer.Name, Role = BullpenRole.Closer });

            return plan;
        }

        private void EnsureAllStarRosterCanFieldLineup(Season season, Team team, string side)
        {
            var candidates = BuildAllStarCandidates(season)
                .Where(c => c.AllStarTeam == side)
                .OrderByDescending(c => c.Score)
                .ThenBy(c => c.PlayerName)
                .ToList();

            for (int attempt = 0; attempt < 12; attempt++)
            {
                var card = LineupEngine.CalculateLineupCard(team);
                bool hasNine = card.BattingOrder.Count >= 9;
                if (card.IsValid && hasNine)
                    return;

                bool added = false;
                foreach (string missing in card.MissingPositions.Where(p => p != "BAT").Distinct().ToList())
                {
                    var candidate = candidates.FirstOrDefault(c => !team.Roster.Any(p => p.Id == c.PlayerId) && CandidateHasPosition(c, missing));
                    if (candidate == null)
                        continue;
                    var source = PlayerById(candidate.PlayerId);
                    if (source == null || source.RedshirtActive)
                        continue;
                    team.Roster.Add(CloneAllStarPlayer(source));
                    added = true;
                }

                while (team.Roster.Count(p => p != null && !p.RedshirtActive) < 9)
                {
                    var candidate = candidates.FirstOrDefault(c => !team.Roster.Any(p => p.Id == c.PlayerId));
                    if (candidate == null)
                        break;
                    var source = PlayerById(candidate.PlayerId);
                    if (source == null || source.RedshirtActive)
                    {
                        candidates.Remove(candidate);
                        continue;
                    }
                    team.Roster.Add(CloneAllStarPlayer(source));
                    added = true;
                }

                if (!added)
                    return;
            }
        }

        private static bool CandidateHasPosition(AllStarCandidate candidate, string position)
        {
            if (candidate == null)
                return false;
            if (position == "P")
                return candidate.Role == PlayerRole.Pitcher || LineupEngine.HasPosition(new Player { Role = candidate.Role, Positions = candidate.Positions }, "P");
            if (position == "DH")
                return candidate.Role != PlayerRole.Pitcher ||
                    LineupEngine.HasPosition(new Player { Role = candidate.Role, Positions = candidate.Positions }, "DH");
            return LineupEngine.HasPosition(new Player { Role = candidate.Role, Positions = candidate.Positions }, position);
        }

        private static Player CloneAllStarPlayer(Player source)
        {
            return new Player
            {
                Id = source.Id,
                Name = source.Name,
                Role = source.Role,
                Classification = source.Classification,
                InitialClassification = source.InitialClassification,
                Positions = source.Positions,
                Bats = source.Bats,
                Throws = source.Throws,
                Potential = source.Potential,
                WorkEthic = source.WorkEthic,
                Durability = source.Durability,
                RegressionRisk = source.RegressionRisk,
                InjuryStatus = PlayerInjuryStatus.Healthy,
                InjuryName = "",
                InjuryGamesRemaining = 0,
                InjurySeverity = 0,
                InjuryMissedGamesThisSeason = 0,
                MedicalTag = false,
                RedshirtActive = source.RedshirtActive,
                RedshirtUsed = source.RedshirtUsed,
                Contact = source.Contact,
                Power = source.Power,
                Speed = source.Speed,
                StealAggression = source.StealAggression,
                BaseRunning = source.BaseRunning,
                Fielding = source.Fielding,
                HoldRunner = source.HoldRunner,
                Pickoff = source.Pickoff,
                DeliveryTime = source.DeliveryTime,
                ArmStrength = source.ArmStrength,
                PopTime = source.PopTime,
                Accuracy = source.Accuracy,
                TagRating = source.TagRating,
                FieldingErrorPenaltyDebt = source.FieldingErrorPenaltyDebt,
                ErrorFreeFieldingChanceStreak = source.ErrorFreeFieldingChanceStreak,
                Pitching = source.Pitching,
                Stamina = source.Stamina,
                CareerPitchCount = source.CareerPitchCount,
                JerseyArgbOverride = source.JerseyArgbOverride,
                PantsArgbOverride = source.PantsArgbOverride,
                CapHelmetArgbOverride = source.CapHelmetArgbOverride,
                AvatarPath = source.AvatarPath,
                SpriteSheetPath = source.SpriteSheetPath
            };
        }

        private void ApplyAllStarTag(Season season, Guid playerId)
        {
            var player = PlayerById(playerId);
            if (player == null) return;
            player.AllStarSeasonIds ??= new List<Guid>();
            if (!player.AllStarSeasonIds.Contains(season.Id))
                player.AllStarSeasonIds.Add(season.Id);
        }

        private void RemoveAllStarTag(Season season, Guid playerId)
        {
            var player = PlayerById(playerId);
            player?.AllStarSeasonIds?.Remove(season.Id);
        }

        private void ClearAllStarTagsForSeason(Season season)
        {
            if (season == null) return;
            foreach (var player in _league?.Teams?.SelectMany(t => t.Roster ?? Enumerable.Empty<Player>()) ?? Enumerable.Empty<Player>())
                player.AllStarSeasonIds?.Remove(season.Id);
        }

        private static string AllStarTagText(Player player)
        {
            int count = player?.AllStarSeasonIds?.Count ?? 0;
            return count <= 0 ? "" : (count == 1 ? "All-Star" : count + "x All-Star");
        }

        private string AwardTagText(Player player)
        {
            if (player == null || _league?.Seasons == null)
                return "";

            var awards = _league.Seasons
                .SelectMany(s => s.Awards ?? Enumerable.Empty<SeasonAwardSelection>())
                .Where(a => a.PlayerId == player.Id)
                .ToList();
            int wins = awards.Count(a => a.Winner);
            int finalists = awards.Count(a => !a.Winner);
            if (wins == 0 && finalists == 0)
                return "";
            if (wins > 0 && finalists > 0)
                return wins + "x Winner, " + finalists + "x Finalist";
            return wins > 0 ? wins + "x Award Winner" : finalists + "x Award Finalist";
        }

        private static string BuildTeamLeadersText(List<TeamSeasonStatLine> stats)
        {
            if (stats == null || stats.Count == 0)
                return "No team stats recorded for this selection.";

            return "Team Leaders    " +
                Leader(stats, "Wins", s => s.Wins.ToString(), s => s.Wins) + "    |    " +
                Leader(stats, "Runs", s => s.RunsFor.ToString(), s => s.RunsFor) + "    |    " +
                Leader(stats, "HR", s => s.HR.ToString(), s => s.HR) + "    |    " +
                Leader(stats, "OPS", s => (ObpValue(s.H, s.BB, s.HBP, s.AB, s.SF) + SlgValue(s.TotalBases, s.AB)).ToString("0.000"), s => ObpValue(s.H, s.BB, s.HBP, s.AB, s.SF) + SlgValue(s.TotalBases, s.AB)) + "    |    " +
                Leader(stats.Where(s => s.IPOuts > 0), "ERA", s => EraValue(s.ER, s.IPOuts).ToString("0.00"), s => -EraValue(s.ER, s.IPOuts));
        }

        private static string BuildPlayerLeadersText(List<PlayerSeasonStatLine> stats)
        {
            if (stats == null || stats.Count == 0)
                return "No player stats recorded for this team and season.";

            return "Team Leaders    " +
                Leader(stats, "AVG", s => AverageValue(s.H, s.AB).ToString("0.000"), s => s.AB <= 0 ? -1 : AverageValue(s.H, s.AB)) + "    |    " +
                Leader(stats, "HR", s => s.HR.ToString(), s => s.HR) + "    |    " +
                Leader(stats, "RBI", s => s.RBI.ToString(), s => s.RBI) + "    |    " +
                Leader(stats, "SB", s => s.SB.ToString(), s => s.SB) + "    |    " +
                Leader(stats.Where(s => s.IPOuts > 0), "ERA", s => EraValue(s.ER, s.IPOuts).ToString("0.00"), s => -EraValue(s.ER, s.IPOuts)) + "    |    " +
                Leader(stats, "K", s => s.K.ToString(), s => s.K);
        }

        private static string Leader<T>(IEnumerable<T> source, string label, Func<T, string> value, Func<T, double> rank) where T : class
        {
            var leader = source?.OrderByDescending(rank).FirstOrDefault();
            if (leader == null)
                return label + ": -";

            string name = leader is TeamSeasonStatLine team ? team.TeamName : ((PlayerSeasonStatLine)(object)leader).PlayerName;
            return label + ": " + name + " (" + value(leader) + ")";
        }

        private void RefreshHallOfFameViews()
        {
            if (_hofDynastyGrid == null) return;

            _league.HallOfFameEntries ??= new List<HallOfFameEntry>();
            var selectedTeam = SelectedTeam(_hofTeamCombo);
            var candidates = BuildHallOfFameCandidates();
            var coachCandidates = BuildCoachHallCandidates();

            FillHallEntriesGrid(_hofDynastyGrid, _league.HallOfFameEntries
                .OrderBy(e => e.InductedSeasonNumber)
                .ThenBy(HallEntryName));

            FillHallEntriesGrid(_hofTeamGrid, _league.HallOfFameEntries
                .Where(e => selectedTeam == null || e.TeamId == selectedTeam.Id)
                .OrderBy(e => e.TeamName)
                .ThenBy(HallEntryName));

            FillHallCandidatesGrid(candidates);
            FillCoachHallCandidatesGrid(coachCandidates);
            FillHallRecordsGrid(candidates);
            RefreshTeamHallOfFamePage(selectedTeam);

            if (_hofSummaryLabel != null)
            {
                int teamCount = selectedTeam == null
                    ? _league.HallOfFameEntries.Select(e => e.TeamId).Distinct().Count()
                    : _league.HallOfFameEntries.Count(e => e.TeamId == selectedTeam.Id);
                _hofSummaryLabel.Text =
                    "Dynasty Hall: " + _league.HallOfFameEntries.Count + " inductee(s)    |    " +
                    "Team Hall selection: " + (selectedTeam?.DisplayName ?? "All Teams") + " (" + teamCount + ")    |    " +
                    "Player candidates: " + candidates.Count + "    |    " +
                    "Coach candidates: " + coachCandidates.Count;
            }
        }

        private void FillHallEntriesGrid(DataGridView grid, IEnumerable<HallOfFameEntry> entries)
        {
            grid.Rows.Clear();
            foreach (var entry in entries)
            {
                int rowIndex = grid.Rows.Add(
                    entry.InductedSeasonNumber <= 0 ? "" : "Season " + entry.InductedSeasonNumber,
                    HallEntryName(entry),
                    entry.TeamName,
                    HallEntryRoleText(entry),
                    entry.HallScore,
                    entry.Games,
                    entry.PlateAppearances,
                    entry.Hits,
                    entry.ExtraBaseHits,
                    entry.HomeRuns,
                    entry.RBI,
                    entry.StolenBases,
                    entry.ReachedOnError,
                    entry.PitchingWins,
                    entry.Saves,
                    entry.Holds,
                    entry.BlownSaves,
                    entry.CompleteGames,
                    entry.Shutouts,
                    entry.Strikeouts,
                    entry.RunsAllowed,
                    entry.DoublesAllowed,
                    entry.TriplesAllowed,
                    FormatInnings(entry.DefensiveOuts),
                    entry.TotalChances,
                    entry.CatcherStealAttempts <= 0 ? "" : entry.CatcherCaughtStealingPercentage.ToString("0.0%"),
                    entry.Average <= 0 ? "" : entry.Average.ToString("0.000"),
                    entry.OPS <= 0 ? "" : entry.OPS.ToString("0.000"),
                    entry.ERA <= 0 ? "" : entry.ERA.ToString("0.00"),
                    entry.Championships,
                    entry.Reason);
                grid.Rows[rowIndex].Tag = entry;
            }
        }

        private static bool IsCoachHallEntry(HallOfFameEntry entry)
            => string.Equals(entry?.EntryType, "Coach", StringComparison.OrdinalIgnoreCase);

        private static string HallEntryName(HallOfFameEntry entry)
        {
            if (entry == null)
                return "";
            return IsCoachHallEntry(entry)
                ? (string.IsNullOrWhiteSpace(entry.CoachName) ? entry.PlayerName : entry.CoachName)
                : entry.PlayerName;
        }

        private static string HallEntryRoleText(HallOfFameEntry entry)
            => IsCoachHallEntry(entry) ? "Coach" : entry.Role.ToString();

        private void FillHallCandidatesGrid(List<HallOfFameCandidate> candidates)
        {
            _hofCandidatesGrid.Rows.Clear();
            foreach (var candidate in candidates
                .Where(c => !IsHallOfFamer(c.PlayerId))
                .OrderByDescending(c => c.HallScore)
                .ThenBy(c => c.PlayerName))
            {
                var s = candidate.Stats;
                int rowIndex = _hofCandidatesGrid.Rows.Add(
                    candidate.Recommendation,
                    candidate.HallScore,
                    candidate.PlayerName,
                    candidate.TeamName,
                    candidate.Role,
                    s.Games,
                    s.PlateAppearances,
                    s.H,
                    s.ExtraBaseHits,
                    s.HR,
                    s.RBI,
                    s.SB,
                    s.ReachedOnError,
                    s.PitchingWins,
                    s.Saves,
                    s.Holds,
                    s.BlownSaves,
                    s.CompleteGames,
                    s.Shutouts,
                    s.K,
                    s.RunsAllowed,
                    s.DoublesAllowed,
                    s.TriplesAllowed,
                    FormatInnings(s.DefensiveOuts),
                    s.TotalChances,
                    s.CatcherStealAttempts <= 0 ? "" : s.CatcherCaughtStealingPercentage.ToString("0.0%"),
                    s.AB <= 0 ? "" : AverageValue(s.H, s.AB).ToString("0.000"),
                    s.AB <= 0 ? "" : (ObpValue(s.H, s.BB, s.HBP, s.AB, s.SF) + SlgValue(s.TotalBases, s.AB)).ToString("0.000"),
                    s.IPOuts <= 0 ? "" : EraValue(s.ER, s.IPOuts).ToString("0.00"),
                    candidate.Championships,
                    candidate.Reason);
                _hofCandidatesGrid.Rows[rowIndex].Tag = candidate;
            }
        }

        private void FillCoachHallCandidatesGrid(List<CoachRecord> candidates)
        {
            if (_hofCoachCandidatesGrid == null)
                return;

            _hofCoachCandidatesGrid.Rows.Clear();
            foreach (var candidate in candidates
                .Where(c => !IsCoachHallOfFamer(c.CoachId))
                .OrderByDescending(c => c.HallScore)
                .ThenBy(c => c.CoachName))
            {
                int rowIndex = _hofCoachCandidatesGrid.Rows.Add(
                    candidate.Recommendation,
                    candidate.HallScore,
                    candidate.CoachName,
                    candidate.TeamName,
                    candidate.Wins,
                    candidate.Losses,
                    candidate.Ties,
                    candidate.ChampionshipWins,
                    candidate.PlayoffWins,
                    candidate.DistrictWins,
                    candidate.RegionWins,
                    candidate.ConferenceWins,
                    candidate.Reason);
                _hofCoachCandidatesGrid.Rows[rowIndex].Tag = candidate;
            }
        }

        private void FillHallRecordsGrid(List<HallOfFameCandidate> candidates)
        {
            _hofRecordsGrid.Rows.Clear();
            AddHallRecord(candidates, "Hits", c => c.Stats.H, c => c.Stats.H.ToString(), "Career batting hits");
            AddHallRecord(candidates, "Plate Appearances", c => c.Stats.PlateAppearances, c => c.Stats.PlateAppearances.ToString(), "Career plate appearances");
            AddHallRecord(candidates, "Extra-Base Hits", c => c.Stats.ExtraBaseHits, c => c.Stats.ExtraBaseHits.ToString(), "Career doubles, triples, and home runs");
            AddHallRecord(candidates, "Reached on Error", c => c.Stats.ReachedOnError, c => c.Stats.ReachedOnError.ToString(), "Career reached-on-error total");
            AddHallRecord(candidates, "Home Runs", c => c.Stats.HR, c => c.Stats.HR.ToString(), "Career home runs");
            AddHallRecord(candidates, "RBI", c => c.Stats.RBI, c => c.Stats.RBI.ToString(), "Career runs batted in");
            AddHallRecord(candidates, "Stolen Bases", c => c.Stats.SB, c => c.Stats.SB.ToString(), "Career stolen bases");
            AddHallRecord(candidates.Where(c => c.Stats.AB >= 20), "AVG", c => AverageValue(c.Stats.H, c.Stats.AB), c => AverageValue(c.Stats.H, c.Stats.AB).ToString("0.000"), "Minimum 20 at-bats");
            AddHallRecord(candidates.Where(c => c.Stats.AB >= 20), "OPS", c => ObpValue(c.Stats.H, c.Stats.BB, c.Stats.HBP, c.Stats.AB, c.Stats.SF) + SlgValue(c.Stats.TotalBases, c.Stats.AB), c => (ObpValue(c.Stats.H, c.Stats.BB, c.Stats.HBP, c.Stats.AB, c.Stats.SF) + SlgValue(c.Stats.TotalBases, c.Stats.AB)).ToString("0.000"), "Minimum 20 at-bats");
            AddHallRecord(candidates, "Pitching Wins", c => c.Stats.PitchingWins, c => c.Stats.PitchingWins.ToString(), "Career pitching wins");
            AddHallRecord(candidates, "Saves", c => c.Stats.Saves, c => c.Stats.Saves.ToString(), "Career saves");
            AddHallRecord(candidates, "Holds", c => c.Stats.Holds, c => c.Stats.Holds.ToString(), "Career holds");
            AddHallRecord(candidates, "Complete Games", c => c.Stats.CompleteGames, c => c.Stats.CompleteGames.ToString(), "Career complete games");
            AddHallRecord(candidates, "Shutouts", c => c.Stats.Shutouts, c => c.Stats.Shutouts.ToString(), "Career shutouts");
            AddHallRecord(candidates, "Strikeouts", c => c.Stats.K, c => c.Stats.K.ToString(), "Career pitching strikeouts");
            AddHallRecord(candidates, "Fewest Runs Allowed", c => -c.Stats.RunsAllowed, c => c.Stats.RunsAllowed.ToString(), "Career runs allowed; displayed for complete statistical record");
            AddHallRecord(candidates, "Doubles Allowed", c => c.Stats.DoublesAllowed, c => c.Stats.DoublesAllowed.ToString(), "Career doubles allowed");
            AddHallRecord(candidates, "Triples Allowed", c => c.Stats.TriplesAllowed, c => c.Stats.TriplesAllowed.ToString(), "Career triples allowed");
            AddHallRecord(candidates, "Total Chances", c => c.Stats.TotalChances, c => c.Stats.TotalChances.ToString(), "Career defensive total chances");
            AddHallRecord(candidates.Where(c => c.Stats.CatcherStealAttempts >= 20), "Catcher CS%", c => c.Stats.CatcherCaughtStealingPercentage, c => c.Stats.CatcherCaughtStealingPercentage.ToString("0.0%"), "Minimum 20 stolen-base attempts");
            AddHallRecord(candidates.Where(c => c.Stats.IPOuts >= 15), "ERA", c => -EraValue(c.Stats.ER, c.Stats.IPOuts), c => EraValue(c.Stats.ER, c.Stats.IPOuts).ToString("0.00"), "Minimum 5 innings pitched");
            AddHallRecord(candidates.Where(c => c.Stats.IPOuts >= 15), "WHIP", c => -WhipValue(c.Stats.WalksAllowed, c.Stats.HitsAllowed, c.Stats.IPOuts), c => WhipValue(c.Stats.WalksAllowed, c.Stats.HitsAllowed, c.Stats.IPOuts).ToString("0.00"), "Minimum 5 innings pitched");
        }

        private void AddHallRecord(IEnumerable<HallOfFameCandidate> candidates, string record, Func<HallOfFameCandidate, double> rank, Func<HallOfFameCandidate, string> value, string detail)
        {
            var leader = candidates?.OrderByDescending(rank).FirstOrDefault();
            if (leader == null)
                return;

            _hofRecordsGrid.Rows.Add(record, leader.PlayerName, leader.TeamName, value(leader), detail);
        }

        private void InductSelectedHallOfFameCandidate()
        {
            var currentGrid = CurrentHallOfFameGrid();
            if (currentGrid == _hofCoachCandidatesGrid && _hofCoachCandidatesGrid?.CurrentRow?.Tag is CoachRecord coachCandidate)
            {
                InductSelectedCoachHallOfFameCandidate(coachCandidate);
                return;
            }

            if (currentGrid != _hofCandidatesGrid || _hofCandidatesGrid?.CurrentRow?.Tag is not HallOfFameCandidate candidate)
            {
                MessageBox.Show(this, "Select a Hall of Fame candidate first.");
                return;
            }

            if (IsHallOfFamer(candidate.PlayerId))
            {
                MessageBox.Show(this, candidate.PlayerName + " is already in the Hall of Fame.");
                return;
            }

            var confirm = MessageBox.Show(this,
                "Induct " + candidate.PlayerName + " into the Hall of Fame?\n\n" + candidate.Reason,
                "Induct Hall of Famer", MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
            if (confirm != DialogResult.OK)
                return;

            _league.HallOfFameEntries ??= new List<HallOfFameEntry>();
            _league.HallOfFameEntries.Add(CreateHallOfFameEntry(candidate));
            MarkDirty();
            RefreshHallOfFameViews();
        }

        private void InductSelectedCoachHallOfFameCandidate(CoachRecord candidate)
        {
            if (candidate == null)
                return;

            if (IsCoachHallOfFamer(candidate.CoachId))
            {
                MessageBox.Show(this, candidate.CoachName + " is already in the Hall of Fame.");
                return;
            }

            var confirm = MessageBox.Show(this,
                "Induct " + candidate.CoachName + " into the Hall of Fame?\n\n" +
                "Score: " + candidate.HallScore + "\n" + candidate.Reason,
                "Induct Coach Hall of Famer", MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
            if (confirm != DialogResult.OK)
                return;

            _league.HallOfFameEntries ??= new List<HallOfFameEntry>();
            _league.HallOfFameEntries.Add(CreateCoachHallOfFameEntry(candidate));
            MarkDirty();
            RefreshHallOfFameViews();
        }

        private void RemoveSelectedHallOfFameEntry()
        {
            HallOfFameEntry entry = _hofDynastyGrid?.CurrentRow?.Tag as HallOfFameEntry
                ?? _hofTeamGrid?.CurrentRow?.Tag as HallOfFameEntry;
            if (entry == null)
            {
                MessageBox.Show(this, "Select an inducted Hall of Famer from Dynasty Hall or Team Hall first.");
                return;
            }

            var confirm = MessageBox.Show(this,
                "Remove " + HallEntryName(entry) + " from the Hall of Fame?",
                "Remove Hall of Famer", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);
            if (confirm != DialogResult.OK)
                return;

            _league.HallOfFameEntries.Remove(entry);
            MarkDirty();
            RefreshHallOfFameViews();
        }

        private HallOfFameEntry CreateHallOfFameEntry(HallOfFameCandidate candidate)
        {
            var s = candidate.Stats;
            return new HallOfFameEntry
            {
                EntryType = "Player",
                PlayerId = candidate.PlayerId,
                TeamId = candidate.TeamId,
                PlayerName = candidate.PlayerName,
                TeamName = candidate.TeamName,
                Role = candidate.Role,
                Classification = candidate.Classification,
                InductedSeasonNumber = Math.Max(1, _league?.Seasons?.Count ?? 1),
                Reason = candidate.Reason,
                HallScore = candidate.HallScore,
                Games = s.Games,
                PlateAppearances = s.PlateAppearances,
                Hits = s.H,
                ExtraBaseHits = s.ExtraBaseHits,
                HomeRuns = s.HR,
                RBI = s.RBI,
                StolenBases = s.SB,
                PitchingWins = s.PitchingWins,
                Saves = s.Saves,
                Holds = s.Holds,
                BlownSaves = s.BlownSaves,
                CompleteGames = s.CompleteGames,
                Shutouts = s.Shutouts,
                RunsAllowed = s.RunsAllowed,
                DoublesAllowed = s.DoublesAllowed,
                TriplesAllowed = s.TriplesAllowed,
                ReachedOnError = s.ReachedOnError,
                DefensiveOuts = s.DefensiveOuts,
                TotalChances = s.TotalChances,
                CatcherCaughtStealing = s.CatcherCaughtStealing,
                CatcherStealAttempts = s.CatcherStealAttempts,
                CatcherCaughtStealingPercentage = s.CatcherCaughtStealingPercentage,
                Strikeouts = s.K,
                Average = AverageValue(s.H, s.AB),
                OPS = ObpValue(s.H, s.BB, s.HBP, s.AB, s.SF) + SlgValue(s.TotalBases, s.AB),
                ERA = s.IPOuts <= 0 ? 0 : EraValue(s.ER, s.IPOuts),
                WHIP = s.IPOuts <= 0 ? 0 : WhipValue(s.WalksAllowed, s.HitsAllowed, s.IPOuts),
                Championships = candidate.Championships
            };
        }

        private HallOfFameEntry CreateCoachHallOfFameEntry(CoachRecord candidate)
        {
            return new HallOfFameEntry
            {
                EntryType = "Coach",
                CoachId = candidate.CoachId,
                TeamId = candidate.TeamId,
                PlayerName = candidate.CoachName,
                CoachName = candidate.CoachName,
                TeamName = candidate.TeamName,
                InductedSeasonNumber = Math.Max(1, _league?.Seasons?.Count ?? 1),
                Reason = candidate.Reason,
                HallScore = candidate.HallScore,
                Games = candidate.Wins + candidate.Losses + candidate.Ties,
                PitchingWins = candidate.Wins,
                Saves = candidate.PlayoffWins,
                Strikeouts = candidate.DistrictWins,
                Championships = candidate.ChampionshipWins
            };
        }

        private void EnsureJohnnyOatesHallOfFameEntry(Season season)
        {
            if (season == null || _league == null)
                return;

            var winner = (season.Awards ?? new List<SeasonAwardSelection>())
                .FirstOrDefault(a => a.Winner && string.Equals(a.AwardName, "Johnny Oates Award", StringComparison.OrdinalIgnoreCase));
            if (winner == null)
                return;

            var team = TeamById(winner.TeamId);
            if (team == null)
                return;

            team.NormalizeText();
            Guid coachId = winner.PlayerId == Guid.Empty ? team.CoachId : winner.PlayerId;
            var coach = CoachById(team, coachId);
            string coachName = CoachDisplayName(team, coach);
            int seasonNumber = CurrentSeasonNumber(season);
            var coachRecord = BuildCoachRecord(team, coach);
            int titles = Math.Max(CountJohnnyOatesAwards(coachId), coachRecord.ChampionshipWins);
            string reason = "Johnny Oates Award winner; " + coachRecord.Reason;

            _league.HallOfFameEntries ??= new List<HallOfFameEntry>();
            var existing = _league.HallOfFameEntries.FirstOrDefault(e => IsCoachHallEntry(e) && e.CoachId == coachId);
            if (existing == null)
            {
                _league.HallOfFameEntries.Add(new HallOfFameEntry
                {
                    EntryType = "Coach",
                    CoachId = coachId,
                    TeamId = team.Id,
                    PlayerName = coachName,
                    CoachName = coachName,
                    TeamName = team.DisplayName,
                    InductedSeasonNumber = seasonNumber,
                    Reason = reason,
                    HallScore = coachRecord.HallScore,
                    Games = coachRecord.Wins + coachRecord.Losses + coachRecord.Ties,
                    PitchingWins = coachRecord.Wins,
                    Saves = coachRecord.PlayoffWins,
                    Strikeouts = coachRecord.DistrictWins,
                    Championships = titles
                });
                return;
            }

            existing.PlayerName = coachName;
            existing.CoachName = coachName;
            existing.TeamId = team.Id;
            existing.TeamName = team.DisplayName;
            existing.Reason = reason;
            existing.HallScore = Math.Max(existing.HallScore, coachRecord.HallScore);
            existing.Games = Math.Max(existing.Games, coachRecord.Wins + coachRecord.Losses + coachRecord.Ties);
            existing.PitchingWins = Math.Max(existing.PitchingWins, coachRecord.Wins);
            existing.Saves = Math.Max(existing.Saves, coachRecord.PlayoffWins);
            existing.Strikeouts = Math.Max(existing.Strikeouts, coachRecord.DistrictWins);
            existing.Championships = Math.Max(existing.Championships, titles);
        }

        private int CountJohnnyOatesAwards(Guid coachId)
        {
            if (coachId == Guid.Empty || _league?.Seasons == null)
                return 0;

            return _league.Seasons.Sum(s => (s.Awards ?? new List<SeasonAwardSelection>())
                .Count(a => a.Winner &&
                    a.PlayerId == coachId &&
                    string.Equals(a.AwardName, "Johnny Oates Award", StringComparison.OrdinalIgnoreCase)));
        }

        private static string CoachDisplayName(Team? team, Coach? coach = null)
            => !string.IsNullOrWhiteSpace(coach?.Name)
                ? coach.Name.Trim()
                : string.IsNullOrWhiteSpace(team?.CoachName) ? "Head Coach" : team.CoachName.Trim();

        private bool IsHallOfFamer(Guid playerId)
            => _league?.HallOfFameEntries?.Any(e => !IsCoachHallEntry(e) && e.PlayerId == playerId) == true;

        private bool IsCoachHallOfFamer(Guid coachId)
            => coachId != Guid.Empty && _league?.HallOfFameEntries?.Any(e => IsCoachHallEntry(e) && e.CoachId == coachId) == true;

        private List<CoachRecord> BuildCoachHallCandidates()
        {
            var records = new List<CoachRecord>();
            if (_league?.Teams == null)
                return records;

            foreach (var team in _league.Teams)
            {
                EnsureTeamCoaches(team);
                foreach (var coach in team.Coaches ?? Enumerable.Empty<Coach>())
                {
                    var record = BuildCoachRecord(team, coach);
                    if (record.HallScore > 0 || record.Wins > 0 || record.PlayoffWins > 0 || record.ChampionshipWins > 0)
                        records.Add(record);
                }
            }

            return records
                .GroupBy(r => r.CoachId)
                .Select(g => MergeCoachRecords(g.ToList()))
                .ToList();
        }

        private static CoachRecord MergeCoachRecords(List<CoachRecord> records)
        {
            var first = records.FirstOrDefault() ?? new CoachRecord();
            var merged = new CoachRecord
            {
                CoachId = first.CoachId,
                TeamId = first.TeamId,
                CoachName = first.CoachName,
                TeamName = string.Join(", ", records.Select(r => r.TeamName).Where(t => !string.IsNullOrWhiteSpace(t)).Distinct()),
                Wins = records.Sum(r => r.Wins),
                Losses = records.Sum(r => r.Losses),
                Ties = records.Sum(r => r.Ties),
                PlayoffWins = records.Sum(r => r.PlayoffWins),
                PlayoffLosses = records.Sum(r => r.PlayoffLosses),
                ChampionshipWins = records.Sum(r => r.ChampionshipWins),
                DistrictWins = records.Sum(r => r.DistrictWins),
                RegionWins = records.Sum(r => r.RegionWins),
                ConferenceWins = records.Sum(r => r.ConferenceWins)
            };
            merged.HallScore = CalculateCoachHallScore(merged);
            merged.Recommendation = CoachHallRecommendation(merged.HallScore);
            merged.Reason = BuildCoachHallReason(merged);
            return merged;
        }

        private List<HallOfFameCandidate> BuildHallOfFameCandidates()
        {
            var map = new Dictionary<Guid, HallOfFameCandidate>();
            if (_league?.Teams == null)
                return new List<HallOfFameCandidate>();

            foreach (var team in _league.Teams)
            {
                foreach (var player in (team.Roster ?? Enumerable.Empty<Player>())
                    .Concat(team.InjuredReserve ?? Enumerable.Empty<Player>())
                    .Concat((team.JvPool ?? new List<Player>()).Where(player => player.VarsitySeasonsPlayed > 0)))
                {
                    map[player.Id] = new HallOfFameCandidate
                    {
                        PlayerId = player.Id,
                        TeamId = team.Id,
                        PlayerName = player.Name,
                        TeamName = team.DisplayName,
                        Role = player.Role,
                        Classification = player.Classification,
                        InitialClassification = player.InitialClassification == PlayerClassification.Unassigned ? player.Classification : player.InitialClassification,
                        Stats = new PlayerSeasonStatLine
                        {
                            PlayerId = player.Id,
                            PlayerName = player.Name,
                            Pitcher = player.Role == PlayerRole.Pitcher,
                            Classification = player.Classification,
                            Positions = player.Positions
                        },
                        PlayoffStats = new PlayerSeasonStatLine
                        {
                            PlayerId = player.Id,
                            PlayerName = player.Name,
                            Pitcher = player.Role == PlayerRole.Pitcher,
                            Classification = player.Classification,
                            Positions = player.Positions
                        }
                    };
                }
            }

            foreach (var season in _league.Seasons ?? Enumerable.Empty<Season>())
            {
                foreach (var game in season.Games ?? Enumerable.Empty<GameResult>())
                {
                    var appearedThisGame = new HashSet<Guid>();
                    foreach (var line in game.Lines ?? Enumerable.Empty<PlayerGameLine>())
                    {
                        if (!map.TryGetValue(line.PlayerId, out var candidate))
                        {
                            var team = TeamById(line.TeamId);
                            candidate = new HallOfFameCandidate
                            {
                                PlayerId = line.PlayerId,
                                TeamId = line.TeamId,
                                PlayerName = line.PlayerName,
                                TeamName = team?.DisplayName ?? "",
                                Role = line.Pitcher ? PlayerRole.Pitcher : PlayerRole.Batter,
                                Classification = line.Classification,
                                InitialClassification = line.InitialClassification == PlayerClassification.Unassigned ? line.Classification : line.InitialClassification,
                                Stats = new PlayerSeasonStatLine
                                {
                                    PlayerId = line.PlayerId,
                                    PlayerName = line.PlayerName,
                                    Pitcher = line.Pitcher,
                                    Classification = line.Classification
                                },
                                PlayoffStats = new PlayerSeasonStatLine
                                {
                                    PlayerId = line.PlayerId,
                                    PlayerName = line.PlayerName,
                                    Pitcher = line.Pitcher,
                                    Classification = line.Classification
                                }
                            };
                            map[line.PlayerId] = candidate;
                        }

                        bool countGame = HasPlayerAppearance(line) && appearedThisGame.Add(line.PlayerId);
                        if (HasPlayerAppearance(line))
                            candidate.StatSeasonIds.Add(season.Id);
                        if (candidate.Classification == PlayerClassification.Unassigned && line.Classification != PlayerClassification.Unassigned)
                            candidate.Classification = line.Classification;
                        if (candidate.InitialClassification == PlayerClassification.Unassigned)
                            candidate.InitialClassification = line.InitialClassification == PlayerClassification.Unassigned ? line.Classification : line.InitialClassification;
                        AccumulatePlayerLine(candidate.Stats, line, countGame);
                        if (game.IsPlayoff)
                            AccumulatePlayerLine(candidate.PlayoffStats, line, countGame);
                        if (line.Pitcher)
                            candidate.Role = PlayerRole.Pitcher;
                    }
                }
            }

            var allCandidates = map.Values.ToList();
            foreach (var candidate in allCandidates)
                ApplyHallCareerExtrapolation(candidate);

            foreach (var candidate in allCandidates)
            {
                candidate.Championships = CountTeamChampionships(candidate.TeamId);
                candidate.LeaderBonus = CalculateHallLeaderBonus(candidate, allCandidates, out string leaderReason);
                candidate.LeaderBonusReason = leaderReason;
                candidate.PlayoffBonus = CalculateHallPlayoffBonus(candidate, out string playoffReason);
                candidate.PlayoffBonusReason = playoffReason;
                candidate.HallScore = CalculateHallScore(candidate);
                candidate.Recommendation = candidate.HallScore >= 100 ? "Must Induct"
                    : candidate.HallScore >= 75 ? "Recommended"
                    : candidate.HallScore >= 55 ? "Watch List"
                    : "Developing";
                candidate.Reason = BuildHallReason(candidate);
            }

            return allCandidates
                .Where(c => c.HallScore >= 35 || c.Stats.Games > 0)
                .ToList();
        }

        private static bool HasPlayerAppearance(PlayerGameLine line)
            => line != null && line.GamesMissedInjury == 0;

        private static void AccumulatePlayerLine(PlayerSeasonStatLine stats, PlayerGameLine line, bool countGame)
        {
            stats.PlayerName = string.IsNullOrWhiteSpace(stats.PlayerName) ? line.PlayerName : stats.PlayerName;
            stats.Pitcher = stats.Pitcher || line.Pitcher;
            if (stats.Classification == PlayerClassification.Unassigned && line.Classification != PlayerClassification.Unassigned)
                stats.Classification = line.Classification;
            if (countGame)
                stats.Games++;
            stats.R += line.R;
            stats.AB += line.AB;
            stats.H += line.H;
            stats.Doubles += line.Doubles;
            stats.Triples += line.Triples;
            stats.HR += line.HR;
            stats.RBI += line.RBI;
            stats.BB += line.BB;
            stats.IBB += line.IBB;
            stats.SO += line.SO;
            stats.SB += line.SB;
            stats.CS += line.CS;
            stats.HBP += line.HBP;
            stats.SH += line.SH;
            stats.SF += line.SF;
            stats.FlyOuts += line.FlyOuts;
            stats.GroundOuts += line.GroundOuts;
            stats.PopOuts += line.PopOuts;
            stats.GroundedIntoDoublePlays += line.GroundedIntoDoublePlays;
            stats.ReachedOnError += line.ReachedOnError;
            stats.IPOuts += line.IPOuts;
            stats.ER += line.ER;
            stats.RunsAllowed += line.RunsAllowed;
            stats.K += line.K;
            stats.HitsAllowed += line.HitsAllowed;
            stats.DoublesAllowed += line.DoublesAllowed;
            stats.TriplesAllowed += line.TriplesAllowed;
            stats.WalksAllowed += line.WalksAllowed;
            stats.IntentionalWalksAllowed += line.IntentionalWalksAllowed;
            stats.HomeRunsAllowed += line.HomeRunsAllowed;
            stats.HitBatters += line.HitBatters;
            stats.WildPitches += line.WildPitches;
            stats.Balks += line.Balks;
            stats.BattersFaced += line.BattersFaced;
            stats.PitchCount += line.PitchCount;
            stats.PitchingWins += line.Wins;
            stats.PitchingLosses += line.Losses;
            stats.Saves += line.Saves;
            stats.Holds += line.Holds;
            stats.BlownSaves += line.BlownSaves;
            stats.CompleteGames += line.CompleteGames;
            stats.Shutouts += line.Shutouts;
            stats.Putouts += line.Putouts;
            stats.Assists += line.Assists;
            stats.Errors += line.Errors;
            stats.DefensiveOuts += line.DefensiveOuts;
            stats.DefensiveDoublePlays += line.DefensiveDoublePlays;
            stats.PassedBalls += line.PassedBalls;
            stats.StolenBasesAllowed += line.StolenBasesAllowed;
            stats.CatcherCaughtStealing += line.CatcherCaughtStealing;
            stats.GamesMissedInjury += line.GamesMissedInjury;
        }

        private static void ApplyHallCareerExtrapolation(HallOfFameCandidate candidate)
        {
            if (candidate?.Stats == null || candidate.Stats.Games <= 0)
                return;

            var initialClass = candidate.InitialClassification != PlayerClassification.Unassigned
                ? candidate.InitialClassification
                : candidate.Classification;
            int projectedSeasons = MissingHallProjectionSeasons(initialClass);
            if (projectedSeasons <= 0)
                return;

            int playedSeasons = Math.Max(1, candidate.StatSeasonIds.Count);
            var s = candidate.Stats;
            s.Games = AddProjectedAverage(s.Games, playedSeasons, projectedSeasons);
            s.R = AddProjectedAverage(s.R, playedSeasons, projectedSeasons);
            s.AB = AddProjectedAverage(s.AB, playedSeasons, projectedSeasons);
            s.H = AddProjectedAverage(s.H, playedSeasons, projectedSeasons);
            s.Doubles = AddProjectedAverage(s.Doubles, playedSeasons, projectedSeasons);
            s.Triples = AddProjectedAverage(s.Triples, playedSeasons, projectedSeasons);
            s.HR = AddProjectedAverage(s.HR, playedSeasons, projectedSeasons);
            s.RBI = AddProjectedAverage(s.RBI, playedSeasons, projectedSeasons);
            s.BB = AddProjectedAverage(s.BB, playedSeasons, projectedSeasons);
            s.IBB = AddProjectedAverage(s.IBB, playedSeasons, projectedSeasons);
            s.SO = AddProjectedAverage(s.SO, playedSeasons, projectedSeasons);
            s.SB = AddProjectedAverage(s.SB, playedSeasons, projectedSeasons);
            s.CS = AddProjectedAverage(s.CS, playedSeasons, projectedSeasons);
            s.HBP = AddProjectedAverage(s.HBP, playedSeasons, projectedSeasons);
            s.SH = AddProjectedAverage(s.SH, playedSeasons, projectedSeasons);
            s.SF = AddProjectedAverage(s.SF, playedSeasons, projectedSeasons);
            s.FlyOuts = AddProjectedAverage(s.FlyOuts, playedSeasons, projectedSeasons);
            s.GroundOuts = AddProjectedAverage(s.GroundOuts, playedSeasons, projectedSeasons);
            s.PopOuts = AddProjectedAverage(s.PopOuts, playedSeasons, projectedSeasons);
            s.GroundedIntoDoublePlays = AddProjectedAverage(s.GroundedIntoDoublePlays, playedSeasons, projectedSeasons);
            s.ReachedOnError = AddProjectedAverage(s.ReachedOnError, playedSeasons, projectedSeasons);
            s.IPOuts = AddProjectedAverage(s.IPOuts, playedSeasons, projectedSeasons);
            s.ER = AddProjectedAverage(s.ER, playedSeasons, projectedSeasons);
            s.RunsAllowed = AddProjectedAverage(s.RunsAllowed, playedSeasons, projectedSeasons);
            s.K = AddProjectedAverage(s.K, playedSeasons, projectedSeasons);
            s.HitsAllowed = AddProjectedAverage(s.HitsAllowed, playedSeasons, projectedSeasons);
            s.DoublesAllowed = AddProjectedAverage(s.DoublesAllowed, playedSeasons, projectedSeasons);
            s.TriplesAllowed = AddProjectedAverage(s.TriplesAllowed, playedSeasons, projectedSeasons);
            s.WalksAllowed = AddProjectedAverage(s.WalksAllowed, playedSeasons, projectedSeasons);
            s.IntentionalWalksAllowed = AddProjectedAverage(s.IntentionalWalksAllowed, playedSeasons, projectedSeasons);
            s.HomeRunsAllowed = AddProjectedAverage(s.HomeRunsAllowed, playedSeasons, projectedSeasons);
            s.HitBatters = AddProjectedAverage(s.HitBatters, playedSeasons, projectedSeasons);
            s.WildPitches = AddProjectedAverage(s.WildPitches, playedSeasons, projectedSeasons);
            s.Balks = AddProjectedAverage(s.Balks, playedSeasons, projectedSeasons);
            s.BattersFaced = AddProjectedAverage(s.BattersFaced, playedSeasons, projectedSeasons);
            s.PitchCount = AddProjectedAverage(s.PitchCount, playedSeasons, projectedSeasons);
            s.PitchingWins = AddProjectedAverage(s.PitchingWins, playedSeasons, projectedSeasons);
            s.PitchingLosses = AddProjectedAverage(s.PitchingLosses, playedSeasons, projectedSeasons);
            s.Saves = AddProjectedAverage(s.Saves, playedSeasons, projectedSeasons);
            s.Holds = AddProjectedAverage(s.Holds, playedSeasons, projectedSeasons);
            s.BlownSaves = AddProjectedAverage(s.BlownSaves, playedSeasons, projectedSeasons);
            s.CompleteGames = AddProjectedAverage(s.CompleteGames, playedSeasons, projectedSeasons);
            s.Shutouts = AddProjectedAverage(s.Shutouts, playedSeasons, projectedSeasons);
            s.Putouts = AddProjectedAverage(s.Putouts, playedSeasons, projectedSeasons);
            s.Assists = AddProjectedAverage(s.Assists, playedSeasons, projectedSeasons);
            s.Errors = AddProjectedAverage(s.Errors, playedSeasons, projectedSeasons);
            s.DefensiveOuts = AddProjectedAverage(s.DefensiveOuts, playedSeasons, projectedSeasons);
            s.DefensiveDoublePlays = AddProjectedAverage(s.DefensiveDoublePlays, playedSeasons, projectedSeasons);
            s.PassedBalls = AddProjectedAverage(s.PassedBalls, playedSeasons, projectedSeasons);
            s.StolenBasesAllowed = AddProjectedAverage(s.StolenBasesAllowed, playedSeasons, projectedSeasons);
            s.CatcherCaughtStealing = AddProjectedAverage(s.CatcherCaughtStealing, playedSeasons, projectedSeasons);
            s.GamesMissedInjury = AddProjectedAverage(s.GamesMissedInjury, playedSeasons, projectedSeasons);

            candidate.ExtrapolationReason = "initial " + initialClass + " career extrapolation: " +
                playedSeasons + " played season(s), +" + projectedSeasons + " projected average season(s)";
        }

        private static int MissingHallProjectionSeasons(PlayerClassification classification)
        {
            return classification switch
            {
                PlayerClassification.Senior => 3,
                PlayerClassification.Junior => 2,
                PlayerClassification.Sophomore => 1,
                _ => 0
            };
        }

        private static int AddProjectedAverage(int value, int playedSeasons, int projectedSeasons)
        {
            if (value == 0 || playedSeasons <= 0 || projectedSeasons <= 0)
                return value;

            return value + (int)Math.Round(value / (double)playedSeasons * projectedSeasons, MidpointRounding.AwayFromZero);
        }

        private int CalculateHallScore(HallOfFameCandidate candidate)
        {
            var s = candidate.Stats;
            double hitterScore =
                s.Games * 0.8 +
                s.H * 1.1 +
                s.ExtraBaseHits * 0.75 +
                s.HR * 4.5 +
                s.RBI * 1.4 +
                s.SB * 1.5 +
                s.TotalChances * 0.03 +
                (s.CatcherStealAttempts >= 20 ? Math.Max(0, s.CatcherCaughtStealingPercentage - 0.25) * 40.0 : 0.0) +
                Math.Max(0, ObpValue(s.H, s.BB, s.HBP, s.AB, s.SF) + SlgValue(s.TotalBases, s.AB) - 0.650) * 95.0;
            double pitcherScore =
                s.PitchingWins * 8.0 +
                s.Saves * 5.0 +
                s.Holds * 3.0 +
                s.CompleteGames * 5.0 +
                s.Shutouts * 8.0 +
                s.K * 1.2 +
                (s.IPOuts / 3.0) * 1.1;
            if (s.IPOuts > 0)
            {
                pitcherScore +=
                    Math.Max(0, 4.50 - EraValue(s.ER, s.IPOuts)) * 10.0 +
                    Math.Max(0, 1.45 - WhipValue(s.WalksAllowed, s.HitsAllowed, s.IPOuts)) * 20.0;
            }
            double titleScore = candidate.Championships * 8.0;
            return (int)Math.Round(Math.Max(hitterScore, pitcherScore) + titleScore + candidate.LeaderBonus + candidate.PlayoffBonus);
        }

        private int CalculateHallPlayoffBonus(HallOfFameCandidate candidate, out string reason)
        {
            reason = "";
            var s = candidate?.PlayoffStats;
            if (s == null || s.Games <= 0)
                return 0;

            double ops = ObpValue(s.H, s.BB, s.HBP, s.AB, s.SF) + SlgValue(s.TotalBases, s.AB);
            double hitterScore =
                s.Games * 1.0 +
                s.H * 2.0 +
                s.ExtraBaseHits * 1.0 +
                s.HR * 8.0 +
                s.RBI * 3.0 +
                s.SB * 2.0;
            if (s.AB >= 10)
                hitterScore += Math.Max(0, ops - 0.800) * 120.0;

            double pitcherScore =
                s.PitchingWins * 12.0 +
                s.Saves * 8.0 +
                s.Holds * 5.0 +
                s.CompleteGames * 8.0 +
                s.Shutouts * 12.0 +
                s.K * 1.8 +
                (s.IPOuts / 3.0) * 1.6;
            if (s.IPOuts >= 9)
            {
                pitcherScore +=
                    Math.Max(0, 3.00 - EraValue(s.ER, s.IPOuts)) * 14.0 +
                    Math.Max(0, 1.20 - WhipValue(s.WalksAllowed, s.HitsAllowed, s.IPOuts)) * 22.0;
            }

            int bonus = (int)Math.Round(Math.Min(75.0, Math.Max(hitterScore, pitcherScore)));
            if (bonus <= 0)
                return 0;

            var details = new List<string>();
            if (s.H > 0) details.Add(s.H + " playoff H");
            if (s.HR > 0) details.Add(s.HR + " playoff HR");
            if (s.RBI > 0) details.Add(s.RBI + " playoff RBI");
            if (s.SB > 0) details.Add(s.SB + " playoff SB");
            if (s.PitchingWins > 0) details.Add(s.PitchingWins + " playoff W");
            if (s.Saves > 0) details.Add(s.Saves + " playoff SV");
            if (s.Holds > 0) details.Add(s.Holds + " playoff HLD");
            if (s.CompleteGames > 0) details.Add(s.CompleteGames + " playoff CG");
            if (s.Shutouts > 0) details.Add(s.Shutouts + " playoff SHO");
            if (s.K > 0) details.Add(s.K + " playoff K");
            if (s.IPOuts > 0) details.Add((s.IPOuts / 3.0).ToString("0.0") + " playoff IP");
            if (s.AB >= 10) details.Add("playoff OPS " + ops.ToString("0.000"));
            if (s.IPOuts >= 9) details.Add("playoff ERA " + EraValue(s.ER, s.IPOuts).ToString("0.00"));

            reason = string.Join("; ", details);
            return bonus;
        }

        private int CalculateHallLeaderBonus(HallOfFameCandidate candidate, List<HallOfFameCandidate> candidates, out string reason)
        {
            var details = new List<string>();
            if (candidate?.Stats == null || candidates == null || candidates.Count == 0)
            {
                reason = "";
                return 0;
            }

            int total = 0;
            total += BestLeaderBonus(candidate, candidates, "G", s => s.Games, s => s.Games > 0, false, details);
            total += BestLeaderBonus(candidate, candidates, "H", s => s.H, s => s.H > 0, false, details);
            total += BestLeaderBonus(candidate, candidates, "XBH", s => s.ExtraBaseHits, s => s.ExtraBaseHits > 0, false, details);
            total += BestLeaderBonus(candidate, candidates, "HR", s => s.HR, s => s.HR > 0, false, details);
            total += BestLeaderBonus(candidate, candidates, "RBI", s => s.RBI, s => s.RBI > 0, false, details);
            total += BestLeaderBonus(candidate, candidates, "SB", s => s.SB, s => s.SB > 0, false, details);
            total += BestLeaderBonus(candidate, candidates, "OPS", s => ObpValue(s.H, s.BB, s.HBP, s.AB, s.SF) + SlgValue(s.TotalBases, s.AB), s => s.AB >= 20 && ObpValue(s.H, s.BB, s.HBP, s.AB, s.SF) + SlgValue(s.TotalBases, s.AB) > 0.650, false, details);
            total += BestLeaderBonus(candidate, candidates, "W", s => s.PitchingWins, s => s.PitchingWins > 0, false, details);
            total += BestLeaderBonus(candidate, candidates, "SV", s => s.Saves, s => s.Saves > 0, false, details);
            total += BestLeaderBonus(candidate, candidates, "HLD", s => s.Holds, s => s.Holds > 0, false, details);
            total += BestLeaderBonus(candidate, candidates, "CG", s => s.CompleteGames, s => s.CompleteGames > 0, false, details);
            total += BestLeaderBonus(candidate, candidates, "SHO", s => s.Shutouts, s => s.Shutouts > 0, false, details);
            total += BestLeaderBonus(candidate, candidates, "Catcher CS%", s => s.CatcherCaughtStealingPercentage, s => s.CatcherStealAttempts >= 20, false, details);
            total += BestLeaderBonus(candidate, candidates, "K", s => s.K, s => s.K > 0, false, details);
            total += BestLeaderBonus(candidate, candidates, "IP", s => s.IPOuts / 3.0, s => s.IPOuts > 0, false, details);
            total += BestLeaderBonus(candidate, candidates, "ERA", s => EraValue(s.ER, s.IPOuts), s => s.IPOuts >= 15, true, details);
            total += BestLeaderBonus(candidate, candidates, "WHIP", s => WhipValue(s.WalksAllowed, s.HitsAllowed, s.IPOuts), s => s.IPOuts >= 15, true, details);

            reason = string.Join("; ", details);
            return total;
        }

        private int BestLeaderBonus(HallOfFameCandidate candidate, List<HallOfFameCandidate> candidates, string label, Func<PlayerSeasonStatLine, double> value, Func<PlayerSeasonStatLine, bool> qualifies, bool lowerIsBetter, List<string> details)
        {
            int bestBonus = 0;
            string bestLevel = "";
            if (IsStatLeader(candidate, candidates, c => IsSameDistrict(candidate.TeamId, c.TeamId), value, qualifies, lowerIsBetter))
            {
                bestBonus = 25;
                bestLevel = "district";
            }
            if (IsStatLeader(candidate, candidates, c => IsSameRegion(candidate.TeamId, c.TeamId), value, qualifies, lowerIsBetter))
            {
                bestBonus = 50;
                bestLevel = "region";
            }
            if (IsStatLeader(candidate, candidates, c => IsSameConference(candidate.TeamId, c.TeamId), value, qualifies, lowerIsBetter))
            {
                bestBonus = 75;
                bestLevel = "conference";
            }
            if (IsStatLeader(candidate, candidates, c => true, value, qualifies, lowerIsBetter))
            {
                bestBonus = 100;
                bestLevel = "league";
            }

            if (bestBonus > 0)
                details?.Add("+" + bestBonus + " " + bestLevel + " " + label + " leader");
            return bestBonus;
        }

        private static bool IsStatLeader(HallOfFameCandidate candidate, IEnumerable<HallOfFameCandidate> candidates, Func<HallOfFameCandidate, bool> sameGroup, Func<PlayerSeasonStatLine, double> value, Func<PlayerSeasonStatLine, bool> qualifies, bool lowerIsBetter)
        {
            if (candidate?.Stats == null || qualifies == null || !qualifies(candidate.Stats))
                return false;

            var group = candidates
                .Where(c => c?.Stats != null && sameGroup(c) && qualifies(c.Stats))
                .ToList();
            if (group.Count == 0)
                return false;

            double candidateValue = value(candidate.Stats);
            double best = lowerIsBetter
                ? group.Min(c => value(c.Stats))
                : group.Max(c => value(c.Stats));
            return Math.Abs(candidateValue - best) < 0.0001;
        }

        private bool IsSameDistrict(Guid teamAId, Guid teamBId)
        {
            var a = FindTeamPlacement(teamAId);
            var b = FindTeamPlacement(teamBId);
            return a?.District != null && b?.District != null && a.District.Id == b.District.Id;
        }

        private bool IsSameRegion(Guid teamAId, Guid teamBId)
        {
            var a = FindTeamPlacement(teamAId);
            var b = FindTeamPlacement(teamBId);
            return a?.Region != null && b?.Region != null && a.Region.Id == b.Region.Id;
        }

        private bool IsSameConference(Guid teamAId, Guid teamBId)
        {
            var a = FindTeamPlacement(teamAId);
            var b = FindTeamPlacement(teamBId);
            return a?.Conference != null && b?.Conference != null && a.Conference.Id == b.Conference.Id;
        }

        private string BuildHallReason(HallOfFameCandidate candidate)
        {
            var s = candidate.Stats;
            var parts = new List<string>();
            if (s.H > 0) parts.Add(s.H + " H");
            if (s.HR > 0) parts.Add(s.HR + " HR");
            if (s.RBI > 0) parts.Add(s.RBI + " RBI");
            if (s.SB > 0) parts.Add(s.SB + " SB");
            if (s.K > 0) parts.Add(s.K + " K");
            if (s.PitchingWins > 0) parts.Add(s.PitchingWins + " W");
            if (s.Saves > 0) parts.Add(s.Saves + " SV");
            if (candidate.Championships > 0) parts.Add(candidate.Championships + " championship team(s)");
            if (!string.IsNullOrWhiteSpace(candidate.ExtrapolationReason)) parts.Add(candidate.ExtrapolationReason);
            if (candidate.LeaderBonus > 0) parts.Add(candidate.LeaderBonus + " leader bonus point(s): " + candidate.LeaderBonusReason);
            if (candidate.PlayoffBonus > 0) parts.Add(candidate.PlayoffBonus + " playoff bonus point(s): " + candidate.PlayoffBonusReason);
            if (parts.Count == 0) parts.Add("career profile");
            return string.Join(", ", parts);
        }

        private int CountTeamChampionships(Guid teamId)
            => _league?.Seasons?.Count(s => s.ChampionTeamId == teamId) ?? 0;

        private void RefreshDynastyGrid()
        {
            if (_championshipGrid == null) return;

            _championshipGrid.Rows.Clear();
            ClearDynastyLogoImages();
            if (_league?.Seasons == null) return;

            for (int i = 0; i < _league.Seasons.Count; i++)
            {
                var season = _league.Seasons[i];
                if (!season.ChampionTeamId.HasValue)
                    continue;

                var champion = TeamById(season.ChampionTeamId.Value);
                if (champion == null)
                    continue;

                Image logo = CreateDynastyLogoImage(champion);
                _dynastyLogoImages.Add(logo);
                _championshipGrid.Rows.Add(
                    "Season " + (i + 1),
                    logo,
                    champion.DisplayName,
                    TeamRecordText(season, champion.Id),
                    season.Name);
            }
        }

        private void ClearDynastyLogoImages()
        {
            foreach (var image in _dynastyLogoImages)
                image.Dispose();
            _dynastyLogoImages.Clear();
        }

        private void RefreshChampionBanner()
        {
            if (_championLabel == null) return;

            var season = SelectedSeason(_seasonCombo);
            if (season == null)
            {
                _championLabel.Text = "No season selected";
                _championLabel.BackColor = Color.FromArgb(242, 244, 248);
                _championLabel.ForeColor = Color.FromArgb(31, 41, 55);
                return;
            }

            var champion = season.ChampionTeamId.HasValue ? TeamById(season.ChampionTeamId.Value) : null;
            if (champion == null)
            {
                _championLabel.Text = season.Year + " Champion: TBD";
                _championLabel.BackColor = Color.FromArgb(242, 244, 248);
                _championLabel.ForeColor = Color.FromArgb(31, 41, 55);
                return;
            }

            Color primary = Color.FromArgb(champion.PrimaryArgb);
            _championLabel.Text = season.Year + " Champion: " + champion.DisplayName + " (" + champion.ScoreboardName + ")";
            _championLabel.BackColor = primary;
            _championLabel.ForeColor = ReadableTextColor(primary);
        }

        private void RefreshGamesGrid()
        {
            if (_gamesGrid == null) return;
            _gamesGrid.Rows.Clear();
            var season = SelectedSeason(_seasonCombo);
            if (season == null) return;
            season.Schedule ??= new List<ScheduledGame>();
            season.Games ??= new List<GameResult>();

            var shownResults = new HashSet<Guid>();
            foreach (var scheduled in season.Schedule.OrderBy(g => g.GameNumber).ThenBy(g => g.Week))
            {
                var game = scheduled.PlayedGameId.HasValue
                    ? season.Games.FirstOrDefault(g => g.Id == scheduled.PlayedGameId.Value)
                    : null;
                if (game != null)
                    shownResults.Add(game.Id);

                string away = FindTeam(scheduled.AwayTeamId);
                string home = FindTeam(scheduled.HomeTeamId);
                string status = game == null ? "Pending" : "Final";
                string date = game == null ? ScheduleDateLabel(scheduled) : game.PlayedAt.ToString("g");
                string awayScore = game == null ? "" : game.AwayScore.ToString();
                string homeScore = game == null ? "" : game.HomeScore.ToString();
                string winner = game == null ? "" : game.AwayScore == game.HomeScore ? "Tie" : game.AwayScore > game.HomeScore ? away : home;
                _gamesGrid.Rows.Add(status, scheduled.Type, date, away, awayScore, home, homeScore, winner);
            }

            foreach (var game in season.Games.Where(g => !shownResults.Contains(g.Id)).OrderByDescending(g => g.PlayedAt))
            {
                var away = FindTeam(game.AwayTeamId);
                var home = FindTeam(game.HomeTeamId);
                string winner = game.AwayScore == game.HomeScore ? "Tie" : game.AwayScore > game.HomeScore ? away : home;
                string gameType = game.IsPlayoff
                    ? (string.IsNullOrWhiteSpace(game.PlayoffRoundName) ? "Playoff" : game.PlayoffRoundName)
                    : "Manual";
                _gamesGrid.Rows.Add("Final", gameType, game.PlayedAt.ToString("g"), away, game.AwayScore, home, game.HomeScore, winner);
            }
        }

        private static string ScheduleDateLabel(ScheduledGame scheduled)
        {
            if (scheduled == null)
                return "";
            string day = string.IsNullOrWhiteSpace(scheduled.DayLabel) ? "Game Day" : scheduled.DayLabel;
            string gameNumber = scheduled.GameNumber > 0 ? " G#" + scheduled.GameNumber : "";
            return "Week " + scheduled.Week + " " + day + gameNumber;
        }

        private void RefreshPlayoffGrid()
        {
            if (_playoffGrid == null) return;
            _playoffGrid.Rows.Clear();
            var season = SelectedSeason(_seasonCombo);
            if (season == null) return;
            foreach (var series in season.Playoffs.OrderBy(s => s.Round).ThenBy(s => s.BracketGroup))
            {
                PlayoffEngine.AssignHomeAdvantage(_league, season, series);
                string a = series.TeamAId == Guid.Empty ? "TBD" : FindTeam(series.TeamAId);
                string b = series.TeamBId == Guid.Empty ? "TBD" : FindTeam(series.TeamBId);
                string homeAdv = series.HomeAdvantageTeamId.HasValue && series.HomeAdvantageTeamId.Value != Guid.Empty
                    ? FindTeam(series.HomeAdvantageTeamId.Value)
                    : "TBD";
                string result = series.WinnerTeamId.HasValue
                    ? (series.TeamAWins + "-" + series.TeamBWins)
                    : "";
                _playoffGrid.Rows.Add(series.Round, series.RoundName, series.BestOf, series.BracketGroup, a, b, homeAdv, result, series.Notes);
            }
        }

        private void GeneratePlayoffs()
        {
            var season = SelectedSeason(_seasonCombo);
            if (season == null) { MessageBox.Show(this, "Select a season first."); return; }
            if (RankingEngine.LatestRegularSeasonPoll(season) == null)
                RankingEngine.SavePoll(season, RankingEngine.GeneratePoll(_league, season, RankingPollType.Weekly, CurrentCompletedWeek(season)));
            var series = PlayoffEngine.GeneratePlayoffs(_league, season, out string error);
            if (error != null)
            {
                MessageBox.Show(this, error, "Could not generate playoffs", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            season.Playoffs = series;
            season.ChampionTeamId = null;
            MarkDirty();
            RefreshSeasonViews();
            RefreshRankingPollCombo();
            RefreshRankingGrid();
        }

        private void SimPlayoffSeries()
        {
            var season = SelectedSeason(_seasonCombo);
            if (season == null || season.Playoffs.Count == 0)
            {
                MessageBox.Show(this, "Generate playoffs first.");
                return;
            }

            int simulated = 0;
            Team newChampion = null;
            PlayoffSeries championshipSeries = null;
            while (true)
            {
                AdvancePlayoffBracket(season);
                var series = season.Playoffs
                    .Where(s => s.TeamAId != Guid.Empty && s.TeamBId != Guid.Empty && !s.WinnerTeamId.HasValue)
                    .OrderBy(s => s.Round)
                    .ThenBy(s => s.BracketGroup)
                    .FirstOrDefault();
                if (series == null)
                    break;

                var a = TeamById(series.TeamAId);
                var b = TeamById(series.TeamBId);
                if (a == null || b == null)
                    break;
                EnsureTeamCoaches(a);
                EnsureTeamCoaches(b);
                if (series.TeamACoachId == Guid.Empty)
                    series.TeamACoachId = a.CoachId;
                if (series.TeamBCoachId == Guid.Empty)
                    series.TeamBCoachId = b.CoachId;
                PlayoffEngine.AssignHomeAdvantage(_league, season, series);

                int need = series.BestOf / 2 + 1;
                while (series.TeamAWins < need && series.TeamBWins < need)
                {
                    int gameNumber = series.TeamAWins + series.TeamBWins + 1;
                    Guid homeTeamId = PlayoffEngine.HomeTeamForSeriesGame(series, gameNumber);
                    Team home = homeTeamId == b.Id ? b : a;
                    Team away = home.Id == a.Id ? b : a;
                    InjuryEngine.ProcessGameInjuries(away, home, _rng);
                    if (!ValidateGameStart(season, null, away, home, enforceScheduleOrder: false))
                    {
                        if (simulated > 0)
                            AutosaveCommittedChanges("Playoff simulation");
                        return;
                    }
                    var game = Simulator.SimGame(_league, away, home, _rng, RankingModifierForGame(season, away, home));
                    game.PlayoffSeriesId = series.Id;
                    CommitGameResult(season, null, game, refresh: false, showChampion: false, autoSave: false);
                }

                if (season.ChampionTeamId.HasValue)
                {
                    newChampion = TeamById(season.ChampionTeamId.Value);
                    championshipSeries = series;
                }
                simulated++;
            }

            if (newChampion == null)
            {
                foreach (var series in season.Playoffs.Where(s => s.WinnerTeamId.HasValue))
                {
                    if (TryRecordChampion(season, series, out var champion))
                    {
                        newChampion = champion;
                        championshipSeries = series;
                        break;
                    }
                }
            }

            if (simulated == 0 && newChampion == null) MessageBox.Show(this, "No unresolved playoff series with two assigned teams are available.");
            MarkDirty();
            bool saved = AutosaveCommittedChanges("Playoff simulation");
            RefreshSeasonViews();
            if (!saved)
                _status.Text = "Playoff results were committed in memory, but autosave was canceled or failed. Use File > Save.";
            if (newChampion != null && championshipSeries != null)
                ShowChampionshipDialog(season, newChampion, championshipSeries);
        }

        private bool TryRecordChampion(Season season, PlayoffSeries series, out Team champion)
        {
            return ChampionshipLifecycleEngine.TryRecordChampion(_league, season, series, out champion);
        }

        private static bool IsFinalChampionshipSeries(PlayoffSeries series)
        {
            return ChampionshipLifecycleEngine.IsFinalChampionshipSeries(series);
        }

        private void ShowChampionshipDialog(Season season, Team champion, PlayoffSeries series)
        {
            int seasonNumber = CurrentSeasonNumber(season);
            bool backToBackChampion = IsBackToBackChampion(season, champion);
            string logoPath = GetTeamLogoPath(champion) ?? "";
            var photoPaths = GetTeamPhotoPaths(champion);
            using var dialog = new ChampionshipDialog(season, seasonNumber, backToBackChampion, champion, series, logoPath, photoPaths);
            TriggerCutscene(CutsceneTrigger.ChampionshipWon, champion);
            _worldSeriesChampionsSound.PlayOnce(LaunchSoundPlayer.FindWorldSeriesChampions());
            dialog.ShowDialog(this);
        }

        private int CurrentSeasonNumber(Season season)
        {
            if (season == null || _league?.Seasons == null)
                return 1;

            int index = _league.Seasons.FindIndex(s => s.Id == season.Id);
            return index < 0 ? 1 : index + 1;
        }

        private bool IsBackToBackChampion(Season season, Team champion)
        {
            if (season == null || champion == null || _league?.Seasons == null)
                return false;

            int index = _league.Seasons.FindIndex(s => s.Id == season.Id);
            if (index <= 0)
                return false;

            return _league.Seasons[index - 1].ChampionTeamId == champion.Id;
        }

        private string FindTeam(Guid id) => _league.Teams.FirstOrDefault(t => t.Id == id)?.DisplayName ?? "(deleted team)";
        private Team TeamById(Guid id) => _league.Teams.FirstOrDefault(t => t.Id == id);
        private Player PlayerById(Guid id)
            => _league?.Teams?
                .SelectMany(t => (t.Roster ?? Enumerable.Empty<Player>())
                    .Concat(t.InjuredReserve ?? Enumerable.Empty<Player>())
                    .Concat((t.JvPool ?? new List<Player>()).Where(player => player.VarsitySeasonsPlayed > 0)))
                .FirstOrDefault(p => p.Id == id);

        private static string ScoreboardLine(Team away, int awayScore, Team home, int homeScore)
            => away.ScoreboardName + " " + awayScore + "  @  " + home.ScoreboardName + " " + homeScore;

        private static Color ReadableTextColor(Color background)
        {
            int brightness = (background.R * 299 + background.G * 587 + background.B * 114) / 1000;
            return brightness >= 145 ? Color.FromArgb(20, 24, 32) : Color.White;
        }

        private void LoadScoreboardPhotos(Team away, Team home)
        {
            _scoreboardPhotoPaths.Clear();
            _scoreboardPhotoPaths.AddRange(GetTeamPhotoPaths(away));
            _scoreboardPhotoPaths.AddRange(GetTeamPhotoPaths(home));
            _scoreboardPhotoIndex = 0;
            _scoreboardPhotoTimer.Enabled = _scoreboardPhotoPaths.Count > 1;
        }

        private void PaintField(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var r = _fieldPanel.ClientRectangle;
            var preset = SelectedFieldPreset();
            string backgroundPath = ResolveFieldPreviewAssetPath(preset);
            if (!string.IsNullOrWhiteSpace(backgroundPath) && File.Exists(backgroundPath))
            {
                try
                {
                    using var image = Image.FromFile(backgroundPath);
                    g.DrawImage(image, CoverImage(image.Size, r));
                    using var shade = new SolidBrush(Color.FromArgb(45, Color.Black));
                    g.FillRectangle(shade, r);
                    DrawFieldPreviewOverlays(g, r, preset);
                    DrawFieldPreviewLabel(g, r, preset);
                    if (_lastGame != null)
                    {
                        var board = new Rectangle(20, 18, Math.Min(520, r.Width - 40), 118);
                        DrawScoreboard(g, board);
                    }
                    return;
                }
                catch { }
            }

            g.Clear(preset.DarkGrassColor);
            Point c = new Point(r.Width / 2, r.Height / 2 + 70);
            Point home = new Point(c.X, c.Y + 120);
            Point first = new Point(c.X + 130, c.Y);
            Point second = new Point(c.X, c.Y - 120);
            Point third = new Point(c.X - 130, c.Y);
            using var wall = new Pen(preset.WallColor, 8);
            g.DrawArc(wall, c.X - 240, c.Y - 300, 480, 380, 25, 130);
            using var seats = new SolidBrush(Color.FromArgb(150, preset.SeatColor));
            g.FillRectangle(seats, c.X - 190, c.Y - 270, 380, 34);
            using var dirt = new SolidBrush(preset.InfieldColor);
            g.FillPolygon(dirt, new[] { home, first, second, third });
            using var grass = new SolidBrush(preset.GrassColor);
            g.FillEllipse(grass, c.X - 80, c.Y - 80, 160, 160);
            using var white = new SolidBrush(Color.White);
            foreach (var p in new[] { home, first, second, third })
                g.FillRectangle(white, p.X - 7, p.Y - 7, 14, 14);
            DrawFieldPreviewOverlays(g, r, preset);
            DrawFieldPreviewLabel(g, r, preset);
            if (_lastGame != null)
            {
                using var font = new Font(Font.FontFamily, 18, FontStyle.Bold);
                var board = new Rectangle(20, 18, Math.Min(520, r.Width - 40), 118);
                DrawScoreboard(g, board);
            }
        }

        private string ResolveFieldPreviewAssetPath(BaseballFieldPreset preset)
        {
            if (preset == null || string.IsNullOrWhiteSpace(preset.BackgroundAssetPath))
                return null;
            return AssetPathResolver.ResolvePath(preset.BackgroundAssetPath);
        }

        private void DrawFieldPreviewOverlays(Graphics g, Rectangle bounds, BaseballFieldPreset preset)
        {
            if (preset?.Overlays == null || preset.Overlays.Count == 0)
                return;

            foreach (var overlay in preset.Overlays)
            {
                string path = ResolveOverlayAssetPath(overlay.AssetPath);
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                    continue;

                try
                {
                    using var image = Image.FromFile(path);
                    float width = Math.Clamp(overlay.Width, 0.02f, 1f) * bounds.Width;
                    float height = Math.Clamp(overlay.Height, 0.02f, 1f) * bounds.Height;
                    float x = bounds.Left + Math.Clamp(overlay.X, 0f, 1f) * bounds.Width - width / 2f;
                    float y = bounds.Top + Math.Clamp(overlay.Y, 0f, 1f) * bounds.Height - height / 2f;
                    var dest = new RectangleF(x, y, width, height);
                    int opacity = Math.Clamp(overlay.Opacity, 0, 255);

                    if (opacity >= 255)
                    {
                        g.DrawImage(image, dest);
                    }
                    else
                    {
                        using var attributes = new ImageAttributes();
                        var matrix = new ColorMatrix { Matrix33 = opacity / 255f };
                        attributes.SetColorMatrix(matrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
                        g.DrawImage(image, Rectangle.Round(dest), 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attributes);
                    }
                }
                catch { }
            }
        }

        private static string ResolveOverlayAssetPath(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
                return null;
            return AssetPathResolver.ResolvePath(assetPath);
        }

        private static Rectangle CoverImage(Size imageSize, Rectangle bounds)
        {
            if (imageSize.Width <= 0 || imageSize.Height <= 0 || bounds.Width <= 0 || bounds.Height <= 0)
                return bounds;

            double scale = Math.Max((double)bounds.Width / imageSize.Width, (double)bounds.Height / imageSize.Height);
            int width = (int)Math.Ceiling(imageSize.Width * scale);
            int height = (int)Math.Ceiling(imageSize.Height * scale);
            return new Rectangle(
                bounds.Left + (bounds.Width - width) / 2,
                bounds.Top + (bounds.Height - height) / 2,
                width,
                height);
        }

        private void DrawFieldPreviewLabel(Graphics g, Rectangle bounds, BaseballFieldPreset preset)
        {
            using var previewFont = new Font(Font.FontFamily, 11, FontStyle.Bold);
            using var labelBg = new SolidBrush(Color.FromArgb(165, 18, 26, 24));
            var label = new Rectangle(18, bounds.Bottom - 42, Math.Min(bounds.Width - 36, 620), 28);
            g.FillRectangle(labelBg, label);
            TextRenderer.DrawText(g, preset.Name + " (" + preset.OpenedYear + ") - " + preset.TeamLabel,
                previewFont, label, Color.White, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        private void DrawScoreboard(Graphics g, Rectangle board)
        {
            using var bg = new SolidBrush(Color.FromArgb(210, 20, 24, 32));
            using var border = new Pen(Color.FromArgb(240, 230, 235, 245), 2);
            g.FillRectangle(bg, board);
            g.DrawRectangle(border, board);

            var photoBox = new Rectangle(board.Right - 130, board.Top + 10, 110, board.Height - 20);
            if (_scoreboardPhotoPaths.Count > 0)
                DrawPhoto(g, _scoreboardPhotoPaths[_scoreboardPhotoIndex % _scoreboardPhotoPaths.Count], photoBox);
            else
            {
                using var empty = new SolidBrush(Color.FromArgb(64, 255, 255, 255));
                g.FillRectangle(empty, photoBox);
                g.DrawRectangle(Pens.White, photoBox);
            }

            using var titleFont = new Font(Font.FontFamily, 18, FontStyle.Bold);
            using var smallFont = new Font(Font.FontFamily, 9, FontStyle.Regular);
            Rectangle textRect = new Rectangle(board.Left + 14, board.Top + 16, board.Width - 160, 44);
            g.DrawString(_simResult.Text, titleFont, Brushes.White, textRect);
            string photoText = _scoreboardPhotoPaths.Count > 0
                ? "TEAM PHOTO " + (_scoreboardPhotoIndex + 1) + "/" + _scoreboardPhotoPaths.Count
                : "NO TEAM PHOTOS";
            g.DrawString(photoText, smallFont, Brushes.Gainsboro, new PointF(board.Left + 16, board.Bottom - 30));
        }

        private static void DrawPhoto(Graphics g, string path, Rectangle bounds)
        {
            try
            {
                using var img = Image.FromFile(path);
                Rectangle dest = FitImage(img.Size, bounds);
                g.FillRectangle(Brushes.Black, bounds);
                g.DrawImage(img, dest);
                g.DrawRectangle(Pens.White, bounds);
            }
            catch
            {
                g.FillRectangle(Brushes.DimGray, bounds);
                g.DrawRectangle(Pens.White, bounds);
            }
        }

        private static Rectangle FitImage(Size image, Rectangle bounds)
        {
            if (image.Width <= 0 || image.Height <= 0) return bounds;
            double scale = Math.Min((double)bounds.Width / image.Width, (double)bounds.Height / image.Height);
            int w = Math.Max(1, (int)Math.Round(image.Width * scale));
            int h = Math.Max(1, (int)Math.Round(image.Height * scale));
            return new Rectangle(bounds.Left + (bounds.Width - w) / 2, bounds.Top + (bounds.Height - h) / 2, w, h);
        }

        private void NewLeague()
        {
            if (!ConfirmDiscard()) return;
            using var setup = new DynastySetupDialog(
                _league?.Rules,
                _league?.Name,
                _league?.OwnerFullName,
                _league?.AssetLibraryPath);
            if (setup.ShowDialog(this) != DialogResult.OK)
                return;

            _league = CreateStarterLeague(setup.SelectedRules, setup.DynastyName, setup.OwnerFullName);
            _league.AssetLibraryPath = setup.AssetLibraryPath ?? "";
            AssetPathResolver.ClearLeagueFilePath();
            using (var controlDialog = new UserControlledTeamsDialog(_league.Teams, _league.UserControlledTeamIds))
            {
                if (controlDialog.ShowDialog(this) == DialogResult.OK)
                    _league.UserControlledTeamIds = controlDialog.SelectedTeamIds;
            }
            if (setup.SelectedRules?.Schedule?.HasAnyGames == true
                && (_league.Seasons.FirstOrDefault()?.Schedule?.Count ?? 0) == 0)
            {
                MessageBox.Show(this,
                    "The dynasty was created, but the initial schedule could not be generated with the current starter teams and structure.\n\nAdd or arrange teams, then use Seasons > Generate Schedule.",
                    "Schedule not generated", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            _path = null;
            AssetPathResolver.ClearLeagueFilePath();
            _lastGame = null;
            _dirty = true;
            RefreshAll();
        }

        private void SetUserControlledTeams()
        {
            _league.UserControlledTeamIds ??= new List<Guid>();
            using var controlDialog = new UserControlledTeamsDialog(_league.Teams, _league.UserControlledTeamIds);
            if (controlDialog.ShowDialog(this) != DialogResult.OK)
                return;

            _league.UserControlledTeamIds = controlDialog.SelectedTeamIds;
            MarkDirty();
            RefreshControlTeamCombo();
            _status.Text = "Updated user controlled teams.";
        }

        private void OpenLeague()
        {
            if (!ConfirmDiscard()) return;
            using var dlg = new OpenFileDialog { Filter = "Dan's RBI Baseball 2026 (*" + LeagueStore.Extension + ")|*" + LeagueStore.Extension + "|JSON (*.json)|*.json|All files (*.*)|*.*" };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            if (LeagueStore.TryLoad(dlg.FileName, out var league, out string error))
            {
                try
                {
                    ApplyLoadedLeague(league, dlg.FileName, recovered: false, null);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, ex.Message, "Could not open dynasty", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                return;
            }

            ShowBackupRecovery(dlg.FileName, error);
        }

        private void RestoreLeagueBackup()
        {
            if (!ConfirmDiscard()) return;
            using var dlg = new OpenFileDialog
            {
                Filter = "Dan's RBI Baseball 2026 (*" + LeagueStore.Extension + ")|*" + LeagueStore.Extension + "|JSON (*.json)|*.json|All files (*.*)|*.*",
                Title = "Select the primary dynasty whose backup you want to restore"
            };
            if (!string.IsNullOrWhiteSpace(_path))
            {
                dlg.InitialDirectory = Path.GetDirectoryName(_path);
                dlg.FileName = Path.GetFileName(_path);
            }
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            ShowBackupRecovery(dlg.FileName, "Manual backup recovery requested.");
        }

        private bool ShowBackupRecovery(string primaryPath, string loadError)
        {
            IReadOnlyList<LeagueBackupInfo> backups;
            try
            {
                backups = LeagueStore.GetBackups(primaryPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this,
                    "The dynasty could not be opened and its backup folder could not be read.\n\n" + ex.Message,
                    "Backup recovery unavailable", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            if (!backups.Any(backup => backup.IsValid))
            {
                string detail = backups.Count == 0
                    ? "No backups were found for this dynasty."
                    : "Backups were found, but none passed validation.";
                MessageBox.Show(this,
                    "The dynasty could not be opened.\n\n" + loadError + "\n\n" + detail,
                    "No valid backup available", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            using var recovery = new DynastyBackupRecoveryDialog(primaryPath, loadError, backups);
            if (recovery.ShowDialog(this) != DialogResult.OK || recovery.SelectedBackup == null)
                return false;

            if (!LeagueStore.TryLoad(recovery.SelectedBackup.Path, out var recoveredLeague, out string recoveryError))
            {
                MessageBox.Show(this,
                    "The selected backup could not be loaded.\n\n" + recoveryError,
                    "Backup recovery failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            try
            {
                ApplyLoadedLeague(recoveredLeague, primaryPath, recovered: true, recovery.SelectedBackup.Path);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Backup recovery failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
        }

        private void ApplyLoadedLeague(LeagueFile league, string primaryPath, bool recovered, string? backupPath)
        {
            _league = league ?? throw new InvalidDataException("The dynasty file did not contain league data.");
            _path = primaryPath;
            AssetPathResolver.SetLeagueFilePath(_path);
            NormalizePortableAssetPaths();
            LoadAllTeamBaseLineupFiles();
            LoadAllTeamPitchingPlanFiles();
            _lastGame = null;
            _dirty = recovered;
            RefreshAll();

            if (!recovered)
                return;

            _status.Text = "Recovered from backup. Save the dynasty to replace the damaged primary file.";
            MessageBox.Show(this,
                "The dynasty was recovered from:\n" + backupPath +
                "\n\nThe damaged primary file has not been changed. Use Save to restore it with the recovered data.",
                "Dynasty recovered", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private bool SaveLeague(bool saveAs)
        {
            LeagueFile? league = _league;
            if (league == null)
            {
                _status.Text = "No dynasty is loaded to save.";
                return false;
            }

            string? previousPath = _path;
            string? targetPath = _path;
            if (saveAs || string.IsNullOrEmpty(targetPath))
            {
                using var dlg = new SaveFileDialog { Filter = "Dan's RBI Baseball 2026 (*" + LeagueStore.Extension + ")|*" + LeagueStore.Extension, FileName = DefaultLeagueSaveFileName() };
                if (dlg.ShowDialog(this) != DialogResult.OK) return false;
                targetPath = dlg.FileName;
            }

            if (string.IsNullOrWhiteSpace(targetPath))
                return false;

            try
            {
                _path = targetPath;
                AssetPathResolver.SetLeagueFilePath(_path);
                foreach (var team in _league?.Teams ?? Enumerable.Empty<Team>())
                {
                    EnsureTeamBaseLineup(team, recalculate: false);
                    EnsureTeamPitchingPlan(team, recalculate: false);
                }
                NormalizePortableAssetPaths();
                string catalogWarning = "";
                try
                {
                    SynchronizeAllSchoolCatalogLogos();
                }
                catch (Exception ex)
                {
                    catalogWarning = ex.Message;
                }
                LeagueStore.Save(targetPath, league);
                SaveAllTeamBaseLineupFiles();
                SaveAllTeamPitchingPlanFiles();
                _dirty = false;
                RefreshAll();
                if (!string.IsNullOrWhiteSpace(catalogWarning))
                    _status.Text = "Dynasty saved, but schools.csv logo synchronization failed: " + catalogWarning;
                return true;
            }
            catch (Exception ex)
            {
                _path = previousPath;
                if (string.IsNullOrWhiteSpace(_path))
                    AssetPathResolver.ClearLeagueFilePath();
                else
                    AssetPathResolver.SetLeagueFilePath(_path);
                _dirty = true;
                RefreshAll();
                _status.Text = "Save failed. The dynasty still has unsaved changes.";
                MessageBox.Show(this,
                    "The dynasty could not be saved. Your in-memory changes remain available.\n\n" + ex.Message,
                    "Could not save dynasty", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
        }

        private string DefaultLeagueSaveFileName()
        {
            string owner = SanitizeFileNamePart(_league?.OwnerFullName);
            string dynasty = SanitizeFileNamePart(_league?.Name);
            string name = string.IsNullOrWhiteSpace(owner)
                ? dynasty
                : owner + " - " + dynasty;
            if (string.IsNullOrWhiteSpace(name))
                name = "Dan's RBI Baseball 2026 Dynasty";
            return name + LeagueStore.Extension;
        }

        private static string SanitizeFileNamePart(string? value)
        {
            value = (value ?? "").Trim();
            if (string.IsNullOrWhiteSpace(value))
                return "";

            foreach (char c in Path.GetInvalidFileNameChars())
                value = value.Replace(c, '_');
            return value.Trim();
        }

        private void ImportRomSnapshot()
        {
            if (!ConfirmDiscard()) return;
            string? leagueDirectory = string.IsNullOrWhiteSpace(_path) ? null : Path.GetDirectoryName(_path);
            string documentsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string initialDirectory = new[] { leagueDirectory, documentsDirectory, AppContext.BaseDirectory }
                .FirstOrDefault(Directory.Exists);
            using var dlg = new OpenFileDialog
            {
                Filter = "NES ROM (*.nes)|*.nes|All files (*.*)|*.*",
                FileName = "R.B.I. Baseball 2025.nes",
                InitialDirectory = initialDirectory ?? ""
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                _league = RomSnapshotImporter.Import(dlg.FileName);
                _path = null;
                AssetPathResolver.ClearLeagueFilePath();
                _lastGame = null;
                _dirty = true;
                RefreshAll();
                MessageBox.Show(this, "Imported a local ROM snapshot into an editable standalone league.\nSave it as a standalone JSON file when ready.", "Import complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Could not import ROM", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void MarkDirty()
        {
            _dirty = true;
            Text = "Dan's RBI Baseball 2026 *";
            if (_status != null) _status.Text = "Unsaved changes.";
        }

        private bool ConfirmDiscard()
        {
            if (!_dirty) return true;
            var r = MessageBox.Show(this, "Save changes first?", "Unsaved changes", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (r == DialogResult.Cancel) return false;
            if (r == DialogResult.Yes) return SaveLeague(false);
            return true;
        }
    }
}
