using System.Threading;
using System.Threading.Tasks;

namespace EasyLog
{
    /// <summary>
    /// Public contract for a log writer.
    /// Implementations write a log entry asynchronously.
    /// </summary>
    public interface ILogWriter
    {
        /// <summary>
        /// Writes a log entry asynchronously.
        /// </summary>
        /// <typeparam name="T">Type of the log entry payload.</typeparam>
        /// <param name="logEntry">The log entry to write.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation if required.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task WriteAllTextAsync<T>(T logEntry, CancellationToken cancellationToken);
    }
}
