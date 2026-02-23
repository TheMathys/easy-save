using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EasyLog;
using EasySave.LogServer.Protocol;
using EasySave.LogServer.Sockets;

namespace EasySave.LogServer;

/// <summary>
/// Entry point for the log centralization service. Reads configuration from environment variables,
/// composes the log writer and socket listener, and runs until shutdown.
/// </summary>
internal static class Program
{
    private const string EnvPort = "LOG_SERVER_PORT";
    private const string EnvLogDir = "LOG_DIR";
    private const int DefaultPort = 9050;
    private const string DefaultLogDir = "/logs";

    public static async Task<int> Main(string[] args)
    {
        int port = GetIntFromEnv(EnvPort, DefaultPort);
        string logDir = Environment.GetEnvironmentVariable(EnvLogDir) ?? DefaultLogDir;

        string resolvedLogDir = Path.GetFullPath(logDir);
        if (!Directory.Exists(resolvedLogDir))
            Directory.CreateDirectory(resolvedLogDir);

        ILogWriter logWriter = new DailyLogWriter(resolvedLogDir);
        ILogEntryHandler handler = new LogEntryHandler(logWriter);
        LogSocketListener listener = new LogSocketListener(handler, port);

        Console.WriteLine($"Log server listening on port {port}. Log directory: {resolvedLogDir}");
        Console.WriteLine("Send newline-delimited JSON log entries (one per line). Response: OK or ERR per line.");

        using CancellationTokenSource cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        try
        {
            await listener.RunAsync(cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }

        Console.WriteLine("Log server stopped.");
        return 0;
    }

    private static int GetIntFromEnv(string name, int defaultValue)
    {
        string? value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value))
            return defaultValue;
        return int.TryParse(value.Trim(), out int result) && result > 0 && result < 65536 ? result : defaultValue;
    }
}
