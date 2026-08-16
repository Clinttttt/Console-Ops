using ConsoleOps.Application.Features.Logs.GetStream;
using Microsoft.EntityFrameworkCore;

namespace ConsoleOps.Infrastructure.Persistence.Repositories;

/// <summary>
/// Reads recorded runs that fall inside a log window.
/// <para>
/// This is a narrow read for the Logs screen rather than a reuse of the release-history store: that store
/// reconciles runs with version and health observations across every project, which is far more work than
/// placing a marker on a timeline needs.
/// </para>
/// </summary>
internal sealed class LogMarkerReadStore(ConsoleOpsDbContext dbContext) : ILogMarkerReadStore
{
    /// <summary>
    /// Upper bound on markers for one window. A 24-hour window holding more releases than this is a
    /// deployment-frequency story the Deployments screen tells better than a log timeline.
    /// </summary>
    internal const int MaximumMarkers = 50;

    public async Task<IReadOnlyList<LogDeploymentMarker>> ReadDeploymentsAsync(
        Guid projectId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken)
    {
        // The projection stays translatable: COALESCE for the instant, and the enum mapped after the read
        // rather than inside the query.
        var rows = await dbContext.Deployments
            .AsNoTracking()
            .Where(deployment => deployment.ProjectId == projectId)
            .Select(deployment => new
            {
                deployment.Id,
                Instant = deployment.CompletedAtUtc ?? deployment.StartedAtUtc ?? deployment.RecordedAtUtc,
                deployment.CommitSha,
                deployment.Result,
            })
            .Where(row => row.Instant >= from && row.Instant <= to)
            .OrderByDescending(row => row.Instant)
            .Take(MaximumMarkers)
            .ToListAsync(cancellationToken);

        return
        [
            .. rows.Select(row => new LogDeploymentMarker(
                row.Id,
                row.Instant,
                row.CommitSha,
                row.Result.ToString()))
        ];
    }
}
