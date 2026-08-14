/**
 * Typed contract for the Deployments screen.
 *
 * Design stage, and entirely ahead of V1. Deployment history is a later product phase and deployment
 * triggering is later still, so nothing here has a V1 source. This file records the intended shape for
 * when those phases arrive; it is not a request to add them now. Until then the screen stays
 * fixture-backed and must not be wired to the API, because wiring it early would mean inventing values.
 *
 * Same rules as every other contract: plain JSON, camel case, ISO-8601 UTC instants, and `null` when a
 * fact could not be established. Presentation choices and derived summaries stay out of this file: the
 * UI computes the verification verdict from the explicit facts below.
 */

import { EnvironmentKind, ProjectEnvironmentRef, VersionSyncState } from './dashboard-overview';

export type { EnvironmentKind, ProjectEnvironmentRef, VersionSyncState };

/** Outcome reported by the deployment provider. */
export type DeploymentResult = 'succeeded' | 'failed' | 'cancelled' | 'inProgress' | 'unknown';

/** Result of the post-deployment health probe. */
export type HealthCheckOutcome = 'passed' | 'failed' | 'notConfigured' | 'unknown';

export interface DeploymentListItem {
  readonly id: string;
  readonly projectId: string;
  readonly projectName: string;
  readonly environment: ProjectEnvironmentRef;
  readonly commitSha: string;
  readonly commitShortSha: string;
  readonly branch: string;
  /** Runtime revision such as `spinner-api--000021`, or `null` when the runtime has no revisions. */
  readonly revision: string | null;
  /** `true` when this deployment is the one currently active in its environment. */
  readonly isCurrent: boolean;
  readonly result: DeploymentResult;
  readonly healthCheck: HealthCheckOutcome;
  /** Version sync observed after this deployment. */
  readonly versionCheck: VersionSyncState;
  readonly runtimeTarget: string | null;
  /** GitHub Actions workflow file such as `deploy-spinner-api.yml`, or `null` when not configured. */
  readonly workflowFile: string | null;
  /** Absolute workflow run URL, or `null` when it could not be composed safely. */
  readonly workflowUrl: string | null;
  /** Trigger source such as `ci-bot`. Never a credential or a personal identifier. */
  readonly triggeredBy: string | null;
  readonly deployedAt: string;
  /** Wall-clock duration in seconds, or `null` when the provider did not report it. */
  readonly durationSeconds: number | null;
}

export interface DeploymentRegistry {
  /**
   * ISO-8601 UTC response-composition time. Relative times and the "today" grouping are measured
   * against it, so the screen reads the same whenever it is rendered.
   */
  readonly observedAt: string;
  readonly deployments: readonly DeploymentListItem[];
}
