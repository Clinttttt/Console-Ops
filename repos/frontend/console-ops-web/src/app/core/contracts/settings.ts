/**
 * Typed contract for the Settings screen.
 *
 * Settings describes **Console Ops itself** - how it connects to providers and how often it observes - not
 * the projects it manages. Repository names, environment URLs, health and deployment history belong to their
 * own screens; putting them here would make two places to look for one fact.
 *
 * The distinction this screen exists to make honestly:
 *
 * - `configured` means the required configuration is present.
 * - `verified` means Console Ops actually contacted the provider and it answered.
 *
 * They are not the same thing, and only the second is evidence. A credential value never appears here in any
 * form: the screen answers "can Console Ops reach this provider?", never "what is the secret?".
 */

/** Whether the configuration a provider needs is present. */
export type IntegrationConfigurationState = 'configured' | 'notConfigured' | 'partial';

/**
 * Whether Console Ops has actually authenticated.
 *
 * `notProbed` is the honest default: a screen that loads has not contacted anything, and reporting that as
 * unverified would be as wrong as reporting it as working.
 */
export type IntegrationVerificationState = 'verified' | 'failed' | 'notProbed';

export interface Integration {
  readonly id: 'github' | 'azure';
  readonly name: string;
  /** One line saying what Console Ops uses it for. */
  readonly purpose: string;
  readonly configuration: IntegrationConfigurationState;
  readonly verification: IntegrationVerificationState;
  /**
   * How the credential is obtained, by mechanism name only - `DefaultAzureCredential`, `Personal access
   * token`. Never the credential.
   */
  readonly authentication: string | null;
  /** When the last probe ran, or `null` when none has. */
  readonly verifiedAt: string | null;
  /** Why the last probe failed, in the operator's words. `null` unless verification is `failed`. */
  readonly failure: string | null;
}

/**
 * How often Console Ops refreshes what it observes, and how the last sweep went.
 *
 * The interval comes from application settings and cannot be changed at runtime, so the screen says so
 * rather than offering a control that would not persist.
 */
export interface CollectionSettings {
  readonly intervalSeconds: number;
  readonly isIntervalEditable: boolean;
  /** When the last sweep finished, or `null` when none has run since start-up. */
  readonly lastSweepAt: string | null;
  readonly lastSweepSucceeded: boolean | null;
  readonly lastSweepMilliseconds: number | null;
  /** When the next sweep is expected, or `null` when collection is off or nothing has run yet. */
  readonly nextSweepAt: string | null;
  readonly isEnabled: boolean;
  /** How many projects the last sweep refreshed, and how many it could not. 
ull before any sweep. */
  readonly projectsRefreshed: number | null;
  readonly projectsFailed: number | null;
}

/** Which build is running, which is the first question worth answering when something looks wrong. */
export interface AboutConsoleOps {
  readonly version: string;
  readonly build: string | null;
  readonly runtime: string;
  readonly databaseSchema: 'upToDate' | 'pendingMigrations' | 'unknown';
}

/**
 * How long Console Ops keeps what it recorded, and what the last sweep removed.
 *
 * Reported because retention is the only thing Console Ops does that destroys recorded facts. A sweep that has
 * not run reports nothing rather than implying it removed zero.
 */
export interface RetentionSettings {
  readonly isEnabled: boolean;
  readonly days: number;
  readonly lastSweepAt: string | null;
  readonly lastSweepSucceeded: boolean | null;
  readonly observationsRemoved: number | null;
}

export interface SettingsSnapshot {
  /** ISO-8601 UTC composition time. Relative times are measured against it. */
  readonly observedAt: string;
  readonly integrations: readonly Integration[];
  readonly collection: CollectionSettings;
  readonly retention: RetentionSettings;
  readonly about: AboutConsoleOps;
}
