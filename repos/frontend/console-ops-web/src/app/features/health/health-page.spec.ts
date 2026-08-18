import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';

import { HealthSnapshot } from '../../core/contracts/health';
import { HealthDataSource } from '../../core/data/health.data-source';
import { HealthPage } from './health-page';

const OBSERVED_AT = '2026-08-18T09:52:14.000Z';

const SNAPSHOT: HealthSnapshot = {
  observedAt: OBSERVED_AT,
  summary: { healthy: 2, degraded: 1, down: 0, lastCheckedAt: '2026-08-18T09:52:00.000Z' },
  environments: [
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
        { name: 'Redis', kind: 'cache', state: 'healthy', responseMilliseconds: null },
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
      id: 'amyl-local',
      projectId: 'amyl',
      projectName: 'AMYL',
      environmentName: 'Local',
      environmentKind: 'local',
      state: 'notConfigured',
      checkedAt: '2026-08-18T09:52:03.000Z',
      responseMilliseconds: null,
      checks: [
        {
          name: 'Application',
          kind: 'application',
          state: 'notConfigured',
          responseMilliseconds: null,
        },
      ],
      healthySince: null,
      failingSince: null,
      consecutiveFailures: 0,
      lastHealthyAt: null,
      window: {
        availabilityPercentage: null,
        checks: 6,
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
  ],
  stateChanges: [
    {
      at: '2026-08-18T09:41:28.000Z',
      projectName: 'StockPilot',
      environmentName: 'Production',
      description: 'Redis became unhealthy',
      level: 'down',
    },
  ],
};

describe('HealthPage', () => {
  let fixture: ComponentFixture<HealthPage>;
  let host: HTMLElement;

  async function render(snapshot: HealthSnapshot = SNAPSHOT): Promise<void> {
    TestBed.resetTestingModule();
    await TestBed.configureTestingModule({
      imports: [HealthPage],
      providers: [
        provideRouter([]),
        { provide: HealthDataSource, useValue: { load: () => of(snapshot) } },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(HealthPage);
    await fixture.whenStable();
    host = fixture.nativeElement as HTMLElement;
  }

  function rowFor(name: string): Element | undefined {
    return Array.from(host.querySelectorAll('co-health-row')).find((row) =>
      row.textContent?.includes(name),
    );
  }

  beforeEach(async () => {
    await render();
  });

  it('does not report an unconfigured check as healthy', () => {
    // No health endpoint was asked for, which is not a failure and is certainly not evidence of health.
    expect(rowFor('AMYL')?.textContent).toContain('Not configured');
    expect(rowFor('AMYL')?.textContent).not.toContain('Healthy');
  });

  it('states the counts and when anything was last checked', () => {
    const strip = host.querySelector('.strip');

    expect(strip?.textContent).toContain('Healthy');
    expect(strip?.textContent).toContain('Degraded');
    expect(strip?.textContent).toContain('09:52:00');
  });

  it('lifts a failing environment into active issues with the scale of the failure', () => {
    const issue = host.querySelector('.issue');

    // The question this screen answers first is "is anything wrong", so a failure is not left to be scrolled to.
    expect(issue?.textContent).toContain('StockPilot / Production');
    expect(issue?.textContent).toContain('Degraded');
    expect(issue?.textContent).toContain('3m 26s');
    expect(issue?.textContent).toContain('6');
    expect(issue?.textContent).toContain('09:41:28');
  });

  it('shows the dependency behind a verdict without needing the row opened', () => {
    const row = rowFor('StockPilot');

    // The application answers in 112ms while Redis does not: the row has to make the cause visible.
    expect(row?.textContent).toContain('Application');
    expect(row?.textContent).toContain('112 ms');
    expect(row?.textContent).toContain('Redis');
    expect(row?.textContent).toContain('Degraded');
  });

  it('narrows the list by state without touching the data source', () => {
    Array.from(host.querySelectorAll<HTMLButtonElement>('.filter'))
      .find((button) => button.textContent?.trim() === 'Failing')
      ?.click();
    fixture.detectChanges();

    expect(host.querySelectorAll('co-health-row').length).toBe(1);
    expect(rowFor('StockPilot')).toBeDefined();
    expect(rowFor('Spinner API')).toBeUndefined();
  });

  it('opens no detail until an environment is chosen', () => {
    expect(host.textContent).toContain('Select an environment to inspect it');
  });

  it('reports an availability window with too few checks as such rather than rounding it up', async () => {
    host.querySelector<HTMLButtonElement>('co-health-row .row')!.click();
    await fixture.whenStable();
    expect(host.textContent).toContain('99.98%');

    Array.from(host.querySelectorAll<HTMLButtonElement>('co-health-row .row'))[1].click();
    await fixture.whenStable();

    // Below the minimum sample a percentage would be a guess dressed as a measurement.
    expect(host.textContent).toContain('Too few checks');
    expect(host.textContent).toContain('Not currently healthy');
  });

  it('re-reads on an interval, because the screen claims to show what is functioning now', async () => {
    let reads = 0;
    TestBed.resetTestingModule();
    await TestBed.configureTestingModule({
      imports: [HealthPage],
      providers: [
        provideRouter([]),
        {
          provide: HealthDataSource,
          useValue: {
            load: () => {
              reads += 1;
              return of(SNAPSHOT);
            },
          },
        },
      ],
    }).compileComponents();

    const created = TestBed.createComponent(HealthPage);
    await created.whenStable();
    expect(reads).toBe(1);

    // What the interval does. A screen frozen at page load is the worst kind of health screen: it looks current.
    document.dispatchEvent(new Event('visibilitychange'));
    await created.whenStable();

    expect(reads).toBe(2);
  });

  it('reports facts it does not have as absent rather than as working', async () => {
    await render({
      ...SNAPSHOT,
      summary: { healthy: 0, degraded: 0, down: 0, lastCheckedAt: null },
      environments: [
        {
          ...SNAPSHOT.environments[1],
          state: 'unknown',
          checkedAt: null,
          checks: [],
        },
      ],
      stateChanges: [],
    });

    expect(host.textContent).toContain('Nothing checked yet');
    expect(host.textContent).toContain('Never checked');
    expect(host.textContent).toContain('No transition has been recorded yet');
  });
});
