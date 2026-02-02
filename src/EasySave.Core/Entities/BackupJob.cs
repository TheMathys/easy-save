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
        public string Name { get; set; } = "";
    
        ///<summary>Source of the file</summary>
        public string SourcePath { get; set; } = "";
    
        ///<summary>Path to the saved file</summary>
        public string TargetPath { get; set; } = "";  
    
        ///<summary>Type of backup</summary>
        public BackupType Type { get; set; }
    }
}