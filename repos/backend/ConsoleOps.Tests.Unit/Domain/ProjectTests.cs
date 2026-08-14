using ConsoleOps.Domain.Projects;

namespace ConsoleOps.Tests.Unit.Domain;

public sealed class ProjectTests
{
    [Fact]
    public void Create_WithValidConfiguration_CreatesProjectAndNormalizesUniquenessKeys()
    {
        ProjectEnvironment environment = CreateEnvironment("Production");

        Project project = Project.Create(
            Guid.CreateVersion7(),
            " Console Ops ",
            " Deployment control center ",
            " Clinttttt ",
            " Console-Ops ",
            " main ",
            " ci.yml ",
            [environment],
            DateTimeOffset.UtcNow);

        Assert.Equal("Console Ops", project.Name);
        Assert.Equal("CONSOLE OPS", project.NormalizedName);
        Assert.Equal("CLINTTTTT", project.NormalizedRepositoryOwner);
        Assert.Equal("CONSOLE-OPS", project.NormalizedRepositoryName);
        Assert.Single(project.Environments);
    }

    [Fact]
    public void Create_WithDuplicateEnvironmentNames_Throws()
    {
        ProjectEnvironment production = CreateEnvironment("Production");
        ProjectEnvironment duplicate = CreateEnvironment(" production ");

        ArgumentException exception = Assert.Throws<ArgumentException>(() => Project.Create(
            Guid.CreateVersion7(),
            "Console Ops",
            null,
            "Clinttttt",
            "Console-Ops",
            "main",
            null,
            [production, duplicate],
            DateTimeOffset.UtcNow));

        Assert.Contains("unique", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Environment_WithEmbeddedCredentials_Throws()
    {
        Assert.Throws<ArgumentException>(() => ProjectEnvironment.Create(
            Guid.CreateVersion7(),
            "Production",
            EnvironmentKind.Production,
            "https://user:password@example.com",
            null,
            null));
    }

    [Fact]
    public void Environment_WithUndefinedKind_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ProjectEnvironment.Create(
            Guid.CreateVersion7(),
            "Production",
            (EnvironmentKind)999,
            null,
            null,
            null));
    }

    [Fact]
    public void UpdateConfiguration_ReconcilesEnvironmentsAndAdvancesVersion()
    {
        ProjectEnvironment production = CreateEnvironment("Production");
        Project project = CreateProject(production);
        DateTimeOffset updatedAt = DateTimeOffset.UtcNow.AddMinutes(1);
        ProjectEnvironment updatedProduction = ProjectEnvironment.Create(
            production.Id,
            "Primary",
            EnvironmentKind.Production,
            "https://new.example.com",
            null,
            null);
        ProjectEnvironment staging = ProjectEnvironment.Create(
            Guid.CreateVersion7(),
            "Staging",
            EnvironmentKind.Staging,
            "https://staging.example.com",
            null,
            null);

        project.UpdateConfiguration(
            "Console Ops API",
            null,
            "Clinttttt",
            "Console-Ops",
            "develop",
            null,
            [updatedProduction, staging],
            updatedAt);

        Assert.Equal(2, project.ConfigurationVersion);
        Assert.Equal(updatedAt, project.UpdatedAtUtc);
        Assert.Equal("Console Ops API", project.Name);
        Assert.Contains(project.Environments, environment =>
            environment.Id == production.Id && environment.Name == "Primary");
        Assert.Contains(project.Environments, environment => environment.Id == staging.Id);
    }

    [Fact]
    public void Archive_MarksProjectArchivedAndAdvancesVersion()
    {
        Project project = CreateProject(CreateEnvironment("Production"));
        DateTimeOffset archivedAt = DateTimeOffset.UtcNow.AddMinutes(1);

        project.Archive(archivedAt);

        Assert.True(project.IsArchived);
        Assert.Equal(archivedAt, project.ArchivedAtUtc);
        Assert.Equal(archivedAt, project.UpdatedAtUtc);
        Assert.Equal(2, project.ConfigurationVersion);
    }

    private static Project CreateProject(ProjectEnvironment environment) => Project.Create(
        Guid.CreateVersion7(),
        "Console Ops",
        null,
        "Clinttttt",
        "Console-Ops",
        "main",
        null,
        [environment],
        DateTimeOffset.UtcNow);

    private static ProjectEnvironment CreateEnvironment(string name) => ProjectEnvironment.Create(
        Guid.CreateVersion7(),
        name,
        EnvironmentKind.Production,
        "https://console.example.com",
        "https://console.example.com/health",
        "https://console.example.com/version");
}
