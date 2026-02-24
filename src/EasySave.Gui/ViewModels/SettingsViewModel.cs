using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;
using Avalonia.Threading;
using EasySave.Core.Entities;
using EasySave.Core.Enums;
using EasySave.Core.Interfaces;
using EasySave.Gui.Services;

namespace EasySave.Gui.ViewModels;

/// <summary>
/// ViewModel for the settings tab: shows the important paths
/// and allows changing the log format.
/// </summary>
public sealed class SettingsViewModel : ViewModelBase
{
    private readonly IConfigurationHolder _configHolder;
    private readonly ILocalizationProvider _localization;
    private readonly IAudiodescription _audioDescription;
    private readonly EasySavePaths _paths;
    private readonly IFilePickerService _filePicker;
    private readonly IBusinessSoftwareDetector _businessSoftwareDetector;
    private int _logFormatIndex;
    private int _logDestinationIndex;
    private string _centralizedLogServerAddress = string.Empty;
    private string _statusText = string.Empty;
    private string _encryptExtensionsText = string.Empty;
    private string _encryptionKeyPath = string.Empty;
    private string _businessSoftwareProcessName = string.Empty;
    private string _selectedProcessChoice = string.Empty;
    private bool _audioDescriptionEnabled;
    private double _volumeValue = 50;

    private string _selectedLanguage = "Français";
    private bool _useDarkTheme;


    public SettingsViewModel(
        IConfigurationHolder configHolder,
        ILocalizationProvider localization,
        IAudiodescription audioDescription,
        EasySavePaths paths,
        IFilePickerService filePicker,
        IBusinessSoftwareDetector businessSoftwareDetector)
    {
        _configHolder = configHolder;
        _localization = localization;
        _audioDescription = audioDescription;
        _paths = paths;
        _filePicker = filePicker;
        _businessSoftwareDetector = businessSoftwareDetector;
        RunningProcessChoices = new ObservableCollection<string>();
        LanguageChoices = new ObservableCollection<string> { "Français", "English" };
        LogDestinationChoices = new ObservableCollection<string>();
        _configHolder.ConfigurationChanged += (_, _) => Dispatcher.UIThread.Post(SyncFromConfig);
        _localization.CultureChanged += (_, _) => RaiseLocalizedProperties();
        SyncFromConfig();
        RefreshRunningProcessesListAsync();
        UpdateLogDestinationChoices();
    }

    /// <summary>
    /// Whether the GUI should use the dark theme. Bound to a ToggleSwitch in settings.
    /// </summary>
    public bool UseDarkTheme
    {
        get => _useDarkTheme;
        set => SetProperty(ref _useDarkTheme, value);
    }

    private void UpdateLogDestinationChoices()
    {
        LogDestinationChoices.Clear();
        LogDestinationChoices.Add(_localization.GetString("Gui_LogDestination_Local"));
        LogDestinationChoices.Add(_localization.GetString("Gui_LogDestination_Centralized"));
        LogDestinationChoices.Add(_localization.GetString("Gui_LogDestination_LocalAndCentralized"));
    }

    private void RaiseLocalizedProperties()
    {
        RaisePropertyChanged(nameof(LabelBasePath));
        RaisePropertyChanged(nameof(LabelConfigPath));
        RaisePropertyChanged(nameof(LabelStatePath));
        RaisePropertyChanged(nameof(LabelLogDir));
        RaisePropertyChanged(nameof(LabelLogFormat));
        RaisePropertyChanged(nameof(LabelLogDestination));
        RaisePropertyChanged(nameof(LabelCentralizedLogServer));
        RaisePropertyChanged(nameof(LabelEncryptExtensions));
        RaisePropertyChanged(nameof(LabelEncryptionKeyPath));
        RaisePropertyChanged(nameof(LabelBusinessSoftware));
        RaisePropertyChanged(nameof(LabelAudioDescription));
        RaisePropertyChanged(nameof(LabelAudioDescriptionVolume));
        RaisePropertyChanged(nameof(LabelLangueSelection));

        RaisePropertyChanged(nameof(RefreshProcessListButtonText));
        RaisePropertyChanged(nameof(BrowseEncryptionKeyButtonText));
        RaisePropertyChanged(nameof(SaveSettingsButtonText));
        UpdateLogDestinationChoices();
    }

    /// <summary>Localized labels for log destination: Local, Centralized, LocalAndCentralized.</summary>
    public ObservableCollection<string> LogDestinationChoices { get; }

    public string BasePath => _paths.BaseDirectory;
    public string ConfigPath => _paths.ConfigFilePath;
    public string StatePath => _paths.StateFilePath;
    public string LogDirectory => _paths.LogDirectory;

    public int LogFormatIndex
    {
        get => _logFormatIndex;
        set => SetProperty(ref _logFormatIndex, value);
    }

