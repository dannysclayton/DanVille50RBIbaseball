using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

#nullable enable annotations

namespace StandaloneBaseball
{
    internal sealed class ReplayValidationReport
    {
        public List<string> Errors { get; } = new List<string>();
        public List<string> Warnings { get; } = new List<string>();
        public bool IsValid => Errors.Count == 0;
    }

    internal static class ReplayExactEngine
    {
        public static ReplayValidationReport Validate(ReplayFile replay)
        {
            var report = new ReplayValidationReport();
            if (replay == null)
            {
                report.Errors.Add("Replay data is missing.");
                return report;
            }
            if (replay.ReplaySchemaVersion < 2)
                return report;
            if (!replay.Deterministic)
                report.Errors.Add("Schema version 2 replays must set deterministic to true.");
            if (replay.StartingState == null)
                report.Errors.Add("starting_state is required for an exact replay.");
            if (replay.FinalState == null)
                report.Errors.Add("final_state is required for an exact replay.");
            if (replay.Events == null || replay.Events.Count == 0)
            {
                report.Errors.Add("At least one exact replay event is required.");
                return report;
            }

            int previousSequence = 0;
            long previousTime = -1;
            long previousEnd = -1;
            var eventIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> playerIds = PlayerIds(replay);
            if (playerIds.Count == 0)
                report.Errors.Add("Exact replay teams must list every player used by the game.");
            foreach (ReplayEvent replayEvent in replay.Events.Where(item => item != null))
            {
                string prefix = "Event " + (replayEvent.Sequence <= 0 ? "?" : replayEvent.Sequence.ToString());
                if (replayEvent.Sequence <= previousSequence)
                    report.Errors.Add(prefix + " sequence must be strictly increasing.");
                previousSequence = replayEvent.Sequence;
                if (string.IsNullOrWhiteSpace(replayEvent.EventId))
                    report.Errors.Add(prefix + " is missing event_id.");
                else if (!eventIds.Add(replayEvent.EventId))
                    report.Errors.Add(prefix + " repeats event_id " + replayEvent.EventId + ".");
                if (replayEvent.TimeMs < previousTime)
                    report.Errors.Add(prefix + " time_ms is earlier than the preceding event.");
                if (previousEnd >= 0 && replayEvent.TimeMs < previousEnd)
                    report.Errors.Add(prefix + " overlaps the preceding event; the runtime requires ordered event windows.");
                previousTime = replayEvent.TimeMs;
                if (replayEvent.DurationMs < 0)
                    report.Errors.Add(prefix + " duration_ms cannot be negative.");
                if (replayEvent.Before == null || replayEvent.After == null)
                    report.Errors.Add(prefix + " must provide before and after state snapshots.");
                if (replayEvent.Animation == null)
                    report.Errors.Add(prefix + " is missing animation data.");
                previousEnd = replayEvent.TimeMs + Math.Max(0, replayEvent.DurationMs);

                ReplayAnimation animation = replayEvent.Animation;
                if (animation != null)
                {
                    ValidatePath(animation.BallPath, replayEvent, prefix + " ball_path", report);
                    foreach (ReplayActorPath path in animation.FielderPaths ?? new List<ReplayActorPath>())
                    {
                        ValidatePlayerId(path?.PlayerId, playerIds, prefix + " fielder path", report);
                        ValidatePath(path?.Path, replayEvent, prefix + " fielder path", report);
                    }
                    foreach (ReplayRunnerPath path in animation.RunnerPaths ?? new List<ReplayRunnerPath>())
                    {
                        ValidatePlayerId(path?.PlayerId, playerIds, prefix + " runner path", report);
                        ValidatePath(path?.Path, replayEvent, prefix + " runner path", report);
                    }
                    foreach (ReplayThrowPath path in animation.Throws ?? new List<ReplayThrowPath>())
                    {
                        ValidatePlayerId(path?.FromPlayerId, playerIds, prefix + " throw source", report);
                        ValidatePlayerId(path?.ToPlayerId, playerIds, prefix + " throw target", report);
                        ValidatePath(path?.Path, replayEvent, prefix + " throw path", report);
                    }
                    foreach (long updateTime in animation.ScoreboardUpdatesAtMs ?? new List<long>())
                        ValidateTimestamp(updateTime, replayEvent, prefix + " scoreboard update", report);
                }

                if (replayEvent.Command?.Pitch != null && (animation?.BallPath?.Count ?? 0) < 2)
                    report.Errors.Add(prefix + " contains a pitch but does not provide at least two ball_path points.");
                ValidatePlayerId(replayEvent.Command?.Pitch?.PitcherId, playerIds, prefix + " pitcher", report);
                ValidatePlayerId(replayEvent.Command?.Pitch?.BatterId, playerIds, prefix + " batter", report);
                foreach (ReplayAudioCue cue in replayEvent.Audio ?? new List<ReplayAudioCue>())
                    ValidateTimestamp(cue?.StartTimeMs ?? -1, replayEvent, prefix + " audio cue", report);
                foreach (ReplayCutsceneCue cue in replayEvent.Cutscenes ?? new List<ReplayCutsceneCue>())
                    ValidateTimestamp(cue?.StartTimeMs ?? -1, replayEvent, prefix + " cutscene cue", report);
                ValidateStatePlayerIds(replayEvent.Before, playerIds, prefix + " before", report);
                ValidateStatePlayerIds(replayEvent.After, playerIds, prefix + " after", report);
                ValidateAfterSnapshot(replayEvent, prefix, report);
            }

            ValidateStatePlayerIds(replay.StartingState, playerIds, "starting_state", report);
            ValidateStatePlayerIds(replay.FinalState, playerIds, "final_state", report);

            return report;
        }

