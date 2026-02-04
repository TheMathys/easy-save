using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using EasySave.Core.Entities;
using EasySave.Core.Enums;
using EasySave.Core.Models;
using EasySave.Infrastructure.Backup;

namespace EasySave.Infrastructure.Tests;

public sealed class FullBackupStrategyTests
{
    private readonly FullBackupStrategy _sut = new();

    [Fact]
    public async Task GetEligibleFilesAsync_ReturnsAllFiles_WhenInputContainsFiles()
    {
        var job = CreateJob();
        var files = new[]
        {
            new FileItem("a.txt", @"C:\Source\a.txt", new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc)),
            new FileItem("sub/b.txt", @"C:\Source\sub\b.txt", new DateTime(2024, 2, 1, 12, 0, 0, DateTimeKind.Utc))
        };

        var result = await CollectAsync(_sut.GetEligibleFilesAsync(job, ToAsyncEnumerable(files), null, CancellationToken.None));

        Assert.Equal(2, result.Count);
        Assert.Equal(files[0].RelativePath, result[0].RelativePath);
        Assert.Equal(files[1].RelativePath, result[1].RelativePath);
    }

    [Fact]
    public async Task GetEligibleFilesAsync_ReturnsEmpty_WhenInputIsEmpty()
    {
        var job = CreateJob();
        var files = Array.Empty<FileItem>();

        var result = await CollectAsync(_sut.GetEligibleFilesAsync(job, ToAsyncEnumerable(files), null, CancellationToken.None));

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetEligibleFilesAsync_IgnoresDifferentialSinceUtc_ReturnsAllFiles()
    {
        var job = CreateJob();
        var files = new[]
        {
            new FileItem("old.txt", @"C:\Source\old.txt", new DateTime(2023, 6, 1, 0, 0, 0, DateTimeKind.Utc)),
            new FileItem("new.txt", @"C:\Source\new.txt", new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc))
        };
        var differentialSince = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var result = await CollectAsync(_sut.GetEligibleFilesAsync(job, ToAsyncEnumerable(files), differentialSince, CancellationToken.None));

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetEligibleFilesAsync_RespectsCancellation()
    {
        var job = CreateJob();
        var cts = new CancellationTokenSource();
        var files = new[]
        {
            new FileItem("a.txt", @"C:\Source\a.txt", DateTime.UtcNow),
            new FileItem("b.txt", @"C:\Source\b.txt", DateTime.UtcNow)
        };

        var count = 0;
        try
        {
            await foreach (var _ in _sut.GetEligibleFilesAsync(job, ToAsyncEnumerableWithDelay(files, cts.Token), null, cts.Token))
            {
                count++;
                if (count == 1)
                    cts.Cancel();
            }
        }
        catch (OperationCanceledException)
        {
            // Annulation attendue
        }

        Assert.Equal(1, count);
    }

    private static BackupJob CreateJob() => new()
    {
        Id = 1,
        Name = "TestJob",
        SourcePath = @"C:\Source",
        TargetPath = @"D:\Target",
        Type = BackupType.Full
    };

    private static async IAsyncEnumerable<FileItem> ToAsyncEnumerable(
        IEnumerable<FileItem> items,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var item in items)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Yield();
            yield return item;
        }
    }

    private static async IAsyncEnumerable<FileItem> ToAsyncEnumerableWithDelay(
        IEnumerable<FileItem> items,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var item in items)
        {
            await Task.Delay(50, ct);
            yield return item;
        }
    }

    private static async Task<List<FileItem>> CollectAsync(IAsyncEnumerable<FileItem> source)
    {
        var list = new List<FileItem>();
        await foreach (var item in source)
            list.Add(item);
        return list;
    }
}
