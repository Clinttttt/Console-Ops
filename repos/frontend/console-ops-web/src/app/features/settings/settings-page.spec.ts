import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { BehaviorSubject, Observable, of } from 'rxjs';

import { SettingsSnapshot } from '../../core/contracts/settings';
import { SettingsDataSource } from '../../core/data/settings.data-source';
import { SettingsStore } from '../../core/state/settings.store';
import { SettingsPage } from './settings-page';

const SNAPSHOT: SettingsSnapshot = {
  observedAt: '2026-08-18T16:19:00.000Z',
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
    projectsRefreshed: 3,
    projectsFailed: 0,
  },
  about: {
    version: '0.4.0',
    build: '8a17c2f',
    runtime: '.NET 10',
    databaseSchema: 'upToDate',
  },
};

describe('SettingsPage', () => {
  let fixture: ComponentFixture<SettingsPage>;
  let host: HTMLElement;
  let probeCalls: number;
  let reads: BehaviorSubject<SettingsSnapshot>;

  async function render(snapshot: SettingsSnapshot = SNAPSHOT): Promise<void> {
    // Re-rendering with a different snapshot needs a fresh module, since beforeEach already built one.
    TestBed.resetTestingModule();
    probeCalls = 0;
    reads = new BehaviorSubject<SettingsSnapshot>(snapshot);
    await TestBed.configureTestingModule({
      imports: [SettingsPage],
      providers: [
        provideRouter([]),
        {
          provide: SettingsDataSource,
          useValue: {
            load: (): Observable<SettingsSnapshot> => of(reads.value),
            probe: (): Observable<SettingsSnapshot> => {
              probeCalls += 1;
              return of({
                ...snapshot,
                integrations: snapshot.integrations.map((integration) => ({
                  ...integration,
                  verification: 'verified' as const,
                  verifiedAt: '2026-08-18T16:20:00.000Z',
                })),
              });
            },
            collectNow: (): Observable<SettingsSnapshot> => of(reads.value),
          },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(SettingsPage);
    await fixture.whenStable();
    host = fixture.nativeElement as HTMLElement;
  }

  function rowFor(name: string): Element | undefined {
    return Array.from(host.querySelectorAll('co-integration-row')).find((row) =>
      row.textContent?.includes(name),
    );
  }

  beforeEach(async () => {
    await render();
  });

  it('separates a configured provider from a verified one', () => {
    // The distinction the screen exists for: a present key is not evidence that anything works.
    expect(rowFor('GitHub')?.textContent).toContain('Verified');
    expect(rowFor('Azure')?.textContent).toContain('Configured');
    expect(rowFor('Azure')?.textContent).toContain('Not yet probed');
    expect(rowFor('Azure')?.textContent).not.toContain('Verified');
  });

  it('names the authentication mechanism and never a credential', () => {
    expect(rowFor('Azure')?.textContent).toContain('DefaultAzureCredential');
    expect(rowFor('GitHub')?.textContent).toContain('Personal access token');
  });

  it('verifies a provider only when the operator asks', async () => {
    expect(probeCalls).toBe(0);

    host.querySelector<HTMLButtonElement>('co-integration-row .test')!.click();
    await fixture.whenStable();

    expect(probeCalls).toBe(1);
    expect(rowFor('Azure')?.textContent).not.toContain('Not yet probed');
  });

  it('states the collection interval as read-only, with where it comes from', () => {
    // Nothing here can be persisted at runtime, so no control pretends otherwise.
    expect(host.textContent).toContain('30 seconds');
    expect(host.textContent).toContain('Set in application settings');
    expect(host.querySelector('.interval button')).toBeNull();
  });

  it('reports the last sweep and when the next one is due', () => {
    expect(host.textContent).toContain('16:18:42');
    expect(host.textContent).toContain('Completed successfully');
    expect(host.textContent).toContain('1.4 s');
    expect(host.textContent).toContain('~16:19:12');
  });

  it('summarises each capability in the rail without repeating the detail', () => {
    const rail = Array.from(host.querySelectorAll('.rail-list li')).map((entry) =>
      entry.textContent?.replace(/\s+/g, ' ').trim(),
    );

    expect(rail).toEqual([
      'GitHub Verified',
      'Azure Configured',
      'Collection Running',
      'Database Up to date',
    ]);
  });

  it('keeps a verification the operator performed when the screen is read again', async () => {
    host.querySelector<HTMLButtonElement>('co-integration-row .test')!.click();
    await fixture.whenStable();
    expect(rowFor('Azure')?.textContent).not.toContain('Not yet probed');

    // What navigating away and back does: a plain read contacts nothing, so it reports nothing as probed.
    // Letting that overwrite the result made the screen forget a check it had actually performed.
    TestBed.inject(SettingsStore).read();
    await fixture.whenStable();

    expect(rowFor('Azure')?.textContent).not.toContain('Not yet probed');
    expect(rowFor('Azure')?.textContent).toContain('16:20:00');
  });

  it('drops a remembered verification once the provider stops being configured', async () => {
    host.querySelector<HTMLButtonElement>('co-integration-row .test')!.click();
    await fixture.whenStable();

    // The configuration changed after the check, so what was verified no longer describes what is there.
    reads.next({
      ...SNAPSHOT,
      integrations: SNAPSHOT.integrations.map((integration) => ({
        ...integration,
        configuration: 'notConfigured' as const,
        authentication: null,
      })),
    });
    TestBed.inject(SettingsStore).read();
    await fixture.whenStable();

    expect(rowFor('Azure')?.textContent).toContain('Not configured');
    expect(rowFor('Azure')?.textContent).not.toContain('16:20:00');
  });

  it('reports facts it does not have as absent rather than as working', async () => {
    await render({
      ...SNAPSHOT,
      integrations: [
        {
          ...SNAPSHOT.integrations[1],
          configuration: 'notConfigured',
          authentication: null,
        },
      ],
      collection: {
        ...SNAPSHOT.collection,
        isEnabled: false,
        lastSweepAt: null,
        lastSweepSucceeded: null,
        lastSweepMilliseconds: null,
        nextSweepAt: null,
        projectsRefreshed: null,
        projectsFailed: null,
      },
      about: { ...SNAPSHOT.about, build: null, databaseSchema: 'unknown' },
    });

    expect(rowFor('Azure')?.textContent).toContain('Not configured');
    // Nothing has swept since start-up, and off is not the same as nothing being due.
    expect(host.textContent).toContain('None since start-up');
    expect(host.textContent).toContain('Collection is off');
    expect(host.textContent).not.toContain('Completed successfully');
    // A database that could not be asked is unknown, which is not the same as up to date.
    expect(host.textContent).toContain('Unknown');
    const rail = Array.from(host.querySelectorAll('.rail-list li')).map((entry) =>
      entry.textContent?.replace(/\s+/g, ' ').trim(),
    );
    expect(rail).toContain('Collection Off');
  });
});
