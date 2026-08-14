namespace ConsoleOps.Domain.Monitoring;

public enum VersionSyncState
{
    InSync,
    Behind,
    Unknown,
    NotConfigured
}

public readonly record struct VersionSyncAssessment(
    VersionSyncState State,
    int? CommitsBehind);

public static class VersionSync
{
    public static VersionSyncAssessment Calculate(
        bool versionConfigured,
        string? sourceCommitSha,
        string? deployedCommitSha,
        int? provenCommitsBehind)
    {
        if (!versionConfigured)
        {
            return new VersionSyncAssessment(VersionSyncState.NotConfigured, null);
        }

        if (!IsFullCommitSha(sourceCommitSha) || !IsFullCommitSha(deployedCommitSha))
        {
            return new VersionSyncAssessment(VersionSyncState.Unknown, null);
        }

        if (string.Equals(sourceCommitSha, deployedCommitSha, StringComparison.OrdinalIgnoreCase))
        {
            return new VersionSyncAssessment(VersionSyncState.InSync, null);
        }

        return provenCommitsBehind is > 0
            ? new VersionSyncAssessment(VersionSyncState.Behind, provenCommitsBehind)
            : new VersionSyncAssessment(VersionSyncState.Unknown, null);
    }

    public static bool IsFullCommitSha(string? value) =>
        value is { Length: 40 or 64 } && value.All(Uri.IsHexDigit);
}
