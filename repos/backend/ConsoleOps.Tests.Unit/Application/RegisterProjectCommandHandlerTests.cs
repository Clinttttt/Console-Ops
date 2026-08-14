using ConsoleOps.Application.Abstractions.Persistence;
using ConsoleOps.Application.Features.Projects.RegisterProject;
using ConsoleOps.Domain.Projects;

namespace ConsoleOps.Tests.Unit.Application;

public sealed class RegisterProjectCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 14, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Handle_WhenRepositoryAcceptsProject_ReturnsCreatedProjection()
    {
        StubProjectRepository repository = new(ProjectRegistrationOutcome.Added);
        RegisterProjectCommandHandler handler = new(repository, new FixedTimeProvider(Now));

        var result = await handler.Handle(CreateCommand(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Console Ops", result.Value.Name);
        Assert.Equal(Now, result.Value.CreatedAtUtc);
        Assert.Equal("production", Assert.Single(result.Value.Environments).Kind);
        Assert.NotNull(repository.Project);
    }

    [Fact]
    public async Task Handle_WhenNameExists_ReturnsConflictResult()
    {
        StubProjectRepository repository = new(ProjectRegistrationOutcome.DuplicateName);
        RegisterProjectCommandHandler handler = new(repository, new FixedTimeProvider(Now));

        var result = await handler.Handle(CreateCommand(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(RegisterProjectErrors.DuplicateName, result.Error);
    }

    private static RegisterProjectCommand CreateCommand() => new(
        "Console Ops",
        "Deployment control center",
        new RegisterProjectRepository("Clinttttt", "Console-Ops", "main", "ci.yml"),
        [new RegisterProjectEnvironment(
            "Production",
            "production",
            "https://console.example.com",
            "https://console.example.com/health",
            "https://console.example.com/version")]);

    private sealed class StubProjectRepository(ProjectRegistrationOutcome outcome) : IProjectRepository
    {
        public Project? Project { get; private set; }

        public Task<ProjectRegistrationOutcome> TryAddAsync(Project project, CancellationToken cancellationToken)
        {
            Project = project;
            return Task.FromResult(outcome);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
