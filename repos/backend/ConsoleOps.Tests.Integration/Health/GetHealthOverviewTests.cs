using System.Net;
using System.Net.Http.Json;
using ConsoleOps.Api.Features.Projects;
using ConsoleOps.Application.Features.Health.GetOverview;
using ConsoleOps.Application.Features.Projects;
using ConsoleOps.Tests.Integration.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ConsoleOps.Tests.Integration.Health;

/// <summary>
/// What the Health screen is told. These tests pin the distinctions the screen exists to make: an environment
/// nobody has checked is not healthy, a window with too few checks has no figure, and a verdict is only ever a
/// recorded observation.
/// </summary>
[Collection(ConsoleOpsApiCollection.Name)]
public sealed class GetHealthOverviewTests(ConsoleOpsApiFactory factory)
{
    [Fact]
    public async Task Overview_ListsAnEnvironmentThatHasNeverBeenChecked_WithoutInventingAVerdict()
    {
        using HttpClient client = CreateClient();
        ProjectResponse project = await RegisterAsync(client);

        HealthOverviewResponse overview = await ReadAsync(client);

        EnvironmentHealthResponse environment = Assert.Single(
            overview.Environments,
            entry => entry.ProjectId == project.Id);
        // No check has run, so there is no verdict and none is guessed at.
        Assert.Equal("unknown", environment.State);
        Assert.Null(environment.CheckedAt);
        Assert.Empty(environment.Checks);
        Assert.Null(environment.HealthySince);
        Assert.Equal(0, environment.ConsecutiveFailures);
        // Unknown counts in neither column: it is not evidence of being up or down.
        Assert.DoesNotContain(
            overview.Environments.Where(entry => entry.ProjectId == project.Id),
            entry => entry.State is "healthy" or "unhealthy");
    }

    [Fact]
    public async Task Overview_ReportsNoAvailabilityFigureBelowTheMinimumSample()
    {
        using HttpClient client = CreateClient();
        ProjectResponse project = await RegisterAsync(client);

        HealthOverviewResponse overview = await ReadAsync(client);

        EnvironmentHealthResponse environment = Assert.Single(
            overview.Environments,
            entry => entry.ProjectId == project.Id);
        // A percentage from a handful of checks would be the most misleading number on the screen.
        Assert.Null(environment.Window.AvailabilityPercentage);
        Assert.Null(environment.Window.FailedChecks);
    }

    [Fact]
    public async Task Overview_SummaryAgreesWithTheEnvironmentsItReturned()
    {
        using HttpClient client = CreateClient();
        await RegisterAsync(client);

        HealthOverviewResponse overview = await ReadAsync(client);

        Assert.NotEqual(default, overview.ObservedAt);
        // Asserted as an invariant rather than against fixed counts: the suite shares one database, so a test
        // that expects a globally empty instance is a test that fails when run beside anything else.
        Assert.Equal(
            overview.Environments.Count(entry => entry.State is "healthy"),
            overview.Summary.Healthy);
        Assert.Equal(
            overview.Environments.Count(entry => entry.State is "degraded"),
            overview.Summary.Degraded);
        Assert.Equal(
            overview.Environments.Count(entry => entry.State is "unhealthy" or "unreachable"),
            overview.Summary.Down);
        // An environment nobody has checked is counted in none of them.
        Assert.DoesNotContain(
            overview.Environments.Where(entry => entry.State == "unknown"),
            entry => entry.CheckedAt is not null);
    }

    private HttpClient CreateClient() =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
        });

    private static async Task<HealthOverviewResponse> ReadAsync(HttpClient client)
    {
        HttpResponseMessage response = await client.GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return Assert.IsType<HealthOverviewResponse>(
            await response.Content.ReadFromJsonAsync<HealthOverviewResponse>());
    }

    private static async Task<ProjectResponse> RegisterAsync(HttpClient client)
    {
        string unique = Guid.NewGuid().ToString("N");
        RegisterProjectRequest request = new(
            $"Health {unique}",
            "Health overview integration test",
            new ProjectRepositoryRequest("console-ops-tests", $"health-{unique}", "main", "deploy.yml"),
            [new RegisterProjectEnvironmentRequest("Production", "production", null, null, null, null)]);

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/projects", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return Assert.IsType<ProjectResponse>(await response.Content.ReadFromJsonAsync<ProjectResponse>());
    }
}
