using ConsoleOps.Application.Abstractions.Messaging;
using MediatR;

namespace ConsoleOps.Application.Features.Logs.GetStream;

/// <summary>
/// Reads one scope's application logs.
/// </summary>
/// <param name="ProjectId">Required: a stream always belongs to one project.</param>
/// <param name="EnvironmentId">
/// Environment to read. When omitted the first configured environment with a log source is used, so the
/// screen has something to show on first open.
/// </param>
/// <param name="Before">Read events older than this instant, which is how the stream pages backwards.</param>
/// <param name="Since">
/// Read only what has happened since this instant, which is how the stream follows a scope while `Live` is
/// on. Bounded by the same maximum window as any other read, so a stale cursor cannot widen the query.
/// </param>
/// <param name="Limit">Clamped by the handler and again by the adapter.</param>
/// <param name="IncludeNoise">
/// Keep framework chatter in the stream. Off by default, because an idle service logs almost nothing else
/// and the lines an operator came for are buried. The response always states how many lines were left out.
/// </param>
public sealed record GetLogStreamQuery(
    Guid? ProjectId = null,
    Guid? EnvironmentId = null,
    string? Search = null,
    DateTimeOffset? Before = null,
    int? Limit = null,
    bool IncludeNoise = false,
    DateTimeOffset? Since = null)
    : IRequest<Result<LogStreamResponse>>;
