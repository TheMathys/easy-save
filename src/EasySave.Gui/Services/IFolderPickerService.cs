using Avalonia.Controls;

namespace EasySave.Gui.Services;

/// <summary>
/// Service abstraction that opens a folder picker dialog (MVVM-friendly and testable).
/// </summary>
public interface IFolderPickerService
{
    /// <summary>
    /// Sets the owner window for the dialog (should be called once at startup).
    /// </summary>
    /// <param name="window">Window that will own folder picker dialogs.</param>
    void SetOwner(Window? window);

    /// <summary>
    /// Shows a folder picker dialog.
    /// </summary>
    /// <param name="title">Optional dialog title.</param>
    /// <returns>The selected folder path, or <c>null</c> if the dialog is cancelled.</returns>
    Task<string?> PickFolderAsync(string? title = null);
}
