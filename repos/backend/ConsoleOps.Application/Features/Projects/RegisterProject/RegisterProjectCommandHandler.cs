using ConsoleOps.Application.Abstractions.Messaging;
using ConsoleOps.Application.Abstractions.Persistence;
using ConsoleOps.Domain.Projects;
using MediatR;

namespace ConsoleOps.Application.Features.Projects.RegisterProject;

public sealed class RegisterProjectCommandHandler(
    IProjectRepository projectRepository,
    TimeProvider timeProvider)
    : IRequestHandler<RegisterProjectCommand, Result<RegisterProjectResponse>>
{
    public async Task<Result<RegisterProjectResponse>> Handle(
        RegisterProjectCommand request,
        CancellationToken cancellationToken)
    {
        ProjectEnvironment[] environments = request.Environments
            .Select(environment => ProjectEnvironment.Create(
                Guid.CreateVersion7(),
                environment.Name,
                Enum.Parse<EnvironmentKind>(environment.Kind, true),
                environment.ApplicationUrl,
                environment.HealthUrl,
                environment.VersionUrl))
            .ToArray();

        Project project = Project.Create(
            Guid.CreateVersion7(),
            request.Name,
            request.Description,
            request.Repository.Owner,
            request.Repository.Name,
            request.Repository.DefaultBranch,
            request.Repository.WorkflowFile,
            environments,
            timeProvider.GetUtcNow());

        ProjectRegistrationOutcome outcome = await projectRepository.TryAddAsync(project, cancellationToken);

        return outcome switch
        {
            ProjectRegistrationOutcome.Added => Result<RegisterProjectResponse>.Success(ToResponse(project)),
            ProjectRegistrationOutcome.DuplicateName => Result<RegisterProjectResponse>.Failure(RegisterProjectErrors.DuplicateName),
            ProjectRegistrationOutcome.DuplicateRepository => Result<RegisterProjectResponse>.Failure(RegisterProjectErrors.DuplicateRepository),
            _ => throw new InvalidOperationException($"Unsupported registration outcome: {outcome}.")
        };
    }

    private static RegisterProjectResponse ToResponse(Project project) => new(
        project.Id,
        project.Name,
        project.Description,
        new RegisterProjectRepository(
            project.RepositoryOwner,
            project.RepositoryName,
            project.DefaultBranch,
            project.WorkflowFile),
        project.Environments.Select(environment => new RegisteredEnvironmentResponse(
            environment.Id,
            environment.Name,
            environment.Kind.ToString().ToLowerInvariant(),
            environment.ApplicationUrl,
            environment.HealthUrl,
            environment.VersionUrl)).ToArray(),
        project.CreatedAtUtc);
}
