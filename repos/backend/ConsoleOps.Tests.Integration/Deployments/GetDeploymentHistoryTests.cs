using System.Net;
using System.Net.Http.Json;
using ConsoleOps.Api.Features.Projects;
using ConsoleOps.Application.Features.Deployments.GetHistory;
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

namespace ConsoleOps.Tests.Integration.Deployments;

/// <summary>
/// Release history end to end: a refresh records the workflow runs GitHub reported, and the query links
/// each release to the environments that were actually observed running its commit.
/// </summary>
[Collection(ConsoleOpsApiCollection.Name)]
public sealed class GetDeploymentHistoryTests(ConsoleOpsApiFactory factory)
{
    private const string FirstSha = "89abcdef0123456789abcdef0123456789abcdef";
    private const string SecondSha = "0123456789abcdef0123456789abcdef01234567";
    private static readonly DateTimeOffset FirstObservedAt =
        new(2026, 8, 14, 7, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset SecondObservedAt =
        new(2026, 8, 14, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ThirdObservedAt =
        new(2026, 8, 14, 9, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task History_RecordsRunsOnceAndReconcilesThemWithRuntimeObservations()
    {
        MutableClock clock = new(FirstObservedAt);
        MutableProbe probe = new(clock, FirstSha, ApplicationHealthState.Healthy);
        MutableGitHubReader gitHub = new(clock, FirstSha);
        gitHub.SetRuns(CompletedRun(4101, FirstSha, FirstObservedAt));
        using WebApplicationFactory<Program> application = CreateApplication(gitHub, probe, clock);
        using HttpClient client = CreateClient(application);
        ProjectResponse project = await RegisterAsync(client);

        await RefreshAsync(client, project.Id);

        DeploymentHistoryResponse afterFirst = await ReadHistoryAsync(client);
        DeploymentResponse first = Assert.Single(
            afterFirst.Deployments,
            record => record.ProjectId == project.Id);
        Assert.Equal("githubActions", first.Provider);
        Assert.Equal("passed", first.Result);
        Assert.Equal(FirstSha, first.CommitSha);
        Assert.Equal("89abcde", first.CommitShortSha);
        Assert.Equal("main", first.Branch);
        Assert.Equal("deploy.yml", first.WorkflowFile);
        Assert.Equal("https://github.com/owner/repository/actions/runs/4101", first.WorkflowUrl);
        Assert.Equal("ci-bot", first.TriggeredBy);
        Assert.Equal(4101, first.RunNumber);
        Assert.Equal(150, first.DurationSeconds);
        Assert.Equal($"{project.Repository.Owner}/{project.Repository.Name}", first.Repository);

        DeploymentEnvironmentResponse firstEnvironment = Assert.Single(first.Environments);
        Assert.Equal("Production", firstEnvironment.Environment.Name);
        Assert.Equal("production", firstEnvironment.Environment.Kind);
        Assert.True(firstEnvironment.IsCurrent);
        Assert.Equal(FirstObservedAt, firstEnvironment.FirstObservedAt);
        // Nothing was observed before this release, which reads as unknown rather than healthy.
        Assert.Equal("unknown", firstEnvironment.HealthBefore);
        Assert.Null(firstEnvironment.HealthBeforeObservedAt);
        Assert.Equal("healthy", firstEnvironment.HealthAfter);
        Assert.Equal(FirstObservedAt, firstEnvironment.HealthAfterObservedAt);
        Assert.Equal("inSync", firstEnvironment.VersionCheck);

        // A newer release goes out and the application stops answering healthily.
        clock.Set(SecondObservedAt);
        probe.Set(SecondSha, ApplicationHealthState.Unhealthy);
        gitHub.SetSourceCommit(SecondSha);
        gitHub.SetRuns(
            InProgressRun(4102, SecondSha, SecondObservedAt),
            CompletedRun(4101, FirstSha, FirstObservedAt));
        await RefreshAsync(client, project.Id);

        DeploymentHistoryResponse afterSecond = await ReadHistoryAsync(client);
        DeploymentResponse[] records = afterSecond.Deployments
            .Where(record => record.ProjectId == project.Id)
            .ToArray();
        Assert.Equal(2, records.Length);
        // Newest first.
        Assert.Equal(SecondSha, records[0].CommitSha);
        Assert.Equal(FirstSha, records[1].CommitSha);

        DeploymentResponse inFlight = records[0];
        Assert.Equal("inProgress", inFlight.Result);
        Assert.Null(inFlight.CompletedAt);
        Assert.Null(inFlight.DurationSeconds);
        DeploymentEnvironmentResponse currentEnvironment = Assert.Single(inFlight.Environments);
        Assert.True(currentEnvironment.IsCurrent);
        Assert.Equal("healthy", currentEnvironment.HealthBefore);
        Assert.Equal(FirstObservedAt, currentEnvironment.HealthBeforeObservedAt);
        Assert.Equal("unhealthy", currentEnvironment.HealthAfter);
        Assert.Equal(SecondObservedAt, currentEnvironment.HealthAfterObservedAt);

        // The superseded release keeps its own observation and stops being current.
        DeploymentEnvironmentResponse supersededEnvironment = Assert.Single(records[1].Environments);
        Assert.False(supersededEnvironment.IsCurrent);
        Assert.Equal("healthy", supersededEnvironment.HealthAfter);
        Assert.Equal(FirstObservedAt, supersededEnvironment.HealthAfterObservedAt);

        // Re-reading the same runs updates them in place: the in-flight run completes.
        clock.Set(ThirdObservedAt);
        gitHub.SetRuns(
            CompletedRun(4102, SecondSha, SecondObservedAt),
            CompletedRun(4101, FirstSha, FirstObservedAt));
        await RefreshAsync(client, project.Id);

        DeploymentHistoryResponse afterThird = await ReadHistoryAsync(client);
        DeploymentResponse[] settled = afterThird.Deployments
            .Where(record => record.ProjectId == project.Id)
            .ToArray();
        Assert.Equal(2, settled.Length);
        Assert.Equal("passed", settled[0].Result);
        Assert.Equal(150, settled[0].DurationSeconds);

        await using AsyncServiceScope scope = application.Services.CreateAsyncScope();
        ConsoleOpsDbContext dbContext = scope.ServiceProvider.GetRequiredService<ConsoleOpsDbContext>();
        Assert.Equal(
            2,
            await dbContext.Deployments.CountAsync(entity => entity.ProjectId == project.Id));
        // The first sighting is preserved even though the run was re-read twice.
        var recorded = await dbContext.Deployments
            .Where(entity => entity.ProjectId == project.Id && entity.ExternalRunId == 4102)
            .Select(entity => new { entity.RecordedAtUtc, entity.ObservedAtUtc })
            .SingleAsync();
        Assert.Equal(SecondObservedAt, recorded.RecordedAtUtc);
        Assert.Equal(ThirdObservedAt, recorded.ObservedAtUtc);
    }

    [Fact]
    public async Task History_WhenTheCommitWasNeverObservedRunning_ReportsNoEnvironments()
    {
        MutableClock clock = new(FirstObservedAt);
        // The runtime reports a different commit than the run built, so no environment can be linked.
        MutableProbe probe = new(clock, SecondSha, ApplicationHealthState.Healthy);
        MutableGitHubReader gitHub = new(clock, SecondSha);
        gitHub.SetRuns(CompletedRun(7001, FirstSha, FirstObservedAt));
        using WebApplicationFactory<Program> application = CreateApplication(gitHub, probe, clock);
        using HttpClient client = CreateClient(application);
        ProjectResponse project = await RegisterAsync(client);

        await RefreshAsync(client, project.Id);
        DeploymentHistoryResponse history = await ReadHistoryAsync(client);

        DeploymentResponse record = Assert.Single(
            history.Deployments,
            candidate => candidate.ProjectId == project.Id);
        Assert.Equal(FirstSha, record.CommitSha);
        Assert.Equal("passed", record.Result);
        Assert.Empty(record.Environments);
    }

    [Fact]
    public async Task History_OmitsArchivedProjects()
    {
        MutableClock clock = new(FirstObservedAt);
        MutableProbe probe = new(clock, FirstSha, ApplicationHealthState.Healthy);
        MutableGitHubReader gitHub = new(clock, FirstSha);
        gitHub.SetRuns(CompletedRun(8001, FirstSha, FirstObservedAt));
        using WebApplicationFactory<Program> application = CreateApplication(gitHub, probe, clock);
        using HttpClient client = CreateClient(application);
        ProjectResponse project = await RegisterAsync(client);
        await RefreshAsync(client, project.Id);
        Assert.Contains(
            (await ReadHistoryAsync(client)).Deployments,
            record => record.ProjectId == project.Id);

        HttpResponseMessage archived = await client.DeleteAsync($"/api/projects/{project.Id}");
        Assert.Equal(HttpStatusCode.NoContent, archived.StatusCode);

        DeploymentHistoryResponse history = await ReadHistoryAsync(client);
        Assert.DoesNotContain(history.Deployments, record => record.ProjectId == project.Id);
    }

    private WebApplicationFactory<Program> CreateApplication(
        IGitHubProjectReader gitHub,
        IApplicationProbe probe,
        TimeProvider clock) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IGitHubProjectReader>();
            services.RemoveAll<IApplicationProbe>();
            services.RemoveAll<TimeProvider>();
            services.AddSingleton(gitHub);
            services.AddSingleton(probe);
            services.AddSingleton(clock);
        }));

