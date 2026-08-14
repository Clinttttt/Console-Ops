using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ConsoleOps.Api.Features.Projects;
using ConsoleOps.Application.Features.Projects;
using ConsoleOps.Infrastructure.Persistence;
using ConsoleOps.Tests.Integration.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ConsoleOps.Tests.Integration.Projects;

[Collection(ConsoleOpsApiCollection.Name)]
public sealed class ProjectCatalogTests(ConsoleOpsApiFactory factory)
{
    [Fact]
    public async Task GetAndList_ReturnActiveProjectConfiguration()
    {
        using HttpClient client = CreateClient();
        ProjectResponse registered = await RegisterAsync(client, Unique("Catalog Read"), Unique("catalog-read"));

        HttpResponseMessage getResponse = await client.GetAsync($"/api/projects/{registered.Id}");
        ProjectResponse[] projects = Assert.IsType<ProjectResponse[]>(
            await client.GetFromJsonAsync<ProjectResponse[]>("/api/projects"));

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        ProjectResponse detail = Assert.IsType<ProjectResponse>(
            await getResponse.Content.ReadFromJsonAsync<ProjectResponse>());
        Assert.Equal(registered.Id, detail.Id);
        Assert.Equal(1, detail.ConfigurationVersion);
        Assert.Contains(projects, project => project.Id == registered.Id);
    }

    [Fact]
    public async Task Put_WithCurrentVersion_UpdatesConfigurationAndRejectsStaleRetry()
    {
        using HttpClient client = CreateClient();
        string repositoryName = Unique("catalog-update");
        ProjectResponse registered = await RegisterAsync(client, Unique("Catalog Update"), repositoryName);
        Guid productionId = Assert.Single(registered.Environments).Id;
        UpdateProjectRequest request = new(
            registered.ConfigurationVersion,
            Unique("Catalog Updated"),
            "Updated project configuration",
            new ProjectRepositoryRequest("console-ops-tests", repositoryName, "develop", "deploy.yml"),
            [
                new UpdateProjectEnvironmentRequest(
                    productionId,
                    "Primary",
                    "production",
                    "https://updated.example.com",
                    "https://updated.example.com/health",
                    null),
                new UpdateProjectEnvironmentRequest(
                    null,
                    "Staging",
                    "staging",
                    "https://staging.example.com",
                    null,
                    null)
            ]);

        HttpResponseMessage updateResponse = await client.PutAsJsonAsync(
            $"/api/projects/{registered.Id}",
            request);
        HttpResponseMessage staleResponse = await client.PutAsJsonAsync(
            $"/api/projects/{registered.Id}",
            request);

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        ProjectResponse updated = Assert.IsType<ProjectResponse>(
            await updateResponse.Content.ReadFromJsonAsync<ProjectResponse>());
        Assert.Equal(2, updated.ConfigurationVersion);
        Assert.NotNull(updated.UpdatedAtUtc);
        Assert.Contains(updated.Environments, environment =>
            environment.Id == productionId && environment.Name == "Primary");
        Assert.Contains(updated.Environments, environment => environment.Name == "Staging");

        Assert.Equal(HttpStatusCode.Conflict, staleResponse.StatusCode);
        ProblemDetails problem = Assert.IsType<ProblemDetails>(
            await staleResponse.Content.ReadFromJsonAsync<ProblemDetails>());
        JsonElement code = Assert.IsType<JsonElement>(problem.Extensions["code"]);
        Assert.Equal("Projects.ConfigurationConflict", code.GetString());

        ProjectEnvironmentResponse staging = Assert.Single(
            updated.Environments,
            environment => environment.Name == "Staging");
        UpdateProjectRequest removeProductionRequest = request with
        {
            ConfigurationVersion = updated.ConfigurationVersion,
            Environments =
            [
                new UpdateProjectEnvironmentRequest(
                    staging.Id,
                    staging.Name,
                    staging.Kind,
                    staging.ApplicationUrl,
                    staging.HealthUrl,
                    staging.VersionUrl)
            ]
        };
        HttpResponseMessage removeResponse = await client.PutAsJsonAsync(
            $"/api/projects/{registered.Id}",
            removeProductionRequest);
        Assert.Equal(HttpStatusCode.OK, removeResponse.StatusCode);
        ProjectResponse afterRemoval = Assert.IsType<ProjectResponse>(
            await removeResponse.Content.ReadFromJsonAsync<ProjectResponse>());
        Assert.Equal(3, afterRemoval.ConfigurationVersion);
        Assert.Equal(staging.Id, Assert.Single(afterRemoval.Environments).Id);

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ConsoleOpsDbContext dbContext = scope.ServiceProvider.GetRequiredService<ConsoleOpsDbContext>();
        var persisted = await dbContext.Projects
            .AsNoTracking()
            .Include(project => project.Environments)
            .SingleAsync(project => project.Id == registered.Id);
        Assert.Single(persisted.Environments);
        Assert.Equal(staging.Id, Assert.Single(persisted.Environments).Id);
        Assert.Equal(3, persisted.ConfigurationVersion);
    }

    [Fact]
    public async Task Delete_ArchivesProjectAndReleasesActiveUniqueness()
    {
        using HttpClient client = CreateClient();
        string name = Unique("Catalog Archive");
        string repositoryName = Unique("catalog-archive");
        RegisterProjectRequest request = CreateRegistration(name, repositoryName);
        ProjectResponse registered = await RegisterAsync(client, request);

        HttpResponseMessage deleteResponse = await client.DeleteAsync($"/api/projects/{registered.Id}");
        HttpResponseMessage getResponse = await client.GetAsync($"/api/projects/{registered.Id}");
        ProjectResponse[] projects = Assert.IsType<ProjectResponse[]>(
            await client.GetFromJsonAsync<ProjectResponse[]>("/api/projects"));
        HttpResponseMessage reregisterResponse = await client.PostAsJsonAsync("/api/projects", request);

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
        Assert.DoesNotContain(projects, project => project.Id == registered.Id);
        Assert.Equal(HttpStatusCode.Created, reregisterResponse.StatusCode);
    }

    private HttpClient CreateClient() => factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost")
    });

    private static Task<ProjectResponse> RegisterAsync(HttpClient client, string name, string repositoryName) =>
        RegisterAsync(client, CreateRegistration(name, repositoryName));

    private static async Task<ProjectResponse> RegisterAsync(HttpClient client, RegisterProjectRequest request)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/projects", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return Assert.IsType<ProjectResponse>(await response.Content.ReadFromJsonAsync<ProjectResponse>());
    }

    private static RegisterProjectRequest CreateRegistration(string name, string repositoryName) => new(
        name,
        "Project catalog integration test",
        new ProjectRepositoryRequest("console-ops-tests", repositoryName, "main", "ci.yml"),
        [
            new RegisterProjectEnvironmentRequest(
                "Production",
                "production",
                "https://catalog.example.com",
                "https://catalog.example.com/health",
                "https://catalog.example.com/version")
        ]);

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}";
}
