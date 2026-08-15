import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';

import { DashboardOverviewDataSource } from '../../core/data/dashboard-overview.data-source';
import { DASHBOARD_OVERVIEW_FIXTURE } from '../../core/data/mock/dashboard-overview.fixture';
import { PROJECT_REGISTRY_FIXTURE } from '../../core/data/mock/project-registry.fixture';
import { ProjectRegistryDataSource } from '../../core/data/project-registry.data-source';
import { EnvironmentScopeStore } from '../../core/state/environment-scope.store';
import { EnvironmentsPage } from './environments-page';

describe('EnvironmentsPage', () => {
  let fixture: ComponentFixture<EnvironmentsPage>;
  let host: HTMLElement;

  /** Every environment configured across the fixture's projects. */
  const configuredCount = PROJECT_REGISTRY_FIXTURE.reduce(
    (total, project) => total + project.environments.length,
    0,
  );

  function configure(projectsFail = false): void {
    TestBed.configureTestingModule({
      imports: [EnvironmentsPage],
      providers: [
        provideRouter([]),
        {
          provide: ProjectRegistryDataSource,
          useValue: {
            load: () =>
              projectsFail
                ? throwError(() => new Error('unavailable'))
                : of(PROJECT_REGISTRY_FIXTURE),
            getProject: () => of(PROJECT_REGISTRY_FIXTURE[0]),
            register: () => of(PROJECT_REGISTRY_FIXTURE[0]),
            refreshProject: () => of(null),
          },
        },
        {
          provide: DashboardOverviewDataSource,
          useValue: { load: () => of(DASHBOARD_OVERVIEW_FIXTURE) },
        },
      ],
    });
  }

  async function render(): Promise<void> {
    fixture = TestBed.createComponent(EnvironmentsPage);
    await fixture.whenStable();
    host = fixture.nativeElement as HTMLElement;
  }

  function cards(): HTMLElement[] {
    return Array.from(host.querySelectorAll('co-environment-groups .card'));
  }

  function rail(): string {
    return host.querySelector('co-selected-environment')?.textContent ?? '';
  }

  it('lists every configured environment across projects', async () => {
    configure();
    await render();

    expect(cards().length).toBe(configuredCount);
    expect(host.textContent).toContain(`Showing ${configuredCount} of ${configuredCount}`);
  });

  it('shows registered configuration for each environment', async () => {
    configure();
    await render();

    const first = cards()[0];
    expect(first.textContent).toContain(PROJECT_REGISTRY_FIXTURE[0].name);
    expect(first.textContent).toContain(
      PROJECT_REGISTRY_FIXTURE[0].environments[0].applicationUrl!,
    );
  });

  it('says an environment has not been observed rather than inventing state', async () => {
    configure();
    await render();

    // The overview fixture holds no observation for these environments.
    expect(host.textContent).toContain('Not observed yet');
  });

  it('highlights nothing until an environment is chosen', async () => {
    configure();
    await render();

    expect(host.querySelectorAll('.card.is-selected').length).toBe(0);
    expect(rail()).toContain('Select an environment to inspect its configuration.');
  });

  it('describes the chosen environment and links to its project', async () => {
    configure();
    await render();

    cards()[0].click();
    await fixture.whenStable();

    const project = PROJECT_REGISTRY_FIXTURE[0];
    expect(host.querySelectorAll('.card.is-selected').length).toBe(1);
    expect(rail()).toContain(`${project.name} / ${project.environments[0].name}`);
    expect(
      host
        .querySelector<HTMLAnchorElement>('co-selected-environment a.button[href]')
        ?.getAttribute('href'),
    ).toBeTruthy();
  });

  it('narrows to one kind through the shared scope', async () => {
    configure();
    await render();

    const staging = Array.from(host.querySelectorAll<HTMLButtonElement>('.view')).find(
      (button) => button.textContent?.trim() === 'Staging',
    );
    staging!.click();
    await fixture.whenStable();

    expect(TestBed.inject(EnvironmentScopeStore).scope()).toBe('staging');
    expect(cards().length).toBeLessThan(configuredCount);
  });

  it('clears a selection the filters removed', async () => {
    configure();
    await render();

    cards()[0].click();
    await fixture.whenStable();
    expect(host.querySelectorAll('.card.is-selected').length).toBe(1);

    const search = host.querySelector<HTMLInputElement>('#co-environment-search');
    search!.value = 'nothing-matches-this';
    search!.dispatchEvent(new Event('input'));
    await fixture.whenStable();

    expect(cards().length).toBe(0);
    expect(rail()).toContain('Select an environment');
    expect(host.textContent).toContain('No environments match this view.');
  });

  it('reports when the registry could not be read', async () => {
    configure(true);
    await render();

    expect(host.textContent).toContain('Environment configuration could not be read.');
  });

  it('keeps environment creation unavailable', async () => {
    configure();
    await render();

    const add = host.querySelector<HTMLButtonElement>('.add');
    expect(add?.disabled).toBe(true);
    expect(add?.title).toContain('editing its project');
  });
});