    /// <summary>Index for log destination: 0=Local, 1=Centralized, 2=LocalAndCentralized.</summary>
    public int LogDestinationIndex
    {
        get => _logDestinationIndex;
        set
        {
            if (!SetProperty(ref _logDestinationIndex, value))
                return;
            RaisePropertyChanged(nameof(IsCentralizedLogServerAddressVisible));
        }
    }

    public string CentralizedLogServerAddress
    {
        get => _centralizedLogServerAddress;
        set => SetProperty(ref _centralizedLogServerAddress, value ?? string.Empty);
    }

    /// <summary>True when destination is Centralized or LocalAndCentralized (show server address field).</summary>
    public bool IsCentralizedLogServerAddressVisible =>
        _logDestinationIndex == (int)LogDestination.Centralized || _logDestinationIndex == (int)LogDestination.LocalAndCentralized;

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    // Localization keys used by JobsTabViewModel
    public string DeleteButtonText => _localization.GetString("Gui_Delete");
    public string ConfirmDeleteMessage => _localization.GetString("Gui_ConfirmDeleteMessage");

    public string LabelBasePath => _localization.GetString("Gui_LabelBasePath");
    public string LabelConfigPath => _localization.GetString("Gui_LabelConfigPath");
    public string LabelStatePath => _localization.GetString("Gui_LabelStatePath");
    public string LabelLogDir => _localization.GetString("Gui_LabelLogDir");
    public string LabelLogFormat => _localization.GetString("Gui_LabelLogFormat");
    public string LabelLogDestination => _localization.GetString("Gui_LabelLogDestination");
    public string LabelCentralizedLogServer => _localization.GetString("Gui_LabelCentralizedLogServer");
    public string LabelEncryptExtensions => _localization.GetString("Gui_LabelEncryptExtensions");
    public string LabelEncryptionKeyPath => _localization.GetString("Gui_LabelEncryptionKeyPath");
    public string LabelBusinessSoftware => _localization.GetString("Gui_LabelBusinessSoftware");
    public string LabelAudioDescription => _localization.GetString("Gui_LabelAudioDescription");
    public string LabelAudioDescriptionVolume => _localization.GetString("Gui_LabelAudioDescriptionVolume");
    public string LabelLangueSelection => _localization.GetString("Gui_LabelLangueSelection");
    public string RefreshProcessListButtonText => _localization.GetString("Gui_RefreshProcessList");
    public string BrowseEncryptionKeyButtonText => _localization.GetString("Gui_BrowseEncryptionKey");
    public string SaveSettingsButtonText => _localization.GetString("Gui_SaveSettings");
    public string DeleteJobLabel => _localization.GetString("Gui_DeleteJobLabel");

    public string EncryptExtensionsText
    {
        get => _encryptExtensionsText;
        set => SetProperty(ref _encryptExtensionsText, value ?? string.Empty);
    }

    public string EncryptionKeyPath
    {
        get => _encryptionKeyPath;
        set => SetProperty(ref _encryptionKeyPath, value ?? string.Empty);
    }

    public string BusinessSoftwareProcessName
    {
        get => _businessSoftwareProcessName;
        set => SetProperty(ref _businessSoftwareProcessName, value ?? string.Empty);
    }

    /// <summary>List of choices for the business software ComboBox: "(None)" + running process names.</summary>
    public ObservableCollection<string> RunningProcessChoices { get; }

    /// <summary>List of available languages.</summary>
    public ObservableCollection<string> LanguageChoices { get; }

    public string SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (!SetProperty(ref _selectedLanguage, value ?? "Français"))
                return;

            // Change the culture based on the selected language
            string cultureCode = value switch
            {
                "Deutch" => "de",
                "English" => "en",
                "Français" => "fr",
                _ => "fr"
            };

