/**
 * Typed contract for `GET /api/dashboard/overview`.
 *
 * Keep this aligned with `docs/Console_Ops_V1_API_Contract.md`: plain JSON data, ISO-8601 UTC
 * timestamps, per-signal provenance, and explicit `null` when Console Ops could not establish a
 * fact. Presentation-only choices such as icon names, project colours, and navigation labels do not
 * belong here.
 */

/** Colour/meaning bucket for an operational value. Drives the indicator, never the wording. */
export type StatusLevel =
  'healthy' | 'running' | 'warning' | 'degraded' | 'down' | 'unknown' | 'notApplicable';

/** Deployment target kind. Production must remain visually distinct from every other kind. */
export type EnvironmentKind = 'production' | 'staging' | 'development' | 'local';

/**
 * One operational fact.
 * `level` of `null` means "render the text without a status indicator" (for example a count).
 */
export interface StatusCell {
  readonly level: StatusLevel | null;
  /** Operational wording: `Healthy`, `Running`, `In Sync`, `Passed`, `N/A`. */
  readonly label: string;
  /** Supporting evidence, or `null` when no safe evidence is available. */
  readonly detail: string | null;
}

/** A stage of the V1 source -> CI -> application-observation pipeline. */
export interface PipelineStage {
  readonly key: 'source' | 'ci' | 'application';
  readonly name: string;
  readonly role: string;
  /** `false` renders the connector to the next stage as unverified. */
  readonly verified: boolean;
}

export interface PipelineFlow {
  readonly stages: readonly PipelineStage[];
  /** Overall deterministic outcome shown after the last stage. */
  readonly outcome: StatusCell;
}

export interface ProjectEnvironmentRef {
  readonly id: string;
  readonly name: string;
  readonly kind: EnvironmentKind;
}

export interface SourceRevision {
  readonly provider: 'github';
  /** GitHub `owner/name`. */
  readonly repository: string;
  readonly branch: string;
  readonly commitSha: string | null;
  readonly commitShortSha: string | null;
  readonly committedAt: string | null;
  readonly observedAt: string | null;
}

export type WorkflowRunState =
  'queued' | 'inProgress' | 'passed' | 'failed' | 'cancelled' | 'unknown' | 'notConfigured';

export interface WorkflowState {
  readonly provider: 'githubActions';
  readonly workflowName: string | null;
  readonly state: WorkflowRunState;
  readonly commitSha: string | null;
  readonly startedAt: string | null;
  readonly completedAt: string | null;
  readonly observedAt: string | null;
}

export interface DeployedVersion {
  readonly application: string | null;
  readonly version: string | null;
  readonly commitSha: string;
  readonly commitShortSha: string;
  readonly environment: string | null;
  readonly builtAt: string | null;
  readonly observedAt: string;
}

export type VersionSyncState = 'inSync' | 'behind' | 'unknown' | 'notConfigured';

export interface VersionSynchronization {
  readonly state: VersionSyncState;
  readonly sourceCommitSha: string | null;
  readonly deployedCommitSha: string | null;
  /** Non-null only when GitHub proves ancestry and the count. */
  readonly commitsBehind: number | null;
  readonly observedAt: string | null;
}

export interface ResponseMeasurement {
  readonly milliseconds: number | null;
  /** Recent real samples, oldest first. Empty when no history has been recorded. */
  readonly samples: readonly number[];
  readonly observedAt: string | null;
}

export interface ProjectSurface {
  /** Project id. A project can appear once per configured environment. */
  readonly id: string;
  readonly name: string;
  readonly environment: ProjectEnvironmentRef;
  readonly source: SourceRevision;
  readonly workflow: WorkflowState;
  readonly health: StatusCell;
  readonly healthObservedAt: string | null;
  readonly deployedVersion: DeployedVersion | null;
  /**
   * Why there is no deployed version: `available`, `unknown`, or `notConfigured`. A missing version used to
   * render as "Not configured" whatever the cause, which sent an operator to fix configuration that was already
   * correct - an endpoint answering 401, or answering with HTML, is configured and unreadable.
   */
  readonly versionState: 'available' | 'unknown' | 'notConfigured';
  readonly versionSync: VersionSynchronization;
  readonly response: ResponseMeasurement;
}

export interface SystemStateColumn {
  readonly projectId: string;
  readonly projectName: string;
  readonly environmentId: string;
  readonly environmentName: string;
}

export type SystemComponentKey = 'api' | 'ci' | 'versionSync' | `dependency:${string}`;

export interface SystemStateRow {
  readonly key: SystemComponentKey;
  readonly label: string;
  /** One cell per column, in column order. `null` when the component does not apply. */
  readonly cells: readonly (StatusCell | null)[];
}

export interface SystemStateMatrix {
  readonly columns: readonly SystemStateColumn[];
  readonly rows: readonly SystemStateRow[];
}

export type ActivityKind =
  'healthFailed' | 'healthRecovered' | 'versionDrift' | 'versionSynchronized';

export interface ActivityEntry {
  readonly id: string;
  readonly kind: ActivityKind;
  readonly title: string;
  /** Scope line such as `Production - 8a17c2f`. `null` when no scope applies. */
  readonly context: string | null;
  /** ISO-8601 UTC. */
  readonly occurredAt: string;
}

/**
 * Availability observed by Console Ops over a window, or `null` when too few checks exist for a figure
 * to mean anything. This is availability as sampled by the collector, not a provider's uptime guarantee.
 */
export interface UptimeWindow {
  /** Window length in hours. The UI decides the wording. */
  readonly windowHours: number;
  /** ISO-8601 UTC start of the window. */
  readonly since: string;
  /** Share of measured checks that were acceptable, to one decimal. */
  readonly percentage: number;
  /** Measured checks behind the figure, so the screen can say what it rests on. */
  readonly checks: number;
  /**
   * Availability per hour, oldest first, for hours that contain checks. Hours with no check are absent
   * rather than reported as zero.
   */
  readonly samples: readonly number[];
}

export interface SystemSummary {
  readonly level: StatusLevel;
  readonly label: string;
  /** `null` until enough health checks have been recorded to support a figure. */
  readonly uptime: UptimeWindow | null;
}

export interface DashboardOverview {
  /** ISO-8601 UTC response-composition time; individual signals carry their own observation time. */
  readonly observedAt: string;
  readonly pipeline: PipelineFlow;
  readonly projects: readonly ProjectSurface[];
  readonly systemState: SystemStateMatrix;
  readonly activity: readonly ActivityEntry[];
  readonly summary: SystemSummary;
}
