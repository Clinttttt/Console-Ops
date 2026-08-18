using System.Net;
using System.Net.Http.Json;
using ConsoleOps.Api.Features.Projects;
using ConsoleOps.Application.Features.Dashboard.GetOverview;
using ConsoleOps.Application.Features.Projects;
using ConsoleOps.Application.Integrations.ApplicationMonitoring;
using ConsoleOps.Application.Integrations.GitHub;
using ConsoleOps.Tests.Integration.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ConsoleOps.Tests.Integration.Dashboard;

[Collection(ConsoleOpsApiCollection.Name)]
public sealed class GetDashboardOverviewTests(ConsoleOpsApiFactory factory)
{
    private const string CommitSha = "0123456789abcdef0123456789abcdef01234567";

    [Fact]
    public async Task Overview_ComposesLatestPersistedFactsWithoutCallingProviders()
    {
        DateTimeOffset observedAt = new(2025, 8, 14, 10, 0, 0, TimeSpan.Zero);
        MutableDashboardProbe probe = new(observedAt);
        DashboardGitHubReader gitHub = new(observedAt);
        using WebApplicationFactory<Program> application = CreateApplication(gitHub, probe);
        using HttpClient client = application.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        ProjectResponse project = await RegisterAsync(client);

        await RefreshAsync(client, project.Id);
        probe.SetProductionFailure(observedAt.AddMinutes(1));
        gitHub.ObservedAtUtc = observedAt.AddMinutes(1);
        await RefreshAsync(client, project.Id);
        int providerCallsAfterRefresh = probe.CallCount + gitHub.CallCount;

        DashboardOverviewResponse overview = await GetOverviewAsync(client);

        Assert.Equal(providerCallsAfterRefresh, probe.CallCount + gitHub.CallCount);
        DashboardProjectSurfaceResponse[] surfaces = overview.Projects
            .Where(surface => surface.Id == project.Id)
            .ToArray();
        Assert.Equal(2, surfaces.Length);
        Assert.Equal(2, surfaces.Select(surface => surface.Environment.Id).Distinct().Count());

        DashboardProjectSurfaceResponse production = Assert.Single(
            surfaces,
            surface => surface.Environment.Kind == "production");
        Assert.Equal("down", production.Health.Level);
        Assert.Equal("Unhealthy", production.Health.Label);
        Assert.Equal(120, production.Response.Milliseconds);
        Assert.Equal([100d, 120d], production.Response.Samples);
        Assert.Equal(CommitSha, production.Source.CommitSha);
        Assert.Equal("inSync", production.VersionSync.State);
        Assert.Equal(CommitSha, production.DeployedVersion?.CommitSha);

        DashboardSystemStateColumnResponse[] columns = overview.SystemState.Columns
            .Where(column => column.ProjectId == project.Id)
            .ToArray();
        Assert.Equal(2, columns.Length);
        Assert.Equal(2, columns.Select(column => column.EnvironmentId).Distinct().Count());
        Assert.Contains(overview.SystemState.Rows, row => row.Key == "dependency:database");
        Assert.Contains(overview.SystemState.Rows, row => row.Key == "dependency:payments");
        Assert.Equal("down", overview.Summary.Level);
        Assert.Null(overview.Summary.Uptime);
        Assert.Contains(overview.Activity, activity =>
            activity.Kind == "healthFailed"
            && activity.Title == $"{project.Name} health failed"
            && activity.Context == "Production");

        await UpdateAsync(client, project);
        DashboardOverviewResponse afterConfigurationChange = await GetOverviewAsync(client);
        DashboardProjectSurfaceResponse[] invalidated = afterConfigurationChange.Projects
            .Where(surface => surface.Id == project.Id)
            .ToArray();
        Assert.All(invalidated, surface =>
        {
            Assert.Null(surface.Source.CommitSha);
            Assert.Equal("unknown", surface.Workflow.State);
            Assert.Equal("Unknown", surface.Health.Label);
            Assert.Null(surface.HealthObservedAt);
            Assert.Null(surface.DeployedVersion);
            Assert.Equal("unknown", surface.VersionSync.State);
            Assert.Empty(surface.Response.Samples);
        });

        HttpResponseMessage archive = await client.DeleteAsync($"/api/projects/{project.Id}");
        Assert.Equal(HttpStatusCode.NoContent, archive.StatusCode);
        DashboardOverviewResponse afterArchive = await GetOverviewAsync(client);
        Assert.DoesNotContain(afterArchive.Projects, surface => surface.Id == project.Id);
        Assert.DoesNotContain(afterArchive.Activity, activity => activity.Title.StartsWith(project.Name));
    }

