using ConsoleOps.Api.Extensions;
using ConsoleOps.Application.Abstractions.Messaging;
using ConsoleOps.Application.Features.Workflows.GetInventory;
using ConsoleOps.Application.Features.Workflows.GetRunJobs;
using MediatR;

namespace ConsoleOps.Api.Features.Workflows;

public static class WorkflowEndpoints
{
    public static IEndpointRouteBuilder MapWorkflowEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder workflows = endpoints.MapGroup("/api/workflows")
            .WithTags("Workflows");

        workflows.MapGetWorkflowInventoryEndpoint();
        workflows.MapGetWorkflowRunJobsEndpoint();
        return endpoints;
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
