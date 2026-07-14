#nullable enable annotations

using System;
using System.Collections.Generic;
using System.Linq;

namespace StandaloneBaseball
{
    public enum GameMode
    {
        UserVsCpu,
        PlayerVsPlayer,
        CpuVsCpuWatch,
        QuickSim
    }

    public enum HalfInning
    {
        Top,
        Bottom
    }

    public enum AtBatResultType
    {
        BallInPlayOut,
        Strikeout,
        Walk,
        Single,
        Double,
        Triple,
        HomeRun,
        SacrificeFly,
        GroundOut,
        DoublePlay,
        Error,
        HitByPitch
    }

    public enum PitchOutcomeType
    {
        Ball,
        CalledStrike,
        SwingingStrike,
        Foul,
        InPlay
    }

    public sealed class GameplayState
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public GameMode Mode { get; set; }
        public Guid UserControlledTeamId { get; set; }
        public Guid KeyboardControlledTeamId { get; set; }
        public Guid ControllerControlledTeamId { get; set; }
        public Team AwayTeam { get; set; }
        public Team HomeTeam { get; set; }
        public string FieldPresetId { get; set; } = BaseballFieldPresets.Default.Id;
        public Guid? AwayUniformSetId { get; set; }
        public Guid? HomeUniformSetId { get; set; }
        public int RegulationInnings { get; set; } = 9;
        public bool AllowExtraInnings { get; set; } = true;
        public bool MercyRuleEnabled { get; set; } = true;
        public int MercyRuleRuns { get; set; } = 10;
        public int MercyRuleMinimumInning { get; set; } = 5;
        public bool EndedByMercyRule { get; set; }
        public bool ExtraInningRunnerOnSecond { get; set; } = true;
        public bool CourtesyRunnerForPitchersCatchers { get; set; } = true;
        public int Inning { get; set; } = 1;
        public HalfInning Half { get; set; } = HalfInning.Top;
        public CountState Count { get; set; } = new CountState();
        public BaseState Bases { get; set; } = new BaseState();
        public int AwayScore { get; set; }
        public int HomeScore { get; set; }
        public int AwayBatterIndex { get; set; }
        public int HomeBatterIndex { get; set; }
        public int AwayPitcherIndex { get; set; }
        public int HomePitcherIndex { get; set; }
        public bool IsComplete { get; set; }
        public Guid? PendingExtraInningRunnerId { get; set; }
        public Dictionary<Guid, int> PinchUseCounts { get; set; } = new Dictionary<Guid, int>();
        public List<Guid> RemovedPlayerIds { get; set; } = new List<Guid>();
        public List<Guid> AwayLineupPlayerIds { get; set; } = new List<Guid>();
        public List<Guid> HomeLineupPlayerIds { get; set; } = new List<Guid>();
        public Guid? AwayDesignatedHitterId { get; set; }
        public Guid? HomeDesignatedHitterId { get; set; }
        public bool AwayDhActive { get; set; }
        public bool HomeDhActive { get; set; }
        public List<PlayerGameLine> LiveLines { get; set; } = new List<PlayerGameLine>();
        public List<int> AwayRunsByInning { get; set; } = new List<int>();
        public List<int> HomeRunsByInning { get; set; } = new List<int>();
        public int AwayLeftOnBase { get; set; }
        public int HomeLeftOnBase { get; set; }
        public List<GamePlayByPlayEntry> PlayByPlay { get; set; } = new List<GamePlayByPlayEntry>();
        public List<HalfInningSnapshot> CompletedHalfInnings { get; set; } = new List<HalfInningSnapshot>();
        public GameplayLiveRulesState LiveRules { get; set; } = new GameplayLiveRulesState();

        public Team BattingTeam => Half == HalfInning.Top ? AwayTeam : HomeTeam;
        public Team PitchingTeam => Half == HalfInning.Top ? HomeTeam : AwayTeam;
        public int BattingTeamScore => Half == HalfInning.Top ? AwayScore : HomeScore;
        public int PitchingTeamScore => Half == HalfInning.Top ? HomeScore : AwayScore;
        public int Outs => Count.Outs;
        public bool IsTopHalf => Half == HalfInning.Top;
        public bool IsBottomHalf => Half == HalfInning.Bottom;

        public Player? CurrentBatter => GetLineupPlayer(BattingTeam, CurrentBatterIndex);
        public Player CurrentPitcher => GameplayRules.GetPitcher(PitchingTeam, CurrentPitcherIndex);

        public int CurrentBatterIndex
        {
            get => Half == HalfInning.Top ? AwayBatterIndex : HomeBatterIndex;
            set
            {
                if (Half == HalfInning.Top)
                    AwayBatterIndex = value;
                else
                    HomeBatterIndex = value;
            }
        }

