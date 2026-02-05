using System.Collections.Generic;

namespace EasySave.Core.Models
{
    /// <summary>
    /// Options applied during backup file enumeration.
    /// </summary>
    public sealed class BackupEnumerationOptions
    {
        /// <summary>
        /// File extensions to exclude
        /// </summary>
        public IReadOnlyList<string> ExcludeExtensions { get; init; } = new List<string>();

        /// <summary>
        /// Directory names to exclude (e.g. "node_modules", ".git").
        /// </summary>
        public IReadOnlyList<string> ExcludeDirectoryNames { get; init; } = new List<string>();
    }
}
