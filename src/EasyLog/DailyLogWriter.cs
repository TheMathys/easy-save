using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace EasyLog
{
    /// <summary>
    /// DailyLogWriter is responsible for writing log entries to daily log files in JSON format.
    /// Each daily file contains a single JSON array with one object per log entry.
    /// </summary>
    public sealed class DailyLogWriter
    {
        private string _baseDirectory;
        private JsonSerializerOptions _jsonOptions;
        private readonly SemaphoreSlim _lock = new(1,1);

        /// <summary>
        /// Initializes a new instance of the <see cref="DailyLogWriter"/> class.
        /// </summary>
        /// <param name="baseDirectory">Base directory where daily log files will be stored.</param>
        public DailyLogWriter(string baseDirectory)
        {
            _baseDirectory = baseDirectory;
            _jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        }

        /// <summary>
        /// Writes the specified log entry asynchronously to a daily log file in JSON array format.
        /// The file name is determined from UTC date (format: yyyy-MM-dd.json).
        /// </summary>
        public async Task WriteLogAsync<T>(T logEntry, CancellationToken cancellationToken = default)
        {
            var logFilePath = Path.Combine(_baseDirectory, $"{DateTime.UtcNow:yyyy-MM-dd}.json");
            Directory.CreateDirectory(_baseDirectory);

            var json = JsonSerializer.Serialize(logEntry, _jsonOptions);

            await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var existing = File.Exists(logFilePath)
                    ? await Task.Run(() => File.ReadAllText(logFilePath, Encoding.UTF8), cancellationToken).ConfigureAwait(false)
                    : string.Empty;
                var trimmed = existing.TrimEnd();
                var trailing = existing.Length > trimmed.Length ? existing.Substring(trimmed.Length) : string.Empty;

                string newContent;
                if (string.IsNullOrEmpty(trimmed) || trimmed == "[")
                    newContent = "[" + json + "]" + trailing;
                else
                {
                    var lastBracket = trimmed.LastIndexOf(']');
                    newContent = lastBracket >= 0
                        ? trimmed.Substring(0, lastBracket) + "," + json + trimmed.Substring(lastBracket) + trailing
                        : trimmed + "," + json + "]" + trailing;
                }

                await Task.Run(() => File.WriteAllText(logFilePath, newContent, Encoding.UTF8), cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _lock.Release();
            }
        }
    }
}