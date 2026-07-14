using System.Text.Json;
using StandaloneBaseball;

namespace StandaloneBaseball.Tests;

public sealed class ReplayExactEngineTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    [Fact]
    public void CreateFrame_InterpolatesBallAndRunnerAtRecordedTime()
    {
        ReplayEvent replayEvent = ValidReplay().Events.Single();

        ReplayRenderFrame frame = ReplayExactEngine.CreateFrame(replayEvent, 1500);

        Assert.True(frame.BallVisible);
        Assert.Equal(0.5f, frame.BallX, 3);
        Assert.Equal(0.4f, frame.BallY, 3);
        Assert.Equal(0.5f, frame.BallZ, 3);
        ReplayRenderActor runner = Assert.Single(frame.Actors);
        Assert.True(runner.Runner);
        Assert.Equal("runner-1", runner.PlayerId);
        Assert.Equal(0.5f, runner.X, 3);
    }

    [Fact]
    public void Validate_AcceptsCompleteDeterministicReplay()
    {
        ReplayValidationReport report = ReplayExactEngine.Validate(ValidReplay());

        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Errors));
    }

    [Fact]
    public void Validate_RejectsStateMismatchAndIncompletePitchPath()
    {
        ReplayFile replay = ValidReplay();
        ReplayEvent replayEvent = replay.Events.Single();
        replayEvent.Animation.BallPath.RemoveAt(1);
        replayEvent.Validation.ScoreAfter = new ReplayScore { Away = 4, Home = 3 };

        ReplayValidationReport report = ReplayExactEngine.Validate(replay);

        Assert.False(report.IsValid);
        Assert.Contains(report.Errors, message => message.Contains("two ball_path points", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.Errors, message => message.Contains("score_after", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ReplayStore_LoadsExactReplayAndResolvesRelativeAudioAsset()
    {
        string directory = TemporaryDirectory();
        try
        {
            string audioPath = Path.Combine(directory, "pitch.mp3");
            File.WriteAllBytes(audioPath, new byte[] { 1, 2, 3 });
            ReplayFile replay = ValidReplay();
            replay.Assets.Audio["pitch_throw"] = JsonSerializer.SerializeToElement("pitch.mp3");
            replay.Events[0].Audio.Add(new ReplayAudioCue
            {
                Cue = "pitch_throw",
                AssetKey = "pitch_throw",
                StartTimeMs = 1000
            });
            string path = WriteReplay(directory, replay);

            ReplayFile loaded = ReplayStore.Load(path);

            Assert.True(loaded.IsExact);
            Assert.Equal(directory, loaded.SourceDirectory);
            Assert.Equal(audioPath, ReplayStore.ResolveAudioCuePath(loaded, loaded.Events[0].Audio[0]));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void ReplayStore_LoadsBestEffortWhenAudioAssetIsMissing()
    {
        string directory = TemporaryDirectory();
        try
        {
            ReplayFile replay = ValidReplay();
            replay.Events[0].Audio.Add(new ReplayAudioCue
            {
                Cue = "missing",
                AssetKey = "missing",
                StartTimeMs = 1000
            });
            string path = WriteReplay(directory, replay);

            ReplayFile loaded = ReplayStore.Load(path);

            Assert.True(loaded.IsBestEffort);
            Assert.True(loaded.UsesTimedPlayback);
            Assert.Contains(loaded.ReplayIssues, issue => issue.Contains("missing", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void ReplayStore_LoadsBestEffortWhenAudioChoiceWasNotRecorded()
    {
        string directory = TemporaryDirectory();
        try
        {
            File.WriteAllBytes(Path.Combine(directory, "one.mp3"), new byte[] { 1 });
            File.WriteAllBytes(Path.Combine(directory, "two.mp3"), new byte[] { 2 });
            ReplayFile replay = ValidReplay();
            replay.Assets.Audio["strike"] = JsonSerializer.SerializeToElement(new[] { "one.mp3", "two.mp3" });
            replay.Events[0].Audio.Add(new ReplayAudioCue
            {
                Cue = "strike",
                AssetKey = "strike",
                StartTimeMs = 1500
            });
            string path = WriteReplay(directory, replay);

            ReplayFile loaded = ReplayStore.Load(path);

            Assert.True(loaded.IsBestEffort);
            Assert.Contains(loaded.ReplayIssues, issue => issue.Contains("first available clip", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void ReplayStore_LoadsBestEffortWhenCutsceneAssetIsMissing()
    {
        string directory = TemporaryDirectory();
        try
        {
            ReplayFile replay = ValidReplay();
            replay.Events[0].Cutscenes.Add(new ReplayCutsceneCue
            {
                Trigger = "home_run",
                AssetPath = "missing.mp4",
                StartTimeMs = 1500,
                DurationMs = 500
            });
            string path = WriteReplay(directory, replay);

            ReplayFile loaded = ReplayStore.Load(path);

            Assert.True(loaded.IsBestEffort);
            Assert.Contains(loaded.ReplayIssues, issue => issue.Contains("cutscene", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void ReplayStore_ReconstructsMissingStatesForBestEffortPlayback()
    {
        string directory = TemporaryDirectory();
        try
        {
            ReplayFile replay = ValidReplay();
            replay.StartingState = null;
            replay.FinalState = null;
            replay.Events[0].Before = null;
            replay.Events[0].After = null;
            replay.Events[0].Animation = null;
            replay.Events[0].Score = new ReplayScore { Away = 2, Home = 1 };
            replay.Events[0].Inning = 4;
            replay.Events[0].Half = "bottom";
            string path = WriteReplay(directory, replay);

            ReplayFile loaded = ReplayStore.Load(path);

            Assert.True(loaded.IsBestEffort);
            Assert.True(loaded.UsesTimedPlayback);
            Assert.NotNull(loaded.StartingState);
            Assert.NotNull(loaded.FinalState);
            Assert.NotNull(loaded.Events[0].Before);
            Assert.NotNull(loaded.Events[0].After);
            Assert.NotNull(loaded.Events[0].Animation);
            Assert.Equal(4, loaded.Events[0].After.Inning);
            Assert.Equal(2, loaded.Events[0].After.Score.Away);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void ReplayStore_NormalizesUsableMovementAndCueTimingForBestEffortPlayback()
    {
        string directory = TemporaryDirectory();
        try
        {
            string audioPath = Path.Combine(directory, "cue.mp3");
            File.WriteAllBytes(audioPath, new byte[] { 1 });
            ReplayFile replay = ValidReplay();
            replay.Events[0].Animation.BallPath = new List<ReplayPathPoint>
            {
                new ReplayPathPoint { TimeMs = 2400, X = 1.4f, Y = -0.2f, Z = 0.2f },
                new ReplayPathPoint { TimeMs = 800, X = 0.2f, Y = 0.7f, Z = 0f }
            };
            replay.Events[0].Audio.Add(new ReplayAudioCue
            {
                Cue = "late",
                File = "cue.mp3",
                StartTimeMs = 3000
            });
            string path = WriteReplay(directory, replay);

            ReplayFile loaded = ReplayStore.Load(path);

            Assert.True(loaded.IsBestEffort);
            Assert.Equal(new long[] { 1000, 2000 }, loaded.Events[0].Animation.BallPath.Select(point => point.TimeMs));
            Assert.Equal(0.2f, loaded.Events[0].Animation.BallPath[0].X, 3);
            Assert.Equal(1f, loaded.Events[0].Animation.BallPath[1].X, 3);
            Assert.Equal(2000, loaded.Events[0].Audio[0].StartTimeMs);
            Assert.Equal(audioPath, ReplayStore.ResolveAudioCuePath(loaded, loaded.Events[0].Audio[0]));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void ReplayStore_PreservesLegacySnapshotReplayCompatibility()
    {
        string directory = TemporaryDirectory();
        try
        {
            var replay = new ReplayFile
            {
                ReplaySchemaVersion = 1,
                Events = new List<ReplayEvent>
                {
                    new ReplayEvent { Sequence = 1, EventType = "single", Description = "Legacy single." }
                }
            };
            string path = WriteReplay(directory, replay);

            ReplayFile loaded = ReplayStore.Load(path);

            Assert.False(loaded.IsExact);
            Assert.Equal("Legacy single.", loaded.Events.Single().Description);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static ReplayFile ValidReplay()
    {
        var before = new ReplayGameState
        {
            TimeMs = 1000,
            Inning = 1,
            Half = "top",
            Score = new ReplayScore { Away = 0, Home = 0 },
            Bases = new ReplayExactBases()
        };
        var after = new ReplayGameState
        {
            TimeMs = 2000,
            Inning = 1,
            Half = "top",
            Score = new ReplayScore { Away = 0, Home = 0 },
            Bases = new ReplayExactBases { First = new ReplayBaseOccupant { PlayerId = "runner-1" } }
        };
        return new ReplayFile
        {
            ReplaySchemaVersion = 2,
            Deterministic = true,
            Teams = new ReplayTeams
            {
                Away = new ReplayTeam
                {
                    TeamId = "away",
                    Lineup = new List<ReplayLineupSlot>
                    {
                        new ReplayLineupSlot
                        {
                            Order = 1,
                            Position = "CF",
                            Player = new ReplayPlayer { PlayerId = "runner-1", TeamId = "away", Name = "Runner" }
                        }
                    }
                },
                Home = new ReplayTeam
                {
                    TeamId = "home",
                    PitchingStaff = new List<ReplayPlayer>
                    {
                        new ReplayPlayer { PlayerId = "pitcher-1", TeamId = "home", Name = "Pitcher", PlayerType = "pitcher" }
                    }
                }
            },
            StartingState = before,
            FinalState = after,
            Events = new List<ReplayEvent>
            {
                new ReplayEvent
                {
                    Sequence = 1,
                    EventId = "event-1",
                    EventType = "pitch",
                    TimeMs = 1000,
                    DurationMs = 1000,
                    Before = before,
                    Command = new ReplayCommand
                    {
                        Pitch = new ReplayPitchCommand { PitcherId = "pitcher-1", BatterId = "runner-1" }
                    },
                    Animation = new ReplayAnimation
                    {
                        BallPath = new List<ReplayPathPoint>
                        {
                            new ReplayPathPoint { TimeMs = 1000, X = 0.2f, Y = 0.6f, Z = 0f },
                            new ReplayPathPoint { TimeMs = 2000, X = 0.8f, Y = 0.2f, Z = 1f }
                        },
                        RunnerPaths = new List<ReplayRunnerPath>
                        {
                            new ReplayRunnerPath
                            {
                                PlayerId = "runner-1",
                                FromBase = 0,
                                ToBase = 1,
                                Safe = true,
                                Path = new List<ReplayPathPoint>
                                {
                                    new ReplayPathPoint { TimeMs = 1000, X = 0.4f, Y = 0.8f },
                                    new ReplayPathPoint { TimeMs = 2000, X = 0.6f, Y = 0.6f }
                                }
                            }
                        }
                    },
                    After = after,
                    Validation = new ReplayValidation
                    {
                        ScoreAfter = new ReplayScore { Away = 0, Home = 0 },
                        OutsAfter = 0,
                        BasesAfter = new ReplayValidationBases { FirstPlayerId = "runner-1" }
                    }
                }
            }
        };
    }

    private static string TemporaryDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "DansRBI-ReplayTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static string WriteReplay(string directory, ReplayFile replay)
    {
        string path = Path.Combine(directory, "test" + ReplayStore.Extension);
        File.WriteAllText(path, JsonSerializer.Serialize(replay, JsonOptions));
        return path;
    }
}
