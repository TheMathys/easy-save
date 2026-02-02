using EasySave.Core.Enums;

namespace EasySave.Core.Entities
{
    /// <summary>
    /// Backup job.
    /// </summary>
    public class BackupJob
    {
        ///<summary>Unique job identifier ranging from 1 to 5</summary>
        public int Id { get; set; }
    
        ///<summary>Name of the backup</summary>
        public string Name { get; set; } = string.Empty;
    
        ///<summary>Source directory</summary>
        public string SourcePath { get; set; } = string.Empty;
    
        ///<summary>Target directoy</summary>
        public string TargetPath { get; set; } = string.Empty;  
    
        ///<summary>Type of backup</summary>
        public BackupType Type { get; set; }
    }
}