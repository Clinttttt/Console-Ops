import { HttpErrorResponse } from '@angular/common/http';
import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { Observable, Subject, of, throwError } from 'rxjs';

import {
  EndpointVerification,
  EndpointVerificationRequest,
} from '../../core/contracts/endpoint-verification';
import {
  DetectedEndpoints,
  GitHubLatestCommit,
  GitHubRepository,
  GitHubRepositoryPage,
  GitHubWorkflow,
  GitHubWorkflowList,
} from '../../core/contracts/github-discovery';
import { ProjectRegistrationRequest } from '../../core/contracts/project-registration';
import { ProjectListItem } from '../../core/contracts/project-registry';
import { AzureDiscoveryDataSource } from '../../core/data/azure-discovery.data-source';
import { DashboardOverviewDataSource } from '../../core/data/dashboard-overview.data-source';
import { EndpointVerificationDataSource } from '../../core/data/endpoint-verification.data-source';
import { GitHubDiscoveryDataSource } from '../../core/data/github-discovery.data-source';
import { DASHBOARD_OVERVIEW_FIXTURE } from '../../core/data/mock/dashboard-overview.fixture';
import { PROJECT_REGISTRY_FIXTURE } from '../../core/data/mock/project-registry.fixture';
import { ProjectRegistryDataSource } from '../../core/data/project-registry.data-source';
import { AddProjectPage } from './add-project-page';

const SPINNER_REPOSITORY: GitHubRepository = {
  owner: 'clint',
  name: 'spinner',
  defaultBranch: 'main',
  isPrivate: true,
  language: 'C#',
  pushedAt: '2026-08-14T09:00:00Z',
  htmlUrl: 'https://github.com/clint/spinner',
};

const DEPLOY_WORKFLOW: GitHubWorkflow = {
  name: 'Deploy Production',
  path: '.github/workflows/deploy-production.yml',
  fileName: 'deploy-production.yml',
  active: true,
  latestRunConclusion: 'success',
  latestRunCompletedAt: '2026-08-14T08:00:00Z',
};

const HEALTHY_VERIFICATION: EndpointVerification = {
  health: { state: 'healthy', responseMilliseconds: 103, dependencies: [] },
  version: {
    state: 'available',
    application: 'Spinner.Api',
    version: '1.5.0',
    commitSha: '8a17c2f4e1b9d0a6c3f5e2b8d7a4c1f0e9b6d3a2',
    commitShortSha: '8a17c2f',
    builtAt: '2026-08-14T08:00:00Z',
  },
  observedAt: '2026-08-14T09:24:00Z',
};

const CI_WORKFLOW: GitHubWorkflow = {
  name: 'CI',
  path: '.github/workflows/ci.yml',
  fileName: 'ci.yml',
  active: true,
  latestRunConclusion: 'success',
  latestRunCompletedAt: '2026-08-14T08:50:00Z',
};

@Component({ template: '' })
class ProjectsDestination {}

