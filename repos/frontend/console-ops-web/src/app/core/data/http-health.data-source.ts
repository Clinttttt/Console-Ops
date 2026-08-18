import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';

import {
  EnvironmentHealth,
  HealthCheck,
  HealthCheckKind,
  HealthCheckState,
  HealthSnapshot,
  HealthStateChange,
} from '../contracts/health';
import { EnvironmentKind, StatusLevel } from '../contracts/dashboard-overview';
import { HealthDataSource } from './health.data-source';

/** The wire shape of `GET /api/health`. */
interface HealthOverview {
  readonly observedAt: string;
  readonly summary: {
    readonly healthy: number;
    readonly degraded: number;
    readonly down: number;
    readonly lastCheckedAt: string | null;
  };
  readonly environments: readonly {
    readonly id: string;
    readonly projectId: string;
    readonly projectName: string;
    readonly environmentName: string;
    readonly environmentKind: string;
    readonly state: string;
    readonly checkedAt: string | null;
    readonly responseMilliseconds: number | null;
    readonly checks: readonly {
      readonly name: string;
      readonly kind: string;
      readonly state: string;
      readonly responseMilliseconds: number | null;
    }[];
    readonly healthySince: string | null;
    readonly failingSince: string | null;
    readonly consecutiveFailures: number;
    readonly lastHealthyAt: string | null;
    readonly window: {
      readonly availabilityPercentage: number | null;
      readonly checks: number | null;
      readonly failedChecks: number | null;
      readonly longestOutageSeconds: number | null;
    };
  }[];
  readonly stateChanges: readonly {
    readonly occurredAt: string;
    readonly projectName: string;
    readonly environmentName: string;
    readonly description: string;
    readonly level: string;
  }[];
}

/**
 * Reads recorded health from Console Ops.
 *
 * A read only: the API reports observations the worker already collected, so opening this screen never causes an
 * application to be probed.
 */
@Injectable()
export class HttpHealthDataSource extends HealthDataSource {
  private readonly http = inject(HttpClient);

  override load(): Observable<HealthSnapshot> {
    return this.http.get<HealthOverview>('/api/health').pipe(map(toSnapshot));
  }
}

function toSnapshot(overview: HealthOverview): HealthSnapshot {
  return {
    observedAt: overview.observedAt,
    summary: overview.summary,
    environments: overview.environments.map(toEnvironment),
    stateChanges: overview.stateChanges.map(toStateChange),
  };
}

function toEnvironment(reported: HealthOverview['environments'][number]): EnvironmentHealth {
  return {
    id: reported.id,
    projectId: reported.projectId,
    projectName: reported.projectName,
    environmentName: reported.environmentName,
    environmentKind: toEnvironmentKind(reported.environmentKind),
    state: toState(reported.state),
    checkedAt: reported.checkedAt,
    responseMilliseconds: reported.responseMilliseconds,
    checks: reported.checks.map(toCheck),
    healthySince: reported.healthySince,
    failingSince: reported.failingSince,
    consecutiveFailures: reported.consecutiveFailures,
    lastHealthyAt: reported.lastHealthyAt,
    window: {
      availabilityPercentage: reported.window.availabilityPercentage,
      checks: reported.window.checks,
      failedChecks: reported.window.failedChecks,
      longestOutageSeconds: reported.window.longestOutageSeconds,
    },
  };
}

function toCheck(reported: {
  name: string;
  kind: string;
  state: string;
  responseMilliseconds: number | null;
}): HealthCheck {
  return {
    name: reported.name,
    kind: toKind(reported.kind),
    state: toState(reported.state),
    responseMilliseconds: reported.responseMilliseconds,
  };
}

function toStateChange(reported: HealthOverview['stateChanges'][number]): HealthStateChange {
  return {
    at: reported.occurredAt,
    projectName: reported.projectName,
    environmentName: reported.environmentName,
    description: reported.description,
    level: (reported.level as StatusLevel) ?? 'unknown',
  };
}

/**
 * An unrecognized state reads as unknown rather than as anything reassuring. The API and this contract are
 * versioned together, so this only matters if they ever drift.
 */
function toState(state: string): HealthCheckState {
  switch (state) {
    case 'healthy':
    case 'degraded':
    case 'unhealthy':
    case 'unreachable':
    case 'notConfigured':
      return state;
    default:
      return 'unknown';
  }
}

function toKind(kind: string): HealthCheckKind {
  switch (kind) {
    case 'application':
    case 'database':
    case 'cache':
    case 'external':
      return kind;
    default:
      return 'unknown';
  }
}

function toEnvironmentKind(kind: string): EnvironmentKind {
  switch (kind) {
    case 'production':
    case 'staging':
    case 'local':
      return kind;
    default:
      return 'staging';
  }
}
