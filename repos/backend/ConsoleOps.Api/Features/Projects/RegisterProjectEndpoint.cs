using ConsoleOps.Api.Extensions;
using ConsoleOps.Application.Features.Projects;
using ConsoleOps.Application.Features.Projects.RegisterProject;
using MediatR;

namespace ConsoleOps.Api.Features.Projects;

internal static class RegisterProjectEndpoint
{
    public static RouteGroupBuilder MapRegisterProjectEndpoint(this RouteGroupBuilder projects)
    {
        projects.MapPost(string.Empty, Handle)
            .WithName("RegisterProject")
            .Produces<ProjectResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return projects;
    }

    private static async Task<IResult> Handle(
        RegisterProjectRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        RegisterProjectCommand command = new(
            request.Name,
            request.Description,
            request.Repository is null
                ? null!
                : new RegisterProjectRepository(
                    request.Repository.Owner,
                    request.Repository.Name,
                    request.Repository.DefaultBranch,
                    request.Repository.WorkflowFile),
            request.Environments?.Select(environment => new RegisterProjectEnvironment(
                environment.Name,
                environment.Kind,
                environment.ApplicationUrl,
                environment.HealthUrl,
                environment.VersionUrl,
                environment.LogSource is null
                    ? null
                    : new ProjectLogSource(
                        environment.LogSource.WorkspaceId,
                        environment.LogSource.ContainerAppName))).ToArray() ?? []);

        var result = await sender.Send(command, cancellationToken);

        return result.IsSuccess
            ? Results.Created($"/api/projects/{result.Value.Id}", result.Value)
            : result.ToProblemDetails();
    }
}

public sealed record RegisterProjectRequest(
    string Name,
    string? Description,
    ProjectRepositoryRequest? Repository,
    IReadOnlyCollection<RegisterProjectEnvironmentRequest>? Environments);
