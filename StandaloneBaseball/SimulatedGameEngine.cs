using System;
using System.Collections.Generic;
using System.Linq;

#nullable enable annotations

namespace StandaloneBaseball
{
    internal static class SimulatedGameEngine
    {
        private const int SeniorStarterPitchLimitFallback = 100;
        private const int ReliefPitcherMaxOuts = 6;

        public static GameResult Simulate(LeagueFile? league, Team away, Team home, Random? rng, RankingGameModifier? rankingModifier = null)
            => SimulateDetailed(league, away, home, rng, rankingModifier).Result;

        public static SimulatedGameRun SimulateDetailed(LeagueFile? league, Team away, Team home, Random? rng, RankingGameModifier? rankingModifier = null)
        {
            if (away == null) throw new ArgumentNullException(nameof(away));
            if (home == null) throw new ArgumentNullException(nameof(home));
            rng ??= new Random();

            var rules = league?.Rules ?? new LeagueRules();
            var game = new EngineGame(league, away, home, rules, rng, rankingModifier ?? RankingGameModifier.None);
            return game.Play();
        }

        public sealed class SimulatedGameRun
        {
            public GameResult Result { get; set; }
            public List<SimulatedGameEvent> Events { get; set; } = new List<SimulatedGameEvent>();
        }

        public sealed class SimulatedGameEvent
        {
            public int Inning { get; set; }
            public bool TopHalf { get; set; }
            public int Outs { get; set; }
            public int AwayScore { get; set; }
            public int HomeScore { get; set; }
            public string Bases { get; set; } = "";
            public string Narration { get; set; } = "";
        }

        private sealed class EngineGame
        {
            private static readonly Coach DefaultDecisionCoach = new Coach
            {
                Style = CoachStyle.Average,
                Strategy = CoachStrategy.Conservative
            };
            private static readonly Player DefaultDefensivePlayer = new Player
            {
                Name = "Replacement Defender",
                Fielding = 50,
                ArmStrength = 50,
                Accuracy = 50,
                TagRating = 50
            };

            private readonly Team _away;
            private readonly Team _home;
            private readonly LeagueFile? _league;
            private readonly LeagueRules _rules;
            private readonly Random _rng;
            private readonly RankingGameModifier _rankingModifier;
            private readonly GameResult _result;
            private readonly List<SimulatedGameEvent> _events = new List<SimulatedGameEvent>();
            private readonly List<int> _awayRunsByInning = new List<int>();
            private readonly List<int> _homeRunsByInning = new List<int>();
            private readonly Dictionary<string, PlayerGameLine> _lines = new Dictionary<string, PlayerGameLine>();
            private readonly List<Player> _awayLineup;
            private readonly List<Player> _homeLineup;
            private readonly List<Player> _awayPitchers;
            private readonly List<Player> _homePitchers;
            private readonly List<PitcherUse> _awayUsedPitchers = new List<PitcherUse>();
            private readonly List<PitcherUse> _homeUsedPitchers = new List<PitcherUse>();
            private readonly BaseSlot[] _bases = { new BaseSlot(), new BaseSlot(), new BaseSlot() };
            private int _awayBatterIndex;
            private int _homeBatterIndex;
            private PitcherUse _awayPitcher;
            private PitcherUse _homePitcher;
            private int _inning = 1;
            private bool _topHalf = true;
            private int _outs;
            private int _awayLeftOnBase;
            private int _homeLeftOnBase;
            private Guid? _winningPitcherCandidateId;
            private Guid? _losingPitcherCandidateId;

            public EngineGame(LeagueFile? league, Team away, Team home, LeagueRules rules, Random rng, RankingGameModifier rankingModifier)
            {
                _league = league;
                _away = away;
                _home = home;
                _rules = rules ?? new LeagueRules();
                _rng = rng;
                _rankingModifier = rankingModifier ?? RankingGameModifier.None;
                _awayLineup = BuildLineup(away);
                _homeLineup = BuildLineup(home);
                _awayPitchers = BuildPitchingStaff(away);
                _homePitchers = BuildPitchingStaff(home);
                _awayPitcher = CreatePitcherUse(away, _awayPitchers.FirstOrDefault(), starter: true);
                _homePitcher = CreatePitcherUse(home, _homePitchers.FirstOrDefault(), starter: true);
                _awayUsedPitchers.Add(_awayPitcher);
                _homeUsedPitchers.Add(_homePitcher);
                var homeField = _league?.CustomFields?.FirstOrDefault(field => string.Equals(field.Id, home.HomeFieldPresetId, StringComparison.OrdinalIgnoreCase));
                _result = new GameResult
                {
                    PlayedAt = DateTime.Now,
                    AwayTeamId = away.Id,
                    HomeTeamId = home.Id,
                    AwayCoachId = away.CoachId,
                    HomeCoachId = home.CoachId,
                    GameType = "Regular Season",
                    GameMode = "Simulation",
                    StadiumId = home.HomeFieldPresetId ?? "",
                    StadiumName = homeField?.Name ?? home.HomeFieldPresetId ?? "",
                    RegulationInnings = Math.Clamp(_rules.Innings, 5, 9),
                    ExtraInningsEnabled = _rules.ExtraInnings,
                    ExtraInningRunnerOnSecond = _rules.ExtraInningRunnerOnSecond,
                    MercyRuleEnabled = _rules.MercyRuleEnabled,
                    MercyRuleRuns = _rules.MercyRuleRuns,
                    MercyRuleMinimumInning = _rules.MercyRuleMinimumInning,
                    AwayStartingLineup = LineupEngine.CaptureStartingLineup(away),
                    HomeStartingLineup = LineupEngine.CaptureStartingLineup(home)
                };
            }

            public SimulatedGameRun Play()
            {
                int regulationInnings = Math.Clamp(_rules.Innings, 5, 9);
                bool complete = false;
                while (!complete)
                {
                    _topHalf = true;
                    Log("Top " + _inning + ": " + _away.ScoreboardName + " batting.");
                    int awayBefore = _result.AwayScore;
                    if (!PlayHalfInning())
                    {
                        Log("All-Star Game ends tied: " + DefenseTeam.ScoreboardName + " has no remaining pitcher.");
                        break;
                    }
                    RecordCompletedHalfInning(_awayRunsByInning, _result.AwayScore - awayBefore, ref _awayLeftOnBase);
                    bool topMercy = IsMercyRuleComplete(completedTop: true);
                    complete = IsCompleteAfterTop(regulationInnings) || topMercy;
                    if (topMercy)
                        _result.EndedByMercyRule = true;
                    if (complete)
                        break;

                    _topHalf = false;
                    Log("Bottom " + _inning + ": " + _home.ScoreboardName + " batting.");
                    int homeBefore = _result.HomeScore;
                    if (!PlayHalfInning())
                    {
                        Log("All-Star Game ends tied: " + DefenseTeam.ScoreboardName + " has no remaining pitcher.");
                        break;
                    }
                    RecordCompletedHalfInning(_homeRunsByInning, _result.HomeScore - homeBefore, ref _homeLeftOnBase);
                    bool bottomMercy = IsMercyRuleComplete(completedTop: false);
                    complete = IsCompleteAfterBottom(regulationInnings) || bottomMercy;
                    if (bottomMercy)
                        _result.EndedByMercyRule = true;
                    if (!complete)
                        _inning++;

                    if (_inning > 30)
                        complete = true;
                }

                _result.Lines = _lines.Values.Where(HasStats).ToList();
                AssignDecisions();
                Log("Final: " + _away.ScoreboardName + " " + _result.AwayScore + ", " + _home.ScoreboardName + " " + _result.HomeScore + ".");
                FinalizeResult();
                return new SimulatedGameRun { Result = _result, Events = _events };
            }

            private void RecordCompletedHalfInning(List<int> inningRuns, int runsScored, ref int leftOnBase)
            {
                inningRuns.Add(Math.Max(0, runsScored));
                leftOnBase += OccupiedBases().Count();
                RegisterParticipation(DefensivePlayer("C"), DefenseTeam, InjuryExposureType.CatcherInning);
            }

            private bool PlayHalfInning()
            {
                if (!ApplyAllStarPitcherForHalfInning())
                    return false;

                _outs = 0;
                ClearBases();
                if (_inning > Math.Clamp(_rules.Innings, 5, 9) && _rules.ExtraInnings && _rules.ExtraInningRunnerOnSecond)
                    PlaceExtraInningRunner();

                CurrentPitcher().ResetHalfInning();
                int plateAppearances = 0;
                while (_outs < 3 && !IsWalkOff() && plateAppearances < 80)
                {
                    TryStealBeforePitch();
                    if (_outs >= 3 || IsWalkOff())
                        break;

                    ResolvePlateAppearance();
                    plateAppearances++;
                    MaybeChangePitcher();
                }
                return true;
            }

            private bool ApplyAllStarPitcherForHalfInning()
            {
                Team team = DefenseTeam;
                if (team?.PitchingPlan?.UseAllStarPitchingRules != true)
                    return true;

                int index = PitchingRotationEngine.AllStarPitcherIndexForInning(team, _inning);
                if (index < 0)
                    return !(_inning > Math.Clamp(_rules.Innings, 5, 9) && _result.AwayScore == _result.HomeScore);

                var staff = team.Id == _away.Id ? _awayPitchers : _homePitchers;
                var player = staff.ElementAtOrDefault(index);
                if (player == null)
                    return true;

                var current = CurrentPitcher();
                if (current?.Player?.Id == player.Id)
                    return true;

                var used = team.Id == _away.Id ? _awayUsedPitchers : _homeUsedPitchers;
                var use = used.FirstOrDefault(p => p.Player?.Id == player.Id);
                if (use == null)
                {
                    use = CreatePitcherUse(team, player, starter: _inning <= 5);
                    used.Add(use);
                }

                if (team.Id == _away.Id)
                    _awayPitcher = use;
                else
                    _homePitcher = use;
                Log(team.ScoreboardName + " All-Star pitcher: " + player.Name + " for inning " + _inning + ".");
                return true;
            }

