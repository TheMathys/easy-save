using System;
using System.IO;
using EasyLog;
using EasySave.Core.Interfaces;
using EasySave.Gui.Services;
using EasySave.Gui.ViewModels;
using EasySave.Infrastructure.Backup;
using EasySave.Infrastructure.FileSystem;
using EasySave.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace EasySave.Gui;

/// <summary>
/// Central composition root for the GUI application.
/// Registers infrastructure services and ViewModels in the dependency injection container.
/// </summary>
public static class CompositionRoot
{
    /// <summary>
    /// Builds and configures the <see cref="IServiceProvider"/> for the GUI layer.
    /// </summary>
    /// <param name="basePath">
    /// Base directory that will be used by infrastructure services to locate configuration,
    /// state and log files.
    /// </param>
    /// <returns>The fully configured service provider.</returns>
    public static IServiceProvider Build(string basePath)
    {
        if (string.IsNullOrWhiteSpace(basePath))
            throw new ArgumentNullException(nameof(basePath));

        string normalizedBasePath = Path.GetFullPath(basePath);
        var services = new ServiceCollection();

        // Paths and persistence
        services.AddSingleton(new EasySavePaths(normalizedBasePath));
        services.AddSingleton<IConfigurationRepository>(sp =>
            new JsonConfigurationRepository(sp.GetRequiredService<EasySavePaths>().BaseDirectory));
        services.AddSingleton<IFileSystemService>(_ => new FileSystemService());
        services.AddSingleton<IStateWriter>(sp =>
        {
            var paths = sp.GetRequiredService<EasySavePaths>();
            return new JsonStateWriter(paths.StateFilePath);
        });

        // Logging
        services.AddSingleton<DailyLogWriter>(sp => new DailyLogWriter(sp.GetRequiredService<EasySavePaths>().LogDirectory));
        services.AddSingleton<XmlDailyLogWriter>(sp => new XmlDailyLogWriter(sp.GetRequiredService<EasySavePaths>().LogDirectory));
        services.AddSingleton<ILogWriter>(sp =>
        {
            var configRepo = sp.GetRequiredService<IConfigurationRepository>();
            var jsonWriter = sp.GetRequiredService<DailyLogWriter>();
            var xmlWriter = sp.GetRequiredService<XmlDailyLogWriter>();
            return new ConfigurableLogWriter(configRepo, jsonWriter, xmlWriter);
        });

        // Backup execution
        services.AddSingleton<IBackupStrategyFactory>(_ => new BackupStrategyFactory());
        services.AddSingleton<IBackupExecutor>(sp => new BackupExecutor(
            sp.GetRequiredService<IConfigurationRepository>(),
            sp.GetRequiredService<IBackupStrategyFactory>(),
            sp.GetRequiredService<IFileSystemService>(),
            sp.GetRequiredService<IStateWriter>(),
            sp.GetRequiredService<ILogWriter>()));

        // GUI services (abstractions for SOLID)
        services.AddSingleton<ILocalizationProvider, LocalizationProvider>();
        services.AddSingleton<IConfigurationHolder, ConfigurationHolder>();
        services.AddSingleton<IFolderPickerService, FolderPickerService>();

        // ViewModels (one per screen / tab)
        services.AddTransient<JobsTabViewModel>();
        services.AddTransient<CreateEditJobViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<MainWindowViewModel>();

        return services.BuildServiceProvider();
    }
}
