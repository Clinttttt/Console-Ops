using ConsoleOps.Domain.Projects;

namespace ConsoleOps.Application.Abstractions.Persistence;

public interface IProjectRepository
{
    Task<ProjectRegistrationOutcome> TryAddAsync(Project project, CancellationToken cancellationToken);

    Task<Project?> GetActiveByIdAsync(Guid projectId, CancellationToken cancellationToken);

    Task<ProjectSaveOutcome> SaveChangesAsync(Project project, CancellationToken cancellationToken);
}

public enum ProjectRegistrationOutcome
{
    Added,
    DuplicateName,
    DuplicateRepository
}

public enum ProjectSaveOutcome
{
    Saved,
    DuplicateName,
    DuplicateRepository,
    ConfigurationConflict
}
