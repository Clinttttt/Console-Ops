using ConsoleOps.Application.Abstractions.Messaging;
using ConsoleOps.Application.Integrations.Diagnostics;
using MediatR;

namespace ConsoleOps.Application.Features.Settings.GetConfigurationStatus;

/// <param name="Probe">
/// Also test the credentials, rather than only reporting that they are present. Off by default: a probe
/// contacts each provider, and a screen that loads should not spend seconds proving what it can state cheaply.
/// </param>
public sealed record GetConfigurationStatusQuery(bool Probe = false)
    : IRequest<Result<ConfigurationStatusResponse>>;

/// <param name="Capabilities">
/// One entry per thing Console Ops needs configured, so the screen reports capabilities rather than keys.
/// </param>
/// <param name="Probed">Whether credentials were tested, so the UI never implies a test that did not run.</param>
/// <param name="About">Which build is running, and whether its schema matches.</param>
/// <param name="Collection">How collection is scheduled, and how the last sweep went.</param>
public sealed record ConfigurationStatusResponse(
    DateTimeOffset ObservedAt,
    IReadOnlyList<CapabilityStatusResponse> Capabilities,
    bool Probed,
    AboutConsoleOpsResponse About,
    CollectionStatusResponse Collection);

/// <param name="IsEnabled">Whether scheduled collection runs at all, which is a configuration, not a fault.</param>
/// <param name="LastSweepAt">
/// When the last sweep finished, or <c>null</c> when none has run since start-up. Sweeps are remembered for this
/// process only: they describe Console Ops, not a project, and are not written to the observation tables.
/// </param>
/// <param name="NextSweepAt">
/// When the next sweep is due, or <c>null</c> when collection is off or nothing has run yet. Derived from the
/// last sweep's start and the interval, so it is an expectation rather than a promise.
/// </param>
public sealed record CollectionStatusResponse(
    bool IsEnabled,
    int IntervalSeconds,
    DateTimeOffset? LastSweepAt,
    bool? LastSweepSucceeded,
    int? LastSweepMilliseconds,
    int? ProjectsRefreshed,
    int? ProjectsFailed,
    DateTimeOffset? NextSweepAt);

/// <param name="Build">The source revision the build came from, or <c>null</c> when it recorded none.</param>
/// <param name="DatabaseSchema"><c>upToDate</c>, <c>pendingMigrations</c>, or <c>unknown</c>.</param>
public sealed record AboutConsoleOpsResponse(
    string Version,
    string? Build,
    string Runtime,
    string DatabaseSchema);

/// <param name="State">
/// <c>configured</c>, <c>missing</c>, or <c>default</c>. The worst state among the capability's keys, because
/// one missing required key is what stops it working.
/// </param>
/// <param name="Keys">The keys behind the verdict, by name only. Never a value.</param>
/// <param name="Connection">
/// The result of testing the credentials, or <c>null</c> when no test was asked for or none exists for this
/// capability. Absent is not failure.
/// </param>
public sealed record CapabilityStatusResponse(
    string Capability,
    string State,
    IReadOnlyList<ConfigurationKeyResponse> Keys,
    ConnectionCheckResponse? Connection);

public sealed record ConfigurationKeyResponse(string Key, string State, bool Required);

/// <param name="Failure">Why the test failed, in the operator's terms and never a credential.</param>
public sealed record ConnectionCheckResponse(bool Succeeded, string? Failure);

