/**
 * Typed contract for `POST /api/projects/verification`, phase 3 of
 * `docs/Console_Ops_Add_Project_Import_Plan.md`.
 *
 * The API probes candidate endpoints before a project exists so setup can be checked rather than
 * guessed at. Probing happens server side, through the same safeguards as registered-project probes;
 * the browser never contacts a target application.
 *
 * Every field here is an observation with a time. An unreachable application is a normal result, not
 * an error, because the application may simply not be deployed yet.
 */

export interface EndpointVerificationRequest {
  /** Absolute health URL, or `null` when none is configured. */
  readonly healthUrl: string | null;
  readonly versionUrl: string | null;
}

export type VerifiedHealthState =
  'healthy' | 'degraded' | 'unhealthy' | 'unreachable' | 'unknown' | 'notConfigured';

export type VerifiedVersionState = 'available' | 'unknown' | 'notConfigured';

export interface VerifiedDependency {
  readonly name: string;
  readonly state: VerifiedHealthState;
}

export interface VerifiedHealth {
  readonly state: VerifiedHealthState;
  /** Round trip in milliseconds, or `null` when no attempt completed. */
  readonly responseMilliseconds: number | null;
  /** Populated only from a supported health payload; Console Ops never guesses a dependency. */
  readonly dependencies: readonly VerifiedDependency[];
}

export interface VerifiedVersion {
  readonly state: VerifiedVersionState;
  readonly application: string | null;
  readonly version: string | null;
  readonly commitSha: string | null;
  readonly commitShortSha: string | null;
  readonly builtAt: string | null;
}

export interface EndpointVerification {
  readonly health: VerifiedHealth;
  readonly version: VerifiedVersion;
  /** ISO-8601 UTC time the probe completed. */
  readonly observedAt: string;
}
