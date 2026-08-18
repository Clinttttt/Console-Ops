using System.Net;
using System.Net.Http.Json;
using ConsoleOps.Api.Features.Projects;
using ConsoleOps.Application.Features.Projects;
using ConsoleOps.Application.Features.Projects.RefreshProject;
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

[Collection(ConsoleOpsApiCollection.Name)]
public sealed class RefreshProjectTests(ConsoleOpsApiFactory factory)
{
    private const string SourceSha = "0123456789abcdef0123456789abcdef01234567";
    private const string OlderSha = "89abcdef0123456789abcdef0123456789abcdef";

    [Fact]
    public async Task Refresh_PersistsFactsAndEmitsOnlyDeterministicTransitions()
    {
        DateTimeOffset observedAt = new(2026, 8, 14, 7, 0, 0, TimeSpan.Zero);
        MutableApplicationProbe probe = new(observedAt, SourceSha, ApplicationHealthState.Healthy);
        StubGitHubReader gitHub = new(observedAt, SourceSha);
        using WebApplicationFactory<Program> application = CreateApplication(gitHub, probe);
        using HttpClient client = application.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        ProjectResponse project = await RegisterAsync(client);

        RefreshProjectResponse first = await RefreshAsync(client, project.Id);

        Assert.Equal("available", first.Source.State);
        Assert.Equal(SourceSha, first.Source.CommitSha);
        Assert.Equal("passed", first.Workflow.State);
        RefreshEnvironmentResponse firstEnvironment = Assert.Single(first.Environments);
        Assert.Equal("healthy", firstEnvironment.Health.State);
        Assert.Equal("available", firstEnvironment.Version.State);
        Assert.Equal("inSync", firstEnvironment.VersionSync.State);
        Assert.Empty(first.Activities);
        Assert.Collection(
            firstEnvironment.Health.Dependencies,
            dependency =>
            {
                Assert.Equal("Database", dependency.Name);
                Assert.Equal("healthy", dependency.State);
            });

        probe.SetState(observedAt.AddMinutes(1), OlderSha, ApplicationHealthState.Unhealthy);
        gitHub.ObservedAtUtc = observedAt.AddMinutes(1);
        RefreshProjectResponse second = await RefreshAsync(client, project.Id);

        RefreshEnvironmentResponse secondEnvironment = Assert.Single(second.Environments);
        Assert.Equal("unhealthy", secondEnvironment.Health.State);
        Assert.Equal("behind", secondEnvironment.VersionSync.State);
        Assert.Equal(2, secondEnvironment.VersionSync.CommitsBehind);
        Assert.Contains(second.Activities, activity => activity.Type == "healthFailed");
        Assert.Contains(second.Activities, activity => activity.Type == "versionDrift");

        probe.SetState(observedAt.AddMinutes(2), OlderSha, ApplicationHealthState.Unhealthy);
        gitHub.ObservedAtUtc = observedAt.AddMinutes(2);
        RefreshProjectResponse unchanged = await RefreshAsync(client, project.Id);
        Assert.Empty(unchanged.Activities);

        probe.SetState(observedAt.AddMinutes(3), SourceSha, ApplicationHealthState.Healthy);
        gitHub.ObservedAtUtc = observedAt.AddMinutes(3);
        RefreshProjectResponse recovered = await RefreshAsync(client, project.Id);
        Assert.Contains(recovered.Activities, activity => activity.Type == "healthRecovered");
        Assert.Contains(recovered.Activities, activity => activity.Type == "versionSynchronized");

        await using AsyncServiceScope scope = application.Services.CreateAsyncScope();
        ConsoleOpsDbContext dbContext = scope.ServiceProvider.GetRequiredService<ConsoleOpsDbContext>();
        Assert.Equal(4, await dbContext.SourceObservations.CountAsync(entity => entity.ProjectId == project.Id));
        Assert.Equal(4, await dbContext.WorkflowObservations.CountAsync(entity => entity.ProjectId == project.Id));
        Assert.Equal(4, await dbContext.HealthObservations.CountAsync(entity => entity.ProjectId == project.Id));
        IQueryable<Guid> healthObservationIds = dbContext.HealthObservations
            .Where(entity => entity.ProjectId == project.Id)
            .Select(entity => entity.Id);
        Assert.Equal(
            4,
            await dbContext.DependencyHealthObservations.CountAsync(entity =>
                healthObservationIds.Contains(entity.HealthObservationId)));
        Assert.Equal(4, await dbContext.VersionObservations.CountAsync(entity => entity.ProjectId == project.Id));
        Assert.Equal(4, await dbContext.VersionSyncObservations.CountAsync(entity => entity.ProjectId == project.Id));
        Assert.Equal(4, await dbContext.MonitoringActivities.CountAsync(entity => entity.ProjectId == project.Id));
        // GitHub reported no workflow runs, so no release was recorded for this project.
        Assert.Equal(0, await dbContext.Deployments.CountAsync(entity => entity.ProjectId == project.Id));
    }

