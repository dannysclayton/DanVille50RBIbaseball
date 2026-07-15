using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StandaloneBaseball
{
    public sealed class ReplayFile
    {
        public int ReplaySchemaVersion { get; set; }
        public string Source { get; set; } = "";
        public string SourceVersion { get; set; } = "";
        public string ExportedAt { get; set; } = "";
        public bool Deterministic { get; set; }
        public ReplayGameInfo Game { get; set; } = new ReplayGameInfo();
        public ReplayRules Rules { get; set; } = new ReplayRules();
        public ReplayTeams Teams { get; set; } = new ReplayTeams();
        public ReplayAssets Assets { get; set; } = new ReplayAssets();
        public ReplayGameState? StartingState { get; set; }
        public List<ReplayEvent> Events { get; set; } = new List<ReplayEvent>();
        public ReplayGameState? FinalState { get; set; }
        public ReplayValidation Validation { get; set; } = new ReplayValidation();
        public List<string> PlayLog { get; set; } = new List<string>();
        public List<string> DetailedPlayLog { get; set; } = new List<string>();

        [JsonIgnore]
        public string SourceDirectory { get; set; } = "";

        [JsonIgnore]
        public List<string> ReplayIssues { get; set; } = new List<string>();

        [JsonIgnore]
        public bool UsesTimedPlayback => ReplaySchemaVersion >= 2 && Events != null &&
            Events.Exists(replayEvent => replayEvent != null &&
                (replayEvent.TimeMs > 0 || replayEvent.DurationMs > 0 || replayEvent.Animation != null ||
                 replayEvent.Before != null || replayEvent.After != null));

        [JsonIgnore]
        public bool IsExact => ReplaySchemaVersion >= 2 && Deterministic && (ReplayIssues?.Count ?? 0) == 0;

        [JsonIgnore]
        public bool IsBestEffort => UsesTimedPlayback && !IsExact;

        [JsonIgnore]
        public string PlaybackQuality => IsExact ? "Exact" : IsBestEffort ? "Best Effort" : "Snapshot";
    }

    public sealed class ReplayGameInfo
    {
        public int Innings { get; set; }
        public string GameId { get; set; } = "";
        public string SeasonId { get; set; } = "";
        public int SeasonNumber { get; set; }
        public string ScheduledGameId { get; set; } = "";
        public string GameType { get; set; } = "";
        public string PlayoffRoundName { get; set; } = "";
        public string StadiumId { get; set; } = "";
        public string StadiumName { get; set; } = "";
        public string DatePlayed { get; set; } = "";
        public ReplayScore FinalScore { get; set; } = new ReplayScore();
        public ReplayLineScore LineScore { get; set; } = new ReplayLineScore();
        public string WinnerTeamId { get; set; } = "";
    }

    public sealed class ReplayLineScore
    {
        public List<int> AwayRunsByInning { get; set; } = new List<int>();
        public List<int> HomeRunsByInning { get; set; } = new List<int>();
        public int AwayHits { get; set; }
        public int HomeHits { get; set; }
        public int AwayErrors { get; set; }
        public int HomeErrors { get; set; }
        public int AwayLeftOnBase { get; set; }
        public int HomeLeftOnBase { get; set; }
    }

    public sealed class ReplayRules
    {
        public bool MercyRuleEnabled { get; set; }
        public int MercyRuleRuns { get; set; } = 10;
        public int MercyRuleMinimumInning { get; set; } = 5;
        public bool ExtraInningsEnabled { get; set; } = true;
        public bool ExtraInningRunnerOnSecond { get; set; }
        public bool CourtesyRunnerForPitchersCatchers { get; set; }
        public bool DesignatedHitterEnabled { get; set; } = true;
        public bool AutomaticIntentionalWalk { get; set; } = true;
        public bool PitcherFatigueEnabled { get; set; } = true;
        public bool InjuriesEnabled { get; set; } = true;
        public bool BalksEnabled { get; set; } = true;
        public bool WildPitchesPassedBallsEnabled { get; set; } = true;
    }

    public sealed class ReplayAssets
    {
        public string StadiumBackground { get; set; } = "";
        public string ScoreboardTemplate { get; set; } = "";
        public string NationalAnthemImage { get; set; } = "";
        public Dictionary<string, JsonElement> Audio { get; set; } = new Dictionary<string, JsonElement>(System.StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> Cutscenes { get; set; } = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
    }

    public sealed class ReplayTeams
    {
        public ReplayTeam Away { get; set; } = new ReplayTeam();
        public ReplayTeam Home { get; set; } = new ReplayTeam();
    }

    public sealed class ReplayTeam
    {
        public string TeamId { get; set; } = "";
        public string TeamName { get; set; } = "";
        public string Mascot { get; set; } = "";
        public string ScoreboardAbbreviation { get; set; } = "";
        public string LogoPath { get; set; } = "";
        public string PrimaryColor { get; set; } = "";
        public string SecondaryColor { get; set; } = "";
        public string UniformKey { get; set; } = "";
        public TeamScoreboardTemplate? ScoreboardTemplate { get; set; }
        public ReplayRecord RecordBeforeGame { get; set; } = new ReplayRecord();
        public int Score { get; set; }
        public int Hits { get; set; }
        public int Errors { get; set; }
        public List<int> RunsByInning { get; set; } = new List<int>();
        public List<ReplayLineupSlot> Lineup { get; set; } = new List<ReplayLineupSlot>();
        public List<ReplayPlayer> Bench { get; set; } = new List<ReplayPlayer>();
        public List<ReplayPlayer> PitchingStaff { get; set; } = new List<ReplayPlayer>();
    }

    public sealed class ReplayRecord
    {
        public int Wins { get; set; }
        public int Losses { get; set; }
        public int Ties { get; set; }
    }

    public sealed class ReplayLineupSlot
    {
        public int Order { get; set; }
        public string Position { get; set; } = "";
        public ReplayPlayer? Player { get; set; }
    }

    public sealed class ReplayPlayer
    {
        public string PlayerId { get; set; } = "";
        public string Name { get; set; } = "";
        public int Number { get; set; }
        public string TeamId { get; set; } = "";
        public string Position { get; set; } = "";
        public List<string> EligiblePositions { get; set; } = new List<string>();
        public string Classification { get; set; } = "";
        public string PlayerType { get; set; } = "";
        public string Handedness { get; set; } = "";
        public string Bats { get; set; } = "";
        public string Throws { get; set; } = "";
        public string Photo { get; set; } = "";
        public string SpriteSheet { get; set; } = "";
        public ReplayPlayerRatings Ratings { get; set; } = new ReplayPlayerRatings();
        public List<ReplayPitchArsenalEntry> PitchArsenal { get; set; } = new List<ReplayPitchArsenalEntry>();
    }

    public sealed class ReplayPlayerRatings
    {
        public int Contact { get; set; } = 55;
        public int Power { get; set; } = 55;
        public int Speed { get; set; } = 55;
        public int BaseRunning { get; set; } = 55;
        public int Pitching { get; set; } = 55;
        public int Stamina { get; set; } = 55;
        public int Fielding { get; set; } = 55;
        public int Throwing { get; set; } = 55;
        public int CatcherBlocking { get; set; }
        public int CatcherArm { get; set; }
    }

    public sealed class ReplayPitchArsenalEntry
    {
        public string PitchType { get; set; } = "";
        public int Effectiveness { get; set; }
    }

    public sealed class ReplayEvent
    {
        public int Sequence { get; set; }
        public string EventId { get; set; } = "";
        public string EventType { get; set; } = "";
        public long TimeMs { get; set; }
        public int DurationMs { get; set; }
        public string Description { get; set; } = "";
        public int Inning { get; set; }
        public string Half { get; set; } = "";
        public int Outs { get; set; }
        public ReplayScore Score { get; set; } = new ReplayScore();
        public ReplayBases Bases { get; set; } = new ReplayBases();
        public int RunsScoredOnPlay { get; set; }
        public List<string> RunnersAdvanced { get; set; } = new List<string>();
        public string OffensiveChoice { get; set; } = "";
        public string DefensiveChoice { get; set; } = "";
        public ReplayGameState? Before { get; set; }
        public ReplayCommand? Command { get; set; }
        public ReplayAnimation? Animation { get; set; }
        public List<ReplayAudioCue> Audio { get; set; } = new List<ReplayAudioCue>();
        public List<ReplayCutsceneCue> Cutscenes { get; set; } = new List<ReplayCutsceneCue>();
        public ReplayAtBatResult? Result { get; set; }
        public List<ReplayRunnerAdvancement> RunnerAdvancements { get; set; } = new List<ReplayRunnerAdvancement>();
        public ReplayGameState? After { get; set; }
        public ReplayValidation Validation { get; set; } = new ReplayValidation();
    }

    public sealed class ReplayGameState
    {
        public long TimeMs { get; set; }
        public int Inning { get; set; } = 1;
        public string Half { get; set; } = "top";
        public int Outs { get; set; }
        public int Balls { get; set; }
        public int Strikes { get; set; }
        public ReplayScore Score { get; set; } = new ReplayScore();
        public ReplayExactBases Bases { get; set; } = new ReplayExactBases();
        public string CurrentBatterId { get; set; } = "";
        public string CurrentPitcherId { get; set; } = "";
        public int AwayBatterIndex { get; set; }
        public int HomeBatterIndex { get; set; }
        public int AwayPitcherIndex { get; set; }
        public int HomePitcherIndex { get; set; }
        public ReplayDhState DhState { get; set; } = new ReplayDhState();
        public ReplayLiveRules LiveRules { get; set; } = new ReplayLiveRules();
        public List<ReplayStateFielder> Fielders { get; set; } = new List<ReplayStateFielder>();
    }

    public sealed class ReplayDhState
    {
        public bool AwayDhActive { get; set; }
        public bool HomeDhActive { get; set; }
        public string AwayDhPlayerId { get; set; } = "";
        public string HomeDhPlayerId { get; set; } = "";
    }

    public sealed class ReplayLiveRules
    {
        public int AwayStarterPitchCount { get; set; }
        public int HomeStarterPitchCount { get; set; }
        public int AwayMoundVisitsThisInning { get; set; }
        public int HomeMoundVisitsThisInning { get; set; }
        public List<string> PitchersRemovedByRunRule { get; set; } = new List<string>();
        public Dictionary<string, int> ReliefPitcherFatigue { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, JsonElement> PitcherRunRules { get; set; } = new Dictionary<string, JsonElement>();
    }

    public sealed class ReplayStateFielder
    {
        public string PlayerId { get; set; } = "";
        public string Position { get; set; } = "";
        public float X { get; set; }
        public float Y { get; set; }
    }

    public sealed class ReplayExactBases
    {
        public ReplayBaseOccupant? First { get; set; }
        public ReplayBaseOccupant? Second { get; set; }
        public ReplayBaseOccupant? Third { get; set; }
    }

    public sealed class ReplayBaseOccupant
    {
        public string PlayerId { get; set; } = "";
        public string ResponsiblePitcherId { get; set; } = "";
        public bool Earned { get; set; } = true;
    }

    public sealed class ReplayCommand
    {
        public ReplayPitchCommand? Pitch { get; set; }
        public ReplayBatterInput? BatterInput { get; set; }
        public ReplayStrategyCommand? Strategy { get; set; }
        public string TeamId { get; set; } = "";
        public string SubstitutionType { get; set; } = "";
        public string OutPlayerId { get; set; } = "";
        public string InPlayerId { get; set; } = "";
        public int BattingOrderSlot { get; set; }
        public string DefensivePosition { get; set; } = "";
        public bool DhLost { get; set; }
        public string Reason { get; set; } = "";
    }

    public sealed class ReplayPitchCommand
    {
        public int PitchNumber { get; set; }
        public string PitcherId { get; set; } = "";
        public string BatterId { get; set; } = "";
        public string PitchType { get; set; } = "";
        public int PitchEffectiveness { get; set; }
        public ReplayZonePoint TargetLocation { get; set; } = new ReplayZonePoint();
        public ReplayZonePoint ActualLocation { get; set; } = new ReplayZonePoint();
        public int VelocityMph { get; set; }
        public int SpinRpm { get; set; }
        public ReplayVector Break { get; set; } = new ReplayVector();
        public long ReleaseTimeMs { get; set; }
        public long PlateTimeMs { get; set; }
        public string CalledZone { get; set; } = "";
        public int PitcherFatigueAdjustmentPercent { get; set; }
    }

    public sealed class ReplayBatterInput
    {
        public bool Swing { get; set; }
        public string SwingType { get; set; } = "";
        public string Timing { get; set; } = "";
        public int TimingOffsetMs { get; set; }
        public ReplayZonePoint Aim { get; set; } = new ReplayZonePoint();
        public int ContactQuality { get; set; }
    }

    public sealed class ReplayStrategyCommand
    {
        public string Offense { get; set; } = "";
        public string Defense { get; set; } = "";
        public string StealDefense { get; set; } = "";
        public string CoachCallQuality { get; set; } = "";
    }

    public sealed class ReplayZonePoint
    {
        public float ZoneX { get; set; }
        public float ZoneY { get; set; }
    }

    public sealed class ReplayVector
    {
        public float X { get; set; }
        public float Y { get; set; }
    }

    public sealed class ReplayAnimation
    {
        public ReplayCamera Camera { get; set; } = new ReplayCamera();
        public List<ReplayPathPoint> BallPath { get; set; } = new List<ReplayPathPoint>();
        public List<ReplayActorPath> FielderPaths { get; set; } = new List<ReplayActorPath>();
        public List<ReplayRunnerPath> RunnerPaths { get; set; } = new List<ReplayRunnerPath>();
        public List<ReplayThrowPath> Throws { get; set; } = new List<ReplayThrowPath>();
        public List<string> HighlightPlayerIds { get; set; } = new List<string>();
        public List<long> ScoreboardUpdatesAtMs { get; set; } = new List<long>();
    }

    public sealed class ReplayCamera
    {
        public string View { get; set; } = "gameplay";
        public float StartZoom { get; set; } = 1f;
        public float EndZoom { get; set; } = 1f;
        public bool Shake { get; set; }
    }

    public sealed class ReplayPathPoint
    {
        public long TimeMs { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public bool Visible { get; set; } = true;
    }

    public class ReplayActorPath
    {
        public string PlayerId { get; set; } = "";
        public string Position { get; set; } = "";
        public List<ReplayPathPoint> Path { get; set; } = new List<ReplayPathPoint>();
    }

    public sealed class ReplayRunnerPath : ReplayActorPath
    {
        public int FromBase { get; set; }
        public int ToBase { get; set; }
        public bool Safe { get; set; }
    }

    public sealed class ReplayThrowPath
    {
        public string FromPlayerId { get; set; } = "";
        public string ToPlayerId { get; set; } = "";
        public long StartTimeMs { get; set; }
        public long CatchTimeMs { get; set; }
        public List<ReplayPathPoint> Path { get; set; } = new List<ReplayPathPoint>();
    }

    public sealed class ReplayAudioCue
    {
        public string Cue { get; set; } = "";
        public string AssetKey { get; set; } = "";
        public string File { get; set; } = "";
        public long StartTimeMs { get; set; }
        public bool Loop { get; set; }
        public bool DuckBackground { get; set; }
    }

    public sealed class ReplayCutsceneCue
    {
        public string Trigger { get; set; } = "";
        public string Level { get; set; } = "";
        public string TeamId { get; set; } = "";
        public string UniformKey { get; set; } = "";
        public string AssetPath { get; set; } = "";
        public long StartTimeMs { get; set; }
        public int DurationMs { get; set; }
        public bool Blocking { get; set; }
    }

    public sealed class ReplayRunnerAdvancement
    {
        public string PlayerId { get; set; } = "";
        public int FromBase { get; set; }
        public int ToBase { get; set; }
        public bool Scored { get; set; }
        public bool Out { get; set; }
        public bool Safe { get; set; }
        public string Reason { get; set; } = "";
        public string ResponsiblePitcherId { get; set; } = "";
        public bool Earned { get; set; } = true;
    }

    public sealed class ReplayValidation
    {
        public string StateHashBefore { get; set; } = "";
        public string StateHashAfter { get; set; } = "";
        public ReplayScore? ScoreAfter { get; set; }
        public int? OutsAfter { get; set; }
        public ReplayValidationBases? BasesAfter { get; set; }
        public Dictionary<string, int> PitchCountAfter { get; set; } = new Dictionary<string, int>();
    }

    public sealed class ReplayValidationBases
    {
        public string FirstPlayerId { get; set; } = "";
        public string SecondPlayerId { get; set; } = "";
        public string ThirdPlayerId { get; set; } = "";
    }

    internal sealed class ReplayRenderFrame
    {
        public bool BallVisible { get; set; }
        public float BallX { get; set; } = 0.5f;
        public float BallY { get; set; } = 0.62f;
        public float BallZ { get; set; }
        public float Progress { get; set; }
        public List<ReplayRenderActor> Actors { get; } = new List<ReplayRenderActor>();
    }

    internal sealed class ReplayRenderActor
    {
        public string PlayerId { get; set; } = "";
        public string DefensivePosition { get; set; } = "";
        public float X { get; set; }
        public float Y { get; set; }
        public bool Runner { get; set; }
        public bool Highlighted { get; set; }
        public Player? Player { get; set; }
        public Team? Team { get; set; }
    }

    public sealed class ReplayScore
    {
        public int Away { get; set; }
        public int Home { get; set; }
    }

    public sealed class ReplayBases
    {
        public ReplayPlayer? First { get; set; }
        public ReplayPlayer? Second { get; set; }
        public ReplayPlayer? Third { get; set; }
    }

    public sealed class ReplayAtBatResult
    {
        public string Outcome { get; set; } = "";
        public ReplayPlayer? Batter { get; set; }
        public ReplayPlayer? Pitcher { get; set; }
        public int BatterRoll { get; set; }
        public int PitcherRoll { get; set; }
        public string Winner { get; set; } = "";
        public string ChartTier { get; set; } = "";
        public int ChartD1 { get; set; }
        public int ChartD2 { get; set; }
        public int ChartRoll { get; set; }
        public string ChartId { get; set; } = "";
        public string ChartName { get; set; } = "";
        public int PitchCount { get; set; }
        public bool ErrorOnPlay { get; set; }
        public string Description { get; set; } = "";
        public string DetailedDescription { get; set; } = "";
        public string NarrationText { get; set; } = "";
        public int RunsScoredOnPlay { get; set; }
        public List<string> RbiPlayerIds { get; set; } = new List<string>();
        public bool EarnedRun { get; set; }
        public ReplayPlayer? Fielder { get; set; }
        public List<string> AssistPlayerIds { get; set; } = new List<string>();
        public string PutoutPlayerId { get; set; } = "";
        public string ErrorPlayerId { get; set; } = "";
        public string WinningPitcherId { get; set; } = "";
        public string LosingPitcherId { get; set; } = "";
        public string SavePitcherId { get; set; } = "";
    }
}
