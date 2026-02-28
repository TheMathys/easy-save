using System.Text;
using System.Text.Json;

namespace EasyLog
{
    /// <summary>
    /// DailyLogWriter is responsible for writing log entries to daily log files in JSON format.
    /// Each daily file contains a single JSON array with one object per log entry.
    /// </summary>
    public sealed class DailyLogWriter : ILogWriter, IDisposable
    {
        private readonly Func<string> _getBaseDirectory;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly SemaphoreSlim _lock = new(1, 1);

        /// <summary>
        /// Initializes a new instance of the <see cref="DailyLogWriter"/> class.
        /// </summary>
        /// <param name="baseDirectory">Base directory where daily log files will be stored.</param>
        public DailyLogWriter(string baseDirectory)
            : this(() => baseDirectory ?? throw new ArgumentNullException(nameof(baseDirectory)))
        {
        }

        /// <summary>
        /// Initializes a new instance with a delegate to resolve the log directory at runtime.
        /// Used when the path can change (e.g. GUI changing base path without losing data).
        /// </summary>
        /// <param name="getBaseDirectory">Delegate returning the current log directory.</param>
        public DailyLogWriter(Func<string> getBaseDirectory)
        {
            _getBaseDirectory = getBaseDirectory ?? throw new ArgumentNullException(nameof(getBaseDirectory));
            _jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        }

        /// <summary>
        /// Writes the specified log entry asynchronously to a daily log file in JSON array format.
        /// The file name is determined from UTC date (format: yyyy-MM-dd.json).
        /// </summary>
        public async Task WriteAllTextAsync<T>(T logEntry, CancellationToken cancellationToken)
        {
            string baseDirectory = _getBaseDirectory();
            var logFilePath = Path.Combine(baseDirectory, $"{DateTime.UtcNow:yyyy-MM-dd}.json");
            Directory.CreateDirectory(baseDirectory);

            var json = JsonSerializer.Serialize(logEntry, _jsonOptions);

            await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var existing = File.Exists(logFilePath) ? await Task.Run(() => File.ReadAllText(logFilePath, Encoding.UTF8), cancellationToken).ConfigureAwait(false) : string.Empty;
                
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

        /// <summary>
        /// Compatibility overload: write without providing a CancellationToken.
        /// </summary>
        public Task WriteAllTextAsync<T>(T logEntry)
        {
            return WriteAllTextAsync<T>(logEntry, CancellationToken.None);
        }

        /// <summary>
        /// Disposes resources used by the writer.
        /// </summary>
        public void Dispose()
        {
            _lock.Dispose();
        }
    }
}