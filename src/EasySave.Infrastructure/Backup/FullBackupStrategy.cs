using EasySave.Core.Entities;
using EasySave.Core.Interfaces;
using EasySave.Core.Models;
using System.Runtime.CompilerServices;

namespace EasySave.Infrastructure.Backup
{
    public sealed class FullBackupStrategy : IBackupStrategy
    {
        public async IAsyncEnumerable<FileItem> GetEligibleFilesAsync(
            BackupJob job,
            IAsyncEnumerable<FileItem> files,
            DateTime? differentialSinceUtc,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            await foreach (var file in files.WithCancellation(ct))
                yield return file;
        }
    }
}
