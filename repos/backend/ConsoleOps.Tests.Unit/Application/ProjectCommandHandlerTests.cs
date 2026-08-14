using ConsoleOps.Application.Abstractions.Persistence;
using ConsoleOps.Application.Features.Projects;
using ConsoleOps.Application.Features.Projects.ArchiveProject;
using ConsoleOps.Application.Features.Projects.UpdateProject;
using ConsoleOps.Domain.Projects;

namespace ConsoleOps.Tests.Unit.Application;

public sealed class ProjectCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 14, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Update_WithCurrentVersion_PreservesEnvironmentIdentity()
    {
        Project project = CreateProject();
        Guid environmentId = Assert.Single(project.Environments).Id;
        StubProjectRepository repository = new(project, ProjectSaveOutcome.Saved);
        UpdateProjectCommandHandler handler = new(repository, new FixedTimeProvider(Now));

        var result = await handler.Handle(
            CreateUpdateCommand(project.Id, project.ConfigurationVersion, environmentId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.ConfigurationVersion);
        Assert.Equal(environmentId, Assert.Single(result.Value.Environments).Id);
        Assert.Equal("Updated Production", Assert.Single(result.Value.Environments).Name);
        Assert.True(repository.SaveCalled);
    }

    [Fact]
    public async Task Update_WithStaleVersion_ReturnsConflictWithoutSaving()
    {
        Project project = CreateProject();
        Guid environmentId = Assert.Single(project.Environments).Id;
        StubProjectRepository repository = new(project, ProjectSaveOutcome.Saved);
        UpdateProjectCommandHandler handler = new(repository, new FixedTimeProvider(Now));

        var result = await handler.Handle(
            CreateUpdateCommand(project.Id, project.ConfigurationVersion + 1, environmentId),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ProjectErrors.ConfigurationConflict, result.Error);
        Assert.False(repository.SaveCalled);
    }

    [Fact]
    public async Task Archive_WhenProjectExists_SoftArchivesAndSaves()
    {
        Project project = CreateProject();
        StubProjectRepository repository = new(project, ProjectSaveOutcome.Saved);
        ArchiveProjectCommandHandler handler = new(repository, new FixedTimeProvider(Now));

        var result = await handler.Handle(new ArchiveProjectCommand(project.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(project.IsArchived);
        Assert.Equal(Now, project.ArchivedAtUtc);
        Assert.True(repository.SaveCalled);
    }

    private static UpdateProjectCommand CreateUpdateCommand(Guid projectId, long version, Guid environmentId) => new(
        projectId,
        version,
        "Console Ops Updated",
        "Updated description",
        new UpdateProjectRepository("Clinttttt", "Console-Ops", "develop", "deploy.yml"),
        [new UpdateProjectEnvironment(
            environmentId,
            "Updated Production",
            "production",
            "https://console.example.com",
            "https://console.example.com/health",
            "https://console.example.com/version")]);

    private static Project CreateProject()
    {
        ProjectEnvironment environment = ProjectEnvironment.Create(
            Guid.CreateVersion7(),
            "Production",
            EnvironmentKind.Production,
            "https://console.example.com",
            null,
            null);

        return Project.Create(
            Guid.CreateVersion7(),
            "Console Ops",
            null,
            "Clinttttt",
            "Console-Ops",
            "main",
            null,
            [environment],
            Now.AddHours(-1));
    }

    private sealed class StubProjectRepository(Project? project, ProjectSaveOutcome saveOutcome) : IProjectRepository
    {
        public bool SaveCalled { get; private set; }

        public Task<ProjectRegistrationOutcome> TryAddAsync(Project candidate, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Project?> GetActiveByIdAsync(Guid projectId, CancellationToken cancellationToken) =>
            Task.FromResult(project?.Id == projectId ? project : null);

        public Task<ProjectSaveOutcome> SaveChangesAsync(Project candidate, CancellationToken cancellationToken)
        {
            SaveCalled = true;
            return Task.FromResult(saveOutcome);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
