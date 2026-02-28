using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EasySave.Core.Interfaces;
using EasySave.Core.Models;

namespace EasySave.Infrastructure.Logging;

/// <summary>
/// Adapter: sends log entries to the centralized log server via TCP (newline-delimited JSON).
/// Does not throw: all exceptions are caught so that backup execution is never failed by log sending.
/// </summary>
public sealed class CentralizedLogClient : ICentralizedLogClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private const int DefaultPort = 9050;
    private const int ConnectTimeoutMs = 3000;
    private const int IoTimeoutMs = 2000;
    private const int FailureBackoffMs = 30000;

    private long _nextAllowedAttemptUtcMs;

    /// <inheritdoc />
    public async Task SendAsync(LogEntry entry, string? serverAddress, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(serverAddress))
            return;

        long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (nowMs < Volatile.Read(ref _nextAllowedAttemptUtcMs))
            return;

        (string host, int port) = ParseAddress(serverAddress.Trim());
        if (string.IsNullOrEmpty(host))
            return;

        try
        {
            await SendOneAsync(entry, host, port, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected when backup is cancelled; do not log.
            if (!cancellationToken.IsCancellationRequested)
            {
                Volatile.Write(ref _nextAllowedAttemptUtcMs, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + FailureBackoffMs);
            }
        }
        catch (Exception)
        {
            // Do not fail the backup: swallow and optionally trace in debug builds.
            Volatile.Write(ref _nextAllowedAttemptUtcMs, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + FailureBackoffMs);
#if DEBUG
            // Trace or log for diagnostics; in release we stay silent.
#endif
        }
    }

    private static async Task SendOneAsync(LogEntry entry, string host, int port, CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(ConnectTimeoutMs);

        using var client = new TcpClient();
        await client.ConnectAsync(host, port, cts.Token).ConfigureAwait(false);
        client.SendTimeout = IoTimeoutMs;
        client.ReceiveTimeout = IoTimeoutMs;

        using NetworkStream stream = client.GetStream();
        using var ioCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        ioCts.CancelAfter(IoTimeoutMs);
        CancellationToken ioToken = ioCts.Token;
        var dto = new CentralizedLogEntryDto
        {
            TimeStamp = entry.TimeStamp,
            BackupName = entry.BackupName,
            SourcePath = entry.SourcePath,
            DestinationPath = entry.DestinationPath,
            FileSizeBytes = entry.FileSizeBytes,
            TransferTimeMs = (long)entry.TransferTimeMs.TotalMilliseconds,
            EncryptionTimeMs = entry.EncryptionTimeMs,
            Reason = entry.Reason
        };
        string json = JsonSerializer.Serialize(dto, JsonOptions);
        byte[] line = Encoding.UTF8.GetBytes(json + "\n");
        await stream.WriteAsync(line, ioToken).ConfigureAwait(false);
        await stream.FlushAsync(ioToken).ConfigureAwait(false);

        // Read response line (OK or ERR) so server can complete the request.
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: false);
        await reader.ReadLineAsync(ioToken).ConfigureAwait(false);
    }

    private static (string host, int port) ParseAddress(string address)
    {
        int colon = address.LastIndexOf(':');
        if (colon <= 0)
            return (address, DefaultPort);
        if (colon == address.Length - 1)
            return (address.Substring(0, colon), DefaultPort);
        if (int.TryParse(address.AsSpan(colon + 1), out int port) && port > 0 && port < 65536)
            return (address.Substring(0, colon), port);
        return (address, DefaultPort);
    }

    /// <summary>
    /// DTO for the wire format (matches LogServer LogEntryDto: camelCase, TransferTimeMs as long).
    /// </summary>
    private sealed class CentralizedLogEntryDto
    {
        public DateTime TimeStamp { get; set; }
        public string BackupName { get; set; } = string.Empty;
        public string SourcePath { get; set; } = string.Empty;
        public string DestinationPath { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }
        public long TransferTimeMs { get; set; }
        public long EncryptionTimeMs { get; set; }
        public string? Reason { get; set; }
    }
}
