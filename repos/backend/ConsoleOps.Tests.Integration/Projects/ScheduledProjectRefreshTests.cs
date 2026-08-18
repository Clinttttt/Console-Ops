using System.Net;
using System.Net.Http.Json;
using ConsoleOps.Api.Features.Projects;
using ConsoleOps.Application.Features.Projects;
using ConsoleOps.Application.Integrations.ApplicationMonitoring;
using ConsoleOps.Application.Integrations.GitHub;
using ConsoleOps.Infrastructure.Persistence;
using ConsoleOps.Tests.Integration.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ConsoleOps.Tests.Integration.Projects;

/// <summary>
/// Scheduled collection: observations and release history appear without anyone calling the refresh
/// endpoint, and one project that cannot be read does not end the sweep.
/// </summary>
[Collection(ConsoleOpsApiCollection.Name)]
public sealed class ScheduledProjectRefreshTests(ConsoleOpsApiFactory factory)
{
    private const string CommitSha = "abcdef0123456789abcdef0123456789abcdef01";
    private static readonly TimeSpan SweepTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task Worker_RecordsObservationsAndReleasesWithoutAManualRefresh()
    {
        SweepGitHubReader gitHub = new();
        using WebApplicationFactory<Program> application = CreateApplication(gitHub, scheduled: true);
        using HttpClient client = CreateClient(application);
        ProjectResponse project = await RegisterAsync(client, "Scheduled");

        bool observed = await WaitForAsync(
            application,
            async dbContext => await dbContext.HealthObservations
                .AnyAsync(entity => entity.ProjectId == project.Id));

        Assert.True(observed, "The scheduled sweep did not record a health observation.");

        await using AsyncServiceScope scope = application.Services.CreateAsyncScope();
        ConsoleOpsDbContext dbContext = scope.ServiceProvider.GetRequiredService<ConsoleOpsDbContext>();
        // The sweep uses the same command as the button, so every fact of a refresh is present.
        Assert.True(await dbContext.SourceObservations.AnyAsync(entity => entity.ProjectId == project.Id));
        Assert.True(await dbContext.VersionObservations.AnyAsync(entity => entity.ProjectId == project.Id));
        // Release history is collected too, which is what closes the gap between manual visits.
        Assert.True(await dbContext.Deployments.AnyAsync(entity => entity.ProjectId == project.Id));
    }

    [Fact]
    public async Task Worker_WhenOneProjectCannotBeRead_StillRefreshesTheRest()
    {
        SweepGitHubReader gitHub = new();
        using WebApplicationFactory<Program> application = CreateApplication(gitHub, scheduled: true);
        using HttpClient client = CreateClient(application);
        // Projects are swept in name order, so the failing one is reached first.
        ProjectResponse failing = await RegisterAsync(client, "Aaa Failing", repositoryPrefix: "boom");
        ProjectResponse healthy = await RegisterAsync(client, "Zzz Healthy");

        bool observed = await WaitForAsync(
            application,
            async dbContext => await dbContext.HealthObservations
                .AnyAsync(entity => entity.ProjectId == healthy.Id));

        Assert.True(observed, "A failing project stopped the sweep.");
        Assert.True(gitHub.FailedReads > 0);

        await using AsyncServiceScope scope = application.Services.CreateAsyncScope();
        ConsoleOpsDbContext dbContext = scope.ServiceProvider.GetRequiredService<ConsoleOpsDbContext>();
        Assert.False(await dbContext.SourceObservations.AnyAsync(entity => entity.ProjectId == failing.Id));
    }

    [Fact]
    public async Task Worker_WhenDisabled_CollectsNothingOnItsOwn()
    {
        SweepGitHubReader gitHub = new();
        using WebApplicationFactory<Program> application = CreateApplication(gitHub, scheduled: false);
        using HttpClient client = CreateClient(application);
        ProjectResponse project = await RegisterAsync(client, "Manual Only");

        await Task.Delay(TimeSpan.FromSeconds(3));

        await using AsyncServiceScope scope = application.Services.CreateAsyncScope();
        ConsoleOpsDbContext dbContext = scope.ServiceProvider.GetRequiredService<ConsoleOpsDbContext>();
        Assert.False(await dbContext.HealthObservations.AnyAsync(entity => entity.ProjectId == project.Id));
    }

