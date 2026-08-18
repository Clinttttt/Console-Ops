import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map, switchMap } from 'rxjs';

import {
  AboutConsoleOps,
  CollectionSettings,
  Integration,
  IntegrationConfigurationState,
  IntegrationVerificationState,
  SettingsSnapshot,
} from '../contracts/settings';
import { SettingsDataSource } from './settings.data-source';

/** The wire shape of `GET /api/settings/configuration`. */
interface ConfigurationStatus {
  readonly observedAt: string;
  readonly probed: boolean;
  readonly capabilities: readonly {
    readonly capability: string;
    readonly state: 'configured' | 'missing' | 'default';
    readonly keys: readonly {
      readonly key: string;
      readonly state: string;
      readonly required: boolean;
    }[];
    readonly connection: { readonly succeeded: boolean; readonly failure: string | null } | null;
  }[];
  readonly collection: {
    readonly isEnabled: boolean;
    readonly intervalSeconds: number;
    readonly lastSweepAt: string | null;
    readonly lastSweepSucceeded: boolean | null;
    readonly lastSweepMilliseconds: number | null;
    readonly projectsRefreshed: number | null;
    readonly projectsFailed: number | null;
    readonly nextSweepAt: string | null;
  };
  readonly about: {
    readonly version: string;
    readonly build: string | null;
    readonly runtime: string;
    readonly databaseSchema: string;
  };
}

/**
 * Reads Console Ops' own configuration from Console Ops.
 *
 * Two capabilities become the integrations the screen shows. The rest of the report - exposure and collection
 * keys - is deliberately not surfaced as an integration: those are not providers Console Ops connects to, and
 * the screen would be claiming a connection that does not exist.
 */
@Injectable()
export class HttpSettingsDataSource extends SettingsDataSource {
  private readonly http = inject(HttpClient);

  override load(): Observable<SettingsSnapshot> {
    return this.read(false);
  }

  override probe(): Observable<SettingsSnapshot> {
    return this.read(true);
  }

  /**
   * Runs the same sweep the scheduled worker runs, then re-reads so the screen reports it from the same place it
   * always does rather than from the command's own answer.
   */
  override collectNow(): Observable<SettingsSnapshot> {
    return this.http
      .post('/api/settings/collection/sweeps', null)
      .pipe(switchMap(() => this.read(false)));
  }

  private read(probe: boolean): Observable<SettingsSnapshot> {
    return this.http
      .get<ConfigurationStatus>('/api/settings/configuration', {
        params: probe ? { probe: 'true' } : {},
      })
      .pipe(map((status) => toSnapshot(status)));
  }
}

function toSnapshot(status: ConfigurationStatus): SettingsSnapshot {
  return {
    observedAt: status.observedAt,
    integrations: [
      toIntegration(
        status,
        'github',
        'Source and CI',
        'GitHub',
        'Source control and workflow access',
      ),
      toIntegration(status, 'azure', 'Azure', 'Azure', 'Container Apps runtime access'),
    ],
    collection: toCollection(status.collection),
    about: toAbout(status.about),
  };
}

function toIntegration(
  status: ConfigurationStatus,
  id: Integration['id'],
  capability: string,
  name: string,
  purpose: string,
): Integration {
  const reported = status.capabilities.find((entry) => entry.capability === capability) ?? null;
  const connection = reported?.connection ?? null;

  return {
    id,
    name,
    purpose,
    configuration: toConfiguration(reported?.state ?? 'missing'),
    verification: toVerification(connection),
    authentication: toAuthentication(id, reported?.state ?? 'missing'),
    // The report states when it was composed, which for a probed read is when the check ran.
    verifiedAt: connection?.succeeded === true ? status.observedAt : null,
    failure: connection?.succeeded === false ? (connection.failure ?? null) : null,
  };
}

function toConfiguration(state: string): IntegrationConfigurationState {
  switch (state) {
    case 'configured':
      return 'configured';
    // An optional key left unset is not missing configuration: something else stands in for it.
    case 'default':
      return 'configured';
    default:
      return 'notConfigured';
  }
}

function toVerification(
  connection: { readonly succeeded: boolean } | null,
): IntegrationVerificationState {
  if (connection === null) {
    return 'notProbed';
  }

  return connection.succeeded ? 'verified' : 'failed';
}

/**
 * How the credential is obtained, by mechanism only. Azure resolves an ambient identity when its keys are
 * unset, which is a fact worth stating because it explains why nothing looks configured yet still works.
 */
function toAuthentication(id: Integration['id'], state: string): string | null {
  if (id === 'azure') {
    return state === 'configured' ? 'Service principal' : 'DefaultAzureCredential';
  }

  return state === 'configured' ? 'Personal access token' : null;
}

/** Collection as Console Ops reports it: schedule from configuration, sweep facts only if one has run. */
function toCollection(reported: ConfigurationStatus['collection']): CollectionSettings {
  return {
    intervalSeconds: reported.intervalSeconds,
    // The interval comes from application settings and cannot be changed at runtime.
    isIntervalEditable: false,
    lastSweepAt: reported.lastSweepAt,
    lastSweepSucceeded: reported.lastSweepSucceeded,
    lastSweepMilliseconds: reported.lastSweepMilliseconds,
    nextSweepAt: reported.nextSweepAt,
    isEnabled: reported.isEnabled,
    projectsRefreshed: reported.projectsRefreshed,
    projectsFailed: reported.projectsFailed,
  };
}

function toAbout(about: ConfigurationStatus['about']): AboutConsoleOps {
  return {
    version: about.version,
    build: about.build,
    runtime: about.runtime,
    databaseSchema:
      about.databaseSchema === 'upToDate'
        ? 'upToDate'
        : about.databaseSchema === 'pendingMigrations'
          ? 'pendingMigrations'
          : 'unknown',
  };
}
