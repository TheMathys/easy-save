using System;
using System.IO;

namespace EasySave.Gui;

/// <summary>
/// Persists the GUI's chosen base path (config/state/logs directory) so it is restored on next launch.
/// Priority at startup: 1) EASYSAVE_BASE_PATH env, 2) saved path file, 3) AppContext.BaseDirectory.
/// </summary>
public static class GuiBasePathStorage
{
    private const string SubFolder = "EasySave";
    private const string FileName = "gui-basepath.txt";

    /// <summary>
    /// Gets the directory where the saved base path file is stored (e.g. %LocalAppData%\EasySave).
    /// </summary>
    private static string GetStorageDirectory()
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, SubFolder);
    }

    /// <summary>
    /// Returns the base path to use at startup.
    /// Priority (GUI):
    /// 1) EASYSAVE_BASE_PATH (if defined and exists)
    /// 2) Last base path chosen in the GUI (saved file), if the directory still exists
    /// 3) Directory of the executable (AppContext.BaseDirectory)
    /// </summary>
    public static string GetBasePath()
    {
        string? envBasePath = Environment.GetEnvironmentVariable("EASYSAVE_BASE_PATH");
        if (!string.IsNullOrWhiteSpace(envBasePath))
        {
            string p = Path.GetFullPath(envBasePath.Trim());
            if (Directory.Exists(p))
                return p;
        }

        string filePath = Path.Combine(GetStorageDirectory(), FileName);
        if (File.Exists(filePath))
        {
            try
            {
                string? saved = File.ReadAllText(filePath).Trim();
                if (!string.IsNullOrWhiteSpace(saved))
                {
                    string fullPath = Path.GetFullPath(saved);
                    if (Directory.Exists(fullPath))
                        return fullPath;
                }
            }
            catch
            {
                // Ignore read errors, fall back to default
            }
        }

        return Path.GetFullPath(AppContext.BaseDirectory);
    }

    /// <summary>
    /// Saves the given base path so the next launch uses it (unless EASYSAVE_BASE_PATH is set).
    /// Call this after the user successfully changes the folder in settings.
    /// </summary>
    public static void SaveBasePath(string basePath)
    {
        if (string.IsNullOrWhiteSpace(basePath))
            return;

        string dir = GetStorageDirectory();
        Directory.CreateDirectory(dir);
        string filePath = Path.Combine(dir, FileName);
        string fullPath = Path.GetFullPath(basePath.Trim());
        File.WriteAllText(filePath, fullPath);
    }
}
