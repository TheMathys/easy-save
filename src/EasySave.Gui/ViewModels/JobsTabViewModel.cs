using System.Collections.ObjectModel;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Threading;
using System;
using System.Threading.Tasks;
using EasySave.Core.Entities;
using EasySave.Core.Enums;
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
        _configHolder.ConfigurationChanged += (_, _) => RefreshJobsFromConfig();
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
    }

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
            }
        }
    }

    public bool CanStop => IsRunning;

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

        var config = _configHolder.Current;
        var newJobs = config.Jobs.Where(j => j.Id != SelectedJob.Id).ToList();
        var newLastFull = config.LastFullBackupUtcByJobId
            .Where(kv => kv.Key != SelectedJob.Id)
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        var updated = new BackupConfiguration
        {
            LogAndStateDirectory = config.LogAndStateDirectory,
            LogFileFormat = config.LogFileFormat,
            Jobs = newJobs,
            LastFullBackupUtcByJobId = newLastFull
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
            await _configHolder.ReloadAsync(CancellationToken.None).ConfigureAwait(true);
            StatusText = _localization.GetString("Gui_JobsRefreshed");
        }
        finally
        {
            IsRunning = false;
            RaisePropertyChanged(nameof(CanRefresh));
            RaisePropertyChanged(nameof(CanRunSelected));
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

        var ids = SelectedJobs.Select(j => j.Id).ToArray();

        // prepare cancellation
        _cts?.Dispose();
        _cts = new CancellationTokenSource();

        IsRunning = true;
        StatusText = _localization.GetString("Gui_JobRunning", string.Join(", ", ids));
        try
        {
            // Run the executor on the thread pool to keep UI responsive
            await Task.Run(async () => await _backupExecutor.ExecuteAsync(ids, progress: null, _cts.Token)).ConfigureAwait(true);

            if (_cts.IsCancellationRequested)
            {
                StatusText = _localization.GetString("Gui_JobCancelledByUser");
            }
            else
            {
                StatusText = _localization.GetString("Gui_JobSuccess", string.Join(", ", ids));
            }
        }
        catch (OperationCanceledException)
        {
            StatusText = _localization.GetString("Gui_JobCancelledByUser");
        }
        catch (Exception ex)
        {
            StatusText = _localization.GetString("Gui_JobError", ex.Message);
        }
        finally
        {
            IsRunning = false;
            _cts?.Dispose();
            _cts = null;
            RaisePropertyChanged(nameof(CanRefresh));
        }
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
        var config = _configHolder.Current;
        // preserve selected ids so we can re-select after reload
        var previouslySelectedIds = SelectedJobs.Select(x => x.Id).ToList();

        Jobs.Clear();
        foreach (var j in config.Jobs.OrderBy(x => x.Id))
            Jobs.Add(new JobItemViewModel(j.Id, j.Name, j.Type));

        // restore selection
        SelectedJobs.Clear();
        foreach (var id in previouslySelectedIds)
        {
            var item = Jobs.FirstOrDefault(x => x.Id == id);
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
            var stillSelected = Jobs.FirstOrDefault(x => x.Id == SelectedJob.Id);
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
        var config = _configHolder.Current;
        var job = config.Jobs.FirstOrDefault(j => j.Id == SelectedJob.Id);
        if (job == null)
            return;

        var typeStr = job.Type == BackupType.Differential
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
