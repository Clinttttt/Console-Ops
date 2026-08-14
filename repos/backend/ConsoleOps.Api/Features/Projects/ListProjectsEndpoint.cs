using ConsoleOps.Application.Features.Projects;
using ConsoleOps.Application.Features.Projects.ListProjects;
using MediatR;

namespace ConsoleOps.Api.Features.Projects;

internal static class ListProjectsEndpoint
{
    public static RouteGroupBuilder MapListProjectsEndpoint(this RouteGroupBuilder projects)
    {
        projects.MapGet(string.Empty, Handle)
            .WithName("ListProjects")
            .Produces<ProjectResponse[]>()
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return projects;
    }

    private static async Task<IResult> Handle(ISender sender, CancellationToken cancellationToken)
    {
        ProjectResponse[] projects = await sender.Send(new ListProjectsQuery(), cancellationToken);
        return Results.Ok(projects);
    }
}
