using System.Reflection;
using System.Xml.Linq;

namespace EasyLog
{
    /// <summary>
    /// Writes log entries to a daily XML file (yyyy-MM-dd.xml).
    /// The file contains a <c>logEntries</c> root element with one <c>logEntry</c> element per write.
    /// </summary>
    public sealed class XmlDailyLogWriter : ILogWriter, IDisposable
    {
        private readonly string _baseDirectory;
        private readonly SemaphoreSlim _lock = new(1, 1);

        public XmlDailyLogWriter(string baseDirectory)
        {
            _baseDirectory = baseDirectory ?? throw new ArgumentNullException(nameof(baseDirectory));
        }

        public async Task WriteAllTextAsync<T>(T logEntry, CancellationToken cancellationToken)
        {
            string logFilePath = Path.Combine(_baseDirectory, $"{DateTime.UtcNow:yyyy-MM-dd}.xml");
            Directory.CreateDirectory(_baseDirectory);

            await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                XDocument document;
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

                    mem.Position = 0;
                    document = XDocument.Load(mem);
                }
                else
                {
                    document = new XDocument(new XElement("logEntries"));
                }

                XElement root = document.Root ?? throw new InvalidOperationException("XML log file has no root element.");
                root.Add(CreateLogEntryElement(logEntry));

                // Save to memory first then write to disk asynchronously to avoid blocking on XML serialization
                using var outStream = new MemoryStream();
                document.Save(outStream);
                var outBytes = outStream.ToArray();

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
        public Task WriteAllTextAsync<T>(T logEntry)
        {
            return WriteAllTextAsync<T>(logEntry, CancellationToken.None);
        }

        private static XElement CreateLogEntryElement<T>(T logEntry)
        {
            XElement element = new("logEntry");

            if (logEntry is null)
                return element;

            Type type = logEntry.GetType();
            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                object? value = property.GetValue(logEntry);
                element.Add(new XElement(property.Name, value));
            }

            return element;
        }

        public void Dispose()
        {
            _lock.Dispose();
        }
    }
}
