import { HttpErrorResponse } from '@angular/common/http';
import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { Observable, of, throwError } from 'rxjs';

import { ProjectRegistrationRequest } from '../../core/contracts/project-registration';
import { ProjectListItem } from '../../core/contracts/project-registry';
import { DashboardOverviewDataSource } from '../../core/data/dashboard-overview.data-source';
import { DASHBOARD_OVERVIEW_FIXTURE } from '../../core/data/mock/dashboard-overview.fixture';
import { PROJECT_REGISTRY_FIXTURE } from '../../core/data/mock/project-registry.fixture';
import { ProjectRegistryDataSource } from '../../core/data/project-registry.data-source';
import { AddProjectPage } from './add-project-page';

@Component({ template: '' })
class ProjectsDestination {}

describe('AddProjectPage', () => {
  let harness: RouterTestingHarness;
  let host: HTMLElement;
  let registeredRequest: ProjectRegistrationRequest | null;
  let refreshedProjectId: string | null;
  let registrationResult: Observable<ProjectListItem>;

  beforeEach(async () => {
    registeredRequest = null;
    refreshedProjectId = null;
    registrationResult = of(PROJECT_REGISTRY_FIXTURE[0]);

    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: 'projects/new', component: AddProjectPage },
          { path: 'projects', component: ProjectsDestination },
        ]),
        {
          provide: ProjectRegistryDataSource,
          useValue: {
            load: () => of(PROJECT_REGISTRY_FIXTURE),
            register: (request: ProjectRegistrationRequest) => {
              registeredRequest = request;
              return registrationResult;
            },
            refreshProject: (projectId: string) => {
              refreshedProjectId = projectId;
              return of(null);
            },
          },
        },
        {
          provide: DashboardOverviewDataSource,
          useValue: { load: () => of(DASHBOARD_OVERVIEW_FIXTURE) },
        },
      ],
    });

    harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/projects/new', AddProjectPage);
    host = harness.routeNativeElement as HTMLElement;
  });

  async function type(selector: string, value: string): Promise<void> {
    const field = host.querySelector<HTMLInputElement>(selector);
    field!.value = value;
    field!.dispatchEvent(new Event('input'));
    harness.detectChanges();
  }

  function summary(): string {
    return host.querySelector('co-add-project-summary')?.textContent ?? '';
  }

  async function completeRequiredFields(): Promise<void> {
    await type('#project-name', 'Spinner API');
    await type('#project-repository', 'clint/spinner');
  }

  it('starts with nothing set instead of implying a configured project', () => {
    expect(summary()).toContain('Not set');
    expect(summary()).toContain('Complete the required fields to compose a valid registration.');
  });

  it('mirrors the persisted V1 configuration in the setup summary', async () => {
    await completeRequiredFields();
    await type('#workflow-file', 'deploy.yml');
    await type('#base-url', 'https://api.spinnerapp.com');
    await type('#health-endpoint', '/health');
    await type('#version-endpoint', '/version');

    expect(summary()).toContain('Spinner API');
    expect(summary()).toContain('clint/spinner (main)');
    expect(summary()).toContain('Production');
    expect(summary()).toContain('deploy.yml');
    expect(summary()).toContain('https://api.spinnerapp.com/health');
    expect(summary()).toContain('https://api.spinnerapp.com/version');
    expect(summary()).not.toContain('Complete the required fields');
  });

  it('rejects a repository that is not owner/name', async () => {
    await type('#project-repository', 'spinner');

    expect(host.textContent).toContain('Use the form owner/name');
    expect(summary()).toContain('Complete the required fields');
  });

  it('rejects a relative base URL and embedded credentials', async () => {
    await completeRequiredFields();
    await type('#base-url', 'api.spinnerapp.com');
    expect(host.textContent).toContain('Enter an absolute URL');

    await type('#base-url', 'https://user:secret@api.spinnerapp.com');
    expect(host.textContent).toContain('Remove the credentials from the URL.');
    expect(summary()).not.toContain('secret');
  });

  it('requires a base URL for a relative endpoint', async () => {
    await completeRequiredFields();
    await type('#health-endpoint', '/health');

    expect(host.textContent).toContain('Add a Base URL before using a relative endpoint.');
    expect(host.querySelector<HTMLButtonElement>('.primary')?.disabled).toBe(true);
  });

  it('keeps the environment name in step with the selected kind', async () => {
    const local = Array.from(host.querySelectorAll<HTMLButtonElement>('.segment')).find(
      (segment) => segment.textContent?.trim() === 'Local',
    );
    local!.click();
    harness.detectChanges();

    expect(host.querySelector<HTMLInputElement>('#environment-name')?.value).toBe('Local');
  });

  it('submits the exact API contract, refreshes observations, and returns to Projects', async () => {
    await completeRequiredFields();
    await type('#workflow-file', 'deploy.yml');
    await type('#base-url', 'https://api.spinnerapp.com');
    await type('#health-endpoint', '/health');
    await type('#version-endpoint', 'https://versions.spinnerapp.com/current');

    host.querySelector<HTMLButtonElement>('.primary')!.click();
    await harness.fixture.whenStable();

    expect(registeredRequest).toEqual({
      name: 'Spinner API',
      description: null,
      repository: {
        owner: 'clint',
        name: 'spinner',
        defaultBranch: 'main',
        workflowFile: 'deploy.yml',
      },
      environments: [
        {
          name: 'Production',
          kind: 'production',
          applicationUrl: 'https://api.spinnerapp.com',
          healthUrl: 'https://api.spinnerapp.com/health',
          versionUrl: 'https://versions.spinnerapp.com/current',
        },
      ],
    });
    expect(refreshedProjectId).toBe(PROJECT_REGISTRY_FIXTURE[0].id);
    expect(TestBed.inject(Router).url).toBe('/projects');
  });

  it('shows a safe duplicate message when registration is rejected', async () => {
    registrationResult = throwError(
      () => new HttpErrorResponse({ status: 409, statusText: 'Conflict' }),
    );
    await completeRequiredFields();

    host.querySelector<HTMLButtonElement>('.primary')!.click();
    harness.detectChanges();

    expect(host.querySelector('[role="alert"]')?.textContent).toContain(
      'name or repository is already registered',
    );
    expect(TestBed.inject(Router).url).toBe('/projects/new');
  });

  it('offers a working way back to the registry', () => {
    expect(host.querySelector<HTMLAnchorElement>('.cancel')?.getAttribute('href')).toBe(
      '/projects',
    );
  });
});
