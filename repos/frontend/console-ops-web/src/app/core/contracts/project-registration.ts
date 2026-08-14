import { EnvironmentKind } from './dashboard-overview';

export type { EnvironmentKind };

export interface ProjectRepositoryRegistration {
  readonly owner: string;
  readonly name: string;
  readonly defaultBranch: string;
  readonly workflowFile: string | null;
}

export interface ProjectEnvironmentRegistration {
  readonly name: string;
  readonly kind: EnvironmentKind;
  /** Absolute HTTP(S) URL without embedded credentials, or `null`. */
  readonly applicationUrl: string | null;
  readonly healthUrl: string | null;
  readonly versionUrl: string | null;
}

/** Exact request body for `POST /api/projects`. Credentials are never project configuration. */
export interface ProjectRegistrationRequest {
  readonly name: string;
  readonly description: string | null;
  readonly repository: ProjectRepositoryRegistration;
  readonly environments: readonly [ProjectEnvironmentRegistration];
}
