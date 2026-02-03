using System;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Text.Json;
using System.Threading;

namespace EasyLog
{
    /// <summary>
    /// LogEntry defines a single log entry for EasyLog.
    /// </summary>
    public class LogEntry
    {
        public DateTime Timestamp { get; } // Get-only to respect the read-only nature of log entries.
        public string BackupName { get;}
        public string SourcePath { get;}
        public string DestinationPath { get;}
        public long FileSizeBytes { get;}
        public TimeSpan TrasnferTimeMs { get;}

        /// <summary>
        /// Initializes a new instance of the LogEntry class.
        /// </summary>
        public LogEntry(string backupName, string sourcePath, string destinationPath,long fileSizeBytes, TimeSpan transferTimeMs)
        {
            Timestamp = DateTime.UtcNow;
            BackupName = backupName;
            SourcePath = sourcePath;
            DestinationPath = destinationPath;
            FileSizeBytes = fileSizeBytes;
            TrasnferTimeMs = transferTimeMs;
        }
    }
}