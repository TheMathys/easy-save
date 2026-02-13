using EasySave.Gui.Services;

namespace EasySave.Gui.ViewModels;

/// <summary>
/// ViewModel for the main window: header texts and tab view models.
/// Contains orchestration logic only, no business rules.
/// </summary>
public sealed class MainWindowViewModel : ViewModelBase
{
    private readonly ILocalizationProvider _localization;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindowViewModel"/> class.
    /// </summary>
    /// <param name="localization">Localization provider used to retrieve UI strings.</param>
    /// <param name="jobsTab">ViewModel for the jobs tab.</param>
    /// <param name="createEditTab">ViewModel for the create/edit tab.</param>
    /// <param name="settingsTab">ViewModel for the settings tab.</param>
    public MainWindowViewModel(
        ILocalizationProvider localization,
        JobsTabViewModel jobsTab,
        CreateEditJobViewModel createEditTab,
        SettingsViewModel settingsTab)
    {
        _localization = localization;
        JobsTab = jobsTab;
        CreateEditTab = createEditTab;
        SettingsTab = settingsTab;
        _localization.CultureChanged += (_, _) => RaiseHeaderProperties();
    }

    /// <summary>ViewModel for the jobs tab.</summary>
    public JobsTabViewModel JobsTab { get; }
    /// <summary>ViewModel for the create/edit tab.</summary>
    public CreateEditJobViewModel CreateEditTab { get; }
    /// <summary>ViewModel for the settings tab.</summary>
    public SettingsViewModel SettingsTab { get; }

    public string WindowTitle => _localization.GetString("Gui_WindowTitle");
    public string HeaderTitle => _localization.GetString("Gui_HeaderTitle");
    public string TabJobs => _localization.GetString("Gui_TabJobs");
    public string TabCreateEdit => _localization.GetString("Gui_TabCreateEdit");
    public string TabSettings => _localization.GetString("Gui_TabSettings");

    private void RaiseHeaderProperties()
    {
        RaisePropertyChanged(nameof(WindowTitle));
        RaisePropertyChanged(nameof(HeaderTitle));
        RaisePropertyChanged(nameof(TabJobs));
        RaisePropertyChanged(nameof(TabCreateEdit));
        RaisePropertyChanged(nameof(TabSettings));
    }
}
