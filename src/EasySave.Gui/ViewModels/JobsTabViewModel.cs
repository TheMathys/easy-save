using System.Collections.ObjectModel;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Threading;
using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using EasySave.Core.Entities;
using EasySave.Core.Enums;
using EasySave.Core.Exceptions;
using EasySave.Core.Interfaces;
using EasySave.Gui.Services;

namespace EasySave.Gui.ViewModels;

/// <summary>
/// ViewModel for the jobs tab: listing, details and execution of backup jobs.
/// </summary>
public sealed class JobsTabViewModel : ViewModelBase
{
    private readonly IConfigurationHolder _configHolder;
    private readonly IBackupExecutor _backupExecutor;
    private readonly ILocalizationProvider _localization;
    private readonly IConfirmationService _confirmation;
    private JobItemViewModel? _selectedJob;
    private string _jobDetailsText = string.Empty;
    private string _statusText = string.Empty;
    private bool _isRunning;
    private CancellationTokenSource? _cts;
    private double _progressPercent;
    private string _progressJobName = string.Empty;
    private string _progressCurrentFile = string.Empty;
    private string _progressFilesText = string.Empty;
    private string _progressEtaText = string.Empty;
    private string _progressSizeText = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="JobsTabViewModel"/> class.
    /// </summary>
    /// <param name="configHolder">Configuration holder providing the current jobs.</param>
    /// <param name="backupExecutor">Executor used to run backup jobs.</param>
    /// <param name="localization">Localization provider for UI strings.</param>
        public JobsTabViewModel(
        IConfigurationHolder configHolder,
        IBackupExecutor backupExecutor,
        ILocalizationProvider localization,
        IConfirmationService confirmation)
    {
        _configHolder = configHolder;
        _backupExecutor = backupExecutor;
        _localization = localization;
        _confirmation = confirmation;
        Jobs = new ObservableCollection<JobItemViewModel>();
        SelectedJobs.CollectionChanged += SelectedJobs_CollectionChanged;
        _configHolder.ConfigurationChanged += (_, _) => Dispatcher.UIThread.Post(RefreshJobsFromConfig);
        _localization.CultureChanged += (_, _) => RaiseLocalizedProperties();
        RefreshJobsFromConfig();
    }

    private void RaiseLocalizedProperties()
    {
        RaisePropertyChanged(nameof(JobsListTitle));
        RaisePropertyChanged(nameof(DetailsTitle));
        RaisePropertyChanged(nameof(RefreshButtonText));
        RaisePropertyChanged(nameof(RunSelectedButtonText));
        RaisePropertyChanged(nameof(JobsHintText));
        RaisePropertyChanged(nameof(ProgressTitle));
        RaisePropertyChanged(nameof(ProgressCurrentFileLabel));
        RaisePropertyChanged(nameof(ProgressSizeLabel));
    }

    public string ProgressTitle => _localization.GetString("Gui_ProgressTitle");
    public string ProgressCurrentFileLabel => _localization.GetString("Gui_ProgressCurrentFile");
    public string ProgressSizeLabel => _localization.GetString("Gui_ProgressSizeLabel");

    public ObservableCollection<JobItemViewModel> Jobs { get; }

    public ObservableCollection<JobItemViewModel> SelectedJobs { get; } = new ObservableCollection<JobItemViewModel>();

    private void SelectedJobs_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (SelectedJobs == null)
        {
            RaisePropertyChanged(nameof(CanRunSelected));
            return;
        }

        // If items were added, prefer the last added item so the details show the most
        // recently selected job. Otherwise fall back to the last item in the collection.
        if (e != null && e.Action == NotifyCollectionChangedAction.Add && e.NewItems != null && e.NewItems.Count > 0)
        {
            SelectedJob = e.NewItems[e.NewItems.Count - 1] as JobItemViewModel ?? SelectedJobs.LastOrDefault();
        }
        else
        {
            SelectedJob = SelectedJobs.LastOrDefault();
        }

