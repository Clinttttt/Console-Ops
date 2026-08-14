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

    expect(host.textContent).toContain('Console Ops');
    expect(host.textContent).not.toContain('DevDeck');
  });

  it('exposes only the Overview destination as navigable', async () => {
    const host = await render();
    const navigable = Array.from(host.querySelectorAll('a.nav-item')).map((item) =>
      item.textContent?.trim(),
    );

    expect(navigable).toEqual(['Overview']);
    expect(host.querySelectorAll('.nav-item.is-planned').length).toBe(6);
  });

  it('shows partial visibility without inventing uptime history', async () => {
    const host = await render();

    expect(host.textContent).toContain('Partial visibility');
    expect(host.textContent).toContain('Not recorded yet');
    expect(host.textContent).not.toContain('All Systems Operational');
  });
});