            private void ResolvePlateAppearance()
            {
                Player? batter = CurrentBatter();
                if (batter == null)
                {
                    _outs = 3;
                    Log(BattingTeam.ScoreboardName + " has no available batter.");
                    return;
                }

                PitcherUse pitcherUse = CurrentPitcher();
                Player pitcher = pitcherUse.Player;
                int runsBefore = BattingScore;

                if (ShouldCallSacrificeBunt())
                {
                    if (TryResolveBalk(pitcherUse))
                        return;
                    CountPitch(pitcherUse);
                    ResolveSacrificeBunt(batter, pitcherUse);
                    RegisterParticipation(batter, BattingTeam, InjuryExposureType.PlateAppearance);
                    AdvanceBatter();
                    FinalizePlateAppearance(pitcherUse, BattingScore - runsBefore);
                    return;
                }

                int pitchAdj = PitcherAdjustment(pitcherUse);
                bool hitAndRun = ShouldCallHitAndRun(batter);
                int hitAndRunExecution = hitAndRun
                    ? CoachDecisionEngine.StrategyExecutionModifier(DecisionCoachForTeam(BattingTeam), IsHitAndRunSound(batter))
                    : 0;
                int balls = 0;
                int strikes = 0;
                bool plateAppearanceResolved = false;
                bool advanceBatterAfterPlateAppearance = true;
                for (int pitchNumber = 0; pitchNumber < 16; pitchNumber++)
                {
                    if (TryResolveBalk(pitcherUse))
                    {
                        if (IsWalkOff())
                            return;
                        pitchNumber--;
                        continue;
                    }

                    CountPitch(pitcherUse);
                    var pitch = GameplayCpu.ChoosePitch(
                        _rng,
                        pitcher,
                        batter,
                        balls,
                        strikes,
                        _bases[0].Occupied,
                        _bases[1].Occupied,
                        _bases[2].Occupied,
                        _outs,
                        pitcherUse.PitchCount,
                        GameplayCpu.CpuMode.CpuVsCpuWatch);
                    var swing = GameplayCpu.DecideSwing(
                        _rng,
                        batter,
                        pitcher,
                        pitch.PitchType,
                        pitch.AimX,
                        pitch.AimY,
                        balls,
                        strikes,
                        180,
                        hitAndRun);
                    SharedSwingType swingType = !swing.ShouldSwing
                        ? SharedSwingType.Take
                        : swing.SwingType == GameplayCpu.SwingType.Power
                            ? SharedSwingType.Power
                            : swing.SwingType == GameplayCpu.SwingType.Contact
                                ? SharedSwingType.Contact
                                : SharedSwingType.Normal;
                    var resolution = SharedGameEngine.ResolvePitch(_rng, new SharedPitchRequest
                    {
                        Batter = batter,
                        Pitcher = pitcher,
                        PitchType = MapPitchType(pitch.PitchType),
                        SwingType = swingType,
                        PitchX = pitch.AimX,
                        PitchY = pitch.AimY,
                        TimingQuality = Math.Clamp(1.0 - Math.Abs(swing.TimingOffsetMs) / 120.0, 0.05, 1.0),
                        Balls = balls,
                        Strikes = strikes,
                        PitcherAdjustmentPercent = pitchAdj,
                        OffensiveStrategyModifier = hitAndRunExecution,
                        BatterBoostPercent = _rankingModifier.BoostForTeam(BattingTeam),
                        PitcherBoostPercent = _rankingModifier.BoostForTeam(DefenseTeam)
                    });

                    if (resolution.ResultType == SharedPitchResultType.HitByPitch)
                    {
                        ResolveHitByPitch(batter, pitcherUse);
                        plateAppearanceResolved = true;
                        break;
                    }
                    if (resolution.ResultType == SharedPitchResultType.Ball)
                    {
                        if (++balls < 4)
                        {
                            if (TryResolvePitchEscape(pitcherUse, pitch, pitchAdj, out bool thirdOut) && thirdOut)
                            {
                                plateAppearanceResolved = true;
                                advanceBatterAfterPlateAppearance = false;
                                break;
                            }
                            continue;
                        }
                        ResolveWalk(batter, pitcherUse);
                        plateAppearanceResolved = true;
                        break;
                    }
                    if (resolution.ResultType == SharedPitchResultType.CalledStrike || resolution.ResultType == SharedPitchResultType.SwingingStrike)
                    {
                        if (++strikes < 3)
                        {
                            if (TryResolvePitchEscape(pitcherUse, pitch, pitchAdj, out bool thirdOut) && thirdOut)
                            {
                                plateAppearanceResolved = true;
                                advanceBatterAfterPlateAppearance = false;
                                break;
                            }
                            continue;
                        }
                        ResolveStrikeout(batter, pitcherUse);
                        plateAppearanceResolved = true;
                        break;
                    }
                    if (resolution.ResultType == SharedPitchResultType.Foul)
                    {
                        if (strikes < 2) strikes++;
                        continue;
                    }

                    var batted = SharedGameEngine.ResolveBattedBall(_rng, new SharedBattedBallRequest
                    {
                        Batter = batter,
                        Pitcher = pitcher,
                        PitchType = MapPitchType(pitch.PitchType),
                        ContactQuality = resolution.ContactQuality,
                        PitcherAdjustmentPercent = pitchAdj,
                        BatterBoostPercent = _rankingModifier.BoostForTeam(BattingTeam),
                        PitcherBoostPercent = _rankingModifier.BoostForTeam(DefenseTeam),
                        DefenseFieldingRating = TeamFielding(DefenseTeam),
                        SafeApproach = HeadCoachForTeam(BattingTeam)?.Strategy == CoachStrategy.Safe,
                        NoDoublesDefense = ShouldUseNoDoublesDefense(),
                        OutfieldIn = ShouldUseOutfieldIn()
                    });
                    if (batted == SharedBattedBallResultType.Error)
                        ResolveError(batter, pitcherUse);
                    else if (batted == SharedBattedBallResultType.Out)
                        ResolveBallInPlayOut(batter, pitcherUse);
                    else
                        ResolveHit(batter, pitcherUse, MapHitType(batted), hitAndRun);
                    plateAppearanceResolved = true;
                    break;
                }

                if (!plateAppearanceResolved)
                    ResolveBallInPlayOut(batter, pitcherUse);

                RegisterParticipation(batter, BattingTeam, InjuryExposureType.PlateAppearance);
                if (advanceBatterAfterPlateAppearance)
                    AdvanceBatter();
                FinalizePlateAppearance(pitcherUse, BattingScore - runsBefore);
            }

            private void CountPitch(PitcherUse pitcherUse)
            {
                pitcherUse.PitchCount++;
                PitcherLine(pitcherUse).PitchCount++;
                RegisterParticipation(pitcherUse.Player, pitcherUse.Team, InjuryExposureType.PitchThrown);
            }

            private void RegisterParticipation(Player? player, Team? team, InjuryExposureType exposure)
                => InjuryEngine.TryParticipationInjury(player, team, _rng, exposure);

            private bool TryResolveBalk(PitcherUse pitcherUse)
            {
                if (pitcherUse?.Player == null || !_bases.Any(baseState => baseState.Occupied))
                    return false;

                BaseSlot? leadRunner = LeadRunner();
                BaseSlot? stealCandidate = LeadStealCandidate();
                DefensiveStealCall call = stealCandidate?.Runner == null
                    ? DefensiveStealCall.Normal
                    : ChooseCpuStealDefense(pitcherUse.Player, DefensivePlayer("C"), stealCandidate.Runner, stealCandidate.BaseNumber);
                bool stealThreat = leadRunner?.Runner != null &&
                    (Rating(leadRunner.Runner, p => p.Speed, 50) + Rating(leadRunner.Runner, p => p.StealAggression, 50)) / 2 >= 62;
                bool highPressure = _inning >= Math.Max(1, Math.Clamp(_rules.Innings, 5, 9) - 1) &&
                    Math.Abs(_result.AwayScore - _result.HomeScore) <= 2;
                BalkResult result = BalkEngine.Roll(
                    _rng,
                    pitcherUse.Player,
                    PitcherAdjustment(pitcherUse),
                    call,
                    0,
                    _bases[2].Occupied,
                    stealThreat,
                    highPressure);
                if (!result.IsBalk)
                    return false;

                PitcherLine(pitcherUse).Balks++;
                var runners = OccupiedBases().OrderByDescending(baseState => baseState.BaseNumber).ToList();
                ClearBases();
                foreach (BaseSlot runner in runners)
                {
                    if (runner.Runner is not Player runnerPlayer)
                        continue;

                    int targetBase = runner.BaseNumber + 1;
                    if (targetBase >= 4)
                    {
                        ScoreRunner(runner);
                        BatterLine(runnerPlayer).R++;
                        Log("Balk: " + runnerPlayer.Name + " scores.");
                    }
                    else
                    {
                        PutRunnerOnBase(runnerPlayer, targetBase, runner.Earned, runner.ResponsiblePitcherId);
                    }
                }

                Log("Balk charged to " + pitcherUse.Player.Name + " (" + result.Reason + "); all runners advance one base.");
                return true;
            }

            private static GameplayPitchType MapPitchType(GameplayCpu.PitchType pitchType)
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

            private static LiveHitType MapHitType(SharedBattedBallResultType result)
                => result switch
                {
                    SharedBattedBallResultType.Double => LiveHitType.Double,
                    SharedBattedBallResultType.Triple => LiveHitType.Triple,
                    SharedBattedBallResultType.HomeRun => LiveHitType.HomeRun,
                    _ => LiveHitType.Single
                };

            private void ResolveSacrificeBunt(Player batter, PitcherUse pitcherUse)
            {
                Player fielder = DefensivePlayer("3B") ?? DefensivePlayer("P") ?? pitcherUse.Player;
                RegisterParticipation(fielder, DefenseTeam, InjuryExposureType.FieldingPlay);
                int defense = PositionFieldingRating(fielder, "3B") + Rating(fielder, p => p.ArmStrength, 50) / 2 + BuntDefenseCoachModifier() + _rng.Next(-18, 19);
                int bunt = Rating(batter, p => p.Contact, 50) + Rating(batter, p => p.Speed, 50) / 2 + Rating(batter, p => p.BaseRunning, 50) / 2 +
                    CoachDecisionEngine.StrategyExecutionModifier(DecisionCoachForTeam(BattingTeam), IsSacrificeBuntSound()) + _rng.Next(-18, 19);
                var batterLine = BatterLine(batter);
                var pitcherLine = PitcherLine(pitcherUse);
                pitcherLine.BattersFaced++;

                if (_rng.Next(100) < 7)
                {
                    batterLine.AB++;
                    batterLine.SO++;
                    pitcherLine.K++;
                    RecordOut(pitcherUse, "SO");
                    Log(batter.Name + " fouls off a bunt with two strikes. Strikeout.");
                    return;
                }

                var leadRunner = LeadRunner();
                if (leadRunner != null && defense > bunt + 18)
                {
                    RegisterParticipation(leadRunner.Runner, BattingTeam, InjuryExposureType.Collision);
                    RegisterParticipation(DefensivePlayerForBase(leadRunner.BaseNumber + 1), DefenseTeam, InjuryExposureType.Collision);
                    batterLine.AB++;
                    ClearBase(leadRunner.BaseNumber);
                    PutBatterOnBase(batter, 1, earned: true);
                    RecordOut(pitcherUse, "FC");
                    RegisterPitcherBaserunner(pitcherUse, fatigueEligible: true);
                    Log(batter.Name + " bunts. The defense gets the lead runner.");
                    return;
                }

                int runs = AdvanceRunnersOnBases(1, batter, batterSafe: false, earned: true);
                batterLine.SH++;
                batterLine.RBI += runs;
                RecordOut(pitcherUse, "SH");
                Log(batter.Name + " lays down a sacrifice bunt" + (runs > 0 ? " and a run scores." : "."));
            }

