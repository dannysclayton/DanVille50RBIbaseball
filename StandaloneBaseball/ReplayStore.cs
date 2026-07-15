#nullable enable annotations

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace StandaloneBaseball
{
    public static class ReplayStore
    {
        public const string Extension = ".rbi-replay.json";
        public static string DefaultReplayFolder => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DanVille50",
            "Dan's RBI Baseball 2026",
            "Replays");

        public static string BundledTemplatePath => Path.Combine(
            AppContext.BaseDirectory,
            "Assets",
            "Replay Templates",
            "ReplayTemplate" + Extension);

        public static string BundledSchemaPath => Path.Combine(AppContext.BaseDirectory, "ExactReplaySchema.md");

        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };

        public static string EnsureReplayFolder(string? folder = null)
        {
            string fullPath = Path.GetFullPath(string.IsNullOrWhiteSpace(folder) ? DefaultReplayFolder : folder);
            Directory.CreateDirectory(fullPath);
            return fullPath;
        }

        public static IReadOnlyList<string> LibraryReplayFiles(string? folder = null)
        {
            string replayFolder = EnsureReplayFolder(folder);
            return Directory.EnumerateFiles(replayFolder, "*.json", SearchOption.TopDirectoryOnly)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .ThenBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static string ImportToLibrary(string sourcePath, string? folder = null)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
                throw new ArgumentException("Select a replay file to import.", nameof(sourcePath));

            string source = Path.GetFullPath(sourcePath);
            if (!File.Exists(source))
                throw new FileNotFoundException("The selected replay file does not exist.", source);

            // A readable snapshot or best-effort replay is valid for import. Only malformed files are blocked.
            Load(source);

            string replayFolder = EnsureReplayFolder(folder);
            if (IsInsideLibrary(source, replayFolder))
                return source;

            string targetName = CanonicalReplayFileName(Path.GetFileName(source));
            string target = UniqueTargetPath(replayFolder, targetName);
            File.Copy(source, target, overwrite: false);
            return target;
        }

        public static void SaveBundledTemplate(string destinationPath)
        {
            if (!File.Exists(BundledTemplatePath))
                throw new FileNotFoundException("The packaged replay template is missing.", BundledTemplatePath);
            if (string.IsNullOrWhiteSpace(destinationPath))
                throw new ArgumentException("Choose where to save the replay template.", nameof(destinationPath));

            string destination = Path.GetFullPath(destinationPath);
            string? parent = Path.GetDirectoryName(destination);
            if (!string.IsNullOrWhiteSpace(parent))
                Directory.CreateDirectory(parent);
            File.Copy(BundledTemplatePath, destination, overwrite: true);

            if (File.Exists(BundledSchemaPath) && !string.IsNullOrWhiteSpace(parent))
            {
                string guideDestination = Path.Combine(parent, "ExactReplaySchema.md");
                if (!string.Equals(Path.GetFullPath(BundledSchemaPath), Path.GetFullPath(guideDestination), StringComparison.OrdinalIgnoreCase))
                    File.Copy(BundledSchemaPath, guideDestination, overwrite: true);
            }
        }

        public static void DeleteLibraryReplay(string? path, string? folder = null)
        {
            string replayFolder = EnsureReplayFolder(folder);
            string fullPath = Path.GetFullPath(path ?? "");
            if (!IsInsideLibrary(fullPath, replayFolder))
                throw new InvalidOperationException("Only replay files inside the managed replay library can be removed.");
            if (File.Exists(fullPath))
                File.Delete(fullPath);
        }

        private static string CanonicalReplayFileName(string? sourceName)
        {
            string name = sourceName ?? "replay";
            if (name.EndsWith(Extension, StringComparison.OrdinalIgnoreCase))
                return SanitizeFileName(name);

            string stem = Path.GetFileNameWithoutExtension(name);
            if (string.IsNullOrWhiteSpace(stem))
                stem = "replay";
            return SanitizeFileName(stem) + Extension;
        }

        private static string SanitizeFileName(string value)
        {
            var invalid = Path.GetInvalidFileNameChars().ToHashSet();
            string cleaned = new string((value ?? "").Select(character => invalid.Contains(character) ? '_' : character).ToArray()).Trim();
            return string.IsNullOrWhiteSpace(cleaned) ? "replay" + Extension : cleaned;
        }

        private static string UniqueTargetPath(string folder, string fileName)
        {
            string candidate = Path.Combine(folder, fileName);
            if (!File.Exists(candidate))
                return candidate;

            string stem = fileName.EndsWith(Extension, StringComparison.OrdinalIgnoreCase)
                ? fileName.Substring(0, fileName.Length - Extension.Length)
                : Path.GetFileNameWithoutExtension(fileName);
            for (int copy = 2; ; copy++)
            {
                candidate = Path.Combine(folder, stem + " (" + copy + ")" + Extension);
                if (!File.Exists(candidate))
                    return candidate;
            }
        }

        private static bool IsInsideLibrary(string path, string folder)
        {
            string fullPath = Path.GetFullPath(path);
            string root = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                          + Path.DirectorySeparatorChar;
            return fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase);
        }

        public static ReplayFile Load(string path)
        {
            var replay = JsonSerializer.Deserialize<ReplayFile>(File.ReadAllText(path), Options)
                         ?? new ReplayFile();
            if (!IsRecognizableReplay(replay))
                throw new InvalidDataException("The selected JSON file does not contain recognizable replay data.");
            replay.SourceDirectory = Path.GetDirectoryName(Path.GetFullPath(path)) ?? "";
            replay.Events ??= new System.Collections.Generic.List<ReplayEvent>();
            replay.PlayLog ??= new System.Collections.Generic.List<string>();
            replay.DetailedPlayLog ??= new System.Collections.Generic.List<string>();
            replay.Teams ??= new ReplayTeams();
            replay.Teams.Away ??= new ReplayTeam();
            replay.Teams.Home ??= new ReplayTeam();
            replay.Game ??= new ReplayGameInfo();
            replay.Rules ??= new ReplayRules();
            replay.Assets ??= new ReplayAssets();
            replay.Assets.Audio = replay.Assets.Audio == null
                ? new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, JsonElement>(replay.Assets.Audio, StringComparer.OrdinalIgnoreCase);
            replay.Assets.Cutscenes = replay.Assets.Cutscenes == null
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(replay.Assets.Cutscenes, StringComparer.OrdinalIgnoreCase);
            replay.Validation ??= new ReplayValidation();
            foreach (ReplayEvent replayEvent in replay.Events.Where(item => item != null))
            {
                replayEvent.Audio ??= new List<ReplayAudioCue>();
                replayEvent.Cutscenes ??= new List<ReplayCutsceneCue>();
                replayEvent.RunnerAdvancements ??= new List<ReplayRunnerAdvancement>();
                replayEvent.Validation ??= new ReplayValidation();
                if (replayEvent.Animation != null)
                {
                    replayEvent.Animation.BallPath ??= new List<ReplayPathPoint>();
                    replayEvent.Animation.FielderPaths ??= new List<ReplayActorPath>();
                    replayEvent.Animation.RunnerPaths ??= new List<ReplayRunnerPath>();
                    replayEvent.Animation.Throws ??= new List<ReplayThrowPath>();
                    replayEvent.Animation.HighlightPlayerIds ??= new List<string>();
                    replayEvent.Animation.ScoreboardUpdatesAtMs ??= new List<long>();
                }
            }

            ReplayValidationReport report = ReplayExactEngine.Validate(replay);
            replay.ReplayIssues = report.Errors
                .Concat(report.Warnings)
                .Where(message => !string.IsNullOrWhiteSpace(message))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            RepairForBestEffortPlayback(replay);
            if (replay.ReplaySchemaVersion >= 2)
            {
                var missingAudio = replay.Events
                    .SelectMany(replayEvent => replayEvent?.Audio ?? new List<ReplayAudioCue>())
                    .Where(cue => string.IsNullOrWhiteSpace(ResolveAudioCuePath(replay, cue)))
                    .Select(cue => cue.Cue)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (missingAudio.Count > 0)
                    replay.ReplayIssues.Add("Audio assets unavailable; those cues will be skipped: " + string.Join(", ", missingAudio));

                var ambiguousAudio = replay.Events
                    .SelectMany(replayEvent => replayEvent?.Audio ?? new List<ReplayAudioCue>())
                    .Where(cue => string.IsNullOrWhiteSpace(cue?.File) && AudioAssetChoiceCount(replay, cue?.AssetKey) > 1)
                    .Select(cue => cue.Cue)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (ambiguousAudio.Count > 0)
                    replay.ReplayIssues.Add("Audio selection was not recorded; the first available clip will be used: " + string.Join(", ", ambiguousAudio));

                var missingCutscenes = replay.Events
                    .SelectMany(replayEvent => replayEvent?.Cutscenes ?? new List<ReplayCutsceneCue>())
                    .Where(cue => string.IsNullOrWhiteSpace(ResolveCutsceneCuePath(replay, cue)))
                    .Select(cue => cue.Trigger)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (missingCutscenes.Count > 0)
                    replay.ReplayIssues.Add("Cutscene assets unavailable; those cutscenes will be skipped: " + string.Join(", ", missingCutscenes));
            }
            replay.ReplayIssues = replay.ReplayIssues
                .Where(message => !string.IsNullOrWhiteSpace(message))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            return replay;
        }

        private static bool IsRecognizableReplay(ReplayFile replay)
        {
            if (replay == null)
                return false;
            if (replay.ReplaySchemaVersion > 0)
                return true;
            if (!string.IsNullOrWhiteSpace(replay.Game?.GameId) ||
                !string.IsNullOrWhiteSpace(replay.Teams?.Away?.TeamName) ||
                !string.IsNullOrWhiteSpace(replay.Teams?.Home?.TeamName))
                return true;
            return (replay.Events?.Count ?? 0) > 0 ||
                   (replay.PlayLog?.Count ?? 0) > 0 ||
                   (replay.DetailedPlayLog?.Count ?? 0) > 0;
        }

        private static void RepairForBestEffortPlayback(ReplayFile replay)
        {
            if (replay == null || replay.ReplaySchemaVersion < 2)
                return;

            replay.Events = (replay.Events ?? new List<ReplayEvent>()).Where(item => item != null).ToList();
            ReplayEvent? first = replay.Events.FirstOrDefault(item => item != null);
            replay.StartingState ??= first?.Before ?? StateFromEvent(first, null);
            ReplayGameState? previous = replay.StartingState;
            int generatedSequence = 1;
            foreach (ReplayEvent replayEvent in replay.Events.Where(item => item != null))
            {
                replayEvent.TimeMs = Math.Max(0, replayEvent.TimeMs);
                replayEvent.DurationMs = Math.Max(0, replayEvent.DurationMs);
                if (replayEvent.Sequence <= 0)
                    replayEvent.Sequence = generatedSequence;
                generatedSequence = Math.Max(generatedSequence + 1, replayEvent.Sequence + 1);
                if (string.IsNullOrWhiteSpace(replayEvent.EventId))
                    replayEvent.EventId = "recovered-event-" + replayEvent.Sequence.ToString("D6");
                replayEvent.Before ??= previous ?? StateFromEvent(replayEvent, null);
                replayEvent.Animation ??= new ReplayAnimation();
                NormalizeEventTimeline(replayEvent);
                replayEvent.After ??= StateFromEvent(replayEvent, replayEvent.Before);
                previous = replayEvent.After;
            }
            replay.FinalState ??= previous ?? replay.StartingState ?? new ReplayGameState
            {
                Score = replay.Game?.FinalScore ?? new ReplayScore()
            };
        }

        private static void NormalizeEventTimeline(ReplayEvent replayEvent)
        {
            long start = replayEvent.TimeMs;
            long end = start + replayEvent.DurationMs;
            ReplayAnimation animation = replayEvent.Animation ??= new ReplayAnimation();
            animation.BallPath = NormalizePath(animation.BallPath, start, end);
            animation.FielderPaths = (animation.FielderPaths ?? new List<ReplayActorPath>())
                .Where(path => path != null)
                .Select(path =>
                {
                    path.Path = NormalizePath(path.Path, start, end);
                    return path;
                }).ToList();
            animation.RunnerPaths = (animation.RunnerPaths ?? new List<ReplayRunnerPath>())
                .Where(path => path != null)
                .Select(path =>
                {
                    path.Path = NormalizePath(path.Path, start, end);
                    return path;
                }).ToList();
            replayEvent.Animation.Throws = (replayEvent.Animation.Throws ?? new List<ReplayThrowPath>())
                .Where(path => path != null)
                .Select(path =>
                {
                    path.StartTimeMs = Math.Clamp(path.StartTimeMs, start, end);
                    path.CatchTimeMs = Math.Clamp(path.CatchTimeMs, path.StartTimeMs, end);
                    path.Path = NormalizePath(path.Path, start, end);
                    return path;
                }).ToList();
            replayEvent.Animation.ScoreboardUpdatesAtMs = (replayEvent.Animation.ScoreboardUpdatesAtMs ?? new List<long>())
                .Select(time => Math.Clamp(time, start, end))
                .OrderBy(time => time)
                .ToList();
            replayEvent.Audio = (replayEvent.Audio ?? new List<ReplayAudioCue>())
                .Where(cue => cue != null)
                .Select(cue =>
                {
                    cue.StartTimeMs = Math.Clamp(cue.StartTimeMs, start, end);
                    return cue;
                }).ToList();
            replayEvent.Cutscenes = (replayEvent.Cutscenes ?? new List<ReplayCutsceneCue>())
                .Where(cue => cue != null)
                .Select(cue =>
                {
                    cue.StartTimeMs = Math.Clamp(cue.StartTimeMs, start, end);
                    cue.DurationMs = Math.Max(0, cue.DurationMs);
                    return cue;
                }).ToList();
        }

        private static List<ReplayPathPoint> NormalizePath(IEnumerable<ReplayPathPoint> points, long start, long end)
            => (points ?? Enumerable.Empty<ReplayPathPoint>())
                .Where(point => point != null)
                .Select(point => new ReplayPathPoint
                {
                    TimeMs = Math.Clamp(point.TimeMs, start, end),
                    X = Math.Clamp(point.X, 0f, 1f),
                    Y = Math.Clamp(point.Y, 0f, 1f),
                    Z = Math.Max(0f, point.Z),
                    Visible = point.Visible
                })
                .OrderBy(point => point.TimeMs)
                .ToList();

        private static ReplayGameState StateFromEvent(ReplayEvent? replayEvent, ReplayGameState? fallback)
        {
            replayEvent ??= new ReplayEvent();
            fallback ??= new ReplayGameState();
            return new ReplayGameState
            {
                TimeMs = replayEvent.TimeMs > 0 ? replayEvent.TimeMs : fallback.TimeMs,
                Inning = replayEvent.Inning > 0 ? replayEvent.Inning : fallback.Inning,
                Half = string.IsNullOrWhiteSpace(replayEvent.Half) ? fallback.Half : replayEvent.Half,
                Outs = replayEvent.Outs > 0 ? replayEvent.Outs : fallback.Outs,
                Balls = fallback.Balls,
                Strikes = fallback.Strikes,
                Score = new ReplayScore
                {
                    Away = Math.Max(replayEvent.Score?.Away ?? 0, fallback.Score?.Away ?? 0),
                    Home = Math.Max(replayEvent.Score?.Home ?? 0, fallback.Score?.Home ?? 0)
                },
                Bases = new ReplayExactBases
                {
                    First = Occupant(replayEvent.Bases?.First, fallback.Bases?.First),
                    Second = Occupant(replayEvent.Bases?.Second, fallback.Bases?.Second),
                    Third = Occupant(replayEvent.Bases?.Third, fallback.Bases?.Third)
                },
                CurrentBatterId = fallback.CurrentBatterId,
                CurrentPitcherId = fallback.CurrentPitcherId,
                AwayBatterIndex = fallback.AwayBatterIndex,
                HomeBatterIndex = fallback.HomeBatterIndex,
                AwayPitcherIndex = fallback.AwayPitcherIndex,
                HomePitcherIndex = fallback.HomePitcherIndex,
                DhState = fallback.DhState ?? new ReplayDhState(),
                LiveRules = fallback.LiveRules ?? new ReplayLiveRules(),
                Fielders = fallback.Fielders ?? new List<ReplayStateFielder>()
            };
        }

        private static ReplayBaseOccupant? Occupant(ReplayPlayer? player, ReplayBaseOccupant? fallback)
        {
            if (player == null)
                return fallback;
            return new ReplayBaseOccupant { PlayerId = player.PlayerId };
        }

        internal static string ResolveAudioCuePath(ReplayFile? replay, ReplayAudioCue? cue)
        {
            string value = cue?.File ?? "";
            if (string.IsNullOrWhiteSpace(value) && replay?.Assets?.Audio != null &&
                replay.Assets.Audio.TryGetValue(cue?.AssetKey ?? "", out JsonElement element))
            {
                if (element.ValueKind == JsonValueKind.String)
                    value = element.GetString() ?? "";
                else if (element.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement item in element.EnumerateArray())
                    {
                        if (item.ValueKind != JsonValueKind.String)
                            continue;
                        value = item.GetString() ?? "";
                        if (!string.IsNullOrWhiteSpace(value))
                            break;
                    }
                }
            }
            return ResolveReplayPath(replay, value);
        }

        internal static string ResolveReplayPath(ReplayFile? replay, string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "";
            if (Path.IsPathRooted(value))
                return File.Exists(value) ? Path.GetFullPath(value) : "";
            foreach (string root in new[] { replay?.SourceDirectory, AppContext.BaseDirectory }
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .OfType<string>())
            {
                string candidate = Path.GetFullPath(Path.Combine(root, value));
                if (File.Exists(candidate))
                    return candidate;
            }
            return "";
        }

        internal static string ResolveCutsceneCuePath(ReplayFile? replay, ReplayCutsceneCue? cue)
            => ResolveReplayPath(replay, cue?.AssetPath ?? "");

        private static int AudioAssetChoiceCount(ReplayFile? replay, string? assetKey)
        {
            if (replay?.Assets?.Audio == null || string.IsNullOrWhiteSpace(assetKey) ||
                !replay.Assets.Audio.TryGetValue(assetKey, out JsonElement element))
                return 0;
            if (element.ValueKind == JsonValueKind.String)
                return string.IsNullOrWhiteSpace(element.GetString()) ? 0 : 1;
            if (element.ValueKind != JsonValueKind.Array)
                return 0;
            return element.EnumerateArray().Count(item => item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()));
        }
    }
}
