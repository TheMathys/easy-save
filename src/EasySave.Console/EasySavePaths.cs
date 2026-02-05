using System.IO;

namespace EasySave.Console
{
    /// <summary>
    /// Paths used by EasySave for config, state and log files (for display in TUI "View paths").
    /// </summary>
    public sealed class EasySavePaths
    {
        public EasySavePaths(string baseDirectory)
        {
            BaseDirectory = Path.GetFullPath(baseDirectory ?? "");
            ConfigFilePath = Path.Combine(BaseDirectory, "backup-config.json");
            StateFilePath = Path.Combine(BaseDirectory, "state.json");
            LogDirectory = BaseDirectory;
        }

        /// <summary>Directory containing backup-config.json, state.json and daily log files.</summary>
        public string BaseDirectory { get; }

        /// <summary>Full path to backup-config.json.</summary>
        public string ConfigFilePath { get; }

        /// <summary>Full path to state.json.</summary>
        public string StateFilePath { get; }

        /// <summary>Directory where daily log files (yyyy-MM-dd.json) are stored.</summary>
        public string LogDirectory { get; }
    }
}
