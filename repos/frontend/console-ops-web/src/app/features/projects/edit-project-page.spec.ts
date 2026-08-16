import { HttpErrorResponse } from '@angular/common/http';
import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { Observable, of, throwError } from 'rxjs';

import { ProjectListItem } from '../../core/contracts/project-registry';
import { ProjectUpdateRequest } from '../../core/contracts/project-update';
import { AzureDiscoveryDataSource } from '../../core/data/azure-discovery.data-source';
import { PROJECT_REGISTRY_FIXTURE } from '../../core/data/mock/project-registry.fixture';
import { ProjectRegistryDataSource } from '../../core/data/project-registry.data-source';
import { EditProjectPage } from './edit-project-page';

@Component({ template: '' })
class Destination {}

describe('EditProjectPage', () => {
  let harness: RouterTestingHarness;
  let host: HTMLElement;
  let updateRequest: ProjectUpdateRequest | null;
  let archivedProjectId: string | null;
  let updateResult: Observable<ProjectListItem>;
  let archiveResult: Observable<unknown>;

  const project = PROJECT_REGISTRY_FIXTURE[0];

  beforeEach(async () => {
    updateRequest = null;
    archivedProjectId = null;
    updateResult = of(project);
    archiveResult = of(null);

    TestBed.configureTestingModule({
      providers: [
        // The form renders the log-source picker, which asks Console Ops for Azure inventory only when
        // it is opened. A stub keeps these tests about the form.
        {
          provide: AzureDiscoveryDataSource,
          useValue: { listLogSources: () => of({ containerApps: [], hasMore: false }) },
        },
        provideRouter([
          { path: 'projects/:projectId/edit', component: EditProjectPage },
          { path: 'projects/:projectId', component: Destination },
          { path: 'projects', component: Destination },
        ]),
        {
          provide: ProjectRegistryDataSource,
          useValue: {
            load: () => of(PROJECT_REGISTRY_FIXTURE),
            getProject: () => of(project),
            register: () => of(project),
            updateProject: (_projectId: string, request: ProjectUpdateRequest) => {
              updateRequest = request;
              return updateResult;
            },
            archiveProject: (projectId: string) => {
              archivedProjectId = projectId;
              return archiveResult;
            },
            refreshProject: () => of(null),
          },
        },
      ],
    });

    harness = await RouterTestingHarness.create();
    await harness.navigateByUrl(`/projects/${project.id}/edit`, EditProjectPage);
    host = harness.routeNativeElement as HTMLElement;
  });

  function value(selector: string): string | undefined {
    return host.querySelector<HTMLInputElement>(selector)?.value;
  }

  async function type(selector: string, text: string): Promise<void> {
    const field = host.querySelector<HTMLInputElement>(selector);
    field!.value = text;
    field!.dispatchEvent(new Event('input'));
    harness.detectChanges();
  }

  it('loads the registered configuration into the form', () => {
    expect(value('#project-name')).toBe(project.name);
    expect(value('#repository-owner')).toBe(project.repository.owner);
    expect(value('#repository-name')).toBe(project.repository.name);
    expect(value('#default-branch')).toBe(project.repository.defaultBranch);
    expect(value('#environment-name-0')).toBe(project.environments[0].name);
  });

  it('sends the complete configuration with the version it loaded', async () => {
    await type('#project-name', 'Spinner API v2');

    host.querySelector<HTMLButtonElement>('.primary')!.click();
    await harness.fixture.whenStable();

    expect(updateRequest).toEqual({
      configurationVersion: project.configurationVersion,
      name: 'Spinner API v2',
      description: project.description,
      repository: {
        owner: project.repository.owner,
        name: project.repository.name,
        defaultBranch: project.repository.defaultBranch,
        workflowFile: project.repository.workflowFile,
      },
      environments: [
        {
          id: project.environments[0].id,
          name: project.environments[0].name,
          kind: project.environments[0].kind,
          applicationUrl: project.environments[0].applicationUrl,
          healthUrl: project.environments[0].healthUrl,
          versionUrl: project.environments[0].versionUrl,
          logSource: null,
        },
      ],
    });
    expect(TestBed.inject(Router).url).toBe(`/projects/${project.id}`);
  });

  it('keeps existing environment ids so the API can match them', async () => {
    await type('#environment-name-0', 'Production EU');

    host.querySelector<HTMLButtonElement>('.primary')!.click();
    await harness.fixture.whenStable();

    expect(updateRequest!.environments[0].id).toBe(project.environments[0].id);
    expect(updateRequest!.environments[0].name).toBe('Production EU');
  });

  it('refuses a name that has been emptied', async () => {
    await type('#project-name', '   ');

    expect(host.textContent).toContain('A project name is required.');
    expect(host.querySelector<HTMLButtonElement>('.primary')?.disabled).toBe(true);
  });

  it('refuses credentials in an environment URL', async () => {
    await type('#environment-health-0', 'https://user:secret@api.example.com/health');

    expect(host.textContent).toContain('Remove the credentials from the URL.');
    expect(host.querySelector<HTMLButtonElement>('.primary')?.disabled).toBe(true);
  });

  it('adds an environment without an id so the API creates it', async () => {
    host.querySelector<HTMLButtonElement>('.add-environment')!.click();
    harness.detectChanges();

    await type('#environment-name-1', 'Staging');

    host.querySelector<HTMLButtonElement>('.primary')!.click();
    await harness.fixture.whenStable();

    expect(updateRequest!.environments.length).toBe(2);
    expect(updateRequest!.environments[0].id).toBe(project.environments[0].id);
    // A new environment must not carry an id at all, or the API would try to match one.
    expect('id' in updateRequest!.environments[1]).toBe(false);
    expect(updateRequest!.environments[1].name).toBe('Staging');
    expect(updateRequest!.environments[1].kind).toBe('staging');
  });

  it('refuses two environments with the same name', async () => {
    host.querySelector<HTMLButtonElement>('.add-environment')!.click();
    harness.detectChanges();

    await type('#environment-name-1', project.environments[0].name);

    expect(host.textContent).toContain('Environment names must be unique');
    expect(host.querySelector<HTMLButtonElement>('.primary')?.disabled).toBe(true);
  });

  it('will not remove the only environment a project has', () => {
    const remove = host.querySelector<HTMLButtonElement>('.remove');

    expect(remove?.disabled).toBe(true);
    expect(remove?.title).toContain('at least one environment');
  });

  it('asks before removing an environment that exists', async () => {
    host.querySelector<HTMLButtonElement>('.add-environment')!.click();
    harness.detectChanges();
    await type('#environment-name-1', 'Staging');

    // The first press on a saved environment asks rather than removing.
    host.querySelectorAll<HTMLButtonElement>('.remove')[0].click();
    harness.detectChanges();
    expect(host.querySelector('.confirm-text')?.textContent).toContain(
      'observations are discarded',
    );
    expect(host.querySelectorAll('.environment').length).toBe(2);

    host.querySelector<HTMLButtonElement>('.environment-head .danger')!.click();
    harness.detectChanges();

    expect(host.querySelectorAll('.environment').length).toBe(1);
  });

  it('drops an unsaved environment immediately', async () => {
    host.querySelector<HTMLButtonElement>('.add-environment')!.click();
    harness.detectChanges();
    expect(host.querySelectorAll('.environment').length).toBe(2);

    // Nothing was saved for it, so there is nothing to warn about.
    host.querySelectorAll<HTMLButtonElement>('.remove')[1].click();
    harness.detectChanges();

    expect(host.querySelectorAll('.environment').length).toBe(1);
    expect(host.querySelector('.confirm-text')).toBeNull();
  });

  it('explains a stale configuration version instead of retrying', async () => {
    updateResult = throwError(() => new HttpErrorResponse({ status: 409 }));

    host.querySelector<HTMLButtonElement>('.primary')!.click();
    await harness.fixture.whenStable();

    expect(host.querySelector('[role="alert"]')?.textContent).toContain('changed since you opened');
    expect(TestBed.inject(Router).url).toBe(`/projects/${project.id}/edit`);
  });

  it('archives only after a second, explicit confirmation', async () => {
    host.querySelector<HTMLButtonElement>('.danger')!.click();
    harness.detectChanges();

    // The first press asks rather than acts.
    expect(archivedProjectId).toBeNull();
    expect(host.querySelector('.confirm-text')?.textContent).toContain(project.name);

    const buttons = Array.from(host.querySelectorAll<HTMLButtonElement>('.danger'));
    buttons[buttons.length - 1].click();
    await harness.fixture.whenStable();

    expect(archivedProjectId).toBe(project.id);
    expect(TestBed.inject(Router).url).toBe('/projects');
  });

  it('can back out of archiving', () => {
    host.querySelector<HTMLButtonElement>('.danger')!.click();
    harness.detectChanges();

    host.querySelector<HTMLButtonElement>('.cancel-archive')!.click();
    harness.detectChanges();

    expect(archivedProjectId).toBeNull();
    expect(host.querySelector('.confirm-text')).toBeNull();
  });

  it('reports a failed archive without leaving the page', async () => {
    archiveResult = throwError(() => new HttpErrorResponse({ status: 500 }));

    host.querySelector<HTMLButtonElement>('.danger')!.click();
    harness.detectChanges();
    const buttons = Array.from(host.querySelectorAll<HTMLButtonElement>('.danger'));
    buttons[buttons.length - 1].click();
    await harness.fixture.whenStable();

    expect(host.querySelector('[role="alert"]')?.textContent).toContain('could not be archived');
    expect(TestBed.inject(Router).url).toBe(`/projects/${project.id}/edit`);
  });
});
