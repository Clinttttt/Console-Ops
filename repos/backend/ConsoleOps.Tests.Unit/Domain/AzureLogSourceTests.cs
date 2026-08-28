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

    /// <summary>
    /// A site name a container app rule would refuse. Reusing that rule would have made real App Service sites
    /// unregisterable, which is why the domain asks per platform.
    /// </summary>
    [Theory]
    [InlineData("StallTrack-API-2026")]
    [InlineData("stalltrack-api-cly-2026-with-a-considerably-longer-name-x")]
    public void A_site_name_is_valid_for_App_Service_and_not_for_a_container_app(string name)
    {
        Assert.True(AzureLogSource.IsValidResourceName(name, AzureLogPlatform.AppService));
        Assert.False(AzureLogSource.IsValidResourceName(name, AzureLogPlatform.ContainerApp));
    }

    [Fact]
    public void A_source_keeps_the_platform_it_was_created_with()
    {
        AzureLogSource source = AzureLogSource.Create(
            Guid.Parse("2e0a9e91-b9f5-4b6a-a4b3-aa423cc37c09"),
            "StallTrack-API-2026",
            AzureLogPlatform.AppService)!;

        Assert.Equal(AzureLogPlatform.AppService, source.Platform);
        Assert.Equal("StallTrack-API-2026", source.ContainerAppName);
    }

    /// <summary>Nothing is stored that could not be queried: a name invalid for its platform is refused.</summary>
    [Fact]
    public void A_container_app_source_still_refuses_a_site_name()
    {
        ArgumentException failure = Assert.Throws<ArgumentException>(() =>
            AzureLogSource.Create(Guid.NewGuid(), "StallTrack-API-2026", AzureLogPlatform.ContainerApp));

        Assert.Contains("lower-case", failure.Message, StringComparison.Ordinal);
    }

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
