using ConsoleOps.Application.Integrations.ApplicationMonitoring;
using ConsoleOps.Application.Integrations.GitHub;
using ConsoleOps.Domain.Monitoring;
using MediatR;

namespace ConsoleOps.Application.Features.Dashboard.GetOverview;

public sealed class GetDashboardOverviewQueryHandler(
    IDashboardOverviewReadStore readStore,
    TimeProvider timeProvider)
    : IRequestHandler<GetDashboardOverviewQuery, DashboardOverviewResponse>
{
    /// <summary>
    /// Availability window. Long enough to be meaningful, short enough that the figure describes the
    /// system as it is now rather than as it was last week.
    /// </summary>
    internal const int UptimeWindowHours = 24;

    public async Task<DashboardOverviewResponse> Handle(
        GetDashboardOverviewQuery request,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        DateTimeOffset availabilitySince = now.AddHours(-UptimeWindowHours);
        DashboardOverviewData data = await readStore.ReadAsync(availabilitySince, cancellationToken);
        SurfaceProjection[] surfaces = data.Surfaces
            .Select(CreateSurface)
            .ToArray();
        OperationalSummaryLevel summaryLevel = OperationalSummary.Calculate(
            surfaces.Select(surface => surface.Assessment).ToArray());
        DashboardSystemSummaryResponse summary = CreateSummary(
            summaryLevel,
            CreateUptime(data.Availability, availabilitySince));

        return new DashboardOverviewResponse(
            now,
            CreatePipeline(surfaces, summaryLevel),
            surfaces.Select(surface => surface.Response).ToArray(),
            CreateSystemState(surfaces),
            data.Activities.Select(CreateActivity).ToArray(),
            summary);
    }

    /// <summary>
    /// Availability across every monitored environment, from the health checks already recorded. The
    /// domain decides whether there is enough evidence to report anything.
    /// </summary>
    private static DashboardUptimeWindowResponse? CreateUptime(
        IReadOnlyList<DashboardAvailabilityData> availability,
        DateTimeOffset sinceUtc)
    {
        UptimeReading? reading = Uptime.Calculate(
            availability
                .Select(point => new UptimeSample(HealthConditions.From(point.State), point.ObservedAtUtc))
                .ToArray(),
            sinceUtc);

        return reading is null
            ? null
            : new DashboardUptimeWindowResponse(
                UptimeWindowHours,
                reading.SinceUtc,
                reading.Percentage,
                reading.Checks,
                reading.HourlySamples);
    }

    private static SurfaceProjection CreateSurface(DashboardSurfaceData surface)
    {
        DashboardSourceData? source = IsCurrent(
            surface.Source?.ObservedAtUtc,
            surface.ConfigurationChangedAtUtc)
            ? surface.Source
            : null;
        DashboardWorkflowData? workflow = IsCurrent(
            surface.Workflow?.ObservedAtUtc,
            surface.ConfigurationChangedAtUtc)
            ? surface.Workflow
            : null;
        DashboardHealthData? health = IsCurrent(
            surface.Health?.ObservedAtUtc,
            surface.ConfigurationChangedAtUtc)
            ? surface.Health
            : null;
        DashboardVersionData? version = IsCurrent(
            surface.Version?.ObservedAtUtc,
            surface.ConfigurationChangedAtUtc)
            ? surface.Version
            : null;
        DashboardVersionSyncData? versionSync = IsCurrent(
            surface.VersionSync?.ObservedAtUtc,
            surface.ConfigurationChangedAtUtc)
            ? surface.VersionSync
            : null;

        GitHubWorkflowState workflowState = workflow?.State
            ?? (string.IsNullOrWhiteSpace(surface.WorkflowFile)
                ? GitHubWorkflowState.NotConfigured
                : GitHubWorkflowState.Unknown);
        ApplicationHealthState healthState = health?.State
            ?? (surface.HealthConfigured
                ? ApplicationHealthState.Unknown
                : ApplicationHealthState.NotConfigured);
        VersionSyncState syncState = versionSync?.State
            ?? (surface.VersionConfigured
                ? VersionSyncState.Unknown
                : VersionSyncState.NotConfigured);
        DashboardDeployedVersionResponse? deployedVersion = CreateDeployedVersion(version);
        DashboardProjectSurfaceResponse response = new(
            surface.ProjectId,
            surface.ProjectName,
            new DashboardEnvironmentResponse(
                surface.EnvironmentId,
                surface.EnvironmentName,
                surface.EnvironmentKind),
            new DashboardSourceResponse(
                "github",
                surface.Repository,
                surface.DefaultBranch,
                source?.CommitSha,
                source?.ShortCommitSha,
                source?.CommittedAtUtc,
                source?.ObservedAtUtc),
            new DashboardWorkflowResponse(
                "githubActions",
                workflow?.WorkflowName ?? workflow?.WorkflowFile ?? surface.WorkflowFile,
                ToCamelCase(workflowState),
                workflow?.CommitSha,
                workflow?.StartedAtUtc,
                workflow?.CompletedAtUtc,
                workflow?.ObservedAtUtc),
            CreateHealthCell(healthState),
            health?.ObservedAtUtc,
            deployedVersion,
            // Why there is no version, so the screen never blames configuration for an unreadable endpoint.
            ToCamelCase(version?.State ?? ApplicationVersionState.NotConfigured),
            new DashboardVersionSyncResponse(
                ToCamelCase(syncState),
                source?.CommitSha,
                deployedVersion?.CommitSha,
                versionSync?.CommitsBehind,
                versionSync?.ObservedAtUtc),
            new DashboardResponseMeasurementResponse(
                health?.ResponseMilliseconds,
                surface.ResponseSamples,
                health?.ObservedAtUtc));

        DashboardDependencyData[] dependencies = health?.Dependencies.ToArray() ?? [];
        bool sourceReliable = source?.CommitSha is not null;
        bool workflowReliable = workflowState is not GitHubWorkflowState.Unknown
            and not GitHubWorkflowState.NotConfigured;
        bool healthReliable = healthState is not ApplicationHealthState.Unknown
            and not ApplicationHealthState.NotConfigured;
        bool syncReliable = syncState is VersionSyncState.InSync or VersionSyncState.Behind;
        bool dependencyDegraded = dependencies.Any(dependency => dependency.State is
            ApplicationHealthState.Degraded
            or ApplicationHealthState.Unhealthy
            or ApplicationHealthState.Unreachable);
        bool dependencyUnknown = dependencies.Any(dependency => dependency.State is
            ApplicationHealthState.Unknown
            or ApplicationHealthState.NotConfigured);
        bool completeVisibility = sourceReliable
            && workflowReliable
            && healthReliable
            && syncReliable;
        OperationalSurfaceAssessment assessment = new(
            sourceReliable || workflowReliable || healthReliable || syncReliable,
            healthState is ApplicationHealthState.Unhealthy or ApplicationHealthState.Unreachable,
            healthState == ApplicationHealthState.Degraded || dependencyDegraded,
            !completeVisibility
                || workflowState != GitHubWorkflowState.Passed
                || syncState == VersionSyncState.Behind
                || dependencyUnknown);

        return new SurfaceProjection(
            response,
            sourceReliable,
            workflowState,
            healthState,
            syncState,
            dependencies,
            assessment);
    }

    private static DashboardDeployedVersionResponse? CreateDeployedVersion(
        DashboardVersionData? version)
    {
        if (version is null
            || version.State != ApplicationVersionState.Available
            || string.IsNullOrWhiteSpace(version.CommitSha))
        {
            return null;
        }

        string commitSha = version.CommitSha;
        return new DashboardDeployedVersionResponse(
            version.Application,
            version.Version,
            commitSha,
            commitSha[..Math.Min(7, commitSha.Length)],
            version.Environment,
            version.BuiltAtUtc,
            version.ObservedAtUtc);
    }

    private static DashboardPipelineResponse CreatePipeline(
        IReadOnlyCollection<SurfaceProjection> surfaces,
        OperationalSummaryLevel summaryLevel)
    {
        bool hasSurfaces = surfaces.Count > 0;
        bool sourceVerified = hasSurfaces && surfaces.All(surface => surface.SourceReliable);
        bool ciVerified = hasSurfaces && surfaces.All(surface => surface.WorkflowState is not
            GitHubWorkflowState.Unknown and not GitHubWorkflowState.NotConfigured);
        bool applicationVerified = hasSurfaces && surfaces.All(surface =>
            surface.HealthState is not ApplicationHealthState.Unknown
                and not ApplicationHealthState.NotConfigured
            && surface.VersionSyncState is VersionSyncState.InSync or VersionSyncState.Behind);

        return new DashboardPipelineResponse(
            [
                new DashboardPipelineStageResponse("source", "GitHub", "Source", sourceVerified),
                new DashboardPipelineStageResponse("ci", "GitHub Actions", "CI/CD", ciVerified),
                new DashboardPipelineStageResponse(
                    "application",
                    "Application",
                    "Health & version",
                    applicationVerified)
            ],
            CreateSummaryCell(summaryLevel));
    }

    private static DashboardSystemStateResponse CreateSystemState(
        IReadOnlyList<SurfaceProjection> surfaces)
    {
        DashboardSystemStateColumnResponse[] columns = surfaces
            .Select(surface => new DashboardSystemStateColumnResponse(
                surface.Response.Id,
                surface.Response.Name,
                surface.Response.Environment.Id,
                surface.Response.Environment.Name))
            .ToArray();
        List<DashboardSystemStateRowResponse> rows =
        [
            new DashboardSystemStateRowResponse(
                "api",
                "API",
                surfaces.Select(CreateApiCell).ToArray())
        ];

        string[] dependencyNames = surfaces
            .SelectMany(surface => surface.Dependencies)
            .Select(dependency => dependency.Name.Trim())
            .Where(name => name.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        rows.AddRange(dependencyNames.Select(dependencyName =>
            new DashboardSystemStateRowResponse(
                DependencyKey(dependencyName),
                dependencyName,
                surfaces.Select(surface => CreateDependencyCell(surface, dependencyName)).ToArray())));
        rows.Add(new DashboardSystemStateRowResponse(
            "ci",
            "CI",
            surfaces.Select(surface => CreateWorkflowCell(surface.WorkflowState)).ToArray()));
        rows.Add(new DashboardSystemStateRowResponse(
            "versionSync",
            "Version Sync",
            surfaces.Select(CreateVersionSyncCell).ToArray()));

        return new DashboardSystemStateResponse(columns, rows);
    }

    private static DashboardStatusCellResponse CreateApiCell(SurfaceProjection surface)
    {
        DashboardStatusCellResponse health = CreateHealthCell(surface.HealthState);
        return health with
        {
            Detail = surface.Response.Response.Milliseconds is double milliseconds
                ? $"{Math.Round(milliseconds):0} ms"
                : health.Detail
        };
    }

    private static DashboardStatusCellResponse? CreateDependencyCell(
        SurfaceProjection surface,
        string dependencyName)
    {
        DashboardDependencyData? dependency = surface.Dependencies.FirstOrDefault(candidate =>
            string.Equals(candidate.Name.Trim(), dependencyName, StringComparison.OrdinalIgnoreCase));
        return dependency is null
            ? null
            : CreateHealthCell(dependency.State) with { Detail = "Reported by application" };
    }

    private static DashboardStatusCellResponse CreateHealthCell(ApplicationHealthState state) => state switch
    {
        ApplicationHealthState.Healthy => new("healthy", "Healthy", null),
        ApplicationHealthState.Degraded => new("degraded", "Degraded", null),
        ApplicationHealthState.Unhealthy => new("down", "Unhealthy", null),
        ApplicationHealthState.Unreachable => new("down", "Unreachable", null),
        ApplicationHealthState.NotConfigured => new("unknown", "Not configured", "No health endpoint"),
        _ => new("unknown", "Unknown", "Observation unavailable")
    };

    private static DashboardStatusCellResponse CreateWorkflowCell(GitHubWorkflowState state) => state switch
    {
        GitHubWorkflowState.Queued => new("running", "Queued", "GitHub Actions"),
        GitHubWorkflowState.InProgress => new("running", "In progress", "GitHub Actions"),
        GitHubWorkflowState.Passed => new("healthy", "Passed", "GitHub Actions"),
        GitHubWorkflowState.Failed => new("warning", "Failed", "GitHub Actions"),
        GitHubWorkflowState.Cancelled => new("warning", "Cancelled", "GitHub Actions"),
        GitHubWorkflowState.NotConfigured => new("notApplicable", "N/A", "Workflow not configured"),
        _ => new("unknown", "Unknown", "Observation unavailable")
    };

    private static DashboardStatusCellResponse CreateVersionSyncCell(SurfaceProjection surface) =>
        surface.VersionSyncState switch
        {
            VersionSyncState.InSync => new(
                "healthy",
                "In Sync",
                surface.Response.DeployedVersion?.CommitShortSha),
            VersionSyncState.Behind => new(
                "warning",
                "Behind",
                surface.Response.VersionSync.CommitsBehind is int commitsBehind
                    ? $"{commitsBehind} commit{(commitsBehind == 1 ? string.Empty : "s")} behind"
                    : null),
            VersionSyncState.NotConfigured => new("unknown", "Not configured", "No version endpoint"),
            _ => new("unknown", "Unknown", "Comparison unavailable")
        };

    private static DashboardActivityResponse CreateActivity(DashboardActivityData activity)
    {
        string title = activity.Type switch
        {
            MonitoringActivityType.HealthFailed => $"{activity.ProjectName} health failed",
            MonitoringActivityType.HealthRecovered => $"{activity.ProjectName} health recovered",
            MonitoringActivityType.VersionDrift => $"{activity.ProjectName} version drift detected",
            MonitoringActivityType.VersionSynchronized => $"{activity.ProjectName} version synchronized",
            _ => throw new InvalidOperationException($"Unsupported activity type: {activity.Type}.")
        };

        return new DashboardActivityResponse(
            activity.Id,
            ToCamelCase(activity.Type),
            title,
            activity.EnvironmentName,
            activity.OccurredAtUtc);
    }

    private static DashboardSystemSummaryResponse CreateSummary(
        OperationalSummaryLevel level,
        DashboardUptimeWindowResponse? uptime) => new(
        ToCamelCase(level),
        level switch
        {
            OperationalSummaryLevel.Healthy => "All Systems Operational",
            OperationalSummaryLevel.Warning => "Attention Required",
            OperationalSummaryLevel.Degraded => "Systems Degraded",
            OperationalSummaryLevel.Down => "Systems Down",
            _ => "System State Unknown"
        },
        uptime);

    private static DashboardStatusCellResponse CreateSummaryCell(OperationalSummaryLevel level) => level switch
    {
        OperationalSummaryLevel.Healthy => new("healthy", "Operational", "All configured signals acceptable"),
        OperationalSummaryLevel.Warning => new("warning", "Attention required", "Some signals need attention"),
        OperationalSummaryLevel.Degraded => new("degraded", "Degraded", "Application or dependency degraded"),
        OperationalSummaryLevel.Down => new("down", "Down", "One or more applications are unavailable"),
        _ => new("unknown", "Unknown", "No reliable current observations")
    };

    private static bool IsCurrent(DateTimeOffset? observedAtUtc, DateTimeOffset? configurationChangedAtUtc) =>
        observedAtUtc is not null
        && (configurationChangedAtUtc is null || observedAtUtc >= configurationChangedAtUtc);

    private static string DependencyKey(string name) =>
        $"dependency:{name.Trim().ToLowerInvariant()}";

    private static string ToCamelCase<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        string text = value.ToString();
        return char.ToLowerInvariant(text[0]) + text[1..];
    }

    private sealed record SurfaceProjection(
        DashboardProjectSurfaceResponse Response,
        bool SourceReliable,
        GitHubWorkflowState WorkflowState,
        ApplicationHealthState HealthState,
        VersionSyncState VersionSyncState,
        IReadOnlyList<DashboardDependencyData> Dependencies,
        OperationalSurfaceAssessment Assessment);
}