            private void ResolveHitByPitch(Player batter, PitcherUse pitcherUse)
            {
                var batterLine = BatterLine(batter);
                var pitcherLine = PitcherLine(pitcherUse);
                pitcherLine.BattersFaced++;
                batterLine.HBP++;
                pitcherLine.HitBatters++;
                InjuryEngine.TryEventInjury(batter, _rng, 28);
                int runs = ForceAdvance(batter, earned: true);
                batterLine.RBI += runs;
                RegisterPitcherBaserunner(pitcherUse, fatigueEligible: true);
                Log(batter.Name + " is hit by a pitch" + (runs > 0 ? " and a run scores." : "."));
            }

            private void ResolveWalk(Player batter, PitcherUse pitcherUse)
            {
                var batterLine = BatterLine(batter);
                var pitcherLine = PitcherLine(pitcherUse);
                pitcherLine.BattersFaced++;
                batterLine.BB++;
                pitcherLine.WalksAllowed++;
                int runs = ForceAdvance(batter, earned: true);
                batterLine.RBI += runs;
                RegisterPitcherBaserunner(pitcherUse, fatigueEligible: true);
                Log(batter.Name + " walks" + (runs > 0 ? " and forces in a run." : "."));
            }

            private void ResolveStrikeout(Player batter, PitcherUse pitcherUse)
            {
                var batterLine = BatterLine(batter);
                var pitcherLine = PitcherLine(pitcherUse);
                pitcherLine.BattersFaced++;
                batterLine.AB++;
                batterLine.SO++;
                pitcherLine.K++;
                RecordOut(pitcherUse, "SO");
                Log(batter.Name + " strikes out.");
            }

            private void ResolveHit(Player batter, PitcherUse pitcherUse, LiveHitType hitType, bool hitAndRun)
            {
                var batterLine = BatterLine(batter);
                var pitcherLine = PitcherLine(pitcherUse);
                pitcherLine.BattersFaced++;
                batterLine.AB++;
                batterLine.H++;
                pitcherLine.HitsAllowed++;

                int bases = BasesForHit(hitType);
                if (hitType == LiveHitType.Double)
                {
                    batterLine.Doubles++;
                    pitcherLine.DoublesAllowed++;
                }
                else if (hitType == LiveHitType.Triple)
                {
                    batterLine.Triples++;
                    pitcherLine.TriplesAllowed++;
                }
                else if (hitType == LiveHitType.HomeRun)
                {
                    batterLine.HR++;
                    pitcherLine.HomeRunsAllowed++;
                }

                int runs = hitType == LiveHitType.HomeRun
                    ? AdvanceHomeRun(batter)
                    : AdvanceRunnersOnHit(batter, bases, hitAndRun);
                batterLine.RBI += runs;
                RegisterPitcherBaserunner(pitcherUse, fatigueEligible: true);
                Log(batter.Name + " hits " + HitText(hitType) + (runs > 0 ? " and drives in " + runs + "." : "."));
            }

            private void ResolveError(Player batter, PitcherUse pitcherUse)
            {
                var batterLine = BatterLine(batter);
                var pitcherLine = PitcherLine(pitcherUse);
                pitcherLine.BattersFaced++;
                batterLine.AB++;
                batterLine.ReachedOnError++;
                var fielder = RandomDefender();
                if (fielder != null)
                {
                    RegisterParticipation(fielder, DefenseTeam, InjuryExposureType.FieldingPlay);
                    BatterLine(fielder, DefenseTeam).Errors++;
                    FieldingDevelopmentEngine.ApplyError(fielder);
                }
                AdvanceRunnersOnBases(1, batter, batterSafe: true, earned: false);
                RegisterPitcherBaserunner(pitcherUse, fatigueEligible: false);
                Log(batter.Name + " reaches on an error by " + (fielder?.Name ?? "the defense") + ".");
            }

            private void ResolveBallInPlayOut(Player batter, PitcherUse pitcherUse)
            {
                var batterLine = BatterLine(batter);
                var pitcherLine = PitcherLine(pitcherUse);
                pitcherLine.BattersFaced++;

                bool outfieldFly = _rng.Next(100) < 34;
                bool pop = !outfieldFly && _rng.Next(100) < 18;
                Player? primaryFielder = outfieldFly
                    ? DefensivePlayer(_rng.Next(3) switch { 0 => "LF", 1 => "CF", _ => "RF" })
                    : pop ? DefensivePlayer("C") : DefensivePlayer(_rng.Next(2) == 0 ? "SS" : "2B");
                if (outfieldFly && _outs < 2 && _bases[2].Occupied && _bases[2].Runner is Player runner)
                {
                    int runnerScore = Rating(runner, p => p.Speed, 50) + Rating(runner, p => p.BaseRunning, 50) + _rng.Next(-16, 17);
                    int arm = Rating(DefensivePlayer("RF") ?? DefensivePlayer("CF") ?? DefensivePlayer("LF"), p => p.ArmStrength, 50) + _rng.Next(-12, 13);
                    if (runnerScore > arm + 12)
                    {
                        var scoringRunner = _bases[2].Copy();
                        ClearBase(3);
                        ScoreRunner(scoringRunner);
                        BatterLine(runner).R++;
                        batterLine.SF++;
                        batterLine.FlyOuts++;
                        batterLine.RBI++;
                        RecordOut(pitcherUse, "SF");
                        if (primaryFielder != null)
                        {
                            RegisterParticipation(primaryFielder, DefenseTeam, InjuryExposureType.FieldingPlay);
                            BatterLine(primaryFielder, DefenseTeam).Putouts++;
                            FieldingDevelopmentEngine.RegisterCleanChance(primaryFielder);
                        }
                        Log(batter.Name + " lifts a sacrifice fly. " + runner.Name + " tags and scores.");
                        return;
                    }
                }

                batterLine.AB++;
                if (outfieldFly)
                    batterLine.FlyOuts++;
                else if (pop)
                    batterLine.PopOuts++;
                else
                    batterLine.GroundOuts++;

                if (!outfieldFly && _outs < 2 && _bases[0].Occupied && _rng.Next(100) < 16)
                {
                    batterLine.GroundedIntoDoublePlays++;
                    ClearBase(1);
                    RecordOut(pitcherUse, "GO");
                    if (_outs < 3)
                        RecordOut(pitcherUse, "DP");
                    if (primaryFielder != null)
                    {
                        RegisterParticipation(primaryFielder, DefenseTeam, InjuryExposureType.FieldingPlay);
                        var primaryLine = BatterLine(primaryFielder, DefenseTeam);
                        primaryLine.Assists++;
                        primaryLine.DefensiveDoublePlays++;
                        primaryLine.TeamDoublePlaysTurned++;
                        FieldingDevelopmentEngine.RegisterCleanChance(primaryFielder);
                    }
                    var first = DefensivePlayer("1B");
                    if (first != null)
                    {
                        RegisterParticipation(first, DefenseTeam, InjuryExposureType.FieldingPlay);
                        var firstLine = BatterLine(first, DefenseTeam);
                        firstLine.Putouts++;
                        if (first.Id != primaryFielder?.Id)
                            firstLine.DefensiveDoublePlays++;
                    }
                    Log(batter.Name + " grounds into a double play.");
                    return;
                }

                RecordOut(pitcherUse, outfieldFly ? "FO" : pop ? "PO" : "GO");
                if (primaryFielder != null)
                {
                    RegisterParticipation(primaryFielder, DefenseTeam, InjuryExposureType.FieldingPlay);
                    var fieldLine = BatterLine(primaryFielder, DefenseTeam);
                    if (outfieldFly || pop) fieldLine.Putouts++;
                    else fieldLine.Assists++;
                    FieldingDevelopmentEngine.RegisterCleanChance(primaryFielder);
                }
                if (!outfieldFly && !pop)
                {
                    var first = DefensivePlayer("1B");
                    if (first != null)
                    {
                        RegisterParticipation(first, DefenseTeam, InjuryExposureType.FieldingPlay);
                        BatterLine(first, DefenseTeam).Putouts++;
                    }
                }
                Log(batter.Name + (outfieldFly ? " flies out." : pop ? " pops out." : " grounds out."));
            }

            private void TryStealBeforePitch()
            {
                if (TryDoubleStealBeforePitch())
                    return;

                var candidate = LeadStealCandidate();
                if (candidate?.Runner is not Player runner)
                    return;

                Player? catcher = DefensivePlayer("C");
                int scoreDifferential = BattingScore - FieldingScore;
                bool nextOccupied = candidate.BaseNumber < 3 && _bases[candidate.BaseNumber].Occupied;
                if (nextOccupied)
                    return;

                bool rightStealCall = StealEngine.ShouldCpuAttemptSteal(_rng, runner, CurrentPitcher().Player, catcher, candidate.BaseNumber, _outs, 0, 0, scoreDifferential, nextOccupied);
                if (!CoachDecisionEngine.ShouldCallRiskyOffense(
                        _rng,
                        HeadCoachForTeam(BattingTeam),
                        rightStealCall,
                        IsGameOnLineForBattingTeam(),
                        candidate.BaseNumber >= 2 || IsGameOnLineForBattingTeam()))
                {
                    return;
                }

                var call = ChooseCpuStealDefense(CurrentPitcher().Player, catcher, runner, candidate.BaseNumber);
                var result = StealEngine.Resolve(_rng, runner, CurrentPitcher().Player, catcher, DefensivePlayerForBase(candidate.BaseNumber + 1), candidate.BaseNumber, _outs, 0, 0, scoreDifferential, call);
                Player? tagFielder = DefensivePlayerForBase(candidate.BaseNumber + 1);
                RegisterStealExposure(runner, catcher, tagFielder, result);
                var runnerLine = BatterLine(runner);
                var catcherLine = catcher == null ? null : BatterLine(catcher, DefenseTeam);
                if (result.SuccessfulSteal)
                {
                    runnerLine.SB++;
                    if (catcherLine != null)
                        catcherLine.StolenBasesAllowed++;
                    ClearBase(candidate.BaseNumber);
                    if (result.FinalBase >= 4)
                    {
                        ScoreRunner(candidate);
                        runnerLine.R++;
                        Log(runner.Name + " steals home.");
                    }
                    else
                    {
                        PutRunnerOnBase(runner, result.FinalBase, earned: candidate.Earned, candidate.ResponsiblePitcherId);
                        Log(runner.Name + " steals " + BaseLabel(result.FinalBase) + ".");
                    }
                }
                else if (result.RunnerOut)
                {
                    runnerLine.CS++;
                    if (catcherLine != null && result.Outcome == StealAttemptOutcome.CaughtStealing)
                        catcherLine.CatcherCaughtStealing++;
                    ClearBase(candidate.BaseNumber);
                    RecordOut(CurrentPitcher(), "CS");
                    Log(runner.Name + " is caught stealing.");
                }
            }

