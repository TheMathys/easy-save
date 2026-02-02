using EasySave.Core.Enums;

namespace EasySave.Core.Entities;

public class BackupJob
{
    public int Id { get; set; }
    public string Name { get; set; } = ""; // Name of the backup
    public string SourcePath { get; set; } = ""; // Source of the file
    public string TargetPath { get; set; } = ""; // Path to the saved file 
    public BackupType Type { get; set; } // Type of backup
}