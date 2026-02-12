using System;
using System.Threading;
using System.Threading.Tasks;
using EasyLog;
using EasySave.Core.Entities;
using EasySave.Core.Enums;
using EasySave.Core.Interfaces;

namespace EasySave.Infrastructure.Persistence
{
    /// <summary>
    /// <see cref="ILogWriter"/> implementation that dynamically selects the log format
    /// (JSON or XML) based on the application configuration (<see cref="BackupConfiguration.LogFileFormat" />).
    /// </summary>
    public sealed class ConfigurableLogWriter : ILogWriter
    {
        private readonly IConfigurationRepository _configRepository;
        private readonly ILogWriter _jsonWriter;
        private readonly ILogWriter _xmlWriter;

        public ConfigurableLogWriter(
            IConfigurationRepository configRepository,
            ILogWriter jsonWriter,
            ILogWriter xmlWriter)
        {
            _configRepository = configRepository ?? throw new ArgumentNullException(nameof(configRepository));
            _jsonWriter = jsonWriter ?? throw new ArgumentNullException(nameof(jsonWriter));
            _xmlWriter = xmlWriter ?? throw new ArgumentNullException(nameof(xmlWriter));
        }

        public async Task WriteAsync<T>(T logEntry, CancellationToken cancellationToken)
        {
            BackupConfiguration? config = await _configRepository.LoadAsync(cancellationToken).ConfigureAwait(false);
            LogFileFormat format = config?.LogFileFormat ?? LogFileFormat.Json;

            switch (format)
            {
                case LogFileFormat.Xml:
                    await _xmlWriter.WriteAsync(logEntry, cancellationToken).ConfigureAwait(false);
                    break;
                case LogFileFormat.Json:
                default:
                    await _jsonWriter.WriteAsync(logEntry, cancellationToken).ConfigureAwait(false);
                    break;
            }
        }
    }
}

