/**
 * Typed contract for the Environments screen.
 *
 * Design stage, and deliberately ahead of V1. `docs/Console_Ops_V1_API_Contract.md` models an
 * environment inside its project as id, name, kind, and optional application/health/version URLs.
 * This screen reports across projects and shows facts that belong to later product phases, marked
 * "later phase" below.
 *
 * Those fields were already rejected for V1 on the Projects screen, so this file is not a request to
 * add them now. It records the intended shape for when Azure runtime awareness (V2), deployment
 * history, and configuration-presence checking arrive. Until then the screen stays fixture-backed and
 * must not be wired to the V1 API; wiring it early would mean inventing values.
 *
 * Same rules as every other contract: plain JSON, camel case, ISO-8601 UTC instants, and `null` when
 * a fact could not be established. Presentation choices stay out of this file.
 */

import { EnvironmentKind, StatusCell, VersionSyncState } from './dashboard-overview';
import { ProjectLifecycle } from './project-registry';

export type { EnvironmentKind, ProjectLifecycle, StatusCell, VersionSyncState };

/** Runtime provider, kept machine-readable so the UI chooses the glyph. Later phase. */
export type RuntimeProvider = 'azure' | 'docker' | 'other';

export interface EnvironmentRuntimeRef {
  readonly provider: RuntimeProvider | null;
  /** Hosting target such as `Azure Container Apps`, or `null` when not configured. */
  readonly target: string | null;
}

/**
 * Presence-only view of expected configuration keys. Later phase: configuration drift is a later
 * phase. Values are never included, only how many expected keys were found.
 */
export interface ConfigurationPresence {
  readonly expectedKeys: number;
  readonly configuredKeys: number;
}

export interface EnvironmentVersionSync {
  readonly state: VersionSyncState;
  /** Short SHA the environment is running, or `null` when it could not be established. */
  readonly deployedCommitShortSha: string | null;
}

export interface EnvironmentListItem {
  /** Environment id, unique within its project. */
  readonly id: string;
  readonly projectId: string;
  readonly projectName: string;
  readonly name: string;
  readonly kind: EnvironmentKind;
  readonly lifecycle: ProjectLifecycle;
  readonly runtime: EnvironmentRuntimeRef | null;
  /** Absolute HTTP(S) base URL without credentials, or `null` when not configured. */
  readonly applicationUrl: string | null;
  /** Configured health path or absolute URL, or `null` when health is `notConfigured`. */
  readonly healthEndpoint: string | null;
  readonly versionEndpoint: string | null;
  readonly health: StatusCell;
  readonly healthCheckedAt: string | null;
  readonly versionSync: EnvironmentVersionSync;
  /** Runtime revision such as `spinner-api--000021`. Later phase: Azure awareness is V2. */
  readonly currentRevision: string | null;
  /** Later phase. `null` when no expected keys are declared or none were checked. */
  readonly configuration: ConfigurationPresence | null;
  /** Later phase: deployment history, so this stays `null` until that phase exists. */
  readonly lastDeployedAt: string | null;
}

export interface EnvironmentRegistry {
  /**
   * ISO-8601 UTC response-composition time. Relative times on this screen are measured against it,
   * so "18 min ago" always means 18 minutes before this observation, not before the browser clock.
   */
  readonly observedAt: string;
  readonly environments: readonly EnvironmentListItem[];
}