    [Fact]
    public async Task Refresh_WhenProjectDoesNotExist_ReturnsNotFoundWithoutCallingProviders()
    {
        DateTimeOffset observedAt = new(2026, 8, 14, 7, 0, 0, TimeSpan.Zero);
        MutableApplicationProbe probe = new(observedAt, SourceSha, ApplicationHealthState.Healthy);
        StubGitHubReader gitHub = new(observedAt, SourceSha);
        using WebApplicationFactory<Program> application = CreateApplication(gitHub, probe);
        using HttpClient client = application.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

        HttpResponseMessage response = await client.PostAsync($"/api/projects/{Guid.NewGuid()}/refresh", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(0, probe.CallCount);
        Assert.Equal(0, gitHub.CallCount);
    }

    [Fact]
    public async Task Refresh_WhenSourceFails_PersistsUnknownSourceAndSuccessfulEnvironmentFacts()
    {
        DateTimeOffset observedAt = new(2026, 8, 14, 8, 0, 0, TimeSpan.Zero);
        MutableApplicationProbe probe = new(observedAt, SourceSha, ApplicationHealthState.Healthy);
        StubGitHubReader gitHub = new(observedAt, SourceSha, sourceUnavailable: true);
        using WebApplicationFactory<Program> application = CreateApplication(gitHub, probe);
        using HttpClient client = application.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        ProjectResponse project = await RegisterAsync(client);

        RefreshProjectResponse result = await RefreshAsync(client, project.Id);

        Assert.Equal("unknown", result.Source.State);
        Assert.Null(result.Source.CommitSha);
        Assert.Equal("passed", result.Workflow.State);
        RefreshEnvironmentResponse environment = Assert.Single(result.Environments);
        Assert.Equal("healthy", environment.Health.State);
        Assert.Equal("available", environment.Version.State);
        Assert.Equal("unknown", environment.VersionSync.State);

        await using AsyncServiceScope scope = application.Services.CreateAsyncScope();
        ConsoleOpsDbContext dbContext = scope.ServiceProvider.GetRequiredService<ConsoleOpsDbContext>();
        var source = await dbContext.SourceObservations.SingleAsync(entity => entity.ProjectId == project.Id);
        Assert.False(source.IsAvailable);
        Assert.Equal(GitHubReadFailure.Unavailable, source.Failure);
        Assert.Single(await dbContext.HealthObservations.Where(entity => entity.ProjectId == project.Id).ToListAsync());
        Assert.Single(await dbContext.VersionObservations.Where(entity => entity.ProjectId == project.Id).ToListAsync());
    }

    [Fact]
    public async Task Refresh_WhenConfigurationChangesDuringProviderReads_ReturnsConflictAndPersistsNothing()
    {
        DateTimeOffset observedAt = new(2026, 8, 14, 9, 0, 0, TimeSpan.Zero);
        BlockingApplicationProbe probe = new(observedAt, SourceSha);
        StubGitHubReader gitHub = new(observedAt, SourceSha);
        using WebApplicationFactory<Program> application = CreateApplication(gitHub, probe);
        using HttpClient client = application.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        ProjectResponse project = await RegisterAsync(client);

        Task<HttpResponseMessage> refreshRequest = client.PostAsync($"/api/projects/{project.Id}/refresh", null);
        await probe.Entered.WaitAsync(TimeSpan.FromSeconds(10));

        try
        {
            ProjectEnvironmentResponse environment = Assert.Single(project.Environments);
            UpdateProjectRequest update = new(
                project.ConfigurationVersion,
                project.Name,
                "Configuration changed during refresh",
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
                        environment.VersionUrl)
                ]);
            HttpResponseMessage updateResponse = await client.PutAsJsonAsync(
                $"/api/projects/{project.Id}",
                update);
            Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        }
        finally
        {
            probe.Release();
        }

        HttpResponseMessage refreshResponse = await refreshRequest;
        Assert.Equal(HttpStatusCode.Conflict, refreshResponse.StatusCode);

        await using AsyncServiceScope scope = application.Services.CreateAsyncScope();
        ConsoleOpsDbContext dbContext = scope.ServiceProvider.GetRequiredService<ConsoleOpsDbContext>();
        Assert.Empty(await dbContext.SourceObservations.Where(entity => entity.ProjectId == project.Id).ToListAsync());
        Assert.Empty(await dbContext.HealthObservations.Where(entity => entity.ProjectId == project.Id).ToListAsync());
        Assert.Empty(await dbContext.MonitoringActivities.Where(entity => entity.ProjectId == project.Id).ToListAsync());
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
            $"Refresh Project {unique}",
            "Refresh integration test",
            new ProjectRepositoryRequest("console-ops-tests", $"refresh-{unique}", "main", "ci.yml"),
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

