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
    private readonly IBackupProgressAggregator _progressAggregator;
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
    private readonly HashSet<int> _runningJobIds = new();
    private readonly Dictionary<int, Task> _runningTasks = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="JobsTabViewModel"/> class.
    /// </summary>
    /// <param name="configHolder">Configuration holder providing the current jobs.</param>
    /// <param name="backupExecutor">Executor used to run backup jobs.</param>
    /// <param name="localization">Localization provider for UI strings.</param>
    /// <param name="confirmation">Confirmation service for delete and other prompts.</param>
    /// <param name="progressAggregator">Aggregates backup progress into per-job view models for the UI.</param>
    public JobsTabViewModel(
        IConfigurationHolder configHolder,
        IBackupExecutor backupExecutor,
        ILocalizationProvider localization,
        IConfirmationService confirmation,
        IBackupProgressAggregator progressAggregator)
    {
        _configHolder = configHolder;
        _backupExecutor = backupExecutor;
        _localization = localization;
        _confirmation = confirmation;
        _progressAggregator = progressAggregator ?? throw new ArgumentNullException(nameof(progressAggregator));
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
        RaisePropertyChanged(nameof(StopButtonText));
        RaisePropertyChanged(nameof(DeleteButtonText));
        RaisePropertyChanged(nameof(JobsHintText));
        RaisePropertyChanged(nameof(ProgressTitle));
        RaisePropertyChanged(nameof(ProgressCurrentFileLabel));
        RaisePropertyChanged(nameof(ProgressSizeLabel));

        // Update job details with new language
        System.Diagnostics.Debug.WriteLine($"JobsTabViewModel: Language changed, updating details. Jobs.Count={Jobs.Count}, SelectedJob={SelectedJob?.Name ?? "null"}");

        if (Jobs.Count == 0)
        {
            JobDetailsText = _localization.GetString("NoJobsFound");
        }
        else
        {
            UpdateDetails();
            ReapplyJobStateBadges();
        }
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
        RaisePropertyChanged(nameof(CanPauseSelected));
        RaisePropertyChanged(nameof(CanResumeSelected));
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
                RaisePropertyChanged(nameof(CanPauseSelected));
                RaisePropertyChanged(nameof(CanResumeSelected));
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

    public ObservableCollection<JobProgressViewModel> JobProgressItems => _progressAggregator.Items;

    /// <summary>True when at least one job is reporting progress.</summary>
    public bool IsProgressVisible => JobProgressItems.Count > 0;

    public string JobsListTitle => _localization.GetString("Gui_JobsListTitle");
    public string DetailsTitle => _localization.GetString("Gui_DetailsTitle");
    public string RefreshButtonText => _localization.GetString("Gui_Refresh");
    public string RunSelectedButtonText => _localization.GetString("Gui_RunSelected");
    public string StopButtonText => _localization.GetString("Gui_Stop");
    public string PauseButtonText => _localization.GetString("Gui_Pause");
    public string ResumeButtonText => _localization.GetString("Gui_Resume");
    public string DeleteButtonText => _localization.GetString("Gui_DeleteJobLabel");
    public string JobsHintText => _localization.GetString("Gui_JobsHint");

    public bool CanDelete => SelectedJob != null && !IsRunning;

    public bool CanPauseSelected
    {
        get
        {
            return ResolvePauseResumeTargets(forPause: true).Count > 0;
        }
    }

    public bool CanResumeSelected
    {
        get
        {
            return ResolvePauseResumeTargets(forPause: false).Count > 0;
        }
    }

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
            LogDestination = config.LogDestination,
            CentralizedLogServerAddress = config.CentralizedLogServerAddress,
            Jobs = newJobs,
            LastFullBackupUtcByJobId = newLastFull,
            EncryptExtensions = config.EncryptExtensions,
            EncryptionKeyPath = config.EncryptionKeyPath,
            BusinessSoftwareProcessName = config.BusinessSoftwareProcessName,
            LargeFileThresholdKb = config.LargeFileThresholdKb
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
                RaisePropertyChanged(nameof(CanPauseSelected));
                RaisePropertyChanged(nameof(CanResumeSelected));
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
                RaisePropertyChanged(nameof(CanPauseSelected));
                RaisePropertyChanged(nameof(CanResumeSelected));
            });
        }
    }

    public bool CanRunSelected =>
        SelectedJobs != null
        && SelectedJobs.Any(j => !_runningJobIds.Contains(j.Id));

    public void RunSelected(object _)
    {
        if (SelectedJobs == null || SelectedJobs.Count == 0)
        {
            StatusText = _localization.GetString("Gui_SelectJobFirst");
            return;
        }

        List<int> toStart = SelectedJobs
            .Select(j => j.Id)
            .Where(id => !_runningJobIds.Contains(id))
            .Distinct()
            .ToList();

        if (toStart.Count == 0)
        {
            StatusText = _localization.GetString("Gui_JobAlreadyRunning");
            return;
        }

        if (_cts == null || _cts.IsCancellationRequested)
        {
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
        }

        if (!IsRunning)
            ClearProgress();

        foreach (int id in toStart)
        {
            _runningJobIds.Add(id);
            JobItemViewModel? job = Jobs.FirstOrDefault(j => j.Id == id);
            if (job != null)
                ApplyJobStateBadge(job, BackupState.Active);
            _runningTasks[id] = StartJobExecutionAsync(id, _cts.Token);
        }

        IsRunning = true;
        StatusText = _localization.GetString("Gui_JobRunning", string.Join(", ", toStart));
        RaisePropertyChanged(nameof(CanRunSelected));
        RaisePropertyChanged(nameof(CanPauseSelected));
        RaisePropertyChanged(nameof(CanResumeSelected));
    }

    private async Task StartJobExecutionAsync(int jobId, CancellationToken token)
    {
        IProgress<BackupProgress> progress = new Progress<BackupProgress>(p =>
        {
            Dispatcher.UIThread.Post(() => UpdateProgress(p));
        });

        try
        {
            await Task.Run(async () => await _backupExecutor.ExecuteAsync(new[] { jobId }, progress, token)).ConfigureAwait(false);
            Dispatcher.UIThread.Post(() =>
            {
                StatusText = _localization.GetString("Gui_JobSuccess", jobId);
            });
        }
        catch (OperationCanceledException)
        {
            Dispatcher.UIThread.Post(() =>
            {
                StatusText = _localization.GetString("Gui_JobCancelledByUser");
            });
        }
        catch (BusinessSoftwareDetectedException)
        {
            Dispatcher.UIThread.Post(() =>
            {
                StatusText = _localization.GetString("Gui_BusinessSoftwareDetected");
            });
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
                _runningTasks.Remove(jobId);
                _runningJobIds.Remove(jobId);
                if (_runningJobIds.Count == 0)
                {
                    IsRunning = false;
                    _cts?.Dispose();
                    _cts = null;
                }
                RaisePropertyChanged(nameof(CanRunSelected));
                RaisePropertyChanged(nameof(CanRefresh));
                RaisePropertyChanged(nameof(CanPauseSelected));
                RaisePropertyChanged(nameof(CanResumeSelected));
            });
        }
    }

    private void ClearProgress()
    {
        _progressAggregator.Clear();
        ProgressPercent = 0;
        ProgressJobName = string.Empty;
        ProgressCurrentFile = string.Empty;
        ProgressFilesText = string.Empty;
        ProgressEtaText = string.Empty;
        ProgressSizeText = string.Empty;
        RaisePropertyChanged(nameof(IsProgressVisible));
    }

    private void UpdateProgress(BackupProgress p)
    {
        JobProgressViewModel? item = _progressAggregator.Update(p);
        SyncJobStateFromProgress(p);
        if (item != null)
        {
            ProgressPercent = item.Percent;
            ProgressJobName = item.JobName;
            ProgressCurrentFile = item.CurrentFile;
            ProgressFilesText = item.FilesText;
            ProgressSizeText = item.SizeText;
            ProgressEtaText = item.EtaText;
        }
        RaisePropertyChanged(nameof(IsProgressVisible));
        RaisePropertyChanged(nameof(CanPauseSelected));
        RaisePropertyChanged(nameof(CanResumeSelected));
    }

    public void Stop(object _)
    {
        if (_cts != null && !_cts.IsCancellationRequested)
        {
            _cts.Cancel();
            StatusText = _localization.GetString("Gui_JobCancelledByUser");
        }
    }

    public async void StopProgressJob(object? parameter)
    {
        int? jobId = ResolveJobId(parameter);
        if (!jobId.HasValue)
            return;

        await _backupExecutor.StopJobAsync(jobId.Value, CancellationToken.None).ConfigureAwait(true);
        StatusText = _localization.GetString("Gui_JobCancelledByUser");
    }

    public async void PauseProgressJob(object? parameter)
    {
        int? jobId = ResolveJobId(parameter);
        if (!jobId.HasValue)
            return;

        await _backupExecutor.PauseJobAsync(jobId.Value, CancellationToken.None).ConfigureAwait(true);
        StatusText = _localization.GetString("Gui_JobPaused", jobId.Value);
    }

    public async void ResumeProgressJob(object? parameter)
    {
        int? jobId = ResolveJobId(parameter);
        if (!jobId.HasValue)
            return;

        await _backupExecutor.ResumeJobAsync(jobId.Value, CancellationToken.None).ConfigureAwait(true);
        StatusText = _localization.GetString("Gui_JobResumed", jobId.Value);
    }

    public async void PauseSelected(object _)
    {
        List<int> targets = ResolvePauseResumeTargets(forPause: true);
        if (targets.Count == 0)
        {
            StatusText = _localization.GetString("Gui_SelectJobFirst");
            return;
        }

        foreach (int id in targets)
            await _backupExecutor.PauseJobAsync(id, CancellationToken.None).ConfigureAwait(true);

        StatusText = targets.Count == 1
            ? _localization.GetString("Gui_JobPaused", targets[0])
            : _localization.GetString("Gui_JobPaused", string.Join(", ", targets));
        RaisePropertyChanged(nameof(CanPauseSelected));
        RaisePropertyChanged(nameof(CanResumeSelected));
    }

    public async void ResumeSelected(object _)
    {
        List<int> targets = ResolvePauseResumeTargets(forPause: false);
        if (targets.Count == 0)
        {
            StatusText = _localization.GetString("Gui_SelectJobFirst");
            return;
        }

        foreach (int id in targets)
            await _backupExecutor.ResumeJobAsync(id, CancellationToken.None).ConfigureAwait(true);

        StatusText = targets.Count == 1
            ? _localization.GetString("Gui_JobResumed", targets[0])
            : _localization.GetString("Gui_JobResumed", string.Join(", ", targets));
        RaisePropertyChanged(nameof(CanPauseSelected));
        RaisePropertyChanged(nameof(CanResumeSelected));
    }

    private List<int> ResolvePauseResumeTargets(bool forPause)
    {
        if (!IsRunning || _runningJobIds.Count == 0)
            return new List<int>();

        List<int> selectedRunning = SelectedJobs
            .Select(j => j.Id)
            .Where(id => _runningJobIds.Contains(id))
            .Distinct()
            .ToList();

        List<int> candidates = selectedRunning.Count > 0
            ? selectedRunning
            : _runningJobIds.ToList();

        return candidates
            .Where(id => forPause ? !IsJobCurrentlyPaused(id) : IsJobCurrentlyPaused(id))
            .ToList();
    }

    private bool IsJobCurrentlyPaused(int jobId)
    {
        JobProgressViewModel? progress = JobProgressItems.FirstOrDefault(p => p.JobId == jobId);
        return progress != null && progress.State == BackupState.Paused;
    }

    private static int? ResolveJobId(object? parameter)
    {
        if (parameter is JobProgressViewModel vm)
            return vm.JobId;
        if (parameter is JobItemViewModel item)
            return item.Id;
        if (parameter is int id)
            return id;
        return null;
    }

    internal void RefreshJobsFromConfig()
    {
        BackupConfiguration config = _configHolder.Current;
        List<int> previouslySelectedIds = SelectedJobs.Select(x => x.Id).ToList();
        Dictionary<int, BackupState> previousStatesByJobId = Jobs.ToDictionary(j => j.Id, j => j.State);

        Jobs.Clear();
        foreach (BackupJob j in config.Jobs.OrderBy(x => x.Id))
        {
            JobItemViewModel item = new JobItemViewModel(j.Id, j.Name, j.Type);
            BackupState initialState = previousStatesByJobId.TryGetValue(j.Id, out BackupState previous)
                ? previous
                : BackupState.Inactive;
            ApplyJobStateBadge(item, initialState);
            Jobs.Add(item);
        }

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
        RaisePropertyChanged(nameof(CanPauseSelected));
        RaisePropertyChanged(nameof(CanResumeSelected));
    }

    private void ReapplyJobStateBadges()
    {
        foreach (JobItemViewModel job in Jobs)
            ApplyJobStateBadge(job, job.State);
    }

    private void SyncJobStateFromProgress(BackupProgress progress)
    {
        JobItemViewModel? job = Jobs.FirstOrDefault(j => j.Id == progress.JobId);
        if (job == null)
            return;

        ApplyJobStateBadge(job, progress.State);
    }

    private void ApplyJobStateBadge(JobItemViewModel job, BackupState state)
    {
        job.State = state;
        switch (state)
        {
            case BackupState.Active:
                job.StateText = _localization.GetString("Gui_ProgressStateActive");
                job.StateBadgeBackground = "#DBEAFE";
                job.StateBadgeForeground = "#1D4ED8";
                break;
            case BackupState.Paused:
                job.StateText = _localization.GetString("Gui_ProgressStatePaused");
                job.StateBadgeBackground = "#FEF3C7";
                job.StateBadgeForeground = "#92400E";
                break;
            case BackupState.Completed:
                job.StateText = _localization.GetString("Gui_ProgressStateCompleted");
                job.StateBadgeBackground = "#DCFCE7";
                job.StateBadgeForeground = "#166534";
                break;
            case BackupState.Error:
                job.StateText = _localization.GetString("Gui_ProgressStateError");
                job.StateBadgeBackground = "#FEE2E2";
                job.StateBadgeForeground = "#991B1B";
                break;
            default:
                job.StateText = _localization.GetString("Gui_ProgressStateInactive");
                job.StateBadgeBackground = "#E5E7EB";
                job.StateBadgeForeground = "#374151";
                break;
        }
    }

    private void UpdateDetails()
    {
        if (SelectedJob == null)
        {
            JobDetailsText = string.Empty;
            return;
        }
        BackupConfiguration config = _configHolder.Current;
        BackupJob? job = config.Jobs.FirstOrDefault(j => j.Id == SelectedJob.Id);
        if (job == null)
        {
            JobDetailsText = string.Empty;
            return;
        }

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

/// <summary>
/// View model for a single job's progress in the GUI (name, percent, files, size, ETA, current file).
/// Used as the data item for each row in the multi-job progress list (Composite display).
/// </summary>
public sealed class JobProgressViewModel : ViewModelBase
{
    private int _jobId;
    private double _percent;
    private string _jobName = string.Empty;
    private string _currentFile = string.Empty;
    private string _filesText = string.Empty;
    private string _etaText = string.Empty;
    private string _sizeText = string.Empty;
    private BackupState _state;
    private string _summaryText = string.Empty;

    public int JobId
    {
        get => _jobId;
        set => SetProperty(ref _jobId, value);
    }

    public double Percent
    {
        get => _percent;
        set => SetProperty(ref _percent, value);
    }

    public string JobName
    {
        get => _jobName;
        set => SetProperty(ref _jobName, value ?? string.Empty);
    }

    public string CurrentFile
    {
        get => _currentFile;
        set => SetProperty(ref _currentFile, value ?? string.Empty);
    }

    public string FilesText
    {
        get => _filesText;
        set => SetProperty(ref _filesText, value ?? string.Empty);
    }

    public string EtaText
    {
        get => _etaText;
        set => SetProperty(ref _etaText, value ?? string.Empty);
    }

    public string SizeText
    {
        get => _sizeText;
        set => SetProperty(ref _sizeText, value ?? string.Empty);
    }

    public BackupState State
    {
        get => _state;
        set
        {
            if (!SetProperty(ref _state, value))
                return;
            RaisePropertyChanged(nameof(CanPause));
            RaisePropertyChanged(nameof(CanResume));
            RaisePropertyChanged(nameof(CanStop));
        }
    }

    private string _stateText = string.Empty;
    private string _stateBadgeBackground = "#E5E7EB";
    private string _stateBadgeForeground = "#374151";

    public string SummaryText
    {
        get => _summaryText;
        set => SetProperty(ref _summaryText, value ?? string.Empty);
    }

    public string StateText
    {
        get => _stateText;
        set => SetProperty(ref _stateText, value ?? string.Empty);
    }

    public string StateBadgeBackground
    {
        get => _stateBadgeBackground;
        set => SetProperty(ref _stateBadgeBackground, value ?? "#E5E7EB");
    }

    public string StateBadgeForeground
    {
        get => _stateBadgeForeground;
        set => SetProperty(ref _stateBadgeForeground, value ?? "#374151");
    }

    public bool CanPause => State == BackupState.Active;

    public bool CanResume => State == BackupState.Paused;

    public bool CanStop => State == BackupState.Active || State == BackupState.Paused;
}
