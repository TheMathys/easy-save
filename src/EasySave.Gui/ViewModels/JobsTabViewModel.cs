using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
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
    private JobItemViewModel? _selectedJob;
    private string _jobDetailsText = string.Empty;
    private string _statusText = string.Empty;
    private bool _isRunning;

    /// <summary>
    /// Initializes a new instance of the <see cref="JobsTabViewModel"/> class.
    /// </summary>
    /// <param name="configHolder">Configuration holder providing the current jobs.</param>
    /// <param name="backupExecutor">Executor used to run backup jobs.</param>
    /// <param name="localization">Localization provider for UI strings.</param>
    public JobsTabViewModel(
        IConfigurationHolder configHolder,
        IBackupExecutor backupExecutor,
        ILocalizationProvider localization)
    {
        _configHolder = configHolder;
        _backupExecutor = backupExecutor;
        _localization = localization;
        Jobs = new ObservableCollection<JobItemViewModel>();
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

    public JobItemViewModel? SelectedJob
    {
        get => _selectedJob;
        set
        {
        if (SetProperty(ref _selectedJob, value))
        {
            UpdateDetails();
            RaisePropertyChanged(nameof(CanRunSelected));
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
        set => SetProperty(ref _isRunning, value);
    }

    public string JobsListTitle => _localization.GetString("Gui_JobsListTitle");
    public string DetailsTitle => _localization.GetString("Gui_DetailsTitle");
    public string RefreshButtonText => _localization.GetString("Gui_Refresh");
    public string RunSelectedButtonText => _localization.GetString("Gui_RunSelected");
    public string JobsHintText => _localization.GetString("Gui_JobsHint");

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

    public bool CanRunSelected => SelectedJob != null && !IsRunning;

    public async void RunSelected(object _)
    {
        if (SelectedJob == null)
        {
            StatusText = _localization.GetString("Gui_SelectJobFirst");
            return;
        }

        IsRunning = true;
        StatusText = _localization.GetString("Gui_JobRunning", SelectedJob.Id);
        try
        {
            await _backupExecutor.ExecuteAsync(
                new[] { SelectedJob.Id },
                progress: null,
                CancellationToken.None).ConfigureAwait(true);
            StatusText = _localization.GetString("Gui_JobSuccess", SelectedJob.Id);
        }
        catch (Exception ex)
        {
            StatusText = _localization.GetString("Gui_JobError", ex.Message);
        }
        finally
        {
            IsRunning = false;
            RaisePropertyChanged(nameof(CanRefresh));
        }
    }

    internal void RefreshJobsFromConfig()
    {
        var config = _configHolder.Current;
        Jobs.Clear();
        foreach (var j in config.Jobs.OrderBy(x => x.Id))
            Jobs.Add(new JobItemViewModel(j.Id, j.Name, j.Type));

        if (Jobs.Count > 0 && SelectedJob == null)
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
