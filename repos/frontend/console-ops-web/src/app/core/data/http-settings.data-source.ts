import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';

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
   * Collection has no endpoint yet: the refresh worker does not record its sweeps. Asking for a refresh would
   * therefore report a result Console Ops cannot observe, so the read is simply repeated and the screen keeps
   * saying the section is not available.
   */
  override collectNow(): Observable<SettingsSnapshot> {
    return this.read(false);
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
    collection: toCollection(),
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

/** Collection is not reported yet, and an absent section is honest where an invented one is not. */
function toCollection(): CollectionSettings | null {
  return null;
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
