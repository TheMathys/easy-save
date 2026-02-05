using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasySave.Core.Interfaces;
using EasySave.Core.Models;
using EasySave.Infrastructure.FileSystem;
using Xunit;

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
    public void EnsureDirectoryExists_CreatesDirectory_WhenDoesNotExist()
    {
        var dirPath = Path.Combine(_tempRoot, "newdir", "nested");

        _service.EnsureDirectoryExists(dirPath);

        Assert.True(Directory.Exists(dirPath));
    }

    [Fact]
    public void EnsureDirectoryExists_DoesNothing_WhenDirectoryExists()
    {
        var existingDir = Path.Combine(_tempRoot, "existing");
        Directory.CreateDirectory(existingDir);

        _service.EnsureDirectoryExists(existingDir);

        Assert.True(Directory.Exists(existingDir));  // Toujours vrai, pas d'exception
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(42L)]
    [InlineData(1024 * 1024L)]  // 1MB
    public void GetFileSize_ReturnsCorrectSize(long expectedSize)
    {
        var filePath = Path.Combine(_tempRoot, "sizefile.txt");
        File.WriteAllBytes(filePath, new byte[expectedSize]);

        var actualSize = _service.GetFileSize(filePath);

        Assert.Equal(expectedSize, actualSize);
    }

    [Fact]
    public void GetFileSize_Throws_OnNonExistentFile()
    {
        var nonExistent = Path.Combine(_tempRoot, "missing.txt");

        Assert.Throws<FileNotFoundException>(() => _service.GetFileSize(nonExistent));
    }

    [Fact]
    public void GetLastWriteTimeUtc_ReturnsCorrectUtcTime()
    {
        var filePath = Path.Combine(_tempRoot, "timefile.txt");
        File.WriteAllText(filePath, "test");
        var expectedUtc = new DateTime(2024, 6, 15, 10, 30, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(filePath, expectedUtc);

        var actual = _service.GetLastWriteTimeUtc(filePath);

        Assert.Equal(expectedUtc, actual);
    }

    [Fact]
    public void GetUncPath_ReturnsFullCanonicalPath()
    {
        var relativePath = Path.Combine(_tempRoot, "rel", "file.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(relativePath)!);
        File.Create(relativePath).Dispose();

        var uncPath = _service.GetUncPath(relativePath);

        Assert.Equal(Path.GetFullPath(relativePath), uncPath);
        Assert.True(Path.IsPathFullyQualified(uncPath));
    }

    [Fact]
    public async Task CopyFileAsync_CreatesDestinationDirectory_And_Copies_Content()
    {
        var sourcePath = Path.Combine(_tempRoot, "src", "file.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
        var sourceContent = "hello world";
        await File.WriteAllTextAsync(sourcePath, sourceContent);

        var destinationPath = Path.Combine(_tempRoot, "dest", "nested", "file.txt");

        var duration = await ((IFileSystemService)_service).CopyFileAsync(sourcePath, destinationPath, null, CancellationToken.None);

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

        var duration = await ((IFileSystemService)_service).CopyFileAsync(missingSource, destinationPath, null, CancellationToken.None);

        Assert.True(duration <= 0, "Failure should yield a non-positive duration.");
        Assert.False(File.Exists(destinationPath), "Destination file should not exist when copy fails.");
    }

    [Fact]
    public async Task EnumerateFilesAsync_ReturnsEmpty_WhenDirectoryDoesNotExist()
    {
        var nonExistentPath = Path.Combine(_tempRoot, "nonexistent");

        var result = await CollectAsync(_service.EnumerateFilesAsync(nonExistentPath, null, CancellationToken.None));

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

        var result = await CollectAsync(_service.EnumerateFilesAsync(sourceDir, null, CancellationToken.None));

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

        var result = await CollectAsync(_service.EnumerateFilesAsync(sourceDir, null, CancellationToken.None));

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
            await foreach (var _ in _service.EnumerateFilesAsync(sourceDir, null, cts.Token))
            {
                count++;
                if (count == 3)
                    cts.Cancel();
            }
        }
        catch (OperationCanceledException) { }

        Assert.Equal(3, count);
    }

    [Fact]
    public async Task EnumerateFilesAsync_ExcludesFilesByExtension_WhenOptionsProvided()
    {
        var sourceDir = Path.Combine(_tempRoot, "source");
        Directory.CreateDirectory(sourceDir);
        await File.WriteAllTextAsync(Path.Combine(sourceDir, "a.txt"), "x");
        await File.WriteAllTextAsync(Path.Combine(sourceDir, "b.tmp"), "x");
        await File.WriteAllTextAsync(Path.Combine(sourceDir, "c.log"), "x");
        await File.WriteAllTextAsync(Path.Combine(sourceDir, "d.txt"), "x");

        var options = new BackupEnumerationOptions
        {
            ExcludeExtensions = new[] { ".tmp", ".log" }
        };
        var result = await CollectAsync(_service.EnumerateFilesAsync(sourceDir, options, CancellationToken.None));

        Assert.Equal(2, result.Count);
        var names = result.Select(f => f.RelativePath.Replace("\\", "/")).OrderBy(p => p).ToList();
        Assert.Contains("a.txt", names);
        Assert.Contains("d.txt", names);
    }

    [Fact]
    public async Task EnumerateFilesAsync_DoesNotTraverseExcludedDirectoryNames_WhenOptionsProvided()
    {
        var sourceDir = Path.Combine(_tempRoot, "source");
        var includedDir = Path.Combine(sourceDir, "included");
        var nodeModules = Path.Combine(sourceDir, "node_modules");
        Directory.CreateDirectory(includedDir);
        Directory.CreateDirectory(nodeModules);
        await File.WriteAllTextAsync(Path.Combine(sourceDir, "root.txt"), "x");
        await File.WriteAllTextAsync(Path.Combine(includedDir, "sub.txt"), "x");
        await File.WriteAllTextAsync(Path.Combine(nodeModules, "pkg.js"), "x");

        var options = new BackupEnumerationOptions
        {
            ExcludeDirectoryNames = new[] { "node_modules" }
        };
        var result = await CollectAsync(_service.EnumerateFilesAsync(sourceDir, options, CancellationToken.None));

        Assert.Equal(2, result.Count);
        var paths = result.Select(f => f.RelativePath.Replace("\\", "/")).OrderBy(p => p).ToList();
        Assert.Contains("root.txt", paths);
        Assert.Contains("included/sub.txt", paths);
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