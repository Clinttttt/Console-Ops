using ConsoleOps.Api.Extensions;
using ConsoleOps.Application.Abstractions.Messaging;
using ConsoleOps.Application.Features.Workflows.GetInventory;
using ConsoleOps.Application.Features.Workflows.GetManualRunSupport;
using ConsoleOps.Application.Features.Workflows.GetRunJobs;
using ConsoleOps.Application.Features.Workflows.GetRuns;
using ConsoleOps.Application.Features.Workflows.SetRisk;
using MediatR;

namespace ConsoleOps.Api.Features.Workflows;

public static class WorkflowEndpoints
{
    public static IEndpointRouteBuilder MapWorkflowEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder workflows = endpoints.MapGroup("/api/workflows")
            .WithTags("Workflows");

        workflows.MapGetWorkflowInventoryEndpoint();
        workflows.MapGetWorkflowRunsEndpoint();
        workflows.MapGetWorkflowRunJobsEndpoint();
        workflows.MapGetManualRunSupportEndpoint();
        workflows.MapSetWorkflowRiskEndpoint();
        return endpoints;
    }
}

internal static class GetWorkflowRunsEndpoint
{
    public static RouteGroupBuilder MapGetWorkflowRunsEndpoint(this RouteGroupBuilder workflows)
    {
        workflows.MapGet("/projects/{projectId:guid}/workflows/{workflowId:long}/runs", Handle)
            .WithName("GetWorkflowRuns")
            .WithSummary("Lists recent runs of one workflow, newest first.")
            .WithDescription(
                "Bounded to one page, because history answers what a workflow has been doing lately rather "
                + "than everything it has ever done, and `hasMore` says so. The repository comes from the "
                + "registered project rather than from the request.")
            .Produces<WorkflowRunsResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return workflows;
    }

    private static async Task<IResult> Handle(
        Guid projectId,
        long workflowId,
        int? limit,
        ISender sender,
        CancellationToken cancellationToken)
    {
        Result<WorkflowRunsResponse> result = await sender.Send(
            new GetWorkflowRunsQuery(projectId, workflowId, limit),
            cancellationToken);

        return result.ToHttpResult();
    }
}

internal static class GetWorkflowInventoryEndpoint
{
    public static RouteGroupBuilder MapGetWorkflowInventoryEndpoint(this RouteGroupBuilder workflows)
    {
        workflows.MapGet("/", Handle)
            .WithName("GetWorkflowInventory")
            .WithSummary("Lists the workflows of every registered repository with each one's latest run.")
            .WithDescription(
                "Read live from GitHub and bounded to one page of workflows per repository plus one run per "
                + "workflow. A workflow is classified as a deployment only where its file is registered as "
                + "the project's deployment workflow; nothing is inferred from a name or a trigger. A "
                + "repository that could not be read reports why on its own group rather than appearing to "
                + "have no automation.")
            .Produces<WorkflowInventoryResponse>(StatusCodes.Status200OK);

        return workflows;
    }

    private static async Task<IResult> Handle(ISender sender, CancellationToken cancellationToken)
    {
        Result<WorkflowInventoryResponse> result = await sender.Send(
            new GetWorkflowInventoryQuery(),
            cancellationToken);

        return result.ToHttpResult();
    }
}

internal static class GetWorkflowRunJobsEndpoint
{
    public static RouteGroupBuilder MapGetWorkflowRunJobsEndpoint(this RouteGroupBuilder workflows)
    {
        workflows.MapGet("/projects/{projectId:guid}/runs/{runId:long}/jobs", Handle)
            .WithName("GetWorkflowRunJobs")
            .WithSummary("Lists the jobs of one workflow run.")
            .WithDescription(
                "Read on demand for the workflow an operator selected, because jobs cost one request per run. "
                + "The repository comes from the registered project rather than from the request, so this read "
                + "cannot be pointed at a repository Console Ops does not manage.")
            .Produces<WorkflowRunJobsResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return workflows;
    }

    private static async Task<IResult> Handle(
        Guid projectId,
        long runId,
        ISender sender,
        CancellationToken cancellationToken)
    {
        Result<WorkflowRunJobsResponse> result = await sender.Send(
            new GetWorkflowRunJobsQuery(projectId, runId),
            cancellationToken);

        return result.ToHttpResult();
    }
}

internal static class GetManualRunSupportEndpoint
{
    public static RouteGroupBuilder MapGetManualRunSupportEndpoint(this RouteGroupBuilder workflows)
    {
        workflows.MapGet("/projects/{projectId:guid}/workflows/{workflowId:long}/manual-run", Handle)
            .WithName("GetWorkflowManualRunSupport")
            .WithSummary("Reports whether one workflow declares a manual dispatch trigger.")
            .WithDescription(
                "Read from the workflow's own definition, because GitHub's listing does not report triggers. "
                + "Costs one request per workflow, so it is read for the workflow an operator selected rather "
                + "than for every workflow on the page. A definition that could not be read reports unknown "
                + "rather than claiming a manual run is unavailable.")
            .Produces<ManualRunSupportResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return workflows;
    }

    private static async Task<IResult> Handle(
        Guid projectId,
        long workflowId,
        string path,
        ISender sender,
        CancellationToken cancellationToken)
    {
        Result<ManualRunSupportResponse> result = await sender.Send(
            new GetManualRunSupportQuery(projectId, workflowId, path),
            cancellationToken);

        return result.ToHttpResult();
    }
}
internal static class SetWorkflowRiskEndpoint
{
    public static RouteGroupBuilder MapSetWorkflowRiskEndpoint(this RouteGroupBuilder workflows)
    {
        workflows.MapPut("/projects/{projectId:guid}/risk", Handle)
            .WithName("SetWorkflowRisk")
            .WithSummary("Records how much intent starting one workflow should require.")
            .WithDescription(
                "The one thing on the Workflows screen an operator changes. Console Ops does not decide this: a "
                + "name cannot prove that a workflow drops a database, so until an operator marks it the "
                + "workflow is not offered for execution. Marking it unclassified again removes the decision.")
            .Produces<WorkflowRiskResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return workflows;
    }

    private static async Task<IResult> Handle(
        Guid projectId,
        SetWorkflowRiskRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        Result<WorkflowRiskResponse> result = await sender.Send(
            new SetWorkflowRiskCommand(projectId, request.WorkflowPath, request.Level),
            cancellationToken);

        return result.ToHttpResult();
    }

    /// <param name="WorkflowPath">The definition path, which is what an operator recognises and what survives a
    /// provider workflow id changing.</param>
    internal sealed record SetWorkflowRiskRequest(string WorkflowPath, string Level);
}