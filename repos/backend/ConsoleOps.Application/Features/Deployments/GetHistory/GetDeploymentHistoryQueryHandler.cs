using ConsoleOps.Application.Integrations.ApplicationMonitoring;
using ConsoleOps.Domain.Monitoring;
using MediatR;

namespace ConsoleOps.Application.Features.Deployments.GetHistory;

/// <summary>
/// Projects recorded releases onto the transport contract.
/// <para>
/// Every value here is either copied from a record or computed from two recorded instants. Nothing is
/// inferred: an unestablished fact becomes <c>unknown</c> or <c>null</c>, never a plausible default.
/// </para>
/// </summary>
public sealed class GetDeploymentHistoryQueryHandler(
    IDeploymentHistoryReadStore readStore,
    TimeProvider timeProvider)
    : IRequestHandler<GetDeploymentHistoryQuery, DeploymentHistoryResponse>
{
    internal const int DefaultLimit = 100;
    internal const int MaximumLimit = 200;

    public async Task<DeploymentHistoryResponse> Handle(
        GetDeploymentHistoryQuery request,
        CancellationToken cancellationToken)
    {
        int limit = Math.Clamp(request.Limit ?? DefaultLimit, 1, MaximumLimit);
        DeploymentHistoryData data = await readStore.ReadAsync(limit, cancellationToken);

        return new DeploymentHistoryResponse(
            timeProvider.GetUtcNow(),
            data.Deployments.Select(CreateDeployment).ToArray());
    }

    private static DeploymentResponse CreateDeployment(DeploymentRecordData deployment) => new(
        deployment.Id,
        deployment.ProjectId,
        deployment.ProjectName,
        "githubActions",
        deployment.Repository,
        deployment.Branch,
        deployment.CommitSha,
        deployment.CommitSha[..Math.Min(7, deployment.CommitSha.Length)],
        ToCamelCase(deployment.Result),
        deployment.WorkflowFile,
        deployment.WorkflowName,
        deployment.RunUrl,
        deployment.RunNumber,
        deployment.TriggeredBy,
        deployment.StartedAtUtc,
        deployment.CompletedAtUtc,
        deployment.CompletedAtUtc ?? deployment.StartedAtUtc ?? deployment.RecordedAtUtc,
        CalculateDurationSeconds(deployment.StartedAtUtc, deployment.CompletedAtUtc),
        deployment.RecordedAtUtc,
        deployment.Environments.Select(CreateEnvironment).ToArray());

    private static DeploymentEnvironmentResponse CreateEnvironment(
        DeploymentEnvironmentData environment) => new(
        new DeploymentEnvironmentRefResponse(
            environment.EnvironmentId,
            environment.EnvironmentName,
            environment.EnvironmentKind),
        environment.IsCurrent,
        environment.FirstObservedAtUtc,
        ToCamelCase(environment.HealthBefore ?? ApplicationHealthState.Unknown),
        environment.HealthBeforeObservedAtUtc,
        ToCamelCase(environment.HealthAfter ?? ApplicationHealthState.Unknown),
        environment.HealthAfterObservedAtUtc,
        ToCamelCase(environment.VersionSync ?? VersionSyncState.Unknown),
        environment.VersionSyncObservedAtUtc);

    /// <summary>
    /// Duration only when both ends are known and ordered. A negative or missing interval is reported as
    /// unknown rather than clamped to zero.
    /// </summary>
    private static int? CalculateDurationSeconds(
        DateTimeOffset? startedAtUtc,
        DateTimeOffset? completedAtUtc)
    {
        if (startedAtUtc is null || completedAtUtc is null || completedAtUtc < startedAtUtc)
        {
            return null;
        }

        return (int)Math.Round((completedAtUtc.Value - startedAtUtc.Value).TotalSeconds);
    }

    private static string ToCamelCase<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        string text = value.ToString();
        return char.ToLowerInvariant(text[0]) + text[1..];
    }
}
