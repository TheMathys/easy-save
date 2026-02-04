using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasySave.Core.Interfaces;
using EasySave.Core.Models;
using EasySave.Infrastructure.FileSystem;

namespace EasySave.Infrastructure.Tests;

public sealed class FileSystemServiceTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly FileSystemService _service = new();

    public FileSystemServiceTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "EasySave.FileSystem.Tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempRoot);
    }

    [Fact]
    public async Task CopyFileAsync_CreatesDestinationDirectory_And_Copies_Content()
    {
        var sourcePath = Path.Combine(_tempRoot, "src", "file.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
        var sourceContent = "hello world";
        await File.WriteAllTextAsync(sourcePath, sourceContent);

        var destinationPath = Path.Combine(_tempRoot, "dest", "nested", "file.txt");

        var duration = await ((IFileSystemService)_service).CopyFileAsync(sourcePath, destinationPath, CancellationToken.None);

        Assert.True(duration >= 0, "Successful copy should return a non-negative duration.");
        Assert.True(File.Exists(destinationPath), "Destination file should exist after copy.");
        var copiedContent = await File.ReadAllTextAsync(destinationPath);
        Assert.Equal(sourceContent, copiedContent);
    }

    [Fact]
    public async Task CopyFileAsync_ReturnsNegativeDuration_When_CopyFails()
    {
        var missingSource = Path.Combine(_tempRoot, "unknown", "missing.txt");
        var destinationPath = Path.Combine(_tempRoot, "dest", "file.txt");

        var duration = await ((IFileSystemService)_service).CopyFileAsync(missingSource, destinationPath, CancellationToken.None);

        Assert.True(duration <= 0, "Failure should yield a non-positive duration.");
        Assert.False(File.Exists(destinationPath), "Destination file should not exist when copy fails.");
    }

    [Fact]
    public async Task EnumerateFilesAsync_ReturnsEmpty_WhenDirectoryDoesNotExist()
    {
        var nonExistentPath = Path.Combine(_tempRoot, "nonexistent");

        var result = await CollectAsync(_service.EnumerateFilesAsync(nonExistentPath, CancellationToken.None));

        Assert.Empty(result);
    }

    [Fact]
    public async Task EnumerateFilesAsync_ReturnsAllFilesRecursively()
    {
        var sourceDir = Path.Combine(_tempRoot, "source");
        var subDir = Path.Combine(sourceDir, "sub");
        Directory.CreateDirectory(subDir);

        var file1 = Path.Combine(sourceDir, "a.txt");
        var file2 = Path.Combine(subDir, "b.txt");
        await File.WriteAllTextAsync(file1, "content1");
        await File.WriteAllTextAsync(file2, "content2");

        var result = await CollectAsync(_service.EnumerateFilesAsync(sourceDir, CancellationToken.None));

        Assert.Equal(2, result.Count);
        var paths = result.Select(f => f.RelativePath.Replace('\\', '/')).OrderBy(p => p).ToList();
        Assert.Contains("a.txt", paths);
        Assert.Contains("sub/b.txt", paths);
    }

    [Fact]
    public async Task EnumerateFilesAsync_ReturnsCorrectFileItemProperties()
    {
        var sourceDir = Path.Combine(_tempRoot, "source");
        Directory.CreateDirectory(sourceDir);
        var filePath = Path.Combine(sourceDir, "test.txt");
        var writeTime = new DateTime(2024, 6, 15, 10, 30, 0, DateTimeKind.Utc);
        await File.WriteAllTextAsync(filePath, "test");
        File.SetLastWriteTimeUtc(filePath, writeTime);

        var result = await CollectAsync(_service.EnumerateFilesAsync(sourceDir, CancellationToken.None));

        var item = Assert.Single(result);
        Assert.Equal("test.txt", item.RelativePath);
        Assert.Equal(filePath, item.FullSourcePath);
        Assert.Equal(writeTime, item.LastWriteTimeUtc);
    }

    [Fact]
    public async Task EnumerateFilesAsync_RespectsCancellation()
    {
        var sourceDir = Path.Combine(_tempRoot, "source");
        Directory.CreateDirectory(sourceDir);
        for (var i = 0; i < 20; i++)
            await File.WriteAllTextAsync(Path.Combine(sourceDir, $"file{i}.txt"), "x");

        var cts = new CancellationTokenSource();
        var count = 0;
        try
        {
            await foreach (var _ in _service.EnumerateFilesAsync(sourceDir, cts.Token))
            {
                count++;
                if (count == 3)
                    cts.Cancel();
            }
        }
        catch (OperationCanceledException) { }

        Assert.Equal(3, count);
    }

    private static async Task<List<FileItem>> CollectAsync(IAsyncEnumerable<FileItem> source)
    {
        var list = new List<FileItem>();
        await foreach (var item in source)
            list.Add(item);
        return list;
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempRoot))
            {
                Directory.Delete(_tempRoot, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup failures.
        }
    }
}