            private bool TryDoubleStealBeforePitch()
            {
                var candidates = DoubleStealCandidates();
                if (candidates.Count < 2)
                    return false;

                Player? catcher = DefensivePlayer("C");
                int scoreDifferential = BattingScore - FieldingScore;
                bool shouldAttempt = candidates.All(candidate =>
                {
                    bool nextOccupied = candidate.BaseNumber < 3 && _bases[candidate.BaseNumber].Occupied &&
                        candidates.Any(c => c.BaseNumber == candidate.BaseNumber + 1);
                    return StealEngine.ShouldCpuAttemptSteal(_rng, candidate.Runner, CurrentPitcher().Player, catcher, candidate.BaseNumber, _outs, 0, 0, scoreDifferential, nextBaseOccupied: false)
                        && (nextOccupied || candidate.BaseNumber >= 3 || !_bases[candidate.BaseNumber].Occupied);
                });
                bool rightDoubleStealCall = shouldAttempt && _rng.Next(100) < 18;
                if (!CoachDecisionEngine.ShouldCallRiskyOffense(
                        _rng,
                        HeadCoachForTeam(BattingTeam),
                        rightDoubleStealCall,
                        IsGameOnLineForBattingTeam(),
                        candidates.Any(candidate => candidate.BaseNumber >= 2)))
                {
                    return false;
                }

                Log("Double steal is on.");
                foreach (var candidate in candidates.OrderByDescending(c => c.BaseNumber).ToList())
                {
                    Player? runner = candidate.Runner;
                    if (_outs >= 3 || runner == null || !_bases[candidate.BaseNumber - 1].Occupied || _bases[candidate.BaseNumber - 1].Runner?.Id != runner.Id)
                        continue;

                    var call = ChooseCpuStealDefense(CurrentPitcher().Player, catcher, runner, candidate.BaseNumber);
                    var result = StealEngine.Resolve(_rng, runner, CurrentPitcher().Player, catcher, DefensivePlayerForBase(candidate.BaseNumber + 1), candidate.BaseNumber, _outs, 0, 0, scoreDifferential, call);
                    Player? tagFielder = DefensivePlayerForBase(candidate.BaseNumber + 1);
                    RegisterStealExposure(runner, catcher, tagFielder, result);
                    var runnerLine = BatterLine(runner);
                    var catcherLine = catcher == null ? null : BatterLine(catcher, DefenseTeam);
                    if (result.SuccessfulSteal)
                    {
                        runnerLine.SB++;
                        if (catcherLine != null)
                            catcherLine.StolenBasesAllowed++;
                        ClearBase(candidate.BaseNumber);
                        if (result.FinalBase >= 4)
                        {
                            ScoreRunner(candidate);
                            runnerLine.R++;
                            Log(runner.Name + " steals home.");
                        }
                        else
                        {
                            PutRunnerOnBase(runner, result.FinalBase, earned: candidate.Earned, candidate.ResponsiblePitcherId);
                            Log(runner.Name + " steals " + BaseLabel(result.FinalBase) + ".");
                        }
                    }
                    else if (result.RunnerOut)
                    {
                        runnerLine.CS++;
                        if (catcherLine != null && result.Outcome == StealAttemptOutcome.CaughtStealing)
                            catcherLine.CatcherCaughtStealing++;
                        ClearBase(candidate.BaseNumber);
                        RecordOut(CurrentPitcher(), "CS");
                        Log(runner.Name + " is caught stealing.");
                    }
                }

                return true;
            }

            private bool TryResolvePitchEscape(PitcherUse pitcherUse, GameplayCpu.PitchDecision pitch, int pitchAdjustment, out bool thirdOut)
            {
                thirdOut = false;
                if (!_bases.Any(baseState => baseState.Occupied))
                    return false;

                Player? catcher = DefensivePlayer("C");
                Player catcherForResolution = catcher ?? DefaultDefensivePlayer;
                var kind = PitchEscapeEngine.Roll(
                    _rng,
                    pitcherUse.Player,
                    catcherForResolution,
                    MapPitchType(pitch.PitchType),
                    pitch.AimX,
                    pitch.AimY,
                    pitchAdjustment,
                    CatcherBlockingRating(catcher));
                if (kind == PitchEscapeKind.None)
                    return false;

                string label = kind == PitchEscapeKind.WildPitch ? "Wild pitch" : "Passed ball";
                RecordPitchEscapeStats(kind, pitcherUse, catcher);
                bool anyAdvance = false;

                foreach (var runner in OccupiedBases().OrderByDescending(b => b.BaseNumber).ToList())
                {
                    if (_outs >= 3)
                        break;
                    if (!_bases[runner.BaseNumber - 1].Occupied || _bases[runner.BaseNumber - 1].Runner?.Id != runner.Runner?.Id)
                        continue;
                    if (runner.Runner == null)
                    {
                        ClearBase(runner.BaseNumber);
                        continue;
                    }

                    Player runnerPlayer = runner.Runner;

                    int targetBase = Math.Min(4, runner.BaseNumber + 1);
                    if (targetBase <= 3 && _bases[targetBase - 1].Occupied)
                        continue;

                    Player? targetFielder = DefensivePlayerForBase(targetBase);

                    var advance = PitchEscapeEngine.ResolveAdvance(
                        _rng,
                        runnerPlayer,
                        runner.BaseNumber,
                        _outs,
                        BattingScore - FieldingScore,
                        catcherForResolution,
                        targetFielder ?? DefaultDefensivePlayer,
                        kind);
                    if (!advance.Attempt)
                        continue;

                    RegisterParticipation(runnerPlayer, BattingTeam, InjuryExposureType.Baserunning);
                    RegisterParticipation(catcher, DefenseTeam, InjuryExposureType.FieldingPlay);
                    RegisterParticipation(targetFielder, DefenseTeam, InjuryExposureType.FieldingPlay);
                    anyAdvance = true;
                    ClearBase(runner.BaseNumber);
                    if (advance.RunnerOut)
                    {
                        RecordPitchEscapeOutStats(catcher, targetFielder);
                        RecordOut(pitcherUse, kind == PitchEscapeKind.WildPitch ? "WP" : "PB");
                        Log(label + ": " + runnerPlayer.Name + " is thrown out trying for " + BaseLabel(targetBase) + ".");
                        thirdOut = _outs >= 3;
                        if (thirdOut)
                            return true;
                    }
                    else if (targetBase >= 4)
                    {
                        var scoringRunner = runner.Copy();
                        if (kind == PitchEscapeKind.PassedBall)
                            scoringRunner.Earned = false;
                        ScoreRunner(scoringRunner);
                        BatterLine(runnerPlayer).R++;
                        Log(label + ": " + runnerPlayer.Name + " scores.");
                    }
                    else
                    {
                        PutRunnerOnBase(runnerPlayer, targetBase, runner.Earned, runner.ResponsiblePitcherId);
                        Log(label + ": " + runnerPlayer.Name + " advances to " + BaseLabel(targetBase) + ".");
                    }
                }

                if (!anyAdvance)
                    Log(label + ": runners hold.");
                return true;
            }

            private void RecordPitchEscapeStats(PitchEscapeKind kind, PitcherUse pitcherUse, Player? catcher)
            {
                if (kind == PitchEscapeKind.WildPitch)
                {
                    PitcherLine(pitcherUse).WildPitches++;
                    return;
                }

                if (catcher != null)
                    BatterLine(catcher, DefenseTeam).PassedBalls++;
            }

            private void RecordPitchEscapeOutStats(Player? catcher, Player? tagFielder)
            {
                if (catcher != null)
                    BatterLine(catcher, DefenseTeam).Assists++;
                if (tagFielder != null)
                    BatterLine(tagFielder, DefenseTeam).Putouts++;
            }

            private int CatcherBlockingRating(Player? catcher)
            {
                if (catcher == null)
                    return 45;
                return (PositionFieldingRating(catcher, "C") +
                    Rating(catcher, p => p.Accuracy, 50) +
                    Rating(catcher, p => p.PopTime, 50) +
                    Rating(catcher, p => p.TagRating, 50) / 2) / 3;
            }

            private int AdvanceRunnersOnHit(Player batter, int batterBases, bool hitAndRun)
            {
                int runsBefore = BattingScore;
                var runners = OccupiedBases().OrderByDescending(b => b.BaseNumber).ToList();
                ClearBases();
                var occupiedTargets = new HashSet<int>();
                foreach (var runner in runners)
                {
                    if (runner.Runner is not Player runnerPlayer)
                        continue;

                    RegisterParticipation(runnerPlayer, BattingTeam, InjuryExposureType.Baserunning);

                    int minimum = runner.BaseNumber <= batterBases ? Math.Min(4, batterBases + 1) : runner.BaseNumber;
                    int fielderArm = Rating(RandomDefender(), p => p.ArmStrength, 50);
                    int depth = batterBases == 3 ? 92 : batterBases == 2 ? 78 : 54;
                    var decision = GameplayCpu.DecideBaserunning(
                        _rng,
                        runnerPlayer,
                        runner.BaseNumber,
                        _outs,
                        batterBases >= 2 ? GameplayCpu.BallLocation.Gap : GameplayCpu.BallLocation.OutfieldCenter,
                        depth,
                        fielderArm,
                        BattingScore - FieldingScore,
                        forced: minimum > runner.BaseNumber,
                        runnerAheadOccupied: occupiedTargets.Any(t => t > runner.BaseNumber && t <= 3));
                    int target = Math.Clamp(Math.Max(decision.TargetBase, minimum), 1, 4);
                    if (hitAndRun && target < 4)
                        target = Math.Min(4, target + 1);

                    if (target >= 4)
                    {
                        ScoreRunner(runner);
                        BatterLine(runnerPlayer).R++;
                        continue;
                    }

                    while (target <= 3 && occupiedTargets.Contains(target))
                        target++;
                    if (target >= 4)
                    {
                        ScoreRunner(runner);
                        BatterLine(runnerPlayer).R++;
                    }
                    else
                    {
                        PutRunnerOnBase(runnerPlayer, target, runner.Earned, runner.ResponsiblePitcherId);
                        occupiedTargets.Add(target);
                    }
                }

                PutRunnerOnBase(batter, Math.Min(3, batterBases), earned: true, CurrentPitcher().Player?.Id);
                RegisterParticipation(batter, BattingTeam, InjuryExposureType.Baserunning);
                return BattingScore - runsBefore;
            }

