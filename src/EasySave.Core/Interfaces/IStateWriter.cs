using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EasySave.Core.Models;

namespace EasySave.Core.Interfaces
{
	/// <summary>
	/// Defines the contract for writing the real-time state of backup jobs to a persistence medium (e.g., a single JSON file).
	/// </summary>
	public interface IStateWriter
	{
		/// <summary>
		/// Asynchronously writes the current state of the provided backup jobs.
		/// </summary>
		/// <param name="progressList">A read-only list containing the progress and status of current backup jobs.</param>
		/// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
		/// <returns>A task that represents the asynchronous write operation.</returns>
		Task WriteStateAsync(IReadOnlyList<BackupProgress> progressList, CancellationToken cancellationToken = default);
	}
}