        public int CurrentPitcherIndex
        {
            get => Half == HalfInning.Top ? HomePitcherIndex : AwayPitcherIndex;
            set
            {
                if (Half == HalfInning.Top)
                    HomePitcherIndex = value;
                else
                    AwayPitcherIndex = value;
            }
        }

        public static GameplayState Create(
            Team awayTeam,
            Team homeTeam,
            GameMode mode = GameMode.UserVsCpu,
            int innings = 9,
            bool extraInnings = true,
            bool mercyRuleEnabled = true,
            int mercyRuleRuns = 10,
            int mercyRuleMinimumInning = 5,
            bool extraInningRunnerOnSecond = true,
            bool courtesyRunnerForPitchersCatchers = true)
        {
            if (awayTeam == null) throw new ArgumentNullException(nameof(awayTeam));
            if (homeTeam == null) throw new ArgumentNullException(nameof(homeTeam));

            var state = new GameplayState
            {
                Mode = mode,
                UserControlledTeamId = awayTeam.Id,
                KeyboardControlledTeamId = awayTeam.Id,
                ControllerControlledTeamId = homeTeam.Id,
                AwayTeam = awayTeam,
                HomeTeam = homeTeam,
                RegulationInnings = Math.Clamp(innings, 5, 9),
                AllowExtraInnings = extraInnings,
                MercyRuleEnabled = mercyRuleEnabled,
                MercyRuleRuns = Math.Max(1, mercyRuleRuns),
                MercyRuleMinimumInning = Math.Max(1, mercyRuleMinimumInning),
                ExtraInningRunnerOnSecond = extraInningRunnerOnSecond,
                CourtesyRunnerForPitchersCatchers = courtesyRunnerForPitchersCatchers,
                AwayPitcherIndex = GameplayRules.FindStartingPitcherIndex(awayTeam),
                HomePitcherIndex = GameplayRules.FindStartingPitcherIndex(homeTeam)
            };
            state.InitializeLineups();
            return state;
        }

        public GameResult ToGameResult()
        {
            return new GameResult
            {
                AwayTeamId = AwayTeam?.Id ?? Guid.Empty,
                HomeTeamId = HomeTeam?.Id ?? Guid.Empty,
                AwayScore = AwayScore,
                HomeScore = HomeScore
            };
        }

        internal void AddRuns(int runs)
        {
            if (runs <= 0) return;
            if (Half == HalfInning.Top)
                AwayScore += runs;
            else
                HomeScore += runs;
        }

        public void InitializeLineups()
        {
            ApplyLineupCard(AwayTeam, AwayLineupPlayerIds, away: true);
            ApplyLineupCard(HomeTeam, HomeLineupPlayerIds, away: false);
        }

        public IReadOnlyList<Player> GetBattingOrder(Team team)
        {
            if (team == null)
                return Array.Empty<Player>();

            var ids = team.Id == AwayTeam?.Id ? AwayLineupPlayerIds :
                team.Id == HomeTeam?.Id ? HomeLineupPlayerIds : null;
            if (ids == null || ids.Count == 0)
                return LineupEngine.GetBattingOrder(team);

            var roster = team.Roster ?? new List<Player>();
            return ids
                .Select(id => roster.FirstOrDefault(p => p != null && p.Id == id))
                .Where(p => p != null)
                .ToList();
        }

        public Player? GetLineupPlayer(Team team, int batterIndex)
        {
            var lineup = GetBattingOrder(team);
            if (lineup.Count == 0)
                return null;
            return lineup[PositiveModulo(batterIndex, lineup.Count)];
        }

        public bool LoseDesignatedHitterForTeam(Team team, Player newPitcher)
        {
            if (team == null || newPitcher == null)
                return false;

            bool away = team.Id == AwayTeam?.Id;
            bool active = away ? AwayDhActive : HomeDhActive;
            Guid? dhId = away ? AwayDesignatedHitterId : HomeDesignatedHitterId;
            var lineup = away ? AwayLineupPlayerIds : HomeLineupPlayerIds;
            if (!active || !dhId.HasValue || lineup == null || lineup.Count == 0)
                return false;

            if (!lineup.Contains(newPitcher.Id))
            {
                int dhIndex = lineup.FindIndex(id => id == dhId.Value);
                if (dhIndex >= 0)
                    lineup[dhIndex] = newPitcher.Id;
            }

            if (away)
                AwayDhActive = false;
            else
                HomeDhActive = false;
            return true;
        }