            private void RegisterStealExposure(Player runner, Player? catcher, Player? tagFielder, StealAttemptResult result)
            {
                RegisterParticipation(runner, BattingTeam, InjuryExposureType.StealOrSlide);
                RegisterParticipation(catcher, DefenseTeam, InjuryExposureType.FieldingPlay);
                RegisterParticipation(tagFielder, DefenseTeam, InjuryExposureType.FieldingPlay);

                int defenseScore = result.ThrowScore + result.TagScore / 2;
                if (Math.Abs(result.JumpScore - defenseScore) <= 10)
                {
                    RegisterParticipation(runner, BattingTeam, InjuryExposureType.Collision);
                    RegisterParticipation(tagFielder, DefenseTeam, InjuryExposureType.Collision);
                }
            }

            private int AdvanceRunnersOnBases(int bases, Player batter, bool batterSafe, bool earned)
            {
                int runsBefore = BattingScore;
                var runners = OccupiedBases().OrderByDescending(b => b.BaseNumber).ToList();
                ClearBases();
                foreach (var runner in runners)
                {
                    RegisterParticipation(runner.Runner, BattingTeam, InjuryExposureType.Baserunning);
                    int target = runner.BaseNumber + bases;
                    if (target >= 4)
                    {
                        ScoreRunner(runner);
                        BatterLine(runner.Runner).R++;
                    }
                    else
                    {
                        PutRunnerOnBase(runner.Runner, target, runner.Earned, runner.ResponsiblePitcherId);
                    }
                }

                if (batterSafe)
                {
                    RegisterParticipation(batter, BattingTeam, InjuryExposureType.Baserunning);
                    PutRunnerOnBase(batter, Math.Min(3, bases), earned, CurrentPitcher().Player?.Id);
                }
                return BattingScore - runsBefore;
            }

            private int ForceAdvance(Player batter, bool earned)
            {
                int runsBefore = BattingScore;
                if (!_bases[0].Occupied)
                {
                    PutRunnerOnBase(batter, 1, earned);
                    return 0;
                }

                if (!_bases[1].Occupied)
                {
                    MoveBase(1, 2);
                    PutRunnerOnBase(batter, 1, earned);
                    return 0;
                }

                if (!_bases[2].Occupied)
                {
                    MoveBase(2, 3);
                    MoveBase(1, 2);
                    PutRunnerOnBase(batter, 1, earned);
                    return 0;
                }

                var forcedRunner = _bases[2].Copy();
                BatterLine(forcedRunner.Runner).R++;
                ScoreRunner(forcedRunner);
                MoveBase(2, 3);
                MoveBase(1, 2);
                PutRunnerOnBase(batter, 1, earned);
                return BattingScore - runsBefore;
            }

            private int AdvanceHomeRun(Player batter)
            {
                int runs = 1;
                foreach (var runner in OccupiedBases())
                {
                    RegisterParticipation(runner.Runner, BattingTeam, InjuryExposureType.Baserunning);
                    ScoreRunner(runner);
                    BatterLine(runner.Runner).R++;
                    runs++;
                }
                ClearBases();
                RegisterParticipation(batter, BattingTeam, InjuryExposureType.Baserunning);
                ScoreRunner(new BaseSlot
                {
                    Occupied = true,
                    Runner = batter,
                    Earned = true,
                    ResponsiblePitcherId = CurrentPitcher().Player?.Id ?? Guid.Empty
                });
                BatterLine(batter).R++;
                return runs;
            }

            private void FinalizePlateAppearance(PitcherUse pitcher, int runs, bool earned = true)
            {
                // Individual runners carry earned status and responsible-pitcher ownership.
            }

            private void RecordOut(PitcherUse pitcher, string kind)
            {
                _outs++;
                pitcher.Outs++;
                pitcher.OutsThisInning++;
                if (pitcher.EarnedRunsThisInning == 0)
                    pitcher.ConsecutiveScorelessOuts++;
                PitcherLine(pitcher).IPOuts++;
                CreditDefensiveOuts();
            }

            private void CreditDefensiveOuts()
            {
                var card = LineupEngine.BuildLineupCard(DefenseTeam);
                foreach (Player player in (card.DefensiveAssignments ?? new Dictionary<string, Player>(StringComparer.OrdinalIgnoreCase))
                    .Values.Where(player => player != null)
                    .GroupBy(player => player.Id)
                    .Select(group => group.First()))
                {
                    BatterLine(player, DefenseTeam).DefensiveOuts++;
                }
            }

            private void RegisterPitcherBaserunner(PitcherUse pitcher, bool fatigueEligible)
            {
                if (fatigueEligible)
                    pitcher.PostLimitBaserunnersThisInning++;
            }

            private void MaybeChangePitcher()
            {
                PitcherUse pitcher = CurrentPitcher();
                if (pitcher == null || pitcher.Team?.PitchingPlan?.UseAllStarPitchingRules == true)
                    return;

                int oneInningRuns = RunsAcrossInnings(pitcher.RunsAllowedByInning, _inning, 1);
                int twoInningRuns = RunsAcrossInnings(pitcher.RunsAllowedByInning, _inning, 2);
                int threeInningRuns = RunsAcrossInnings(pitcher.RunsAllowedByInning, _inning, 3);
                bool mustRemove = oneInningRuns >= 5 || twoInningRuns >= 6 || threeInningRuns >= 7 ||
                    pitcher.PitchCount > pitcher.AvailablePitchLimit + 18 || (!pitcher.Starter && pitcher.Outs >= ReliefPitcherMaxOuts + 3);
                bool strategic = pitcher.PitchCount > pitcher.AvailablePitchLimit && _rng.Next(100) < 38;
                if (pitcher.PitchCount > pitcher.AvailablePitchLimit)
                    InjuryEngine.TryEventInjury(pitcher.Player, _rng, 12);
                if (!mustRemove && !strategic)
                    return;

                var next = NextPitcher(DefenseTeam, pitcher.Team == _away ? _awayPitchers : _homePitchers, pitcher);
                if (next == null)
                    return;

                InitializeReliefAppearance(next);

                if (_topHalf)
                    _homePitcher = next;
                else
                    _awayPitcher = next;
                GameLineupTracker.RecordPitcherChange(
                    DefenseTeam.Id == _away.Id ? _result.AwayStartingLineup : _result.HomeStartingLineup,
                    next.Player,
                    pitcher.Player,
                    _inning,
                    _topHalf ? HalfInning.Top : HalfInning.Bottom,
                    reason: "Simulation pitcher change");
            }

            private PitcherUse? NextPitcher(Team team, List<Player> staff, PitcherUse current)
            {
                var used = team.Id == _away.Id ? _awayUsedPitchers : _homeUsedPitchers;
                var nextPlayer = staff.FirstOrDefault(p => p != null &&
                    !PitchingRotationEngine.IsRotationStarter(team, p) &&
                    used.All(u => u.Player?.Id != p.Id));
                if (nextPlayer == null && ShouldUseStarterInRelief(team))
                {
                    nextPlayer = staff
                        .Where(p => p != null && PitchingRotationEngine.CanUseStarterInRelief(team, p))
                        .Where(p => used.All(u => u.Player?.Id != p.Id))
                        .OrderByDescending(PitchingRotationEngine.StarterScore)
                        .FirstOrDefault();
                }
                if (nextPlayer == null)
                    nextPlayer = team.Roster?
                        .Where(InjuryEngine.IsAvailable)
                        .Where(p => p.Role != PlayerRole.Pitcher && !PitchingRotationEngine.IsRotationStarter(team, p))
                        .Where(p => used.All(u => u.Player?.Id != p.Id))
                        .OrderByDescending(p => p.Fielding + p.ArmStrength)
                        .FirstOrDefault();
                if (nextPlayer == null)
                    return null;

                if (nextPlayer.Role != PlayerRole.Pitcher && !PitchProfileEngine.IsPitcherClassified(nextPlayer))
                    PitchProfileEngine.AssignEmergencyPitchArsenal(nextPlayer, _rng);
                else
                    PitchProfileEngine.EnsurePitcherMinimumArsenal(nextPlayer, _rng);
                var next = CreatePitcherUse(team, nextPlayer, starter: false);
                used.Add(next);
                return next;
            }

            private void InitializeReliefAppearance(PitcherUse pitcher)
            {
                if (pitcher == null || pitcher.Starter)
                    return;

                int lead = FieldingScore - BattingScore;
                int tyingRunDistance = OccupiedBases().Count() + 2;
                pitcher.EnteredWithThreeRunLead = lead > 0 && lead <= 3;
                pitcher.EnteredWithTyingRunThreat = lead > 0 && tyingRunDistance >= lead;
                pitcher.EnteredInSaveSituation = pitcher.EnteredWithThreeRunLead || pitcher.EnteredWithTyingRunThreat;
                pitcher.LeadPreserved = true;
            }

            private bool ShouldUseStarterInRelief(Team team)
            {
                if (team?.PitchingPlan?.UseAllStarPitchingRules == true)
                    return false;

                int deficit = FieldingScore - BattingScore;
                bool closeDeficit = deficit < 0 && deficit > -3;
                if (!closeDeficit)
                    return false;

                var coach = HeadCoachForTeam(team);
                int chance = coach?.Strategy switch
                {
                    CoachStrategy.Aggressive => 72,
                    CoachStrategy.Conservative => 34,
                    CoachStrategy.Safe => 12,
                    _ => 25
                };

                chance += coach?.Style switch
                {
                    CoachStyle.Championship => 12,
                    CoachStyle.AboveAverage => 6,
                    CoachStyle.BelowAverage => -8,
                    _ => 0
                };

                return _rng.Next(100) < Math.Clamp(chance, 0, 95);
            }

