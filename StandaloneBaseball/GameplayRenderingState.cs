#nullable enable annotations

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace StandaloneBaseball
{
    internal enum GameplayRenderingPhase
    {
        Ready,
        Pitching,
        BallInPlay,
        DeadBall
    }

    internal enum GameplayCameraPhase
    {
        AtBat,
        BallTracking,
        ThrowToBase,
        ClosePlay
    }

    internal enum GameplayBallFlightType
    {
        None,
        Pitch,
        GroundBall,
        LineDrive,
        FlyBall,
        HomeRun,
        Throw
    }

    internal enum GameplayPresentationKind
    {
        None,
        BaseHit,
        Steal,
        Strikeout
    }

    internal sealed class GameplayRenderingPlayerMarker
    {
        public string Label { get; set; } = "";
        public string Detail { get; set; } = "";
        public PointF Position { get; set; }
        public Color Color { get; set; } = Color.White;
        public bool Runner { get; set; }
        public Player? Player { get; set; }
        public Team? Team { get; set; }
    }

    internal sealed class GameplayRenderingBaseState
    {
        public string Label { get; set; } = "";
        public bool Occupied { get; set; }
        public Color RunnerColor { get; set; } = Color.White;
        public Player? Player { get; set; }
        public Team? Team { get; set; }
        public Player? CourtesyForPlayer { get; set; }
        public Guid ResponsiblePitcherId { get; set; }
        public bool Earned { get; set; } = true;
    }

    internal sealed class GameplayScoredRunner
    {
        public Player? Player { get; set; }
        public Guid ResponsiblePitcherId { get; set; }
        public bool Earned { get; set; }
    }

    internal sealed class GameplayRenderingGameState
    {
        public Team AwayTeam { get; private set; } = new Team();
        public Team HomeTeam { get; private set; } = new Team();
        public int AwayScore { get; set; }
        public int HomeScore { get; set; }
        public int Inning { get; set; } = 1;
        public bool TopHalf { get; set; } = true;
        public int Balls { get; set; }
        public int Strikes { get; set; }
        public int Outs { get; set; }
        public int RegulationInnings { get; set; } = 9;
        public bool AllowExtraInnings { get; set; } = true;
        public bool MercyRuleEnabled { get; set; } = true;
        public int MercyRuleRuns { get; set; } = 10;
        public int MercyRuleMinimumInning { get; set; } = 5;
        public bool ExtraInningRunnerOnSecond { get; set; } = true;
        public bool CourtesyRunnerForPitchersCatchers { get; set; } = true;
        public Guid? AwayUniformSetId { get; set; }
        public Guid? HomeUniformSetId { get; set; }
        public string AwayLogoPath { get; set; } = "";
        public string HomeLogoPath { get; set; } = "";
        public int AwayBatterIndex { get; set; }
        public int HomeBatterIndex { get; set; }
        public int AwayPitcherIndex { get; set; }
        public int HomePitcherIndex { get; set; }
        public Guid? AwayEmergencyPitcherId { get; set; }
        public Guid? HomeEmergencyPitcherId { get; set; }
        public List<Guid> AwayLineupPlayerIds { get; } = new List<Guid>();
        public List<Guid> HomeLineupPlayerIds { get; } = new List<Guid>();
        public List<GameLineupEntry> AwayStartingLineup { get; set; } = new List<GameLineupEntry>();
        public List<GameLineupEntry> HomeStartingLineup { get; set; } = new List<GameLineupEntry>();
        public Guid? AwayDesignatedHitterId { get; set; }
        public Guid? HomeDesignatedHitterId { get; set; }
        public bool AwayDhActive { get; set; }
        public bool HomeDhActive { get; set; }
        public Dictionary<Guid, int> PinchUseCounts { get; } = new Dictionary<Guid, int>();
        public List<Guid> RemovedPlayerIds { get; } = new List<Guid>();
        public string ModeLabel { get; set; } = "Ready";
        public string PitchTypeLabel { get; set; } = "Fastball";
        public BaseballFieldPreset FieldPreset { get; set; } = BaseballFieldPresets.Default;
        public GameplayRenderingPhase Phase { get; set; } = GameplayRenderingPhase.Ready;
        public GameplayCameraPhase CameraPhase { get; set; } = GameplayCameraPhase.AtBat;
        public GameplayBallFlightType BallFlightType { get; set; } = GameplayBallFlightType.None;
        public GameplayPresentationKind PresentationKind { get; set; } = GameplayPresentationKind.None;
        public float PresentationProgress { get; set; }
        public int PresentationFromBase { get; set; }
        public int PresentationTargetBase { get; set; }
        public bool PresentationSuccessful { get; set; }
        public string PresentationVariant { get; set; } = "";
        public PointF BallPosition { get; set; } = new PointF(0.5f, 0.62f);
        public PointF CameraFocus { get; set; } = new PointF(0.5f, 0.70f);
        public PointF ThrowTarget { get; set; } = new PointF(0.64f, 0.72f);
        public int BatterTargetBase { get; set; } = 1;
        public float BallHeight { get; set; }
        public bool BallVisible { get; set; } = true;
        public float BallTrail { get; set; }
        public float AnimationProgress { get; set; }
        public string ControlHint { get; set; } = "";
        public int ActiveFielderIndex { get; set; } = 1;
        public List<GameplayRenderingPlayerMarker> Fielders { get; } = new List<GameplayRenderingPlayerMarker>();
        public List<GameplayRenderingPlayerMarker> ReplayActors { get; } = new List<GameplayRenderingPlayerMarker>();
        private readonly List<GameplayScoredRunner> _scoredRunners = new List<GameplayScoredRunner>();
        public GameplayRenderingBaseState[] Bases { get; } =
        {
            new GameplayRenderingBaseState(),
            new GameplayRenderingBaseState(),
            new GameplayRenderingBaseState()
        };

        public string AwayName => TeamName(AwayTeam, "AWAY");
        public string HomeName => TeamName(HomeTeam, "HOME");
        public Color AwayPrimary => TeamColor(AwayTeam, Color.FromArgb(40, 90, 180), true);
        public Color HomePrimary => TeamColor(HomeTeam, Color.FromArgb(200, 60, 55), true);
        public Color DefenseColor => TopHalf ? HomePrimary : AwayPrimary;
        public Color OffenseColor => TopHalf ? AwayPrimary : HomePrimary;
        public Team BattingTeam => TopHalf ? AwayTeam : HomeTeam;
        public Team FieldingTeam => TopHalf ? HomeTeam : AwayTeam;
        public Guid? CurrentEmergencyPitcherId
        {
            get => TopHalf ? HomeEmergencyPitcherId : AwayEmergencyPitcherId;
            set
            {
                if (TopHalf) HomeEmergencyPitcherId = value;
                else AwayEmergencyPitcherId = value;
            }
        }

        public int CurrentPitcherIndex
        {
            get => TopHalf ? HomePitcherIndex : AwayPitcherIndex;
            set
            {
                if (TopHalf) HomePitcherIndex = value;
                else AwayPitcherIndex = value;
            }
        }

        public int CurrentBatterIndex
        {
            get => TopHalf ? AwayBatterIndex : HomeBatterIndex;
            set
            {
                if (TopHalf) AwayBatterIndex = value;
                else HomeBatterIndex = value;
            }
        }

        public void SetTeams(Team away, Team home)
        {
            AwayTeam = away;
            HomeTeam = home;
            InitializeLineups();
            SeedFielders();
            ClearBases();
        }

        public void SetUniforms(Guid? awayUniformSetId, Guid? homeUniformSetId)
        {
            AwayUniformSetId = awayUniformSetId;
            HomeUniformSetId = homeUniformSetId;
            SeedFielders();
            foreach (var baseState in Bases.Where(b => b.Occupied && b.Team != null))
                baseState.RunnerColor = TeamColor(baseState.Team, baseState.RunnerColor, true);
        }

        public void SetTeamLogos(string awayLogoPath, string homeLogoPath)
        {
            AwayLogoPath = awayLogoPath ?? "";
            HomeLogoPath = homeLogoPath ?? "";
        }

        public TeamUniformSet? UniformForTeam(Team? team)
        {
            if (team == null)
                return null;
            if (AwayTeam != null && team.Id == AwayTeam.Id)
                return GameUniformResolver.ResolveUniform(team, homeRole: false, AwayUniformSetId);
            if (HomeTeam != null && team.Id == HomeTeam.Id)
                return GameUniformResolver.ResolveUniform(team, homeRole: true, HomeUniformSetId);
            return team.DefaultUniform();
        }

        public void InitializeLineups()
        {
            ApplyLineupCard(AwayTeam, AwayLineupPlayerIds, away: true);
            ApplyLineupCard(HomeTeam, HomeLineupPlayerIds, away: false);
            AwayStartingLineup = LineupEngine.CaptureStartingLineup(AwayTeam);
            HomeStartingLineup = LineupEngine.CaptureStartingLineup(HomeTeam);
        }

        public void SeedFielders()
        {
            Fielders.Clear();
            Color color = DefenseColor;
            string[] labels = { "C", "P", "1B", "2B", "SS", "3B", "LF", "CF", "RF" };
            PointF[] positions =
            {
                new PointF(0.5f, 0.90f),
                new PointF(0.5f, 0.62f),
                new PointF(0.68f, 0.70f),
                new PointF(0.59f, 0.62f),
                new PointF(0.41f, 0.62f),
                new PointF(0.32f, 0.70f),
                new PointF(0.25f, 0.38f),
                new PointF(0.5f, 0.28f),
                new PointF(0.75f, 0.38f)
            };

            var card = LineupEngine.BuildLineupCard(FieldingTeam);
            for (int i = 0; i < labels.Length; i++)
            {
                Player? player = i == 1 ? CurrentPitcherPlayer() : null;
                if (player == null)
                    card.DefensiveAssignments.TryGetValue(labels[i], out player);
                string detail = player?.Name ?? labels[i];
                Fielders.Add(new GameplayRenderingPlayerMarker
                {
                    Label = labels[i],
                    Detail = detail,
                    Position = positions[i],
                    Color = color,
                    Player = player,
                    Team = FieldingTeam
                });
            }
        }

        public Player? CurrentPitcherPlayer()
        {
            Player? emergency = FieldingTeam?.Roster?.FirstOrDefault(p => p.Id == CurrentEmergencyPitcherId);
            return emergency ?? (FieldingTeam == null ? null : GameplayRules.GetPitcher(FieldingTeam, CurrentPitcherIndex));
        }

        public void ClearBases()
        {
            foreach (GameplayRenderingBaseState baseState in Bases)
            {
                baseState.Label = "";
                baseState.Occupied = false;
                baseState.RunnerColor = OffenseColor;
                baseState.Player = null;
                baseState.Team = null;
                baseState.CourtesyForPlayer = null;
                baseState.ResponsiblePitcherId = Guid.Empty;
                baseState.Earned = true;
            }
        }

        public void SetBaseRunner(int baseNumber, Player? player, Team? team)
        {
            if (baseNumber < 1 || baseNumber > 3)
                throw new ArgumentOutOfRangeException(nameof(baseNumber), "Base number must be 1, 2, or 3.");

            GameplayRenderingBaseState baseState = Bases[baseNumber - 1];
            baseState.Occupied = player != null;
            baseState.Label = player?.Name ?? "";
            baseState.RunnerColor = TeamColor(team, OffenseColor, true);
            baseState.Player = player;
            baseState.Team = team;
            baseState.CourtesyForPlayer = null;
            baseState.ResponsiblePitcherId = CurrentPitcherPlayer()?.Id ?? Guid.Empty;
            baseState.Earned = true;
        }

        public void AdvanceRunners(bool batterSafe, Random rng, bool batterEarned = true)
        {
            var scoringRunner = Bases[2].Occupied ? ScoredRunner(Bases[2]) : null;
            CopyBase(Bases[1], Bases[2]);
            CopyBase(Bases[0], Bases[1]);
            if (batterSafe)
            {
                SetBase(Bases[0], CurrentBatter(), BattingTeam);
                Bases[0].Earned = batterEarned;
            }
            else
                ClearBase(Bases[0]);

            if (scoringRunner != null)
            {
                _scoredRunners.Add(scoringRunner);
                if (TopHalf) AwayScore++;
                else HomeScore++;
            }
        }

        public void AdvanceRunnersTwoBasesWithSingle()
        {
            AdvanceExtraBaseHit(1, runnerAdvanceBases: 2);
        }

        public void AdvanceExtraBaseHit(int basesAwarded)
        {
            AdvanceExtraBaseHit(basesAwarded, runnerAdvanceBases: basesAwarded, excludedRunnerId: null);
        }

        public void AdvanceExtraBaseHitExcludingRunner(int basesAwarded, Guid excludedRunnerId)
        {
            AdvanceExtraBaseHit(basesAwarded, runnerAdvanceBases: basesAwarded, excludedRunnerId: excludedRunnerId);
        }

        private void AdvanceExtraBaseHit(int batterBasesAwarded, int runnerAdvanceBases, Guid? excludedRunnerId = null)
        {
            var runners = Bases
                .Select((baseState, index) => new
                {
                    Index = index,
                    baseState.Label,
                    baseState.Occupied,
                    baseState.RunnerColor,
                    baseState.Player,
                    baseState.Team,
                    baseState.CourtesyForPlayer,
                    baseState.ResponsiblePitcherId,
                    baseState.Earned
                })
                .Where(baseState => baseState.Occupied)
                .Where(baseState => !excludedRunnerId.HasValue || baseState.Player == null || baseState.Player.Id != excludedRunnerId.Value)
                .ToList();

            int runs = 0;
            ClearBases();
            foreach (var runner in runners)
            {
                int target = runner.Index + runnerAdvanceBases;
                if (target >= Bases.Length)
                {
                    runs++;
                    _scoredRunners.Add(new GameplayScoredRunner
                    {
                        Player = runner.Player,
                        ResponsiblePitcherId = runner.ResponsiblePitcherId,
                        Earned = runner.Earned
                    });
                    continue;
                }

                Bases[target].Label = runner.Label;
                Bases[target].Occupied = true;
                Bases[target].RunnerColor = runner.RunnerColor;
                Bases[target].Player = runner.Player;
                Bases[target].Team = runner.Team;
                Bases[target].CourtesyForPlayer = runner.CourtesyForPlayer;
                Bases[target].ResponsiblePitcherId = runner.ResponsiblePitcherId;
                Bases[target].Earned = runner.Earned;
            }

            int batterBaseIndex = Math.Clamp(batterBasesAwarded, 1, 3) - 1;
            SetBase(Bases[batterBaseIndex], CurrentBatter(), BattingTeam);
            if (TopHalf) AwayScore += runs;
            else HomeScore += runs;
        }

        public int OccupiedBaseCount()
            => Bases.Count(baseState => baseState.Occupied);

        public void AdvanceHomeRun()
        {
            int runs = OccupiedBaseCount() + 1;
            foreach (var baseState in Bases.Where(b => b.Occupied))
                _scoredRunners.Add(ScoredRunner(baseState));
            _scoredRunners.Add(new GameplayScoredRunner
            {
                Player = CurrentBatter(),
                ResponsiblePitcherId = CurrentPitcherPlayer()?.Id ?? Guid.Empty,
                Earned = true
            });
            ClearBases();
            if (TopHalf) AwayScore += runs;
            else HomeScore += runs;
        }

        public IReadOnlyList<GameplayScoredRunner> DrainScoredRunners()
        {
            var result = _scoredRunners.ToList();
            _scoredRunners.Clear();
            return result;
        }

        public void ScoreRunnerFromBase(int baseNumber)
        {
            if (baseNumber < 1 || baseNumber > 3 || !Bases[baseNumber - 1].Occupied)
                return;
            var baseState = Bases[baseNumber - 1];
            _scoredRunners.Add(ScoredRunner(baseState));
            ClearBase(baseState);
            if (TopHalf) AwayScore++; else HomeScore++;
        }

        public void ScoreRunner(Player player, Guid responsiblePitcherId, bool earned)
        {
            _scoredRunners.Add(new GameplayScoredRunner
            {
                Player = player,
                ResponsiblePitcherId = responsiblePitcherId,
                Earned = earned
            });
            if (TopHalf) AwayScore++; else HomeScore++;
        }

        public void ApplyCourtesyRunners(Func<Team, Player, IReadOnlyList<Player>, IReadOnlyDictionary<Guid, int>, Player?>? chooseCourtesyRunner)
        {
            if (!CourtesyRunnerForPitchersCatchers)
                return;

            foreach (var baseState in Bases.Where(b => b.Occupied && b.Player != null && b.CourtesyForPlayer == null).ToList())
            {
                Player? protectedPlayer = baseState.Player;
                if (protectedPlayer == null || !IsPitcherOrCatcher(protectedPlayer))
                    continue;

                var candidates = GetRunnerCandidates(protectedPlayer, excludePitchersCatchers: true);
                Player? selected = chooseCourtesyRunner?.Invoke(BattingTeam, protectedPlayer, candidates, PinchUseCounts);
                if (selected == null || !candidates.Any(p => p.Id == selected.Id))
                    selected = candidates.FirstOrDefault();
                if (selected == null)
                    continue;

                Guid responsiblePitcherId = baseState.ResponsiblePitcherId;
                bool earned = baseState.Earned;
                SetBase(baseState, selected, BattingTeam);
                baseState.ResponsiblePitcherId = responsiblePitcherId;
                baseState.Earned = earned;
                baseState.CourtesyForPlayer = protectedPlayer;
                MarkPinchUse(selected);
            }
        }

        public void NextHalfInning(Random? rng = null, Func<Team, IReadOnlyList<Player>, IReadOnlyDictionary<Guid, int>, Player?>? chooseExtraRunner = null)
        {
            Balls = 0;
            Strikes = 0;
            Outs = 0;
            ClearBases();
            TopHalf = !TopHalf;
            if (TopHalf)
                Inning++;
            if (ExtraInningRunnerOnSecond && Inning > Math.Max(1, RegulationInnings))
            {
                var candidates = GetExtraInningRunnerCandidates();
                Player? selected = chooseExtraRunner?.Invoke(BattingTeam, candidates, PinchUseCounts);
                if (selected == null || !candidates.Any(p => p.Id == selected.Id))
                    selected = candidates.FirstOrDefault();
                PlaceExtraInningRunner(selected, rng);
            }
            SeedFielders();
            BallPosition = new PointF(0.5f, 0.62f);
            Phase = GameplayRenderingPhase.Ready;
            ModeLabel = TopHalf ? "Top " + Inning : "Bottom " + Inning;
        }

        public IReadOnlyList<Player> GetExtraInningRunnerCandidates()
            => GetRunnerCandidates(null, excludePitchersCatchers: false);

        public Player? CurrentBatterPlayer()
            => CurrentBatter();

        public bool LoseDesignatedHitterForFieldingTeam(Player newPitcher)
        {
            return LoseDesignatedHitterForTeam(FieldingTeam, newPitcher);
        }

        public bool EnsurePitcherBatsForFieldingTeam(Player newPitcher, Player replacedPitcher)
        {
            return EnsurePitcherBatsForTeam(FieldingTeam, newPitcher, replacedPitcher);
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

        public bool EnsurePitcherBatsForTeam(Team team, Player newPitcher, Player replacedPitcher)
        {
            if (team == null || newPitcher == null)
                return false;

            bool away = team.Id == AwayTeam?.Id;
            bool active = away ? AwayDhActive : HomeDhActive;
            if (active)
                return false;

            var lineup = away ? AwayLineupPlayerIds : HomeLineupPlayerIds;
            if (lineup == null || lineup.Count == 0 || lineup.Contains(newPitcher.Id))
                return false;

            int slot = replacedPitcher == null ? -1 : lineup.FindIndex(id => id == replacedPitcher.Id);
            Guid? dhId = away ? AwayDesignatedHitterId : HomeDesignatedHitterId;
            if (slot < 0 && dhId.HasValue)
                slot = lineup.FindIndex(id => id == dhId.Value);
            if (slot < 0)
                slot = lineup.Count - 1;

            lineup[slot] = newPitcher.Id;
            return true;
        }

        private IReadOnlyList<Player> GetRunnerCandidates(Player? protectedPlayer, bool excludePitchersCatchers)
        {
            var team = BattingTeam;
            if (team?.Roster == null)
                return Array.Empty<Player>();

            var blocked = NextScheduledBatterIds(8);
            var removed = new HashSet<Guid>(RemovedPlayerIds);
            var occupied = Bases
                .Where(b => b.Occupied && b.Player != null)
                .Select(b => b.Player)
                .OfType<Player>()
                .Select(player => player.Id)
                .ToHashSet();
            var candidates = team.Roster
                .Where(p => p != null)
                .Where(p => protectedPlayer == null || p.Id != protectedPlayer.Id)
                .Where(p => !blocked.Contains(p.Id))
                .Where(p => !removed.Contains(p.Id))
                .Where(p => !occupied.Contains(p.Id))
                .Where(p => !excludePitchersCatchers || !IsPitcherOrCatcher(p))
                .Where(InjuryEngine.IsAvailable)
                .OrderByDescending(p => p.Speed)
                .ThenByDescending(p => p.Overall)
                .ToList();

            if (candidates.Count > 0)
                return candidates;

            return team.Roster
                .Where(p => p != null)
                .Where(p => protectedPlayer == null || p.Id != protectedPlayer.Id)
                .Where(p => !blocked.Contains(p.Id))
                .Where(p => !removed.Contains(p.Id))
                .Where(p => !occupied.Contains(p.Id))
                .Where(p => !excludePitchersCatchers || !IsPitcherOrCatcher(p))
                .OrderByDescending(p => p.Speed)
                .ThenByDescending(p => p.Overall)
                .ToList();
        }

        public void AdvanceBatter()
        {
            var lineup = CurrentLineup();
            if (lineup.Count == 0)
                return;
            CurrentBatterIndex = PositiveModulo(CurrentBatterIndex + 1, lineup.Count);
        }

        private void PlaceExtraInningRunner(Player? player, Random? rng)
        {
            player ??= GetExtraInningRunnerCandidates().FirstOrDefault();
            GameplayRenderingBaseState second = Bases[1];
            SetBase(second, player, BattingTeam);
            second.Earned = false;
            if (player == null)
                second.Label = NextBatterName(rng ?? new Random());
            if (player != null)
                MarkPinchUse(player);
        }

        private void MarkPinchUse(Player player)
        {
            PinchUseCounts.TryGetValue(player.Id, out int uses);
            uses++;
            PinchUseCounts[player.Id] = uses;
            if (uses >= 2 && !RemovedPlayerIds.Contains(player.Id))
                RemovedPlayerIds.Add(player.Id);
        }

        private HashSet<Guid> NextScheduledBatterIds(int atBats)
        {
            var blocked = new HashSet<Guid>();
            var lineup = CurrentLineup();
            if (lineup.Count == 0)
                return blocked;

            for (int i = 0; i < atBats; i++)
                blocked.Add(lineup[PositiveModulo(CurrentBatterIndex + i, lineup.Count)].Id);
            return blocked;
        }

        private List<Player> CurrentLineup()
        {
            if (BattingTeam?.Roster == null || BattingTeam.Roster.Count == 0)
                return new List<Player>();

            var ids = BattingTeam.Id == AwayTeam?.Id ? AwayLineupPlayerIds : HomeLineupPlayerIds;
            if (ids.Count == 0)
                return LineupEngine.GetBattingOrder(BattingTeam).ToList();

            return ids
                .Select(id => BattingTeam.Roster.FirstOrDefault(p => p != null && p.Id == id))
                .OfType<Player>()
                .ToList();
        }

        private Player? CurrentBatter()
        {
            var lineup = CurrentLineup();
            if (lineup.Count == 0)
                return null;
            return lineup[PositiveModulo(CurrentBatterIndex, lineup.Count)];
        }

        private static void CopyBase(GameplayRenderingBaseState source, GameplayRenderingBaseState target)
        {
            target.Label = source.Label;
            target.Occupied = source.Occupied;
            target.RunnerColor = source.RunnerColor;
            target.Player = source.Player;
            target.Team = source.Team;
            target.CourtesyForPlayer = source.CourtesyForPlayer;
            target.ResponsiblePitcherId = source.ResponsiblePitcherId;
            target.Earned = source.Earned;
        }

        private void SetBase(GameplayRenderingBaseState baseState, Player? player, Team? team)
        {
            baseState.Occupied = player != null;
            baseState.Label = player?.Name ?? "";
            baseState.RunnerColor = TeamColor(team, OffenseColor, true);
            baseState.Player = player;
            baseState.Team = team;
            baseState.CourtesyForPlayer = null;
            baseState.ResponsiblePitcherId = CurrentPitcherPlayer()?.Id ?? Guid.Empty;
            baseState.Earned = true;
        }

        private void ClearBase(GameplayRenderingBaseState baseState)
        {
            baseState.Occupied = false;
            baseState.Label = "";
            baseState.RunnerColor = OffenseColor;
            baseState.Player = null;
            baseState.Team = null;
            baseState.CourtesyForPlayer = null;
            baseState.ResponsiblePitcherId = Guid.Empty;
            baseState.Earned = true;
        }

        private static GameplayScoredRunner ScoredRunner(GameplayRenderingBaseState baseState)
            => new GameplayScoredRunner
            {
                Player = baseState.Player,
                ResponsiblePitcherId = baseState.ResponsiblePitcherId,
                Earned = baseState.Earned
            };

        private static bool IsPitcherOrCatcher(Player? player)
        {
            if (player == null)
                return false;
            if (player.Role == PlayerRole.Pitcher)
                return true;

            return (player.Positions ?? "")
                .Split(new[] { '/', ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Any(p => string.Equals(p.Trim(), "C", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(p.Trim(), "P", StringComparison.OrdinalIgnoreCase));
        }

        public void ResetCount()
        {
            Balls = 0;
            Strikes = 0;
        }

        public string NextBatterName(Random rng)
        {
            List<Player> hitters = CurrentLineup();

            if (hitters.Count == 0)
                return "Runner";

            return hitters[rng.Next(hitters.Count)].Name;
        }

        private static int PositiveModulo(int value, int divisor)
        {
            if (divisor <= 0)
                return 0;

            int result = value % divisor;
            return result < 0 ? result + divisor : result;
        }

        private static string TeamName(Team? team, string fallback)
            => string.IsNullOrWhiteSpace(team?.ScoreboardName) ? fallback : team.ScoreboardName;

        private Color TeamColor(Team? team, Color fallback, bool primary)
        {
            if (team == null)
                return fallback;

            var uniform = UniformForTeam(team);
            int argb = primary
                ? uniform?.JerseyArgb ?? team.PrimaryArgb
                : uniform?.CapHelmetArgb ?? team.SecondaryArgb;
            return Color.FromArgb(argb);
        }

        private void ApplyLineupCard(Team? team, List<Guid> target, bool away)
        {
            target.Clear();
            if (team == null)
            {
                if (away)
                {
                    AwayDesignatedHitterId = null;
                    AwayDhActive = false;
                }
                else
                {
                    HomeDesignatedHitterId = null;
                    HomeDhActive = false;
                }
                return;
            }

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
    }
}
