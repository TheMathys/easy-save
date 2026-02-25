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
        private readonly string _baseDirectory;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly SemaphoreSlim _lock = new(1, 1);

        /// <summary>
        /// Initializes a new instance of the <see cref="DailyLogWriter"/> class.
        /// </summary>
        /// <param name="baseDirectory">Base directory where daily log files will be stored.</param>
        public DailyLogWriter(string baseDirectory)
        {
            _baseDirectory = baseDirectory ?? throw new ArgumentNullException(nameof(baseDirectory));
            _jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        }

        /// <summary>
        /// Writes the specified log entry asynchronously to a daily log file in JSON array format.
        /// The file name is determined from UTC date (format: yyyy-MM-dd.json).
        /// </summary>
        public async Task WriteAsync<T>(T logEntry, CancellationToken cancellationToken)
        {
            var logFilePath = Path.Combine(_baseDirectory, $"{DateTime.UtcNow:yyyy-MM-dd}.json");
            Directory.CreateDirectory(_baseDirectory);

            var json = JsonSerializer.Serialize(logEntry, _jsonOptions);

            await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                string existing = string.Empty;
                if (File.Exists(logFilePath))
                {
                    using var fs = new FileStream(logFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
                    using var mem = new MemoryStream();
                    byte[] buffer = new byte[81920];
                    int read;
                    while ((read = await fs.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
                    {
                        mem.Write(buffer, 0, read);
                    }

                    var bytes = mem.ToArray();
                    existing = Encoding.UTF8.GetString(bytes);
                }

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

                var outBytes = Encoding.UTF8.GetBytes(newContent);
                using var writeFs = new FileStream(logFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);
                await writeFs.WriteAsync(outBytes, 0, outBytes.Length, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// Compatibility overload: write without providing a CancellationToken.
        /// </summary>
        public Task WriteAsync<T>(T logEntry)
        {
            return WriteAsync<T>(logEntry, CancellationToken.None);
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