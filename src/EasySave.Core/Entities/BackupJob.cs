using System.Collections.Generic;
using EasySave.Core.Enums;

namespace EasySave.Core.Entities
{
    /// <summary>
    /// Backup job.
    /// </summary>
    public class BackupJob
    {
        ///<summary>Unique job identifier (positive integer)</summary>
        public int Id { get; set; }

        ///<summary>Name of the backup</summary>
        public string Name { get; set; } = string.Empty;

        ///<summary>Source directory</summary>
        public string SourcePath { get; set; } = string.Empty;

        ///<summary>Target directory</summary>
        public string TargetPath { get; set; } = string.Empty;

        ///<summary>Type of backup</summary>
        public BackupType Type { get; set; }

        /// <summary>
        /// File extensions to exclude from the backup (e.g. .tmp, .log).
        /// </summary>
        public IReadOnlyList<string> ExcludeExtensions { get; set; } = new List<string>();

        /// <summary>
        /// Directory names to exclude from the backup, they're not traveled (e.g. node_modules, .git).
        /// </summary>
        public IReadOnlyList<string> ExcludeDirectoryNames { get; set; } = new List<string>();
    }
}