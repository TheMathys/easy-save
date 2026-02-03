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
        /// Get the eligible files according to the backup type (complete or differential)
        /// </summary>
        /// <param name="job"></param>
        /// <param name="files"></param>
        /// <param name="differentialSinceUtc"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        public IAsyncEnumerable<FileItem> GetEligibleFilesAsync(BackupJob job, IAsyncEnumerable<FileItem> files,
            DateTime? differentialSinceUtc, CancellationToken ct);
    }
}