    private WebApplicationFactory<Program> CreateApplication(
        IGitHubProjectReader gitHub,
        IApplicationProbe probe) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IGitHubProjectReader>();
            services.RemoveAll<IApplicationProbe>();
            services.AddSingleton(gitHub);
            services.AddSingleton(probe);
        }));

    private static async Task<ProjectResponse> RegisterAsync(HttpClient client)
    {
        string unique = Guid.NewGuid().ToString("N");
        RegisterProjectRequest request = new(
            $"Dashboard Project {unique}",
            "Dashboard integration test",
            new ProjectRepositoryRequest("console-ops-tests", $"dashboard-{unique}", "main", "ci.yml"),
            [
                new RegisterProjectEnvironmentRequest(
                    "Production",
                    "production",
                    "https://production.example",
                    "https://production.example/health",
                    "https://production.example/version"),
                new RegisterProjectEnvironmentRequest(
                    "Staging",
                    "staging",
                    "https://staging.example",
                    "https://staging.example/health",
                    "https://staging.example/version")
            ]);
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/projects", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return Assert.IsType<ProjectResponse>(await response.Content.ReadFromJsonAsync<ProjectResponse>());
    }

    private static async Task UpdateAsync(HttpClient client, ProjectResponse project)
    {
        UpdateProjectRequest request = new(
            project.ConfigurationVersion,
            project.Name,
            "Updated after observations were recorded",
            new ProjectRepositoryRequest(
                project.Repository.Owner,
                project.Repository.Name,
                project.Repository.DefaultBranch,
                project.Repository.WorkflowFile),
            project.Environments.Select(environment => new UpdateProjectEnvironmentRequest(
                environment.Id,
                environment.Name,
                environment.Kind,
                environment.ApplicationUrl,
                environment.HealthUrl,
                environment.VersionUrl)).ToArray());
        HttpResponseMessage response = await client.PutAsJsonAsync($"/api/projects/{project.Id}", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task RefreshAsync(HttpClient client, Guid projectId)
    {
        HttpResponseMessage response = await client.PostAsync($"/api/projects/{projectId}/refresh", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<DashboardOverviewResponse> GetOverviewAsync(HttpClient client)
    {
        HttpResponseMessage response = await client.GetAsync("/api/dashboard/overview");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return Assert.IsType<DashboardOverviewResponse>(
            await response.Content.ReadFromJsonAsync<DashboardOverviewResponse>());
    }

    private sealed class DashboardGitHubReader(DateTimeOffset observedAtUtc) : IGitHubProjectReader
    {
        public int CallCount { get; private set; }
        public DateTimeOffset ObservedAtUtc { get; set; } = observedAtUtc;

        public Task<GitHubProjectReadResult> ReadAsync(
            GitHubProjectReference project,
            IReadOnlyCollection<string> deployedCommitShas,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(new GitHubProjectReadResult(
                GitHubFactResult<GitHubSourceObservation>.Success(new GitHubSourceObservation(
                    $"{project.Owner}/{project.Repository}",
                    project.DefaultBranch,
                    CommitSha,
                    CommitSha[..7],
                    ObservedAtUtc.AddMinutes(-5),
                    ObservedAtUtc)),
                GitHubFactResult<GitHubWorkflowObservation>.Success(new GitHubWorkflowObservation(
                    project.WorkflowFile,
                    "CI",
                    GitHubWorkflowState.Passed,
                    CommitSha,
                    ObservedAtUtc.AddMinutes(-3),
                    ObservedAtUtc.AddMinutes(-1),
                    ObservedAtUtc)),
                [],
                []));
        }
    }

    private sealed class MutableDashboardProbe(DateTimeOffset observedAtUtc) : IApplicationProbe
    {
        private DateTimeOffset _observedAtUtc = observedAtUtc;
        private bool _productionFailed;
        private int _callCount;

        public int CallCount => _callCount;

        public Task<ApplicationProbeResult> ProbeAsync(
            ApplicationProbeTarget target,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _callCount);
            bool production = target.HealthUrl?.Contains("production", StringComparison.Ordinal) == true;
            ApplicationHealthState state = production && _productionFailed
                ? ApplicationHealthState.Unhealthy
                : ApplicationHealthState.Healthy;
            double milliseconds = _productionFailed ? 120 : 100;
            string dependencyName = production ? "Database" : "Payments";

            return Task.FromResult(new ApplicationProbeResult(
                new ApplicationHealthObservation(
                    state,
                    TimeSpan.FromMilliseconds(milliseconds),
                    _observedAtUtc,
                    [new DependencyHealthObservation(dependencyName, ApplicationHealthState.Healthy)]),
                new ApplicationVersionObservation(
                    ApplicationVersionState.Available,
                    "ConsoleOps.TestApi",
                    "1.0.0",
                    CommitSha,
                    production ? "Production" : "Staging",
                    _observedAtUtc.AddHours(-1),
                    _observedAtUtc)));
        }

        public void SetProductionFailure(DateTimeOffset observedAtUtc)
        {
            _observedAtUtc = observedAtUtc;
            _productionFailed = true;
        }
    }
}
