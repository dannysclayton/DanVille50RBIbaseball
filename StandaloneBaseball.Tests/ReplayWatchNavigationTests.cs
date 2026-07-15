using StandaloneBaseball;

namespace StandaloneBaseball.Tests;

[Collection(WinFormsTestCollection.Name)]
public sealed class ReplayWatchNavigationTests
{
    private static readonly List<ReplayEvent> Events = new()
    {
        new ReplayEvent { Sequence = 1, Inning = 1, Half = "top" },
        new ReplayEvent { Sequence = 2, Inning = 1, Half = "bottom" },
        new ReplayEvent { Sequence = 3, Inning = 2, Half = "top" },
        new ReplayEvent { Sequence = 4, Inning = 2, Half = "bottom" },
        new ReplayEvent { Sequence = 5, Inning = 3, Half = "top" }
    };

    [Fact]
    public void EventNavigation_IncludesReadyStateAndStopsAtFinalEvent()
    {
        Assert.Equal(0, ReplayWatchNavigation.NextEventIndex(-1, Events.Count));
        Assert.Equal(-1, ReplayWatchNavigation.PreviousEventIndex(0, Events.Count));
        Assert.Equal(Events.Count - 1, ReplayWatchNavigation.NextEventIndex(Events.Count - 1, Events.Count));
        Assert.True(ReplayWatchNavigation.IsTerminalEvent(Events.Count - 1, Events.Count));
        Assert.False(ReplayWatchNavigation.IsTerminalEvent(Events.Count - 2, Events.Count));
    }

    [Fact]
    public void InningNavigation_JumpsToFirstEventOfAdjacentInning()
    {
        Assert.Equal(2, ReplayWatchNavigation.NextInningIndex(Events, 0));
        Assert.Equal(2, ReplayWatchNavigation.NextInningIndex(Events, 1));
        Assert.Equal(0, ReplayWatchNavigation.PreviousInningIndex(Events, 2));
        Assert.Equal(2, ReplayWatchNavigation.PreviousInningIndex(Events, 4));
    }

    [Fact]
    public void InningNavigation_HandlesReplayBoundaries()
    {
        Assert.Equal(0, ReplayWatchNavigation.NextInningIndex(Events, -1));
        Assert.Equal(-1, ReplayWatchNavigation.PreviousInningIndex(Events, 0));
        Assert.Equal(-1, ReplayWatchNavigation.NextInningIndex(Events, Events.Count - 1));
        Assert.Equal(-1, ReplayWatchNavigation.NextInningIndex(Array.Empty<ReplayEvent>(), -1));
    }

    [Fact]
    public void SeekRebuild_ReplaysEveryPriorEventAndPreservesTerminalCheck()
    {
        Assert.Equal(new[] { 0, 1, 2 }, ReplayWatchNavigation.RebuildEventIndexes(2, Events.Count));
        Assert.Equal(Enumerable.Range(0, Events.Count),
            ReplayWatchNavigation.RebuildEventIndexes(Events.Count - 1, Events.Count));
        Assert.True(ReplayWatchNavigation.IsTerminalEvent(Events.Count - 1, Events.Count));
        Assert.Empty(ReplayWatchNavigation.RebuildEventIndexes(-1, Events.Count));
    }

    [Fact]
    public void TimedSpeedScaling_PreservesFractionAcrossTimerTicks()
    {
        ReplayPlaybackSpeed halfSpeed = ReplayWatchNavigation.PlaybackSpeeds.Single(speed => speed.Label == "0.5x");
        long remainder = 0;

        long first = ReplayWatchNavigation.ScaleElapsed(15, halfSpeed, ref remainder);
        long second = ReplayWatchNavigation.ScaleElapsed(15, halfSpeed, ref remainder);

        Assert.Equal(7, first);
        Assert.Equal(8, second);
        Assert.Equal(0, remainder);
    }

    [Fact]
    public void SnapshotSpeedScaling_AdjustsEventCadence()
    {
        ReplayPlaybackSpeed halfSpeed = ReplayWatchNavigation.PlaybackSpeeds.Single(speed => speed.Label == "0.5x");
        ReplayPlaybackSpeed normalSpeed = ReplayWatchNavigation.NormalSpeed;
        ReplayPlaybackSpeed quadrupleSpeed = ReplayWatchNavigation.PlaybackSpeeds.Single(speed => speed.Label == "4x");

        Assert.Equal(2400, ReplayWatchNavigation.SnapshotInterval(halfSpeed));
        Assert.Equal(1200, ReplayWatchNavigation.SnapshotInterval(normalSpeed));
        Assert.Equal(300, ReplayWatchNavigation.SnapshotInterval(quadrupleSpeed));
    }

