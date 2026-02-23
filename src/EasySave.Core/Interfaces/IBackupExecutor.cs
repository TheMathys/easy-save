using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EasySave.Core.Entities;

namespace EasySave.Core.Interfaces
{
    /// <summary>
    /// Executes one or more backup jobs. When multiple job identifiers are provided, jobs run in parallel;
    /// when a single job is provided, it runs alone (same behavior as before). Depends on strategies,
    /// the FileSystem, the StateWriter, and logging (injected). Supports cooperative cancellation.
    /// </summary>
    public interface IBackupExecutor
    {
        /// <summary>
        /// Executes the backup jobs whose identifiers are provided. When multiple ids are given, jobs run in parallel.
        /// </summary>
        /// <param name="jobIds">
        /// Identifiers of the backup jobs to execute (e.g. 1, 3 or 1, 2, 3).
        /// </param>
        /// <param name="progress">Optional. Reports current backup progress (for real-time progress bar and ETA).</param>
        /// <param name="cancellationToken">
        /// Token used to request cancellation of the execution.
        /// Allows the operation to stop gracefully (for example when the user cancels
        /// the process or the application is shutting down).
        /// </param>
        Task ExecuteAsync(IReadOnlyList<int> jobIds, IProgress<BackupProgress>? progress = null, CancellationToken cancellationToken = default);
    }
}
