using System;

namespace EasySave.LogServer.Protocol;

/// <summary>
/// Data transfer object for a log entry received over the wire. Mirrors <see cref="EasySave.Core.Models.LogEntry"/>
/// with settable properties for JSON deserialization. TimeSpan is sent as total milliseconds (long).
/// </summary>
public sealed class LogEntryDto
{
    /// <summary>Date and time of the log entry (ISO 8601 or .NET format).</summary>
    public DateTime TimeStamp { get; set; }

    /// <summary>Name of the backup job.</summary>
    public string BackupName { get; set; } = string.Empty;

    /// <summary>Source path of the backup.</summary>
    public string SourcePath { get; set; } = string.Empty;

    /// <summary>Destination path of the backup.</summary>
    public string DestinationPath { get; set; } = string.Empty;

    /// <summary>Size of the file transferred in bytes.</summary>
    public long FileSizeBytes { get; set; }

    /// <summary>Time taken to transfer the file: total milliseconds (stored as long for JSON).</summary>
    public long TransferTimeMs { get; set; }

    /// <summary>Time required to encrypt the file in milliseconds. 0 = none, &gt;0 = ms, &lt;0 = error code.</summary>
    public long EncryptionTimeMs { get; set; }

    /// <summary>Optional stop reason (e.g. BusinessSoftwareDetected). When set, paths/sizes may be empty.</summary>
    public string? Reason { get; set; }
}
