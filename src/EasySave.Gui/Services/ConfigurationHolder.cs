using System.Collections.Generic;
using EasySave.Core.Entities;
using EasySave.Core.Enums;
using EasySave.Core.Interfaces;

namespace EasySave.Gui.Services;

/// <summary>
/// Default implementation of <see cref="IConfigurationHolder"/>, keeping the
/// current configuration in memory and delegating persistence to <see cref="IConfigurationRepository"/>.
/// </summary>
public sealed class ConfigurationHolder : IConfigurationHolder
{
    private readonly IConfigurationRepository _repository;
    private readonly Func<string> _getBaseDirectory;
    private BackupConfiguration _current;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationHolder"/> class.
    /// </summary>
    /// <param name="repository">Repository used to load and save configuration.</param>
    /// <param name="paths">Helper containing the base directory for configuration files.</param>
    public ConfigurationHolder(IConfigurationRepository repository, EasySavePaths paths)
        : this(repository, () => paths?.BaseDirectory ?? throw new ArgumentNullException(nameof(paths)))
    {
    }

    /// <summary>
    /// Initializes with a delegate to resolve the base directory at runtime (e.g. for mutable paths).
    /// </summary>
    public ConfigurationHolder(IConfigurationRepository repository, Func<string> getBaseDirectory)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _getBaseDirectory = getBaseDirectory ?? throw new ArgumentNullException(nameof(getBaseDirectory));
        _current = BuildDefault(_getBaseDirectory());
    }

    /// <inheritdoc />
    public BackupConfiguration Current => _current;

    /// <inheritdoc />
    public event EventHandler? ConfigurationChanged;

    /// <inheritdoc />
    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        BackupConfiguration? loaded = await _repository.LoadAsync(cancellationToken).ConfigureAwait(false);
        _current = loaded ?? BuildDefault(_getBaseDirectory());
        ConfigurationChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public async Task SaveAsync(BackupConfiguration configuration, CancellationToken cancellationToken = default)
    {
        await _repository.SaveAsync(configuration, cancellationToken).ConfigureAwait(false);
        _current = configuration;
        ConfigurationChanged?.Invoke(this, EventArgs.Empty);
    }

    private static BackupConfiguration BuildDefault(string baseDirectory)
    {
        return new BackupConfiguration
        {
            LogAndStateDirectory = baseDirectory,
            LogFileFormat = LogFileFormat.Json,
            LogDestination = LogDestination.Local,
            Jobs = Array.Empty<BackupJob>(),
            LastFullBackupUtcByJobId = new Dictionary<int, DateTime>(),
            UseDarkTheme = false,
            LargeFileThresholdKb = null,
            PriorityExtensions = Array.Empty<string>()
        };
    }
}
