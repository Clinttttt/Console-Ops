using ConsoleOps.Application.Abstractions.Messaging;
using ConsoleOps.Application.Abstractions.Persistence;
using ConsoleOps.Domain.Projects;
using MediatR;

namespace ConsoleOps.Application.Features.Projects.ArchiveProject;

public sealed class ArchiveProjectCommandHandler(
    IProjectRepository projectRepository,
    TimeProvider timeProvider)
    : IRequestHandler<ArchiveProjectCommand, Result>
{
    public async Task<Result> Handle(ArchiveProjectCommand request, CancellationToken cancellationToken)
    {
        Project? project = await projectRepository.GetActiveByIdAsync(request.ProjectId, cancellationToken);
        if (project is null)
        {
            return Result.Failure(ProjectErrors.NotFound);
        }

        project.Archive(timeProvider.GetUtcNow());
        ProjectSaveOutcome outcome = await projectRepository.SaveChangesAsync(project, cancellationToken);

        return outcome switch
        {
            ProjectSaveOutcome.Saved => Result.Success(),
            ProjectSaveOutcome.ConfigurationConflict => Result.Failure(ProjectErrors.ConfigurationConflict),
            _ => throw new InvalidOperationException($"Unsupported archive outcome: {outcome}.")
        };
    }
}