        public static ReplayRenderFrame CreateFrame(ReplayEvent replayEvent, long replayTimeMs)
        {
            var frame = new ReplayRenderFrame();
            if (replayEvent == null)
                return frame;

            long end = replayEvent.TimeMs + Math.Max(0, replayEvent.DurationMs);
            frame.Progress = replayEvent.DurationMs <= 0
                ? 1f
                : Math.Clamp((float)(replayTimeMs - replayEvent.TimeMs) / replayEvent.DurationMs, 0f, 1f);
            ReplayAnimation animation = replayEvent.Animation ?? new ReplayAnimation();
            var ballPoints = (animation.BallPath ?? new List<ReplayPathPoint>())
                .Concat((animation.Throws ?? new List<ReplayThrowPath>()).SelectMany(path => path?.Path ?? new List<ReplayPathPoint>()))
                .OrderBy(point => point.TimeMs)
                .ToList();
            ReplayPathPoint ball = Sample(ballPoints, Math.Clamp(replayTimeMs, replayEvent.TimeMs, end));
            if (ball != null)
            {
                frame.BallVisible = ball.Visible;
                frame.BallX = Clamp01(ball.X);
                frame.BallY = Clamp01(ball.Y);
                frame.BallZ = Math.Max(0f, ball.Z);
            }

            var highlighted = new HashSet<string>(animation.HighlightPlayerIds ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
            foreach (ReplayActorPath path in animation.FielderPaths ?? new List<ReplayActorPath>())
                AddActor(frame, path, replayTimeMs, runner: false, highlighted.Contains(path?.PlayerId ?? ""));
            foreach (ReplayRunnerPath path in animation.RunnerPaths ?? new List<ReplayRunnerPath>())
                AddActor(frame, path, replayTimeMs, runner: true, highlighted.Contains(path?.PlayerId ?? ""));
            return frame;
        }

        internal static ReplayPathPoint? Sample(IReadOnlyList<ReplayPathPoint>? points, long timeMs)
        {
            if (points == null || points.Count == 0)
                return null;
            if (timeMs <= points[0].TimeMs)
                return Clone(points[0]);
            if (timeMs >= points[^1].TimeMs)
                return Clone(points[^1]);

            for (int index = 1; index < points.Count; index++)
            {
                ReplayPathPoint right = points[index];
                if (timeMs > right.TimeMs)
                    continue;
                ReplayPathPoint left = points[index - 1];
                long span = Math.Max(1, right.TimeMs - left.TimeMs);
                float amount = Math.Clamp((float)(timeMs - left.TimeMs) / span, 0f, 1f);
                return new ReplayPathPoint
                {
                    TimeMs = timeMs,
                    X = Lerp(left.X, right.X, amount),
                    Y = Lerp(left.Y, right.Y, amount),
                    Z = Lerp(left.Z, right.Z, amount),
                    Visible = amount < 0.5f ? left.Visible : right.Visible
                };
            }
            return Clone(points[^1]);
        }

        private static void AddActor(ReplayRenderFrame frame, ReplayActorPath? actorPath, long timeMs, bool runner, bool highlighted)
        {
            if (actorPath is not { } path)
                return;
            ReplayPathPoint? point = Sample(path.Path, timeMs);
            if (point == null)
                return;
            frame.Actors.Add(new ReplayRenderActor
            {
                PlayerId = path.PlayerId,
                DefensivePosition = path.Position,
                X = Clamp01(point.X),
                Y = Clamp01(point.Y),
                Runner = runner,
                Highlighted = highlighted
            });
        }

        private static void ValidatePath(IReadOnlyList<ReplayPathPoint>? points, ReplayEvent replayEvent, string label, ReplayValidationReport report)
        {
            if (points == null)
                return;
            long previous = long.MinValue;
            long eventEnd = replayEvent.TimeMs + Math.Max(0, replayEvent.DurationMs);
            foreach (ReplayPathPoint point in points.Where(item => item != null))
            {
                if (point.TimeMs < previous)
                    report.Errors.Add(label + " timestamps must be increasing.");
                if (point.TimeMs < replayEvent.TimeMs || point.TimeMs > eventEnd)
                    report.Errors.Add(label + " contains a timestamp outside its event window.");
                if (point.X < 0f || point.X > 1f || point.Y < 0f || point.Y > 1f)
                    report.Errors.Add(label + " coordinates must be normalized from 0 through 1.");
                previous = point.TimeMs;
            }
        }

        private static void ValidateTimestamp(long timeMs, ReplayEvent replayEvent, string label, ReplayValidationReport report)
        {
            long eventEnd = replayEvent.TimeMs + Math.Max(0, replayEvent.DurationMs);
            if (timeMs < replayEvent.TimeMs || timeMs > eventEnd)
                report.Errors.Add(label + " timestamp is outside its event window.");
        }

        private static HashSet<string> PlayerIds(ReplayFile replay)
        {
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ReplayTeam? team in new[] { replay?.Teams?.Away, replay?.Teams?.Home }.Where(team => team != null))
            {
                if (team == null)
                    continue;
                foreach (ReplayLineupSlot slot in team.Lineup ?? new List<ReplayLineupSlot>())
                    Add(slot?.Player?.PlayerId);
                foreach (ReplayPlayer player in team.Bench ?? new List<ReplayPlayer>())
                    Add(player?.PlayerId);
                foreach (ReplayPlayer player in team.PitchingStaff ?? new List<ReplayPlayer>())
                    Add(player?.PlayerId);
            }
            return ids;

            void Add(string? playerId)
            {
                if (!string.IsNullOrWhiteSpace(playerId))
                    ids.Add(playerId);
            }
        }