/// <summary>
/// Reports what Console Ops has been configured with, by key name only, and optionally whether those
/// credentials work.
/// <para>
/// This exists because a missing GitHub token once looked like an empty repository list. Every fact here is
/// either "a key is set" or "a provider answered"; no value is read, returned, or logged.
/// </para>
/// </summary>
public sealed class GetConfigurationStatusQueryHandler(
    IConfigurationInspector inspector,
    IEnumerable<IIntegrationProbe> probes,
    IConsoleOpsBuildInfo buildInfo,
    ICollectionJournal journal,
    TimeProvider timeProvider)
    : IRequestHandler<GetConfigurationStatusQuery, Result<ConfigurationStatusResponse>>
{
    public async Task<Result<ConfigurationStatusResponse>> Handle(
        GetConfigurationStatusQuery request,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ConfigurationKeyStatus> keys = inspector.Inspect();
        Dictionary<string, ConnectionCheckResponse> connections = request.Probe
            ? await ProbeAllAsync(cancellationToken)
            : [];

        CapabilityStatusResponse[] capabilities = keys
            .GroupBy(key => key.Capability, StringComparer.Ordinal)
            .Select(group => new CapabilityStatusResponse(
                group.Key,
                ToCamelCase(Worst(group)),
                [.. group.Select(key => new ConfigurationKeyResponse(
                    key.Key,
                    ToCamelCase(key.State),
                    key.IsRequired))],
                connections.GetValueOrDefault(group.Key)))
            .OrderBy(capability => capability.Capability, StringComparer.Ordinal)
            .ToArray();

        ConsoleOpsBuild build = await buildInfo.ReadAsync(cancellationToken);

        return Result<ConfigurationStatusResponse>.Success(new ConfigurationStatusResponse(
            timeProvider.GetUtcNow(),
            capabilities,
            request.Probe,
            new AboutConsoleOpsResponse(build.Version, build.Build, build.Runtime, build.SchemaState),
            ToCollectionStatus(journal)));
    }

    /// <summary>
    /// Collection as it stands. Every value is either configuration or a sweep that actually ran; nothing is
    /// filled in for a sweep that has not happened.
    /// </summary>
    private static CollectionStatusResponse ToCollectionStatus(ICollectionJournal journal)
    {
        CollectionSchedule schedule = journal.Schedule;
        CollectionSweep? sweep = journal.LastSweep;

        if (sweep is null)
        {
            return new CollectionStatusResponse(
                schedule.IsEnabled,
                (int)schedule.Interval.TotalSeconds,
                null,
                null,
                null,
                null,
                null,
                null);
        }

        return new CollectionStatusResponse(
            schedule.IsEnabled,
            (int)schedule.Interval.TotalSeconds,
            sweep.CompletedAt,
            sweep.Succeeded,
            (int)sweep.Duration.TotalMilliseconds,
            sweep.ProjectsRefreshed,
            sweep.ProjectsFailed,
            // The timer runs from the start of a sweep, not from its end, so the next one is due an interval
            // after this one began. Off means nothing is due at all.
            schedule.IsEnabled ? sweep.StartedAt + schedule.Interval : null);
    }

    /// <summary>
    /// Runs every probe. One provider being unreachable must not hide the state of the others, so a probe that
    /// throws is reported as a failed check rather than failing the request.
    /// </summary>
    private async Task<Dictionary<string, ConnectionCheckResponse>> ProbeAllAsync(
        CancellationToken cancellationToken)
    {
        Dictionary<string, ConnectionCheckResponse> results = new(StringComparer.Ordinal);

        foreach (IIntegrationProbe probe in probes)
        {
            try
            {
                IntegrationProbeResult result = await probe.ProbeAsync(cancellationToken);
                results[probe.Capability] = new ConnectionCheckResponse(result.Succeeded, result.Failure);
            }
            catch (Exception failure) when (failure is not OperationCanceledException)
            {
                results[probe.Capability] = new ConnectionCheckResponse(false, "The check could not be completed.");
            }
        }

        return results;
    }

    /// <summary>
    /// A missing required key decides the verdict. A key that is optional and unset reads as a default, not as
    /// configured, because something else is standing in for it - an ambient credential, or a built-in value.
    /// </summary>
    private static ConfigurationKeyState Worst(IEnumerable<ConfigurationKeyStatus> keys)
    {
        ConfigurationKeyState worst = ConfigurationKeyState.Configured;

        foreach (ConfigurationKeyStatus key in keys)
        {
            if (key.State == ConfigurationKeyState.Missing && key.IsRequired)
            {
                return ConfigurationKeyState.Missing;
            }

            if (key.State != ConfigurationKeyState.Configured && worst == ConfigurationKeyState.Configured)
            {
                worst = ConfigurationKeyState.Default;
            }
        }

        return worst;
    }

    private static string ToCamelCase(ConfigurationKeyState state)
    {
        string text = state.ToString();
        return char.ToLowerInvariant(text[0]) + text[1..];
    }
}
