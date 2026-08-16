/**
 * Typed contract for `PUT /api/projects/{projectId}`.
 *
 * The request replaces the editable project configuration, so it carries the full repository and the
 * complete environment list. `configurationVersion` is the version returned by the last read: sending a
 * stale one is rejected with a conflict rather than silently overwriting newer state.
 *
 * An existing environment sends its `id`. A new environment omits it.
 */

import { EnvironmentKind } from './dashboard-overview';
import { ProjectLogSourceInput } from './project-registration';

export type { ProjectLogSourceInput };

export interface ProjectRepositoryUpdate {
  readonly owner: string;
  readonly name: string;
  readonly defaultBranch: string;
  readonly workflowFile: string | null;
}

export interface ProjectEnvironmentUpdate {
  /** Omitted for a new environment. */
  readonly id?: string;
  readonly name: string;
  readonly kind: EnvironmentKind;
  /** Absolute HTTP(S) URL without credentials, or `null`. */
  readonly applicationUrl: string | null;
  readonly healthUrl: string | null;
  readonly versionUrl: string | null;
  /** `null` clears the source; both parts are required together when present. */
  readonly logSource: ProjectLogSourceInput | null;
}

export interface ProjectUpdateRequest {
  readonly configurationVersion: number;
  readonly name: string;
  readonly description: string | null;
  readonly repository: ProjectRepositoryUpdate;
  readonly environments: readonly ProjectEnvironmentUpdate[];
}
