using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using EasyLog;

namespace EasyLog.Tests;

/// <summary>
/// Unit tests for the <see cref="DailyLogWriter"/> class.
/// Checks creation and appending of JSON entries in daily log files.
/// </summary>
public sealed class DailyLogWriterTests : IDisposable
{
    private readonly string _tempDir;

    public DailyLogWriterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "EasyLogTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public async Task WriteLogAsync_CreatesFileWithArray_WhenMissing()
    {
        var writer = new DailyLogWriter(_tempDir);
        var entry = new LogEntry(DateTime.UtcNow, "job1", "src", "dest", 123L, TimeSpan.FromMilliseconds(10));
        await writer.WriteLogAsync(entry);

        var file = Path.Combine(_tempDir, $"{DateTime.UtcNow:yyyy-MM-dd}.json");
        Assert.True(File.Exists(file));

        var content = File.ReadAllText(file);
        using (var doc = JsonDocument.Parse(content))
        {
            Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
            Assert.Equal(1, doc.RootElement.GetArrayLength());
            var obj = doc.RootElement[0];
            Assert.Equal("job1", obj.GetProperty("BackupName").GetString());
        }
    }

    [Fact]
    public async Task WriteLogAsync_AppendsToExistingArray_WhenFileHasArray()
    {
        var file = Path.Combine(_tempDir, $"{DateTime.UtcNow:yyyy-MM-dd}.json");
        var initial = new { TimeStamp = DateTime.UtcNow, BackupName = "initial", SourcePath = "s", DestinationPath = "d", FileSizeBytes = 1, TrasnferTimeMs = TimeSpan.FromMilliseconds(1) };
        var initialJson = JsonSerializer.Serialize(initial, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(file, "[" + initialJson + "]");

        var writer = new DailyLogWriter(_tempDir);
        var entry = new LogEntry(DateTime.UtcNow, "appended", "src2", "dest2", 456L, TimeSpan.FromMilliseconds(20));
        await writer.WriteLogAsync(entry);

        var content = File.ReadAllText(file);
        using (var doc = JsonDocument.Parse(content))
        {
            Assert.Equal(2, doc.RootElement.GetArrayLength());
            Assert.Equal("appended", doc.RootElement[1].GetProperty("BackupName").GetString());
        }
    }

    [Fact]
    public async Task WriteLogAsync_PreservesTrailingWhitespace_WhenFileContainsOnlyOpeningBracket()
    {
        var file = Path.Combine(_tempDir, $"{DateTime.UtcNow:yyyy-MM-dd}.json");
        var trailing = "\r\n\r\n";
        File.WriteAllText(file, "[" + trailing);

        var writer = new DailyLogWriter(_tempDir);
        var entry = new LogEntry(DateTime.UtcNow, "onlyBracket", "s", "d", 2, TimeSpan.Zero);
        await writer.WriteLogAsync(entry);

        var content = File.ReadAllText(file);
        using (var doc = JsonDocument.Parse(content))
        {
            Assert.Equal(1, doc.RootElement.GetArrayLength());
            Assert.Equal("onlyBracket", doc.RootElement[0].GetProperty("BackupName").GetString());
        }
        Assert.EndsWith(trailing, content);
    }

    [Fact]
    public async Task WriteLogAsync_ConcurrentWrites_ProducesAllEntries()
    {
        var writer = new DailyLogWriter(_tempDir);
        var n = 10;
        var tasks = Enumerable.Range(0, n).Select(i =>
        {
            var entry = new LogEntry(DateTime.UtcNow, "job" + i, "s", "d", i, TimeSpan.FromMilliseconds(i));
            return writer.WriteLogAsync(entry);
        }).ToArray();

        await Task.WhenAll(tasks);

        var file = Path.Combine(_tempDir, $"{DateTime.UtcNow:yyyy-MM-dd}.json");
        var content = File.ReadAllText(file);
        using (var doc = JsonDocument.Parse(content))
        {
            Assert.Equal(n, doc.RootElement.GetArrayLength());
            var names = doc.RootElement.EnumerateArray().Select(e => e.GetProperty("BackupName").GetString()).ToArray();
            for (var i = 0; i < n; i++)
                Assert.Contains("job" + i, names);
        }
    }
}