        UpdateDetails();
        RaisePropertyChanged(nameof(CanRunSelected));
    }

    public JobItemViewModel? SelectedJob
    {
        get => _selectedJob;
        set
        {
            if (SetProperty(ref _selectedJob, value))
            {
                UpdateDetails();
                RaisePropertyChanged(nameof(CanRunSelected));
                RaisePropertyChanged(nameof(CanDelete));
                RaisePropertyChanged(nameof(DeleteButtonText));
            }
        }
    }

    public string JobDetailsText
    {
        get => _jobDetailsText;
        set => SetProperty(ref _jobDetailsText, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public bool IsRunning
    {
        get => _isRunning;
        set
        {
            if (SetProperty(ref _isRunning, value))
            {
                RaisePropertyChanged(nameof(CanStop));
                RaisePropertyChanged(nameof(CanRunSelected));
                RaisePropertyChanged(nameof(CanRefresh));
                RaisePropertyChanged(nameof(IsProgressVisible));
                if (!value)
                    ClearProgress();
            }
        }
    }

    public bool CanStop => IsRunning;

    /// <summary>Progress 0–100 when a job is running.</summary>
    public double ProgressPercent
    {
        get => _progressPercent;
        private set => SetProperty(ref _progressPercent, value);
    }

    /// <summary>Name of the job currently reporting progress.</summary>
    public string ProgressJobName
    {
        get => _progressJobName;
        private set => SetProperty(ref _progressJobName, value ?? string.Empty);
    }

    /// <summary>Current file being transferred (path or name).</summary>
    public string ProgressCurrentFile
    {
        get => _progressCurrentFile;
        private set => SetProperty(ref _progressCurrentFile, value ?? string.Empty);
    }

    /// <summary>e.g. "12 / 50 fichiers".</summary>
    public string ProgressFilesText
    {
        get => _progressFilesText;
        private set => SetProperty(ref _progressFilesText, value ?? string.Empty);
    }

    /// <summary>Estimated time remaining (e.g. "~2 min").</summary>
    public string ProgressEtaText
    {
        get => _progressEtaText;
        private set => SetProperty(ref _progressEtaText, value ?? string.Empty);
    }

    /// <summary>Transferred size / total size (e.g. "2.5 Go / 100 Go").</summary>
    public string ProgressSizeText
    {
        get => _progressSizeText;
        private set => SetProperty(ref _progressSizeText, value ?? string.Empty);
    }

    /// <summary>True when progress data should be shown (job running and progress received).</summary>
    public bool IsProgressVisible => IsRunning && !string.IsNullOrEmpty(_progressJobName);

    public string JobsListTitle => _localization.GetString("Gui_JobsListTitle");
    public string DetailsTitle => _localization.GetString("Gui_DetailsTitle");
    public string RefreshButtonText => _localization.GetString("Gui_Refresh");
    public string RunSelectedButtonText => _localization.GetString("Gui_RunSelected");
    public string StopButtonText => _localization.GetString("Gui_Stop");
    public string JobsHintText => _localization.GetString("Gui_JobsHint");

    public string DeleteButtonText => "Supprimer";

    public bool CanDelete => SelectedJob != null && !IsRunning;

    public void DeleteSelected(object _)
    {
        _ = DeleteSelectedAsync();
    }

    private async Task DeleteSelectedAsync()
    {
        if (SelectedJob == null)
        {
            StatusText = _localization.GetString("Gui_SelectJobFirst");
            return;
        }

        bool confirmed = await _confirmation.ConfirmAsync(
            _localization.GetString("TuiConfirmDelete") ?? "Confirm deletion",
            $"Êtes-vous sur de vouloir supprimer : {SelectedJob.Name}");

        if (!confirmed)
        {
            StatusText = _localization.GetString("Gui_DeleteCancelled") ?? "Cancelled.";
            return;
        }

        BackupConfiguration config = _configHolder.Current;
        List<BackupJob> newJobs = config.Jobs.Where(j => j.Id != SelectedJob.Id).ToList();
        Dictionary<int, DateTime> newLastFull = config.LastFullBackupUtcByJobId
            .Where(kv => kv.Key != SelectedJob.Id)
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        BackupConfiguration updated = new BackupConfiguration
        {
            LogAndStateDirectory = config.LogAndStateDirectory,
            LogFileFormat = config.LogFileFormat,
            Jobs = newJobs,
            LastFullBackupUtcByJobId = newLastFull,
            EncryptExtensions = config.EncryptExtensions,
            EncryptionKeyPath = config.EncryptionKeyPath
        };

        await _configHolder.SaveAsync(updated, CancellationToken.None).ConfigureAwait(true);
        StatusText = _localization.GetString("TuiJobDeleted", SelectedJob.Id);
        RefreshJobsFromConfig();
    }

    public bool CanRefresh => !IsRunning;

    public void Refresh(object _)
    {
        _ = RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        IsRunning = true;
        try
        {
            await _configHolder.ReloadAsync(CancellationToken.None).ConfigureAwait(false);
            string message = _localization.GetString("Gui_JobsRefreshed");
            Dispatcher.UIThread.Post(() =>
            {
                StatusText = message;
                IsRunning = false;
                RaisePropertyChanged(nameof(CanRefresh));
                RaisePropertyChanged(nameof(CanRunSelected));
            });
        }
        catch (Exception ex)
        {
            string errorMessage = _localization.GetString("Gui_JobError", ex.Message);
            Dispatcher.UIThread.Post(() =>
            {
                StatusText = errorMessage;
                IsRunning = false;
                RaisePropertyChanged(nameof(CanRefresh));
                RaisePropertyChanged(nameof(CanRunSelected));
            });
        }
    }

    public bool CanRunSelected => SelectedJobs != null && SelectedJobs.Count > 0 && !IsRunning;

    public async void RunSelected(object _)
    {
        if (IsRunning)
        {
            return;
        }

        if (SelectedJobs == null || SelectedJobs.Count == 0)
        {
            StatusText = _localization.GetString("Gui_SelectJobFirst");
            return;
        }

        int[] ids = SelectedJobs.Select(j => j.Id).ToArray();

        // prepare cancellation
        _cts?.Dispose();
        _cts = new CancellationTokenSource();

        IsRunning = true;
        ClearProgress();
        StatusText = _localization.GetString("Gui_JobRunning", string.Join(", ", ids));
        IProgress<BackupProgress> progress = new Progress<BackupProgress>(p =>
        {
            Dispatcher.UIThread.Post(() => UpdateProgress(p));
        });
        string? successMessage = _localization.GetString("Gui_JobSuccess", string.Join(", ", ids));
        string cancelMessage = _localization.GetString("Gui_JobCancelledByUser");
        try
        {
            await Task.Run(async () => await _backupExecutor.ExecuteAsync(ids, progress, _cts.Token)).ConfigureAwait(false);
            string message = _cts?.IsCancellationRequested == true ? cancelMessage : successMessage;
            Dispatcher.UIThread.Post(() => { StatusText = message; });
        }
        catch (OperationCanceledException)
        {
            Dispatcher.UIThread.Post(() => { StatusText = cancelMessage; });
        }
        catch (BusinessSoftwareDetectedException)
        {
            StatusText = _localization.GetString("Gui_BusinessSoftwareDetected");
        }
        catch (Exception ex)
        {
            string errorMessage = _localization.GetString("Gui_JobError", ex.Message);
            Dispatcher.UIThread.Post(() => { StatusText = errorMessage; });
        }
        finally
        {
            Dispatcher.UIThread.Post(() =>
            {
                IsRunning = false;
                _cts?.Dispose();
                _cts = null;
                RaisePropertyChanged(nameof(CanRefresh));
            });
        }
    }

    private void ClearProgress()
    {
        ProgressPercent = 0;
        ProgressJobName = string.Empty;
        ProgressCurrentFile = string.Empty;
        ProgressFilesText = string.Empty;
        ProgressEtaText = string.Empty;
        ProgressSizeText = string.Empty;
        RaisePropertyChanged(nameof(IsProgressVisible));
    }

    private static string FormatBytes(long bytes)
    {
        const long Ko = 1024L;
        const long Mo = Ko * 1024;
        const long Go = Mo * 1024;
        if (bytes >= Go)
            return string.Format(System.Globalization.CultureInfo.CurrentUICulture, "{0:N1} Go", (double)bytes / Go);
        if (bytes >= Mo)
            return string.Format(System.Globalization.CultureInfo.CurrentUICulture, "{0:N1} Mo", (double)bytes / Mo);
        if (bytes >= Ko)
            return string.Format(System.Globalization.CultureInfo.CurrentUICulture, "{0:N1} Ko", (double)bytes / Ko);
        return string.Format(System.Globalization.CultureInfo.CurrentUICulture, "{0} o", bytes);
    }

    private void UpdateProgress(BackupProgress p)
    {
        ProgressPercent = p.ProgressPercent;
        ProgressJobName = p.BackupName;
        ProgressCurrentFile = string.IsNullOrEmpty(p.CurrentSourcePath)
            ? string.Empty
            : System.IO.Path.GetFileName(p.CurrentSourcePath) ?? p.CurrentSourcePath;
        int done = p.TotalFilesCount - p.RemainingFilesCount;
        ProgressFilesText = string.Format(_localization.GetString("Gui_ProgressFilesFormat") ?? "{0} / {1} files", done, p.TotalFilesCount);
        long transferredBytes = p.TotalSizeBytes - p.RemainingSizeBytes;
        ProgressSizeText = string.Format(_localization.GetString("Gui_ProgressSizeFormat") ?? "{0} / {1}", FormatBytes(transferredBytes), FormatBytes(p.TotalSizeBytes));
        if (p.EstimatedTimeRemainingSeconds.HasValue && p.EstimatedTimeRemainingSeconds.Value >= 1)
        {
            int sec = (int)Math.Round(p.EstimatedTimeRemainingSeconds.Value);
            if (sec >= 60)
                ProgressEtaText = string.Format(_localization.GetString("Gui_ProgressEtaMinutes") ?? "~{0} min", (sec + 30) / 60);
            else
                ProgressEtaText = string.Format(_localization.GetString("Gui_ProgressEtaSeconds") ?? "~{0} s", sec);
        }
        else
            ProgressEtaText = string.Empty;
        RaisePropertyChanged(nameof(IsProgressVisible));
    }

    public void Stop(object _)
    {
        if (_cts != null && !_cts.IsCancellationRequested)
        {
            _cts.Cancel();
            StatusText = _localization.GetString("Gui_JobCancelledByUser");
        }
    }

    internal void RefreshJobsFromConfig()
    {
        BackupConfiguration config = _configHolder.Current;
        List<int> previouslySelectedIds = SelectedJobs.Select(x => x.Id).ToList();

        Jobs.Clear();
        foreach (BackupJob j in config.Jobs.OrderBy(x => x.Id))
            Jobs.Add(new JobItemViewModel(j.Id, j.Name, j.Type));

        // restore selection
        SelectedJobs.Clear();
        foreach (int id in previouslySelectedIds)
        {
            JobItemViewModel? item = Jobs.FirstOrDefault(x => x.Id == id);
            if (item != null)
                SelectedJobs.Add(item);
        }

        // keep single SelectedJob compatible with previous behaviour
        if (SelectedJobs.Count > 0)
            SelectedJob = SelectedJobs[0];
        else if (Jobs.Count > 0 && SelectedJob == null)
            SelectedJob = Jobs[0];
        else if (SelectedJob != null)
        {
            JobItemViewModel? stillSelected = Jobs.FirstOrDefault(x => x.Id == SelectedJob.Id);
            SelectedJob = stillSelected;
        }

        if (Jobs.Count == 0)
            JobDetailsText = _localization.GetString("NoJobsFound");
        else
            UpdateDetails();
        RaisePropertyChanged(nameof(CanRunSelected));
    }

    private void UpdateDetails()
    {
        if (SelectedJob == null)
            return;
        BackupConfiguration config = _configHolder.Current;
        BackupJob? job = config.Jobs.FirstOrDefault(j => j.Id == SelectedJob.Id);
        if (job == null)
            return;

        string typeStr = job.Type == BackupType.Differential
            ? _localization.GetString("DifferentialBackup")
            : _localization.GetString("FullBackup");
        JobDetailsText = string.Format(
            _localization.GetString("Gui_JobDetailsFormat"),
            job.Id,
            job.Name,
            typeStr,
            job.SourcePath,
            job.TargetPath,
            string.Join(", ", job.ExcludeExtensions ?? Array.Empty<string>()),
            string.Join(", ", job.ExcludeDirectoryNames ?? Array.Empty<string>()));
    }
}
