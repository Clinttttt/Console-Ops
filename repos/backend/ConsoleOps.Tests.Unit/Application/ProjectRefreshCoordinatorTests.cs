using ConsoleOps.Application.Features.Projects.RefreshProject;

namespace ConsoleOps.Tests.Unit.Application;

public sealed class ProjectRefreshCoordinatorTests
{
    [Fact]
    public async Task AcquireAsync_SerializesRefreshesForTheSameProject()
    {
        ProjectRefreshCoordinator coordinator = new();
        Guid projectId = Guid.NewGuid();
        IAsyncDisposable firstLease = await coordinator.AcquireAsync(
            projectId,
            CancellationToken.None);
        Task<IAsyncDisposable> secondLeaseTask = coordinator
            .AcquireAsync(projectId, CancellationToken.None)
            .AsTask();

        Assert.False(secondLeaseTask.IsCompleted);

        await firstLease.DisposeAsync();
        await using IAsyncDisposable secondLease = await secondLeaseTask;
    }

    [Fact]
    public async Task AcquireAsync_AllowsDifferentProjectsConcurrently()
    {
        ProjectRefreshCoordinator coordinator = new();
        await using IAsyncDisposable firstLease = await coordinator.AcquireAsync(
            Guid.NewGuid(),
            CancellationToken.None);

        ValueTask<IAsyncDisposable> secondLease = coordinator.AcquireAsync(
            Guid.NewGuid(),
            CancellationToken.None);

        Assert.True(secondLease.IsCompletedSuccessfully);
        await using IAsyncDisposable completedSecondLease = await secondLease;
    }
}
