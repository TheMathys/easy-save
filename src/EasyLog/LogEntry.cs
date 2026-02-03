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
        public DateTime TimeStamp { get; } // Date and time of the log entry
        public string BackupName { get; } // Name of the backup job
        public string SourcePath { get; } // Source path of the backup
        public string DestinationPath { get; } // Destination path of the backup
        public long FileSizeBytes { get; } // Size of the file transferred in bytes
        public TimeSpan TrasnferTimeMs { get; } // Time taken to transfer the file

        /// <summary>
        /// Initializes a new instance of the LogEntry class.
        /// </summary>
        public LogEntry(DateTime timeStamp, string backupName, string sourcePath, string destinationPath,long fileSizeBytes, TimeSpan transferTimeMs)
        {
            TimeStamp = timeStamp;
            BackupName = backupName;
            SourcePath = sourcePath;
            DestinationPath = destinationPath;
            FileSizeBytes = fileSizeBytes;
            TrasnferTimeMs = transferTimeMs;
        }
    }
}