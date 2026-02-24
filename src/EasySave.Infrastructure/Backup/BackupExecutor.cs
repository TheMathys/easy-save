using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasySave.Core.Entities;
using EasySave.Core.Enums;
using EasySave.Core.Exceptions;
using EasySave.Core.Interfaces;
using EasyLog;
using EasySave.Core.Models;
using FileItem = EasySave.Core.Models.FileItem;

namespace EasySave.Infrastructure.Backup
{
    /// <summary>
    /// Executes backup jobs in parallel when multiple are selected, otherwise runs a single job.
    /// Logging, real-time state, full/differential strategy. Progress by size (bytes) with ETA and report during file copy.
    /// Blocks start and stops during backup when business software is detected.
    /// State and log writes are coordinated so that the state file and logs remain consistent when several jobs run concurrently.
    /// </summary>
    public sealed class BackupExecutor : IBackupExecutor
    {
        private readonly IConfigurationRepository _configRepository;
        private readonly IBackupStrategyFactory _strategyFactory;
        private readonly IFileSystemService _fileSystem;
        private readonly IStateWriter _stateWriter;
        private readonly ILogWriter _logWriter;
        private readonly IFileEncryptor? _fileEncryptor;
        private readonly IBusinessSoftwareDetector _businessSoftwareDetector;
        private readonly SemaphoreSlim _stateWriteLock = new(1, 1);

        /// <summary>
        /// Global semaphore used to ensure that at most one large file (above the configured threshold)
        /// is transferred at any given time across all running jobs in this process.
        /// </summary>
        private static readonly SemaphoreSlim LargeFileSemaphore = new(1, 1);

        public const string StopReasonBusinessSoftware = "BusinessSoftwareDetected";

        public BackupExecutor(
            IConfigurationRepository configRepository,
            IBackupStrategyFactory strategyFactory,
            IFileSystemService fileSystem,
            IStateWriter stateWriter,
            ILogWriter logWriter,
            IFileEncryptor? fileEncryptor,
            IBusinessSoftwareDetector businessSoftwareDetector)
        {
            _configRepository = configRepository;
            _strategyFactory = strategyFactory;
            _fileSystem = fileSystem;
            _stateWriter = stateWriter;
            _logWriter = logWriter;
            _fileEncryptor = fileEncryptor;
            _businessSoftwareDetector = businessSoftwareDetector ?? throw new ArgumentNullException(nameof(businessSoftwareDetector));
        }

        private const int ProgressReportThrottleMs = 150;

