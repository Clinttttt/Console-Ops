using ConsoleOps.Api.Extensions;
using ConsoleOps.Application.Features.Projects.ArchiveProject;
using MediatR;

namespace ConsoleOps.Api.Features.Projects;

internal static class ArchiveProjectEndpoint
{
    public static RouteGroupBuilder MapArchiveProjectEndpoint(this RouteGroupBuilder projects)
    {
        projects.MapDelete("/{projectId:guid}", Handle)
            .WithName("ArchiveProject")
            .Produces(StatusCodes.Status204NoContent)
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
        var result = await sender.Send(new ArchiveProjectCommand(projectId), cancellationToken);

        return result.IsSuccess
            ? Results.NoContent()
            : result.ToProblemDetails();
    }
}
