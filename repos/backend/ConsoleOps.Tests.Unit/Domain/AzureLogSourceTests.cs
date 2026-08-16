using ConsoleOps.Domain.Projects;

namespace ConsoleOps.Tests.Unit.Domain;

public sealed class AzureLogSourceTests
{
    private static readonly Guid Workspace = Guid.Parse("6f5c1a2b-3d4e-5f60-7182-93a4b5c6d7e8");

    [Fact]
    public void Create_WithNeitherPart_ReportsNoSource()
    {
        Assert.Null(AzureLogSource.Create(null, null));
        Assert.Null(AzureLogSource.Create(Guid.Empty, "   "));
    }

    [Fact]
    public void Create_WithBothParts_KeepsThemTrimmed()
    {
        AzureLogSource source = Assert.IsType<AzureLogSource>(
            AzureLogSource.Create(Workspace, "  spinner-api  "));

        Assert.Equal(Workspace, source.WorkspaceId);
        Assert.Equal("spinner-api", source.ContainerAppName);
    }

    [Fact]
    public void Create_WithOnlyAWorkspace_IsRefused()
    {
        // Half a source cannot be queried, so it must not be storable.
        ArgumentException failure = Assert.Throws<ArgumentException>(
            () => AzureLogSource.Create(Workspace, null));

        Assert.Equal("containerAppName", failure.ParamName);
    }

    [Fact]
    public void Create_WithOnlyAContainerApp_IsRefused()
    {
        ArgumentException failure = Assert.Throws<ArgumentException>(
            () => AzureLogSource.Create(null, "spinner-api"));

        Assert.Equal("workspaceId", failure.ParamName);
    }

    [Theory]
    [InlineData("spinner-api")]
    [InlineData("spinner-api-2")]
    [InlineData("ab")]
    [InlineData("a1")]
    public void IsValidContainerAppName_AcceptsAzureNames(string name) =>
        Assert.True(AzureLogSource.IsValidContainerAppName(name));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("Spinner-Api")]
    [InlineData("1spinner")]
    [InlineData("-spinner")]
    [InlineData("spinner-")]
    [InlineData("spinner--api")]
    [InlineData("spinner_api")]
    [InlineData("spinner api")]
    [InlineData("spinner.api")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public void IsValidContainerAppName_RefusesWhatAzureWouldNotAccept(string? name) =>
        Assert.False(AzureLogSource.IsValidContainerAppName(name));

    [Fact]
    public void Create_WithAnImpossibleName_IsRefused()
    {
        // The name reaches a provider query, so a value that cannot be a real app is never stored.
        ArgumentException failure = Assert.Throws<ArgumentException>(
            () => AzureLogSource.Create(Workspace, "Spinner API'; drop"));

        Assert.Equal("containerAppName", failure.ParamName);
    }

    [Fact]
    public void Environment_CarriesTheLogSourceThroughAnUpdate()
    {
        Guid environmentId = Guid.CreateVersion7();
        ProjectEnvironment environment = ProjectEnvironment.Create(
            environmentId,
            "Production",
            EnvironmentKind.Production,
            null,
            null,
            null,
            AzureLogSource.Create(Workspace, "spinner-api"));
        Project project = Project.Create(
            Guid.CreateVersion7(),
            "Spinner API",
            null,
            "owner",
            "spinner-api",
            "main",
            "deploy.yml",
            [environment],
            DateTimeOffset.UtcNow);

        Assert.Equal("spinner-api", project.Environments.Single().LogSource?.ContainerAppName);

        project.UpdateConfiguration(
            "Spinner API",
            null,
            "owner",
            "spinner-api",
            "main",
            "deploy.yml",
            [
                ProjectEnvironment.Create(
                    environmentId,
                    "Production",
                    EnvironmentKind.Production,
                    null,
                    null,
                    null,
                    null)
            ],
            DateTimeOffset.UtcNow);

        // Clearing the source is a real edit, not an omission to ignore.
        Assert.Null(project.Environments.Single().LogSource);
    }
}
