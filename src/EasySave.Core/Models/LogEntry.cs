using System;

namespace EasySave.Core.Models
{
    /// <summary>
    /// Represents a single backup log entry for EasySave.
    /// </summary>
    public class LogEntry
    {
        /// <summary>
        /// Date and time of the log entry.
        /// </summary>
        public DateTime TimeStamp { get; }

        /// <summary>
        /// Name of the backup job.
        /// </summary>
        public string BackupName { get; }

        /// <summary>
        /// Source path of the backup.
        /// </summary>
        public string SourcePath { get; }

        /// <summary>
        /// Destination path of the backup.
        /// </summary>
        public string DestinationPath { get; }

        /// <summary>
        /// Size of the file transferred in bytes.
        /// </summary>
        public long FileSizeBytes { get; }

        /// <summary>
        /// Time taken to transfer the file.
        /// </summary>
        public TimeSpan TransferTimeMs { get; }

        /// <summary>
        /// Time required to encrypt the file in milliseconds (log journalier).
        /// 0 = pas de cryptage, &gt;0 = temps de cryptage (ms), &lt;0 = code erreur.
        /// </summary>
        public long EncryptionTimeMs { get; }

        /// <summary>
        /// Optional stop reason (e.g. "BusinessSoftwareDetected"). When set, this entry is a stop record; paths/sizes may be empty.
        /// </summary>
        public string? Reason { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="LogEntry"/> class.
        /// </summary>
        /// <param name="timeStamp">Date and time of the log entry.</param>
        /// <param name="backupName">Name of the backup job.</param>
        /// <param name="sourcePath">Source path of the backup.</param>
        /// <param name="destinationPath">Destination path of the backup.</param>
        /// <param name="fileSizeBytes">Size of the file transferred in bytes.</param>
        /// <param name="transferTimeMs">Time taken to transfer the file.</param>
        /// <param name="encryptionTimeMs">Temps nécessaire au cryptage (ms): 0 = pas de cryptage, &gt;0 = temps en ms, &lt;0 = code erreur.</param>
        /// <param name="reason">Optional stop reason (e.g. BusinessSoftwareDetected) for stop-only entries.</param>
        public LogEntry(DateTime timeStamp, string backupName, string sourcePath, string destinationPath, long fileSizeBytes, TimeSpan transferTimeMs, long encryptionTimeMs = 0, string? reason = null)
        {
            TimeStamp = timeStamp;
            BackupName = backupName;
            SourcePath = sourcePath ?? string.Empty;
            DestinationPath = destinationPath ?? string.Empty;
            FileSizeBytes = fileSizeBytes;
            TransferTimeMs = transferTimeMs;
            EncryptionTimeMs = encryptionTimeMs;
            Reason = reason;
        }
    }
}

