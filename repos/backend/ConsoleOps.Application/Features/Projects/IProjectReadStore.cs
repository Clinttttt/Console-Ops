namespace ConsoleOps.Application.Features.Projects;

public interface IProjectReadStore
{
    Task<ProjectResponse?> GetAsync(Guid projectId, CancellationToken cancellationToken);

    Task<ProjectResponse[]> ListAsync(CancellationToken cancellationToken);
}
