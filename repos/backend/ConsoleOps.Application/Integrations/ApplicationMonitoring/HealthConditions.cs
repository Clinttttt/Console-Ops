using ConsoleOps.Domain.Monitoring;

namespace ConsoleOps.Application.Integrations.ApplicationMonitoring;

/// <summary>
/// How an observed health state counts when availability is measured.
/// <para>
/// One definition, because three copies of it existed and a fourth was about to: the dashboard, the refresh
/// handler, and now the Health screen all have to agree on what "up" means, or the same window produces
/// different figures on different screens.
/// </para>
/// </summary>
public static class HealthConditions
{
    /// <summary>
    /// A degraded application answered, so availability counts it as served. That is a different question from
    /// whether it needs attention - the Health screen treats degraded as an active issue while this counts it as
    /// up, and both are true.
    /// </summary>
    public static MonitoringCondition From(ApplicationHealthState state) => state switch
    {
        ApplicationHealthState.Healthy or ApplicationHealthState.Degraded =>
            MonitoringCondition.Acceptable,
        ApplicationHealthState.Unhealthy or ApplicationHealthState.Unreachable =>
            MonitoringCondition.Failure,
        // Unknown, not configured, or running without a health endpoint: not evidence either way, so it is
        // excluded from the ratio rather than charitably counted as available.
        _ => MonitoringCondition.Indeterminate
    };
}
