using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace EasyLog.Tests;

/// <summary>
/// Unit tests for <see cref="EasyLog.XmlDailyLogWriter"/>.
/// Checks file creation and multiple appended entries.
/// </summary>
public sealed class XmlDailyLogWriterTests : IDisposable
{
    private readonly string _tempDir;

    public XmlDailyLogWriterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "EasyLogXmlTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task WriteAsync_CreatesXmlFile_WithRootAndSingleEntry()
    {
        var writer = new EasyLog.XmlDailyLogWriter(_tempDir);
        var entry = new TestLogEntry(DateTime.UtcNow, "job1", "src", "dest", 123L, TimeSpan.FromMilliseconds(10));

        await writer.WriteAsync(entry, default);

        string file = Path.Combine(_tempDir, $"{DateTime.UtcNow:yyyy-MM-dd}.xml");
        Assert.True(File.Exists(file));

        XDocument doc = XDocument.Load(file);
        Assert.NotNull(doc.Root);
        Assert.Equal("logEntries", doc.Root!.Name.LocalName);

        XElement single = Assert.Single(doc.Root.Elements("logEntry"));
        Assert.Equal("job1", single.Element("BackupName")?.Value);
        Assert.Equal("src", single.Element("SourcePath")?.Value);
        Assert.Equal("dest", single.Element("DestinationPath")?.Value);
    }

    [Fact]
    public async Task WriteAsync_AppendsMultipleEntries()
    {
        var writer = new EasyLog.XmlDailyLogWriter(_tempDir);

        await writer.WriteAsync(new TestLogEntry(DateTime.UtcNow, "job1", "s1", "d1", 1, TimeSpan.FromMilliseconds(1)), default);
        await writer.WriteAsync(new TestLogEntry(DateTime.UtcNow, "job2", "s2", "d2", 2, TimeSpan.FromMilliseconds(2)), default);

        string file = Path.Combine(_tempDir, $"{DateTime.UtcNow:yyyy-MM-dd}.xml");
        XDocument doc = XDocument.Load(file);

        var entries = doc.Root!.Elements("logEntry").ToList();
        Assert.Equal(2, entries.Count);
        Assert.Contains(entries, e => e.Element("BackupName")?.Value == "job1");
        Assert.Contains(entries, e => e.Element("BackupName")?.Value == "job2");
    }

    private sealed class TestLogEntry
    {
        public DateTime TimeStamp { get; }
        public string BackupName { get; }
        public string SourcePath { get; }
        public string DestinationPath { get; }
        public long FileSizeBytes { get; }
        public TimeSpan TrasnferTimeMs { get; }

        public TestLogEntry(DateTime timeStamp, string backupName, string sourcePath, string destinationPath, long fileSizeBytes, TimeSpan transferTimeMs)
        {
            TimeStamp = timeStamp;
            BackupName = backupName;
            SourcePath = sourcePath;
            DestinationPath = destinationPath;
            FileSizeBytes = fileSizeBytes;
            TrasnferTimeMs = transferTimeMs;
        }
    }
}