        public async Task ExecuteAsync(IReadOnlyList<int> jobIds, IProgress<BackupProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            BackupConfiguration? config = await _configRepository.LoadAsync(cancellationToken).ConfigureAwait(false);
            if (config == null || config.Jobs.Count == 0)
                return;

            if (!string.IsNullOrWhiteSpace(config.BusinessSoftwareProcessName) && _businessSoftwareDetector.IsRunning(config.BusinessSoftwareProcessName))
                throw new BusinessSoftwareDetectedException();

            Dictionary<int, BackupJob> jobById = config.Jobs.ToDictionary(j => j.Id);
            List<BackupProgress> progressList = config.Jobs.Select(j => new BackupProgress
            {
                BackupName = j.Name,
                LastActionTimestamp = DateTime.UtcNow,
                State = BackupState.Inactive
            }).ToList();

            await WriteStateUnderLockAsync(progressList, cancellationToken).ConfigureAwait(false);

            List<(BackupJob Job, int ProgressIndex)> toRun = new();
            foreach (int jobId in jobIds)
            {
                if (!jobById.TryGetValue(jobId, out BackupJob? job))
                    continue;
                int idx = progressList.FindIndex(p => p.BackupName == job.Name);
                if (idx >= 0)
                    toRun.Add((job, idx));
            }

            if (toRun.Count == 0)
                return;

            if (toRun.Count == 1)
            {
                (BackupJob job, int idx) = toRun[0];
                await ExecuteSingleJobAsync(job, idx, progressList, config, cancellationToken, progress, onBusinessSoftwareDetected: null).ConfigureAwait(false);
                return;
            }

            // Parallel execution: several jobs at once (Task.WhenAll), shared cancellation when business software is detected.
            using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            Action onBusinessSoftwareDetected = () => linkedCts.Cancel();

            IReadOnlyList<Task> tasks = toRun.Select(t => ExecuteSingleJobAsync(
                t.Job,
                t.ProgressIndex,
                progressList,
                config,
                linkedCts.Token,
                progress,
                onBusinessSoftwareDetected)).ToList();

            try
            {
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (linkedCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                // Stopped due to business software (or internal cancel), do not rethrow.
            }
            catch (OperationCanceledException)
            {
                throw;
            }

            // Write final state after all jobs complete to ensure a snapshot with all jobs in their final state exists
            await WriteStateUnderLockAsync(progressList, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Writes the current progress list to state under the shared lock so that concurrent jobs produce a consistent state file.
        /// </summary>
        private async Task WriteStateUnderLockAsync(List<BackupProgress> progressList, CancellationToken cancellationToken)
        {
            await _stateWriteLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                IReadOnlyList<BackupProgress> snapshot = progressList.ToList();
                await _stateWriter.WriteStateAsync(snapshot, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _stateWriteLock.Release();
            }
        }

        /// <summary>
        /// Writes state and reports progress for one job under the shared lock (used after updating a single slot in progressList).
        /// </summary>
        private async Task WriteStateAndReportAsync(
            List<BackupProgress> progressList,
            int progressIndex,
            IProgress<BackupProgress>? progress,
            CancellationToken cancellationToken)
        {
            await _stateWriteLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                IReadOnlyList<BackupProgress> snapshot = progressList.ToList();
                await _stateWriter.WriteStateAsync(snapshot, cancellationToken).ConfigureAwait(false);
                progress?.Report(progressList[progressIndex]);
            }
            finally
            {
                _stateWriteLock.Release();
            }
        }

        private async Task ExecuteSingleJobAsync(
            BackupJob job,
            int idx,
            List<BackupProgress> progressList,
            BackupConfiguration config,
            CancellationToken cancellationToken,
            IProgress<BackupProgress>? progress,
            Action? onBusinessSoftwareDetected)
        {
            if (!string.IsNullOrWhiteSpace(job.TargetPath))
                _fileSystem.EnsureDirectoryExists(job.TargetPath);

            IBackupStrategy strategy = _strategyFactory.GetStrategy(job.Type);
            DateTime? differentialSince = job.Type == BackupType.Differential && config.LastFullBackupUtcByJobId.TryGetValue(job.Id, out DateTime since) ? since : null;

            BackupEnumerationOptions enumOptions = new BackupEnumerationOptions
            {
                ExcludeExtensions = job.ExcludeExtensions ?? Array.Empty<string>(),
                ExcludeDirectoryNames = job.ExcludeDirectoryNames ?? Array.Empty<string>()
            };

            long totalSize = 0L;
            int fileCount = 0;
            IAsyncEnumerable<FileItem> pass1Stream = _fileSystem.EnumerateFilesAsync(job.SourcePath, enumOptions, cancellationToken);
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
                CurrentDestinationPath = null,
                EstimatedTimeRemainingSeconds = null
            };
            await WriteStateAndReportAsync(progressList, idx, progress, cancellationToken).ConfigureAwait(false);

            DateTime jobStartUtc = DateTime.UtcNow;
            long bytesCompleted = 0L;
            int processedCount = 0;
            long lastReportTicks = 0;

            void UpdateProgress(long bytesCopiedInCurrentFile, string? uncSource, string? uncDest)
            {
                long totalCompleted = bytesCompleted + bytesCopiedInCurrentFile;
                long remainingSize = totalSize - totalCompleted;
                double percentDone = totalSize > 0 ? Math.Round((double)totalCompleted / totalSize * 100.0, 2) : 100.0;
                double elapsedSeconds = (DateTime.UtcNow - jobStartUtc).TotalSeconds;
                double? etaSeconds = null;
                if (elapsedSeconds > 0.5 && totalCompleted > 0 && remainingSize > 0)
                {
                    double speedBytesPerSec = totalCompleted / elapsedSeconds;
                    etaSeconds = remainingSize / speedBytesPerSec;
                }

                progressList[idx] = new BackupProgress
                {
                    BackupName = job.Name,
                    LastActionTimestamp = DateTime.UtcNow,
                    State = BackupState.Active,
                    TotalFilesCount = fileCount,
                    TotalSizeBytes = totalSize,
                    ProgressPercent = percentDone,
                    RemainingFilesCount = fileCount - processedCount,
                    RemainingSizeBytes = remainingSize,
                    CurrentSourcePath = uncSource,
                    CurrentDestinationPath = uncDest,
                    EstimatedTimeRemainingSeconds = etaSeconds
                };
            }

            HashSet<string> encryptExtensionsSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (config.EncryptExtensions?.Count > 0 == true)
            {
                foreach (string ext in config.EncryptExtensions)
                {
                    string normalized = ext.Trim();
                    if (normalized.Length > 0 && normalized[0] != '.')
                        normalized = "." + normalized;
                    if (normalized.Length > 0)
                        encryptExtensionsSet.Add(normalized);
                }
            }

            string cryptoSoftExePath = Path.Combine(AppContext.BaseDirectory, "CryptoSoft", "CryptoSoft.exe");
            bool useEncryption = encryptExtensionsSet.Count > 0
                && !string.IsNullOrWhiteSpace(config.EncryptionKeyPath)
                && _fileEncryptor != null;

            long? largeFileThresholdBytes = null;
            if (config.LargeFileThresholdKb.HasValue && config.LargeFileThresholdKb.Value > 0)
            {
                largeFileThresholdBytes = config.LargeFileThresholdKb.Value * 1024L;
            }

            IAsyncEnumerable<FileItem> pass2Stream = _fileSystem.EnumerateFilesAsync(job.SourcePath, enumOptions, cancellationToken);
            await foreach (FileItem item in strategy.GetEligibleFilesAsync(job, pass2Stream, differentialSince, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                string destPath = Path.Combine(job.TargetPath, item.RelativePath);
                string? dir = Path.GetDirectoryName(destPath);
                if (!string.IsNullOrEmpty(dir))
                    _fileSystem.EnsureDirectoryExists(dir);

                long fileSize = _fileSystem.GetFileSize(item.FullSourcePath);
                string uncSource = _fileSystem.GetUncPath(item.FullSourcePath);
                string uncDest = _fileSystem.GetUncPath(destPath);

                long transferMs;
                long encryptionTimeMs;

                bool isLargeFile = largeFileThresholdBytes.HasValue && fileSize > largeFileThresholdBytes.Value;
                if (isLargeFile)
                    await LargeFileSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

                try
                {
                    if (useEncryption && encryptExtensionsSet.Contains(Path.GetExtension(item.FullSourcePath)))
                    {
                        transferMs = await _fileEncryptor!.EncryptFileAsync(
                            item.FullSourcePath,
                            destPath,
                            config.EncryptionKeyPath!.Trim(),
                            cryptoSoftExePath,
                            cancellationToken).ConfigureAwait(false);
                        encryptionTimeMs = transferMs;
                    }
                    else
                    {
                        Progress<long> fileProgress = new Progress<long>(bytesCopied =>
                        {
                            UpdateProgress(bytesCopied, uncSource, uncDest);
                            long now = Environment.TickCount64;
                            if (now - lastReportTicks >= ProgressReportThrottleMs)
                            {
                                lastReportTicks = now;
                                progress?.Report(progressList[idx]);
                            }
                        });
                        transferMs = await _fileSystem.CopyFileAsync(item.FullSourcePath, destPath, fileProgress, cancellationToken).ConfigureAwait(false);
                        encryptionTimeMs = 0;
                    }
                }
                finally
                {
                    if (isLargeFile)
                        LargeFileSemaphore.Release();
                }

                string uncSourceLog = _fileSystem.GetUncPath(item.FullSourcePath);
                string uncDestLog = _fileSystem.GetUncPath(destPath);
                TimeSpan transferTime = TimeSpan.FromMilliseconds(Math.Abs(transferMs));
                await _logWriter.WriteAsync(new LogEntry(DateTime.UtcNow, job.Name, uncSourceLog, uncDestLog, fileSize, transferTime, encryptionTimeMs), cancellationToken).ConfigureAwait(false);

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
                    CurrentDestinationPath = uncDest,
                    EstimatedTimeRemainingSeconds = remainingSize > 0 && (DateTime.UtcNow - jobStartUtc).TotalSeconds > 0.5
                        ? (double?)(remainingSize / (bytesCompleted / (DateTime.UtcNow - jobStartUtc).TotalSeconds))
                        : null
                };
                await WriteStateAndReportAsync(progressList, idx, progress, cancellationToken).ConfigureAwait(false);

                if (!string.IsNullOrWhiteSpace(config.BusinessSoftwareProcessName) && _businessSoftwareDetector.IsRunning(config.BusinessSoftwareProcessName))
                {
                    LogEntry stopEntry = new LogEntry(DateTime.UtcNow, job.Name, "", "", 0, TimeSpan.Zero, 0, reason: StopReasonBusinessSoftware);
                    await _logWriter.WriteAsync(stopEntry, cancellationToken).ConfigureAwait(false);
                    onBusinessSoftwareDetected?.Invoke();
                    return;
                }
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
                CurrentDestinationPath = null,
                EstimatedTimeRemainingSeconds = null
            };

            if (job.Type == BackupType.Full)
                await _configRepository.UpdateLastFullBackupAsync(job.Id, DateTime.UtcNow, cancellationToken).ConfigureAwait(false);

            await WriteStateAndReportAsync(progressList, idx, progress, cancellationToken).ConfigureAwait(false);
        }
    }
}