    private static HttpClient CreateClient(WebApplicationFactory<Program> application) =>
        application.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

    private static GitHubWorkflowRun CompletedRun(
        int runNumber,
        string commitSha,
        DateTimeOffset startedAt) =>
        CreateRun(runNumber, commitSha, GitHubWorkflowState.Passed, startedAt, startedAt.AddSeconds(150));

    private static GitHubWorkflowRun InProgressRun(
        int runNumber,
        string commitSha,
        DateTimeOffset startedAt) =>
        CreateRun(runNumber, commitSha, GitHubWorkflowState.InProgress, startedAt, null);

    private static GitHubWorkflowRun CreateRun(
        int runNumber,
        string commitSha,
        GitHubWorkflowState state,
        DateTimeOffset startedAt,
        DateTimeOffset? completedAt) =>
        new(
            runNumber,
            runNumber,
            "deploy.yml",
            "Deploy",
            "main",
            commitSha,
            state,
            startedAt,
            completedAt,
            "ci-bot",
            $"https://github.com/owner/repository/actions/runs/{runNumber}",
            startedAt);

    private static async Task<DeploymentHistoryResponse> ReadHistoryAsync(HttpClient client)
    {
        HttpResponseMessage response = await client.GetAsync("/api/deployments");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return Assert.IsType<DeploymentHistoryResponse>(
            await response.Content.ReadFromJsonAsync<DeploymentHistoryResponse>());
    }

