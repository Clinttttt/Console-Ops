import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';

import { App } from './app';
import { DashboardOverviewDataSource } from './core/data/dashboard-overview.data-source';
import { DASHBOARD_OVERVIEW_FIXTURE } from './core/data/mock/dashboard-overview.fixture';

describe('App shell', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [
        provideRouter([]),
        {
          provide: DashboardOverviewDataSource,
          useValue: { load: () => of(DASHBOARD_OVERVIEW_FIXTURE) },
        },
      ],
    }).compileComponents();
  });

  async function render(): Promise<HTMLElement> {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    return fixture.nativeElement as HTMLElement;
  }

  it('uses Console Ops branding', async () => {
    const host = await render();
    const logo = host.querySelector<HTMLImageElement>('.mark-image');

    expect(host.textContent).toContain('Console Ops');
    expect(host.textContent).not.toContain('DevDeck');
    expect(logo?.getAttribute('src')).toBe('/console-ops-logo.png');
    expect(logo?.getAttribute('alt')).toBe('');
  });

  it('exposes the shipped destinations as navigable and the rest as planned', async () => {
    const host = await render();
    const navigable = Array.from(host.querySelectorAll('a.nav-item')).map((item) =>
      item.textContent?.trim(),
    );

    expect(navigable).toEqual(['Overview', 'Projects', 'Deployments', 'Environments']);
    expect(host.querySelectorAll('.nav-item.is-planned').length).toBe(3);
  });

  it('shows partial visibility without inventing uptime history', async () => {
    const host = await render();

    expect(host.textContent).toContain('Partial visibility');
    expect(host.textContent).toContain('Not recorded yet');
    expect(host.textContent).not.toContain('All Systems Operational');
  });

  it('reports observed availability with what it was measured from', async () => {
    TestBed.resetTestingModule();
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [
        provideRouter([]),
        {
          provide: DashboardOverviewDataSource,
          useValue: {
            load: () =>
              of({
                ...DASHBOARD_OVERVIEW_FIXTURE,
                summary: {
                  ...DASHBOARD_OVERVIEW_FIXTURE.summary,
                  uptime: {
                    windowHours: 24,
                    since: '2026-08-15T09:00:00Z',
                    percentage: 99.7,
                    checks: 288,
                    samples: [100, 95.5, 100],
                  },
                },
              }),
          },
        },
      ],
    }).compileComponents();

    const host = await render();

    expect(host.textContent).toContain('99.7%');
    // The figure names its own basis, because sampled availability is not a guarantee.
    expect(host.textContent).toContain('last 24h');
    expect(host.textContent).toContain('288 checks');
    expect(host.textContent).not.toContain('Not recorded yet');
    expect(host.querySelector('co-sparkline')).not.toBeNull();
  });
});
