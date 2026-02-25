using System;
using System.Collections.Generic;
using EasySave.Core.Enums;

namespace EasySave.Core.Entities
{
    /// <summary>
    /// Global configuration: list of jobs and paths for log/state files.
    /// </summary>
    public sealed class BackupConfiguration
    {
        /// <summary>
        /// Directory where the daily log and the state file are stored (not c:\temp\).
        /// </summary>
        public string LogAndStateDirectory { get; init; } = string.Empty;

        /// <summary>
        /// Log file format (JSON or XML). Default: JSON for backward compatibility.
        /// </summary>
        public LogFileFormat LogFileFormat { get; init; } = LogFileFormat.Json;

        /// <summary>
        /// Where logs are written: local only, centralized server only, or both.
        /// </summary>
        public LogDestination LogDestination { get; init; } = LogDestination.Local;

        /// <summary>
        /// Address of the centralized log server (host or host:port). Used when
        /// <see cref="LogDestination"/> is <see cref="LogDestination.Centralized"/> or
        /// <see cref="LogDestination.LocalAndCentralized"/>. Default port is 9050.
        /// </summary>
        public string? CentralizedLogServerAddress { get; init; }

        /// <summary>
        /// List of backup jobs.
        /// </summary>
        public IReadOnlyList<BackupJob> Jobs { get; init; } = Array.Empty<BackupJob>();

        /// <summary>
        /// Date of the last full backup per job identifier (used for differential backups).
        /// </summary>
        public IReadOnlyDictionary<int, DateTime> LastFullBackupUtcByJobId { get; init; } = new Dictionary<int, DateTime>();

        /// <summary>
        /// File extensions that must be encrypted during backup (e.g. .doc, .pdf).
        /// Only files with these extensions are passed to the external encryption process.
        /// </summary>
        public IReadOnlyList<string> EncryptExtensions { get; init; } = Array.Empty<string>();

        /// <summary>
        /// Full path to the encryption key file used by the external encryption tool.
        /// If null or empty, encryption is skipped even when <see cref="EncryptExtensions"/> is set.
        /// </summary>
        public string? EncryptionKeyPath { get; init; }

        /// <summary>
        /// Process name of the "business software" to detect (e.g. "Calculator" for Calculator.exe).
        /// When set, backup start is blocked if this process is running; during backup, execution stops after the current file.
        /// Null or empty = feature disabled.
        /// </summary>
        public string? BusinessSoftwareProcessName { get; init; }

        /// <summary>
        /// When true the GUI should use a dark theme. Default: false (light theme).
        /// </summary>
        public bool UseDarkTheme { get; init; } = false;

        /// <summary>
        /// UI text scale percentage for the GUI (e.g. 75, 100, 125).
        /// 100 means "system/default" size.
        /// </summary>
        public int TextScalePercent { get; init; } = 100;

        /// <summary>
        /// Threshold, in kilobytes, above which a file is considered a "large file"
        /// for concurrency throttling. When greater than zero, at most one file whose
        /// size is strictly greater than this threshold is allowed to be transferred
        /// at the same time across all running jobs.
        /// Null or a non-positive value disables the large file concurrency rule.
        /// </summary>
        public int? LargeFileThresholdKb { get; init; }
    }
}
