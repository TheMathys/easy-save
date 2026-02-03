using System;
using System.Collections.Generic;
using System.Threading;
using EasySave.Core.Entities;
using EasySave.Core.Models;

namespace EasySave.Core.Interfaces
{
    /// <summary>
    /// Defines the filtering of eligible files (complete vs differential)
    /// </summary>
    public interface IBackupStrategy
    {
        /// <summary>
        /// Retrieves the files eligible for backup based on the backup type (full or differential).
        /// </summary>
        /// <param name="job">The backup job configuration.</param>
        /// <param name="files">The list of source files to evaluate.</param>
        /// <param name="differentialSinceUtc">The reference date for differential backups (UTC). Null for full backups.</param>
        /// <param name="ct">The token used to observe cancellation requests.</param>
        /// <returns>An asynchronous stream of files that should be included in the backup.</returns>
        public IAsyncEnumerable<FileItem> GetEligibleFilesAsync(BackupJob job, IAsyncEnumerable<FileItem> files, DateTime? differentialSinceUtc, CancellationToken ct);
    }
}