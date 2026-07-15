#nullable enable annotations

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace StandaloneBaseball
{
    public enum LeagueAutosaveReason
    {
        RosterChanged,
        ScheduleGenerated,
        AwardsFinalized,
        HallOfFameChanged
    }

    public sealed class LeagueAutosaveCoordinator : IDisposable
    {
        private readonly object _gate = new object();
        private readonly Func<IReadOnlyCollection<LeagueAutosaveReason>, bool> _save;
        private readonly SynchronizationContext? _synchronizationContext;
        private readonly TimeSpan _debounceDelay;
        private readonly HashSet<LeagueAutosaveReason> _pendingReasons = new HashSet<LeagueAutosaveReason>();
        private readonly System.Threading.Timer _timer;
        private bool _saveInProgress;
        private bool _disposed;
        private int _suspensionCount;
        private long _generation;

        public LeagueAutosaveCoordinator(
            Func<IReadOnlyCollection<LeagueAutosaveReason>, bool> save,
            TimeSpan debounceDelay,
            SynchronizationContext? synchronizationContext = null)
        {
            _save = save ?? throw new ArgumentNullException(nameof(save));
            if (debounceDelay < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(debounceDelay));

            _debounceDelay = debounceDelay;
            _synchronizationContext = synchronizationContext;
            _timer = new System.Threading.Timer(OnTimerElapsed, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }

        public void Request(LeagueAutosaveReason reason)
        {
            lock (_gate)
            {
                ThrowIfDisposed();
                _pendingReasons.Add(reason);
                if (_suspensionCount == 0)
                    _timer.Change(_debounceDelay, Timeout.InfiniteTimeSpan);
            }
        }

        public IDisposable Suspend()
        {
            lock (_gate)
            {
                ThrowIfDisposed();
                _suspensionCount++;
                _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                return new Suspension(this);
            }
        }

        public bool Flush()
        {
            PendingBatch? batch;
            lock (_gate)
            {
                ThrowIfDisposed();
                if (_suspensionCount > 0)
                    throw new InvalidOperationException("Autosave cannot be flushed while it is suspended.");
                _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                batch = TakePendingBatch();
            }

            return batch == null || ExecuteBatch(batch);
        }

        public void CancelPending()
        {
            lock (_gate)
            {
                if (_disposed)
                    return;
                _generation++;
                _pendingReasons.Clear();
                _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            }
        }

        private void OnTimerElapsed(object? state)
        {
            PendingBatch? batch;
            lock (_gate)
            {
                if (_disposed || _suspensionCount > 0)
                    return;
                batch = TakePendingBatch();
            }

            if (batch == null)
                return;

            if (_synchronizationContext == null)
                ExecuteBatch(batch);
            else
                _synchronizationContext.Post(_ => ExecuteBatch(batch), null);
        }

        private PendingBatch? TakePendingBatch()
        {
            if (_pendingReasons.Count == 0)
                return null;

            var reasons = _pendingReasons.OrderBy(reason => reason).ToArray();
            _pendingReasons.Clear();
            return new PendingBatch(_generation, reasons);
        }

        private bool ExecuteBatch(PendingBatch batch)
        {
            lock (_gate)
            {
                if (_disposed || batch.Generation != _generation)
                    return true;
                if (_suspensionCount > 0 || _saveInProgress)
                {
                    Requeue(batch.Reasons);
                    if (_suspensionCount == 0)
                        _timer.Change(_debounceDelay, Timeout.InfiniteTimeSpan);
                    return true;
                }
                _saveInProgress = true;
            }

            try
            {
                return _save(batch.Reasons);
            }
            catch
            {
                return false;
            }
            finally
            {
                lock (_gate)
                {
                    _saveInProgress = false;
                    if (!_disposed && _suspensionCount == 0 && _pendingReasons.Count > 0)
                        _timer.Change(_debounceDelay, Timeout.InfiniteTimeSpan);
                }
            }
        }

        private void Requeue(IEnumerable<LeagueAutosaveReason> reasons)
        {
            foreach (LeagueAutosaveReason reason in reasons)
                _pendingReasons.Add(reason);
        }

        private void Resume()
        {
            lock (_gate)
            {
                if (_disposed)
                    return;
                if (_suspensionCount <= 0)
                    throw new InvalidOperationException("Autosave is not suspended.");

                _suspensionCount--;
                if (_suspensionCount == 0 && _pendingReasons.Count > 0)
                    _timer.Change(_debounceDelay, Timeout.InfiniteTimeSpan);
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(LeagueAutosaveCoordinator));
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed)
                    return;
                _disposed = true;
                _generation++;
                _pendingReasons.Clear();
            }
            _timer.Dispose();
        }

        private sealed class PendingBatch
        {
            public PendingBatch(long generation, IReadOnlyCollection<LeagueAutosaveReason> reasons)
            {
                Generation = generation;
                Reasons = reasons;
            }

            public long Generation { get; }
            public IReadOnlyCollection<LeagueAutosaveReason> Reasons { get; }
        }

        private sealed class Suspension : IDisposable
        {
            private LeagueAutosaveCoordinator? _owner;

            public Suspension(LeagueAutosaveCoordinator owner)
            {
                _owner = owner;
            }

            public void Dispose()
            {
                LeagueAutosaveCoordinator? owner = Interlocked.Exchange(ref _owner, null);
                owner?.Resume();
            }
        }
    }
}
