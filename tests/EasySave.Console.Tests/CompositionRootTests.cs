using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EasyLog;
using EasySave.Core.Entities;
using EasySave.Core.Interfaces;
using EasySave.Infrastructure.Backup;
using EasySave.Infrastructure.FileSystem;
using EasySave.Infrastructure.Persistence;
using EasySave.ConsoleApp;
using Microsoft.Extensions.DependencyInjection;

namespace EasySave.Console.Tests;

public sealed class CompositionRootTests : IDisposable
{
    private readonly string _basePath;

    public CompositionRootTests()
    {
        _basePath = Path.Combine(Path.GetTempPath(), "EasySave.Console.Tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_basePath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_basePath))
        {
            Directory.Delete(_basePath, recursive: true);
        }
    }

    [Fact]
    public void Build_ThrowsArgumentNullException_WhenBasePathIsNullOrWhitespace()
    {
        Assert.Throws<ArgumentNullException>(() => CompositionRoot.Build(null!));
        Assert.Throws<ArgumentNullException>(() => CompositionRoot.Build("   "));
    }

    [Fact]
    public void Build_Registers_AllCoreServices_AsSingletons()
    {
        IServiceProvider provider = CompositionRoot.Build(_basePath);

        IConfigurationRepository config1 = provider.GetRequiredService<IConfigurationRepository>();
        IConfigurationRepository config2 = provider.GetRequiredService<IConfigurationRepository>();
        IFileSystemService fs1 = provider.GetRequiredService<IFileSystemService>();
        IFileSystemService fs2 = provider.GetRequiredService<IFileSystemService>();
        IStateWriter state1 = provider.GetRequiredService<IStateWriter>();
        IStateWriter state2 = provider.GetRequiredService<IStateWriter>();
        ILogWriter log1 = provider.GetRequiredService<ILogWriter>();
        ILogWriter log2 = provider.GetRequiredService<ILogWriter>();
        IBackupStrategyFactory factory1 = provider.GetRequiredService<IBackupStrategyFactory>();
        IBackupStrategyFactory factory2 = provider.GetRequiredService<IBackupStrategyFactory>();
        IBackupExecutor exec1 = provider.GetRequiredService<IBackupExecutor>();
        IBackupExecutor exec2 = provider.GetRequiredService<IBackupExecutor>();

        Assert.IsType<JsonConfigurationRepository>(config1);
        Assert.IsType<FileSystemService>(fs1);
        Assert.IsType<JsonStateWriter>(state1);
        Assert.IsType<ConfigurableLogWriter>(log1);
        Assert.IsType<BackupStrategyFactory>(factory1);
        Assert.IsType<BackupExecutor>(exec1);

        Assert.Same(config1, config2);
        Assert.Same(fs1, fs2);
        Assert.Same(state1, state2);
        Assert.Same(log1, log2);
        Assert.Same(factory1, factory2);
        Assert.Same(exec1, exec2);
    }

    [Fact]
    public async Task Build_UsesBasePathForStateAndConfigFiles()
    {
        IServiceProvider provider = CompositionRoot.Build(_basePath);

        // Verify state.json written under basePath
        IStateWriter stateWriter = provider.GetRequiredService<IStateWriter>();
        List<BackupProgress> progress = new()
        {
            new BackupProgress
            {
                BackupName = "job1",
                LastActionTimestamp = DateTime.UtcNow,
                State = Core.Enums.BackupState.Active,
                TotalFilesCount = 1,
                TotalSizeBytes = 100
            }
        };

        await stateWriter.WriteStateAsync(progress, CancellationToken.None);

        string stateFile = Path.Combine(_basePath, "state.json");
        Assert.True(File.Exists(stateFile));

        // Verify backup-config.json written under basePath
        IConfigurationRepository configRepo = provider.GetRequiredService<IConfigurationRepository>();
        BackupConfiguration config = new()
        {
            LogAndStateDirectory = _basePath,
            Jobs = Array.Empty<BackupJob>(),
            LastFullBackupUtcByJobId = new Dictionary<int, DateTime>()
        };

        await configRepo.SaveAsync(config, CancellationToken.None);

        string configFile = Path.Combine(_basePath, "backup-config.json");
        Assert.True(File.Exists(configFile));
    }
}

