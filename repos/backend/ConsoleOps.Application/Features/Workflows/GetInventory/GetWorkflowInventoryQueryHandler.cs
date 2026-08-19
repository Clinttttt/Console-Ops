using ConsoleOps.Application.Abstractions.Messaging;
using ConsoleOps.Application.Features.Projects;
using ConsoleOps.Application.Integrations.GitHub;
using MediatR;

namespace ConsoleOps.Application.Features.Workflows.GetInventory;

/// <summary>
/// Reads every registered repository's workflows from the provider.
/// </summary>
/// <remarks>
/// <para>
/// Classification comes from configuration, never from the provider: a workflow is a deployment where its file
/// is the one registered as the project's deployment workflow, and unclassified otherwise. GitHub reports no
/// business category, so anything else would be Console Ops guessing and presenting the guess as a fact.
/// </para>
/// <para>
/// A repository that cannot be read is reported as such on its own group. The remaining projects still answer,
/// because "GitHub rejected our token for this repository" and "this repository has no automation" are different
/// facts and must not look the same.
/// </para>
/// </remarks>
public sealed class GetWorkflowInventoryQueryHandler(
    IProjectReadStore projects,
    IGitHubWorkflowInventory inventory,
    TimeProvider timeProvider)
    : IRequestHandler<GetWorkflowInventoryQuery, Result<WorkflowInventoryResponse>>
{
    public async Task<Result<WorkflowInventoryResponse>> Handle(
        GetWorkflowInventoryQuery request,
        CancellationToken cancellationToken)
    {
        ProjectResponse[] registered = await projects.ListAsync(cancellationToken);
        List<WorkflowProjectGroupResponse> groups = new(registered.Length);

        foreach (ProjectResponse project in registered)
        {
            GitHubFactResult<GitHubWorkflowInventoryPage> result = await inventory.ListWorkflowsAsync(
                project.Repository.Owner,
                project.Repository.Name,
                cancellationToken);

            groups.Add(new WorkflowProjectGroupResponse(
                project.Id,
                project.Name,
                $"{project.Repository.Owner}/{project.Repository.Name}",
                result.IsSuccess
                    ? ToWorkflows(result.Observation!.Workflows, project.Repository.WorkflowFile)
                    : [],
                result.IsSuccess ? null : ToCamelCase(result.Failure!.Value)));
        }

        return Result<WorkflowInventoryResponse>.Success(new WorkflowInventoryResponse(
            timeProvider.GetUtcNow(),
            groups));
    }

    private static WorkflowResponse[] ToWorkflows(
        IReadOnlyList<GitHubWorkflowDefinition> workflows,
        string? deploymentWorkflowFile) =>
        workflows
            .Select(workflow => new WorkflowResponse(
                workflow.WorkflowId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                workflow.Name,
                workflow.Path,
                workflow.Active ? "active" : "disabled",
                IsDeploymentWorkflow(workflow, deploymentWorkflowFile) ? "deployment" : "unclassified",
                ToManualRun(workflow.SupportsManualRun),
                ToRun(workflow.LatestRun)))
            .ToArray();

    /// <summary>
    /// Whether this is the workflow the operator registered as the project's deployment workflow.
    /// </summary>
    /// <remarks>
    /// Matched on the file name, because that is what registration stores while the provider reports a full
    /// path. Both are compared case-insensitively without inferring anything from the rest of the path.
    /// </remarks>
    private static bool IsDeploymentWorkflow(
        GitHubWorkflowDefinition workflow,
        string? deploymentWorkflowFile)
    {
        if (string.IsNullOrWhiteSpace(deploymentWorkflowFile))
        {
            return false;
        }

        string configured = FileNameOf(deploymentWorkflowFile);
        return configured.Length > 0
            && string.Equals(FileNameOf(workflow.Path), configured, StringComparison.OrdinalIgnoreCase);
    }

    private static string FileNameOf(string path)
    {
        string trimmed = path.Trim().Replace('\\', '/');
        int separator = trimmed.LastIndexOf('/');
        return separator < 0 ? trimmed : trimmed[(separator + 1)..];
    }

    private static WorkflowRunResponse? ToRun(GitHubRunSummary? run)
    {
        if (run is null)
        {
            return null;
        }

        return new WorkflowRunResponse(
            run.RunId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            run.Number,
            ToCamelCase(run.Status),
            run.Conclusion is null ? null : ToCamelCase(run.Conclusion.Value),
            run.Branch,
            run.CommitSha,
            ShortSha(run.CommitSha),
            run.Event,
            run.Actor,
            run.StartedAtUtc,
            run.CompletedAtUtc,
            DurationSeconds(run.StartedAtUtc, run.CompletedAtUtc),
            run.RunUrl,
            // Read only for the workflow an operator selects, so a page of workflows costs a page of requests
            // rather than one per run on top.
            []);
    }

    /// <summary>
    /// The run's own elapsed time, or <c>null</c> when either end is missing.
    /// </summary>
    private static int? DurationSeconds(DateTimeOffset? startedAt, DateTimeOffset? completedAt)
    {
        if (startedAt is null || completedAt is null || completedAt < startedAt)
        {
            return null;
        }

        return (int)(completedAt.Value - startedAt.Value).TotalSeconds;
    }

    private static string ShortSha(string commitSha) =>
        commitSha.Length <= 7 ? commitSha : commitSha[..7];

    private static string ToManualRun(bool? supportsManualRun) => supportsManualRun switch
    {
        true => "supported",
        false => "unavailable",
        _ => "unknown"
    };

    private static string ToCamelCase<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        string name = value.ToString()!;
        return string.Concat(char.ToLowerInvariant(name[0]), name[1..]);
    }
}
