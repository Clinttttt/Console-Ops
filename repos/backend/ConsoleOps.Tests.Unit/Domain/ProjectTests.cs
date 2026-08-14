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

    private static ProjectEnvironment CreateEnvironment(string name) => ProjectEnvironment.Create(
        Guid.CreateVersion7(),
        name,
        EnvironmentKind.Production,
        "https://console.example.com",
        "https://console.example.com/health",
        "https://console.example.com/version");
}