describe('AddProjectPage', () => {
  let harness: RouterTestingHarness;
  let host: HTMLElement;
  let registeredRequest: ProjectRegistrationRequest | null;
  let refreshedProjectId: string | null;
  let refreshResult: Observable<unknown>;
  let registrationResult: Observable<ProjectListItem>;
  let repositoryResult: Observable<GitHubRepositoryPage>;
  let workflowResult: Observable<GitHubWorkflowList>;
  let verificationResult: Observable<EndpointVerification>;
  let commitResult: Observable<GitHubLatestCommit>;
  let detectionResult: Observable<DetectedEndpoints>;
  let verifiedRequest: EndpointVerificationRequest | null;

  beforeEach(async () => {
    registeredRequest = null;
    refreshedProjectId = null;
    refreshResult = of(null);
    verifiedRequest = null;
    registrationResult = of(PROJECT_REGISTRY_FIXTURE[0]);
    repositoryResult = of({ repositories: [SPINNER_REPOSITORY], hasMore: false });
    workflowResult = of({ workflows: [DEPLOY_WORKFLOW, CI_WORKFLOW] });
    verificationResult = of(HEALTHY_VERIFICATION);
    commitResult = of({
      commitSha: '8a17c2f4e1b9d0a6c3f5e2b8d7a4c1f0e9b6d3a2',
      commitShortSha: '8a17c2f',
      committedAt: '2026-08-14T08:00:00Z',
    });
    detectionResult = of({
      endpoints: [{ kind: 'health', path: '/health', sourceFile: 'src/Api/Program.cs' }],
      inspectedFileCount: 1,
    });

    TestBed.configureTestingModule({
      providers: [
        // The form renders the log-source picker, which asks Console Ops for Azure inventory only when
        // it is opened. A stub keeps these tests about the form.
        {
          provide: AzureDiscoveryDataSource,
          useValue: { listLogSources: () => of({ containerApps: [], hasMore: false }) },
        },
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
              return refreshResult;
            },
          },
        },
        {
          provide: DashboardOverviewDataSource,
          useValue: { load: () => of(DASHBOARD_OVERVIEW_FIXTURE) },
        },
        {
          provide: GitHubDiscoveryDataSource,
          useValue: {
            listRepositories: () => repositoryResult,
            listWorkflows: () => workflowResult,
            getLatestCommit: () => commitResult,
            detectEndpoints: () => detectionResult,
          },
        },
        {
          provide: EndpointVerificationDataSource,
          useValue: {
            verify: (request: EndpointVerificationRequest) => {
              verifiedRequest = request;
              return verificationResult;
            },
          },
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

  it('previews nothing until the configuration is valid', () => {
    expect(summary()).toContain('Enter a repository and project name to begin.');
    expect(summary()).not.toContain('Spinner API');
  });

  it('mirrors the persisted V1 configuration in the project preview', async () => {
    await completeRequiredFields();
    await type('#workflow-file', 'deploy.yml');
    await type('#base-url', 'https://api.spinnerapp.com');
    await type('#health-endpoint', '/health');
    await type('#version-endpoint', '/version');

    expect(summary()).toContain('Spinner API');
    expect(summary()).toContain('clint/spinner');
    expect(summary()).toContain('main');
    expect(summary()).toContain('Production');
    expect(summary()).toContain('deploy.yml');
    expect(summary()).toContain('https://api.spinnerapp.com/health');
    expect(summary()).toContain('https://api.spinnerapp.com/version');
    expect(summary()).not.toContain('Enter a repository');
  });

  it('rejects a repository that is not owner/name', async () => {
    await type('#project-repository', 'spinner');

    expect(host.textContent).toContain('Use the form owner/name');
    expect(summary()).toContain('Enter a repository and project name to begin.');
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

  it('derives the environment name from the kind without asking for it', async () => {
    const local = Array.from(host.querySelectorAll<HTMLButtonElement>('.segment')).find(
      (segment) => segment.textContent?.trim() === 'Local',
    );
    local!.click();
    harness.detectChanges();

    // No name input is shown at all: the kind already answered the question.
    expect(host.querySelector('#environment-name')).toBeNull();
    expect(host.querySelector('.derived-value')?.textContent?.trim()).toBe('Local');
  });

  it('lets the operator take over the environment name', async () => {
    host.querySelector<HTMLButtonElement>('.derived-action')!.click();
    harness.detectChanges();

    const field = host.querySelector<HTMLInputElement>('#environment-name');
    expect(field?.value).toBe('Production');

    await type('#environment-name', 'Production EU');
    const staging = Array.from(host.querySelectorAll<HTMLButtonElement>('.segment')).find(
      (segment) => segment.textContent?.trim() === 'Staging',
    );
    staging!.click();
    harness.detectChanges();

    // Once customized, changing the kind must not overwrite the operator's name.
    expect(host.querySelector<HTMLInputElement>('#environment-name')?.value).toBe('Production EU');
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
          // Left blank on the form, so no source is claimed.
          logSource: null,
        },
      ],
    });
    expect(refreshedProjectId).toBe(PROJECT_REGISTRY_FIXTURE[0].id);

    // The operator stays put and is told what happened, rather than landing on a list.
    const outcome = host.querySelector('co-registration-outcome');
    expect(outcome?.textContent).toContain('is registered');
    expect(outcome?.textContent).toContain('Project registered');
    expect(outcome?.textContent).toContain('Source connected');
    expect(outcome?.querySelector<HTMLAnchorElement>('.primary')?.getAttribute('href')).toBe(
      `/projects/${PROJECT_REGISTRY_FIXTURE[0].id}`,
    );
    expect(host.querySelector('form')).toBeNull();
  });

  it('says the initial observation did not run when the refresh fails', async () => {
    refreshResult = throwError(() => new HttpErrorResponse({ status: 500 }));
    await completeRequiredFields();

    host.querySelector<HTMLButtonElement>('.primary')!.click();
    await harness.fixture.whenStable();

    const outcome = host.querySelector('co-registration-outcome');
    // Registration is durable, so it still succeeded; the refresh is reported honestly.
    expect(outcome?.textContent).toContain('is registered');
    expect(outcome?.textContent).toContain('Could not run now');
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

  function checkButton(): HTMLButtonElement {
    return host.querySelector<HTMLButtonElement>('co-endpoint-monitoring .action')!;
  }

  function monitoring(): string {
    return host.querySelector('co-endpoint-monitoring')?.textContent ?? '';
  }

  it('states source sync only once both commits are observed', async () => {
    await importSpinner();
    await type('#base-url', 'https://api.spinnerapp.com');
    await type('#version-endpoint', '/version');

    // The source commit is known from the import, but nothing is deployed-known until a check runs.
    expect(monitoring()).not.toContain('Source sync');

    checkButton().click();
    harness.detectChanges();

    expect(monitoring()).toContain('Source sync');
    expect(monitoring()).toContain('In Sync');
  });

  it('reports differing commits without claiming a direction', async () => {
    commitResult = of({
      commitSha: '1111111111111111111111111111111111111111',
      commitShortSha: '1111111',
      committedAt: null,
    });

    await importSpinner();
    await type('#base-url', 'https://api.spinnerapp.com');
    await type('#version-endpoint', '/version');
    checkButton().click();
    harness.detectChanges();

    expect(monitoring()).toContain('Differs');
    // Ancestry is unknown before registration, so neither Behind nor Ahead may be claimed.
    expect(monitoring()).not.toContain('Behind');
    expect(monitoring()).toContain('Ancestry is known after the first refresh');
  });

  it('suggests detected endpoints without filling them in', async () => {
    await importSpinner();

    // Detection must not write to the fields on its own.
    expect(host.querySelector<HTMLInputElement>('#health-endpoint')?.value).toBe('');
    expect(host.textContent).toContain('Detected');
    expect(host.textContent).toContain('/health');
    expect(host.textContent).toContain('src/Api/Program.cs');

    const use = Array.from(host.querySelectorAll<HTMLButtonElement>('.suggestion-action'));
    use[0].click();
    harness.detectChanges();

    expect(host.querySelector<HTMLInputElement>('#health-endpoint')?.value).toBe('/health');
    // Once applied, the suggestion has nothing left to offer.
    expect(host.querySelectorAll('.suggestion-action').length).toBe(use.length - 1);
  });

  it('asks for the paths when detection finds nothing', async () => {
    detectionResult = of({ endpoints: [], inspectedFileCount: 3 });
    await importSpinner();

    expect(host.querySelector('.suggestion')).toBeNull();
    expect(host.querySelector('#health-endpoint')).not.toBeNull();
  });

  it('cannot check endpoints until there is something to probe', () => {
    expect(checkButton().disabled).toBe(true);
    expect(checkButton().title).toContain('Add a base URL');
  });

  it('sends absolute endpoint URLs and reports what was observed', async () => {
    await completeRequiredFields();
    await type('#base-url', 'https://api.spinnerapp.com');
    await type('#health-endpoint', '/health');
    await type('#version-endpoint', '/version');

    expect(checkButton().disabled).toBe(false);
    checkButton().click();
    harness.detectChanges();

    // Relative paths are resolved before they leave the browser, as registration does.
    expect(verifiedRequest).toEqual({
      healthUrl: 'https://api.spinnerapp.com/health',
      versionUrl: 'https://api.spinnerapp.com/version',
    });
    expect(monitoring()).toContain('Healthy');
    expect(monitoring()).toContain('103 ms');
    expect(monitoring()).toContain('1.5.0');
    expect(monitoring()).toContain('8a17c2f');
    expect(monitoring()).toContain('by the API rather than your browser');
  });

  it('reports an unreachable application without blocking registration', async () => {
    verificationResult = of({
      health: { state: 'unreachable', responseMilliseconds: 331, dependencies: [] },
      version: {
        state: 'notConfigured',
        application: null,
        version: null,
        commitSha: null,
        commitShortSha: null,
        builtAt: null,
      },
      observedAt: '2026-08-14T09:24:00Z',
    } satisfies EndpointVerification);

    await completeRequiredFields();
    await type('#base-url', 'https://api.spinnerapp.com');
    await type('#health-endpoint', '/health');
    checkButton().click();
    harness.detectChanges();

    expect(monitoring()).toContain('Unreachable');
    // The application may simply not be deployed yet, so registration stays available.
    expect(host.querySelector<HTMLButtonElement>('.primary')?.disabled).toBe(false);
  });

  it('explains a failed check without blaming the configuration', async () => {
    verificationResult = throwError(() => new HttpErrorResponse({ status: 429 }));

    await completeRequiredFields();
    await type('#base-url', 'https://api.spinnerapp.com');
    await type('#health-endpoint', '/health');
    checkButton().click();
    harness.detectChanges();

    expect(host.querySelector('[role="alert"]')?.textContent).toContain('Too many checks');
    expect(host.querySelector<HTMLButtonElement>('.primary')?.disabled).toBe(false);
  });

  async function importSpinner(): Promise<void> {
    host.querySelector<HTMLButtonElement>('.import-action')!.click();
    harness.detectChanges();
    await harness.fixture.whenStable();

    host.querySelector<HTMLButtonElement>('.result')!.click();
    harness.detectChanges();
    await harness.fixture.whenStable();
  }

  it('takes repository, branch and name from the imported repository', async () => {
    await importSpinner();

    // The manual repository and branch inputs are replaced by the discovered facts.
    expect(host.querySelector('#project-repository')).toBeNull();
    expect(host.querySelector('#default-branch')).toBeNull();
    expect(host.querySelector('.discovered-name')?.textContent?.trim()).toBe('clint/spinner');
    expect(host.querySelector('.discovered-meta')?.textContent).toContain('main');
    expect(host.querySelector<HTMLInputElement>('#project-name')?.value).toBe('spinner');
  });

  it('keeps a project name the operator already typed', async () => {
    await type('#project-name', 'Spinner API');
    await importSpinner();

    expect(host.querySelector<HTMLInputElement>('#project-name')?.value).toBe('Spinner API');
  });

  it('suggests a deployment workflow without selecting it', async () => {
    await importSpinner();

    const options = Array.from(host.querySelectorAll('co-workflow-selector .option'));
    // Suggested first, then CI, then the explicit opt-out.
    expect(options.length).toBe(3);
    expect(options[0].textContent).toContain('Deploy Production');
    expect(options[0].textContent).toContain('Suggested');
    expect(options[0].textContent).toContain('Passed');

    // Nothing is chosen for the operator, not even "no deployment workflow".
    expect(host.querySelectorAll('.option.is-selected').length).toBe(0);
  });

  it('collapses to the chosen workflow and can be reopened', async () => {
    await importSpinner();

    host.querySelector<HTMLInputElement>('co-workflow-selector .option input')!.click();
    harness.detectChanges();

    // The list gives way to the confirmed choice.
    expect(host.querySelectorAll('co-workflow-selector .option').length).toBe(0);
    const chosen = host.querySelector('co-workflow-selector .chosen');
    expect(chosen?.textContent).toContain('Deploy Production');
    expect(chosen?.textContent).toContain('deploy-production.yml');

    host.querySelector<HTMLButtonElement>('.change')!.click();
    harness.detectChanges();

    expect(host.querySelectorAll('co-workflow-selector .option').length).toBe(3);
    expect(host.querySelector('.option.is-selected')?.textContent).toContain('Deploy Production');
  });

  it('collapses on an explicit choice of no workflow', async () => {
    await importSpinner();

    const inputs = host.querySelectorAll<HTMLInputElement>('co-workflow-selector .option input');
    inputs[inputs.length - 1].click();
    harness.detectChanges();

    const chosen = host.querySelector('co-workflow-selector .chosen');
    expect(chosen?.textContent).toContain('No deployment workflow');
    expect(chosen?.textContent).toContain('notConfigured');
  });

  it('submits the workflow file the operator confirmed', async () => {
    await type('#project-name', 'Spinner API');
    await importSpinner();

    host.querySelector<HTMLInputElement>('co-workflow-selector .option input')!.click();
    harness.detectChanges();

    host.querySelector<HTMLButtonElement>('.primary')!.click();
    await harness.fixture.whenStable();

    expect(registeredRequest?.repository).toEqual({
      owner: 'clint',
      name: 'spinner',
      defaultBranch: 'main',
      workflowFile: 'deploy-production.yml',
    });
  });

  it('reports that it is reading workflows instead of looking frozen', async () => {
    const workflows = new Subject<GitHubWorkflowList>();
    workflowResult = workflows;

    await importSpinner();

    expect(host.querySelector('.reading')?.textContent).toContain('Reading workflows from GitHub');
    expect(host.querySelector('.reading')?.getAttribute('aria-busy')).toBe('true');
    // The manual field is not offered yet: discovery has not failed, it is still running.
    expect(host.querySelector('#workflow-file')).toBeNull();

    workflows.next({ workflows: [DEPLOY_WORKFLOW] });
    workflows.complete();
    harness.detectChanges();

    expect(host.querySelector('.reading')).toBeNull();
    expect(host.querySelector('co-workflow-selector')).not.toBeNull();
  });

  it('falls back to manual entry when repository discovery is unavailable', async () => {
    repositoryResult = throwError(
      () => new HttpErrorResponse({ status: 500, error: { code: 'GitHub.Unauthorized' } }),
    );

    host.querySelector<HTMLButtonElement>('.import-action')!.click();
    harness.detectChanges();
    await harness.fixture.whenStable();

    expect(host.textContent).toContain('Console Ops has no working GitHub credential.');
    expect(host.textContent).toContain('GitHub:Token');
    // The manual field is still there, so registration is never blocked by discovery.
    expect(host.querySelector('#project-repository')).not.toBeNull();
  });

  it('names a missing discovery endpoint rather than blaming the credential', async () => {
    repositoryResult = throwError(() => new HttpErrorResponse({ status: 404 }));

    host.querySelector<HTMLButtonElement>('.import-action')!.click();
    harness.detectChanges();
    await harness.fixture.whenStable();

    expect(host.textContent).toContain('Repository discovery is unavailable.');
    expect(host.textContent).toContain('/api/github/repositories');
  });

  it('asks for the workflow file when workflow discovery is unavailable', async () => {
    workflowResult = throwError(() => new HttpErrorResponse({ status: 404 }));
    await importSpinner();

    expect(host.querySelector('co-workflow-selector')).toBeNull();
    expect(host.querySelector('#workflow-file')).not.toBeNull();
    expect(host.textContent).toContain('Workflow discovery is unavailable');
  });
});
