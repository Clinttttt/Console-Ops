using ConsoleOps.Application.Features.Dashboard.GetOverview;
using ConsoleOps.Application.Integrations.ApplicationMonitoring;
using ConsoleOps.Application.Integrations.GitHub;
using ConsoleOps.Domain.Monitoring;

namespace ConsoleOps.Tests.Unit.Application;

public sealed class GetDashboardOverviewQueryHandlerTests
{
    private static readonly DateTimeOffset ObservedAt = new(2026, 8, 14, 6, 0, 0, TimeSpan.Zero);
    private const string CommitSha = "0123456789abcdef0123456789abcdef01234567";

    [Fact]
    public async Task Handle_WhenNoProjectsExist_ReturnsHonestUnknownState()
    {
        GetDashboardOverviewQueryHandler handler = CreateHandler(new DashboardOverviewData([], []));

        DashboardOverviewResponse response = await handler.Handle(
            new GetDashboardOverviewQuery(),
            CancellationToken.None);

        Assert.Equal("unknown", response.Summary.Level);
        Assert.Equal("System State Unknown", response.Summary.Label);
        Assert.Empty(response.Projects);
        Assert.All(response.Pipeline.Stages, stage => Assert.False(stage.Verified));
        Assert.Equal(["api", "ci", "versionSync"], response.SystemState.Rows.Select(row => row.Key));
    }

    [Fact]
    public async Task Handle_WhenAllCoreFactsAreAcceptable_ReturnsHealthyState()
    {
        GetDashboardOverviewQueryHandler handler = CreateHandler(
            new DashboardOverviewData([CreateHealthySurface()], []));

        DashboardOverviewResponse response = await handler.Handle(
            new GetDashboardOverviewQuery(),
            CancellationToken.None);

        Assert.Equal("healthy", response.Summary.Level);
        Assert.Equal("All Systems Operational", response.Summary.Label);
        Assert.All(response.Pipeline.Stages, stage => Assert.True(stage.Verified));
        DashboardProjectSurfaceResponse surface = Assert.Single(response.Projects);
        Assert.Equal("healthy", surface.Health.Level);
        Assert.Equal([91d, 103d], surface.Response.Samples);
        Assert.Equal(CommitSha, surface.DeployedVersion?.CommitSha);
        Assert.Null(surface.VersionSync.CommitsBehind);
    }

    [Fact]
    public async Task Handle_WhenOnlySourceIsVisible_ReturnsWarningInsteadOfInventingHealth()
    {
        DashboardSurfaceData sourceOnly = CreateHealthySurface() with
        {
            WorkflowFile = null,
            HealthConfigured = false,
            VersionConfigured = false,
            Workflow = null,
            Health = null,
            Version = null,
            VersionSync = null,
            ResponseSamples = []
        };
        GetDashboardOverviewQueryHandler handler = CreateHandler(
            new DashboardOverviewData([sourceOnly], []));

        DashboardOverviewResponse response = await handler.Handle(
            new GetDashboardOverviewQuery(),
            CancellationToken.None);

        Assert.Equal("warning", response.Summary.Level);
        DashboardProjectSurfaceResponse surface = Assert.Single(response.Projects);
        Assert.Equal("Not configured", surface.Health.Label);
        Assert.Equal("notConfigured", surface.Workflow.State);
        Assert.Equal("notConfigured", surface.VersionSync.State);
        Assert.Null(surface.DeployedVersion);
    }

    private static GetDashboardOverviewQueryHandler CreateHandler(DashboardOverviewData data) =>
        new(new StubReadStore(data), new FixedTimeProvider(ObservedAt));

    private static DashboardSurfaceData CreateHealthySurface() => new(
        Guid.Parse("0198a690-37e4-7a10-8c60-58fdf549cc11"),
        "Spinner API",
        "clint/spinner",
        "main",
        "ci.yml",
        Guid.Parse("0198a690-37e4-7f8b-8f50-7f79ae6f9492"),
        "Production",
        "production",
        true,
        true,
        null,
        new DashboardSourceData(
            "clint/spinner",
            "main",
            CommitSha,
            CommitSha[..7],
            ObservedAt.AddMinutes(-5),
            ObservedAt),
        new DashboardWorkflowData(
            "ci.yml",
            "CI",
            GitHubWorkflowState.Passed,
            CommitSha,
            ObservedAt.AddMinutes(-3),
            ObservedAt.AddMinutes(-1),
            ObservedAt),
        new DashboardHealthData(
            ApplicationHealthState.Healthy,
            103,
            ObservedAt,
            [new DashboardDependencyData("Database", ApplicationHealthState.Healthy)]),
        new DashboardVersionData(
            ApplicationVersionState.Available,
            "Spinner.Api",
            "1.0.0",
            CommitSha,
            "Production",
            ObservedAt.AddHours(-1),
            ObservedAt),
        new DashboardVersionSyncData(
            VersionSyncState.InSync,
            null,
            ObservedAt),
        [91, 103]);

    private sealed class StubReadStore(DashboardOverviewData data) : IDashboardOverviewReadStore
    {
        public Task<DashboardOverviewData> ReadAsync(CancellationToken cancellationToken) =>
            Task.FromResult(data);
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
