namespace ConsoleOps.Application.Integrations.GitHub;

public sealed record GitHubProjectReference(
    string Owner,
    string Repository,
    string DefaultBranch,
    string? WorkflowFile);

public sealed record GitHubProjectReadResult(
    GitHubFactResult<GitHubSourceObservation> Source,
    GitHubFactResult<GitHubWorkflowObservation> Workflow,
    IReadOnlyList<GitHubCommitComparison> CommitComparisons,
    IReadOnlyList<GitHubWorkflowRun> WorkflowRuns);

/// <summary>
/// One completed or in-flight run of the project's configured workflow.
/// <para>
/// This is the release history Console Ops can actually establish: GitHub reports the run, its commit,
/// and its outcome. Which environment the run reached is deliberately absent, because a V1 project
/// configures one workflow for the whole project and GitHub does not tell us where the artifact landed.
/// That link is established later from runtime version observations, never guessed here.
/// </para>
/// </summary>
/// <param name="TriggeredBy">
/// Login of the account that started the run, or <c>null</c> when GitHub omitted it.
/// </param>
/// <param name="RunUrl">Absolute GitHub run URL, or <c>null</c> when it could not be trusted.</param>
public sealed record GitHubWorkflowRun(
    long RunId,
    int? RunNumber,
    string? WorkflowFile,
    string? WorkflowName,
    string Branch,
    string CommitSha,
    GitHubWorkflowState State,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    string? TriggeredBy,
    string? RunUrl,
    DateTimeOffset ObservedAtUtc);

public sealed record GitHubCommitComparison(
    string DeployedCommitSha,
    string SourceCommitSha,
    GitHubCommitRelation Relation,
    int? CommitsBehind,
    GitHubReadFailure? Failure,
    DateTimeOffset ObservedAtUtc);

public enum GitHubCommitRelation
{
    DeployedIsAncestor,
    Identical,
    Unknown
}

public sealed record GitHubSourceObservation(
    string Repository,
    string DefaultBranch,
    string CommitSha,
    string ShortCommitSha,
    DateTimeOffset? CommittedAtUtc,
    DateTimeOffset ObservedAtUtc);

public sealed record GitHubWorkflowObservation(
    string? WorkflowFile,
    string? WorkflowName,
    GitHubWorkflowState State,
    string? CommitSha,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    DateTimeOffset ObservedAtUtc);

public enum GitHubWorkflowState
{
    Queued,
    InProgress,
    Passed,
    Failed,
    Cancelled,
    Unknown,
    NotConfigured
}

public enum GitHubReadFailure
{
    Unauthorized,
    NotFound,
    RateLimited,
    Unavailable,
    InvalidResponse
}

public sealed class GitHubFactResult<TObservation>
    where TObservation : class
{
    private GitHubFactResult(TObservation? observation, GitHubReadFailure? failure)
    {
        Observation = observation;
        Failure = failure;
    }

    public TObservation? Observation { get; }

    public GitHubReadFailure? Failure { get; }

    public bool IsSuccess => Failure is null;

    public static GitHubFactResult<TObservation> Success(TObservation observation) =>
        new(observation ?? throw new ArgumentNullException(nameof(observation)), null);

    public static GitHubFactResult<TObservation> Failed(GitHubReadFailure failure) =>
        new(null, failure);
}
