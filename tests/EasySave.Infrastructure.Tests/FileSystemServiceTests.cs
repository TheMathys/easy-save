using System;
using System.IO;
using System.Threading;
using EasySave.Core.Interfaces;
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