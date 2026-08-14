using ConsoleOps.Api.Extensions;
using ConsoleOps.Application.Features.Projects.RefreshProject;
using MediatR;

namespace ConsoleOps.Api.Features.Projects;

internal static class RefreshProjectEndpoint
{
    public static RouteGroupBuilder MapRefreshProjectEndpoint(this RouteGroupBuilder projects)
    {
        projects.MapPost("/{projectId:guid}/refresh", Handle)
            .WithName("RefreshProject")
            .Produces<RefreshProjectResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return projects;
    }

    private static async Task<IResult> Handle(
        Guid projectId,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new RefreshProjectCommand(projectId),
            cancellationToken);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : result.ToProblemDetails();
    }
}
