namespace ConsoleOps.Application.Features.Logs.GetStream;

/// <summary>
/// Reads the release history Console Ops already recorded, so the Logs screen can explain what changed.
/// <para>
/// Markers are composed at query time from the <c>deployments</c> rows the refresh already writes: no marker
/// table, no second collection path, so Deployments and Logs cannot tell different stories. Rules in
/// <c>docs/Console_Ops_Logs_Plan.md</c>.
/// </para>
/// </summary>
public interface ILogMarkerReadStore
{
    /// <summary>Runs of the given project whose instant falls inside the window, newest first.</summary>
    Task<IReadOnlyList<LogDeploymentMarker>> ReadDeploymentsAsync(
        Guid projectId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken);
}

/// <param name="OccurredAt">
/// When the run ended, falling back to when it started and then to first sighting: the most specific instant
/// Console Ops actually has.
/// </param>
/// <param name="Result">
/// The outcome as recorded. A run that did not succeed is still shown - an operator reading logs around a
/// failed release needs to see it.
/// </param>
public sealed record LogDeploymentMarker(
    Guid Id,
    DateTimeOffset OccurredAt,
    string? CommitSha,
    string Result);