    private WebApplicationFactory<Program> CreateApplication(
        IGitHubProjectReader gitHub,
        bool scheduled) =>
        factory.WithWebHostBuilder(builder =>
        {
            if (scheduled)
            {
                builder.UseSetting("Monitoring:Refresh:Enabled", "true");
                // The host starts before a test can register anything, so the first sweep is delayed
                // just long enough for registration to land in it. Relying on the second sweep would
                // mean waiting out the minimum interval.
                builder.UseSetting("Monitoring:Refresh:StartupDelaySeconds", "3");
                builder.UseSetting("Monitoring:Refresh:ProjectSpacingMilliseconds", "0");
            }

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IGitHubProjectReader>();
                services.RemoveAll<IApplicationProbe>();
                services.AddSingleton(gitHub);
                services.AddSingleton<IApplicationProbe>(new SweepProbe());
            });
        });

    private static HttpClient CreateClient(WebApplicationFactory<Program> application) =>
        application.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

    /// <summary>Polls until the sweep has done its work, rather than sleeping for a fixed guess.</summary>
    private static async Task<bool> WaitForAsync(
        WebApplicationFactory<Program> application,
        Func<ConsoleOpsDbContext, Task<bool>> condition)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(SweepTimeout);

        while (DateTimeOffset.UtcNow < deadline)
        {
            await using AsyncServiceScope scope = application.Services.CreateAsyncScope();
            ConsoleOpsDbContext dbContext =
                scope.ServiceProvider.GetRequiredService<ConsoleOpsDbContext>();

            if (await condition(dbContext))
            {
                return true;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250));
        }

        return false;
    }

    private static async Task<ProjectResponse> RegisterAsync(
        HttpClient client,
        string namePrefix,
        string repositoryPrefix = "scheduled")
    {
        string unique = Guid.NewGuid().ToString("N");
        RegisterProjectRequest request = new(
            $"{namePrefix} {unique}",
            "Scheduled refresh integration test",
            new ProjectRepositoryRequest(
                "console-ops-tests",
                $"{repositoryPrefix}-{unique}",
                "main",
                "deploy.yml"),
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

    private sealed class SweepGitHubReader : IGitHubProjectReader
    {
        private int _failedReads;

        public int FailedReads => Volatile.Read(ref _failedReads);

        public Task<GitHubProjectReadResult> ReadAsync(
            GitHubProjectReference project,
            IReadOnlyCollection<string> deployedCommitShas,
            CancellationToken cancellationToken)
        {
            if (project.Repository.StartsWith("boom", StringComparison.OrdinalIgnoreCase))
            {
                Interlocked.Increment(ref _failedReads);
                throw new InvalidOperationException("Simulated provider fault for one project.");
            }

            DateTimeOffset observedAt = DateTimeOffset.UtcNow;
            GitHubSourceObservation source = new(
                $"{project.Owner}/{project.Repository}",
                project.DefaultBranch,
                CommitSha,
                CommitSha[..7],
                observedAt.AddMinutes(-5),
                observedAt);
            GitHubWorkflowObservation workflow = new(
                project.WorkflowFile,
                "Deploy",
                GitHubWorkflowState.Passed,
                CommitSha,
                observedAt.AddMinutes(-4),
                observedAt.AddMinutes(-2),
                observedAt);
            GitHubWorkflowRun run = new(
                4242,
                42,
                project.WorkflowFile,
                "Deploy",
                project.DefaultBranch,
                CommitSha,
                GitHubWorkflowState.Passed,
                observedAt.AddMinutes(-4),
                observedAt.AddMinutes(-2),
                "ci-bot",
                "https://github.com/owner/repository/actions/runs/4242",
                observedAt);

            return Task.FromResult(new GitHubProjectReadResult(
                GitHubFactResult<GitHubSourceObservation>.Success(source),
                GitHubFactResult<GitHubWorkflowObservation>.Success(workflow),
                [],
                [run]));
        }
    }

    private sealed class SweepProbe : IApplicationProbe
    {
        public Task<ApplicationProbeResult> ProbeAsync(
            ApplicationProbeTarget target,
            CancellationToken cancellationToken)
        {
            DateTimeOffset observedAt = DateTimeOffset.UtcNow;
            return Task.FromResult(new ApplicationProbeResult(
                new ApplicationHealthObservation(
                    ApplicationHealthState.Healthy,
                    TimeSpan.FromMilliseconds(42),
                    observedAt,
                    []),
                new ApplicationVersionObservation(
                    ApplicationVersionState.Available,
                    "Scheduled.Api",
                    "1.0.0",
                    CommitSha,
                    "Production",
                    observedAt.AddHours(-1),
                    observedAt)));
        }
    }
}
