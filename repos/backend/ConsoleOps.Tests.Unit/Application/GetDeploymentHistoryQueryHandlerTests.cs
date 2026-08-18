using ConsoleOps.Application.Features.Deployments.GetHistory;
using ConsoleOps.Application.Integrations.ApplicationMonitoring;
using ConsoleOps.Application.Integrations.GitHub;
using ConsoleOps.Domain.Monitoring;

namespace ConsoleOps.Tests.Unit.Application;

public sealed class GetDeploymentHistoryQueryHandlerTests
{
    private const string CommitSha = "8a17c2f9abcdef0123456789abcdef0123456789";
    private static readonly DateTimeOffset ObservedAt = new(2026, 8, 14, 9, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task Handle_ProjectsRecordedFactsWithoutInventingAnything()
    {
        DeploymentRecordData deployment = CreateDeployment() with
        {
            StartedAtUtc = ObservedAt.AddMinutes(-20),
            CompletedAtUtc = ObservedAt.AddMinutes(-17).AddSeconds(-42),
            Environments =
            [
                new DeploymentEnvironmentData(
                    Guid.NewGuid(),
                    "Production",
                    "production",
                    true,
                    ObservedAt.AddMinutes(-15),
                    ApplicationHealthState.Healthy,
                    ObservedAt.AddMinutes(-45),
                    ApplicationHealthState.Unhealthy,
                    ObservedAt.AddMinutes(-15),
                    VersionSyncState.InSync,
                    ObservedAt.AddMinutes(-15))
            ]
        };
        StubReadStore store = new([deployment]);
        GetDeploymentHistoryQueryHandler handler = CreateHandler(store);

        DeploymentHistoryResponse response = await handler.Handle(
            new GetDeploymentHistoryQuery(),
            CancellationToken.None);

        Assert.Equal(ObservedAt, response.ObservedAt);
        DeploymentResponse record = Assert.Single(response.Deployments);
        Assert.Equal("githubActions", record.Provider);
        Assert.Equal("passed", record.Result);
        Assert.Equal(CommitSha, record.CommitSha);
        Assert.Equal("8a17c2f", record.CommitShortSha);
        Assert.Equal(deployment.CompletedAtUtc, record.DeployedAt);
        // 09:10:00 to 09:12:18 is 2m 18s, reported in whole seconds.
        Assert.Equal(138, record.DurationSeconds);

        DeploymentEnvironmentResponse environment = Assert.Single(record.Environments);
        Assert.Equal("Production", environment.Environment.Name);
        Assert.Equal("production", environment.Environment.Kind);
        Assert.True(environment.IsCurrent);
        Assert.Equal("healthy", environment.HealthBefore);
        Assert.Equal("unhealthy", environment.HealthAfter);
        Assert.Equal("inSync", environment.VersionCheck);
        Assert.Equal(ObservedAt.AddMinutes(-45), environment.HealthBeforeObservedAt);
        Assert.Equal(ObservedAt.AddMinutes(-15), environment.HealthAfterObservedAt);
    }

    [Fact]
    public async Task Handle_WhenNoEnvironmentObservedTheCommit_ReportsNoEnvironments()
    {
        StubReadStore store = new([CreateDeployment()]);
        GetDeploymentHistoryQueryHandler handler = CreateHandler(store);

        DeploymentHistoryResponse response = await handler.Handle(
            new GetDeploymentHistoryQuery(),
            CancellationToken.None);

        // A release that was never seen running is reported as such. It is not marked failed, and it is
        // not attributed to an environment on the strength of the workflow name.
        DeploymentResponse record = Assert.Single(response.Deployments);
        Assert.Empty(record.Environments);
    }

    [Fact]
    public async Task Handle_WhenHealthWasNeverObserved_ReportsUnknownRatherThanPassing()
    {
        DeploymentRecordData deployment = CreateDeployment() with
        {
            Environments =
            [
                new DeploymentEnvironmentData(
                    Guid.NewGuid(),
                    "Staging",
                    "staging",
                    false,
                    ObservedAt.AddHours(-2),
                    null,
                    null,
                    null,
                    null,
                    null,
                    null)
            ]
        };
        GetDeploymentHistoryQueryHandler handler = CreateHandler(new StubReadStore([deployment]));

        DeploymentHistoryResponse response = await handler.Handle(
            new GetDeploymentHistoryQuery(),
            CancellationToken.None);

        DeploymentEnvironmentResponse environment =
            Assert.Single(Assert.Single(response.Deployments).Environments);
        Assert.Equal("unknown", environment.HealthBefore);
        Assert.Equal("unknown", environment.HealthAfter);
        Assert.Equal("unknown", environment.VersionCheck);
        Assert.Null(environment.HealthBeforeObservedAt);
        Assert.Null(environment.HealthAfterObservedAt);
    }

    [Fact]
    public async Task Handle_WhenProviderTimesAreIncomplete_LeavesDurationUnknown()
    {
        DeploymentRecordData started = CreateDeployment() with
        {
            StartedAtUtc = ObservedAt.AddMinutes(-5),
            CompletedAtUtc = null
        };
        DeploymentRecordData reversed = CreateDeployment() with
        {
            StartedAtUtc = ObservedAt,
            CompletedAtUtc = ObservedAt.AddMinutes(-5)
        };
        GetDeploymentHistoryQueryHandler handler =
            CreateHandler(new StubReadStore([started, reversed]));

        DeploymentHistoryResponse response = await handler.Handle(
            new GetDeploymentHistoryQuery(),
            CancellationToken.None);

        Assert.All(response.Deployments, record => Assert.Null(record.DurationSeconds));
        // An unfinished run is still dated: the timeline uses its start.
        Assert.Equal(ObservedAt.AddMinutes(-5), response.Deployments[0].DeployedAt);
    }

    [Fact]
    public async Task Handle_WhenProviderReportedNoTimes_FallsBackToWhenConsoleOpsRecordedTheRun()
    {
        DeploymentRecordData deployment = CreateDeployment() with
        {
            StartedAtUtc = null,
            CompletedAtUtc = null,
            RecordedAtUtc = ObservedAt.AddMinutes(-1)
        };
        GetDeploymentHistoryQueryHandler handler = CreateHandler(new StubReadStore([deployment]));

        DeploymentHistoryResponse response = await handler.Handle(
            new GetDeploymentHistoryQuery(),
            CancellationToken.None);

        Assert.Equal(ObservedAt.AddMinutes(-1), Assert.Single(response.Deployments).DeployedAt);
    }

    [Theory]
    [InlineData(null, GetDeploymentHistoryQueryHandler.DefaultLimit)]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(25, 25)]
    [InlineData(10_000, GetDeploymentHistoryQueryHandler.MaximumLimit)]
    public async Task Handle_ClampsTheRequestedPageSize(int? requested, int expected)
    {
        StubReadStore store = new([]);
        GetDeploymentHistoryQueryHandler handler = CreateHandler(store);

        await handler.Handle(new GetDeploymentHistoryQuery(requested), CancellationToken.None);

        Assert.Equal(expected, store.RequestedLimit);
    }

    private static GetDeploymentHistoryQueryHandler CreateHandler(StubReadStore store) =>
        new(store, new FixedTimeProvider(ObservedAt));

    private static DeploymentRecordData CreateDeployment() => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        "Spinner API",
        "owner/spinner-api",
        "main",
        CommitSha,
        GitHubWorkflowState.Passed,
        "deploy-spinner-api.yml",
        "Deploy Spinner API",
        "https://github.com/owner/spinner-api/actions/runs/4102",
        42,
        "ci-bot",
        null,
        null,
        ObservedAt.AddMinutes(-10),
        []);

    private sealed class StubReadStore(IReadOnlyList<DeploymentRecordData> deployments)
        : IDeploymentHistoryReadStore
    {
        public int? RequestedLimit { get; private set; }

        public Task<DeploymentHistoryData> ReadAsync(int limit, CancellationToken cancellationToken)
        {
            RequestedLimit = limit;
            return Task.FromResult(new DeploymentHistoryData(deployments));
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
