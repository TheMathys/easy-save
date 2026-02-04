using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using EasyLog;
using EasySave.Core.Entities;
using EasySave.Core.Enums;
using EasySave.Core.Interfaces;
using EasySave.Core.Models;
using EasySave.Infrastructure.Backup;
using EasySave.Infrastructure.FileSystem;

namespace EasySave.Infrastructure.Tests;

public sealed class BackupExecutorTests : IDisposable
{
    private readonly string _tempRoot;

    public BackupExecutorTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "EasySave.BackupExecutor.Tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempRoot))
                Directory.Delete(_tempRoot, recursive: true);
        }
        catch { }
    }

    [Fact]
    public async Task ExecuteAsync_DoesNothing_WhenConfigIsNull()
    {
        FakeConfigRepository configRepo = new(null);
        FakeStateWriter stateWriter = new();
        BackupExecutor executor = CreateExecutor(configRepository: configRepo, stateWriter: stateWriter);

        await executor.ExecuteAsync(new[] { 1 });

        Assert.Empty(stateWriter.WrittenStates);
    }

    [Fact]
    public async Task ExecuteAsync_DoesNothing_WhenConfigHasNoJobs()
    {
        BackupConfiguration config = new() { Jobs = Array.Empty<BackupJob>() };
        FakeConfigRepository configRepo = new(config);
        FakeStateWriter stateWriter = new();
        BackupExecutor executor = CreateExecutor(configRepository: configRepo, stateWriter: stateWriter);

        await executor.ExecuteAsync(new[] { 1 });

        Assert.Empty(stateWriter.WrittenStates);
    }

    [Fact]
    public async Task ExecuteAsync_SkipsUnknownJobIds()
    {
        string sourceDir = Path.Combine(_tempRoot, "source");
        string targetDir = Path.Combine(_tempRoot, "target");
        Directory.CreateDirectory(sourceDir);
        BackupJob job = new() { Id = 1, Name = "Job1", SourcePath = sourceDir, TargetPath = targetDir, Type = BackupType.Full };
        BackupConfiguration config = new() { Jobs = new[] { job } };
        FakeConfigRepository configRepo = new(config);
        FakeStateWriter stateWriter = new();
        BackupExecutor executor = CreateExecutor(configRepository: configRepo, stateWriter: stateWriter);

        await executor.ExecuteAsync(new[] { 99, 100 }); // unknown ids

        Assert.Single(stateWriter.WrittenStates); // only initial Inactive state
        Assert.All(stateWriter.WrittenStates[0], p => Assert.Equal(BackupState.Inactive, p.State));
    }

    [Fact]
    public async Task ExecuteAsync_WritesInitialStateWithInactiveJobs()
    {
        string sourceDir = Path.Combine(_tempRoot, "source");
        string targetDir = Path.Combine(_tempRoot, "target");
        Directory.CreateDirectory(sourceDir);
        BackupJob job = new() { Id = 1, Name = "Job1", SourcePath = sourceDir, TargetPath = targetDir, Type = BackupType.Full };
        BackupConfiguration config = new() { Jobs = new[] { job } };
        FakeConfigRepository configRepo = new(config);
        FakeStateWriter stateWriter = new();
        BackupExecutor executor = CreateExecutor(configRepository: configRepo, stateWriter: stateWriter);

        await executor.ExecuteAsync(new[] { 1 });

        Assert.True(stateWriter.WrittenStates.Count >= 1);
        IReadOnlyList<BackupProgress> initial = stateWriter.WrittenStates[0];
        BackupProgress first = Assert.Single(initial);
        Assert.Equal("Job1", first.BackupName);
        Assert.Equal(BackupState.Inactive, first.State);
    }

    [Fact]
    public async Task ExecuteAsync_CopiesFilesAndUpdatesState()
    {
        string sourceDir = Path.Combine(_tempRoot, "source");
        string targetDir = Path.Combine(_tempRoot, "target");
        Directory.CreateDirectory(sourceDir);
        string file1 = Path.Combine(sourceDir, "a.txt");
        string file2 = Path.Combine(sourceDir, "b.txt");
        await File.WriteAllTextAsync(file1, "content1");
        await File.WriteAllTextAsync(file2, "content2");

        BackupJob job = new() { Id = 1, Name = "Job1", SourcePath = sourceDir, TargetPath = targetDir, Type = BackupType.Full };
        BackupConfiguration config = new() { Jobs = new[] { job } };
        FakeConfigRepository configRepo = new(config);
        FakeStateWriter stateWriter = new();
        FileSystemService fileSystem = new();
        IBackupStrategyFactory strategyFactory = new BackupStrategyFactory();
        FakeLogWriter logWriter = new();

        BackupExecutor executor = new(configRepo, strategyFactory, fileSystem, stateWriter, logWriter);

        await executor.ExecuteAsync(new[] { 1 });

        string dest1 = Path.Combine(targetDir, "a.txt");
        string dest2 = Path.Combine(targetDir, "b.txt");
        Assert.True(File.Exists(dest1));
        Assert.True(File.Exists(dest2));
        Assert.Equal("content1", await File.ReadAllTextAsync(dest1));
        Assert.Equal("content2", await File.ReadAllTextAsync(dest2));

        Assert.True(stateWriter.WrittenStates.Count >= 1);
        IReadOnlyList<BackupProgress> lastState = stateWriter.WrittenStates[^1];
        BackupProgress completed = Assert.Single(lastState);
        Assert.Equal(BackupState.Completed, completed.State);
        Assert.Equal(100, completed.ProgressPercent);
        Assert.Equal(2, completed.TotalFilesCount);
    }

    [Fact]
    public async Task ExecuteAsync_CallsUpdateLastFullBackup_ForFullBackup()
    {
        string sourceDir = Path.Combine(_tempRoot, "source");
        string targetDir = Path.Combine(_tempRoot, "target");
        Directory.CreateDirectory(sourceDir);
        BackupJob job = new() { Id = 1, Name = "Job1", SourcePath = sourceDir, TargetPath = targetDir, Type = BackupType.Full };
        BackupConfiguration config = new() { Jobs = new[] { job } };
        FakeConfigRepository configRepo = new(config);
        BackupExecutor executor = CreateExecutor(configRepository: configRepo);

        await executor.ExecuteAsync(new[] { 1 });

        Assert.True(configRepo.UpdateLastFullBackupCalled);
        Assert.Equal(1, configRepo.LastUpdatedJobId);
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotCallUpdateLastFullBackup_ForDifferentialBackup()
    {
        string sourceDir = Path.Combine(_tempRoot, "source");
        string targetDir = Path.Combine(_tempRoot, "target");
        Directory.CreateDirectory(sourceDir);
        BackupJob job = new() { Id = 1, Name = "Job1", SourcePath = sourceDir, TargetPath = targetDir, Type = BackupType.Differential };
        BackupConfiguration config = new() { Jobs = new[] { job }, LastFullBackupUtcByJobId = new Dictionary<int, DateTime> { [1] = DateTime.UtcNow.AddDays(-1) } };
        FakeConfigRepository configRepo = new(config);
        BackupExecutor executor = CreateExecutor(configRepository: configRepo);

        await executor.ExecuteAsync(new[] { 1 });

        Assert.False(configRepo.UpdateLastFullBackupCalled);
    }

    [Fact]
    public async Task ExecuteAsync_ThrowsOperationCanceledException_WhenCancelled()
    {
        string sourceDir = Path.Combine(_tempRoot, "source");
        string targetDir = Path.Combine(_tempRoot, "target");
        Directory.CreateDirectory(sourceDir);
        for (int i = 0; i < 50; i++)
            await File.WriteAllTextAsync(Path.Combine(sourceDir, $"file{i}.txt"), "x");

        BackupJob job = new() { Id = 1, Name = "Job1", SourcePath = sourceDir, TargetPath = targetDir, Type = BackupType.Full };
        BackupConfiguration config = new() { Jobs = new[] { job } };
        FakeConfigRepository configRepo = new(config);
        BackupExecutor executor = CreateExecutor(configRepository: configRepo);

        CancellationTokenSource cts = new();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => executor.ExecuteAsync(new[] { 1 }, cts.Token));
    }

    [Fact]
    public async Task ExecuteAsync_LogsEachCopiedFile()
    {
        string sourceDir = Path.Combine(_tempRoot, "source");
        string targetDir = Path.Combine(_tempRoot, "target");
        Directory.CreateDirectory(sourceDir);
        await File.WriteAllTextAsync(Path.Combine(sourceDir, "f1.txt"), "a");
        await File.WriteAllTextAsync(Path.Combine(sourceDir, "f2.txt"), "b");

        BackupJob job = new() { Id = 1, Name = "Job1", SourcePath = sourceDir, TargetPath = targetDir, Type = BackupType.Full };
        BackupConfiguration config = new() { Jobs = new[] { job } };
        FakeConfigRepository configRepo = new(config);
        FakeLogWriter logWriter = new();
        BackupExecutor executor = CreateExecutor(configRepository: configRepo, logWriter: logWriter);

        await executor.ExecuteAsync(new[] { 1 });

        Assert.Equal(2, logWriter.LogEntries.Count);
        Assert.Contains(logWriter.LogEntries, e => e.BackupName == "Job1" && e.SourcePath.Contains("f1.txt"));
        Assert.Contains(logWriter.LogEntries, e => e.BackupName == "Job1" && e.SourcePath.Contains("f2.txt"));
    }

    private BackupExecutor CreateExecutor(
        IConfigurationRepository? configRepository = null,
        IBackupStrategyFactory? strategyFactory = null,
        IFileSystemService? fileSystem = null,
        IStateWriter? stateWriter = null,
        ILogWriter? logWriter = null)
    {
        configRepository ??= new FakeConfigRepository(new BackupConfiguration { Jobs = Array.Empty<BackupJob>() });
        strategyFactory ??= new BackupStrategyFactory();
        fileSystem ??= new FileSystemService();
        stateWriter ??= new FakeStateWriter();
        logWriter ??= new FakeLogWriter();

        return new BackupExecutor(configRepository, strategyFactory, fileSystem, stateWriter, logWriter);
    }

    private sealed class FakeConfigRepository : IConfigurationRepository
    {
        private readonly BackupConfiguration? _config;
        public bool UpdateLastFullBackupCalled { get; private set; }
        public int LastUpdatedJobId { get; private set; }

        public FakeConfigRepository(BackupConfiguration? config) => _config = config;

        public Task<BackupConfiguration?> LoadAsync(CancellationToken cancellationToken) => Task.FromResult(_config);
        public Task SaveAsync(BackupConfiguration backupConfiguration, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task UpdateLastFullBackupAsync(int jobId, DateTime utc, CancellationToken cancellationToken)
        {
            UpdateLastFullBackupCalled = true;
            LastUpdatedJobId = jobId;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeStateWriter : IStateWriter
    {
        public List<IReadOnlyList<BackupProgress>> WrittenStates { get; } = new();

        public Task WriteStateAsync(IReadOnlyList<BackupProgress> progressList, CancellationToken cancellationToken = default)
        {
            WrittenStates.Add(progressList.ToList());
            return Task.CompletedTask;
        }
    }

    private sealed class FakeLogWriter : ILogWriter
    {
        public List<LogEntry> LogEntries { get; } = new();

        public Task WriteAsync(LogEntry logEntry, CancellationToken cancellationToken)
        {
            LogEntries.Add(logEntry);
            return Task.CompletedTask;
        }
    }

    private sealed class BackupStrategyFactory : IBackupStrategyFactory
    {
        public IBackupStrategy GetStrategy(BackupType type) =>
            type == BackupType.Differential ? (IBackupStrategy)new DifferentialBackupStrategy() : new FullBackupStrategy();
    }
}
