using EasySave.Core.Enums;

namespace EasySave.Core.Interfaces
{
    /// <summary>
    /// Provides the appropriate backup strategy based on the specified backup type.
    /// </summary>
    public interface IBackupStrategyFactory
    {
        /// <summary>
        /// Retrieves the backup strategy that corresponds to the given backup type.
        /// </summary>
        /// <param name="type">The backup type used to determine which strategy to return.</param>
        /// <returns>The corresponding <see cref="IBackupStrategy"/> implementation.</returns>
        public IBackupStrategy GetStrategy(BackupType type);
    }
}