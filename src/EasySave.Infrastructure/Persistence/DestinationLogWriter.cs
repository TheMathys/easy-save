using System;
using System.Threading;
using System.Threading.Tasks;
using EasyLog;
using EasySave.Core.Entities;
using EasySave.Core.Enums;
using EasySave.Core.Interfaces;
using EasySave.Core.Models;

namespace EasySave.Infrastructure.Persistence;

/// <summary>
/// <see cref="ILogWriter"/> that routes log entries according to <see cref="BackupConfiguration.LogDestination"/>:
/// local only, centralized server only, or both. Local writing is delegated to the configured format writer (JSON/XML).
/// Centralized sending never throws so that backup execution is not failed by network errors.
/// </summary>
public sealed class DestinationLogWriter : ILogWriter
{
    private const int ConfigurationCacheDurationMs = 2000;

    private readonly IConfigurationRepository _configRepository;
    private readonly ConfigurableLogWriter _localWriter;
    private readonly ICentralizedLogClient _centralizedClient;
    private readonly SemaphoreSlim _routingCacheLock = new(1, 1);
    private LogRouting? _cachedRouting;
    private long _nextRoutingRefreshAtUtcMs;

    public DestinationLogWriter(
        IConfigurationRepository configRepository,
        ConfigurableLogWriter localWriter,
        ICentralizedLogClient centralizedClient)
    {
        _configRepository = configRepository ?? throw new ArgumentNullException(nameof(configRepository));
        _localWriter = localWriter ?? throw new ArgumentNullException(nameof(localWriter));
        _centralizedClient = centralizedClient ?? throw new ArgumentNullException(nameof(centralizedClient));
    }

    /// <inheritdoc />
    public async Task WriteAllTextAsync<T>(T logEntry, CancellationToken cancellationToken)
    {
        LogRouting routing = await GetRoutingAsync(cancellationToken).ConfigureAwait(false);

        if (routing.WriteLocal)
            await _localWriter.WriteAllTextAsync(logEntry, cancellationToken).ConfigureAwait(false);

        if (routing.SendCentral && logEntry is LogEntry entry)
            await _centralizedClient.SendAsync(entry, routing.ServerAddress, cancellationToken).ConfigureAwait(false);
    }

    private async Task<LogRouting> GetRoutingAsync(CancellationToken cancellationToken)
    {
        long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        LogRouting? cached = Volatile.Read(ref _cachedRouting);
        if (cached != null && nowMs < Volatile.Read(ref _nextRoutingRefreshAtUtcMs))
            return cached;

        await _routingCacheLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            cached = _cachedRouting;
            if (cached != null && nowMs < _nextRoutingRefreshAtUtcMs)
                return cached;

            BackupConfiguration? config = await _configRepository.LoadAsync(cancellationToken).ConfigureAwait(false);
            LogDestination destination = config?.LogDestination ?? LogDestination.Local;
            string? serverAddress = config?.CentralizedLogServerAddress;

            LogRouting refreshed = new(
                WriteLocal: destination == LogDestination.Local || destination == LogDestination.LocalAndCentralized,
                SendCentral: (destination == LogDestination.Centralized || destination == LogDestination.LocalAndCentralized)
                    && !string.IsNullOrWhiteSpace(serverAddress),
                ServerAddress: serverAddress);

            Volatile.Write(ref _cachedRouting, refreshed);
            Volatile.Write(ref _nextRoutingRefreshAtUtcMs, nowMs + ConfigurationCacheDurationMs);
            return refreshed;
        }
        finally
        {
            _routingCacheLock.Release();
        }
    }

    private sealed record LogRouting(bool WriteLocal, bool SendCentral, string? ServerAddress);
}
