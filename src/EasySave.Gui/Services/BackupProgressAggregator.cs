using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using EasySave.Core.Entities;
using EasySave.Gui.ViewModels;
using EasySave.Core.Enums;

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

        viewModel.JobId = progress.JobId;
        viewModel.State = progress.State;
        ApplyStateBadge(viewModel, progress.State);
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

        viewModel.SummaryText = BuildSummary(progress);

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

    private void ApplyStateBadge(JobProgressViewModel viewModel, BackupState state)
    {
        switch (state)
        {
            case BackupState.Active:
                viewModel.StateText = _localization.GetString("Gui_ProgressStateActive") ?? "Active";
                viewModel.StateBadgeBackground = "#DBEAFE";
                viewModel.StateBadgeForeground = "#1D4ED8";
                break;
            case BackupState.Paused:
                viewModel.StateText = _localization.GetString("Gui_ProgressStatePaused") ?? "Paused";
                viewModel.StateBadgeBackground = "#FEF3C7";
                viewModel.StateBadgeForeground = "#92400E";
                break;
            case BackupState.Completed:
                viewModel.StateText = _localization.GetString("Gui_ProgressStateCompleted") ?? "Completed";
                viewModel.StateBadgeBackground = "#DCFCE7";
                viewModel.StateBadgeForeground = "#166534";
                break;
            case BackupState.Error:
                viewModel.StateText = _localization.GetString("Gui_ProgressStateError") ?? "Error";
                viewModel.StateBadgeBackground = "#FEE2E2";
                viewModel.StateBadgeForeground = "#991B1B";
                break;
            default:
                viewModel.StateText = _localization.GetString("Gui_ProgressStateInactive") ?? "Inactive";
                viewModel.StateBadgeBackground = "#E5E7EB";
                viewModel.StateBadgeForeground = "#374151";
                break;
        }
    }

    private string BuildSummary(BackupProgress progress)
    {
        if (!progress.ElapsedTimeSeconds.HasValue || progress.ElapsedTimeSeconds.Value < 0.5)
            return string.Empty;

        string elapsed = FormatDuration(progress.ElapsedTimeSeconds.Value);
        if (progress.State == BackupState.Completed)
        {
            return string.Format(
                CultureInfo.CurrentUICulture,
                _localization.GetString("Gui_ProgressSummaryCompleted") ?? "Completed in {0}.",
                elapsed);
        }

        if (progress.State == BackupState.Inactive && progress.TotalFilesCount > 0 && progress.RemainingFilesCount > 0)
        {
            return string.Format(
                CultureInfo.CurrentUICulture,
                _localization.GetString("Gui_ProgressSummaryStopped") ?? "Stopped after {0}.",
                elapsed);
        }

        if (progress.State == BackupState.Error)
        {
            return string.Format(
                CultureInfo.CurrentUICulture,
                _localization.GetString("Gui_ProgressSummaryError") ?? "Failed after {0}.",
                elapsed);
        }

        return string.Empty;
    }

    private static string FormatDuration(double seconds)
    {
        TimeSpan ts = TimeSpan.FromSeconds(Math.Max(0, seconds));
        if (ts.TotalHours >= 1)
            return string.Format(CultureInfo.CurrentUICulture, "{0}h {1:D2}m {2:D2}s", (int)ts.TotalHours, ts.Minutes, ts.Seconds);
        if (ts.TotalMinutes >= 1)
            return string.Format(CultureInfo.CurrentUICulture, "{0}m {1:D2}s", ts.Minutes, ts.Seconds);
        return string.Format(CultureInfo.CurrentUICulture, "{0}s", Math.Max(1, ts.Seconds));
    }
}
