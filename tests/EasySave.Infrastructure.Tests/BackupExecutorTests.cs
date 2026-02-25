using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using EasySave.Core.Entities;
using EasySave.Core.Enums;
using EasySave.Core.Interfaces;
using EasySave.Core.Models;
using EasyLog;
using EasySave.Infrastructure.Backup;
using EasySave.Infrastructure.FileSystem;

namespace EasySave.Infrastructure.Tests;

    public sealed class BackupExecutorTests : IDisposable
    {
        private readonly string _tempRoot;
        private const int LargeThresholdKb = 1;

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

        BusinessSoftwareDetector businessDetector = new BusinessSoftwareDetector();
        BackupExecutor executor = new(configRepo, strategyFactory, fileSystem, stateWriter, logWriter, null, businessDetector);

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

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => executor.ExecuteAsync(new[] { 1 }, null, cts.Token));
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
    public async Task ExecuteAsync_ExecutesMultipleJobsInParallel_WhenMultipleSelected()
    {
        string source1 = Path.Combine(_tempRoot, "source1");
        string target1 = Path.Combine(_tempRoot, "target1");
        string source2 = Path.Combine(_tempRoot, "source2");
        string target2 = Path.Combine(_tempRoot, "target2");
        Directory.CreateDirectory(source1);
        Directory.CreateDirectory(source2);
        for (int i = 0; i < 15; i++)
        {
            await File.WriteAllTextAsync(Path.Combine(source1, $"f1_{i}.txt"), "job1");
            await File.WriteAllTextAsync(Path.Combine(source2, $"f2_{i}.txt"), "job2");
        }

        BackupJob job1 = new() { Id = 1, Name = "Job1", SourcePath = source1, TargetPath = target1, Type = BackupType.Full };
        BackupJob job2 = new() { Id = 2, Name = "Job2", SourcePath = source2, TargetPath = target2, Type = BackupType.Full };
        BackupConfiguration config = new() { Jobs = new[] { job1, job2 } };
        FakeConfigRepository configRepo = new(config);
        FakeStateWriter stateWriter = new();
        BackupExecutor executor = CreateExecutor(configRepository: configRepo, stateWriter: stateWriter);

        await executor.ExecuteAsync(new[] { 1, 2 });

        Assert.True(File.Exists(Path.Combine(target1, "f1_0.txt")));
        Assert.True(File.Exists(Path.Combine(target2, "f2_0.txt")));
        IReadOnlyList<IReadOnlyList<BackupProgress>> states = stateWriter.WrittenStates;
        Assert.True(states.Count >= 2, "Expected at least 2 state snapshots (initial + updates).");

        // Prove parallelism: at least one snapshot had both jobs Active at the same time.
        bool hadBothActive = states.Any(snapshot =>
            snapshot.Count(p => p.State == BackupState.Active) >= 2);
        Assert.True(hadBothActive, "Expected at least one state snapshot with two jobs Active (parallel execution).");

        // With the final state write after Task.WhenAll, we can now verify both jobs completed.
        // Poll for a snapshot with both Completed to tolerate CI thread scheduling.
        IReadOnlyList<BackupProgress>? finalSnapshot = null;
        for (int i = 0; i < 6; i++)
        {
            await Task.Delay(50);
            states = stateWriter.WrittenStates;
            finalSnapshot = states.FirstOrDefault(s => s.Count == 2 && s.All(p => p.State == BackupState.Completed));
            if (finalSnapshot != null)
                break;
        }
        Assert.True(finalSnapshot != null,
            "Expected at least one state snapshot with both jobs Completed. " +
            "Last snapshot: " + string.Join("; ", states[^1].Select(p => $"{p.BackupName}={p.State}")) + ".");
    }

    [Fact]
    public async Task ExecuteAsync_MultipleJobs_BothCompleteAndFilesCopied()
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

        // With parallel execution, the last snapshot may be from the first job that completed (other still Active).
        // Poll for a snapshot with both Completed so we tolerate CI thread scheduling (same as ExecuteAsync_ExecutesMultipleJobsInParallel_WhenMultipleSelected).
        IReadOnlyList<IReadOnlyList<BackupProgress>> states = stateWriter.WrittenStates;
        IReadOnlyList<BackupProgress>? finalSnapshot = null;
        for (int i = 0; i < 6; i++)
        {
            await Task.Delay(50);
            states = stateWriter.WrittenStates;
            Assert.True(states.Count >= 1);
            Assert.Equal(2, states[^1].Count);
            finalSnapshot = states.FirstOrDefault(s => s.Count == 2 && s.All(p => p.State == BackupState.Completed));
            if (finalSnapshot != null)
                break;
        }
        Assert.True(finalSnapshot != null,
            "Expected at least one state snapshot with both jobs Completed. " +
            "Last snapshot: " + string.Join("; ", states[^1].Select(p => $"{p.BackupName}={p.State}")) + ".");
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
        Assert.True(entry.TransferTimeMs >= TimeSpan.Zero);
        Assert.Equal(0L, entry.EncryptionTimeMs); // pas de cryptage = 0
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
        Assert.Equal(2, configRepo.LastUpdatedJobIds.Count);
        Assert.Contains(1, configRepo.LastUpdatedJobIds);
        Assert.Contains(2, configRepo.LastUpdatedJobIds);
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

    [Fact]
    public async Task ExecuteAsync_NeverTransfersTwoLargeFilesInParallel_WhenThresholdConfigured()
    {
        string source1 = Path.Combine(_tempRoot, "source1");
        string target1 = Path.Combine(_tempRoot, "target1");
        string source2 = Path.Combine(_tempRoot, "source2");
        string target2 = Path.Combine(_tempRoot, "target2");

        long smallFileSize = 512;
        long largeFileSize = 4096;

        InMemoryFileSystemService fileSystem = new InMemoryFileSystemService();
        fileSystem.AddFile(source1, "small1.txt", smallFileSize);
        fileSystem.AddFile(source1, "large1.bin", largeFileSize);
        fileSystem.AddFile(source2, "small2.txt", smallFileSize);
        fileSystem.AddFile(source2, "large2.bin", largeFileSize);

        BackupJob job1 = new() { Id = 1, Name = "Job1", SourcePath = source1, TargetPath = target1, Type = BackupType.Full };
        BackupJob job2 = new() { Id = 2, Name = "Job2", SourcePath = source2, TargetPath = target2, Type = BackupType.Full };

        BackupConfiguration config = new()
        {
            Jobs = new[] { job1, job2 },
            LargeFileThresholdKb = LargeThresholdKb
        };

        FakeConfigRepository configRepo = new(config);
        FakeStateWriter stateWriter = new();
        FakeLogWriter logWriter = new();
        BackupExecutor executor = new(configRepo, new BackupStrategyFactory(), fileSystem, stateWriter, logWriter, null, new BusinessSoftwareDetector());

        await executor.ExecuteAsync(new[] { 1, 2 });

        Assert.Equal(1, fileSystem.MaxConcurrentLargeCopies);
    }

    [Fact]
    public async Task ExecuteAsync_ReusesComputedUncPaths_ForProgressAndLog()
    {
        string source = Path.Combine(_tempRoot, "source");
        string target = Path.Combine(_tempRoot, "target");
        string sourceFile = Path.Combine(source, "a.txt");
        string destinationFile = Path.Combine(target, "a.txt");

        CountingUncFileSystemService fileSystem = new(sourceFile, destinationFile, fileSize: 1024);
        BackupJob job = new() { Id = 1, Name = "Job1", SourcePath = source, TargetPath = target, Type = BackupType.Full };
        BackupConfiguration config = new() { Jobs = new[] { job } };

        FakeConfigRepository configRepo = new(config);
        FakeStateWriter stateWriter = new();
        FakeLogWriter logWriter = new();
        BackupExecutor executor = new(configRepo, new BackupStrategyFactory(), fileSystem, stateWriter, logWriter, null, new BusinessSoftwareDetector());

        await executor.ExecuteAsync(new[] { 1 });

        Assert.Equal(1, fileSystem.GetUncPathCallsByPath[sourceFile]);
        Assert.Equal(1, fileSystem.GetUncPathCallsByPath[destinationFile]);
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

        BusinessSoftwareDetector businessDetector = new BusinessSoftwareDetector();
        return new BackupExecutor(configRepository, strategyFactory, fileSystem, stateWriter, logWriter, null, businessDetector);
    }

    private sealed class FakeConfigRepository : IConfigurationRepository
    {
        private readonly BackupConfiguration? _config;
        public bool UpdateLastFullBackupCalled { get; private set; }
        public int LastUpdatedJobId { get; private set; }
        /// <summary>All job ids for which UpdateLastFullBackupAsync was called (order may vary in parallel).</summary>
        public IReadOnlyList<int> LastUpdatedJobIds => _lastUpdatedJobIds;
        private readonly List<int> _lastUpdatedJobIds = new();

        public FakeConfigRepository(BackupConfiguration? config) => _config = config;

        public Task<BackupConfiguration?> LoadAsync(CancellationToken cancellationToken) => Task.FromResult(_config);
        public Task SaveAsync(BackupConfiguration backupConfiguration, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task UpdateLastFullBackupAsync(int jobId, DateTime utc, CancellationToken cancellationToken)
        {
            UpdateLastFullBackupCalled = true;
            LastUpdatedJobId = jobId;
            _lastUpdatedJobIds.Add(jobId);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeStateWriter : IStateWriter
    {
        private readonly List<IReadOnlyList<BackupProgress>> _writtenStates = new();
        private readonly object _lock = new();

        /// <summary>Returns a snapshot of all written states under lock so tests see a consistent view after parallel execution.</summary>
        public IReadOnlyList<IReadOnlyList<BackupProgress>> WrittenStates
        {
            get { lock (_lock) { return _writtenStates.ToList(); } }
        }

        public Task WriteStateAsync(IReadOnlyList<BackupProgress> progressList, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                _writtenStates.Add(progressList.ToList());
            }
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

    private sealed class InMemoryFileSystemService : IFileSystemService
    {
        private readonly Dictionary<string, List<FileItem>> _filesBySource = new();
        private readonly Dictionary<string, long> _sizesByFullPath = new(StringComparer.OrdinalIgnoreCase);
        private int _currentLargeCopies;

        public int MaxConcurrentLargeCopies { get; private set; }

        public void AddFile(string sourceRoot, string relativePath, long sizeBytes)
        {
            string fullPath = Path.Combine(sourceRoot, relativePath);
            if (!_filesBySource.TryGetValue(sourceRoot, out List<FileItem>? list))
            {
                list = new List<FileItem>();
                _filesBySource[sourceRoot] = list;
            }

            list.Add(new FileItem(relativePath, fullPath, DateTime.UtcNow));
            _sizesByFullPath[fullPath] = sizeBytes;
        }

        public IAsyncEnumerable<FileItem> EnumerateFilesAsync(string sourcePath, BackupEnumerationOptions? options = null, CancellationToken cancellationToken = default)
        {
            if (!_filesBySource.TryGetValue(sourcePath, out List<FileItem>? list))
                list = new List<FileItem>();

            return EnumerateAsync(list, cancellationToken);
        }

        private static async IAsyncEnumerable<FileItem> EnumerateAsync(IEnumerable<FileItem> items, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            foreach (FileItem item in items)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return item;
                await Task.Yield();
            }
        }

        public async Task<long> CopyFileAsync(string sourcePath, string destinationPath, IProgress<long>? progress = null, CancellationToken cancellationToken = default)
        {
            long size = GetFileSize(sourcePath);
            bool isLarge = size > LargeThresholdKb * 1024L;

            if (isLarge)
            {
                int current = Interlocked.Increment(ref _currentLargeCopies);
                int observed = current;
                if (observed > MaxConcurrentLargeCopies)
                {
                    MaxConcurrentLargeCopies = observed;
                }
            }

            try
            {
                progress?.Report(size);
                await Task.Delay(50, cancellationToken);
            }
            finally
            {
                if (isLarge)
                {
                    Interlocked.Decrement(ref _currentLargeCopies);
                }
            }

            return 50;
        }

        public string GetUncPath(string path) => path;

        public long GetFileSize(string path)
        {
            return _sizesByFullPath.TryGetValue(path, out long size) ? size : 0;
        }

        public void EnsureDirectoryExists(string directoryPath)
        {
        }

        public DateTime GetLastWriteTimeUtc(string path) => DateTime.UtcNow;
    }

    private sealed class CountingUncFileSystemService : IFileSystemService
    {
        private readonly string _sourceFile;
        private readonly long _fileSize;

        public CountingUncFileSystemService(string sourceFile, string destinationFile, long fileSize)
        {
            _sourceFile = sourceFile;
            _fileSize = fileSize;
        }

        public Dictionary<string, int> GetUncPathCallsByPath { get; } = new(StringComparer.OrdinalIgnoreCase);

        public IAsyncEnumerable<FileItem> EnumerateFilesAsync(string sourcePath, BackupEnumerationOptions? options = null, CancellationToken cancellationToken = default)
        {
            return EnumerateAsync(cancellationToken);
        }

        private async IAsyncEnumerable<FileItem> EnumerateAsync([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return new FileItem(Path.GetFileName(_sourceFile), _sourceFile, DateTime.UtcNow);
            await Task.CompletedTask;
        }

        public Task<long> CopyFileAsync(string sourcePath, string destinationPath, IProgress<long>? progress = null, CancellationToken cancellationToken = default)
        {
            progress?.Report(_fileSize);
            return Task.FromResult(1L);
        }

        public string GetUncPath(string path)
        {
            if (GetUncPathCallsByPath.TryGetValue(path, out int current))
                GetUncPathCallsByPath[path] = current + 1;
            else
                GetUncPathCallsByPath[path] = 1;

            return path;
        }

        public long GetFileSize(string path) => _fileSize;

        public void EnsureDirectoryExists(string directoryPath)
        {
        }

        public DateTime GetLastWriteTimeUtc(string path) => DateTime.UtcNow;
    }
}
