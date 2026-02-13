using Avalonia.Controls;

namespace EasySave.Gui.Services;

/// <summary>
/// Service abstraction that opens a file picker dialog (MVVM-friendly and testable).
/// </summary>
public interface IFilePickerService
{
    /// <summary>
    /// Sets the owner window for the dialog (should be called once at startup).
    /// </summary>
    /// <param name="window">Window that will own file picker dialogs.</param>
    void SetOwner(Window? window);

    /// <summary>
    /// Shows a file picker dialog. No extension filter is applied (all files can be selected).
    /// </summary>
    /// <param name="title">Optional dialog title.</param>
    /// <returns>The selected file path, or <c>null</c> if the dialog is cancelled.</returns>
    Task<string?> PickFileAsync(string? title = null);
}
