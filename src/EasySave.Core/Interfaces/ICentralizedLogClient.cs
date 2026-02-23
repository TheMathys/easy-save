using System.Threading;
using System.Threading.Tasks;
using EasySave.Core.Models;

namespace EasySave.Core.Interfaces;

/// <summary>
/// Port for sending log entries to a centralized log server.
/// Implementations (adapters) must not throw: network errors must be handled without failing the backup.
/// </summary>
public interface ICentralizedLogClient
{
    /// <summary>
    /// Sends a single log entry to the server at the given address.
    /// Does nothing if <paramref name="serverAddress"/> is null or whitespace.
    /// Must not throw; failures (timeout, connection refused) are swallowed by the implementation.
    /// </summary>
    /// <param name="entry">The log entry to send.</param>
    /// <param name="serverAddress">Host or host:port (default port 9050).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SendAsync(LogEntry entry, string? serverAddress, CancellationToken cancellationToken);
}
