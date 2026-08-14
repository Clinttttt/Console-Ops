using ConsoleOps.Application.Abstractions.Messaging;
using ConsoleOps.Application.Abstractions.Persistence;
using ConsoleOps.Domain.Projects;
using MediatR;

namespace ConsoleOps.Application.Features.Projects.UpdateProject;

public sealed class UpdateProjectCommandHandler(
    IProjectRepository projectRepository,
    TimeProvider timeProvider)
    : IRequestHandler<UpdateProjectCommand, Result<ProjectResponse>>
{
    public async Task<Result<ProjectResponse>> Handle(
        UpdateProjectCommand request,
        CancellationToken cancellationToken)
    {
        Project? project = await projectRepository.GetActiveByIdAsync(request.ProjectId, cancellationToken);
        if (project is null)
        {
            return Result<ProjectResponse>.Failure(ProjectErrors.NotFound);
        }

        if (project.ConfigurationVersion != request.ConfigurationVersion)
        {
            return Result<ProjectResponse>.Failure(ProjectErrors.ConfigurationConflict);
        }

        HashSet<Guid> existingEnvironmentIds = project.Environments
            .Select(environment => environment.Id)
            .ToHashSet();

        if (request.Environments.Any(environment =>
                environment.Id.HasValue && !existingEnvironmentIds.Contains(environment.Id.Value)))
        {
            return Result<ProjectResponse>.Failure(ProjectErrors.UnknownEnvironment);
        }

        ProjectEnvironment[] environments = request.Environments
            .Select(environment => ProjectEnvironment.Create(
                environment.Id ?? Guid.CreateVersion7(),
                environment.Name,
                Enum.Parse<EnvironmentKind>(environment.Kind, true),
                environment.ApplicationUrl,
                environment.HealthUrl,
                environment.VersionUrl))
            .ToArray();

        project.UpdateConfiguration(
            request.Name,
            request.Description,
            request.Repository.Owner,
            request.Repository.Name,
            request.Repository.DefaultBranch,
            request.Repository.WorkflowFile,
            environments,
            timeProvider.GetUtcNow());

        ProjectSaveOutcome outcome = await projectRepository.SaveChangesAsync(project, cancellationToken);

        return outcome switch
        {
            ProjectSaveOutcome.Saved => Result<ProjectResponse>.Success(ProjectResponseMapper.FromDomain(project)),
            ProjectSaveOutcome.DuplicateName => Result<ProjectResponse>.Failure(ProjectErrors.DuplicateName),
            ProjectSaveOutcome.DuplicateRepository => Result<ProjectResponse>.Failure(ProjectErrors.DuplicateRepository),
            ProjectSaveOutcome.ConfigurationConflict => Result<ProjectResponse>.Failure(ProjectErrors.ConfigurationConflict),
            _ => throw new InvalidOperationException($"Unsupported project save outcome: {outcome}.")
        };
    }
}
