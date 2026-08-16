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
/// </summary>
public sealed class GetLogStreamQueryHandler(
    IProjectReadStore projects,
    IApplicationLogReader logs,
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
        DateTimeOffset from = to.AddHours(-WindowHours);
        LogStreamWindowResponse window = new(from, to, WindowHours, false);

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
                request.Search),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return Result<LogStreamResponse>.Failure(
                LogStreamErrors.From(result.Failure ?? ApplicationLogReadFailure.Unavailable));
        }

        return Result<LogStreamResponse>.Success(new LogStreamResponse(
            now,
            scopeResponses,
            ToScopeResponse(selected),
            window with { Truncated = result.Truncated },
            result.Entries.Select(ToEventResponse).ToArray()));
    }

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
        // The stream is a tagged union on the client, so the tag travels with every item.
        "event",
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
