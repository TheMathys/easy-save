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
        private readonly FullBackupStrategy _full = new();
        private readonly DifferentialBackupStrategy _differential = new();
       
        public IBackupStrategy GetStrategy(BackupType type) => type switch
        {
            BackupType.Full => _full,
            BackupType.Differential => _differential,
            _ => _full
        };
    }
}