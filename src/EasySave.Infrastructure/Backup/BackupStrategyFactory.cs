using System;
using EasySave.Core.Enums;
using EasySave.Core.Interfaces;

namespace EasySave.Infrastructure.Backup
{
    /// <summary>
    /// Simple factory that returns the right IBackupStrategy implementation for a BackupType.
    /// </summary>
    public sealed class BackupStrategyFactory : IBackupStrategyFactory
    {
        private readonly FullBackupStrategy _full;
        private readonly DifferentialBackupStrategy _differential;
        public BackupStrategyFactory(
            FullBackupStrategy fullBackupStrategy,
            DifferentialBackupStrategy differentialBackupStrategy)
        {
            _full = fullBackupStrategy ?? throw new ArgumentNullException(nameof(fullBackupStrategy));
            _differential = differentialBackupStrategy ?? throw new ArgumentNullException(nameof(differentialBackupStrategy));
        }
        public IBackupStrategy GetStrategy(BackupType type) =>
            type switch
            {
                BackupType.Full => _full,
                BackupType.Differential => _differential,
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown backup type")
            };
    }
}