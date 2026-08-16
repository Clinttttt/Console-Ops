import { EnvironmentKind } from './dashboard-overview';

export type { EnvironmentKind };

/** Shared lifecycle vocabulary; normal V1 project queries still return active resources only. */
export type ProjectLifecycle = 'active' | 'archived';

/** GitHub repository configuration returned by the V1 project resource. */
export interface ProjectRepositoryRef {
  readonly owner: string;
  readonly name: string;
  readonly defaultBranch: string;
  readonly workflowFile: string | null;
}

/**
 * Where an environment's application logs are read from.
 *
 * Console Ops pulls logs as it pulls everything else, so this is a pointer to a provider rather than a
 * destination that pushes. Both parts are required together, and no credential appears here: Console Ops
 * authenticates to the provider from its own configuration.
 */
export interface ProjectLogSourceRef {
  readonly provider: 'azureContainerApps';
  readonly workspaceId: string;
  readonly containerAppName: string;
}

/** One configured application environment returned by the V1 project resource. */
export interface ProjectEnvironmentRef {
  readonly id: string;
  readonly name: string;
  readonly kind: EnvironmentKind;
  readonly applicationUrl: string | null;
  readonly healthUrl: string | null;
  readonly versionUrl: string | null;
  /** `null` when no log source is configured, which reads as not configured rather than empty. */
  readonly logSource: ProjectLogSourceRef | null;
}

/** Exact frontend representation of the active project resource returned by `GET /api/projects`. */
export interface ProjectListItem {
  readonly id: string;
  readonly name: string;
  readonly description: string | null;
  readonly repository: ProjectRepositoryRef;
  readonly environments: readonly ProjectEnvironmentRef[];
  readonly createdAtUtc: string;
  readonly updatedAtUtc: string | null;
  readonly configurationVersion: number;
}

export type ProjectRegistry = readonly ProjectListItem[];
