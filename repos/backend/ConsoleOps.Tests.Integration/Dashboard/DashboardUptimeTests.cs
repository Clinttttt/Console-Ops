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

/// <summary>
/// Availability is counted from recorded health checks, so it only appears once enough checks exist.
/// </summary>
[Collection(ConsoleOpsApiCollection.Name)]
public sealed class DashboardUptimeTests(ConsoleOpsApiFactory factory)
{
    private const string CommitSha = "0123456789abcdef0123456789abcdef01234567";

    [Fact]
    public async Task Overview_ReportsAvailabilityOnlyOnceEnoughChecksExist()
    {
        RecentProbe probe = new();
        UptimeGitHubReader gitHub = new();
        using WebApplicationFactory<Program> application = CreateApplication(gitHub, probe);
        using HttpClient client = application.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        ProjectResponse project = await RegisterAsync(client);

        await RefreshAsync(client, project.Id);
        DashboardOverviewResponse afterOneCheck = await GetOverviewAsync(client);

        // One check is not availability. It must read as unrecorded rather than 100%.
        Assert.Null(afterOneCheck.Summary.Uptime);

        // Enough checks to clear the minimum, one of which found the application unreachable.
        for (int index = 0; index < 12; index++)
        {
            probe.Healthy = index != 4;
            await RefreshAsync(client, project.Id);
        }

        DashboardOverviewResponse overview = await GetOverviewAsync(client);

        DashboardUptimeWindowResponse uptime =
            Assert.IsType<DashboardUptimeWindowResponse>(overview.Summary.Uptime);
        Assert.Equal(24, uptime.WindowHours);
        Assert.True(uptime.Checks >= 13, $"Expected at least 13 checks, got {uptime.Checks}.");
        // A recorded failure means the figure cannot be a clean 100%.
        Assert.InRange(uptime.Percentage, 1d, 99.9d);
        Assert.NotEmpty(uptime.Samples);
        Assert.All(uptime.Samples, sample => Assert.InRange(sample, 0d, 100d));
        Assert.True(uptime.Since <= DateTimeOffset.UtcNow);
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
            $"Uptime {unique}",
            "Uptime integration test",
            new ProjectRepositoryRequest("console-ops-tests", $"uptime-{unique}", "main", "ci.yml"),
            [
                new RegisterProjectEnvironmentRequest(
                    "Production",
                    "production",
                    "https://application.example",
                    "https://application.example/health",
                    "https://application.example/version")
            ]);
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/projects", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return Assert.IsType<ProjectResponse>(await response.Content.ReadFromJsonAsync<ProjectResponse>());
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

    /// <summary>Stamps observations at the current instant, so they fall inside the window.</summary>
    private sealed class RecentProbe : IApplicationProbe
    {
        public bool Healthy { get; set; } = true;

        public Task<ApplicationProbeResult> ProbeAsync(
            ApplicationProbeTarget target,
            CancellationToken cancellationToken)
        {
            DateTimeOffset observedAt = DateTimeOffset.UtcNow;
            return Task.FromResult(new ApplicationProbeResult(
                new ApplicationHealthObservation(
                    Healthy ? ApplicationHealthState.Healthy : ApplicationHealthState.Unreachable,
                    TimeSpan.FromMilliseconds(75),
                    observedAt,
                    []),
                new ApplicationVersionObservation(
                    ApplicationVersionState.Available,
                    "Uptime.Api",
                    "1.0.0",
                    CommitSha,
                    "Production",
                    observedAt.AddHours(-2),
                    observedAt)));
        }
    }

    private sealed class UptimeGitHubReader : IGitHubProjectReader
    {
        public Task<GitHubProjectReadResult> ReadAsync(
            GitHubProjectReference project,
            IReadOnlyCollection<string> deployedCommitShas,
            CancellationToken cancellationToken)
        {
            DateTimeOffset observedAt = DateTimeOffset.UtcNow;
            return Task.FromResult(new GitHubProjectReadResult(
                GitHubFactResult<GitHubSourceObservation>.Success(new GitHubSourceObservation(
                    $"{project.Owner}/{project.Repository}",
                    project.DefaultBranch,
                    CommitSha,
                    CommitSha[..7],
                    observedAt.AddMinutes(-10),
                    observedAt)),
                GitHubFactResult<GitHubWorkflowObservation>.Success(new GitHubWorkflowObservation(
                    project.WorkflowFile,
                    "CI",
                    GitHubWorkflowState.Passed,
                    CommitSha,
                    observedAt.AddMinutes(-8),
                    observedAt.AddMinutes(-6),
                    observedAt)),
                [],
                []));
        }
    }
}
