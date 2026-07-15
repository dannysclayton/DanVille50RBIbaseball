#nullable enable annotations

using System;
using System.Collections.Generic;
using System.Linq;

namespace StandaloneBaseball
{
    public static class GameplayRules
    {
        public static GameplayEvent ApplyPitchOutcome(GameplayState state, PitchOutcome outcome)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (outcome == null) throw new ArgumentNullException(nameof(outcome));
            EnsurePlayable(state);

            var gameEvent = GameplayEvent.FromState(state);
            gameEvent.PitchType = outcome.Type;

            switch (outcome.Type)
            {
                case PitchOutcomeType.Ball:
                    state.Count.Balls++;
                    if (state.Count.Balls >= 4)
                        Merge(gameEvent, ApplyAtBatResult(state, AtBatResult.Walk()));
                    break;

                case PitchOutcomeType.CalledStrike:
                case PitchOutcomeType.SwingingStrike:
                    state.Count.Strikes++;
                    if (state.Count.Strikes >= 3)
                        Merge(gameEvent, ApplyAtBatResult(state, AtBatResult.Strikeout()));
                    break;

                case PitchOutcomeType.Foul:
                    if (state.Count.Strikes < 2)
                        state.Count.Strikes++;
                    break;

                case PitchOutcomeType.InPlay:
                    if (outcome.AtBatResult == null)
                        throw new ArgumentException("In-play pitch outcomes require an at-bat result.", nameof(outcome));
                    Merge(gameEvent, ApplyAtBatResult(state, outcome.AtBatResult));
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(outcome), outcome.Type, "Unknown pitch outcome.");
            }

