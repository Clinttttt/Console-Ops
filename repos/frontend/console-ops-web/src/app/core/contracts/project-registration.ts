/**
 * Typed contract for project registration (`POST /api/projects`).
 *
 * The core of this file mirrors the frozen V1 request in `docs/Console_Ops_V1_API_Contract.md`:
 * name, optional description, repository owner/name/default branch, and at least one environment
 * with optional application, health, and version URLs. Credentials are never part of a project.
 *
 * Fields marked "beyond frozen V1" are the same proposal recorded for the project list read model in
 * `project-registry.ts`. The Add Project screen collects them so the registry can display them; the
 * backend owner either accepts them into the contract or the screen drops them.
 */

import { EnvironmentKind } from './dashboard-overview';
import { ProjectKind } from './project-registry';

export type { EnvironmentKind, ProjectKind };

export interface ProjectRepositoryRegistration {
  /** GitHub account or organisation. */
  readonly owner: string;
  readonly name: string;
  readonly defaultBranch: string;
}

export interface ProjectEnvironmentRegistration {
  readonly name: string;
  readonly kind: EnvironmentKind;
  /** Absolute HTTP(S) base URL without embedded credentials, or `null`. */
  readonly applicationUrl: string | null;
  /** Absolute URL or a path relative to `applicationUrl`, or `null` when not configured. */
  readonly healthUrl: string | null;
  readonly versionUrl: string | null;
}

/** Runtime descriptor. Beyond frozen V1. */
export interface ProjectRuntimeRegistration {
  readonly target: string | null;
}

/**
 * Which observations Console Ops should collect once the project exists. Beyond frozen V1: V1 probes
 * are driven by whether an endpoint is configured, so these are intent flags for a later phase.
 */
export interface ProjectMonitoringRegistration {
  readonly healthMonitoring: boolean;
  readonly versionSync: boolean;
}

export interface ProjectRegistrationRequest {
  readonly name: string;
  readonly description: string | null;
  /** Beyond frozen V1. */
  readonly kind: ProjectKind | null;
  readonly repository: ProjectRepositoryRegistration;
  /** V1 requires at least one environment; this screen registers the primary one. */
  readonly environments: readonly [ProjectEnvironmentRegistration];
  readonly runtime: ProjectRuntimeRegistration | null;
  readonly monitoring: ProjectMonitoringRegistration;
}
