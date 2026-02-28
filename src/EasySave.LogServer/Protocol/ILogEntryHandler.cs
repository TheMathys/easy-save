using System.Threading;
using System.Threading.Tasks;

namespace EasySave.LogServer.Protocol;

/// <summary>
/// Defines the contract for handling a single received log entry (validation and persistence).
/// Allows the socket layer to remain agnostic of storage (Dependency Inversion).
/// </summary>
public interface ILogEntryHandler
{
    /// <summary>
    /// Handles one log entry (e.g. validate and persist). Called from the socket receive loop.
    /// </summary>
    /// <param name="dto">Deserialized log entry from the client.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the entry was accepted and stored; false if validation failed.</returns>
    Task<bool> HandleAsync(LogEntryDto dto, CancellationToken cancellationToken);
}
