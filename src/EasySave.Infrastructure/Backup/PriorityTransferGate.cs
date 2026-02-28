using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EasySave.Core.Interfaces;

namespace EasySave.Infrastructure.Backup
{
    /// <summary>
    /// Thread-safe implementation of <see cref="IPriorityTransferGate"/>.
    /// Tracks per-job priority file counts and signals waiters when the total pending priority count reaches zero.
    /// </summary>
    public sealed class PriorityTransferGate : IPriorityTransferGate
    {
        private readonly object _lock = new();
        private readonly Dictionary<int, int> _perJobPriorityPending = new();
        private int _totalPriorityPending;
        private ManualResetEventSlim _signalWhenZero = new(initialState: false);

        /// <inheritdoc />
        public void RegisterJob(int jobId, int priorityFileCount)
        {
            if (priorityFileCount <= 0) return;

            lock (_lock)
            {
                _perJobPriorityPending[jobId] = priorityFileCount;
                _totalPriorityPending += priorityFileCount;
                _signalWhenZero.Reset();
            }
        }

        /// <inheritdoc />
        public void NotifyPriorityFileStarted(int jobId)
        {
            lock (_lock)
            {
                if (!_perJobPriorityPending.TryGetValue(jobId, out int count) || count <= 0)
                    return;
                _perJobPriorityPending[jobId] = count - 1;
                _totalPriorityPending--;
                if (_totalPriorityPending == 0)
                    _signalWhenZero.Set();
            }
        }

        /// <inheritdoc />
        public void UnregisterJob(int jobId)
        {
            lock (_lock)
            {
                if (!_perJobPriorityPending.Remove(jobId, out int remaining))
                    return;
                _totalPriorityPending -= remaining;
                if (_totalPriorityPending < 0)
                    _totalPriorityPending = 0;
                if (_totalPriorityPending == 0)
                    _signalWhenZero.Set();
            }
        }

        /// <inheritdoc />
        public async Task WaitUntilCanTransferNonPriorityAsync(CancellationToken cancellationToken)
        {
            while (true)
            {
                lock (_lock)
                {
                    if (_totalPriorityPending == 0)
                        return;
                }

                await Task.Run(() =>
                    _signalWhenZero.Wait(TimeSpan.FromMilliseconds(100), cancellationToken), cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
