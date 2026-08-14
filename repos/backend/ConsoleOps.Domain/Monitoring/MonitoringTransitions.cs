namespace ConsoleOps.Domain.Monitoring;

public enum MonitoringCondition
{
    Acceptable,
    Failure,
    Indeterminate
}

public enum MonitoringActivityType
{
    HealthFailed,
    HealthRecovered,
    VersionDrift,
    VersionSynchronized
}

public static class MonitoringTransitions
{
    public static MonitoringActivityType? DetectHealth(
        MonitoringCondition? previous,
        MonitoringCondition current)
    {
        if (previous is null || current == MonitoringCondition.Indeterminate)
        {
            return null;
        }

        return (previous.Value, current) switch
        {
            (MonitoringCondition.Acceptable, MonitoringCondition.Failure) =>
                MonitoringActivityType.HealthFailed,
            (MonitoringCondition.Failure, MonitoringCondition.Acceptable) =>
                MonitoringActivityType.HealthRecovered,
            _ => null
        };
    }

    public static MonitoringActivityType? DetectVersionSync(
        VersionSyncState? previous,
        VersionSyncState current)
    {
        if (previous is null)
        {
            return null;
        }

        return (previous.Value, current) switch
        {
            (VersionSyncState.InSync, VersionSyncState.Behind) =>
                MonitoringActivityType.VersionDrift,
            (VersionSyncState.Behind, VersionSyncState.InSync) =>
                MonitoringActivityType.VersionSynchronized,
            _ => null
        };
    }
}