    [Fact]
    public void TimedNavigation_RebuildsTargetFrameWithoutRetainingPriorRenderState()
    {
        WinFormsTestHost.Run(() =>
        {
            ReplayFile replay = TimedReplay();
            using var form = new ReplayWatchForm(replay);
            GameplayForm gameplay = WinFormsTestHost.Field<GameplayForm>(form, "_gameplay");
            GameplayRenderingGameState renderState =
                WinFormsTestHost.Field<GameplayRenderingGameState>(gameplay, "_state");

            WinFormsTestHost.Invoke(form, "JumpToNextEvent");

            Assert.Equal(1, renderState.AwayScore);
            Assert.True(renderState.BallVisible);
            Assert.Equal(0.8f, renderState.BallPosition.X, 3);
            Assert.Equal(0f, renderState.BallTrail);
            Assert.Equal(7, renderState.ActiveFielderIndex);
            GameplayRenderingPlayerMarker runner = Assert.Single(renderState.ReplayActors);
            Assert.Equal(0.75f, runner.Position.X, 3);
            Assert.Equal(0.55f, renderState.Fielders.Single(marker => marker.Label == "P").Position.X, 3);
            Assert.Equal(200L, WinFormsTestHost.Field<long>(form, "_lastExactFrameTimeMs"));

            ReplayRenderFrame staleMidEventFrame = ReplayExactEngine.CreateFrame(replay.Events[0], 150);
            gameplay.ApplyExactReplayFrame(staleMidEventFrame);
            Assert.Equal(0.5f, renderState.BallTrail, 3);

            WinFormsTestHost.Invoke(form, "JumpToNextInning");

            Assert.Equal(1, WinFormsTestHost.Field<int>(form, "_index"));
            Assert.Equal(9, renderState.AwayScore);
            Assert.False(renderState.BallVisible);
            Assert.Equal(0.2f, renderState.BallPosition.X, 3);
            Assert.Equal(0f, renderState.BallHeight);
            Assert.Equal(0f, renderState.BallTrail);
            Assert.Equal(1, renderState.ActiveFielderIndex);
            Assert.Empty(renderState.ReplayActors);
            Assert.Equal(0.25f, renderState.Fielders.Single(marker => marker.Label == "P").Position.X, 3);
            Assert.Equal(400L, WinFormsTestHost.Field<long>(form, "_lastExactFrameTimeMs"));
            Assert.Equal(400L, WinFormsTestHost.Field<long>(form, "_replayTimeMs"));

            WinFormsTestHost.Invoke(form, "JumpToPreviousEvent");

            Assert.Equal(0, WinFormsTestHost.Field<int>(form, "_index"));
            Assert.Equal(1, renderState.AwayScore);
            Assert.True(renderState.BallVisible);
            Assert.Equal(0.8f, renderState.BallPosition.X, 3);
            Assert.Equal(0f, renderState.BallTrail);
            Assert.Equal(7, renderState.ActiveFielderIndex);
            Assert.Equal(0.75f, Assert.Single(renderState.ReplayActors).Position.X, 3);
            Assert.Equal(0.55f, renderState.Fielders.Single(marker => marker.Label == "P").Position.X, 3);
            Assert.Equal(200L, WinFormsTestHost.Field<long>(form, "_lastExactFrameTimeMs"));
        });
    }

    private static ReplayFile TimedReplay()
    {
        ReplayGameState starting = State(0, 1, 0, 0.5f);
        ReplayGameState firstAfter = State(200, 1, 1, 0.55f);
        ReplayGameState secondBefore = State(300, 2, 1, 0.45f);
        ReplayGameState secondAfter = State(400, 2, 2, 0.35f);
        ReplayGameState final = State(400, 2, 9, 0.25f);

        return new ReplayFile
        {
            ReplaySchemaVersion = 2,
            Deterministic = true,
            Game = new ReplayGameInfo { Innings = 9 },
            StartingState = starting,
            FinalState = final,
            Events = new List<ReplayEvent>
            {
                new ReplayEvent
                {
                    Sequence = 1,
                    Inning = 1,
                    Half = "top",
                    TimeMs = 100,
                    DurationMs = 100,
                    Before = starting,
                    After = firstAfter,
                    Animation = new ReplayAnimation
                    {
                        BallPath = Path(100, 0.1f, true, 200, 0.8f, true),
                        FielderPaths = new List<ReplayActorPath>
                        {
                            new ReplayActorPath
                            {
                                PlayerId = "fielder-1",
                                Position = "CF",
                                Path = Path(100, 0.5f, true, 200, 0.7f, true)
                            }
                        },
                        RunnerPaths = new List<ReplayRunnerPath>
                        {
                            new ReplayRunnerPath
                            {
                                PlayerId = "runner-1",
                                Path = Path(100, 0.25f, true, 200, 0.75f, true)
                            }
                        },
                        HighlightPlayerIds = new List<string> { "fielder-1" }
                    }
                },
                new ReplayEvent
                {
                    Sequence = 2,
                    Inning = 2,
                    Half = "top",
                    TimeMs = 300,
                    DurationMs = 100,
                    Before = secondBefore,
                    After = secondAfter,
                    Animation = new ReplayAnimation
                    {
                        BallPath = Path(300, 0.9f, true, 400, 0.2f, false)
                    }
                }
            }
        };
    }

    private static ReplayGameState State(long timeMs, int inning, int awayScore, float pitcherX)
        => new ReplayGameState
        {
            TimeMs = timeMs,
            Inning = inning,
            Score = new ReplayScore { Away = awayScore },
            Fielders = new List<ReplayStateFielder>
            {
                new ReplayStateFielder { Position = "P", X = pitcherX, Y = 0.62f }
            }
        };

    private static List<ReplayPathPoint> Path(
        long startTimeMs,
        float startX,
        bool startVisible,
        long endTimeMs,
        float endX,
        bool endVisible)
        => new List<ReplayPathPoint>
        {
            new ReplayPathPoint { TimeMs = startTimeMs, X = startX, Y = 0.5f, Z = 0.4f, Visible = startVisible },
            new ReplayPathPoint { TimeMs = endTimeMs, X = endX, Y = 0.6f, Z = 0f, Visible = endVisible }
        };
}
