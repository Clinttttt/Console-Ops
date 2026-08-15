import { HttpErrorResponse } from '@angular/common/http';
import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { Observable, of, throwError } from 'rxjs';

import { ProjectListItem } from '../../core/contracts/project-registry';
import { ProjectUpdateRequest } from '../../core/contracts/project-update';
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