            _localization.SetCulture(cultureCode);
        }
    }

    public string SelectedProcessChoice
    {
        get => _selectedProcessChoice;
        set
        {
            if (!SetProperty(ref _selectedProcessChoice, value ?? string.Empty))
                return;
            string noneLabel = _localization.GetString("Gui_NoBusinessSoftware");
            _businessSoftwareProcessName = (value == noneLabel || string.IsNullOrWhiteSpace(value)) ? string.Empty : value.Trim();
            RaisePropertyChanged(nameof(BusinessSoftwareProcessName));
        }
    }

    /// <summary>
    /// Gets or sets whether audio description is enabled.
    /// When enabled, starts the audio description service; when disabled, stops it.
    /// </summary>
    public bool AudioDescriptionEnabled
    {
        get => _audioDescriptionEnabled;
        set
        {
            if (!SetProperty(ref _audioDescriptionEnabled, value))
                return;
            _audioDescription.ServiceStatement(value);
        }
    }

    /// <summary>
    /// Gets or sets the volume level for audio description (0-100).
    /// Changes are immediately applied to the audio description service.
    /// </summary>
    public double VolumeValue
    {
        get => _volumeValue;
        set
        {
            if (!SetProperty(ref _volumeValue, value))
                return;
            _audioDescription.SetVolume((int)value);
        }
    }

    internal void SyncFromConfig()
    {
        BackupConfiguration c = _configHolder.Current;
        LogFormatIndex = c.LogFileFormat == LogFileFormat.Xml ? 1 : 0;
        LogDestinationIndex = (int)c.LogDestination;
        CentralizedLogServerAddress = c.CentralizedLogServerAddress ?? string.Empty;
        EncryptExtensionsText = c.EncryptExtensions?.Count > 0 ? string.Join(", ", c.EncryptExtensions) : string.Empty;
        EncryptionKeyPath = c.EncryptionKeyPath ?? string.Empty;
        BusinessSoftwareProcessName = c.BusinessSoftwareProcessName ?? string.Empty;
        UseDarkTheme = c.UseDarkTheme;
        RefreshRunningProcessesListAsync();
        ApplySelectedProcessFromConfig();
    }

    private void ApplySelectedProcessFromConfig()
    {
        string noneLabel = _localization.GetString("Gui_NoBusinessSoftware");
        if (string.IsNullOrWhiteSpace(_businessSoftwareProcessName))
        {
            _selectedProcessChoice = noneLabel;
            RaisePropertyChanged(nameof(SelectedProcessChoice));
            return;
        }
        string? match = RunningProcessChoices.FirstOrDefault(s => string.Equals(s, _businessSoftwareProcessName, StringComparison.OrdinalIgnoreCase));
        if (match != null)
            _selectedProcessChoice = match;
        else
            _selectedProcessChoice = _businessSoftwareProcessName;
        RaisePropertyChanged(nameof(SelectedProcessChoice));
    }

    /// <summary>
    /// Loads the process list on a background thread to avoid blocking the UI (GetProcesses is heavy).
    /// </summary>
    private void RefreshRunningProcessesListAsync()
    {
        string currentProcessName = _businessSoftwareProcessName;
        string noneLabel = _localization.GetString("Gui_NoBusinessSoftware");
        _ = Task.Run(() => _businessSoftwareDetector.GetRunningProcessNames())
            .ContinueWith(t =>
            {
                if (!t.IsCompletedSuccessfully || t.Result == null) return;
                var names = t.Result;
                var list = new List<string> { noneLabel };
                foreach (string name in names)
                    list.Add(name);
                if (!string.IsNullOrWhiteSpace(currentProcessName) && !names.Contains(currentProcessName, StringComparer.OrdinalIgnoreCase))
                    list.Insert(1, currentProcessName);
                Dispatcher.UIThread.Post(() =>
                {
                    RunningProcessChoices.Clear();
                    foreach (string item in list)
                        RunningProcessChoices.Add(item);
                    ApplySelectedProcessFromConfig();
                });
            }, TaskScheduler.Default);
    }

    public void RefreshRunningProcesses(object _) => RefreshRunningProcessesListAsync();

    public bool CanRefreshRunningProcesses(object _) => true;

    public async void SaveSettings(object _)
    {
        LogFileFormat format = LogFormatIndex == 1 ? LogFileFormat.Xml : LogFileFormat.Json;
        LogDestination destination = (LogDestination)Math.Clamp(LogDestinationIndex, 0, 2);
        BackupConfiguration config = _configHolder.Current;
        List<string> extensions = new List<string>();
        foreach (string part in (EncryptExtensionsText ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string ext = part.Trim();
            if (ext.Length > 0)
            {
                if (ext[0] != '.') ext = "." + ext;
                extensions.Add(ext);
            }
        }
        BackupConfiguration updated = new BackupConfiguration
        {
            LogAndStateDirectory = config.LogAndStateDirectory,
            LogFileFormat = format,
            LogDestination = destination,
            CentralizedLogServerAddress = string.IsNullOrWhiteSpace(CentralizedLogServerAddress) ? null : CentralizedLogServerAddress.Trim(),
            Jobs = config.Jobs,
            LastFullBackupUtcByJobId = config.LastFullBackupUtcByJobId,
            EncryptExtensions = extensions,
            EncryptionKeyPath = string.IsNullOrWhiteSpace(EncryptionKeyPath) ? null : EncryptionKeyPath.Trim(),
            BusinessSoftwareProcessName = string.IsNullOrWhiteSpace(BusinessSoftwareProcessName) ? null : BusinessSoftwareProcessName.Trim(),
            UseDarkTheme = UseDarkTheme
        };
        await _configHolder.SaveAsync(updated, CancellationToken.None).ConfigureAwait(true);
        StatusText = _localization.GetString("Gui_SettingsSaved", format.ToString());
    }

    public bool CanSaveSettings(object _) => true;

    public async void PickEncryptionKeyFile(object _)
    {
        string? path = await _filePicker.PickFileAsync(_localization.GetString("Gui_EncryptionKeyFileDialogTitle")).ConfigureAwait(true);
        if (path != null)
            EncryptionKeyPath = path;
    }

    public bool CanPickEncryptionKeyFile(object _) => true;
}
