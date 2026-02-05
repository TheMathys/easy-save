using EasySave.Core.Enums;
using EasySave.Core.Interfaces;
using EasySave.Infrastructure.Backup;
using Xunit;

namespace EasySave.Infrastructure.Tests;

/// <summary>
/// Tests for <see cref="BackupStrategyFactory"/> covering all usage cases.
/// </summary>
public sealed class BackupStrategyFactoryTests
{
    [Fact]
    public void GetStrategy_ReturnsFullBackupStrategy_WhenTypeIsFull()
    {
        BackupStrategyFactory factory = new();

        IBackupStrategy result = factory.GetStrategy(BackupType.Full);

        Assert.IsType<FullBackupStrategy>(result);
    }

    [Fact]
    public void GetStrategy_ReturnsDifferentialBackupStrategy_WhenTypeIsDifferential()
    {
        BackupStrategyFactory factory = new();

        IBackupStrategy result = factory.GetStrategy(BackupType.Differential);

        Assert.IsType<DifferentialBackupStrategy>(result);
    }

    [Fact]
    public void GetStrategy_ReturnsSameSingletonInstances_ForEachType()
    {
        BackupStrategyFactory factory = new();

        IBackupStrategy full1 = factory.GetStrategy(BackupType.Full);
        IBackupStrategy full2 = factory.GetStrategy(BackupType.Full);
        IBackupStrategy diff1 = factory.GetStrategy(BackupType.Differential);
        IBackupStrategy diff2 = factory.GetStrategy(BackupType.Differential);

        Assert.Same(full1, full2);
        Assert.Same(diff1, diff2);
        Assert.NotSame(full1, diff1);
    }

    [Fact]
    public void GetStrategy_FallbacksToFullStrategy_ForUnknownType()
    {
        BackupStrategyFactory factory = new();
        BackupType invalid = (BackupType)99;

        IBackupStrategy full = factory.GetStrategy(BackupType.Full);
        IBackupStrategy result = factory.GetStrategy(invalid);

        Assert.Same(full, result);
    }
}
