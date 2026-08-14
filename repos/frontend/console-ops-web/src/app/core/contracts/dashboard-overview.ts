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
  readonly application: string;
  readonly version: string;
  readonly commitSha: string;
  readonly commitShortSha: string;
  readonly environment: string;
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
  readonly id: string;
  readonly name: string;
  /** Technology descriptor, for example `ASP.NET Core Web API`. */
  readonly kind: string;
  readonly environment: ProjectEnvironmentRef;
  readonly source: SourceRevision;
  readonly workflow: WorkflowState;
  readonly health: StatusCell;
  readonly healthObservedAt: string | null;
  readonly deployedVersion: DeployedVersion | null;
  readonly versionSync: VersionSynchronization;
  readonly response: ResponseMeasurement;
}

export interface SystemStateColumn {
  readonly projectId: string;
  readonly projectName: string;
  readonly environmentName: string;
}

export type SystemComponentKey = 'api' | 'database' | 'cache' | 'ci' | 'versionSync';

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

export interface UptimeWindow {
  readonly label: string;
  /** Recent availability samples, oldest first. Empty when no history exists. */
  readonly samples: readonly number[];
}

export interface SystemSummary {
  readonly level: StatusLevel;
  readonly label: string;
  /** `null` until health history supports the selected window and calculation. */
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
