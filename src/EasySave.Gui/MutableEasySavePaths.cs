using System.IO;

namespace EasySave.Gui;

/// <summary>
/// Mutable version of <see cref="EasySavePaths"/> allowing to change the base directory at runtime.
/// When changing the path, existing configuration, state and log files are copied to the new
/// location so that no data is lost.
/// </summary>
public sealed class MutableEasySavePaths
{
    private readonly object _lock = new();
    private string _baseDirectory;

    /// <summary>
    /// Initializes a new instance with the given base directory.
    /// </summary>
    public MutableEasySavePaths(string baseDirectory)
    {
        _baseDirectory = Path.GetFullPath(baseDirectory ?? string.Empty);
    }

    /// <summary>
    /// Directory containing configuration, state and log files.
    /// </summary>
    public string BaseDirectory
    {
        get { lock (_lock) return _baseDirectory; }
    }

    /// <summary>
    /// Full path to <c>backup-config.json</c>.
    /// </summary>
    public string ConfigFilePath => Path.Combine(BaseDirectory, "backup-config.json");

    /// <summary>
    /// Full path to <c>state.json</c>.
    /// </summary>
    public string StateFilePath => Path.Combine(BaseDirectory, "state.json");

    /// <summary>
    /// Directory where daily log files are stored.
    /// </summary>
    public string LogDirectory => BaseDirectory;

    /// <summary>
    /// Raised when the base directory has been changed (after files have been copied).
    /// </summary>
    public event EventHandler? PathsChanged;

    /// <summary>
    /// Changes the base directory to the new path. Copies existing backup-config.json,
    /// state.json and all daily log files (*.json, *.xml) from the current location
    /// to the new one so that no data is lost, then switches to the new path.
    /// </summary>
    /// <param name="newBaseDirectory">The new base directory (will be created if needed).</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>True if the path was changed, false if the new path is the same as the current one.</returns>
    public async Task<bool> SetBaseDirectoryAsync(string newBaseDirectory, CancellationToken cancellationToken = default)
    {
        string newPath = Path.GetFullPath(newBaseDirectory?.Trim() ?? string.Empty);
        string oldPath;
        lock (_lock)
        {
            oldPath = _baseDirectory;
            if (string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        Directory.CreateDirectory(newPath);

        await CopyFileIfExistsAsync(Path.Combine(oldPath, "backup-config.json"), Path.Combine(newPath, "backup-config.json"), cancellationToken).ConfigureAwait(false);
        await CopyFileIfExistsAsync(Path.Combine(oldPath, "state.json"), Path.Combine(newPath, "state.json"), cancellationToken).ConfigureAwait(false);

        if (Directory.Exists(oldPath))
        {
            foreach (string file in Directory.EnumerateFiles(oldPath, "*.json", SearchOption.TopDirectoryOnly))
            {
                string fileName = Path.GetFileName(file);
                if (fileName != "backup-config.json" && fileName != "state.json")
                    await CopyFileIfExistsAsync(file, Path.Combine(newPath, fileName), cancellationToken).ConfigureAwait(false);
            }
            foreach (string file in Directory.EnumerateFiles(oldPath, "*.xml", SearchOption.TopDirectoryOnly))
                await CopyFileIfExistsAsync(file, Path.Combine(newPath, Path.GetFileName(file)), cancellationToken).ConfigureAwait(false);
        }

        lock (_lock)
            _baseDirectory = newPath;

        PathsChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    private static async Task CopyFileIfExistsAsync(string source, string destination, CancellationToken cancellationToken)
    {
        if (!File.Exists(source))
            return;
        await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            File.Copy(source, destination, overwrite: true);
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Returns a snapshot with the same shape as <see cref="EasySavePaths"/> for backward compatibility.
    /// </summary>
    public EasySavePaths Snapshot() => new EasySavePaths(BaseDirectory);
}
