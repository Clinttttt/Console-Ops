import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';

import { PROJECT_REGISTRY_FIXTURE } from '../../core/data/mock/project-registry.fixture';
import { ProjectRegistryDataSource } from '../../core/data/project-registry.data-source';
import { EnvironmentScopeStore } from '../../core/state/environment-scope.store';
import { ProjectsPage } from './projects-page';

describe('ProjectsPage', () => {
  let fixture: ComponentFixture<ProjectsPage>;
  let host: HTMLElement;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ProjectsPage],
      providers: [
        provideRouter([]),
        {
          provide: ProjectRegistryDataSource,
          useValue: { load: () => of(PROJECT_REGISTRY_FIXTURE) },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(ProjectsPage);
    await fixture.whenStable();
    host = fixture.nativeElement as HTMLElement;
  });

  function rows(): HTMLElement[] {
    return Array.from(host.querySelectorAll('co-project-registry-table tbody tr'));
  }

  function rowNames(): (string | undefined)[] {
    return rows().map((row) => row.querySelector('.project-name')?.textContent?.trim());
  }

  function clickView(label: string): void {
    const view = Array.from(host.querySelectorAll<HTMLButtonElement>('.view')).find(
      (button) => button.textContent?.trim() === label,
    );
    view?.click();
  }

  it('lists active projects and excludes archived ones by default', () => {
    expect(rowNames()).toEqual(['Spinner API', 'StallTrack', 'AMYL', 'Console Ops', 'StockPilot']);
    expect(host.textContent).not.toContain('Legacy Billing');
  });

  it('shows configuration and state for each project', () => {
    const spinner = rows()[0];

    expect(spinner.textContent).toContain('ASP.NET Core Web API');
    expect(spinner.textContent).toContain('API');
    expect(spinner.textContent).toContain('Production');
    expect(spinner.textContent).toContain('Healthy');
    expect(spinner.textContent).toContain('spinner/api');
    expect(spinner.textContent).toContain('main');
    expect(spinner.textContent).toContain('.NET 8');
    expect(spinner.textContent).toContain('Azure App Service');
    expect(spinner.textContent).toContain('May 14, 2025');
    expect(spinner.textContent).not.toContain('09:23');
    expect(spinner.textContent).toContain('by ci-bot');
  });

  it('narrows the registry to a quick view', () => {
    clickView('Local');
    fixture.detectChanges();

    expect(rowNames()).toEqual(['AMYL']);
    expect(TestBed.inject(EnvironmentScopeStore).scope()).toBe('local');
  });

  it('shows archived projects with honest unmonitored state', () => {
    clickView('Archived');
    fixture.detectChanges();

    expect(rowNames()).toEqual(['Legacy Billing']);
    const archived = rows()[0];
    expect(archived.textContent).toContain('Unknown');
    expect(archived.textContent).toContain('Not configured');
    expect(archived.textContent).toContain('Never deployed');
  });

  it('filters by name, repository, or descriptor', async () => {
    const search = host.querySelector<HTMLInputElement>('#co-project-search');
    search!.value = 'stockpilot/worker';
    search!.dispatchEvent(new Event('input'));
    await fixture.whenStable();

    expect(rowNames()).toEqual(['StockPilot']);
  });

  it('reports an empty view instead of showing nothing', async () => {
    const search = host.querySelector<HTMLInputElement>('#co-project-search');
    search!.value = 'nothing-matches-this';
    search!.dispatchEvent(new Event('input'));
    await fixture.whenStable();

    expect(rows().length).toBe(0);
    expect(host.textContent).toContain('No registered projects match this view.');
  });

  it('clears every filter from the empty state', async () => {
    TestBed.inject(EnvironmentScopeStore).select('staging');
    await fixture.whenStable();
    expect(rows().length).toBe(0);

    host.querySelector<HTMLButtonElement>('.reset')!.click();
    await fixture.whenStable();

    expect(rowNames().length).toBe(5);
    expect(TestBed.inject(EnvironmentScopeStore).scope()).toBeNull();
  });

  it('lists the newest registrations in the intake rail', () => {
    const recent = Array.from(host.querySelectorAll('co-project-intake .recent-name')).map((item) =>
      item.textContent?.trim(),
    );

    expect(recent).toEqual(['Console Ops', 'StockPilot', 'AMYL']);
  });

  it('links to registration and keeps per-project actions unavailable', () => {
    const add = host.querySelector<HTMLAnchorElement>('.add');

    expect(add?.getAttribute('href')).toBe('/projects/new');
    expect(host.querySelectorAll('.action[aria-disabled="true"]').length).toBe(10);
  });
});
