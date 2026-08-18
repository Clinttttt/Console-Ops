using ConsoleOps.Api.Extensions;
using ConsoleOps.Application.Features.Projects;
using ConsoleOps.Application.Features.Projects.UpdateProject;
using MediatR;

namespace ConsoleOps.Api.Features.Projects;

internal static class UpdateProjectEndpoint
{
    public static RouteGroupBuilder MapUpdateProjectEndpoint(this RouteGroupBuilder projects)
    {
        projects.MapPut("/{projectId:guid}", Handle)
            .WithName("UpdateProject")
            .Produces<ProjectResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return projects;
    }

    private static async Task<IResult> Handle(
        Guid projectId,
        UpdateProjectRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        UpdateProjectCommand command = new(
            projectId,
            request.ConfigurationVersion,
            request.Name,
            request.Description,
            request.Repository is null
                ? null!
                : new UpdateProjectRepository(
                    request.Repository.Owner,
                    request.Repository.Name,
                    request.Repository.DefaultBranch,
                    request.Repository.WorkflowFile),
            request.Environments?.Select(environment => new UpdateProjectEnvironment(
                environment.Id,
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
            ? Results.Ok(result.Value)
            : result.ToProblemDetails();
    }
}

public sealed record UpdateProjectRequest(
    long ConfigurationVersion,
    string Name,
    string? Description,
    ProjectRepositoryRequest? Repository,
    IReadOnlyCollection<UpdateProjectEnvironmentRequest>? Environments);
