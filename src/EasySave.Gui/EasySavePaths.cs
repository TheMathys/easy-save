using System.IO;

namespace EasySave.Gui;

/// <summary>
/// Provides strongly-typed paths used by the EasySave GUI
/// for configuration, state and log files.
/// </summary>
public sealed class EasySavePaths
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EasySavePaths"/> class.
    /// </summary>
    /// <param name="baseDirectory">
    /// Base directory where configuration, state file and log files are stored.
    /// </param>
    public EasySavePaths(string baseDirectory)
    {
        BaseDirectory = Path.GetFullPath(baseDirectory ?? string.Empty);
        ConfigFilePath = Path.Combine(BaseDirectory, "backup-config.json");
        StateFilePath = Path.Combine(BaseDirectory, "state.json");
        LogDirectory = BaseDirectory;
    }

    /// <summary>
    /// Directory containing configuration, state and log files.
    /// </summary>
    public string BaseDirectory { get; }

    /// <summary>
    /// Full path to <c>backup-config.json</c>.
    /// </summary>
    public string ConfigFilePath { get; }

    /// <summary>
    /// Full path to <c>state.json</c>.
    /// </summary>
    public string StateFilePath { get; }

    /// <summary>
    /// Directory where daily log files are stored.
    /// </summary>
    public string LogDirectory { get; }
}
