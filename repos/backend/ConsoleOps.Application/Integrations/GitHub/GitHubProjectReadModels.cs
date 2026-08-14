namespace ConsoleOps.Application.Integrations.GitHub;

public sealed record GitHubProjectReference(
    string Owner,
    string Repository,
    string DefaultBranch,
    string? WorkflowFile);

public sealed record GitHubProjectReadResult(
    GitHubFactResult<GitHubSourceObservation> Source,
    GitHubFactResult<GitHubWorkflowObservation> Workflow,
    IReadOnlyList<GitHubCommitComparison> CommitComparisons);

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
