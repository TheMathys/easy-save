using System.Collections.ObjectModel;
using EasySave.Core.Entities;
using EasySave.Gui.ViewModels;

namespace EasySave.Gui.Services;

/// <summary>
/// Aggregates raw <see cref="BackupProgress"/> reports into a stable list of <see cref="JobProgressViewModel"/>
/// for display (one entry per job). Single responsibility: progress-to-view adaptation (Adapter pattern).
/// </summary>
public interface IBackupProgressAggregator
{
    /// <summary>
    /// Observable collection of per-job progress view models. One item per backup job that has reported progress.
    /// </summary>
    ObservableCollection<JobProgressViewModel> Items { get; }

    /// <summary>
    /// Updates the aggregator with a new progress report. Adds or updates the corresponding job entry.
    /// </summary>
    /// <param name="progress">Latest backup progress from the executor.</param>
    /// <returns>The view model that was updated, or null if the report was ignored (e.g. empty job name).</returns>
    JobProgressViewModel? Update(BackupProgress progress);

    /// <summary>
    /// Clears all aggregated progress (e.g. when a run starts or stops).
    /// </summary>
    void Clear();
}
