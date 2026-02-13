using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace EasySave.Gui.Services;

/// <summary>
/// Default implementation of <see cref="IFilePickerService"/> using
/// Avalonia's <see cref="IStorageProvider"/> to display a native file picker dialog.
/// No file type filter is applied.
/// </summary>
public sealed class FilePickerService : IFilePickerService
{
    private Window? _owner;

    /// <inheritdoc />
    public void SetOwner(Window? window)
    {
        _owner = window;
    }

    /// <inheritdoc />
    public async Task<string?> PickFileAsync(string? title = null)
    {
        if (_owner == null)
            return null;

        IReadOnlyList<IStorageFile> files = await _owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title ?? "Select file",
            AllowMultiple = false
        }).ConfigureAwait(true);

        if (files.Count == 0)
            return null;

        return files[0].TryGetLocalPath();
    }
}
