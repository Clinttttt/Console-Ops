using ConsoleOps.Domain.Monitoring;

namespace ConsoleOps.Tests.Unit.Domain;

public sealed class MonitoringTests
{
    private const string SourceSha = "0123456789abcdef0123456789abcdef01234567";
    private const string DeployedSha = "89abcdef0123456789abcdef0123456789abcdef";

    [Fact]
    public void VersionSync_RequiresFullCommitEvidence()
    {
        Assert.Equal(
            VersionSyncState.NotConfigured,
            VersionSync.Calculate(false, SourceSha, SourceSha, null).State);
        Assert.Equal(
            VersionSyncState.Unknown,
            VersionSync.Calculate(true, "0123456", "0123456", null).State);
        VersionSyncAssessment inSync = VersionSync.Calculate(
            true,
            SourceSha,
            SourceSha.ToUpperInvariant(),
            null);
        Assert.Equal(VersionSyncState.InSync, inSync.State);
        Assert.Null(inSync.CommitsBehind);
        Assert.Equal(
            VersionSyncState.Unknown,
            VersionSync.Calculate(true, SourceSha, DeployedSha, null).State);
    }

    [Fact]
    public void VersionSync_ReturnsBehindOnlyWithProvenPositiveDistance()
    {
        VersionSyncAssessment assessment = VersionSync.Calculate(
            true,
            SourceSha,
            DeployedSha,
            3);

        Assert.Equal(VersionSyncState.Behind, assessment.State);
        Assert.Equal(3, assessment.CommitsBehind);
    }

    [Theory]
    [InlineData(MonitoringCondition.Acceptable, MonitoringCondition.Failure, MonitoringActivityType.HealthFailed)]
    [InlineData(MonitoringCondition.Failure, MonitoringCondition.Acceptable, MonitoringActivityType.HealthRecovered)]
    [InlineData(MonitoringCondition.Acceptable, MonitoringCondition.Indeterminate, null)]
    [InlineData(MonitoringCondition.Failure, MonitoringCondition.Failure, null)]
    public void HealthTransitions_EmitOnlyDeterministicChanges(
        MonitoringCondition previous,
        MonitoringCondition current,
        MonitoringActivityType? expected)
    {
        Assert.Equal(expected, MonitoringTransitions.DetectHealth(previous, current));
    }

    [Fact]
    public void Transitions_FirstObservationDoesNotEmitActivity()
    {
        Assert.Null(MonitoringTransitions.DetectHealth(null, MonitoringCondition.Failure));
        Assert.Null(MonitoringTransitions.DetectVersionSync(null, VersionSyncState.Behind));
    }

    [Theory]
    [InlineData(VersionSyncState.InSync, VersionSyncState.Behind, MonitoringActivityType.VersionDrift)]
    [InlineData(VersionSyncState.Behind, VersionSyncState.InSync, MonitoringActivityType.VersionSynchronized)]
    [InlineData(VersionSyncState.InSync, VersionSyncState.Unknown, null)]
    [InlineData(VersionSyncState.Behind, VersionSyncState.Unknown, null)]
    public void VersionTransitions_EmitOnlyProvenChanges(
        VersionSyncState previous,
        VersionSyncState current,
        MonitoringActivityType? expected)
    {
        Assert.Equal(expected, MonitoringTransitions.DetectVersionSync(previous, current));
    }
}
