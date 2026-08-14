namespace ConsoleOps.Api.Features.Projects;

public static class ProjectEndpoints
{
    public static IEndpointRouteBuilder MapProjectEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder projects = endpoints.MapGroup("/api/projects")
            .WithTags("Projects");

        projects.MapRegisterProjectEndpoint();
        projects.MapUpdateProjectEndpoint();
        projects.MapArchiveProjectEndpoint();
        projects.MapGetProjectEndpoint();
        projects.MapListProjectsEndpoint();

        return endpoints;
    }
}
