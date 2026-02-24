namespace EasySave.Core.Enums
{
	/// <summary>
	/// Represents the current state of a backup job.
	/// </summary>
	public enum BackupState
	{
		/// <summary>
		/// The job is pending or is not currently running.
		/// </summary>
		Inactive,

		/// <summary>
		/// The backup is currently running (copying files, calculations, etc.).
		/// </summary>
		Active,

        /// <summary>
        /// The backup is temporarily paused by the user. Progress values remain frozen.
        /// </summary>
        Paused,

        /// <summary>
        /// The backup completed successfully (100%).
        /// </summary>
        Completed,

		/// <summary>
		/// A critical error occurred and the backup was interrupted.
		/// </summary>
		Error
	}
}