            private int PitcherAdjustment(PitcherUse pitcher)
            {
                int adjustment = 0;
                if (pitcher.PitchCount > pitcher.AvailablePitchLimit || (!pitcher.Starter && pitcher.Outs >= ReliefPitcherMaxOuts))
                {
                    if (pitcher.PostLimitBaserunnersThisInning >= 4)
                        adjustment -= 45;
                    else if (pitcher.PostLimitBaserunnersThisInning >= 3)
                        adjustment -= 20;
                    else if (pitcher.PostLimitBaserunnersThisInning >= 2)
                        adjustment -= 10;
                }

                if (!pitcher.Starter)
                    adjustment -= Math.Max(0, pitcher.Player?.ConsecutiveReliefGames ?? 0) * 10;

                if (pitcher.EarnedRunsThisInning >= 5)
                    adjustment -= 10 * Math.Max(1, pitcher.EarnedRunsThisInning / 5);

                int scorelessInnings = pitcher.ConsecutiveScorelessOuts / 3;
                if (scorelessInnings >= 5)
                    adjustment += Math.Min(40, (scorelessInnings - 4) * 10);
                return adjustment;
            }

            private static int RunsAcrossInnings(Dictionary<int, int> runsByInning, int endingInning, int count)
            {
                int total = 0;
                for (int inning = Math.Max(1, endingInning - count + 1); inning <= endingInning; inning++)
                    total += runsByInning.GetValueOrDefault(inning);
                return total;
            }

            private bool ShouldUseNoDoublesDefense()
                => FieldingScore > BattingScore && FieldingScore - BattingScore <= 3 && _inning >= Math.Max(1, _rules.Innings - 1);

            private bool ShouldUseOutfieldIn()
                => _outs < 2 && _bases[2].Occupied && BattingScore <= FieldingScore && FieldingScore - BattingScore <= 2;

            private bool ShouldCallSacrificeBunt()
            {
                if (_outs >= 2)
                    return false;
                int differential = BattingScore - FieldingScore;
                bool closeNeedRun = differential <= 0 && differential >= -3;
                bool rightCall = _bases[2].Occupied && closeNeedRun ||
                    _outs == 0 && (_bases[0].Occupied || _bases[1].Occupied) && Math.Abs(differential) <= 2;
                return CoachDecisionEngine.ShouldCallSafeOffense(
                    _rng,
                    DecisionCoachForTeam(BattingTeam),
                    rightCall,
                    IsGameOnLineForBattingTeam(),
                    HasScoringOpportunity());
            }

            private bool ShouldCallHitAndRun(Player batter)
            {
                if (_outs >= 2)
                    return false;
                Player? runner = BestHitAndRunRunner();
                if (runner == null)
                    return false;
                bool rightCall = Rating(runner, p => p.Speed, 50) + Rating(runner, p => p.BaseRunning, 50) >= 105
                    && Rating(batter, p => p.Contact, 50) >= 48
                    && BattingScore - FieldingScore >= -3;
                return CoachDecisionEngine.ShouldCallRiskyOffense(
                    _rng,
                    HeadCoachForTeam(BattingTeam),
                    rightCall,
                    IsGameOnLineForBattingTeam(),
                    HasScoringOpportunity());
            }

            private static Coach? HeadCoachForTeam(Team? team)
            {
                if (team == null)
                    return null;
                team.NormalizeText();
                return team.Coaches?.FirstOrDefault(c => c.Id == team.CoachId)
                    ?? team.Coaches?.FirstOrDefault(c => string.Equals(c.Role, "Head Coach", StringComparison.OrdinalIgnoreCase))
                    ?? team.Coaches?.FirstOrDefault();
            }

            private static Coach DecisionCoachForTeam(Team? team)
                => HeadCoachForTeam(team) ?? DefaultDecisionCoach;

            private static int CoachStrategyChanceAdjustment(Coach? coach, int safeAdjustment, int aggressiveAdjustment)
            {
                return coach?.Strategy switch
                {
                    CoachStrategy.Safe => safeAdjustment,
                    CoachStrategy.Aggressive => aggressiveAdjustment,
                    _ => 0
                };
            }

            private bool IsGameOnLineForBattingTeam()
            {
                int differential = BattingScore - FieldingScore;
                int regulation = Math.Max(1, Math.Clamp(_rules.Innings, 5, 9));
                bool late = _inning >= Math.Max(1, regulation - 1);
                bool extra = _inning > regulation;
                bool close = Math.Abs(differential) <= 1;
                bool comeback = differential < 0 && differential >= -3;
                return extra || close || (late && comeback);
            }

            private bool HasScoringOpportunity()
            {
                return _outs < 2 && _bases.Any(slot => slot.Occupied);
            }

            private bool IsSacrificeBuntSound()
            {
                int differential = BattingScore - FieldingScore;
                bool closeNeedRun = differential <= 0 && differential >= -3;
                return _outs < 2 && (_bases[2].Occupied && closeNeedRun ||
                    _outs == 0 && (_bases[0].Occupied || _bases[1].Occupied) && Math.Abs(differential) <= 2);
            }

            private bool IsHitAndRunSound(Player batter)
            {
                Player? runner = BestHitAndRunRunner();
                if (runner == null || _outs >= 2)
                    return false;
                return Rating(runner, p => p.Speed, 50) + Rating(runner, p => p.BaseRunning, 50) >= 105 &&
                    Rating(batter, p => p.Contact, 50) >= 48 &&
                    BattingScore - FieldingScore >= -3;
            }

            private int BuntDefenseCoachModifier()
            {
                bool runThreat = _outs < 2 && _bases.Any(slot => slot.Occupied);
                Coach coach = DecisionCoachForTeam(DefenseTeam);
                bool preventCall = CoachDecisionEngine.ShouldCallPreventDefense(
                    _rng,
                    coach,
                    runThreat,
                    IsGameOnLineForBattingTeam(),
                    runThreat);
                return CoachDecisionEngine.StrategyExecutionModifier(coach, preventCall && runThreat);
            }

            private DefensiveStealCall ChooseCpuStealDefense(Player pitcher, Player? catcher, Player runner, int fromBase)
            {
                Coach coach = DecisionCoachForTeam(DefenseTeam);
                DefensiveStealCall bestCall = StealEngine.ChooseCpuDefense(_rng, pitcher, catcher, runner, fromBase);
                bool runThreat = fromBase >= 2 || IsGameOnLineForBattingTeam();
                bool correctPreventCall = bestCall != DefensiveStealCall.Normal;
                if (!CoachDecisionEngine.ShouldCallPreventDefense(_rng, coach, correctPreventCall, IsGameOnLineForBattingTeam(), runThreat))
                    return coach.Strategy == CoachStrategy.Safe && runThreat ? DefensiveStealCall.HoldRunner : DefensiveStealCall.Normal;

                return bestCall;
            }

            private Player? BestHitAndRunRunner()
            {
                for (int i = 2; i >= 0; i--)
                {
                    if (!_bases[i].Occupied || _bases[i].Runner == null)
                        continue;

                    int targetBase = i + 2;
                    if (targetBase >= 4 || !_bases[targetBase - 1].Occupied)
                        return _bases[i].Runner;
                }

                return null;
            }

            private LiveHitType ResolveHitType(int power, int speed, int pitchAdj)
            {
                int homeRunChance = Math.Clamp(34 + (power - 50) * 2 + pitchAdj, 8, 145);
                if (_rng.Next(1000) < homeRunChance)
                    return LiveHitType.HomeRun;
                int tripleChance = Math.Clamp(12 + (speed - 50) / 3 + pitchAdj / 2, 4, 80);
                if (_rng.Next(1000) < tripleChance)
                    return LiveHitType.Triple;
                int doubleChance = Math.Clamp(115 + (power - 50) + (speed - 50) / 2 + pitchAdj, 55, 240);
                if (_rng.Next(1000) < doubleChance)
                    return LiveHitType.Double;
                return LiveHitType.Single;
            }

            private bool IsCompleteAfterTop(int regulationInnings)
            {
                return _inning >= regulationInnings && _result.HomeScore > _result.AwayScore;
            }

            private bool IsCompleteAfterBottom(int regulationInnings)
            {
                if (_inning < regulationInnings)
                    return false;
                if (!_rules.ExtraInnings)
                    return true;
                return _result.HomeScore != _result.AwayScore;
            }

            private bool IsWalkOff()
            {
                return !_topHalf && _inning >= Math.Clamp(_rules.Innings, 5, 9) && _result.HomeScore > _result.AwayScore;
            }

            private bool IsMercyRuleComplete(bool completedTop)
            {
                if (!_rules.MercyRuleEnabled || _inning < Math.Max(1, _rules.MercyRuleMinimumInning))
                    return false;
                int lead = Math.Abs(_result.HomeScore - _result.AwayScore);
                if (lead < Math.Max(1, _rules.MercyRuleRuns))
                    return false;
                if (completedTop)
                    return _result.HomeScore > _result.AwayScore;
                return _result.HomeScore != _result.AwayScore;
            }

            private void PlaceExtraInningRunner()
            {
                var lineup = BattingLineup;
                if (lineup.Count == 0)
                    return;

                int currentIndex = _topHalf ? _awayBatterIndex : _homeBatterIndex;
                var blocked = Enumerable.Range(0, Math.Min(8, lineup.Count)).Select(i => lineup[(currentIndex + i) % lineup.Count].Id).ToHashSet();
                var runner = lineup.Where(p => !blocked.Contains(p.Id)).OrderByDescending(p => p.BaseRunning + p.Speed).FirstOrDefault()
                    ?? lineup[(currentIndex + lineup.Count - 1) % lineup.Count];
                PutRunnerOnBase(runner, 2, earned: false);
            }

