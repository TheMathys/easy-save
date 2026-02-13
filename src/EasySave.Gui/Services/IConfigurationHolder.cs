using EasySave.Core.Entities;

namespace EasySave.Gui.Services;

/// <summary>
/// Holds the current backup configuration in memory as a single source of truth,
/// shared across all tabs for smooth navigation without reloading from disk.
/// </summary>
public interface IConfigurationHolder
{
    /// <summary>
    /// Current configuration instance shared by all tabs.
    /// </summary>
    BackupConfiguration Current { get; }

    /// <summary>
    /// Raised whenever the configuration has been reloaded or saved.
    /// </summary>
    event EventHandler? ConfigurationChanged;

    /// <summary>
    /// Reloads the configuration from the underlying repository.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    Task ReloadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists the provided configuration and updates <see cref="Current"/>.
    /// </summary>
    /// <param name="configuration">Configuration to persist.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    Task SaveAsync(BackupConfiguration configuration, CancellationToken cancellationToken = default);
}
