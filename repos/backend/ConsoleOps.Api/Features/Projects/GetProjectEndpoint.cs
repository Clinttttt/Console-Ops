using ConsoleOps.Api.Extensions;
using ConsoleOps.Application.Features.Projects;
using ConsoleOps.Application.Features.Projects.GetProject;
using MediatR;

namespace ConsoleOps.Api.Features.Projects;

internal static class GetProjectEndpoint
{
    public static RouteGroupBuilder MapGetProjectEndpoint(this RouteGroupBuilder projects)
    {
        projects.MapGet("/{projectId:guid}", Handle)
            .WithName("GetProject")
            .Produces<ProjectResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return projects;
    }

    private static async Task<IResult> Handle(
        Guid projectId,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetProjectQuery(projectId), cancellationToken);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : result.ToProblemDetails();
    }
}
