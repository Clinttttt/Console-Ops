using ConsoleOps.Application.Abstractions.Messaging;
using ConsoleOps.Application.Abstractions.Persistence;
using ConsoleOps.Application.Features.Projects;
using ConsoleOps.Domain.Projects;
using MediatR;

namespace ConsoleOps.Application.Features.Workflows.SetRisk;

/// <summary>
/// Records how much intent starting one workflow should require.
/// </summary>
/// <remarks>
/// The one thing on the Workflows screen an operator can change. It exists because Console Ops must not decide
/// this: a name cannot prove that a workflow drops a database, and until somebody says so the workflow is not
/// offered for execution at all.
/// </remarks>
public sealed record SetWorkflowRiskCommand(Guid ProjectId, string WorkflowPath, string Level)
    : IRequest<Result<WorkflowRiskResponse>>;

/// <param name="Level"><c>unclassified</c>, <c>normal</c>, or <c>destructive</c>.</param>
/// <param name="DecidedAt">
/// When an operator decided, or <c>null</c> for <c>unclassified</c> - the absence of a decision has no date.
/// </param>
public sealed record WorkflowRiskResponse(string WorkflowPath, string Level, DateTimeOffset? DecidedAt);

public sealed class SetWorkflowRiskCommandHandler(
    IProjectRepository projectRepository,
    TimeProvider timeProvider)
    : IRequestHandler<SetWorkflowRiskCommand, Result<WorkflowRiskResponse>>
{
    public async Task<Result<WorkflowRiskResponse>> Handle(
        SetWorkflowRiskCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.WorkflowPath))
        {
            return Result<WorkflowRiskResponse>.Failure(WorkflowErrors.WorkflowPathRequired);
        }

        if (!TryParseLevel(request.Level, out WorkflowRiskLevel level))
        {
            return Result<WorkflowRiskResponse>.Failure(WorkflowErrors.UnknownRiskLevel);
        }

        Project? project = await projectRepository.GetActiveByIdAsync(request.ProjectId, cancellationToken);
        if (project is null)
        {
            return Result<WorkflowRiskResponse>.Failure(ProjectErrors.NotFound);
        }

        project.SetWorkflowRisk(
            Guid.CreateVersion7(),
            request.WorkflowPath,
            level,
            timeProvider.GetUtcNow());

        ProjectSaveOutcome outcome = await projectRepository.SaveChangesAsync(project, cancellationToken);
        if (outcome == ProjectSaveOutcome.ConfigurationConflict)
        {
            return Result<WorkflowRiskResponse>.Failure(ProjectErrors.ConfigurationConflict);
        }

        ProjectWorkflowRisk? stored = project.WorkflowRisks.FirstOrDefault(risk =>
            risk.NormalizedWorkflowPath == ProjectRules.Normalize(request.WorkflowPath));

        return Result<WorkflowRiskResponse>.Success(new WorkflowRiskResponse(
            request.WorkflowPath.Trim(),
            WorkflowRunMapping.ToCamelCase(stored?.Level ?? WorkflowRiskLevel.Unclassified),
            stored?.DecidedAtUtc));
    }

    /// <summary>
    /// Parses the wire value. An unrecognised level is refused rather than falling back to a safer-sounding one:
    /// silently recording <c>normal</c> for a value Console Ops did not understand would be the dangerous default.
    /// </summary>
    private static bool TryParseLevel(string? value, out WorkflowRiskLevel level)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case "unclassified":
                level = WorkflowRiskLevel.Unclassified;
                return true;
            case "normal":
                level = WorkflowRiskLevel.Normal;
                return true;
            case "destructive":
                level = WorkflowRiskLevel.Destructive;
                return true;
            default:
                level = WorkflowRiskLevel.Unclassified;
                return false;
        }
    }
}
