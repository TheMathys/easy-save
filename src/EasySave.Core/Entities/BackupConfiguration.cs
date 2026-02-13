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
    }
}
