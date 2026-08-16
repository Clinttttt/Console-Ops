/**
 * Typed contract for `GET /api/deployments`.
 *
 * A deployment record is one run of a project's configured GitHub Actions workflow. That is what
 * Console Ops can establish: GitHub proves a commit was built and how the run ended, not where the
 * artifact landed.
 *
 * The link to an environment is therefore evidence rather than attribution. An environment appears
 * under a release because its own `/version` endpoint reported that release's commit, and the release
 * is `isCurrent` there while that is still the latest thing the environment reported. A release with no
 * environments was built but never seen running anywhere; that is a fact to show, not a failure.
 *
 * Same rules as every other contract: plain JSON, camel case, ISO-8601 UTC instants, and `null` when a
 * fact could not be established. Presentation choices and derived verdicts stay out of this file: the
 * UI maps these machine values to wording and computes the verification verdict from them.
 *
 * Not present, because V1 has no source for it: the runtime revision (for example
 * `spinner-api--000021`) and the runtime target. Both need Azure awareness, which is a later phase.
 */

import { EnvironmentKind, ProjectEnvironmentRef, VersionSyncState } from './dashboard-overview';

export type { EnvironmentKind, ProjectEnvironmentRef, VersionSyncState };

/**
 * How the provider's run ended. Deliberately the same vocabulary as the workflow state on Overview and
 * Projects, so one run never reads as `Passed` on one screen and `Succeeded` on another.
 */
export type DeploymentResult =
  'queued' | 'inProgress' | 'passed' | 'failed' | 'cancelled' | 'unknown';

/** Health an environment reported, as recorded. `unknown` means no check exists, not "fine". */
export type HealthState =
  'healthy' | 'degraded' | 'unhealthy' | 'unreachable' | 'notConfigured' | 'unknown';

/** An environment that was observed running this release's commit. */
export interface DeploymentEnvironmentObservation {
  readonly environment: ProjectEnvironmentRef;
  /** `true` while this environment's latest version observation still reports this commit. */
  readonly isCurrent: boolean;
  /** First time this environment was seen reporting this commit. */
  readonly firstObservedAt: string;
  /** Health on the last check before the release was seen here. */
  readonly healthBefore: HealthState;
  readonly healthBeforeObservedAt: string | null;
  /** Health on the first check at or after the release was seen here. */
  readonly healthAfter: HealthState;
  readonly healthAfterObservedAt: string | null;
  /** Version sync observed for this environment once the release was seen. */
  readonly versionCheck: VersionSyncState;
  readonly versionCheckObservedAt: string | null;
}

export interface DeploymentListItem {
  readonly id: string;
  readonly projectId: string;
  readonly projectName: string;
  readonly provider: 'githubActions';
  /** GitHub `owner/name`. */
  readonly repository: string;
  readonly branch: string;
  readonly commitSha: string;
  readonly commitShortSha: string;
  readonly result: DeploymentResult;
  /** Workflow file such as `deploy-spinner-api.yml`, or `null` when none is configured. */
  readonly workflowFile: string | null;
  readonly workflowName: string | null;
  /** Absolute GitHub run URL, or `null` when it could not be trusted. */
  readonly workflowUrl: string | null;
  readonly runNumber: number | null;
  /** Account that started the run. Never a credential. */
  readonly triggeredBy: string | null;
  readonly startedAt: string | null;
  readonly completedAt: string | null;
  /** Instant the timeline orders and groups by: completion when known, otherwise start. */
  readonly deployedAt: string;
  /** Wall-clock duration in seconds, or `null` when the provider did not report both ends. */
  readonly durationSeconds: number | null;
  /** When Console Ops first recorded this run. */
  readonly recordedAt: string;
  readonly environments: readonly DeploymentEnvironmentObservation[];
}

export interface DeploymentRegistry {
  /**
   * ISO-8601 UTC response-composition time. Relative times and the "today" grouping are measured
   * against it, so the screen reads the same whenever it is rendered.
   */
  readonly observedAt: string;
  readonly deployments: readonly DeploymentListItem[];
}
