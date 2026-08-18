import { Injectable } from '@angular/core';
import { Observable, delay, of } from 'rxjs';

import { SettingsSnapshot } from '../../contracts/settings';
import { SettingsDataSource } from '../settings.data-source';

/**
 * Sample data for the Settings screen, while the screen is designed.
 *
 * Labelled at the source: every snapshot carries `isSampleData: true`, and the screen states it. This exists
 * to settle layout and wording, not to describe the running instance - `GET /api/settings/configuration`
 * already reports the real integration state, and this adapter is deleted when the screen reads it.
 *
 * The values are chosen to show the distinction the screen is for: GitHub is configured **and verified**,
 * Azure is configured but **not yet probed**. Those must never look the same.
 */
const OBSERVED_AT = '2026-08-18T16:19:00.000Z';

const SAMPLE: SettingsSnapshot = {
  observedAt: OBSERVED_AT,
  isSampleData: true,
  integrations: [
    {
      id: 'github',
      name: 'GitHub',
      purpose: 'Source control and workflow access',
      configuration: 'configured',
      verification: 'verified',
      authentication: 'Personal access token',
      verifiedAt: '2026-08-18T16:05:00.000Z',
      failure: null,
    },
    {
      id: 'azure',
      name: 'Azure',
      purpose: 'Container Apps runtime access',
      configuration: 'configured',
      verification: 'notProbed',
      authentication: 'DefaultAzureCredential',
      verifiedAt: null,
      failure: null,
    },
  ],
  collection: {
    intervalSeconds: 30,
    isIntervalEditable: false,
    lastSweepAt: '2026-08-18T16:18:42.000Z',
    lastSweepSucceeded: true,
    lastSweepMilliseconds: 1400,
    nextSweepAt: '2026-08-18T16:19:12.000Z',
    isEnabled: true,
  },
  about: {
    version: '0.4.0',
    build: '8a17c2f',
    runtime: '.NET 10',
    databaseSchema: 'upToDate',
  },
};

@Injectable()
export class MockSettingsDataSource extends SettingsDataSource {
  override load(): Observable<SettingsSnapshot> {
    return of(SAMPLE).pipe(delay(120));
  }

  /**
   * Stands in for a real probe: both integrations come back verified as of now. Delayed to match how long
   * contacting two providers actually takes, so the screen is designed against the wait rather than around it.
   */
  override probe(): Observable<SettingsSnapshot> {
    const verifiedAt = new Date(Date.parse(OBSERVED_AT) + 60_000).toISOString();

    return of({
      ...SAMPLE,
      observedAt: verifiedAt,
      integrations: SAMPLE.integrations.map((integration) => ({
        ...integration,
        verification: 'verified' as const,
        verifiedAt,
      })),
    }).pipe(delay(900));
  }

  override collectNow(): Observable<SettingsSnapshot> {
    const sweptAt = new Date(Date.parse(OBSERVED_AT) + 60_000).toISOString();

    return of({
      ...SAMPLE,
      observedAt: sweptAt,
      collection: {
        ...SAMPLE.collection,
        lastSweepAt: sweptAt,
        lastSweepSucceeded: true,
        lastSweepMilliseconds: 1120,
        nextSweepAt: new Date(Date.parse(sweptAt) + 30_000).toISOString(),
      },
    }).pipe(delay(700));
  }
}
