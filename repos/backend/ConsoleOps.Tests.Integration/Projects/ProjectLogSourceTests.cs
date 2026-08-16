using System.Net;
using System.Net.Http.Json;
using ConsoleOps.Api.Features.Projects;
using ConsoleOps.Application.Features.Projects;
using ConsoleOps.Tests.Integration.Infrastructure;

namespace ConsoleOps.Tests.Integration.Projects;

/// <summary>
/// An environment may say where its logs are read from. The source is optional, both parts are required
/// together, and it never carries a credential.
/// </summary>
[Collection(ConsoleOpsApiCollection.Name)]
public sealed class ProjectLogSourceTests(ConsoleOpsApiFactory factory)
{
    private static readonly Guid Workspace = Guid.Parse("6f5c1a2b-3d4e-5f60-7182-93a4b5c6d7e8");

    [Fact]
    public async Task Register_StoresTheLogSourceAndReadsItBack()
    {
        using HttpClient client = CreateClient();

        ProjectResponse project = await RegisterAsync(
            client,
            new ProjectLogSourceRequest(Workspace, "spinner-api"));

        ProjectEnvironmentResponse environment = Assert.Single(project.Environments);
        Assert.Equal("azureContainerApps", environment.LogSource?.Provider);
        Assert.Equal(Workspace, environment.LogSource?.WorkspaceId);
        Assert.Equal("spinner-api", environment.LogSource?.ContainerAppName);

        ProjectResponse reread = Assert.IsType<ProjectResponse>(
            await client.GetFromJsonAsync<ProjectResponse>($"/api/projects/{project.Id}"));
        Assert.Equal(
            "spinner-api",
            Assert.Single(reread.Environments).LogSource?.ContainerAppName);
    }

    [Fact]
    public async Task Register_WithoutALogSource_ReportsNoneRatherThanAnEmptyOne()
    {
        using HttpClient client = CreateClient();

        ProjectResponse project = await RegisterAsync(client, logSource: null);

        Assert.Null(Assert.Single(project.Environments).LogSource);
    }

    [Fact]
    public async Task Update_AddsAndThenClearsTheLogSource()
    {
        using HttpClient client = CreateClient();
        ProjectResponse project = await RegisterAsync(client, logSource: null);
        ProjectEnvironmentResponse environment = Assert.Single(project.Environments);

        ProjectResponse configured = await UpdateAsync(
            client,
            project,
            environment,
            new ProjectLogSourceRequest(Workspace, "stalltrack-api"));
        Assert.Equal(
            "stalltrack-api",
            Assert.Single(configured.Environments).LogSource?.ContainerAppName);

        ProjectResponse cleared = await UpdateAsync(
            client,
            configured,
            Assert.Single(configured.Environments),
            logSource: null);
        // Removing the source is an edit, not an omission to ignore.
        Assert.Null(Assert.Single(cleared.Environments).LogSource);
    }

    [Theory]
    [InlineData(null, "spinner-api")]
    [InlineData("6f5c1a2b-3d4e-5f60-7182-93a4b5c6d7e8", null)]
    [InlineData("6f5c1a2b-3d4e-5f60-7182-93a4b5c6d7e8", "Spinner_API")]
    [InlineData("6f5c1a2b-3d4e-5f60-7182-93a4b5c6d7e8", "spinner--api")]
    public async Task Register_WithAnUnusableLogSource_IsRefusedAtTheBoundary(
        string? workspaceId,
        string? containerAppName)
    {
        using HttpClient client = CreateClient();
        string unique = Guid.NewGuid().ToString("N");
        RegisterProjectRequest request = new(
            $"Log Source {unique}",
            "Log source integration test",
            new ProjectRepositoryRequest("console-ops-tests", $"logs-{unique}", "main", "deploy.yml"),
            [
                new RegisterProjectEnvironmentRequest(
                    "Production",
                    "production",
                    null,
                    null,
                    null,
                    new ProjectLogSourceRequest(
                        workspaceId is null ? null : Guid.Parse(workspaceId),
                        containerAppName))
            ]);

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/projects", request);

        // Half a source, or a name Azure would not accept, is a validation problem rather than a fault.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private HttpClient CreateClient() =>
        factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

    private static async Task<ProjectResponse> RegisterAsync(
        HttpClient client,
        ProjectLogSourceRequest? logSource)
    {
        string unique = Guid.NewGuid().ToString("N");
        RegisterProjectRequest request = new(
            $"Log Source {unique}",
            "Log source integration test",
            new ProjectRepositoryRequest("console-ops-tests", $"logs-{unique}", "main", "deploy.yml"),
            [
                new RegisterProjectEnvironmentRequest(
                    "Production",
                    "production",
                    "https://application.example",
                    "https://application.example/health",
                    "https://application.example/version",
                    logSource)
            ]);
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/projects", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return Assert.IsType<ProjectResponse>(await response.Content.ReadFromJsonAsync<ProjectResponse>());
    }

    private static async Task<ProjectResponse> UpdateAsync(
        HttpClient client,
        ProjectResponse project,
        ProjectEnvironmentResponse environment,
        ProjectLogSourceRequest? logSource)
    {
        UpdateProjectRequest request = new(
            project.ConfigurationVersion,
            project.Name,
            project.Description,
            new ProjectRepositoryRequest(
                project.Repository.Owner,
                project.Repository.Name,
                project.Repository.DefaultBranch,
                project.Repository.WorkflowFile),
            [
                new UpdateProjectEnvironmentRequest(
                    environment.Id,
                    environment.Name,
                    environment.Kind,
                    environment.ApplicationUrl,
                    environment.HealthUrl,
                    environment.VersionUrl,
                    logSource)
            ]);
        HttpResponseMessage response = await client.PutAsJsonAsync($"/api/projects/{project.Id}", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return Assert.IsType<ProjectResponse>(await response.Content.ReadFromJsonAsync<ProjectResponse>());
    }
}