            Refresh(gameEvent, state);
            return gameEvent;
        }

        public static GameplayEvent ApplyAtBatResult(GameplayState state, AtBatResult result)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (result == null) throw new ArgumentNullException(nameof(result));
            EnsurePlayable(state);

            int scoreBefore = state.BattingTeamScore;
            int outsBefore = state.Count.Outs;
            var gameEvent = GameplayEvent.FromState(state);
            gameEvent.AtBatType = result.Type;
            gameEvent.AtBatCompleted = true;

            int availableOuts = Math.Max(0, 3 - state.Count.Outs);
            int outsRecorded = Math.Min(Math.Max(0, result.OutsRecorded), availableOuts);
            state.Count.Outs += outsRecorded;

            if (result.Type == AtBatResultType.DoublePlay)
                RemoveLeadForcedRunner(state);

            if (state.Count.Outs < 3)
                ApplyRunnerAdvancement(state, result);

            ApplyCourtesyRunners(state);
            AdvanceBatter(state);
            state.Count.ResetAtBat();

            if (state.Count.Outs >= 3)
                AdvanceHalfInning(state, scoreBefore, gameEvent);
            else if (IsWalkOffComplete(state))
            {
                state.IsComplete = true;
                gameEvent.GameCompleted = true;
            }

            gameEvent.RunsScored += state.BattingTeamScore - scoreBefore;
            gameEvent.OutsRecorded += state.Count.Outs >= 3 && gameEvent.HalfInningAdvanced ? 3 - outsBefore : outsRecorded;
            Refresh(gameEvent, state);
            return gameEvent;
        }

        public static Player? GetLineupPlayer(Team team, int batterIndex)
        {
            var lineup = GetLineup(team);
            if (lineup.Count == 0)
                return null;
            int index = PositiveModulo(batterIndex, lineup.Count);
            return lineup[index];
        }

        public static Player? GetPitcher(Team team, int pitcherIndex)
            => LineupEngine.GetPitcher(team, pitcherIndex);

        public static int FindStartingPitcherIndex(Team team)
            => LineupEngine.FindStartingPitcherIndex(team);

        public static IReadOnlyList<Player> GetLineup(Team team)
            => LineupEngine.GetBattingOrder(team);

        public static IReadOnlyList<Player> GetExtraInningRunnerCandidates(GameplayState state)
        {
            return GetRunnerCandidates(state, null, excludePitchersCatchers: false);
        }

        private static void ApplyRunnerAdvancement(GameplayState state, AtBatResult result)
        {
            if (result.BasesAwarded <= 0)
                return;

            if (result.AdvanceForcedRunnersOnly)
            {
                ForceAdvance(state, BaseRunner.FromBatter(state));
                return;
            }

            AdvanceAllRunners(state, result.BasesAwarded, result.AdvancesBatter ? BaseRunner.FromBatter(state) : null);
        }

        private static void RemoveLeadForcedRunner(GameplayState state)
        {
            if (state.Bases.First != null)
                state.Bases.First = null;
            else if (state.Bases.Second != null)
                state.Bases.Second = null;
            else if (state.Bases.Third != null)
                state.Bases.Third = null;
        }

        private static void ForceAdvance(GameplayState state, BaseRunner batter)
        {
            var bases = state.Bases;

            if (bases.First == null)
            {
                bases.First = batter;
                return;
            }

            if (bases.Second == null)
            {
                bases.Second = bases.First;
                bases.First = batter;
                return;
            }

            if (bases.Third == null)
            {
                bases.Third = bases.Second;
                bases.Second = bases.First;
                bases.First = batter;
                return;
            }

            state.AddRuns(1);
            bases.Third = bases.Second;
            bases.Second = bases.First;
            bases.First = batter;
        }

        private static void AdvanceAllRunners(GameplayState state, int basesAwarded, BaseRunner? batter)
        {
            int runs = 0;
            var placements = new Dictionary<int, BaseRunner>();
            MoveRunner(state.Bases.Third, 3, basesAwarded, placements, ref runs);
            MoveRunner(state.Bases.Second, 2, basesAwarded, placements, ref runs);
            MoveRunner(state.Bases.First, 1, basesAwarded, placements, ref runs);
            MoveRunner(batter, 0, basesAwarded, placements, ref runs);

            state.Bases.First = placements.TryGetValue(1, out var first) ? first : null;
            state.Bases.Second = placements.TryGetValue(2, out var second) ? second : null;
            state.Bases.Third = placements.TryGetValue(3, out var third) ? third : null;
            state.AddRuns(runs);
        }

        private static void MoveRunner(BaseRunner? runner, int fromBase, int basesAwarded, Dictionary<int, BaseRunner> placements, ref int runs)
        {
            if (runner == null)
                return;

            int target = fromBase + basesAwarded;
            if (target >= 4)
                runs++;
            else
                placements[target] = runner;
        }

        private static void ApplyCourtesyRunners(GameplayState state)
        {
            if (!state.CourtesyRunnerForPitchersCatchers)
                return;

            foreach (var slot in BaseSlots(state).Where(s => s.Runner?.Player != null && s.Runner.CourtesyForPlayer == null).ToList())
            {
                BaseRunner? currentRunner = slot.Runner;
                if (currentRunner?.Player == null)
                    continue;
                Player protectedPlayer = currentRunner.Player;
                if (!IsPitcherOrCatcher(protectedPlayer))
                    continue;

                var candidates = GetRunnerCandidates(state, protectedPlayer, excludePitchersCatchers: true);
                Player? selected = candidates.FirstOrDefault();
                if (selected == null)
                    continue;

                slot.Assign(new BaseRunner
                {
                    Player = selected,
                    Team = state.BattingTeam,
                    BatterIndex = -1,
                    CourtesyForPlayer = protectedPlayer
                });
                MarkPinchUse(state, selected);
            }
        }

        private static void AdvanceBatter(GameplayState state)
        {
            var lineup = state.GetBattingOrder(state.BattingTeam);
            if (lineup.Count == 0)
                return;

            state.CurrentBatterIndex = PositiveModulo(state.CurrentBatterIndex + 1, lineup.Count);
        }

        private static void AdvanceHalfInning(GameplayState state, int scoreBefore, GameplayEvent gameEvent)
        {
            gameEvent.HalfInningAdvanced = true;
            state.CompletedHalfInnings.Add(new HalfInningSnapshot
            {
                Inning = state.Inning,
                Half = state.Half,
                BattingTeamId = state.BattingTeam?.Id ?? Guid.Empty,
                RunsScored = state.BattingTeamScore - scoreBefore,
                AwayScore = state.AwayScore,
                HomeScore = state.HomeScore
            });

            state.Count.ResetHalfInning();
            state.Bases.Clear();

            if (ApplyMercyRule(state, state.Half))
            {
                gameEvent.GameCompleted = true;
                return;
            }

            if (state.Half == HalfInning.Top)
            {
                state.Half = HalfInning.Bottom;
                if (state.Inning >= state.RegulationInnings && state.HomeScore > state.AwayScore)
                    state.IsComplete = true;
            }
            else
            {
                if (state.Inning >= state.RegulationInnings && state.HomeScore != state.AwayScore)
                {
                    state.IsComplete = true;
                }
                else if (state.Inning >= state.RegulationInnings && !state.AllowExtraInnings)
                {
                    state.IsComplete = true;
                }
                else
                {
                    state.Inning++;
                    state.Half = HalfInning.Top;
                }
            }

            gameEvent.GameCompleted = state.IsComplete;
            if (!state.IsComplete)
                ApplyExtraInningRunner(state);
        }

        private static void ApplyExtraInningRunner(GameplayState state)
        {
            if (!state.AllowExtraInnings || !state.ExtraInningRunnerOnSecond)
                return;
            if (state.Inning <= state.RegulationInnings)
                return;

            var lineup = state.GetBattingOrder(state.BattingTeam);
            var candidates = GetExtraInningRunnerCandidates(state);
            if (lineup.Count == 0 && candidates.Count == 0)
                return;

            Player? runner = null;
            if (state.PendingExtraInningRunnerId.HasValue)
                runner = candidates.FirstOrDefault(p => p.Id == state.PendingExtraInningRunnerId.Value);
            runner ??= candidates.FirstOrDefault();
            if (runner == null && lineup.Count > 0)
                runner = lineup[PositiveModulo(state.CurrentBatterIndex - 1, lineup.Count)];
            if (runner == null)
                return;

            int runnerIndex = lineup.Count == 0 ? -1 : lineup.ToList().FindIndex(p => p.Id == runner.Id);
            state.Bases.Second = new BaseRunner
            {
                Player = runner,
                Team = state.BattingTeam,
                BatterIndex = runnerIndex
            };
            MarkPinchUse(state, runner);
            state.PendingExtraInningRunnerId = null;
        }

        private static IReadOnlyList<Player> GetRunnerCandidates(GameplayState state, Player? protectedPlayer, bool excludePitchersCatchers)
        {
            if (state?.BattingTeam?.Roster == null)
                return Array.Empty<Player>();

            var blocked = NextScheduledBatterIds(state, 8);
            var removed = new HashSet<Guid>(state.RemovedPlayerIds ?? new List<Guid>());
            var occupied = BaseSlots(state)
                .Select(s => s.Runner?.Player?.Id)
                .Where(id => id.HasValue)
                .Select(id => id.GetValueOrDefault())
                .ToHashSet();

            var candidates = state.BattingTeam.Roster
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

            return state.BattingTeam.Roster
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

        private static IEnumerable<BaseSlot> BaseSlots(GameplayState state)
        {
            if (state?.Bases == null)
                yield break;

            yield return new BaseSlot(() => state.Bases.First, value => state.Bases.First = value);
            yield return new BaseSlot(() => state.Bases.Second, value => state.Bases.Second = value);
            yield return new BaseSlot(() => state.Bases.Third, value => state.Bases.Third = value);
        }

        private sealed class BaseSlot
        {
            private readonly Func<BaseRunner?> _get;
            private readonly Action<BaseRunner?> _set;

            public BaseSlot(Func<BaseRunner?> get, Action<BaseRunner?> set)
            {
                _get = get;
                _set = set;
            }

            public BaseRunner? Runner => _get();
            public void Assign(BaseRunner? runner) => _set(runner);
        }

        private static bool IsPitcherOrCatcher(Player player)
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

        private static HashSet<Guid> NextScheduledBatterIds(GameplayState state, int atBats)
        {
            var blocked = new HashSet<Guid>();
            var lineup = state.GetBattingOrder(state.BattingTeam);
            if (lineup.Count == 0)
                return blocked;

            for (int i = 0; i < atBats; i++)
                blocked.Add(lineup[PositiveModulo(state.CurrentBatterIndex + i, lineup.Count)].Id);
            return blocked;
        }

        private static void MarkPinchUse(GameplayState state, Player player)
        {
            if (state.PinchUseCounts == null)
                state.PinchUseCounts = new Dictionary<Guid, int>();
            if (state.RemovedPlayerIds == null)
                state.RemovedPlayerIds = new List<Guid>();

            state.PinchUseCounts.TryGetValue(player.Id, out int uses);
            uses++;
            state.PinchUseCounts[player.Id] = uses;
            if (uses >= 2 && !state.RemovedPlayerIds.Contains(player.Id))
                state.RemovedPlayerIds.Add(player.Id);
        }

        private static bool ApplyMercyRule(GameplayState state, HalfInning completedHalf)
        {
            if (!state.MercyRuleEnabled)
                return false;
            if (state.MercyRuleRuns < 1)
                state.MercyRuleRuns = 10;
            if (state.MercyRuleMinimumInning < 1)
                state.MercyRuleMinimumInning = 5;
            if (state.Inning < state.MercyRuleMinimumInning)
                return false;

            int lead = Math.Abs(state.HomeScore - state.AwayScore);
            if (lead < state.MercyRuleRuns)
                return false;

            bool homeCanEndAfterTop = completedHalf == HalfInning.Top && state.HomeScore > state.AwayScore;
            bool eitherCanEndAfterBottom = completedHalf == HalfInning.Bottom && state.HomeScore != state.AwayScore;
            if (!homeCanEndAfterTop && !eitherCanEndAfterBottom)
                return false;

            state.IsComplete = true;
            state.EndedByMercyRule = true;
            return true;
        }

        private static bool IsWalkOffComplete(GameplayState state)
        {
            return state.Half == HalfInning.Bottom
                && state.Inning >= state.RegulationInnings
                && state.HomeScore > state.AwayScore;
        }

        private static void EnsurePlayable(GameplayState state)
        {
            if (state.IsComplete)
                throw new InvalidOperationException("The game is already complete.");
            if (state.AwayTeam == null)
                throw new InvalidOperationException("Away team is required.");
            if (state.HomeTeam == null)
                throw new InvalidOperationException("Home team is required.");
            if (state.Count == null)
                state.Count = new CountState();
            if (state.Bases == null)
                state.Bases = new BaseState();
            if (state.PinchUseCounts == null)
                state.PinchUseCounts = new Dictionary<Guid, int>();
            if (state.RemovedPlayerIds == null)
                state.RemovedPlayerIds = new List<Guid>();
            if (state.RegulationInnings < 1)
                state.RegulationInnings = 1;
            if (state.MercyRuleRuns < 1)
                state.MercyRuleRuns = 10;
            if (state.MercyRuleMinimumInning < 1)
                state.MercyRuleMinimumInning = 5;
        }

        private static int PositiveModulo(int value, int divisor)
        {
            if (divisor <= 0)
                return 0;

            int result = value % divisor;
            return result < 0 ? result + divisor : result;
        }

        private static void Merge(GameplayEvent target, GameplayEvent source)
        {
            target.AtBatType = source.AtBatType;
            target.AtBatCompleted = target.AtBatCompleted || source.AtBatCompleted;
            target.HalfInningAdvanced = target.HalfInningAdvanced || source.HalfInningAdvanced;
            target.GameCompleted = target.GameCompleted || source.GameCompleted;
            target.RunsScored += source.RunsScored;
            target.OutsRecorded += source.OutsRecorded;
        }

        private static void Refresh(GameplayEvent gameEvent, GameplayState state)
        {
            gameEvent.AwayScore = state.AwayScore;
            gameEvent.HomeScore = state.HomeScore;
            gameEvent.Inning = state.Inning;
            gameEvent.Half = state.Half;
            gameEvent.Balls = state.Count.Balls;
            gameEvent.Strikes = state.Count.Strikes;
            gameEvent.Outs = state.Count.Outs;
            gameEvent.GameCompleted = state.IsComplete;
        }
    }
}
