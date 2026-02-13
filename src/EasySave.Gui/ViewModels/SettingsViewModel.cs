using System.Threading;
using System.Threading.Tasks;
using EasySave.Core.Entities;
using EasySave.Core.Enums;
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
    private readonly EasySavePaths _paths;
    private int _logFormatIndex;
    private string _statusText = string.Empty;

    public SettingsViewModel(
        IConfigurationHolder configHolder,
        ILocalizationProvider localization,
        EasySavePaths paths)
    {
        _configHolder = configHolder;
        _localization = localization;
        _paths = paths;
        _configHolder.ConfigurationChanged += (_, _) => SyncFromConfig();
        _localization.CultureChanged += (_, _) => RaiseLocalizedProperties();
        SyncFromConfig();
    }

    private void RaiseLocalizedProperties()
    {
        RaisePropertyChanged(nameof(LabelBasePath));
        RaisePropertyChanged(nameof(LabelConfigPath));
        RaisePropertyChanged(nameof(LabelStatePath));
        RaisePropertyChanged(nameof(LabelLogDir));
        RaisePropertyChanged(nameof(LabelLogFormat));
        RaisePropertyChanged(nameof(SaveSettingsButtonText));
    }

    public string BasePath => _paths.BaseDirectory;
    public string ConfigPath => _paths.ConfigFilePath;
    public string StatePath => _paths.StateFilePath;
    public string LogDirectory => _paths.LogDirectory;

    public int LogFormatIndex
    {
        get => _logFormatIndex;
        set => SetProperty(ref _logFormatIndex, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public string LabelBasePath => _localization.GetString("Gui_LabelBasePath");
    public string LabelConfigPath => _localization.GetString("Gui_LabelConfigPath");
    public string LabelStatePath => _localization.GetString("Gui_LabelStatePath");
    public string LabelLogDir => _localization.GetString("Gui_LabelLogDir");
    public string LabelLogFormat => _localization.GetString("Gui_LabelLogFormat");
    public string SaveSettingsButtonText => _localization.GetString("Gui_SaveSettings");

    internal void SyncFromConfig()
    {
        LogFormatIndex = _configHolder.Current.LogFileFormat == LogFileFormat.Xml ? 1 : 0;
    }

    public async void SaveSettings(object _)
    {
        var format = LogFormatIndex == 1 ? LogFileFormat.Xml : LogFileFormat.Json;
        var config = _configHolder.Current;
        var updated = new BackupConfiguration
        {
            LogAndStateDirectory = config.LogAndStateDirectory,
            LogFileFormat = format,
            Jobs = config.Jobs,
            LastFullBackupUtcByJobId = config.LastFullBackupUtcByJobId
        };
        await _configHolder.SaveAsync(updated, CancellationToken.None).ConfigureAwait(true);
        StatusText = _localization.GetString("Gui_SettingsSaved", format.ToString());
    }

    public bool CanSaveSettings(object _) => true;
}
