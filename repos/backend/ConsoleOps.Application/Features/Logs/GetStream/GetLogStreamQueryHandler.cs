using ConsoleOps.Application.Abstractions.Messaging;
using ConsoleOps.Application.Features.Projects;
using ConsoleOps.Application.Integrations.AzureMonitor;
using MediatR;

namespace ConsoleOps.Application.Features.Logs.GetStream;

/// <summary>
/// Reads a scope's logs from its provider and normalizes them for the screen.
/// <para>
/// This is Console Ops' one pass-through provider read: it asks Azure during the request rather than
/// serving stored observations, because copying a log store into Postgres would duplicate it and force a
/// second retention policy on top of the provider's. The bounds that make that acceptable - window, row
/// cap, timeout - live in the adapter.
/// </para>
/// <para>
/// Scopes come from the operator's own configuration. An environment with no log source is not offered,
/// and asking for one is refused rather than answered with an empty stream, because "nothing was logged"
/// and "Console Ops has nowhere to look" are different facts.
/// </para>
/// <para>
/// Markers are woven in from data Console Ops already holds - recorded runs, and the revisions the log
/// rows themselves report. A marker never comes from the log store and never asserts a deployment target,
/// so the screen gains context without gaining a claim the provider did not make.
/// </para>
/// </summary>
public sealed class GetLogStreamQueryHandler(
    IProjectReadStore projects,
    IApplicationLogReader logs,
    ILogMarkerReadStore markers,
    TimeProvider timeProvider)
    : IRequestHandler<GetLogStreamQuery, Result<LogStreamResponse>>
{
    internal const int DefaultLimit = 200;
    internal const int MaximumLimit = 1_000;
    internal const int WindowHours = 24;

    public async Task<Result<LogStreamResponse>> Handle(
        GetLogStreamQuery request,
        CancellationToken cancellationToken)
    {
        ProjectResponse[] all = await projects.ListAsync(cancellationToken);
        LogScope[] scopes = all
            .SelectMany(project => project.Environments
                .Where(environment => environment.LogSource is not null)
                .Select(environment => new LogScope(project, environment)))
            .OrderBy(scope => scope.Project.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(scope => scope.Environment.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        LogStreamScopeResponse[] scopeResponses = scopes.Select(ToScopeResponse).ToArray();
        DateTimeOffset now = timeProvider.GetUtcNow();
        DateTimeOffset to = request.Before ?? now;
        DateTimeOffset earliest = to.AddHours(-WindowHours);
        // A tail read asks only for what has happened since the last one. It is still bounded by the same
        // maximum window, so a stale or hostile cursor cannot widen the provider query.
        DateTimeOffset from = request.Since is { } since && since > earliest && since < to ? since : earliest;
        LogStreamWindowResponse window = new(from, to, (int)Math.Round((to - from).TotalHours), false);

        if (scopes.Length == 0)
        {
            // Nothing is configured to read. The screen says so rather than showing an empty stream.
            return Result<LogStreamResponse>.Failure(LogStreamErrors.NoLogSourceConfigured);
        }

        LogScope? selected = Select(scopes, request);
        if (selected is null)
        {
            return Result<LogStreamResponse>.Failure(LogStreamErrors.ScopeNotFound);
        }

        ProjectLogSourceResponse source = selected.Environment.LogSource!;
        ApplicationLogReadResult result = await logs.ReadAsync(
            new ApplicationLogQuery(
                source.WorkspaceId,
                source.ContainerAppName,
                from,
                to,
                Math.Clamp(request.Limit ?? DefaultLimit, 1, MaximumLimit),
                request.Search,
                !request.IncludeNoise),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return Result<LogStreamResponse>.Failure(
                LogStreamErrors.From(result.Failure ?? ApplicationLogReadFailure.Unavailable));
        }

        LogEventResponse[] events = result.Entries.Select(ToEventResponse).ToArray();
        IReadOnlyList<LogDeploymentMarker> runs = events.Length == 0
            // A marker explains the lines around it. With no lines there is nothing to explain, and markers
            // on their own would be a release history the Deployments screen already tells properly.
            ? []
            : await markers.ReadDeploymentsAsync(
                selected.Project.Id,
                // Markers are bounded by the events on screen, not by the requested window: placing a marker
                // below the oldest line the provider returned would put it where the operator cannot see what
                // it explains.
                events[^1].OccurredAt,
                to,
                cancellationToken);

        return Result<LogStreamResponse>.Success(new LogStreamResponse(
            now,
            scopeResponses,
            ToScopeResponse(selected),
            window with { Truncated = result.Truncated },
            Merge(events, runs),
            new LogStreamNoiseResponse(
                !request.IncludeNoise,
                result.NoiseHidden,
                [
                    .. (result.NoiseByCategory ?? [])
                        .Select(count => new LogStreamNoiseCategoryResponse(count.Category, count.Count))
                ])));
    }

    /// <summary>
    /// Interleaves events with markers, newest first, so a marker sits between the lines it separates.
    /// <para>
    /// Ties place the marker after the events at the same instant in newest-first order, which puts it
    /// above them on screen: a release is the reason for the lines that follow it, not for the ones before.
    /// </para>
    /// </summary>
    private static LogStreamItemResponse[] Merge(
        LogEventResponse[] events,
        IReadOnlyList<LogDeploymentMarker> runs)
    {
        LogMarkerResponse[] all =
        [
            .. runs.Select(ToMarkerResponse),
            .. RevisionMarkers(events)
        ];
        if (all.Length == 0)
        {
            return [.. events];
        }

        return
        [
            .. events
                .Concat<LogStreamItemResponse>(all)
                .OrderByDescending(item => item.OccurredAt)
                .ThenBy(item => item is LogMarkerResponse ? 1 : 0)
        ];
    }

    /// <summary>
    /// One marker for each revision the log rows report, placed at the earliest line Console Ops has from
    /// it.
    /// <para>
    /// Deliberately not "the revision changed between these two lines". During a rollout both revisions
    /// serve at once and their lines interleave, so a change-detecting rule flaps between them and claims a
    /// revision started several times over - observed against a real deployment, which produced three
    /// markers for two revisions. One marker per revision states what was seen without asserting a
    /// lifecycle Console Ops cannot see from console output.
    /// </para>
    /// <para>
    /// The revision that was already serving when the window opened gets no marker: its first line here is
    /// an artifact of where the read began, not of anything happening.
    /// </para>
    /// </summary>
    private static IEnumerable<LogMarkerResponse> RevisionMarkers(LogEventResponse[] events)
    {
        HashSet<string> seen = new(StringComparer.Ordinal);
        for (int index = events.Length - 1; index >= 0; index--)
        {
            if (events[index].Revision is not { Length: > 0 } revision)
            {
                continue;
            }

            if (!seen.Add(revision) || seen.Count == 1)
            {
                continue;
            }

            yield return new LogMarkerResponse(
                // Deterministic, and distinct from a provider id: this marker is Console Ops' reading of
                // the rows, not a record the provider handed over.
                $"revision-{revision}-{events[index].Id}",
                events[index].OccurredAt,
                "revision",
                null,
                revision,
                null);
        }
    }

    private static LogMarkerResponse ToMarkerResponse(LogDeploymentMarker marker) => new(
        $"deployment-{marker.Id:n}",
        marker.OccurredAt,
        "deployment",
        // Seven characters, matching how the rest of Console Ops shortens a commit.
        marker.CommitSha is { Length: >= 7 } sha ? sha[..7] : marker.CommitSha,
        null,
        marker.Id);

    /// <summary>
    /// The asked-for scope, or the first readable one so the screen has something on first open. An
    /// explicit request for a scope that is not readable is refused rather than quietly redirected.
    /// </summary>
    private static LogScope? Select(LogScope[] scopes, GetLogStreamQuery request)
    {
        if (request.ProjectId is null && request.EnvironmentId is null)
        {
            return scopes[0];
        }

        return scopes.FirstOrDefault(scope =>
            (request.ProjectId is null || scope.Project.Id == request.ProjectId)
            && (request.EnvironmentId is null || scope.Environment.Id == request.EnvironmentId));
    }

    private static LogStreamScopeResponse ToScopeResponse(LogScope scope) => new(
        scope.Project.Id,
        scope.Project.Name,
        new LogStreamEnvironmentResponse(
            scope.Environment.Id,
            scope.Environment.Name,
            scope.Environment.Kind),
        scope.Environment.LogSource!.Provider);

    private static LogEventResponse ToEventResponse(ApplicationLogEntry entry) => new(
        entry.Id,
        entry.OccurredAtUtc,
        entry.ReceivedAtUtc,
        ToCamelCase(entry.Level),
        entry.LevelIsDerived,
        entry.Category,
        // V1 reads console output, which is the application's own. Runtime and platform events come from
        // a different table and are not claimed here.
        "application",
        entry.Message,
        entry.StackTrace,
        ToCamelCase(entry.Stream),
        entry.Revision,
        entry.Replica);

    private static string ToCamelCase<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        string text = value.ToString();
        return char.ToLowerInvariant(text[0]) + text[1..];
    }

    private sealed record LogScope(ProjectResponse Project, ProjectEnvironmentResponse Environment);
}
