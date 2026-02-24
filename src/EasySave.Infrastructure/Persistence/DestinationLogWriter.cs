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
    private readonly IConfigurationRepository _configRepository;
    private readonly ConfigurableLogWriter _localWriter;
    private readonly ICentralizedLogClient _centralizedClient;

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
    public async Task WriteAsync<T>(T logEntry, CancellationToken cancellationToken)
    {
        BackupConfiguration? config = await _configRepository.LoadAsync(cancellationToken).ConfigureAwait(false);
        LogDestination destination = config?.LogDestination ?? LogDestination.Local;
        string? serverAddress = config?.CentralizedLogServerAddress;

        bool writeLocal = destination == LogDestination.Local || destination == LogDestination.LocalAndCentralized;
        bool sendCentral = (destination == LogDestination.Centralized || destination == LogDestination.LocalAndCentralized)
            && !string.IsNullOrWhiteSpace(serverAddress);

        if (writeLocal)
            await _localWriter.WriteAsync(logEntry, cancellationToken).ConfigureAwait(false);

        if (sendCentral && logEntry is LogEntry entry)
            await _centralizedClient.SendAsync(entry, serverAddress, cancellationToken).ConfigureAwait(false);
    }
}
