using System.Collections.ObjectModel;
using Avalonia.Threading;
using EasySave.Core.Entities;
using EasySave.Core.Enums;
using EasySave.Gui.Services;

namespace EasySave.Gui.ViewModels;

/// <summary>
/// ViewModel for the create/edit tab: allows creating a new job
/// or editing an existing one through a form.
/// </summary>
public sealed class CreateEditJobViewModel : ViewModelBase
{
    private readonly IConfigurationHolder _configHolder;
    private readonly ILocalizationProvider _localization;
    private readonly IFolderPickerService _folderPicker;
    private JobItemViewModel? _selectedExistingJob;
    private string _name = string.Empty;
    private string _sourcePath = string.Empty;
    private string _targetPath = string.Empty;
    private int _selectedTypeIndex;
    private string _excludeExtensions = string.Empty;
    private string _excludeDirectories = string.Empty;
    private string _statusText = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateEditJobViewModel"/> class.
    /// </summary>
    /// <param name="configHolder">Configuration holder providing current jobs and persistence.</param>
    /// <param name="localization">Localization provider for UI strings.</param>
    /// <param name="folderPicker">Service used to pick source and target folders.</param>
    public CreateEditJobViewModel(IConfigurationHolder configHolder, ILocalizationProvider localization, IFolderPickerService folderPicker)
    {
        _configHolder = configHolder;
        _localization = localization;
        _folderPicker = folderPicker;
        ExistingJobs = new ObservableCollection<JobItemViewModel>();
        _configHolder.ConfigurationChanged += (_, _) => Dispatcher.UIThread.Post(RefreshExistingJobs);
        _localization.CultureChanged += (_, _) => RaiseLocalizedProperties();
        RefreshExistingJobs();
    }

    private void RaiseLocalizedProperties()
    {
        RaisePropertyChanged(nameof(ExistingJobLabel));
        RaisePropertyChanged(nameof(NewJobButtonText));
        RaisePropertyChanged(nameof(EditHintText));
        RaisePropertyChanged(nameof(LabelName));
        RaisePropertyChanged(nameof(LabelSource));
        RaisePropertyChanged(nameof(LabelTarget));
        RaisePropertyChanged(nameof(LabelType));
        RaisePropertyChanged(nameof(LabelExcludeExtensions));
        RaisePropertyChanged(nameof(LabelExcludeDirs));
        RaisePropertyChanged(nameof(SaveButtonText));
        RaisePropertyChanged(nameof(FullBackupLabel));
        RaisePropertyChanged(nameof(DifferentialBackupLabel));
        RaisePropertyChanged(nameof(BrowseButtonText));
    }

    public string BrowseButtonText => _localization.GetString("Gui_Browse");

    public ObservableCollection<JobItemViewModel> ExistingJobs { get; }

    public JobItemViewModel? SelectedExistingJob
    {
        get => _selectedExistingJob;
        set
        {
            if (SetProperty(ref _selectedExistingJob, value))
                LoadJobIntoForm();
        }
    }

    public string Name { get => _name; set => SetProperty(ref _name, value ?? string.Empty); }
    public string SourcePath { get => _sourcePath; set => SetProperty(ref _sourcePath, value ?? string.Empty); }
    public string TargetPath { get => _targetPath; set => SetProperty(ref _targetPath, value ?? string.Empty); }
    public int SelectedTypeIndex { get => _selectedTypeIndex; set => SetProperty(ref _selectedTypeIndex, value); }
    public string ExcludeExtensions { get => _excludeExtensions; set => SetProperty(ref _excludeExtensions, value ?? string.Empty); }
    public string ExcludeDirectories { get => _excludeDirectories; set => SetProperty(ref _excludeDirectories, value ?? string.Empty); }
    public string StatusText { get => _statusText; set => SetProperty(ref _statusText, value); }

    public bool IsEditing => SelectedExistingJob != null;
    public string ExistingJobLabel => _localization.GetString("Gui_ExistingJob");
    public string NewJobButtonText => _localization.GetString("Gui_NewJob");
    public string EditHintText => _localization.GetString("Gui_EditHint");
    public string LabelName => _localization.GetString("Gui_LabelName");
    public string LabelSource => _localization.GetString("Gui_LabelSource");
    public string LabelTarget => _localization.GetString("Gui_LabelTarget");
    public string LabelType => _localization.GetString("Gui_LabelType");
    public string LabelExcludeExtensions => _localization.GetString("Gui_LabelExcludeExtensions");
    public string LabelExcludeDirs => _localization.GetString("Gui_LabelExcludeDirs");
    public string SaveButtonText => _localization.GetString("Gui_Save");
    public string FullBackupLabel => _localization.GetString("FullBackup");
    public string DifferentialBackupLabel => _localization.GetString("DifferentialBackup");

    internal void RefreshExistingJobs()
    {
        ExistingJobs.Clear();
        foreach (BackupJob? j in _configHolder.Current.Jobs.OrderBy(x => x.Id))
            ExistingJobs.Add(new JobItemViewModel(j.Id, j.Name, j.Type));
    }