            private void AssignDecisions()
            {
                var appearances = BuildDecisionAppearances(_awayUsedPitchers)
                    .Concat(BuildDecisionAppearances(_homeUsedPitchers))
                    .ToList();
                PitcherDecisionResult decisions = PitcherDecisionEngine.Apply(new PitcherDecisionRequest
                {
                    AwayTeamId = _away.Id,
                    HomeTeamId = _home.Id,
                    AwayScore = _result.AwayScore,
                    HomeScore = _result.HomeScore,
                    RegulationInnings = Math.Clamp(_rules.Innings, 5, 9),
                    WinningPitcherCandidateId = _winningPitcherCandidateId,
                    LosingPitcherCandidateId = _losingPitcherCandidateId,
                    Appearances = appearances
                });
                _result.WinningPitcherId = decisions.WinningPitcherId;
                _result.WinningPitcherName = decisions.WinningPitcherName;
                _result.LosingPitcherId = decisions.LosingPitcherId;
                _result.LosingPitcherName = decisions.LosingPitcherName;
                _result.SavePitcherId = decisions.SavePitcherId;
                _result.SavePitcherName = decisions.SavePitcherName;
            }

            private List<PitcherDecisionAppearance> BuildDecisionAppearances(List<PitcherUse> staff)
            {
                var valid = (staff ?? new List<PitcherUse>())
                    .Where(use => use?.Player != null)
                    .ToList();
                return valid.Select((use, index) => new PitcherDecisionAppearance
                {
                    TeamId = use.Team?.Id ?? Guid.Empty,
                    PlayerId = use.Player?.Id ?? Guid.Empty,
                    PlayerName = use.Player?.Name ?? "",
                    Starter = use.Starter,
                    FinishedGame = index == valid.Count - 1,
                    AppearanceOrder = index,
                    EnteredInSaveSituation = use.EnteredInSaveSituation,
                    EnteredWithThreeRunLead = use.EnteredWithThreeRunLead,
                    EnteredWithTyingRunThreat = use.EnteredWithTyingRunThreat,
                    LeadPreserved = use.LeadPreserved,
                    Line = PitcherLine(use)
                }).ToList();
            }

            private void FinalizeResult()
            {
                _result.Lines ??= new List<PlayerGameLine>();
                _result.AwayRunsByInning = _awayRunsByInning.ToList();
                _result.HomeRunsByInning = _homeRunsByInning.ToList();
                _result.GameLengthInnings = Math.Max(_result.AwayRunsByInning.Count, _result.HomeRunsByInning.Count);
                _result.GameLengthOuts = Math.Clamp(_outs, 0, 3);
                _result.AwayLeftOnBase = _awayLeftOnBase;
                _result.HomeLeftOnBase = _homeLeftOnBase;
                _result.AwayHits = _result.Lines.Where(line => line.TeamId == _result.AwayTeamId).Sum(line => line.H);
                _result.HomeHits = _result.Lines.Where(line => line.TeamId == _result.HomeTeamId).Sum(line => line.H);
                _result.AwayErrors = _result.Lines.Where(line => line.TeamId == _result.AwayTeamId).Sum(line => line.Errors);
                _result.HomeErrors = _result.Lines.Where(line => line.TeamId == _result.HomeTeamId).Sum(line => line.Errors);
                _result.PlayByPlay = _events.Select((gameEvent, index) => new GamePlayByPlayEntry
                {
                    Sequence = index + 1,
                    Inning = gameEvent.Inning,
                    Half = gameEvent.TopHalf ? HalfInning.Top : HalfInning.Bottom,
                    Outs = Math.Clamp(gameEvent.Outs, 0, 3),
                    AwayScore = gameEvent.AwayScore,
                    HomeScore = gameEvent.HomeScore,
                    Bases = gameEvent.Bases ?? "",
                    Description = gameEvent.Narration ?? ""
                }).ToList();
            }

            private PlayerGameLine BatterLine(Player? player)
                => BatterLine(player, BattingTeam);

            private PlayerGameLine BatterLine(Player? player, Team team)
                => Line(player, team, pitcher: false);

            private PlayerGameLine PitcherLine(PitcherUse pitcher)
            {
                var line = Line(pitcher.Player, pitcher.Team, pitcher: true);
                if (pitcher.Starter)
                    line.StartingPitcher = true;
                return line;
            }

            private PlayerGameLine Line(Player? player, Team team, bool pitcher)
            {
                if (player == null)
                    player = new Player { Name = pitcher ? "Emergency Pitcher" : "Unknown Player", Role = pitcher ? PlayerRole.Pitcher : PlayerRole.Batter };
                string key = team.Id + "|" + player.Id + "|" + pitcher;
                if (_lines.TryGetValue(key, out var line))
                    return line;

                line = new PlayerGameLine
                {
                    TeamId = team.Id,
                    PlayerId = player.Id,
                    PlayerName = player.Name,
                    Pitcher = pitcher,
                    StartingPitcher = false,
                    Classification = player.Classification,
                    InitialClassification = player.InitialClassification == PlayerClassification.Unassigned ? player.Classification : player.InitialClassification
                };
                _lines[key] = line;
                return line;
            }

            private Player? CurrentBatter()
            {
                var lineup = BattingLineup;
                if (lineup.Count == 0)
                    return null;
                int index = _topHalf ? _awayBatterIndex : _homeBatterIndex;
                return lineup[PositiveModulo(index, lineup.Count)];
            }

            private void AdvanceBatter()
            {
                if (_topHalf)
                    _awayBatterIndex = PositiveModulo(_awayBatterIndex + 1, Math.Max(1, _awayLineup.Count));
                else
                    _homeBatterIndex = PositiveModulo(_homeBatterIndex + 1, Math.Max(1, _homeLineup.Count));
            }

            private PitcherUse CurrentPitcher()
                => _topHalf ? _homePitcher : _awayPitcher;

            private Team BattingTeam => _topHalf ? _away : _home;
            private Team DefenseTeam => _topHalf ? _home : _away;
            private List<Player> BattingLineup => _topHalf ? _awayLineup : _homeLineup;
            private int BattingScore => _topHalf ? _result.AwayScore : _result.HomeScore;
            private int FieldingScore => _topHalf ? _result.HomeScore : _result.AwayScore;

            private void AddRun()
            {
                if (_topHalf) _result.AwayScore++;
                else _result.HomeScore++;
            }

            private void ScoreRunner(BaseSlot runner)
            {
                int beforeDiff = _result.AwayScore - _result.HomeScore;
                AddRun();
                ChargeRun(runner);
                int afterDiff = _result.AwayScore - _result.HomeScore;
                if (afterDiff == 0)
                {
                    _winningPitcherCandidateId = null;
                    _losingPitcherCandidateId = null;
                }
                else if (beforeDiff == 0 || Math.Sign(beforeDiff) != Math.Sign(afterDiff))
                {
                    bool awayLeading = afterDiff > 0;
                    _winningPitcherCandidateId = (awayLeading ? _awayPitcher : _homePitcher)?.Player?.Id;
                    _losingPitcherCandidateId = (FindPitcherUse(runner?.ResponsiblePitcherId ?? Guid.Empty) ??
                        (awayLeading ? _homePitcher : _awayPitcher))?.Player?.Id;
                }
            }

            private void ChargeRun(BaseSlot runner)
            {
                var pitcher = FindPitcherUse(runner?.ResponsiblePitcherId ?? Guid.Empty) ?? CurrentPitcher();
                if (pitcher == null)
                    return;
                pitcher.RunsThisInning++;
                pitcher.RunsAllowedByInning[_inning] = pitcher.RunsAllowedByInning.GetValueOrDefault(_inning) + 1;
                if (runner?.Earned == true)
                {
                    pitcher.EarnedRunsThisInning++;
                    pitcher.EarnedRunsAllowedByInning[_inning] = pitcher.EarnedRunsAllowedByInning.GetValueOrDefault(_inning) + 1;
                    PitcherLine(pitcher).ER++;
                }
                PitcherLine(pitcher).RunsAllowed++;
                if (pitcher.EnteredInSaveSituation && !PitcherTeamHasLead(pitcher))
                    pitcher.LeadPreserved = false;
                pitcher.ConsecutiveScorelessOuts = 0;
            }

            private bool PitcherTeamHasLead(PitcherUse pitcher)
            {
                if (pitcher?.Team == null)
                    return false;
                int teamScore = pitcher.Team.Id == _away.Id ? _result.AwayScore : _result.HomeScore;
                int opponentScore = pitcher.Team.Id == _away.Id ? _result.HomeScore : _result.AwayScore;
                return teamScore > opponentScore;
            }

            private PitcherUse? FindPitcherUse(Guid playerId)
                => playerId == Guid.Empty ? null :
                    _awayUsedPitchers.Concat(_homeUsedPitchers).LastOrDefault(p => p.Player?.Id == playerId);

            private void ClearBases()
            {
                foreach (var slot in _bases)
                    slot.Clear();
            }

            private void ClearBase(int baseNumber)
            {
                if (baseNumber >= 1 && baseNumber <= 3)
                    _bases[baseNumber - 1].Clear();
            }

            private void MoveBase(int fromBase, int toBase)
            {
                var from = _bases[fromBase - 1];
                var to = _bases[toBase - 1];
                to.Runner = from.Runner;
                to.Earned = from.Earned;
                to.ResponsiblePitcherId = from.ResponsiblePitcherId;
                to.Occupied = from.Occupied;
                from.Clear();
            }

            private void PutBatterOnBase(Player batter, int baseNumber, bool earned)
                => PutRunnerOnBase(batter, baseNumber, earned);

            private void PutRunnerOnBase(Player? runner, int baseNumber, bool earned, Guid? responsiblePitcherId = null)
            {
                if (runner == null || baseNumber < 1 || baseNumber > 3)
                    return;
                var slot = _bases[baseNumber - 1];
                slot.Occupied = true;
                slot.Runner = runner;
                slot.Earned = earned;
                slot.ResponsiblePitcherId = responsiblePitcherId ?? CurrentPitcher().Player?.Id ?? Guid.Empty;
            }

            private IEnumerable<BaseSlot> OccupiedBases()
            {
                for (int i = 0; i < _bases.Length; i++)
                {
                    if (_bases[i].Occupied && _bases[i].Runner != null)
                        yield return _bases[i].Copy(i + 1);
                }
            }

            private BaseSlot? LeadRunner()
                => OccupiedBases().OrderByDescending(b => b.BaseNumber).FirstOrDefault();

            private BaseSlot? LeadStealCandidate()
            {
                for (int i = 2; i >= 0; i--)
                {
                    if (!_bases[i].Occupied || _bases[i].Runner == null || i == 2)
                        continue;
                    if (!_bases[i + 1].Occupied)
                        return _bases[i].Copy(i + 1);
                }
                return null;
            }

