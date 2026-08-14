/**
 * Typed contract for the Projects screen (proposed `GET /api/projects` list read model).
 *
 * Design stage. `docs/Console_Ops_V1_API_Contract.md` freezes the project resource as id, name,
 * description, configurationVersion, timestamps, repository, and environments. The Projects screen
 * additionally needs the fields marked "beyond frozen V1" below, so this file is the proposal the
 * backend list slice must either satisfy or explicitly reject. Nothing here is implemented yet.
 *
 * Same rules as every other contract: plain JSON, camel case, ISO-8601 UTC instants, and `null`
 * when a fact could not be established. Presentation choices (glyphs, tones, labels for enums) stay
 * out of this file.
 *
 * `StatusCell`, `EnvironmentKind`, and `ProjectEnvironmentRef` are reused from the overview contract.
 * When the real list slice lands, move them into a shared `operational.ts` rather than duplicating.
 */

import { EnvironmentKind, ProjectEnvironmentRef, StatusCell } from './dashboard-overview';

export type { EnvironmentKind, ProjectEnvironmentRef, StatusCell };

/** Application shape of a registered project. Beyond frozen V1. */
export type ProjectKind = 'api' | 'webApp' | 'worker';

/** Registration state. `archived` rows are excluded from normal queries per the V1 contract. */
export type ProjectLifecycle = 'active' | 'archived';

export interface ProjectRepositoryRef {
  readonly provider: 'github';
  /** GitHub `owner/name`. */
  readonly repository: string;
  readonly defaultBranch: string;
  /** Absolute repository URL, or `null` when it could not be composed safely. */
  readonly url: string | null;
}

/** Runtime descriptor from project configuration, not a live observation. Beyond frozen V1. */
export interface ProjectRuntimeRef {
  /** Framework such as `.NET 8`, or `null` when not configured. */
  readonly framework: string | null;
  /** Hosting target such as `Azure App Service` or `Docker Desktop`, or `null`. */
  readonly target: string | null;
}

/**
 * Most recent deployment known for the project's primary environment. Beyond frozen V1: deployment
 * history is a later phase, so this stays `null` until that phase exists.
 */
export interface LastDeployment {
  readonly deployedAt: string;
  /** Trigger source such as `ci-bot`, or `null` when the trigger is unknown. */
  readonly triggeredBy: string | null;
}

export interface ProjectListItem {
  readonly id: string;
  readonly name: string;
  /** Short descriptor such as `ASP.NET Core Web API`, or `null` when none was given. */
  readonly description: string | null;
  readonly kind: ProjectKind | null;
  readonly lifecycle: ProjectLifecycle;
  /**
   * Environment this row reports on. A project may configure several; the list reports its primary
   * environment and the project detail screen shows the rest.
   */
  readonly environment: ProjectEnvironmentRef;
  readonly repository: ProjectRepositoryRef;
  readonly runtime: ProjectRuntimeRef | null;
  readonly health: StatusCell;
  readonly healthObservedAt: string | null;
  readonly lastDeployment: LastDeployment | null;
  /** ISO-8601 UTC registration time, used for the recently-added list. */
  readonly registeredAt: string;
}

export interface ProjectRegistry {
  /** ISO-8601 UTC response-composition time. */
  readonly observedAt: string;
  readonly projects: readonly ProjectListItem[];
}