    public void NewJob(object _)
    {
        SelectedExistingJob = null;
        Name = string.Empty;
        SourcePath = string.Empty;
        TargetPath = string.Empty;
        SelectedTypeIndex = 0;
        ExcludeExtensions = string.Empty;
        ExcludeDirectories = string.Empty;
        StatusText = _localization.GetString("Gui_EditModeCreate");
    }

    public bool CanNewJob(object _) => true;

    public async void PickSourceFolder(object _)
    {
        string? path = await _folderPicker.PickFolderAsync(_localization.GetString("Gui_PickSourceFolder")).ConfigureAwait(true);
        if (!string.IsNullOrEmpty(path))
            SourcePath = path;
    }

    public bool CanPickSourceFolder(object _) => true;

    public async void PickTargetFolder(object _)
    {
        string? path = await _folderPicker.PickFolderAsync(_localization.GetString("Gui_PickTargetFolder")).ConfigureAwait(true);
        if (!string.IsNullOrEmpty(path))
            TargetPath = path;
    }

    public bool CanPickTargetFolder(object _) => true;

    private void LoadJobIntoForm()
    {
        if (SelectedExistingJob == null)
            return;
        BackupJob? job = _configHolder.Current.Jobs.FirstOrDefault(j => j.Id == SelectedExistingJob.Id);
        if (job == null)
            return;
        Name = job.Name;
        SourcePath = job.SourcePath;
        TargetPath = job.TargetPath;
        SelectedTypeIndex = job.Type == BackupType.Differential ? 1 : 0;
        ExcludeExtensions = string.Join(",", job.ExcludeExtensions ?? Array.Empty<string>());
        ExcludeDirectories = string.Join(",", job.ExcludeDirectoryNames ?? Array.Empty<string>());
        StatusText = _localization.GetString("Gui_EditModeEdit", job.Id);
    }

    public async void SaveJob(object _)
    {
        string? name = Name.Trim();
        string? source = SourcePath.Trim();
        string? target = TargetPath.Trim();
        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(source) || string.IsNullOrEmpty(target))
        {
            StatusText = _localization.GetString("Gui_RequiredFields");
            return;
        }

        BackupType type = SelectedTypeIndex == 1 ? BackupType.Differential : BackupType.Full;
        List<string>? excludeExt = ParseList(ExcludeExtensions);
        List<string>? excludeDirs = ParseList(ExcludeDirectories);
        BackupConfiguration? config = _configHolder.Current;
        List<BackupJob>? jobs = config.Jobs.ToList();

        if (SelectedExistingJob == null)
        {
            int newId = jobs.Count > 0 ? jobs.Max(j => j.Id) + 1 : 1;
            jobs.Add(new BackupJob
            {
                Id = newId,
                Name = name,
                SourcePath = source,
                TargetPath = target,
                Type = type,
                ExcludeExtensions = excludeExt,
                ExcludeDirectoryNames = excludeDirs
            });
            BackupConfiguration newConfig = new()
            {
                LogAndStateDirectory = config.LogAndStateDirectory,
                LogFileFormat = config.LogFileFormat,
                LogDestination = config.LogDestination,
                CentralizedLogServerAddress = config.CentralizedLogServerAddress,
                Jobs = jobs,
                LastFullBackupUtcByJobId = config.LastFullBackupUtcByJobId,
                EncryptExtensions = config.EncryptExtensions,
                PriorityExtensions = config.PriorityExtensions,
                EncryptionKeyPath = config.EncryptionKeyPath,
                BusinessSoftwareProcessName = config.BusinessSoftwareProcessName,
                LargeFileThresholdKb = config.LargeFileThresholdKb
            };
            await _configHolder.SaveAsync(newConfig, CancellationToken.None).ConfigureAwait(true);
            StatusText = _localization.GetString("JobCreated", newId);
            NewJob(null!);
            return;
        }

        int editId = SelectedExistingJob.Id;
        BackupJob updated = new()
        {
            Id = editId,
            Name = name,
            SourcePath = source,
            TargetPath = target,
            Type = type,
            ExcludeExtensions = excludeExt,
            ExcludeDirectoryNames = excludeDirs
        };
        List<BackupJob> replaced = jobs.Select(j => j.Id == editId ? updated : j).ToList();
        BackupConfiguration updatedConfig = new()
        {
            LogAndStateDirectory = config.LogAndStateDirectory,
            LogFileFormat = config.LogFileFormat,
            LogDestination = config.LogDestination,
            CentralizedLogServerAddress = config.CentralizedLogServerAddress,
            Jobs = replaced,
            LastFullBackupUtcByJobId = config.LastFullBackupUtcByJobId,
            EncryptExtensions = config.EncryptExtensions,
            PriorityExtensions = config.PriorityExtensions,
            EncryptionKeyPath = config.EncryptionKeyPath,
            BusinessSoftwareProcessName = config.BusinessSoftwareProcessName,
            LargeFileThresholdKb = config.LargeFileThresholdKb
        };
        await _configHolder.SaveAsync(updatedConfig, CancellationToken.None).ConfigureAwait(true);
        StatusText = _localization.GetString("TuiJobUpdated", editId);
    }

    public bool CanSaveJob(object _) => true;

    private static List<string> ParseList(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return new List<string>();
        return raw
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
