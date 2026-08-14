using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ConsoleOps.Api.Features.Projects;
using ConsoleOps.Application.Features.Projects;
using ConsoleOps.Application.Features.Projects.RegisterProject;
using ConsoleOps.Infrastructure.Persistence;
using ConsoleOps.Tests.Integration.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ConsoleOps.Tests.Integration.Projects;

[Collection(ConsoleOpsApiCollection.Name)]
public sealed class RegisterProjectTests(ConsoleOpsApiFactory factory)
{
    [Fact]
    public async Task Post_WithValidProject_PersistsAggregateAndReturnsCreated()
    {
        RegisterProjectRequest request = CreateRequest("Console Ops", "Console-Ops");
        using HttpClient client = CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/projects", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        ProjectResponse body = Assert.IsType<ProjectResponse>(
            await response.Content.ReadFromJsonAsync<ProjectResponse>());
        Assert.Equal($"/api/projects/{body.Id}", response.Headers.Location?.OriginalString);
        Assert.Equal("production", Assert.Single(body.Environments).Kind);

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ConsoleOpsDbContext dbContext = scope.ServiceProvider.GetRequiredService<ConsoleOpsDbContext>();
        var persisted = await dbContext.Projects
            .AsNoTracking()
            .Include(project => project.Environments)
            .SingleAsync(project => project.Id == body.Id);

        Assert.Equal("CONSOLE OPS", persisted.NormalizedName);
        Assert.Single(persisted.Environments);
    }

    [Fact]
    public async Task Post_WithInvalidUrl_ReturnsValidationProblem()
    {
        RegisterProjectRequest request = CreateRequest("Invalid URL Project", "invalid-url") with
        {
            Environments =
            [
                new RegisterProjectEnvironmentRequest(
                    "Production",
                    "production",
                    "https://user:password@example.com",
                    null,
                    null)
            ]
        };
        using HttpClient client = CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/projects", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        HttpValidationProblemDetails problem = Assert.IsType<HttpValidationProblemDetails>(
            await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>());
        Assert.Contains("environments[0].ApplicationUrl", problem.Errors.Keys);
        Assert.True(problem.Extensions.ContainsKey("traceId"));
    }

    [Fact]
    public async Task Post_WithDuplicateName_ReturnsConflictProblem()
    {
        RegisterProjectRequest first = CreateRequest("Duplicate Project", "first-repository");
        RegisterProjectRequest duplicate = CreateRequest(" duplicate project ", "second-repository");
        using HttpClient client = CreateClient();
        HttpResponseMessage firstResponse = await client.PostAsJsonAsync("/api/projects", first);

        HttpResponseMessage duplicateResponse = await client.PostAsJsonAsync("/api/projects", duplicate);

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);
        ProblemDetails problem = Assert.IsType<ProblemDetails>(
            await duplicateResponse.Content.ReadFromJsonAsync<ProblemDetails>());
        JsonElement code = Assert.IsType<JsonElement>(problem.Extensions["code"]);
        Assert.Equal("Projects.DuplicateName", code.GetString());
        Assert.True(problem.Extensions.ContainsKey("traceId"));
    }

    [Fact]
    public async Task Post_WithMalformedJson_ReturnsSafeBadRequestProblem()
    {
        using HttpClient client = CreateClient();
        using StringContent malformedBody = new("{ not-json", Encoding.UTF8, "application/json");

        HttpResponseMessage response = await client.PostAsync("/api/projects", malformedBody);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        ProblemDetails problem = Assert.IsType<ProblemDetails>(
            await response.Content.ReadFromJsonAsync<ProblemDetails>());
        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
        Assert.True(problem.Extensions.ContainsKey("traceId"));
    }

    private HttpClient CreateClient() => factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost")
    });

    private static RegisterProjectRequest CreateRequest(string name, string repositoryName) => new(
        name,
        "Deployment control center",
        new ProjectRepositoryRequest("Clinttttt", repositoryName, "main", "ci.yml"),
        [
            new RegisterProjectEnvironmentRequest(
                "Production",
                "production",
                "https://console.example.com",
                "https://console.example.com/health",
                "https://console.example.com/version")
        ]);
}