            private List<BaseSlot> DoubleStealCandidates()
            {
                var candidates = new List<BaseSlot>();
                for (int i = 2; i >= 0; i--)
                {
                    if (!_bases[i].Occupied || _bases[i].Runner == null)
                        continue;

                    int baseNumber = i + 1;
                    int targetBase = baseNumber + 1;
                    bool nextBaseOccupiedByRunnerAlsoStealing = targetBase <= 3 &&
                        _bases[targetBase - 1].Occupied &&
                        candidates.Any(c => c.BaseNumber == targetBase);
                    if (targetBase <= 3 && _bases[targetBase - 1].Occupied && !nextBaseOccupiedByRunnerAlsoStealing)
                        continue;

                    candidates.Add(new BaseSlot
                    {
                        BaseNumber = baseNumber,
                        Occupied = true,
                        Runner = _bases[i].Runner,
                        Earned = _bases[i].Earned,
                        ResponsiblePitcherId = _bases[i].ResponsiblePitcherId
                    });
                }

                return candidates;
            }

            private Player? DefensivePlayer(string position)
            {
                var card = LineupEngine.BuildLineupCard(DefenseTeam);
                if (card.DefensiveAssignments.TryGetValue(position, out Player? assigned) && assigned != null)
                    return assigned;

                return DefenseTeam?.Roster?
                    .Where(p => InjuryEngine.IsAvailable(p) && !p.RedshirtActive)
                    .FirstOrDefault(p => HasPosition(p, position))
                    ?? DefenseTeam?.Roster?.Where(InjuryEngine.IsAvailable).OrderByDescending(p => p.Fielding).FirstOrDefault();
            }

            private Player? DefensivePlayerForBase(int baseNumber)
            {
                return baseNumber switch
                {
                    2 => DefensivePlayer("2B") ?? DefensivePlayer("SS"),
                    3 => DefensivePlayer("3B"),
                    4 => DefensivePlayer("C"),
                    _ => DefensivePlayer("1B")
                };
            }

            private Player? RandomDefender()
            {
                var defenders = DefenseTeam?.Roster?.Where(InjuryEngine.IsAvailable).ToList() ?? new List<Player>();
                return defenders.Count == 0 ? null : defenders[_rng.Next(defenders.Count)];
            }

            private PitcherUse CreatePitcherUse(Team team, Player? player, bool starter)
            {
                player ??= team.Roster?.FirstOrDefault() ?? new Player { Name = "Emergency Pitcher", Role = PlayerRole.Pitcher };
                return new PitcherUse
                {
                    Team = team,
                    Player = player,
                    Starter = starter,
                    AvailablePitchLimit = starter ? StarterPitchLimit(player, team) : 32
                };
            }

            private static int StarterPitchLimit(Player pitcher, Team team)
            {
                int baseLimit = pitcher?.CareerPitchCount > 0 ? pitcher.CareerPitchCount : SeniorStarterPitchLimitFallback;
                double multiplier = pitcher?.Classification switch
                {
                    PlayerClassification.Freshman => 0.70,
                    PlayerClassification.Sophomore => 0.80,
                    PlayerClassification.Junior => 0.90,
                    PlayerClassification.Senior => 1.00,
                    _ => 1.00
                };
                int classifiedLimit = Math.Max(1, (int)Math.Round(baseLimit * multiplier));
                return PitchingRotationEngine.ApplyStarterPitchCountPenalty(pitcher, team, classifiedLimit);
            }

            private static List<Player> BuildLineup(Team team)
                => LineupEngine.GetBattingOrder(team).ToList();

            private static List<Player> BuildPitchingStaff(Team team)
                => LineupEngine.GetPitchingStaff(team).ToList();

            private static int BasesForHit(LiveHitType hitType)
            {
                return hitType switch
                {
                    LiveHitType.Double => 2,
                    LiveHitType.Triple => 3,
                    LiveHitType.HomeRun => 4,
                    _ => 1
                };
            }

            private static string HitText(LiveHitType hitType)
            {
                return hitType switch
                {
                    LiveHitType.Double => "a double",
                    LiveHitType.Triple => "a triple",
                    LiveHitType.HomeRun => "a home run",
                    _ => "a single"
                };
            }

            private static string BaseLabel(int baseNumber)
            {
                return baseNumber switch
                {
                    1 => "first",
                    2 => "second",
                    3 => "third",
                    4 => "home",
                    _ => "the next base"
                };
            }

            private int TeamFielding(Team team)
            {
                var card = LineupEngine.BuildLineupCard(team);
                var ratings = (card.DefensiveAssignments ?? new Dictionary<string, Player>(StringComparer.OrdinalIgnoreCase))
                    .Where(pair => pair.Value != null)
                    .Select(pair => PositionFieldingRating(pair.Value, pair.Key))
                    .ToList();
                int rating = ratings.Count == 0 ? 50 : (int)ratings.Average();
                return _rankingModifier.Apply(team, rating);
            }

            private static bool HasPosition(Player player, string position)
                => LineupEngine.HasPosition(player, position);

            private int Rating(Player? player, Func<Player, int> selector, int fallback)
            {
                if (player == null)
                    return fallback;
                int rating = InjuryEngine.EffectiveRating(player, selector(player));
                return RankingGameModifier.Apply(rating, _rankingModifier.BoostForTeam(TeamForPlayer(player)));
            }

            private int PositionFieldingRating(Player? player, string assignedPosition)
            {
                if (player == null)
                    return 50;
                int rating = FieldingDevelopmentEngine.EffectiveRating(player);
                rating = LineupEngine.ApplyPositionFieldingPenalty(player, assignedPosition, rating);
                return RankingGameModifier.Apply(rating, _rankingModifier.BoostForTeam(TeamForPlayer(player)));
            }

            private Team? TeamForPlayer(Player? player)
            {
                if (player == null)
                    return null;
                if (_away?.Roster?.Any(p => p != null && p.Id == player.Id) == true)
                    return _away;
                if (_home?.Roster?.Any(p => p != null && p.Id == player.Id) == true)
                    return _home;
                return null;
            }

            private static int PositiveModulo(int value, int divisor)
            {
                if (divisor <= 0)
                    return 0;
                int result = value % divisor;
                return result < 0 ? result + divisor : result;
            }

            private static bool HasStats(PlayerGameLine line)
            {
                return line != null && (line.R != 0 || line.AB != 0 || line.H != 0 || line.Doubles != 0 ||
                    line.Triples != 0 || line.HR != 0 || line.RBI != 0 || line.BB != 0 || line.IBB != 0 || line.SO != 0 ||
                    line.SB != 0 || line.CS != 0 || line.HBP != 0 || line.SH != 0 || line.SF != 0 ||
                    line.FlyOuts != 0 || line.GroundOuts != 0 || line.PopOuts != 0 || line.IPOuts != 0 ||
                    line.GroundedIntoDoublePlays != 0 ||
                    line.ReachedOnError != 0 ||
                    line.ER != 0 || line.K != 0 || line.HitsAllowed != 0 || line.WalksAllowed != 0 || line.IntentionalWalksAllowed != 0 ||
                    line.RunsAllowed != 0 || line.DoublesAllowed != 0 || line.TriplesAllowed != 0 ||
                    line.HomeRunsAllowed != 0 || line.HitBatters != 0 || line.BattersFaced != 0 ||
                    line.WildPitches != 0 ||
                    line.Balks != 0 ||
                    line.PitchCount != 0 || line.Wins != 0 || line.Losses != 0 || line.Saves != 0 || line.Holds != 0 || line.BlownSaves != 0 ||
                    line.CompleteGames != 0 || line.Shutouts != 0 ||
                    line.Putouts != 0 || line.Assists != 0 || line.Errors != 0 || line.DefensiveOuts != 0 ||
                    line.DefensiveDoublePlays != 0 || line.TeamDoublePlaysTurned != 0 ||
                    line.PassedBalls != 0 || line.StolenBasesAllowed != 0 || line.CatcherCaughtStealing != 0 ||
                    line.GamesMissedInjury != 0);
            }

            private void Log(string narration)
            {
                _events.Add(new SimulatedGameEvent
                {
                    Inning = _inning,
                    TopHalf = _topHalf,
                    Outs = _outs,
                    AwayScore = _result.AwayScore,
                    HomeScore = _result.HomeScore,
                    Bases = BaseStateText(),
                    Narration = narration ?? ""
                });
            }

            private string BaseStateText()
            {
                if (!_bases.Any(baseState => baseState.Occupied))
                    return "Bases empty";

                var occupied = new List<string>();
                if (_bases[0].Occupied) occupied.Add("1B");
                if (_bases[1].Occupied) occupied.Add("2B");
                if (_bases[2].Occupied) occupied.Add("3B");
                return string.Join(", ", occupied);
            }
        }

        private enum LiveHitType
        {
            Single,
            Double,
            Triple,
            HomeRun
        }

        private sealed class BaseSlot
        {
            public int BaseNumber { get; set; }
            public bool Occupied { get; set; }
            public Player? Runner { get; set; }
            public bool Earned { get; set; } = true;
            public Guid ResponsiblePitcherId { get; set; }

            public BaseSlot Copy(int baseNumber = 0)
                => new BaseSlot
                {
                    BaseNumber = baseNumber == 0 ? BaseNumber : baseNumber,
                    Occupied = Occupied,
                    Runner = Runner,
                    Earned = Earned,
                    ResponsiblePitcherId = ResponsiblePitcherId
                };

            public void Clear()
            {
                BaseNumber = 0;
                Occupied = false;
                Runner = null;
                Earned = true;
                ResponsiblePitcherId = Guid.Empty;
            }
        }

        private sealed class PitcherUse
        {
            public Team Team { get; set; }
            public Player Player { get; set; }
            public bool Starter { get; set; }
            public bool EnteredInSaveSituation { get; set; }
            public bool EnteredWithThreeRunLead { get; set; }
            public bool EnteredWithTyingRunThreat { get; set; }
            public bool LeadPreserved { get; set; } = true;
            public int PitchCount { get; set; }
            public int AvailablePitchLimit { get; set; }
            public int Outs { get; set; }
            public int OutsThisInning { get; set; }
            public int RunsThisInning { get; set; }
            public int EarnedRunsThisInning { get; set; }
            public int PostLimitBaserunnersThisInning { get; set; }
            public int ConsecutiveScorelessOuts { get; set; }
            public Dictionary<int, int> RunsAllowedByInning { get; } = new Dictionary<int, int>();
            public Dictionary<int, int> EarnedRunsAllowedByInning { get; } = new Dictionary<int, int>();

            public void ResetHalfInning()
            {
                OutsThisInning = 0;
                RunsThisInning = 0;
                EarnedRunsThisInning = 0;
                PostLimitBaserunnersThisInning = 0;
            }
        }
    }
}
