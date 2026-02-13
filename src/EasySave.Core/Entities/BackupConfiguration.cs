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
    }
}
