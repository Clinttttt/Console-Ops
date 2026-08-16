using ConsoleOps.Application.Integrations.GitHub;

namespace ConsoleOps.Infrastructure.Persistence.Deployments;

/// <summary>
/// A release Console Ops has seen: one run of a project's configured GitHub Actions workflow.
/// <para>
/// Unlike the monitoring observation tables this row is not append-only. A run is re-read on every
/// refresh and its outcome changes while it is in flight, so the row is keyed on the provider's run id
/// and updated in place. <see cref="RecordedAtUtc"/> keeps the moment Console Ops first saw the run and
/// <see cref="ObservedAtUtc"/> the last time it confirmed it.
/// </para>
/// <para>
/// No environment column exists on purpose. GitHub reports that a commit was built, not where it was
/// deployed; the environment link is established from runtime version observations when the history is
/// read.
/// </para>
/// </summary>
public sealed class DeploymentEntity
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }

    /// <summary>GitHub Actions run id. Unique per project and stable across re-reads.</summary>
    public long ExternalRunId { get; set; }

    public int? RunNumber { get; set; }
    public string? WorkflowFile { get; set; }
    public string? WorkflowName { get; set; }
    public string Branch { get; set; } = string.Empty;
    public string CommitSha { get; set; } = string.Empty;
    public GitHubWorkflowState Result { get; set; }
    public DateTimeOffset? StartedAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }

    /// <summary>Account that started the run. Never a token, email, or other credential.</summary>
    public string? TriggeredBy { get; set; }

    /// <summary>Absolute GitHub run URL, validated before it is stored.</summary>
    public string? RunUrl { get; set; }

    public DateTimeOffset RecordedAtUtc { get; set; }
    public DateTimeOffset ObservedAtUtc { get; set; }
}
