import { EnvironmentKind } from './dashboard-overview';

export type { EnvironmentKind };

export interface ProjectRepositoryRegistration {
  readonly owner: string;
  readonly name: string;
  readonly defaultBranch: string;
  readonly workflowFile: string | null;
}

/**
 * Optional pointer to where this environment's logs can be read.
 *
 * Sent only when both parts are known: half a source cannot be queried, so the API refuses it. Never a
 * credential - Console Ops authenticates to the provider from its own configuration.
 */
export interface ProjectLogSourceInput {
  readonly workspaceId: string;
  readonly containerAppName: string;
}

export interface ProjectEnvironmentRegistration {
  readonly name: string;
  readonly kind: EnvironmentKind;
  /** Absolute HTTP(S) URL without embedded credentials, or `null`. */
  readonly applicationUrl: string | null;
  readonly healthUrl: string | null;
  readonly versionUrl: string | null;
  readonly logSource: ProjectLogSourceInput | null;
}

/** Exact request body for `POST /api/projects`. Credentials are never project configuration. */
export interface ProjectRegistrationRequest {
  readonly name: string;
  readonly description: string | null;
  readonly repository: ProjectRepositoryRegistration;
  readonly environments: readonly [ProjectEnvironmentRegistration];
}
