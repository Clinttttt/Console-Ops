import { Injectable } from '@angular/core';
import { Observable, delay, of } from 'rxjs';

import { EnvironmentHealth, HealthSnapshot } from '../../contracts/health';
import { HealthDataSource } from '../health.data-source';

/**
 * Sample data for the Health screen, while the screen is designed.
 *
 * Labelled at the source: the snapshot carries `isSampleData: true` and the screen states it. Deleted when the
 * screen reads recorded health observations, which Console Ops already collects - this exists to settle layout
 * and wording, not to describe anything running.
 *
 * The values are chosen to exercise what the screen must get right: a healthy environment with four checks, one
 * degraded by a dependency rather than by the application, a local project that is running without claiming to
 * be healthy, and an environment whose availability window has too few checks to report.
 */
const OBSERVED_AT = '2026-08-18T09:52:14.000Z';

const ENVIRONMENTS: readonly EnvironmentHealth[] = [
  {
    id: 'spinner-production',
    projectId: 'spinner',
    projectName: 'Spinner API',
    environmentName: 'Production',
    environmentKind: 'production',
    state: 'healthy',
    checkedAt: '2026-08-18T09:52:00.000Z',
    responseMilliseconds: 91,
    checks: [
      { name: 'Application', kind: 'application', state: 'healthy', responseMilliseconds: 91 },
      { name: 'Database', kind: 'database', state: 'healthy', responseMilliseconds: null },
      { name: 'Redis', kind: 'cache', state: 'healthy', responseMilliseconds: null },
      { name: 'Payments', kind: 'external', state: 'healthy', responseMilliseconds: null },
    ],
    healthySince: '2026-08-15T19:41:00.000Z',
    failingSince: null,
    consecutiveFailures: 0,
    lastHealthyAt: '2026-08-18T09:52:00.000Z',
    window: {
      availabilityPercentage: 99.98,
      checks: 2874,
      failedChecks: 1,
      longestOutageSeconds: 31,
    },
  },
  {
    id: 'stalltrack-production',
    projectId: 'stalltrack',
    projectName: 'StallTrack',
    environmentName: 'Production',
    environmentKind: 'production',
    state: 'healthy',
    checkedAt: '2026-08-18T09:51:56.000Z',
    responseMilliseconds: 104,
    checks: [
      { name: 'Application', kind: 'application', state: 'healthy', responseMilliseconds: 104 },
      { name: 'Database', kind: 'database', state: 'healthy', responseMilliseconds: null },
    ],
    healthySince: '2026-08-17T04:12:00.000Z',
    failingSince: null,
    consecutiveFailures: 0,
    lastHealthyAt: '2026-08-18T09:51:56.000Z',
    window: {
      availabilityPercentage: 99.86,
      checks: 2861,
      failedChecks: 4,
      longestOutageSeconds: 118,
    },
  },
  {
    id: 'amyl-local',
    projectId: 'amyl',
    projectName: 'AMYL',
    environmentName: 'Local',
    environmentKind: 'local',
    state: 'running',
    checkedAt: '2026-08-18T09:52:03.000Z',
    responseMilliseconds: null,
    checks: [
      // Running is not healthy: the process answers, but it has no health endpoint to claim more than that.
      { name: 'Application', kind: 'application', state: 'running', responseMilliseconds: null },
      { name: 'Database', kind: 'database', state: 'healthy', responseMilliseconds: null },
    ],
    healthySince: null,
    failingSince: null,
    consecutiveFailures: 0,
    lastHealthyAt: null,
    window: {
      // Too few recorded checks to report an availability figure, which is stated rather than rounded up.
      availabilityPercentage: null,
      checks: 6,
      failedChecks: 0,
      longestOutageSeconds: null,
    },
  },
  {
    id: 'console-ops-staging',
    projectId: 'console-ops',
    projectName: 'Console Ops',
    environmentName: 'Staging',
    environmentKind: 'staging',
    state: 'healthy',
    checkedAt: '2026-08-18T09:51:52.000Z',
    responseMilliseconds: 97,
    checks: [
      { name: 'Application', kind: 'application', state: 'healthy', responseMilliseconds: 97 },
      { name: 'Database', kind: 'database', state: 'healthy', responseMilliseconds: null },
    ],
    healthySince: '2026-08-11T06:30:00.000Z',
    failingSince: null,
    consecutiveFailures: 0,
    lastHealthyAt: '2026-08-18T09:51:52.000Z',
    window: {
      availabilityPercentage: 100,
      checks: 2880,
      failedChecks: 0,
      longestOutageSeconds: null,
    },
  },
  {
    id: 'stockpilot-production',
    projectId: 'stockpilot',
    projectName: 'StockPilot',
    environmentName: 'Production',
    environmentKind: 'production',
    // Degraded by a dependency, not by the application: the app answers in 112ms while Redis does not.
    state: 'degraded',
    checkedAt: '2026-08-18T09:52:00.000Z',
    responseMilliseconds: 112,
    checks: [
      { name: 'Application', kind: 'application', state: 'healthy', responseMilliseconds: 112 },
      { name: 'Redis', kind: 'cache', state: 'degraded', responseMilliseconds: null },
    ],
    healthySince: null,
    failingSince: '2026-08-18T09:48:48.000Z',
    consecutiveFailures: 6,
    lastHealthyAt: '2026-08-18T09:41:28.000Z',
    window: {
      availabilityPercentage: 99.51,
      checks: 2870,
      failedChecks: 14,
      longestOutageSeconds: 192,
    },
  },
];

const SAMPLE: HealthSnapshot = {
  observedAt: OBSERVED_AT,
  isSampleData: true,
  summary: {
    healthy: 3,
    degraded: 1,
    down: 0,
    lastCheckedAt: '2026-08-18T09:52:00.000Z',
  },
  environments: ENVIRONMENTS,
  stateChanges: [
    {
      at: '2026-08-18T09:52:00.000Z',
      projectName: 'StockPilot',
      environmentName: 'Production',
      description: 'Redis recovered',
      level: 'healthy',
    },
    {
      at: '2026-08-18T09:41:28.000Z',
      projectName: 'StockPilot',
      environmentName: 'Production',
      description: 'Redis became unhealthy',
      level: 'down',
    },
    {
      at: '2026-08-18T06:18:00.000Z',
      projectName: 'Spinner API',
      environmentName: 'Production',
      description: 'Application recovered',
      level: 'healthy',
    },
  ],
};

@Injectable()
export class MockHealthDataSource extends HealthDataSource {
  override load(): Observable<HealthSnapshot> {
    return of(SAMPLE).pipe(delay(120));
  }
}
