using EasySave.Core.Enums;

namespace EasySave.Core.Interfaces
{
    /// <summary>
    /// Returns the strategy according to the type
    /// </summary>
    public interface IBackupStrategyFactory
    {
        /// <summary>
        /// Give the strategy according to the backup type
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public IBackupStrategy GetStrategy(BackupType type);
    }
}