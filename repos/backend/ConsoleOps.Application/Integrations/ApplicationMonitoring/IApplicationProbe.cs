namespace ConsoleOps.Application.Integrations.ApplicationMonitoring;

public interface IApplicationProbe
{
    Task<ApplicationProbeResult> ProbeAsync(
        ApplicationProbeTarget target,
        CancellationToken cancellationToken);
}