        private void ApplyLineupCard(Team team, List<Guid> target, bool away)
        {
            target.Clear();
            var card = LineupEngine.BuildLineupCard(team);
            target.AddRange(card.BattingOrder.Select(s => s.Player?.Id ?? Guid.Empty).Where(id => id != Guid.Empty));
            var dh = card.BattingOrder.FirstOrDefault(s => s.DesignatedHitter)?.Player;
            if (away)
            {
                AwayDesignatedHitterId = dh?.Id;
                AwayDhActive = dh != null;
            }
            else
            {
                HomeDesignatedHitterId = dh?.Id;
                HomeDhActive = dh != null;
            }
        }

        private static int PositiveModulo(int value, int divisor)
        {
            if (divisor <= 0)
                return 0;

            int result = value % divisor;
            return result < 0 ? result + divisor : result;
        }
    }

    public sealed class CountState
    {
        public int Balls { get; set; }
        public int Strikes { get; set; }
        public int Outs { get; set; }

        public void ResetAtBat()
        {
            Balls = 0;
            Strikes = 0;
        }

        public void ResetHalfInning()
        {
            Balls = 0;
            Strikes = 0;
            Outs = 0;
        }
    }

    public sealed class BaseState
    {
        public BaseRunner? First { get; set; }
        public BaseRunner? Second { get; set; }
        public BaseRunner? Third { get; set; }

        public bool HasRunnerOnFirst => First != null;
        public bool HasRunnerOnSecond => Second != null;
        public bool HasRunnerOnThird => Third != null;
        public bool BasesLoaded => First != null && Second != null && Third != null;

        public void Clear()
        {
            First = null;
            Second = null;
            Third = null;
        }

        public int CountRunners()
        {
            int total = 0;
            if (First != null) total++;
            if (Second != null) total++;
            if (Third != null) total++;
            return total;
        }
    }

    public sealed class BaseRunner
    {
        public Player Player { get; set; }
        public Team Team { get; set; }
        public int BatterIndex { get; set; }
        public Player? CourtesyForPlayer { get; set; }
        public Guid ResponsiblePitcherId { get; set; }
        public bool Earned { get; set; } = true;

        public static BaseRunner FromBatter(GameplayState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            var player = state.CurrentBatter ?? throw new InvalidOperationException("Cannot create a base runner without a current batter.");
            return new BaseRunner
            {
                Player = player,
                Team = state.BattingTeam,
                BatterIndex = state.CurrentBatterIndex
            };
        }
    }

    public sealed class HalfInningSnapshot
    {
        public int Inning { get; set; }
        public HalfInning Half { get; set; }
        public Guid BattingTeamId { get; set; }
        public int RunsScored { get; set; }
        public int AwayScore { get; set; }
        public int HomeScore { get; set; }
    }

    public sealed class GameplayLiveRulesState
    {
        public Guid AwayStarterPitcherId { get; set; }
        public Guid HomeStarterPitcherId { get; set; }
        public Guid? AwayEmergencyPitcherId { get; set; }
        public Guid? HomeEmergencyPitcherId { get; set; }
        public Guid? WinningPitcherCandidateId { get; set; }
        public Guid? LosingPitcherCandidateId { get; set; }
        public int AwayStarterPitchCount { get; set; }
        public int HomeStarterPitchCount { get; set; }
        public int AwayStarterPostLimitBaserunnersThisInning { get; set; }
        public int HomeStarterPostLimitBaserunnersThisInning { get; set; }
        public int AwayMoundVisitsThisInning { get; set; }
        public int HomeMoundVisitsThisInning { get; set; }
        public bool AwayCoachVisitBoostActive { get; set; }
        public bool HomeCoachVisitBoostActive { get; set; }
        public List<Guid> PitchersRemovedByRunRule { get; set; } = new List<Guid>();
        public Dictionary<Guid, GameplayReliefPitcherState> ReliefPitcherFatigue { get; set; } = new Dictionary<Guid, GameplayReliefPitcherState>();
        public Dictionary<Guid, GameplayPitcherRunRuleState> PitcherRunRules { get; set; } = new Dictionary<Guid, GameplayPitcherRunRuleState>();
    }

    public sealed class GameplayReliefPitcherState
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

    public sealed class GameplayPitcherRunRuleState
    {
        public Dictionary<int, int> RunsAllowedByInning { get; set; } = new Dictionary<int, int>();
        public Dictionary<int, int> EarnedRunsAllowedByInning { get; set; } = new Dictionary<int, int>();
        public HashSet<int> FinalizedInnings { get; set; } = new HashSet<int>();
        public int ConsecutiveScorelessInnings { get; set; }
        public int AdvancementBoostPercent { get; set; }
        public bool EarnedRunReductionImmune { get; set; }
    }

    public sealed class PitchOutcome
    {
        public PitchOutcomeType Type { get; set; }
        public AtBatResult AtBatResult { get; set; }

        public static PitchOutcome Ball()
        {
            return new PitchOutcome { Type = PitchOutcomeType.Ball };
        }

        public static PitchOutcome CalledStrike()
        {
            return new PitchOutcome { Type = PitchOutcomeType.CalledStrike };
        }

        public static PitchOutcome SwingingStrike()
        {
            return new PitchOutcome { Type = PitchOutcomeType.SwingingStrike };
        }

        public static PitchOutcome Foul()
        {
            return new PitchOutcome { Type = PitchOutcomeType.Foul };
        }

        public static PitchOutcome InPlay(AtBatResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            return new PitchOutcome { Type = PitchOutcomeType.InPlay, AtBatResult = result };
        }
    }

    public sealed class AtBatResult
    {
        public AtBatResultType Type { get; set; }
        public int BasesAwarded { get; set; }
        public int OutsRecorded { get; set; }
        public bool AdvancesBatter { get; set; } = true;
        public bool AdvanceForcedRunnersOnly { get; set; }
        public bool BatterIsOut { get; set; }

        public static AtBatResult BallInPlayOut()
        {
            return new AtBatResult { Type = AtBatResultType.BallInPlayOut, OutsRecorded = 1, BatterIsOut = true, AdvancesBatter = false };
        }

        public static AtBatResult Strikeout()
        {
            return new AtBatResult { Type = AtBatResultType.Strikeout, OutsRecorded = 1, BatterIsOut = true, AdvancesBatter = false };
        }

        public static AtBatResult Walk()
        {
            return new AtBatResult { Type = AtBatResultType.Walk, BasesAwarded = 1, AdvanceForcedRunnersOnly = true };
        }

        public static AtBatResult Single()
        {
            return new AtBatResult { Type = AtBatResultType.Single, BasesAwarded = 1 };
        }

        public static AtBatResult Double()
        {
            return new AtBatResult { Type = AtBatResultType.Double, BasesAwarded = 2 };
        }

        public static AtBatResult Triple()
        {
            return new AtBatResult { Type = AtBatResultType.Triple, BasesAwarded = 3 };
        }

        public static AtBatResult HomeRun()
        {
            return new AtBatResult { Type = AtBatResultType.HomeRun, BasesAwarded = 4 };
        }

        public static AtBatResult SacrificeFly()
        {
            return new AtBatResult { Type = AtBatResultType.SacrificeFly, OutsRecorded = 1, BasesAwarded = 1, BatterIsOut = true, AdvancesBatter = false };
        }

        public static AtBatResult GroundOut()
        {
            return new AtBatResult { Type = AtBatResultType.GroundOut, OutsRecorded = 1, BatterIsOut = true, AdvancesBatter = false };
        }

        public static AtBatResult DoublePlay()
        {
            return new AtBatResult { Type = AtBatResultType.DoublePlay, OutsRecorded = 2, BatterIsOut = true, AdvancesBatter = false };
        }

        public static AtBatResult Error(int basesAwarded = 1)
        {
            return new AtBatResult { Type = AtBatResultType.Error, BasesAwarded = Math.Max(1, basesAwarded) };
        }

        public static AtBatResult HitByPitch()
        {
            return new AtBatResult { Type = AtBatResultType.HitByPitch, BasesAwarded = 1, AdvanceForcedRunnersOnly = true };
        }
    }

    public sealed class GameplayEvent
    {
        public PitchOutcomeType? PitchType { get; set; }
        public AtBatResultType? AtBatType { get; set; }
        public bool AtBatCompleted { get; set; }
        public bool HalfInningAdvanced { get; set; }
        public bool GameCompleted { get; set; }
        public int RunsScored { get; set; }
        public int OutsRecorded { get; set; }
        public int AwayScore { get; set; }
        public int HomeScore { get; set; }
        public int Inning { get; set; }
        public HalfInning Half { get; set; }
        public int Balls { get; set; }
        public int Strikes { get; set; }
        public int Outs { get; set; }

        internal static GameplayEvent FromState(GameplayState state)
        {
            return new GameplayEvent
            {
                AwayScore = state.AwayScore,
                HomeScore = state.HomeScore,
                Inning = state.Inning,
                Half = state.Half,
                Balls = state.Count.Balls,
                Strikes = state.Count.Strikes,
                Outs = state.Count.Outs,
                GameCompleted = state.IsComplete
            };
        }
    }
}
