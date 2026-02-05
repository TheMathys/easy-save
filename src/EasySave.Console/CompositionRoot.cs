using System;
using System.IO;
using EasyLog;
using EasySave.Core.Interfaces;
using EasySave.Infrastructure.Backup;
using EasySave.Infrastructure.FileSystem;
using EasySave.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace EasySave.ConsoleApp;

/// <summary>
/// Central place where all EasySave services are wired together.
/// Builds an <see cref="IServiceProvider"/> from a given base path.
/// </summary>
public static class CompositionRoot
{
    /// <summary>
    /// Builds the service provider and registers all EasySave services as singletons.
    /// </summary>
    /// <param name="basePath">
    /// Base directory used for configuration, state and log files.
    /// </param>
    public static IServiceProvider Build(string basePath)
    {
        if (string.IsNullOrWhiteSpace(basePath))
            throw new ArgumentNullException(nameof(basePath));

        string normalizedBasePath = Path.GetFullPath(basePath);

        string configDirectory = Path.Combine(normalizedBasePath, "config");
        string stateDirectory = Path.Combine(normalizedBasePath, "state");
        string stateFilePath = Path.Combine(stateDirectory, "state.json");
        string logDirectory = Path.Combine(normalizedBasePath, "logs");

        ServiceCollection services = new();

        services.AddSingleton<IConfigurationRepository>(_ => new JsonConfigurationRepository(basePath));
        services.AddSingleton<IFileSystemService>(_ => new FileSystemService());
        services.AddSingleton<IStateWriter>(sp =>
        {
            var path = Path.Combine(basePath, "state.json");
            return new JsonStateWriter(path);
        });
        services.AddSingleton<ILogWriter>(_ => new DailyLogWriter(basePath));
        services.AddSingleton<IBackupStrategyFactory>(_ => new BackupStrategyFactory());
        services.AddSingleton<IBackupExecutor>(sp => new BackupExecutor(
            sp.GetRequiredService<IConfigurationRepository>(),
            sp.GetRequiredService<IBackupStrategyFactory>(),
            sp.GetRequiredService<IFileSystemService>(),
            sp.GetRequiredService<IStateWriter>(),
            sp.GetRequiredService<ILogWriter>()));

        return services.BuildServiceProvider();
    }
}
