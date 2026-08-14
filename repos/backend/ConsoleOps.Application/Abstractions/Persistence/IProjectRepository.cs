using ConsoleOps.Domain.Projects;

namespace ConsoleOps.Application.Abstractions.Persistence;

public interface IProjectRepository
{
    Task<ProjectRegistrationOutcome> TryAddAsync(Project project, CancellationToken cancellationToken);
}

public enum ProjectRegistrationOutcome
{
    Added,
    DuplicateName,
    DuplicateRepository
}