    private static async Task<RefreshProjectResponse> RefreshAsync(HttpClient client, Guid projectId)
    {
        HttpResponseMessage response = await client.PostAsync($"/api/projects/{projectId}/refresh", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return Assert.IsType<RefreshProjectResponse>(
            await response.Content.ReadFromJsonAsync<RefreshProjectResponse>());
    }

    private sealed class StubGitHubReader(
        DateTimeOffset observedAtUtc,
        string sourceSha,
        bool sourceUnavailable = false)
        : IGitHubProjectReader
    {
        public int CallCount { get; private set; }
        public DateTimeOffset ObservedAtUtc { get; set; } = observedAtUtc;

        public Task<GitHubProjectReadResult> ReadAsync(
            GitHubProjectReference project,
            IReadOnlyCollection<string> deployedCommitShas,
            CancellationToken cancellationToken)
        {
            CallCount++;
            GitHubSourceObservation source = new(
                $"{project.Owner}/{project.Repository}",
                project.DefaultBranch,
                sourceSha,
                sourceSha[..7],
                ObservedAtUtc.AddMinutes(-1),
                ObservedAtUtc);
            GitHubWorkflowObservation workflow = new(
                project.WorkflowFile,
                "CI",
                GitHubWorkflowState.Passed,
                sourceSha,
                ObservedAtUtc.AddMinutes(-2),
                ObservedAtUtc.AddMinutes(-1),
                ObservedAtUtc);
            GitHubCommitComparison[] comparisons = deployedCommitShas
                .Where(commit => !string.Equals(commit, sourceSha, StringComparison.OrdinalIgnoreCase))
                .Select(commit => new GitHubCommitComparison(
                    commit,
                    sourceSha,
                    GitHubCommitRelation.DeployedIsAncestor,
                    2,
                    null,
                    ObservedAtUtc))
                .ToArray();

            return Task.FromResult(new GitHubProjectReadResult(
                sourceUnavailable
                    ? GitHubFactResult<GitHubSourceObservation>.Failed(GitHubReadFailure.Unavailable)
                    : GitHubFactResult<GitHubSourceObservation>.Success(source),
                GitHubFactResult<GitHubWorkflowObservation>.Success(workflow),
                sourceUnavailable ? [] : comparisons,
                []));
        }
    }

    private sealed class MutableApplicationProbe(
        DateTimeOffset observedAtUtc,
        string deployedCommitSha,
        ApplicationHealthState healthState) : IApplicationProbe
    {
        private DateTimeOffset _observedAtUtc = observedAtUtc;
        private string _deployedCommitSha = deployedCommitSha;
        private ApplicationHealthState _healthState = healthState;

        public int CallCount { get; private set; }

        public Task<ApplicationProbeResult> ProbeAsync(
            ApplicationProbeTarget target,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(new ApplicationProbeResult(
                new ApplicationHealthObservation(
                    _healthState,
                    TimeSpan.FromMilliseconds(100),
                    _observedAtUtc,
                    [new DependencyHealthObservation("Database", ApplicationHealthState.Healthy)]),
                new ApplicationVersionObservation(
                    ApplicationVersionState.Available,
                    "Spinner.Api",
                    "1.0.0",
                    _deployedCommitSha,
                    "Production",
                    _observedAtUtc.AddHours(-1),
                    _observedAtUtc)));
        }

        public void SetState(
            DateTimeOffset observedAtUtc,
            string deployedCommitSha,
            ApplicationHealthState healthState)
        {
            _observedAtUtc = observedAtUtc;
            _deployedCommitSha = deployedCommitSha;
            _healthState = healthState;
        }
    }

    private sealed class BlockingApplicationProbe(
        DateTimeOffset observedAtUtc,
        string deployedCommitSha) : IApplicationProbe
    {
        private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Entered => _entered.Task;

        public async Task<ApplicationProbeResult> ProbeAsync(
            ApplicationProbeTarget target,
            CancellationToken cancellationToken)
        {
            _entered.TrySetResult();
            await _released.Task.WaitAsync(cancellationToken);

            return new ApplicationProbeResult(
                new ApplicationHealthObservation(
                    ApplicationHealthState.Healthy,
                    TimeSpan.FromMilliseconds(100),
                    observedAtUtc,
                    []),
                new ApplicationVersionObservation(
                    ApplicationVersionState.Available,
                    "Spinner.Api",
                    "1.0.0",
                    deployedCommitSha,
                    "Production",
                    observedAtUtc.AddHours(-1),
                    observedAtUtc));
        }

        public void Release() => _released.TrySetResult();
    }
}
