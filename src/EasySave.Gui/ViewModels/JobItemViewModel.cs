using EasySave.Core.Enums;

namespace EasySave.Gui.ViewModels;

/// <summary>
/// Lightweight view model used to display a backup job in lists
/// (separating presentation from domain entities).
/// </summary>
public sealed class JobItemViewModel : ViewModelBase
{
    private string _displayText = string.Empty;

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

    /// <inheritdoc />
    public override string ToString() => DisplayText;
}
