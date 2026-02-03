using System;

namespace EasySave.Core.Models
{
	/// <summary>
	/// Represents a file eligible for backup.
	/// This record is immutable and used to transfer file metadata between the file system service and the backup strategy.
	/// </summary>
	/// <param name="RelativePath">The relative path of the file from the source directory (e.g., "Documents/report.docx").</param>
	/// <param name="FullSourcePath">The absolute path of the source file (e.g., "C:/Users/Bob/Documents/report.docx").</param>
	/// <param name="LastWriteTimeUtc">The last modification date and time in UTC, essential for identifying changed files in differential backups.</param>
	public record FileItem(
		string RelativePath,
		string FullSourcePath,
		DateTime LastWriteTimeUtc
	);
}