    private static async Task<ProjectResponse> RegisterAsync(HttpClient client)
    {
        string unique = Guid.NewGuid().ToString("N");
        RegisterProjectRequest request = new(
            $"Deployment Project {unique}",
            "Deployment history integration test",
            new ProjectRepositoryRequest("console-ops-tests", $"deployments-{unique}", "main", "deploy.yml"),
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

    private sealed class MutableClock(DateTimeOffset value) : TimeProvider
    {
        private DateTimeOffset _value = value;

        public override DateTimeOffset GetUtcNow() => _value;

        public void Set(DateTimeOffset value) => _value = value;
    }

    private sealed class MutableGitHubReader(MutableClock clock, string sourceCommitSha)
        : IGitHubProjectReader
    {
        private string _sourceCommitSha = sourceCommitSha;
        private GitHubWorkflowRun[] _runs = [];

        public void SetSourceCommit(string commitSha) => _sourceCommitSha = commitSha;

        public void SetRuns(params GitHubWorkflowRun[] runs) => _runs = runs;

        public Task<GitHubProjectReadResult> ReadAsync(
            GitHubProjectReference project,
            IReadOnlyCollection<string> deployedCommitShas,
            CancellationToken cancellationToken)
        {
            DateTimeOffset observedAt = clock.GetUtcNow();
            // The real adapter stamps every run with the moment it was read, not the moment it ran.
            GitHubWorkflowRun[] runs = _runs
                .Select(run => run with { ObservedAtUtc = observedAt })
                .ToArray();
            GitHubSourceObservation source = new(
                $"{project.Owner}/{project.Repository}",
                project.DefaultBranch,
                _sourceCommitSha,
                _sourceCommitSha[..7],
                observedAt.AddMinutes(-5),
                observedAt);
            GitHubWorkflowRun? newest = runs.FirstOrDefault();
            GitHubWorkflowObservation workflow = new(
                project.WorkflowFile,
                newest?.WorkflowName,
                newest?.State ?? GitHubWorkflowState.Unknown,
                newest?.CommitSha,
                newest?.StartedAtUtc,
                newest?.CompletedAtUtc,
                observedAt);

            return Task.FromResult(new GitHubProjectReadResult(
                GitHubFactResult<GitHubSourceObservation>.Success(source),
                GitHubFactResult<GitHubWorkflowObservation>.Success(workflow),
                [],
                runs));
        }
    }

    private sealed class MutableProbe(
        MutableClock clock,
        string deployedCommitSha,
        ApplicationHealthState healthState) : IApplicationProbe
    {
        private string _deployedCommitSha = deployedCommitSha;
        private ApplicationHealthState _healthState = healthState;

        public void Set(string deployedCommitSha, ApplicationHealthState healthState)
        {
            _deployedCommitSha = deployedCommitSha;
            _healthState = healthState;
        }

        public Task<ApplicationProbeResult> ProbeAsync(
            ApplicationProbeTarget target,
            CancellationToken cancellationToken)
        {
            DateTimeOffset observedAt = clock.GetUtcNow();
            return Task.FromResult(new ApplicationProbeResult(
                new ApplicationHealthObservation(
                    _healthState,
                    TimeSpan.FromMilliseconds(80),
                    observedAt,
                    []),
                new ApplicationVersionObservation(
                    ApplicationVersionState.Available,
                    "Spinner.Api",
                    "1.0.0",
                    _deployedCommitSha,
                    "Production",
                    observedAt.AddMinutes(-10),
                    observedAt)));
        }
    }
}
