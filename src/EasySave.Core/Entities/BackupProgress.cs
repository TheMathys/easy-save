using EasySave.Core.Enums;
using System;

namespace EasySave.Core.Entities
{
    /// <summary>
    /// Backup job progress (for the real-time status file).
    /// </summary>
    public sealed class BackupProgress
    {
        /// <summary>Backup job identifier.</summary>
        public int JobId { get; init; }

        /// <summary>Backup job name.</summary>
        public string BackupName { get; init; } = string.Empty;

        /// <summary>Timestamp of the last action.</summary>
        public DateTime LastActionTimestamp { get; set; }

        /// <summary>Job state (Active, Inactive, etc.).</summary>
        public BackupState State { get; init; }

        /// <summary>Total number of eligible files (if active).</summary>
        public int TotalFilesCount { get; set; }

        /// <summary>Total size of files to transfer in bytes (if active).</summary>
        public long TotalSizeBytes { get; set; }

        /// <summary>Progress percentage (0–100) (if active).</summary>
        public double ProgressPercent { get; set; }

        /// <summary>Number of remaining files (if active).</summary>
        public int RemainingFilesCount { get; set; }

        /// <summary>Size of remaining files in bytes (if active).</summary>
        public long RemainingSizeBytes { get; set; }

        /// <summary>Full path of the current source file (if active).</summary>
        public string? CurrentSourcePath { get; set; }

        /// <summary>Full path of the current destination file (if active).</summary>
        public string? CurrentDestinationPath { get; set; }

        /// <summary>Estimated time remaining in seconds.</summary>
        public double? EstimatedTimeRemainingSeconds { get; set; }

        /// <summary>Elapsed time in seconds since this backup job started.</summary>
        public double? ElapsedTimeSeconds { get; set; }
    }
}
