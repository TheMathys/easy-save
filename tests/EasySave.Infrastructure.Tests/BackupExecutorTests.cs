using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        await Assert.ThrowsAsync<OperationCanceledException>(() => executor.ExecuteAsync(new[] { 1 }, null, cts.Token));
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

    [Fact]
    public async Task ExecuteAsync_WithEmptyJobIds_WritesOnlyInitialState()
    {
        string sourceDir = Path.Combine(_tempRoot, "source");
        string targetDir = Path.Combine(_tempRoot, "target");
        Directory.CreateDirectory(sourceDir);
        BackupJob job = new() { Id = 1, Name = "Job1", SourcePath = sourceDir, TargetPath = targetDir, Type = BackupType.Full };
        BackupConfiguration config = new() { Jobs = new[] { job } };
        FakeConfigRepository configRepo = new(config);
        FakeStateWriter stateWriter = new();
        BackupExecutor executor = CreateExecutor(configRepository: configRepo, stateWriter: stateWriter);

        await executor.ExecuteAsync(Array.Empty<int>());

        Assert.Single(stateWriter.WrittenStates);
        Assert.All(stateWriter.WrittenStates[0], p => Assert.Equal(BackupState.Inactive, p.State));
    }

    [Fact]
    public async Task ExecuteAsync_WithNonExistentSource_CompletesWithZeroFiles()
    {
        string sourceDir = Path.Combine(_tempRoot, "nonexistent");
        string targetDir = Path.Combine(_tempRoot, "target");
        BackupJob job = new() { Id = 1, Name = "Job1", SourcePath = sourceDir, TargetPath = targetDir, Type = BackupType.Full };
        BackupConfiguration config = new() { Jobs = new[] { job } };
        FakeConfigRepository configRepo = new(config);
        FakeStateWriter stateWriter = new();
        BackupExecutor executor = CreateExecutor(configRepository: configRepo, stateWriter: stateWriter);

        await executor.ExecuteAsync(new[] { 1 });

        IReadOnlyList<BackupProgress> lastState = stateWriter.WrittenStates[^1];
        BackupProgress completed = Assert.Single(lastState);
        Assert.Equal(BackupState.Completed, completed.State);
        Assert.Equal(0, completed.TotalFilesCount);
        Assert.Equal(100, completed.ProgressPercent);
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptySourceDirectory_CompletesWithZeroFiles()
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

        IReadOnlyList<BackupProgress> lastState = stateWriter.WrittenStates[^1];
        BackupProgress completed = Assert.Single(lastState);
        Assert.Equal(BackupState.Completed, completed.State);
        Assert.Equal(0, completed.TotalFilesCount);
        Assert.Equal(0, completed.RemainingFilesCount);
        Assert.Equal(0L, completed.RemainingSizeBytes);
    }

    [Fact]
    public async Task ExecuteAsync_PreservesSubdirectoryStructure()
    {
        string sourceDir = Path.Combine(_tempRoot, "source");
        string targetDir = Path.Combine(_tempRoot, "target");
        string subDir = Path.Combine(sourceDir, "subdir");
        Directory.CreateDirectory(subDir);
        string fileInSub = Path.Combine(subDir, "nested.txt");
        await File.WriteAllTextAsync(fileInSub, "nested content");

        BackupJob job = new() { Id = 1, Name = "Job1", SourcePath = sourceDir, TargetPath = targetDir, Type = BackupType.Full };
        BackupConfiguration config = new() { Jobs = new[] { job } };
        FakeConfigRepository configRepo = new(config);
        FileSystemService fileSystem = new();
        IBackupStrategyFactory strategyFactory = new BackupStrategyFactory();
        BackupExecutor executor = CreateExecutor(configRepository: configRepo, fileSystem: fileSystem, strategyFactory: strategyFactory);

        await executor.ExecuteAsync(new[] { 1 });

        string expectedDest = Path.Combine(targetDir, "subdir", "nested.txt");
        Assert.True(File.Exists(expectedDest));
        Assert.Equal("nested content", await File.ReadAllTextAsync(expectedDest));
    }

    [Fact]
    public async Task ExecuteAsync_WritesProgressStatesDuringCopy()
    {
        string sourceDir = Path.Combine(_tempRoot, "source");
        string targetDir = Path.Combine(_tempRoot, "target");
        Directory.CreateDirectory(sourceDir);
        await File.WriteAllTextAsync(Path.Combine(sourceDir, "a.txt"), "a");
        await File.WriteAllTextAsync(Path.Combine(sourceDir, "b.txt"), "bb");
        await File.WriteAllTextAsync(Path.Combine(sourceDir, "c.txt"), "ccc");

        BackupJob job = new() { Id = 1, Name = "Job1", SourcePath = sourceDir, TargetPath = targetDir, Type = BackupType.Full };
        BackupConfiguration config = new() { Jobs = new[] { job } };
        FakeConfigRepository configRepo = new(config);
        FakeStateWriter stateWriter = new();
        BackupExecutor executor = CreateExecutor(configRepository: configRepo, stateWriter: stateWriter);

        await executor.ExecuteAsync(new[] { 1 });

        IReadOnlyList<BackupProgress> activeStates = stateWriter.WrittenStates
            .SelectMany(s => s)
            .Where(p => p.State == BackupState.Active)
            .ToList();
        Assert.True(activeStates.Count >= 3);

        BackupProgress? withCurrentPath = activeStates.FirstOrDefault(p => p.CurrentSourcePath != null);
        Assert.NotNull(withCurrentPath);
        Assert.NotNull(withCurrentPath.CurrentDestinationPath);
        Assert.True(withCurrentPath.ProgressPercent >= 0 && withCurrentPath.ProgressPercent <= 100);
        Assert.True(withCurrentPath.RemainingFilesCount >= 0);
    }

    [Fact]
    public async Task ExecuteAsync_ExecutesMultipleJobsInSequence()
    {
        string source1 = Path.Combine(_tempRoot, "source1");
        string target1 = Path.Combine(_tempRoot, "target1");
        string source2 = Path.Combine(_tempRoot, "source2");
        string target2 = Path.Combine(_tempRoot, "target2");
        Directory.CreateDirectory(source1);
        Directory.CreateDirectory(source2);
        await File.WriteAllTextAsync(Path.Combine(source1, "f1.txt"), "job1");
        await File.WriteAllTextAsync(Path.Combine(source2, "f2.txt"), "job2");

        BackupJob job1 = new() { Id = 1, Name = "Job1", SourcePath = source1, TargetPath = target1, Type = BackupType.Full };
        BackupJob job2 = new() { Id = 2, Name = "Job2", SourcePath = source2, TargetPath = target2, Type = BackupType.Full };
        BackupConfiguration config = new() { Jobs = new[] { job1, job2 } };
        FakeConfigRepository configRepo = new(config);
        FakeStateWriter stateWriter = new();
        BackupExecutor executor = CreateExecutor(configRepository: configRepo, stateWriter: stateWriter);

        await executor.ExecuteAsync(new[] { 1, 2 });

        Assert.True(File.Exists(Path.Combine(target1, "f1.txt")));
        Assert.True(File.Exists(Path.Combine(target2, "f2.txt")));
        Assert.Equal("job1", await File.ReadAllTextAsync(Path.Combine(target1, "f1.txt")));
        Assert.Equal("job2", await File.ReadAllTextAsync(Path.Combine(target2, "f2.txt")));

        IReadOnlyList<BackupProgress> lastState = stateWriter.WrittenStates[^1];
        Assert.Equal(2, lastState.Count);
        Assert.All(lastState, p => Assert.Equal(BackupState.Completed, p.State));
    }

    [Fact]
    public async Task ExecuteAsync_DifferentialBackup_CopiesFiles()
    {
        string sourceDir = Path.Combine(_tempRoot, "source");
        string targetDir = Path.Combine(_tempRoot, "target");
        Directory.CreateDirectory(sourceDir);
        string recentFile = Path.Combine(sourceDir, "recent.txt");
        await File.WriteAllTextAsync(recentFile, "modified recently");
        File.SetLastWriteTimeUtc(recentFile, DateTime.UtcNow);

        BackupJob job = new() { Id = 1, Name = "Job1", SourcePath = sourceDir, TargetPath = targetDir, Type = BackupType.Differential };
        BackupConfiguration config = new() { Jobs = new[] { job }, LastFullBackupUtcByJobId = new Dictionary<int, DateTime> { [1] = DateTime.UtcNow.AddDays(-1) } };
        FakeConfigRepository configRepo = new(config);
        FileSystemService fileSystem = new();
        IBackupStrategyFactory strategyFactory = new BackupStrategyFactory();
        BackupExecutor executor = CreateExecutor(configRepository: configRepo, fileSystem: fileSystem, strategyFactory: strategyFactory);

        await executor.ExecuteAsync(new[] { 1 });

        Assert.True(File.Exists(Path.Combine(targetDir, "recent.txt")));
        Assert.Equal("modified recently", await File.ReadAllTextAsync(Path.Combine(targetDir, "recent.txt")));
    }

    [Fact]
    public async Task ExecuteAsync_LogEntryContainsCorrectData()
    {
        string sourceDir = Path.Combine(_tempRoot, "source");
        string targetDir = Path.Combine(_tempRoot, "target");
        Directory.CreateDirectory(sourceDir);
        string filePath = Path.Combine(sourceDir, "test.txt");
        string content = "hello world";
        await File.WriteAllTextAsync(filePath, content);

        BackupJob job = new() { Id = 1, Name = "Job1", SourcePath = sourceDir, TargetPath = targetDir, Type = BackupType.Full };
        BackupConfiguration config = new() { Jobs = new[] { job } };
        FakeConfigRepository configRepo = new(config);
        FakeLogWriter logWriter = new();
        BackupExecutor executor = CreateExecutor(configRepository: configRepo, logWriter: logWriter);

        await executor.ExecuteAsync(new[] { 1 });

        LogEntry entry = Assert.Single(logWriter.LogEntries);
        Assert.Equal("Job1", entry.BackupName);
        Assert.Equal(content.Length, entry.FileSizeBytes);
        Assert.Contains("test.txt", entry.SourcePath);
        Assert.Contains("test.txt", entry.DestinationPath);
        Assert.True(entry.TrasnferTimeMs >= TimeSpan.Zero);
    }

    [Fact]
    public async Task ExecuteAsync_TotalSizeBytesAndRemainingSizeBytes_AreCorrect()
    {
        string sourceDir = Path.Combine(_tempRoot, "source");
        string targetDir = Path.Combine(_tempRoot, "target");
        Directory.CreateDirectory(sourceDir);
        await File.WriteAllTextAsync(Path.Combine(sourceDir, "small.txt"), "x");
        await File.WriteAllTextAsync(Path.Combine(sourceDir, "big.txt"), new string('x', 1000));

        BackupJob job = new() { Id = 1, Name = "Job1", SourcePath = sourceDir, TargetPath = targetDir, Type = BackupType.Full };
        BackupConfiguration config = new() { Jobs = new[] { job } };
        FakeConfigRepository configRepo = new(config);
        FakeStateWriter stateWriter = new();
        BackupExecutor executor = CreateExecutor(configRepository: configRepo, stateWriter: stateWriter);

        await executor.ExecuteAsync(new[] { 1 });

        BackupProgress completed = stateWriter.WrittenStates[^1].First(p => p.BackupName == "Job1");
        Assert.Equal(2, completed.TotalFilesCount);
        Assert.Equal(1001L, completed.TotalSizeBytes);
        Assert.Equal(0, completed.RemainingFilesCount);
        Assert.Equal(0L, completed.RemainingSizeBytes);
    }

    [Fact]
    public async Task ExecuteAsync_UpdateLastFullBackup_CalledForEachFullBackupJob()
    {
        string source1 = Path.Combine(_tempRoot, "source1");
        string target1 = Path.Combine(_tempRoot, "target1");
        string source2 = Path.Combine(_tempRoot, "source2");
        string target2 = Path.Combine(_tempRoot, "target2");
        Directory.CreateDirectory(source1);
        Directory.CreateDirectory(source2);

        BackupJob job1 = new() { Id = 1, Name = "Job1", SourcePath = source1, TargetPath = target1, Type = BackupType.Full };
        BackupJob job2 = new() { Id = 2, Name = "Job2", SourcePath = source2, TargetPath = target2, Type = BackupType.Full };
        BackupConfiguration config = new() { Jobs = new[] { job1, job2 } };
        FakeConfigRepository configRepo = new(config);
        BackupExecutor executor = CreateExecutor(configRepository: configRepo);

        await executor.ExecuteAsync(new[] { 1, 2 });

        Assert.True(configRepo.UpdateLastFullBackupCalled);
        Assert.Equal(2, configRepo.LastUpdatedJobId);
    }

    [Fact]
    public async Task ExecuteAsync_MixedJobIds_ExecutesOnlyRequestedJobs()
    {
        string source1 = Path.Combine(_tempRoot, "source1");
        string target1 = Path.Combine(_tempRoot, "target1");
        string source2 = Path.Combine(_tempRoot, "source2");
        string target2 = Path.Combine(_tempRoot, "target2");
        Directory.CreateDirectory(source1);
        Directory.CreateDirectory(source2);
        await File.WriteAllTextAsync(Path.Combine(source1, "f1.txt"), "1");
        await File.WriteAllTextAsync(Path.Combine(source2, "f2.txt"), "2");

        BackupJob job1 = new() { Id = 1, Name = "Job1", SourcePath = source1, TargetPath = target1, Type = BackupType.Full };
        BackupJob job2 = new() { Id = 2, Name = "Job2", SourcePath = source2, TargetPath = target2, Type = BackupType.Full };
        BackupConfiguration config = new() { Jobs = new[] { job1, job2 } };
        FakeConfigRepository configRepo = new(config);
        BackupExecutor executor = CreateExecutor(configRepository: configRepo);

        await executor.ExecuteAsync(new[] { 2 });

        Assert.False(File.Exists(Path.Combine(target1, "f1.txt")));
        Assert.True(File.Exists(Path.Combine(target2, "f2.txt")));
    }

    [Fact]
    public async Task ExecuteAsync_StateSequence_InactiveThenActiveThenCompleted()
    {
        string sourceDir = Path.Combine(_tempRoot, "source");
        string targetDir = Path.Combine(_tempRoot, "target");
        Directory.CreateDirectory(sourceDir);
        await File.WriteAllTextAsync(Path.Combine(sourceDir, "f.txt"), "x");

        BackupJob job = new() { Id = 1, Name = "Job1", SourcePath = sourceDir, TargetPath = targetDir, Type = BackupType.Full };
        BackupConfiguration config = new() { Jobs = new[] { job } };
        FakeConfigRepository configRepo = new(config);
        FakeStateWriter stateWriter = new();
        BackupExecutor executor = CreateExecutor(configRepository: configRepo, stateWriter: stateWriter);

        await executor.ExecuteAsync(new[] { 1 });

        BackupProgress initial = stateWriter.WrittenStates[0].First(p => p.BackupName == "Job1");
        Assert.Equal(BackupState.Inactive, initial.State);

        BackupProgress active = stateWriter.WrittenStates.First(s => s.Any(p => p.BackupName == "Job1" && p.State == BackupState.Active))
            .First(p => p.BackupName == "Job1");
        Assert.Equal(BackupState.Active, active.State);

        BackupProgress completed = stateWriter.WrittenStates[^1].First(p => p.BackupName == "Job1");
        Assert.Equal(BackupState.Completed, completed.State);
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

        public Task WriteAsync<T>(T logEntry, CancellationToken cancellationToken)
        {
            if (logEntry is LogEntry entry)
            {
                LogEntries.Add(entry);
            }
            return Task.CompletedTask;
        }
    }

    private sealed class BackupStrategyFactory : IBackupStrategyFactory
    {
        public IBackupStrategy GetStrategy(BackupType type) =>
            type == BackupType.Differential ? (IBackupStrategy)new DifferentialBackupStrategy() : new FullBackupStrategy();
    }
}
