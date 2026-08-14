using ConsoleOps.Application.Abstractions.Messaging;
using ConsoleOps.Application.Abstractions.Persistence;
using ConsoleOps.Domain.Projects;
using MediatR;

namespace ConsoleOps.Application.Features.Projects.RegisterProject;

public sealed class RegisterProjectCommandHandler(
    IProjectRepository projectRepository,
    TimeProvider timeProvider)
    : IRequestHandler<RegisterProjectCommand, Result<ProjectResponse>>
{
    public async Task<Result<ProjectResponse>> Handle(
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
            ProjectRegistrationOutcome.Added => Result<ProjectResponse>.Success(ProjectResponseMapper.FromDomain(project)),
            ProjectRegistrationOutcome.DuplicateName => Result<ProjectResponse>.Failure(ProjectErrors.DuplicateName),
            ProjectRegistrationOutcome.DuplicateRepository => Result<ProjectResponse>.Failure(ProjectErrors.DuplicateRepository),
            _ => throw new InvalidOperationException($"Unsupported registration outcome: {outcome}.")
        };
    }
}
