namespace ConsoleOps.Application.Features.Logs.GetStream;

/// <summary>
/// Reads the release history Console Ops already recorded, so the Logs screen can explain what changed
/// during the window it is showing.
/// <para>
/// Markers are composed at query time from the <c>deployments</c> rows the refresh already writes. There is
/// no marker table and no second collection path: the same GitHub Actions run that appears on Deployments
/// is what appears here, so the two screens can never tell different stories.
/// </para>
/// </summary>
public interface ILogMarkerReadStore
{
    /// <summary>
    /// Runs of the given project whose instant falls inside the window, newest first.
    /// </summary>
    Task<IReadOnlyList<LogDeploymentMarker>> ReadDeploymentsAsync(
        Guid projectId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken);
}

/// <summary>
/// A recorded run positioned in time.
/// </summary>
/// <param name="OccurredAt">
/// When the run ended, falling back to when it started and then to first sighting. A marker is placed at
/// the most specific instant Console Ops actually has.
/// </param>
/// <param name="Result">
/// The run's outcome as recorded. A run that did not succeed is still shown: an operator reading logs
/// around a failed release needs to see it, and hiding it would misrepresent the timeline.
/// </param>
public sealed record LogDeploymentMarker(
    Guid Id,
    DateTimeOffset OccurredAt,
    string? CommitSha,
    string Result);
