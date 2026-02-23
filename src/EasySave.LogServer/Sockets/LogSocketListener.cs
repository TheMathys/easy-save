using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EasySave.LogServer.Protocol;

namespace EasySave.LogServer.Sockets;

/// <summary>
/// Listens for incoming TCP connections and processes newline-delimited JSON log entries per line.
/// One connection can send multiple lines; each line is one JSON <see cref="LogEntryDto"/>.
/// Sends "OK" or "ERR" per line (synchronous response per log entry).
/// </summary>
public sealed class LogSocketListener
{
    private readonly ILogEntryHandler _handler;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly int _port;

    public LogSocketListener(ILogEntryHandler handler, int port)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        _port = port;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    /// <summary>
    /// Starts listening on the configured port and accepts clients until cancellation.
    /// Each client is handled in a separate task (fire-and-forget).
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        TcpListener listener = new TcpListener(IPAddress.Any, _port);
        listener.Start();
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                TcpClient client = await listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
                _ = HandleClientAsync(client, cancellationToken);
            }
        }
        finally
        {
            listener.Stop();
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        try
        {
            await using NetworkStream stream = client.GetStream();
            using StreamReader reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
            using StreamWriter writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };

            string? line;
            while ((line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)) != null)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    await writer.WriteLineAsync("OK").ConfigureAwait(false);
                    continue;
                }

                bool ok = await ProcessLineAsync(line, cancellationToken).ConfigureAwait(false);
                await writer.WriteLineAsync(ok ? "OK" : "ERR").ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Client error: {ex.Message}");
        }
        finally
        {
            client.Dispose();
        }
    }

    private async Task<bool> ProcessLineAsync(string line, CancellationToken cancellationToken)
    {
        try
        {
            LogEntryDto? dto = JsonSerializer.Deserialize<LogEntryDto>(line, _jsonOptions);
            if (dto == null)
                return false;
            return await _handler.HandleAsync(dto, cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
