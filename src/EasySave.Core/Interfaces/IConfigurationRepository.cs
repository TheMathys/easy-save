using System;
using System.Threading;
using System.Threading.Tasks;
using EasySave.Core.Entities;

namespace EasySave.Core.Interfaces
{
    public interface IConfigurationRepository
    {
        /// <summary>
        /// Load the file configuration
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public Task LoadAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Save the configuration
        /// </summary>
        /// <param name="backupConfiguration"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public Task SaveAsync(BackupConfiguration backupConfiguration, CancellationToken cancellationToken);

        /// <summary>
        /// Update the date
        /// </summary>
        /// <param name="jobId"></param>
        /// <param name="utc"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public Task UpdateLastFullBackuoAsync(int jobId, DateTime utc, CancellationToken cancellationToken);
    }
}