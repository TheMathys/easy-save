using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace EasySave.Gui.Services;

/// <summary>
/// Default implementation of <see cref="IFolderPickerService"/> using
/// Avalonia's <see cref="IStorageProvider"/> to display a native folder picker dialog.
/// </summary>
public sealed class FolderPickerService : IFolderPickerService
{
    private Window? _owner;

    /// <inheritdoc />
    public void SetOwner(Window? window)
    {
        _owner = window;
    }

    /// <inheritdoc />
    public async Task<string?> PickFolderAsync(string? title = null)
    {
        if (_owner == null)
            return null;

        var folders = await _owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title ?? "Select folder",
            AllowMultiple = false
        }).ConfigureAwait(true);

        if (folders.Count == 0)
            return null;

        var path = folders[0].TryGetLocalPath();
        return path;
    }
}
