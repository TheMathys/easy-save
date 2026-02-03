using System.Threading;
using System.Threading.Tasks;

namespace EasyLog
{
    /// <summary>
    /// Internal contract for a log writer.
    /// Implementations write a log entry asynchronously.
    /// </summary>
    internal interface ILogWriter
    {
        /// <summary>
        /// Writes a log entry asynchronously.
        /// </summary>
        /// <param name="logEntry">The log entry to write.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation if required.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task WriteAsync(LogEntry logEntry, CancellationToken cancellationToken);
    }
}