        private static void ValidateStatePlayerIds(ReplayGameState? state, HashSet<string> playerIds, string label, ReplayValidationReport report)
        {
            if (state == null)
                return;
            ValidatePlayerId(state.CurrentBatterId, playerIds, label + " current batter", report);
            ValidatePlayerId(state.CurrentPitcherId, playerIds, label + " current pitcher", report);
            ValidatePlayerId(state.Bases?.First?.PlayerId, playerIds, label + " first-base runner", report);
            ValidatePlayerId(state.Bases?.Second?.PlayerId, playerIds, label + " second-base runner", report);
            ValidatePlayerId(state.Bases?.Third?.PlayerId, playerIds, label + " third-base runner", report);
            foreach (ReplayStateFielder fielder in state.Fielders ?? new List<ReplayStateFielder>())
                ValidatePlayerId(fielder?.PlayerId, playerIds, label + " fielder", report);
        }

        private static void ValidatePlayerId(string? playerId, HashSet<string> playerIds, string label, ReplayValidationReport report)
        {
            if (!string.IsNullOrWhiteSpace(playerId) && !playerIds.Contains(playerId))
                report.Errors.Add(label + " references unknown player_id " + playerId + ".");
        }

        private static void ValidateAfterSnapshot(ReplayEvent replayEvent, string prefix, ReplayValidationReport report)
        {
            if (replayEvent.After == null || replayEvent.Validation == null)
                return;
            ReplayValidation validation = replayEvent.Validation;
            if (validation.ScoreAfter != null &&
                (validation.ScoreAfter.Away != replayEvent.After.Score?.Away || validation.ScoreAfter.Home != replayEvent.After.Score?.Home))
                report.Errors.Add(prefix + " validation score_after does not match after.score.");
            if (validation.OutsAfter.HasValue && validation.OutsAfter.Value != replayEvent.After.Outs)
                report.Errors.Add(prefix + " validation outs_after does not match after.outs.");
            if (validation.BasesAfter != null)
            {
                if (!Same(validation.BasesAfter.FirstPlayerId, replayEvent.After.Bases?.First?.PlayerId) ||
                    !Same(validation.BasesAfter.SecondPlayerId, replayEvent.After.Bases?.Second?.PlayerId) ||
                    !Same(validation.BasesAfter.ThirdPlayerId, replayEvent.After.Bases?.Third?.PlayerId))
                    report.Errors.Add(prefix + " validation bases_after does not match after.bases.");
            }
        }

        private static bool Same(string? left, string? right)
            => string.Equals(left ?? "", right ?? "", StringComparison.OrdinalIgnoreCase);
        private static float Lerp(float start, float end, float amount) => start + (end - start) * amount;
        private static float Clamp01(float value) => Math.Clamp(value, 0f, 1f);
        private static ReplayPathPoint Clone(ReplayPathPoint point) => new ReplayPathPoint
        {
            TimeMs = point.TimeMs,
            X = point.X,
            Y = point.Y,
            Z = point.Z,
            Visible = point.Visible
        };
    }
}
