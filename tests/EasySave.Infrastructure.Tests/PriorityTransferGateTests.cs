using System.Threading;
using System.Threading.Tasks;
using EasySave.Core.Interfaces;
using EasySave.Infrastructure.Backup;
using Xunit;

namespace EasySave.Infrastructure.Tests;

/// <summary>
/// Unit tests for <see cref="PriorityTransferGate"/> (global priority rule coordination across jobs).
/// </summary>
public sealed class PriorityTransferGateTests
{
    [Fact]
    public void RegisterJob_WithZeroCount_DoesNotBlockNonPriorityTransfer()
    {
        var gate = new PriorityTransferGate();
        gate.RegisterJob(1, 0);

        // Wait should return immediately when total priority pending is 0.
        Task waitTask = gate.WaitUntilCanTransferNonPriorityAsync(CancellationToken.None);
        Assert.True(waitTask.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task WaitUntilCanTransferNonPriorityAsync_Blocks_UntilPriorityCountReachesZero()
    {
        var gate = new PriorityTransferGate();
        gate.RegisterJob(1, 1);

        var cts = new CancellationTokenSource();
        Task waitTask = gate.WaitUntilCanTransferNonPriorityAsync(cts.Token);

        await Task.Delay(50);
        Assert.False(waitTask.IsCompleted);

        gate.NotifyPriorityFileStarted(1);
        await waitTask;
        Assert.True(waitTask.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task WaitUntilCanTransferNonPriorityAsync_Completes_WhenUnregisterJobRemovesPendingCount()
    {
        var gate = new PriorityTransferGate();
        gate.RegisterJob(1, 2);

        Task waitTask = gate.WaitUntilCanTransferNonPriorityAsync(CancellationToken.None);
        await Task.Delay(50);
        Assert.False(waitTask.IsCompleted);

        gate.UnregisterJob(1);
        await waitTask;
        Assert.True(waitTask.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task MultipleJobs_AllPriorityMustBeStartedBeforeNonPriorityProceeds()
    {
        var gate = new PriorityTransferGate();
        gate.RegisterJob(1, 1);
        gate.RegisterJob(2, 1);

        Task waitTask = gate.WaitUntilCanTransferNonPriorityAsync(CancellationToken.None);
        await Task.Delay(50);
        Assert.False(waitTask.IsCompleted);

        gate.NotifyPriorityFileStarted(1);
        await Task.Delay(50);
        Assert.False(waitTask.IsCompleted);

        gate.NotifyPriorityFileStarted(2);
        await waitTask;
        Assert.True(waitTask.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task WaitUntilCanTransferNonPriorityAsync_Throws_WhenCancelled()
    {
        var gate = new PriorityTransferGate();
        gate.RegisterJob(1, 1);

        var cts = new CancellationTokenSource();
        Task waitTask = gate.WaitUntilCanTransferNonPriorityAsync(cts.Token);
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await waitTask);
    }
}
