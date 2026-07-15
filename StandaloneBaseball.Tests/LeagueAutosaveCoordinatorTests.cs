using StandaloneBaseball;

namespace StandaloneBaseball.Tests;

public sealed class LeagueAutosaveCoordinatorTests
{
    [Fact]
    public void Flush_CoalescesRepeatedRequestsIntoOneSave()
    {
        var saves = new List<IReadOnlyCollection<LeagueAutosaveReason>>();
        using var autosave = new LeagueAutosaveCoordinator(
            reasons =>
            {
                saves.Add(reasons);
                return true;
            },
            TimeSpan.FromHours(1));

        autosave.Request(LeagueAutosaveReason.RosterChanged);
        autosave.Request(LeagueAutosaveReason.RosterChanged);
        autosave.Request(LeagueAutosaveReason.AwardsFinalized);

        Assert.True(autosave.Flush());
        Assert.True(autosave.Flush());

        IReadOnlyCollection<LeagueAutosaveReason> savedReasons = Assert.Single(saves);
        Assert.Equal(2, savedReasons.Count);
        Assert.Contains(LeagueAutosaveReason.RosterChanged, savedReasons);
        Assert.Contains(LeagueAutosaveReason.AwardsFinalized, savedReasons);
    }

    [Fact]
    public void Debounce_CoalescesRequestsThatArriveCloseTogether()
    {
        using var completed = new ManualResetEventSlim();
        int saveCount = 0;
        IReadOnlyCollection<LeagueAutosaveReason> savedReasons = Array.Empty<LeagueAutosaveReason>();
        using var autosave = new LeagueAutosaveCoordinator(
            reasons =>
            {
                savedReasons = reasons;
                Interlocked.Increment(ref saveCount);
                completed.Set();
                return true;
            },
            TimeSpan.FromMilliseconds(75));

        autosave.Request(LeagueAutosaveReason.RosterChanged);
        Thread.Sleep(20);
        autosave.Request(LeagueAutosaveReason.ScheduleGenerated);

        Assert.True(completed.Wait(TimeSpan.FromSeconds(3)));
        Thread.Sleep(125);
        Assert.Equal(1, Volatile.Read(ref saveCount));
        Assert.Contains(LeagueAutosaveReason.RosterChanged, savedReasons);
        Assert.Contains(LeagueAutosaveReason.ScheduleGenerated, savedReasons);
    }

    [Fact]
    public void CancelPending_PreventsQueuedSave()
    {
        int saveCount = 0;
        using var autosave = new LeagueAutosaveCoordinator(
            _ =>
            {
                Interlocked.Increment(ref saveCount);
                return true;
            },
            TimeSpan.FromMilliseconds(50));

        autosave.Request(LeagueAutosaveReason.HallOfFameChanged);
        autosave.CancelPending();
        Thread.Sleep(150);

        Assert.Equal(0, Volatile.Read(ref saveCount));
    }

    [Fact]
    public void Suspension_DefersCallbackAlreadyQueuedToSynchronizationContext()
    {
        using var context = new QueuedSynchronizationContext();
        int saveCount = 0;
        using var autosave = new LeagueAutosaveCoordinator(
            _ =>
            {
                Interlocked.Increment(ref saveCount);
                return true;
            },
            TimeSpan.Zero,
            context);

        autosave.Request(LeagueAutosaveReason.RosterChanged);
        Assert.True(context.WaitForPost());

        using (autosave.Suspend())
        {
            Assert.True(context.RunOne());
            Assert.Equal(0, Volatile.Read(ref saveCount));
        }

        Assert.True(context.WaitForPost());
        Assert.True(context.RunOne());
        Assert.Equal(1, Volatile.Read(ref saveCount));
    }

    [Fact]
    public void CancelPending_DuringSuspensionDropsCallbackDrainedByModalSynchronizationContext()
    {
        using var context = new QueuedSynchronizationContext();
        int saveCount = 0;
        using var autosave = new LeagueAutosaveCoordinator(
            _ =>
            {
                Interlocked.Increment(ref saveCount);
                return true;
            },
            TimeSpan.Zero,
            context);

        autosave.Request(LeagueAutosaveReason.HallOfFameChanged);
        Assert.True(context.WaitForPost());

        using (autosave.Suspend())
        {
            Assert.True(context.RunOne());
            Assert.Equal(0, Volatile.Read(ref saveCount));
            autosave.CancelPending();
        }

        Assert.Equal(0, Volatile.Read(ref saveCount));
    }

    [Fact]
    public void Flush_PersistsLatestLeagueStateWithOneWrite()
    {
        string directory = Path.Combine(Path.GetTempPath(), "DansRBI-AutosaveTests", Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "autosave" + LeagueStore.Extension);
        Directory.CreateDirectory(directory);
        try
        {
            var league = new LeagueFile { Name = "Initial" };
            LeagueStore.Save(path, league);
            int saveCount = 0;
            using var autosave = new LeagueAutosaveCoordinator(
                _ =>
                {
                    Interlocked.Increment(ref saveCount);
                    LeagueStore.Save(path, league);
                    return true;
                },
                TimeSpan.FromHours(1));

            league.Name = "Roster edited";
            autosave.Request(LeagueAutosaveReason.RosterChanged);
            league.Name = "Awards finalized";
            autosave.Request(LeagueAutosaveReason.AwardsFinalized);

            Assert.True(autosave.Flush());

            Assert.Equal(1, saveCount);
            Assert.Equal("Awards finalized", LeagueStore.Load(path).Name);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class QueuedSynchronizationContext : SynchronizationContext, IDisposable
    {
        private readonly Queue<(SendOrPostCallback Callback, object State)> _callbacks = new();
        private readonly AutoResetEvent _posted = new(false);

        public override void Post(SendOrPostCallback d, object state)
        {
            lock (_callbacks)
                _callbacks.Enqueue((d, state));
            _posted.Set();
        }

        public bool WaitForPost()
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(3);
            while (true)
            {
                lock (_callbacks)
                {
                    if (_callbacks.Count > 0)
                        return true;
                }

                TimeSpan remaining = deadline - DateTime.UtcNow;
                if (remaining <= TimeSpan.Zero || !_posted.WaitOne(remaining))
                    return false;
            }
        }

        public bool RunOne()
        {
            (SendOrPostCallback Callback, object State) callback;
            lock (_callbacks)
            {
                if (_callbacks.Count == 0)
                    return false;
                callback = _callbacks.Dequeue();
            }
            callback.Callback(callback.State);
            return true;
        }

        public void Dispose() => _posted.Dispose();
    }
}
