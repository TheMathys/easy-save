using EasySave.Core.Entities;
using EasySave.Core.Interfaces;
using EasySave.Core.Models;
using System.Runtime.CompilerServices;

namespace EasySave.Infrastructure.Backup
{
    /// <summary>
    /// Differential backup strategy: only files modified since the last full backup.
    /// </summary>
    public sealed class DifferentialBackupStrategy : IBackupStrategy
    {
        public async IAsyncEnumerable<FileItem> GetEligibleFilesAsync(BackupJob job, IAsyncEnumerable<FileItem> files, DateTime? differentialSinceUtc, [EnumeratorCancellation] CancellationToken ct = default)
        {
            DateTime since = differentialSinceUtc ?? DateTime.MinValue;

            await foreach (var file in files.WithCancellation(ct))
            {
                if (file.LastWriteTimeUtc > since)
                    yield return file;
            }
        }
    }
}
