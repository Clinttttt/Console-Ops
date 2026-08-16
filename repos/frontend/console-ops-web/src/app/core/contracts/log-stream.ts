/**
 * Typed contract for `GET /api/logs`.
 *
 * Console Ops pulls logs from the environment's provider during the request; the browser never talks to a
 * provider. What arrives is container console output, so the shape is honest about what that can carry:
 * severity and category are parsed from the line and marked as derived, and structure a console line cannot
 * hold - trace ids, properties, exception objects - is absent until a richer source exists.
 *
 * Same rules as every other contract: plain JSON, camel case, ISO-8601 UTC instants, and `null` when a fact
 * could not be established. Presentation stays out of this file: the UI composes the trailing context line,
 * the wording of each level, and the marker labels.
 *
 * CI/CD execution logs are deliberately absent. A workflow run's output belongs to that run on the
 * Deployments screen; this stream is application, and later runtime and platform, events.
 */

import { EnvironmentKind } from './dashboard-overview';

export type { EnvironmentKind };

/**
 * Severity as parsed from the line.
 *
 * `unknown` is the honest answer for a plain line of output: console logs carry no severity column, so a
 * line without a recognizable prefix is not evidence of information.
 */
export type LogLevel =
  'trace' | 'debug' | 'information' | 'warning' | 'error' | 'critical' | 'unknown';

/**
 * Where an event came from.
 *
 * - `application`: the application's own console output.
 * - `runtime`: container or process lifecycle.
 * - `platform`: the hosting platform.
 *
 * V1 reads application output only; the other two need the platform's system log table.
 */
export type LogSourceKind = 'application' | 'runtime' | 'platform';

/** Which stream the line was written to. `unknown` when the provider did not say. */
export type LogStreamName = 'stdout' | 'stderr' | 'unknown';

export interface LogEvent {
  readonly kind: 'event';
  readonly id: string;
  /** The emitter's own timestamp. ISO-8601 UTC with milliseconds. */
  readonly occurredAt: string;
  /** When the provider ingested the line, or `null`. Kept so ingestion delay stays visible. */
  readonly receivedAt: string | null;
  readonly level: LogLevel;
  /** `true` when Console Ops parsed the level rather than the emitter declaring it. */
  readonly levelIsDerived: boolean;
  /** Emitter category such as `Spinner.Payments`, or `null` when the line carried none. */
  readonly source: string | null;
  readonly sourceKind: LogSourceKind;
  /** One line. Detail belongs in the stack trace or behind selection. */
  readonly message: string;
  /** Continuation lines that belonged to this event, or `null`. */
  readonly stackTrace: string | null;
  readonly stream: LogStreamName;
  /** Runtime revision that emitted the line, or `null`. */
  readonly revision: string | null;
  /** Host or replica that emitted the line, or `null`. */
  readonly host: string | null;
}

/**
 * Something that happened to the deployment or the runtime, shown inline as context rather than as a card.
 * Derived from what Console Ops already recorded, never from the log store.
 */
export type LogMarkerKind = 'deployment' | 'revision' | 'containerRestart';

export interface LogMarker {
  readonly kind: 'marker';
  readonly id: string;
  readonly occurredAt: string;
  readonly markerKind: LogMarkerKind;
  readonly commitShortSha: string | null;
  readonly revision: string | null;
  readonly deploymentId: string | null;
}

/** The stream is one ordered sequence, so markers keep their position among the events. */
export type LogStreamItem = LogEvent | LogMarker;

export interface LogStreamEnvironmentRef {
  readonly id: string;
  readonly name: string;
  readonly kind: EnvironmentKind;
}

/** A project environment with a log source Console Ops can read. */
export interface LogStreamScope {
  readonly projectId: string;
  readonly projectName: string;
  readonly environment: LogStreamEnvironmentRef;
  readonly provider: string;
}

/**
 * The range that was actually queried. Logs are retained by the provider, not by Console Ops, so the
 * screen states its window rather than implying complete history.
 */
export interface LogStreamWindow {
  readonly from: string;
  readonly to: string;
  readonly hours: number;
  /** `true` when the row cap cut the result, so the window holds more than is shown. */
  readonly truncated: boolean;
}

export interface LogStream {
  /**
   * ISO-8601 UTC response-composition time. Relative times and the day grouping are measured against it.
   */
  readonly observedAt: string;
  /** Scopes the operator can inspect. An environment without a log source is not among them. */
  readonly scopes: readonly LogStreamScope[];
  /** The scope this stream belongs to, or `null` when none could be read. */
  readonly scope: LogStreamScope | null;
  readonly window: LogStreamWindow;
  /** Newest first, as the provider returns them. */
  readonly items: readonly LogStreamItem[];
}
