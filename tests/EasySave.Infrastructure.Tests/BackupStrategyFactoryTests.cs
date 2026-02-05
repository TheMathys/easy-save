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
        FullBackupStrategy full = new();
        DifferentialBackupStrategy differential = new();
        BackupStrategyFactory factory = new(full, differential);

        IBackupStrategy result = factory.GetStrategy(BackupType.Full);

        Assert.Same(full, result);
    }

    [Fact]
    public void GetStrategy_ReturnsDifferentialBackupStrategy_WhenTypeIsDifferential()
    {
        FullBackupStrategy full = new();
        DifferentialBackupStrategy differential = new();
        BackupStrategyFactory factory = new(full, differential);

        IBackupStrategy result = factory.GetStrategy(BackupType.Differential);

        Assert.Same(differential, result);
    }

    [Fact]
    public void GetStrategy_ThrowsArgumentOutOfRangeException_WhenTypeIsInvalid()
    {
        FullBackupStrategy full = new();
        DifferentialBackupStrategy differential = new();
        BackupStrategyFactory factory = new(full, differential);
        BackupType invalidType = (BackupType)99;

        ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => factory.GetStrategy(invalidType));

        Assert.Equal("type", ex.ParamName);
        Assert.Equal(invalidType, ex.ActualValue);
        Assert.Contains("Unknown backup type", ex.Message);
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenFullBackupStrategyIsNull()
    {
        DifferentialBackupStrategy differential = new();

        ArgumentNullException ex = Assert.Throws<ArgumentNullException>(
            () => new BackupStrategyFactory(null!, differential));

        Assert.Equal("fullBackupStrategy", ex.ParamName);
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenDifferentialBackupStrategyIsNull()
    {
        FullBackupStrategy full = new();

        ArgumentNullException ex = Assert.Throws<ArgumentNullException>(
            () => new BackupStrategyFactory(full, null!));

        Assert.Equal("differentialBackupStrategy", ex.ParamName);
    }

    [Fact]
    public void GetStrategy_ReturnsSameInstance_ForMultipleCallsWithSameType()
    {
        FullBackupStrategy full = new();
        DifferentialBackupStrategy differential = new();
        BackupStrategyFactory factory = new(full, differential);

        IBackupStrategy result1 = factory.GetStrategy(BackupType.Full);
        IBackupStrategy result2 = factory.GetStrategy(BackupType.Full);

        Assert.Same(result1, result2);
        Assert.Same(full, result1);
    }
}
