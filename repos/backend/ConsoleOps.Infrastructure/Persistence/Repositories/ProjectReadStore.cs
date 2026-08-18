using ConsoleOps.Application.Features.Projects;
using ConsoleOps.Domain.Projects;
using Microsoft.EntityFrameworkCore;

namespace ConsoleOps.Infrastructure.Persistence.Repositories;

internal sealed class ProjectReadStore(ConsoleOpsDbContext dbContext) : IProjectReadStore
{
    public async Task<ProjectResponse?> GetAsync(Guid projectId, CancellationToken cancellationToken)
    {
        IQueryable<Project> query = dbContext.Projects
            .AsNoTracking()
            .Where(project => !project.IsArchived && project.Id == projectId);

        ProjectReadModel? project = await ProjectQuery(query)
            .SingleOrDefaultAsync(cancellationToken);

        return project is null ? null : Map(project);
    }

    public async Task<ProjectResponse[]> ListAsync(CancellationToken cancellationToken)
    {
        IQueryable<Project> query = dbContext.Projects
            .AsNoTracking()
            .Where(project => !project.IsArchived)
            .OrderBy(project => project.NormalizedName);

        ProjectReadModel[] projects = await ProjectQuery(query)
            .ToArrayAsync(cancellationToken);

        return projects.Select(Map).ToArray();
    }

    private static IQueryable<ProjectReadModel> ProjectQuery(IQueryable<Project> projects) =>
        projects.Select(project => new ProjectReadModel(
            project.Id,
            project.Name,
            project.NormalizedName,
            project.Description,
            project.RepositoryOwner,
            project.RepositoryName,
            project.DefaultBranch,
            project.WorkflowFile,
            project.Environments
                .OrderBy(environment => environment.Kind)
                .ThenBy(environment => environment.Name)
                .Select(environment => new ProjectEnvironmentReadModel(
                    environment.Id,
                    environment.Name,
                    environment.Kind,
                    environment.ApplicationUrl,
                    environment.HealthUrl,
                    environment.VersionUrl,
                    environment.LogSource))
                .ToArray(),
            project.CreatedAtUtc,
            project.UpdatedAtUtc,
            project.ConfigurationVersion));

    private static ProjectResponse Map(ProjectReadModel project) => new(
        project.Id,
        project.Name,
        project.Description,
        new ProjectRepositoryResponse(
            project.RepositoryOwner,
            project.RepositoryName,
            project.DefaultBranch,
            project.WorkflowFile),
        project.Environments.Select(environment => new ProjectEnvironmentResponse(
            environment.Id,
            environment.Name,
            environment.Kind.ToString().ToLowerInvariant(),
            environment.ApplicationUrl,
            environment.HealthUrl,
            environment.VersionUrl,
            environment.LogSource is null
                ? null
                : new ProjectLogSourceResponse(
                    "azureContainerApps",
                    environment.LogSource.WorkspaceId,
                    environment.LogSource.ContainerAppName))).ToArray(),
        project.CreatedAtUtc,
        project.UpdatedAtUtc,
        project.ConfigurationVersion);

    private sealed record ProjectReadModel(
        Guid Id,
        string Name,
        string NormalizedName,
        string? Description,
        string RepositoryOwner,
        string RepositoryName,
        string DefaultBranch,
        string? WorkflowFile,
        ProjectEnvironmentReadModel[] Environments,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset? UpdatedAtUtc,
        long ConfigurationVersion);

    private sealed record ProjectEnvironmentReadModel(
        Guid Id,
        string Name,
        EnvironmentKind Kind,
        string? ApplicationUrl,
        string? HealthUrl,
        string? VersionUrl,
        AzureLogSource? LogSource);
}
