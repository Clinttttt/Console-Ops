using ConsoleOps.Api.Infrastructure;
using ConsoleOps.Application.Features.Projects.RegisterProject;
using MediatR;

namespace ConsoleOps.Api.Features.Projects;

public static class ProjectEndpoints
{
    public static IEndpointRouteBuilder MapProjectEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder projects = endpoints.MapGroup("/api/projects")
            .WithTags("Projects");

        projects.MapPost(string.Empty, RegisterProject)
            .WithName("RegisterProject")
            .Produces<RegisterProjectResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return endpoints;
    }

    private static async Task<IResult> RegisterProject(
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
                environment.VersionUrl)).ToArray() ?? []);

        var result = await sender.Send(command, cancellationToken);

        return result.IsSuccess
            ? Results.Created($"/api/projects/{result.Value.Id}", result.Value)
            : result.ToProblemDetails();
    }
}

public sealed record RegisterProjectRequest(
    string Name,
    string? Description,
    RegisterProjectRepositoryRequest? Repository,
    IReadOnlyCollection<RegisterProjectEnvironmentRequest>? Environments);

public sealed record RegisterProjectRepositoryRequest(
    string Owner,
    string Name,
    string DefaultBranch,
    string? WorkflowFile);

public sealed record RegisterProjectEnvironmentRequest(
    string Name,
    string Kind,
    string? ApplicationUrl,
    string? HealthUrl,
    string? VersionUrl);
