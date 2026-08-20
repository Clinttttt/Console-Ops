namespace ConsoleOps.Application.Features.Workflows;

/// <param name="Level"><c>normal</c> or <c>destructive</c>. Unmarked workflows are absent rather than listed.</param>
public sealed record WorkflowRiskRecord(
    Guid ProjectId,
    string WorkflowPath,
    string NormalizedWorkflowPath,
    string Level,
    DateTimeOffset DecidedAtUtc);

/// <summary>
/// The risk markings operators have made, read for the Workflows screen.
/// </summary>
/// <remarks>
/// Its own read rather than a field on the project contract: a marking is a fact about a workflow, the Projects
/// screens have no use for it, and widening their response would make an unrelated contract carry it forever.
/// </remarks>
public interface IWorkflowRiskReadStore
{
    Task<IReadOnlyList<WorkflowRiskRecord>> ListAsync(CancellationToken cancellationToken);
}
