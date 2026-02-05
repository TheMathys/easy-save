using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyLog;
using EasySave.Core.Entities;
using EasySave.Core.Enums;
using EasySave.Core.Interfaces;
using FileItem = EasySave.Core.Models.FileItem;

namespace EasySave.Infrastructure.Backup
{
    /// <summary>
    /// Executes backup jobs sequentially: logging, real-time state, full/differential strategy.
    /// Progress by size (bytes) with ETA and report during file copy.
    /// </summary>
    public sealed class BackupExecutor : IBackupExecutor
    {
        private readonly IConfigurationRepository _configRepository;
        private readonly IBackupStrategyFactory _strategyFactory;
        private readonly IFileSystemService _fileSystem;
        private readonly IStateWriter _stateWriter;
        private readonly ILogWriter _logWriter;

        public BackupExecutor(
            IConfigurationRepository configRepository,
            IBackupStrategyFactory strategyFactory,
            IFileSystemService fileSystem,
            IStateWriter stateWriter,
            ILogWriter logWriter)
        {
            _configRepository = configRepository;
            _strategyFactory = strategyFactory;
            _fileSystem = fileSystem;
            _stateWriter = stateWriter;
            _logWriter = logWriter;
        }

        public async Task ExecuteAsync(IReadOnlyList<int> jobIds, CancellationToken cancellationToken = default)
        {
            BackupConfiguration? config = await _configRepository.LoadAsync(cancellationToken).ConfigureAwait(false);
            if (config == null || config.Jobs.Count == 0)
                return;

            Dictionary<int, BackupJob> jobById = config.Jobs.ToDictionary(j => j.Id);
            List<BackupProgress> progressList = config.Jobs.Select(j => new BackupProgress
            {
                BackupName = j.Name,
                LastActionTimestamp = DateTime.UtcNow,
                State = BackupState.Inactive
            }).ToList();

            await _stateWriter.WriteStateAsync(progressList, cancellationToken).ConfigureAwait(false);

            foreach (int jobId in jobIds)
            {
                if (!jobById.TryGetValue(jobId, out BackupJob? job))
                    continue;

                if (!string.IsNullOrWhiteSpace(job.TargetPath))
                    _fileSystem.EnsureDirectoryExists(job.TargetPath);

                int idx = progressList.FindIndex(p => p.BackupName == job.Name);
                if (idx < 0) continue;

                if (!string.IsNullOrWhiteSpace(job.TargetPath))
                    _fileSystem.EnsureDirectoryExists(job.TargetPath);

                IBackupStrategy strategy = _strategyFactory.GetStrategy(job.Type);
                DateTime? differentialSince = job.Type == BackupType.Differential && config.LastFullBackupUtcByJobId.TryGetValue(job.Id, out DateTime since) ? since : null;

                // First pass: compute total size and file count without keeping the list in memory.
                long totalSize = 0L;
                int fileCount = 0;
                IAsyncEnumerable<FileItem> pass1Stream = _fileSystem.EnumerateFilesAsync(job.SourcePath, cancellationToken);
                await foreach (FileItem f in strategy.GetEligibleFilesAsync(job, pass1Stream, differentialSince, cancellationToken))
                {
                    totalSize += _fileSystem.GetFileSize(f.FullSourcePath);
                    fileCount++;
                }

                progressList[idx] = new BackupProgress
                {
                    BackupName = job.Name,
                    LastActionTimestamp = DateTime.UtcNow,
                    State = BackupState.Active,
                    TotalFilesCount = fileCount,
                    TotalSizeBytes = totalSize,
                    ProgressPercent = 0,
                    RemainingFilesCount = fileCount,
                    RemainingSizeBytes = totalSize,
                    CurrentSourcePath = null,
                    CurrentDestinationPath = null
                };
                await _stateWriter.WriteStateAsync(progressList, cancellationToken).ConfigureAwait(false);

                // Second pass: stream and copy one file at a time so we never hold the full list.
                long bytesCompleted = 0L;
                int processedCount = 0;
                IAsyncEnumerable<FileItem> pass2Stream = _fileSystem.EnumerateFilesAsync(job.SourcePath, cancellationToken);
                await foreach (FileItem item in strategy.GetEligibleFilesAsync(job, pass2Stream, differentialSince, cancellationToken))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    string destPath = Path.Combine(job.TargetPath, item.RelativePath);
                    string? dir = Path.GetDirectoryName(destPath);
                    if (!string.IsNullOrEmpty(dir))
                        _fileSystem.EnsureDirectoryExists(dir);

                    long fileSize = _fileSystem.GetFileSize(item.FullSourcePath);
                    long transferMs = await _fileSystem.CopyFileAsync(item.FullSourcePath, destPath, cancellationToken).ConfigureAwait(false);

                    string uncSource = _fileSystem.GetUncPath(item.FullSourcePath);
                    string uncDest = _fileSystem.GetUncPath(destPath);
                    TimeSpan transferTime = TimeSpan.FromMilliseconds(Math.Abs(transferMs));
                    await _logWriter.WriteAsync(new LogEntry(DateTime.UtcNow, job.Name, uncSource, uncDest, fileSize, transferTime), cancellationToken).ConfigureAwait(false);

                    bytesCompleted += fileSize;
                    processedCount++;
                    int remainingFiles = fileCount - processedCount;
                    long remainingSize = totalSize - bytesCompleted;
                    double percentDone = totalSize > 0 ? Math.Round((double)bytesCompleted / totalSize * 100.0, 2) : 100.0;

                    progressList[idx] = new BackupProgress
                    {
                        BackupName = job.Name,
                        LastActionTimestamp = DateTime.UtcNow,
                        State = BackupState.Active,
                        TotalFilesCount = fileCount,
                        TotalSizeBytes = totalSize,
                        ProgressPercent = percentDone,
                        RemainingFilesCount = remainingFiles,
                        RemainingSizeBytes = remainingSize,
                        CurrentSourcePath = uncSource,
                        CurrentDestinationPath = uncDest
                    };
                    await _stateWriter.WriteStateAsync(progressList, cancellationToken).ConfigureAwait(false);
                }

                progressList[idx] = new BackupProgress
                {
                    BackupName = job.Name,
                    LastActionTimestamp = DateTime.UtcNow,
                    State = BackupState.Completed,
                    TotalFilesCount = fileCount,
                    TotalSizeBytes = totalSize,
                    ProgressPercent = 100,
                    RemainingFilesCount = 0,
                    RemainingSizeBytes = 0,
                    CurrentSourcePath = null,
                    CurrentDestinationPath = null
                };

                if (job.Type == BackupType.Full)
                    await _configRepository.UpdateLastFullBackupAsync(job.Id, DateTime.UtcNow, cancellationToken).ConfigureAwait(false);

                await _stateWriter.WriteStateAsync(progressList, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
