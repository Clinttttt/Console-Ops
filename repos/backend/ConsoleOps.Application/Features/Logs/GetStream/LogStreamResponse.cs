using System.Text.Json.Serialization;

namespace ConsoleOps.Application.Features.Logs.GetStream;

/// <summary>
/// Transport for the Logs screen. Enumerations cross the wire as camel-case strings, and every fact the
/// provider did not give is <c>null</c> or <c>unknown</c> rather than filled in.
/// </summary>
/// <param name="ObservedAt">
/// Response-composition time. Relative times and the day grouping are measured against it.
/// </param>
/// <param name="Scopes">
/// Project/environment pairs the operator can read, so the toolbar can offer them. An environment without
/// a log source is not a scope: there would be nothing to read.
/// </param>
/// <param name="Window">The range actually queried. The screen states it rather than implying completeness.</param>
/// <param name="Items">
/// Events and markers in one ordered list, newest first, so a marker keeps its place in time.
/// </param>
/// <param name="Noise">What was left out of the stream to make it readable, and whether it was.</param>
public sealed record LogStreamResponse(
    DateTimeOffset ObservedAt,
    IReadOnlyList<LogStreamScopeResponse> Scopes,
    LogStreamScopeResponse? Scope,
    LogStreamWindowResponse Window,
    IReadOnlyList<LogStreamItemResponse> Items,
    LogStreamNoiseResponse Noise);

/// <summary>
/// Framework chatter that was excluded, stated rather than silently dropped.
/// </summary>
/// <param name="Excluded"><c>true</c> when the read left framework categories out.</param>
/// <param name="HiddenCount">
/// How many lines were removed. This is why a quiet window is quiet, and without it an operator cannot tell
/// "nothing happened" from "everything that happened was noise".
/// </param>
public sealed record LogStreamNoiseResponse(bool Excluded, int HiddenCount);

public sealed record LogStreamScopeResponse(
    Guid ProjectId,
    string ProjectName,
    LogStreamEnvironmentResponse Environment,
    string Provider);

public sealed record LogStreamEnvironmentResponse(Guid Id, string Name, string Kind);

/// <param name="Truncated">
/// <c>true</c> when the row cap cut the result, so the screen can say the window holds more.
/// </param>
public sealed record LogStreamWindowResponse(
    DateTimeOffset From,
    DateTimeOffset To,
    int Hours,
    bool Truncated);

/// <summary>
/// One item in the stream: an event, or a marker that explains a change in what follows.
/// <para>
/// Serialized polymorphically with <c>kind</c> as the discriminator, because the screen selects on it. The
/// tag is emitted by the serializer rather than by hand, so a new item type cannot forget to carry one.
/// </para>
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(LogEventResponse), "event")]
[JsonDerivedType(typeof(LogMarkerResponse), "marker")]
public abstract record LogStreamItemResponse(string Id, DateTimeOffset OccurredAt);

/// <summary>
/// Something that happened to the deployment or the runtime, shown inline as context.
/// <para>
/// Derived from what Console Ops already recorded and from the revisions the log rows themselves report.
/// Nothing is written to a log store, and no marker claims a destination the provider did not state.
/// </para>
/// </summary>
/// <param name="MarkerKind"><c>deployment</c>, <c>revision</c>, or <c>containerRestart</c>.</param>
/// <param name="DeploymentId">The recorded release this marker refers to, so the UI can link to it.</param>
public sealed record LogMarkerResponse(
    string Id,
    DateTimeOffset OccurredAt,
    string MarkerKind,
    string? CommitShortSha,
    string? Revision,
    Guid? DeploymentId) : LogStreamItemResponse(Id, OccurredAt);

/// <param name="LevelIsDerived">
/// <c>true</c> when Console Ops parsed the level out of the line rather than the emitter declaring it.
/// Console output carries no severity column, so this is how the screen avoids overstating what it knows.
/// </param>
/// <param name="Source">Emitter category, or <c>null</c> when the line carried none.</param>
/// <param name="Stream"><c>stdout</c>, <c>stderr</c>, or <c>unknown</c>.</param>
/// <param name="ReceivedAt">
/// When the provider ingested the line. Kept alongside <c>occurredAt</c> so clock skew stays visible.
/// </param>
public sealed record LogEventResponse(
    string Id,
    DateTimeOffset OccurredAt,
    DateTimeOffset? ReceivedAt,
    string Level,
    bool LevelIsDerived,
    string? Source,
    string SourceKind,
    string Message,
    string? StackTrace,
    string Stream,
    string? Revision,
    string? Host) : LogStreamItemResponse(Id, OccurredAt);
