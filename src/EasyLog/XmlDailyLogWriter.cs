using System.Reflection;
using System.Xml.Linq;

namespace EasyLog
{
    /// <summary>
    /// Writes log entries to a daily XML file (yyyy-MM-dd.xml).
    /// The file contains a <logEntries> root element with one <logEntry> element per write.
    /// </summary>
    public sealed class XmlDailyLogWriter : ILogWriter
    {
        private readonly string _baseDirectory;
        private readonly SemaphoreSlim _lock = new(1, 1);

        public XmlDailyLogWriter(string baseDirectory)
        {
            _baseDirectory = baseDirectory ?? throw new ArgumentNullException(nameof(baseDirectory));
        }

        public async Task WriteAsync<T>(T logEntry, CancellationToken cancellationToken)
        {
            string logFilePath = Path.Combine(_baseDirectory, $"{DateTime.UtcNow:yyyy-MM-dd}.xml");
            Directory.CreateDirectory(_baseDirectory);

            await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                XDocument document;
                if (File.Exists(logFilePath))
                {
                    using FileStream stream = new(logFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    document = XDocument.Load(stream);
                }
                else
                {
                    document = new XDocument(new XElement("logEntries"));
                }

                XElement root = document.Root ?? throw new InvalidOperationException("XML log file has no root element.");
                root.Add(CreateLogEntryElement(logEntry));

                using FileStream writeStream = new(logFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
                document.Save(writeStream);
            }
            finally
            {
                _lock.Release();
            }
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
    }
}

