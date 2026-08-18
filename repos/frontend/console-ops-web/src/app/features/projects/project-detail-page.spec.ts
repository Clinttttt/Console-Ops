import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { Observable, of, throwError } from 'rxjs';

import { DashboardOverviewDataSource } from '../../core/data/dashboard-overview.data-source';
import { DASHBOARD_OVERVIEW_FIXTURE } from '../../core/data/mock/dashboard-overview.fixture';
import { PROJECT_REGISTRY_FIXTURE } from '../../core/data/mock/project-registry.fixture';
import { ProjectRegistryDataSource } from '../../core/data/project-registry.data-source';
import { ProjectListItem } from '../../core/contracts/project-registry';
import { ProjectDetailPage } from './project-detail-page';

describe('ProjectDetailPage', () => {
  let harness: RouterTestingHarness;
  let host: HTMLElement;
  let projectResult: Observable<ProjectListItem>;
  let refreshResult: Observable<unknown>;
  let refreshedProjectId: string | null;

  const project = PROJECT_REGISTRY_FIXTURE[0];

  beforeEach(() => {
    refreshedProjectId = null;
    projectResult = of(project);
    refreshResult = of(null);

    TestBed.configureTestingModule({
      providers: [
        provideRouter([{ path: 'projects/:projectId', component: ProjectDetailPage }]),
        {
          provide: ProjectRegistryDataSource,
          useValue: {
            load: () => of(PROJECT_REGISTRY_FIXTURE),
            getProject: () => projectResult,
            refreshProject: (projectId: string) => {
              refreshedProjectId = projectId;
              return refreshResult;
            },
          },
        },
        {
          provide: DashboardOverviewDataSource,
          useValue: { load: () => of(DASHBOARD_OVERVIEW_FIXTURE) },
        },
      ],
    });
  });

  async function open(projectId = project.id): Promise<void> {
    harness = await RouterTestingHarness.create();
    await harness.navigateByUrl(`/projects/${projectId}`, ProjectDetailPage);
    host = harness.routeNativeElement as HTMLElement;
  }

  it('shows the registered configuration', async () => {
    await open();

    expect(host.querySelector('.name')?.textContent?.trim()).toBe(project.name);
    expect(host.textContent).toContain(`${project.repository.owner}/${project.repository.name}`);
    expect(host.textContent).toContain(project.repository.defaultBranch);
    expect(host.textContent).toContain('Configuration version');
  });

  it('renders one panel per configured environment', async () => {
    await open();

    expect(host.querySelectorAll('co-project-environment-panel').length).toBe(
      project.environments.length,
    );
  });

  it('says an environment has not been observed rather than borrowing another', async () => {
    await open();

    // The dashboard fixture holds no observation for this project, so nothing may be shown as observed.
    expect(host.textContent).toContain('Not observed yet');
    expect(host.textContent).not.toContain('Healthy');
  });

  it('refreshes observations on request', async () => {
    await open();

    host.querySelector<HTMLButtonElement>('.refresh')!.click();
    harness.detectChanges();

    expect(refreshedProjectId).toBe(project.id);
  });

  it('reports a failed refresh without discarding the project', async () => {
    refreshResult = throwError(() => new HttpErrorResponse({ status: 500 }));
    await open();

    host.querySelector<HTMLButtonElement>('.refresh')!.click();
    harness.detectChanges();

    expect(host.querySelector('[role="alert"]')?.textContent).toContain('could not be completed');
    expect(host.querySelector('.name')?.textContent?.trim()).toBe(project.name);
  });

  it('reports an unknown project instead of an empty screen', async () => {
    projectResult = throwError(() => new HttpErrorResponse({ status: 404 }));
    await open('11111111-1111-1111-1111-111111111111');

    expect(host.textContent).toContain('not registered');
    expect(host.querySelector<HTMLAnchorElement>('.back')?.getAttribute('href')).toBe('/projects');
  });
});
