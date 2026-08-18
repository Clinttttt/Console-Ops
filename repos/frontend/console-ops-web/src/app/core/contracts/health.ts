/**
 * Typed contract for the Health screen.
 *
 * Health answers one question: what is functioning right now, and what stopped. Every value here is either a
 * recorded check or absent - `unknown` means no check exists, never "probably fine".
 *
 * Deliberately not here: source state, release history, and log lines. Health links to Logs for the moment a
 * failure began, but it does not restate what those screens own.
 */

import { EnvironmentKind, StatusLevel } from './dashboard-overview';

export type { EnvironmentKind, StatusLevel };

/**
 * What a check reported.
 *
 * `running` is separate from `healthy` on purpose: a local process that answers is running, but a project with
 * no health endpoint has never claimed to be healthy. `notConfigured` says the operator never asked for the
 * check, which is not a failure.
 */
export type HealthCheckState =
  'healthy' | 'degraded' | 'unhealthy' | 'unreachable' | 'running' | 'notConfigured' | 'unknown';

/** What a dependency check covers, so the row can group them the way an operator reads them. */
export type HealthCheckKind = 'application' | 'database' | 'cache' | 'external' | 'unknown';

export interface HealthCheck {
  readonly name: string;
  readonly kind: HealthCheckKind;
  readonly state: HealthCheckState;
  /** Measured round trip, or `null` when the check reported none. */
  readonly responseMilliseconds: number | null;
}

/** What the last 24 hours of recorded checks add up to. Absent below the minimum sample Console Ops requires. */
export interface HealthWindow {
  readonly availabilityPercentage: number | null;
  readonly checks: number | null;
  readonly failedChecks: number | null;
  /** Longest unbroken run of failing checks, in seconds, or `null` when there was none. */
  readonly longestOutageSeconds: number | null;
}

export interface EnvironmentHealth {
  readonly id: string;
  readonly projectId: string;
  readonly projectName: string;
  readonly environmentName: string;
  readonly environmentKind: EnvironmentKind;
  readonly state: HealthCheckState;
  /** When the last check ran. `null` when none has, which is why a row can exist with no verdict. */
  readonly checkedAt: string | null;
  readonly responseMilliseconds: number | null;
  readonly checks: readonly HealthCheck[];
  /** Since when the current healthy run has held, or `null` when it is not healthy or is not known. */
  readonly healthySince: string | null;
  /** Since when the current failing run has held, or `null` when it is not failing. */
  readonly failingSince: string | null;
  readonly consecutiveFailures: number;
  readonly lastHealthyAt: string | null;
  readonly window: HealthWindow;
}

/** A recorded transition, which is the only honest way to say something changed. */
export interface HealthStateChange {
  readonly at: string;
  readonly projectName: string;
  readonly environmentName: string;
  /** Operational wording for what changed, such as `Redis recovered`. */
  readonly description: string;
  readonly level: StatusLevel;
}

export interface HealthSummary {
  readonly healthy: number;
  readonly degraded: number;
  readonly down: number;
  /** The most recent check across every environment, or `null` when nothing has been checked. */
  readonly lastCheckedAt: string | null;
}

export interface HealthSnapshot {
  /** ISO-8601 UTC composition time. Relative times are measured against it. */
  readonly observedAt: string;
  readonly summary: HealthSummary;
  readonly environments: readonly EnvironmentHealth[];
  readonly stateChanges: readonly HealthStateChange[];
  /**
   * `true` while the screen is backed by sample data. It must say so plainly: a health screen that invents a
   * verdict is the most dangerous screen in the product.
   */
  readonly isSampleData: boolean;
}
