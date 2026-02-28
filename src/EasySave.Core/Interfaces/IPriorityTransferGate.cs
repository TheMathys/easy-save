using System.Threading;
using System.Threading.Tasks;

namespace EasySave.Core.Interfaces
{
    /// <summary>
    /// Coordinates transfer order across multiple backup jobs so that the global priority rule is respected:
    /// as long as any job has a priority file pending, no job may transfer a non-priority file.
    /// Used only when several jobs run in parallel. Single-job execution does not require a gate.
    /// </summary>
    public interface IPriorityTransferGate
    {
        /// <summary>
        /// Registers a job with the given number of priority files still pending (not yet started).
        /// Must be called once per job at the start of its file loop when running in parallel.
        /// </summary>
        /// <param name="jobId">Identifier of the backup job.</param>
        /// <param name="priorityFileCount">Number of priority files in this job's queue.</param>
        void RegisterJob(int jobId, int priorityFileCount);

        /// <summary>
        /// Notifies the gate that this job is about to start transferring a priority file,
        /// so the global pending count can be decremented. Call before starting the transfer.
        /// </summary>
        /// <param name="jobId">Identifier of the backup job.</param>
        void NotifyPriorityFileStarted(int jobId);

        /// <summary>
        /// Unregisters the job and subtracts its remaining priority pending count from the total.
        /// Must be called when the job ends (e.g. in a finally block) so that waiters are not blocked forever.
        /// </summary>
        /// <param name="jobId">Identifier of the backup job.</param>
        void UnregisterJob(int jobId);

        /// <summary>
        /// Waits until no priority file is pending on any registered job, so that the caller
        /// is allowed to transfer a non-priority file. Returns immediately if the total priority
        /// pending count is already zero. Supports cancellation.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the wait.</param>
        Task WaitUntilCanTransferNonPriorityAsync(CancellationToken cancellationToken);
    }
}
