using ConsoleOps.Application.Abstractions.Messaging;
using ConsoleOps.Application.Integrations.ApplicationMonitoring;
using ConsoleOps.Application.Integrations.GitHub;
using ConsoleOps.Domain.Monitoring;
using MediatR;

namespace ConsoleOps.Application.Features.Projects.RefreshProject;

public sealed class RefreshProjectCommandHandler(
    IProjectRefreshStore refreshStore,
    IGitHubProjectReader gitHubReader,
    IApplicationProbe applicationProbe,
    ProjectRefreshCoordinator refreshCoordinator,
    TimeProvider timeProvider)
    : IRequestHandler<RefreshProjectCommand, Result<RefreshProjectResponse>>
{
    private const int MaximumConcurrentEnvironmentProbes = 4;

    public async Task<Result<RefreshProjectResponse>> Handle(
        RefreshProjectCommand request,
        CancellationToken cancellationToken)
    {
        await using IAsyncDisposable refreshLease = await refreshCoordinator.AcquireAsync(
            request.ProjectId,
            cancellationToken);
        ProjectRefreshContext? context = await refreshStore.GetContextAsync(
            request.ProjectId,
            cancellationToken);
        if (context is null)
        {
            return Result<RefreshProjectResponse>.Failure(ProjectErrors.NotFound);
        }

        ApplicationProbeResult[] probeResults = await ProbeEnvironmentsAsync(
            context.Environments,
            cancellationToken);
        string[] deployedCommitShas = probeResults
            .Select(result => result.Version.CommitSha)
            .Where(commit => commit is not null)
            .Cast<string>()
            .ToArray();
        GitHubProjectReadResult gitHub = await gitHubReader.ReadAsync(
            new GitHubProjectReference(
                context.RepositoryOwner,
                context.RepositoryName,
                context.DefaultBranch,
                context.WorkflowFile),
            deployedCommitShas,
            cancellationToken);
        DateTimeOffset refreshedAtUtc = timeProvider.GetUtcNow();

        SourceObservationWriteModel source = CreateSource(context, gitHub, refreshedAtUtc);
        WorkflowObservationWriteModel workflow = CreateWorkflow(context, gitHub, refreshedAtUtc);
        List<EnvironmentObservationWriteModel> environments = new(context.Environments.Count);
        List<ActivityWriteModel> activities = [];

        for (int index = 0; index < context.Environments.Count; index++)
        {
            ProjectRefreshEnvironment environment = context.Environments[index];
            ApplicationProbeResult probe = probeResults[index];
            int? provenCommitsBehind = GetProvenCommitsBehind(
                gitHub,
                probe.Version.CommitSha);
            VersionSyncAssessment versionSync = VersionSync.Calculate(
                !string.IsNullOrWhiteSpace(environment.VersionUrl),
                source.CommitSha,
                probe.Version.CommitSha,
                provenCommitsBehind);
            DateTimeOffset syncObservedAtUtc = timeProvider.GetUtcNow();
            EnvironmentObservationWriteModel observation = new(
                environment.Id,
                environment.Name,
                environment.Kind,
                probe.Health,
                probe.Version,
                versionSync,
                syncObservedAtUtc);
            environments.Add(observation);

            context.Baselines.TryGetValue(environment.Id, out EnvironmentMonitoringBaseline? baseline);
            AddTransitionActivities(
                environment,
                baseline,
                observation,
                activities);
        }

        ProjectRefreshWriteModel writeModel = new(
            context.ProjectId,
            context.ConfigurationVersion,
            refreshedAtUtc,
            source,
            workflow,
            environments,
            activities,
            CreateDeployments(gitHub));
        ProjectRefreshSaveOutcome saveOutcome = await refreshStore.SaveAsync(
            writeModel,
            cancellationToken);
        if (saveOutcome != ProjectRefreshSaveOutcome.Saved)
        {
            return saveOutcome switch
            {
                ProjectRefreshSaveOutcome.ProjectNotActive =>
                    Result<RefreshProjectResponse>.Failure(ProjectErrors.NotFound),
                ProjectRefreshSaveOutcome.ConfigurationConflict =>
                    Result<RefreshProjectResponse>.Failure(ProjectErrors.ConfigurationConflict),
                _ => throw new InvalidOperationException($"Unsupported refresh save outcome: {saveOutcome}.")
            };
        }

        return Result<RefreshProjectResponse>.Success(ToResponse(writeModel));
    }

    private async Task<ApplicationProbeResult[]> ProbeEnvironmentsAsync(
        IReadOnlyList<ProjectRefreshEnvironment> environments,
        CancellationToken cancellationToken)
    {
        ApplicationProbeResult[] results = new ApplicationProbeResult[environments.Count];

        await Parallel.ForEachAsync(
            Enumerable.Range(0, environments.Count),
            new ParallelOptions
            {
                MaxDegreeOfParallelism = MaximumConcurrentEnvironmentProbes,
                CancellationToken = cancellationToken
            },
            async (index, token) =>
            {
                ProjectRefreshEnvironment environment = environments[index];
                results[index] = await applicationProbe.ProbeAsync(
                    new ApplicationProbeTarget(environment.HealthUrl, environment.VersionUrl),
                    token);
            });

        return results;
    }

    private static SourceObservationWriteModel CreateSource(
        ProjectRefreshContext context,
        GitHubProjectReadResult gitHub,
        DateTimeOffset fallbackObservedAtUtc)
    {
        GitHubSourceObservation? observation = gitHub.Source.Observation;
        return new SourceObservationWriteModel(
            observation is not null,
            $"{context.RepositoryOwner}/{context.RepositoryName}",
            context.DefaultBranch,
            observation?.CommitSha,
            observation?.ShortCommitSha,
            observation?.CommittedAtUtc,
            gitHub.Source.Failure,
            observation?.ObservedAtUtc ?? fallbackObservedAtUtc);
    }

    private static WorkflowObservationWriteModel CreateWorkflow(
        ProjectRefreshContext context,
        GitHubProjectReadResult gitHub,
        DateTimeOffset fallbackObservedAtUtc)
    {
        GitHubWorkflowObservation? observation = gitHub.Workflow.Observation;
        return new WorkflowObservationWriteModel(
            observation?.WorkflowFile ?? context.WorkflowFile,
            observation?.WorkflowName,
            observation?.State ?? GitHubWorkflowState.Unknown,
            observation?.CommitSha,
            observation?.StartedAtUtc,
            observation?.CompletedAtUtc,
            gitHub.Workflow.Failure,
            observation?.ObservedAtUtc ?? fallbackObservedAtUtc);
    }

    /// <summary>
    /// Records the workflow runs GitHub reported for this project as releases.
    /// <para>
    /// No environment is attributed here. A run proves that CI produced a commit, not where it landed;
    /// the Deployments query establishes that link from runtime version observations.
    /// </para>
    /// </summary>
    private static IReadOnlyList<DeploymentRunWriteModel> CreateDeployments(
        GitHubProjectReadResult gitHub) =>
        gitHub.WorkflowRuns
            .Select(run => new DeploymentRunWriteModel(
                run.RunId,
                run.RunNumber,
                run.WorkflowFile,
                run.WorkflowName,
                run.Branch,
                run.CommitSha,
                run.State,
                run.StartedAtUtc,
                run.CompletedAtUtc,
                run.TriggeredBy,
                run.RunUrl,
                run.ObservedAtUtc))
            .ToArray();

    private static int? GetProvenCommitsBehind(
        GitHubProjectReadResult gitHub,
        string? deployedCommitSha)
    {
        if (deployedCommitSha is null || gitHub.Source.Observation is null)
        {
            return null;
        }

        GitHubCommitComparison? comparison = gitHub.CommitComparisons.FirstOrDefault(candidate =>
            string.Equals(
                candidate.DeployedCommitSha,
                deployedCommitSha,
                StringComparison.OrdinalIgnoreCase)
            && string.Equals(
                candidate.SourceCommitSha,
                gitHub.Source.Observation.CommitSha,
                StringComparison.OrdinalIgnoreCase));

        return comparison?.Relation == GitHubCommitRelation.DeployedIsAncestor
            ? comparison.CommitsBehind
            : null;
    }

    private static void AddTransitionActivities(
        ProjectRefreshEnvironment environment,
        EnvironmentMonitoringBaseline? baseline,
        EnvironmentObservationWriteModel current,
        ICollection<ActivityWriteModel> activities)
    {
        MonitoringActivityType? healthActivity = MonitoringTransitions.DetectHealth(
            baseline?.HealthState is null ? null : ToCondition(baseline.HealthState.Value),
            ToCondition(current.Health.State));
        if (healthActivity is not null)
        {
            activities.Add(new ActivityWriteModel(
                environment.Id,
                environment.Name,
                healthActivity.Value,
                current.Health.ObservedAtUtc));
        }

        MonitoringActivityType? syncActivity = MonitoringTransitions.DetectVersionSync(
            baseline?.VersionSyncState,
            current.VersionSync.State);
        if (syncActivity is not null)
        {
            activities.Add(new ActivityWriteModel(
                environment.Id,
                environment.Name,
                syncActivity.Value,
                current.VersionSyncObservedAtUtc));
        }
    }

    private static MonitoringCondition ToCondition(ApplicationHealthState state) => state switch
    {
        ApplicationHealthState.Healthy or ApplicationHealthState.Degraded =>
            MonitoringCondition.Acceptable,
        ApplicationHealthState.Unhealthy or ApplicationHealthState.Unreachable =>
            MonitoringCondition.Failure,
        _ => MonitoringCondition.Indeterminate
    };

    private static RefreshProjectResponse ToResponse(ProjectRefreshWriteModel refresh) => new(
        refresh.ProjectId,
        refresh.RefreshedAtUtc,
        new RefreshSourceResponse(
            refresh.Source.IsAvailable ? "available" : "unknown",
            refresh.Source.Repository,
            refresh.Source.DefaultBranch,
            refresh.Source.CommitSha,
            refresh.Source.ShortCommitSha,
            refresh.Source.CommittedAtUtc,
            refresh.Source.ObservedAtUtc),
        new RefreshWorkflowResponse(
            "githubActions",
            refresh.Workflow.WorkflowFile,
            refresh.Workflow.WorkflowName,
            ToCamelCase(refresh.Workflow.State),
            refresh.Workflow.CommitSha,
            refresh.Workflow.StartedAtUtc,
            refresh.Workflow.CompletedAtUtc,
            refresh.Workflow.ObservedAtUtc),
        refresh.Environments.Select(ToResponse).ToArray(),
        refresh.Activities.Select(activity => new RefreshActivityResponse(
            activity.EnvironmentId,
            activity.EnvironmentName,
            ToCamelCase(activity.Type),
            activity.OccurredAtUtc)).ToArray());

    private static RefreshEnvironmentResponse ToResponse(
        EnvironmentObservationWriteModel environment) => new(
        environment.EnvironmentId,
        environment.EnvironmentName,
        environment.EnvironmentKind,
        new RefreshHealthResponse(
            ToCamelCase(environment.Health.State),
            environment.Health.ResponseDuration?.TotalMilliseconds,
            environment.Health.ObservedAtUtc,
            environment.Health.Dependencies.Select(dependency =>
                new RefreshDependencyResponse(
                    dependency.Name,
                    ToCamelCase(dependency.State))).ToArray()),
        new RefreshVersionResponse(
            ToCamelCase(environment.Version.State),
            environment.Version.Application,
            environment.Version.Version,
            environment.Version.CommitSha,
            environment.Version.Environment,
            environment.Version.BuiltAtUtc,
            environment.Version.ObservedAtUtc),
        new RefreshVersionSyncResponse(
            ToCamelCase(environment.VersionSync.State),
            environment.VersionSync.CommitsBehind,
            environment.VersionSyncObservedAtUtc));

    private static string ToCamelCase<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        string text = value.ToString();
        return char.ToLowerInvariant(text[0]) + text[1..];
    }
}
