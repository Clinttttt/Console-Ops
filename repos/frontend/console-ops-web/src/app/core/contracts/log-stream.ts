/**
 * Typed contract for the Logs screen.
 *
 * Design stage, and ahead of V1: Console Ops has no log ingestion yet, so nothing on this screen has a
 * real source. This file records the intended shape for when ingestion arrives; it is not a request to
 * build ingestion now. Until then the screen stays fixture-backed and must not be wired to the API,
 * because wiring it early would mean inventing events.
 *
 * The Logs screen answers one question the other screens cannot: what did the application and its
 * runtime actually say around the time something happened. It therefore carries no project
 * configuration, no deployment history, and no health summaries - those have homes. Deployment and
 * revision markers appear only as lightweight context in the stream.
 *
 * Structured logging is the point. An event carries its message template and its properties separately,
 * so `Payment failed for order {OrderId}` keeps `OrderId = 2048` as a value rather than a formatted
 * string. Presentation stays out of this file: the UI composes the trailing context line, the wording of
 * each level, and the marker labels.
 *
 * CI/CD execution logs are deliberately absent. A workflow run's output belongs to that run on the
 * Deployments screen; this stream is application, runtime, and platform events.
 */

import { EnvironmentKind, ProjectEnvironmentRef } from './dashboard-overview';

export type { EnvironmentKind, ProjectEnvironmentRef };

/** Severity as the emitter reported it. */
export type LogLevel = 'info' | 'warning' | 'error';

/**
 * Where an event came from.
 *
 * - `application`: the ASP.NET application's own logger.
 * - `runtime`: container or process lifecycle.
 * - `platform`: the hosting platform, once Console Ops is aware of one.
 */
export type LogSourceKind = 'application' | 'runtime' | 'platform';

/** One structured property carried by an event. Values arrive already stringified. */
export interface LogProperty {
  readonly name: string;
  readonly value: string;
}

/** Correlation identifiers, when the emitter provided them. */
export interface LogCorrelation {
  readonly traceId: string | null;
  readonly requestId: string | null;
}

/** Exception detail. The stack trace is separate so the stream never has to show it. */
export interface LogException {
  readonly type: string;
  readonly message: string;
  readonly stackTrace: string | null;
}

/**
 * A request or operation outcome, when the event describes one. The UI composes the trailing context
 * from these rather than the contract sending a rendered `200 · 91 ms`.
 */
export interface LogOutcome {
  readonly statusCode: number | null;
  readonly durationMs: number | null;
}

export interface LogEvent {
  readonly kind: 'event';
  readonly id: string;
  /** ISO-8601 UTC with milliseconds. */
  readonly occurredAt: string;
  readonly level: LogLevel;
  /** Emitter category such as `HTTP`, `Payments`, `Database`, `Runtime`. */
  readonly source: string;
  readonly sourceKind: LogSourceKind;
  /** Rendered message. Keep it one line; detail belongs in properties. */
  readonly message: string;
  /** Message template when the emitter used structured logging, otherwise `null`. */
  readonly messageTemplate: string | null;
  readonly outcome: LogOutcome | null;
  readonly correlation: LogCorrelation;
  readonly exception: LogException | null;
  readonly properties: readonly LogProperty[];
  /** Host or replica that emitted the event, or `null` when unknown. */
  readonly host: string | null;
}

/**
 * Something that happened to the deployment or the runtime, shown inline as context rather than as a
 * card. It explains a change in the stream: errors that begin right after a release are the point.
 */
export type LogMarkerKind = 'deployment' | 'revision' | 'containerRestart';

export interface LogMarker {
  readonly kind: 'marker';
  readonly id: string;
  readonly occurredAt: string;
  readonly markerKind: LogMarkerKind;
  /** Commit the release carried, for a `deployment` marker. */
  readonly commitShortSha: string | null;
  /** Runtime revision, once a platform reports one. */
  readonly revision: string | null;
  /** Deployment record this marker refers to, so the UI can link to it. */
  readonly deploymentId: string | null;
}

/** The stream is one ordered sequence, so markers keep their position among the events. */
export type LogStreamItem = LogEvent | LogMarker;

export interface LogStreamScope {
  readonly projectId: string;
  readonly projectName: string;
  readonly environment: ProjectEnvironmentRef;
}

export interface LogStream {
  /**
   * ISO-8601 UTC response-composition time. Relative times and the day grouping are measured against
   * it, so the screen reads the same whenever it is rendered.
   */
  readonly observedAt: string;
  /** Scopes the operator can inspect. A stream always belongs to one project and environment. */
  readonly scopes: readonly LogStreamScope[];
  /** Newest last, so the stream reads like a terminal and live tail appends at the bottom. */
  readonly items: readonly LogStreamItem[];
}
