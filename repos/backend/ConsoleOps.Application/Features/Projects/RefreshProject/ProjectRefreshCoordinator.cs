using System.Collections.Concurrent;

namespace ConsoleOps.Application.Features.Projects.RefreshProject;

public sealed class ProjectRefreshCoordinator
{
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _projectLocks = new();

    public async ValueTask<IAsyncDisposable> AcquireAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        SemaphoreSlim projectLock = _projectLocks.GetOrAdd(
            projectId,
            static _ => new SemaphoreSlim(1, 1));
        await projectLock.WaitAsync(cancellationToken);
        return new Lease(projectLock);
    }

    private sealed class Lease(SemaphoreSlim projectLock) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            projectLock.Release();
            return ValueTask.CompletedTask;
        }
    }
}
