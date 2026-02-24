using EasySave.Core.Enums;

namespace EasySave.Gui.ViewModels;

/// <summary>
/// Lightweight view model used to display a backup job in lists
/// (separating presentation from domain entities).
/// </summary>
public sealed class JobItemViewModel : ViewModelBase
{
    private string _displayText = string.Empty;
    private BackupState _state = BackupState.Inactive;
    private string _stateText = string.Empty;
    private string _stateBadgeBackground = "#E5E7EB";
    private string _stateBadgeForeground = "#374151";

    /// <summary>Identifier of the backup job.</summary>
    public int Id { get; }
    /// <summary>Name of the backup job.</summary>
    public string Name { get; }
    /// <summary>Backup type (Full or Differential).</summary>
    public BackupType Type { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="JobItemViewModel"/> class.
    /// </summary>
    /// <param name="id">Job identifier.</param>
    /// <param name="name">Job name.</param>
    /// <param name="type">Job backup type.</param>
    public JobItemViewModel(int id, string name, BackupType type)
    {
        Id = id;
        Name = name;
        Type = type;
        _displayText = $"{Id} - {Name} ({Type})";
    }

    public string DisplayText
    {
        get => _displayText;
        set => SetProperty(ref _displayText, value);
    }

    public BackupState State
    {
        get => _state;
        set => SetProperty(ref _state, value);
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

    /// <inheritdoc />
    public override string ToString() => DisplayText;
}
