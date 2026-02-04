using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using EasySave.Core.Entities;
using EasySave.Infrastructure.Persistence;

namespace EasySave.Infrastructure.Tests;

public sealed class JsonStateWriterTests : IDisposable
{
    private readonly List<string> _filesToDelete = new();

    private string GetTempFilePath()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "state.json");
        _filesToDelete.Add(path);
        return path;
    }

    public void Dispose()
    {
        foreach (var file in _filesToDelete)
        {
            try
            {
                if (File.Exists(file)) File.Delete(file);
                var dir = Path.GetDirectoryName(file);
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir) && Directory.GetFiles(dir).Length == 0)
                    Directory.Delete(dir);
            }
            catch { }
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_ThrowsArgumentNullException_WhenPathIsInvalid(string? invalidPath)
    {
        Assert.Throws<ArgumentNullException>(() => new JsonStateWriter(invalidPath!));
    }

    [Fact]
    public async Task WriteStateAsync_CreatesFile_WhenCalled()
    {
        var filePath = GetTempFilePath();
        var writer = new JsonStateWriter(filePath);
        var progressList = new List<BackupProgress>
        {
            new BackupProgress(),
            new BackupProgress()
        };

        await writer.WriteStateAsync(progressList);

        Assert.True(File.Exists(filePath));
        var content = await File.ReadAllTextAsync(filePath);
        Assert.NotEmpty(content);
        Assert.Contains("updatedAt", content);
        Assert.Contains("jobs", content);
    }

    [Fact]
    public async Task WriteStateAsync_CreatesDirectory_WhenItDoesNotExist()
    {
        var deepPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "SubFolder", "state.json");
        _filesToDelete.Add(deepPath);
        var writer = new JsonStateWriter(deepPath);

        await writer.WriteStateAsync(new List<BackupProgress>());

        Assert.True(File.Exists(deepPath));
    }
}
