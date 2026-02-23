using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using EasySave.Core.Entities;
using EasySave.Gui.ViewModels;

namespace EasySave.Gui.Services;

/// <summary>
/// Adapts <see cref="BackupProgress"/> reports into a list of <see cref="JobProgressViewModel"/> for the GUI.
/// Maintains one view model per job name (by ordinal-ignore-case key) so that parallel jobs each have a stable row.
/// </summary>
public sealed class BackupProgressAggregator : IBackupProgressAggregator
{
    private readonly ILocalizationProvider _localization;
    private readonly Dictionary<string, JobProgressViewModel> _byJobName = new(StringComparer.OrdinalIgnoreCase);

    public BackupProgressAggregator(ILocalizationProvider localization)
    {
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
    }

    /// <inheritdoc />
    public ObservableCollection<JobProgressViewModel> Items { get; } = new ObservableCollection<JobProgressViewModel>();

    /// <inheritdoc />
    public JobProgressViewModel? Update(BackupProgress progress)
    {
        if (progress == null || string.IsNullOrWhiteSpace(progress.BackupName))
            return null;

        if (!_byJobName.TryGetValue(progress.BackupName, out JobProgressViewModel? viewModel))
        {
            viewModel = new JobProgressViewModel { JobName = progress.BackupName };
            _byJobName[progress.BackupName] = viewModel;
            Items.Add(viewModel);
        }

        viewModel.Percent = progress.ProgressPercent;
        viewModel.CurrentFile = string.IsNullOrEmpty(progress.CurrentSourcePath)
            ? string.Empty
            : Path.GetFileName(progress.CurrentSourcePath) ?? progress.CurrentSourcePath;

        int done = progress.TotalFilesCount - progress.RemainingFilesCount;
        viewModel.FilesText = string.Format(
            CultureInfo.CurrentUICulture,
            _localization.GetString("Gui_ProgressFilesFormat") ?? "{0} / {1} files",
            done,
            progress.TotalFilesCount);

        long transferredBytes = progress.TotalSizeBytes - progress.RemainingSizeBytes;
        viewModel.SizeText = string.Format(
            CultureInfo.CurrentUICulture,
            _localization.GetString("Gui_ProgressSizeFormat") ?? "{0} / {1}",
            FormatBytes(transferredBytes),
            FormatBytes(progress.TotalSizeBytes));

        if (progress.EstimatedTimeRemainingSeconds.HasValue && progress.EstimatedTimeRemainingSeconds.Value >= 1)
        {
            int sec = (int)Math.Round(progress.EstimatedTimeRemainingSeconds.Value);
            viewModel.EtaText = sec >= 60
                ? string.Format(CultureInfo.CurrentUICulture, _localization.GetString("Gui_ProgressEtaMinutes") ?? "~{0} min", (sec + 30) / 60)
                : string.Format(CultureInfo.CurrentUICulture, _localization.GetString("Gui_ProgressEtaSeconds") ?? "~{0} s", sec);
        }
        else
        {
            viewModel.EtaText = string.Empty;
        }

        return viewModel;
    }

    /// <inheritdoc />
    public void Clear()
    {
        _byJobName.Clear();
        Items.Clear();
    }

    private static string FormatBytes(long bytes)
    {
        const long Ko = 1024L;
        const long Mo = Ko * 1024;
        const long Go = Mo * 1024;
        if (bytes >= Go)
            return string.Format(CultureInfo.CurrentUICulture, "{0:N1} Go", (double)bytes / Go);
        if (bytes >= Mo)
            return string.Format(CultureInfo.CurrentUICulture, "{0:N1} Mo", (double)bytes / Mo);
        if (bytes >= Ko)
            return string.Format(CultureInfo.CurrentUICulture, "{0:N1} Ko", (double)bytes / Ko);
        return string.Format(CultureInfo.CurrentUICulture, "{0} o", bytes);
    }
}
