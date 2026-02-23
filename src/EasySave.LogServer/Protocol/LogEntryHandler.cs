using System;
using System.Threading;
using System.Threading.Tasks;
using EasyLog;
using EasySave.Core.Models;

namespace EasySave.LogServer.Protocol;

/// <summary>
/// Handles received log DTOs: validates and forwards them to the configured <see cref="ILogWriter"/>.
/// Converts <see cref="LogEntryDto"/> to <see cref="LogEntry"/> for storage (Adapter pattern).
/// </summary>
public sealed class LogEntryHandler : ILogEntryHandler
{
    private readonly ILogWriter _logWriter;

    public LogEntryHandler(ILogWriter logWriter)
    {
        _logWriter = logWriter ?? throw new ArgumentNullException(nameof(logWriter));
    }

    /// <inheritdoc />
    public async Task<bool> HandleAsync(LogEntryDto dto, CancellationToken cancellationToken)
    {
        if (dto == null)
            return false;

        if (string.IsNullOrWhiteSpace(dto.BackupName))
            return false;

        LogEntry entry = new LogEntry(
            dto.TimeStamp,
            dto.BackupName,
            dto.SourcePath ?? string.Empty,
            dto.DestinationPath ?? string.Empty,
            dto.FileSizeBytes,
            TimeSpan.FromMilliseconds(dto.TransferTimeMs),
            dto.EncryptionTimeMs,
            dto.Reason);

        await _logWriter.WriteAsync(entry, cancellationToken).ConfigureAwait(false);
        return true;
    }
}
