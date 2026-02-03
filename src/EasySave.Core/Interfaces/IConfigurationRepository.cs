using System;
using System.Threading;
using System.Threading.Tasks;
using EasySave.Core.Entities;

namespace EasySave.Core.Interfaces
{
    /// <summary>
    /// Defines methods for loading and saving the backup configuration,
    /// including the list of jobs, log/status directory, and the date of the last full backup for each job.
    /// </summary>
    public interface IConfigurationRepository
    {
        /// <summary>
        /// Loads the backup configuration from persistent storage.
        /// </summary>
        /// <param name="cancellationToken">Token used to observe cancellation requests.</param>
        public Task<BackupConfiguration?> LoadAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Saves the provided backup configuration to persistent storage.
        /// </summary>
        /// <param name="backupConfiguration">The configuration data to save.</param>
        /// <param name="cancellationToken">Token used to observe cancellation requests.</param>
        public Task SaveAsync(BackupConfiguration backupConfiguration, CancellationToken cancellationToken);

        /// <summary>
        /// Updates the date of the last full backup for the specified job.
        /// </summary>
        /// <param name="jobId">The unique identifier of the job to update.</param>
        /// <param name="utc">The date and time (in UTC) of the last full backup.</param>
        /// <param name="cancellationToken">Token used to observe cancellation requests.</param>
        public Task UpdateLastFullBackupAsync(int jobId, DateTime utc, CancellationToken cancellationToken);
    }
}
