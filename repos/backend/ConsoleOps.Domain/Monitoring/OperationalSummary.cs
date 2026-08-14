namespace ConsoleOps.Domain.Monitoring;

public enum OperationalSummaryLevel
{
    Healthy,
    Warning,
    Degraded,
    Down,
    Unknown
}

public readonly record struct OperationalSurfaceAssessment(
    bool HasReliableObservation,
    bool ApplicationIsDown,
    bool IsDegraded,
    bool RequiresAttention);

public static class OperationalSummary
{
    public static OperationalSummaryLevel Calculate(
        IReadOnlyCollection<OperationalSurfaceAssessment> surfaces)
    {
        ArgumentNullException.ThrowIfNull(surfaces);

        if (surfaces.Any(surface => surface.ApplicationIsDown))
        {
            return OperationalSummaryLevel.Down;
        }

        if (surfaces.Any(surface => surface.IsDegraded))
        {
            return OperationalSummaryLevel.Degraded;
        }

        if (!surfaces.Any(surface => surface.HasReliableObservation))
        {
            return OperationalSummaryLevel.Unknown;
        }

        return surfaces.Any(surface => surface.RequiresAttention)
            ? OperationalSummaryLevel.Warning
            : OperationalSummaryLevel.Healthy;
    }
}
