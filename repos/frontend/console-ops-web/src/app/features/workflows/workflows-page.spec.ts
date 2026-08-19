import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Observable, of, throwError } from 'rxjs';

import {
  ManualRunSupportReading,
  WorkflowInventory,
  WorkflowRunHistory,
  WorkflowRunJob,
} from '../../core/contracts/workflows';
import { WorkflowsDataSource } from '../../core/data/workflows.data-source';
import { WorkflowsPage } from './workflows-page';

const READ_AT = '2026-08-19T07:05:00.000Z';

/**
 * One inventory covering what the screen must get right: a configured deployment mid-run, a workflow that has
 * never run, one whose dispatch support is unknown, a failed run, a disabled workflow, and a second project.
 */
const INVENTORY: WorkflowInventory = {
  isSampleData: false,
  readAt: READ_AT,
  groups: [
    {
      projectId: 'eemo',
      projectName: 'EEMO-Cantilan-SDS',
      repository: 'clint/EEMO-Cantilan-SDS',
      readFailure: null,
      workflows: [
        {
          id: '101',
          name: 'Deploy production',
          path: '.github/workflows/deploy-production.yml',
          state: 'active',
          classification: 'deployment',
          manualRun: 'supported',
          latestRun: {
            id: '535',
            number: 535,
            status: 'inProgress',
            conclusion: null,
            branch: 'master',
            commitSha: '2ac8bf0f4c1e9d7a3b5c8e2f1a4d6b9c0e3f7a21',
            commitShortSha: '2ac8bf0',
            trigger: 'push',
            actor: 'Clinttttt',
            startedAt: '2026-08-19T06:57:00.000Z',
            completedAt: null,
            durationSeconds: null,
            runUrl: 'https://github.test/run/535',
            jobs: [],
          },
        },
        {
          id: '202',
          name: 'Database backup',
          path: '.github/workflows/database-backup.yml',
          state: 'active',
          classification: 'unclassified',
          manualRun: 'unknown',
          latestRun: {
            id: '212',
            number: 212,
            status: 'completed',
            conclusion: 'failed',
            branch: 'master',
            commitSha: '89ad1c2e7f4b0d9a3c6e2f8b1d5a7c4e0f3b9d62',
            commitShortSha: '89ad1c2',
            trigger: 'schedule',
            actor: null,
            startedAt: '2026-08-19T02:00:00.000Z',
            completedAt: '2026-08-19T02:03:41.000Z',
            durationSeconds: 221,
            runUrl: null,
            jobs: [],
          },
        },
        {
          id: '303',
          name: 'Database restore',
          path: '.github/workflows/database-restore.yml',
          state: 'active',
          classification: 'unclassified',
          manualRun: 'unknown',
          latestRun: null,
        },
      ],
    },
    {
      projectId: 'spinner',
      projectName: 'Spinner API',
      repository: 'clint/Spinner',
      readFailure: null,
      workflows: [
        {
          id: '404',
          name: 'Security scan',
          path: '.github/workflows/security-scan.yml',
          state: 'disabled',
          classification: 'unclassified',
          manualRun: 'unavailable',
          latestRun: null,
        },
      ],
    },
  ],
};

const JOBS: readonly WorkflowRunJob[] = [
  {
    name: 'Prepare',
    status: 'completed',
    conclusion: 'passed',
    durationSeconds: 72,
    failedStep: null,
    steps: [],
  },
  {
    name: 'Deploy',
    status: 'inProgress',
    conclusion: null,
    durationSeconds: null,
    failedStep: null,
    steps: [],
  },
];

class StubWorkflows extends WorkflowsDataSource {
  jobRequests: { projectId: string; runId: string }[] = [];
  manualRunRequests: { projectId: string; workflowId: string; workflowPath: string }[] = [];

  constructor(
    private readonly inventory: Observable<WorkflowInventory> = of(INVENTORY),
    private readonly jobs: Observable<readonly WorkflowRunJob[]> = of(JOBS),
    private readonly manualRun: Observable<ManualRunSupportReading> = of({
      manualRun: 'supported' as const,
      definitionPath: '.github/workflows/deploy-production.yml',
    }),
  ) {
    super();
  }

  override load(): Observable<WorkflowInventory> {
    return this.inventory;
  }

  override loadRunJobs(projectId: string, runId: string): Observable<readonly WorkflowRunJob[]> {
    this.jobRequests.push({ projectId, runId });
    return this.jobs;
  }

  override loadRuns(_projectId: string, workflowId: string): Observable<WorkflowRunHistory> {
    return of({ workflowId, runs: [], hasMore: false });
  }

  override loadManualRunSupport(
    projectId: string,
    workflowId: string,
    workflowPath: string,
  ): Observable<ManualRunSupportReading> {
    this.manualRunRequests.push({ projectId, workflowId, workflowPath });
    return this.manualRun;
  }
}

describe('WorkflowsPage', () => {
  let fixture: ComponentFixture<WorkflowsPage>;
  let host: HTMLElement;
  let dataSource: StubWorkflows;

  async function render(stub: StubWorkflows = new StubWorkflows()): Promise<void> {
    dataSource = stub;
    TestBed.resetTestingModule();
    await TestBed.configureTestingModule({
      imports: [WorkflowsPage],
      providers: [
        // The rows link to run history, so the router has to exist for them to render.
        provideRouter([]),
        { provide: WorkflowsDataSource, useValue: stub },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(WorkflowsPage);
    await fixture.whenStable();
    host = fixture.nativeElement as HTMLElement;
  }

  function rowFor(name: string): Element | undefined {
    return Array.from(host.querySelectorAll('co-workflow-row')).find(
      (row) => row.querySelector('.name')?.textContent?.trim() === name,
    );
  }

  async function select(name: string): Promise<void> {
    rowFor(name)!.querySelector<HTMLButtonElement>('.pick')!.click();
    await fixture.whenStable();
  }

  beforeEach(async () => {
    await render();
  });

  it('reads real workflows and no longer claims to be sample data', () => {
    expect(host.textContent).not.toContain('Sample data');
    expect(host.querySelectorAll('co-workflow-row').length).toBe(4);
    expect(host.textContent).toContain('Showing 4 of 4 workflows');
  });

  it('groups workflows under the project that owns them', () => {
    const groups = Array.from(host.querySelectorAll('.group'));

    expect(groups.length).toBe(2);
    expect(groups[0].textContent).toContain('EEMO-Cantilan-SDS');
    expect(groups[0].textContent).toContain('3 workflows');
  });

  it('calls a workflow a deployment only where one was configured, and names no environment', () => {
    // Console Ops records the workflow against the project, so claiming an environment would invent the link.
    expect(rowFor('Deploy production')?.textContent).toContain('Primary deployment');
    expect(rowFor('Deploy production')?.textContent).not.toContain('·');

    expect(rowFor('Database backup')?.textContent).toContain('Unclassified');
    expect(rowFor('Database backup')?.textContent).not.toContain('Primary deployment');
  });

  it('reports a workflow that never ran as having no run, not as a failure', () => {
    expect(rowFor('Database restore')?.textContent).toContain('No run recorded');
    expect(rowFor('Database restore')?.textContent).not.toContain('Failed');
  });

  it('does not describe a running run by an outcome it has not reached', () => {
    expect(rowFor('Deploy production')?.textContent).toContain('In progress');
    expect(rowFor('Deploy production')?.textContent).not.toContain('Passed');
  });

  it('reports a disabled workflow as disabled rather than as failed', () => {
    expect(rowFor('Security scan')?.textContent).toContain('Disabled at the provider');
    expect(rowFor('Security scan')?.textContent).not.toContain('Failed');
  });

  it('says nothing about a manual run until the definition has been read', () => {
    // The inventory cannot know: a dispatch trigger is declared in the definition, not in the listing. Stating
    // "unknown" on every row would repeat the same non-answer once per workflow.
    expect(host.textContent).not.toContain('Run unknown');
    expect(dataSource.manualRunRequests).toEqual([]);
  });

  it('establishes manual dispatch from the definition when a workflow is selected', async () => {
    await select('Deploy production');

    expect(dataSource.manualRunRequests).toEqual([
      {
        projectId: 'eemo',
        workflowId: '101',
        workflowPath: '.github/workflows/deploy-production.yml',
      },
    ]);
    expect(host.querySelector('co-workflow-detail')?.textContent).toContain('Supported');

    // A definition already read is not asked for again while the screen is open.
    await select('Database backup');
    await select('Deploy production');
    expect(
      dataSource.manualRunRequests.filter((request) => request.workflowId === '101').length,
    ).toBe(1);
  });

  it('leaves a failed definition read unknown rather than calling it unavailable', async () => {
    await render(
      new StubWorkflows(
        of(INVENTORY),
        of(JOBS),
        throwError(() => new Error('rate limited')),
      ),
    );
    await select('Deploy production');

    const detail = host.querySelector('co-workflow-detail')!;
    expect(detail.textContent).toContain('Unknown');
    expect(detail.textContent).not.toContain('Not available');
  });

  it('offers a run action only where the provider reported manual dispatch', () => {
    const deploy = rowFor('Deploy production')!.querySelector('.run');
    expect(deploy?.textContent?.trim()).toBe('Run');
    expect(deploy?.classList.contains('is-unavailable')).toBe(true);

    // Reported unavailable: no action at all rather than a button that cannot work.
    expect(rowFor('Security scan')!.querySelector('.run')).toBeNull();
    // Not established: no action and no claim either way until the definition is read.
    expect(rowFor('Database backup')!.querySelector('.run')).toBeNull();
  });

  it('reads the jobs of the selected run only, and once', async () => {
    expect(dataSource.jobRequests).toEqual([]);

    await select('Deploy production');
    expect(dataSource.jobRequests).toEqual([{ projectId: 'eemo', runId: '535' }]);

    const detail = host.querySelector('co-workflow-detail')!;
    expect(detail.textContent).toContain('#535');
    expect(detail.textContent).toContain('Still running');

    const jobs = Array.from(detail.querySelectorAll('.job'));
    expect(jobs.length).toBe(2);
    expect(jobs[0].textContent).toContain('Prepare');
    expect(jobs[0].textContent).toContain('1m 12s');
    expect(jobs[1].textContent).toContain('In progress');

    // Selecting it again does not ask the provider a second time for a run already read.
    await select('Database backup');
    await select('Deploy production');
    expect(dataSource.jobRequests.filter((request) => request.runId === '535').length).toBe(1);
  });

  it('asks for nothing when a workflow has no run to inspect', async () => {
    await select('Database restore');

    expect(dataSource.jobRequests).toEqual([]);
    expect(host.querySelector('co-workflow-detail')?.textContent).toContain('No run recorded');
  });

  it('names jobs that could not be read rather than showing a run without any', async () => {
    await render(
      new StubWorkflows(
        of(INVENTORY),
        throwError(() => new Error('rate limited')),
      ),
    );
    await select('Deploy production');

    expect(host.querySelector('co-workflow-detail')?.textContent).toContain(
      'jobs of this run could not be read',
    );
  });

  it('reports an unreadable repository as such rather than as one with no automation', async () => {
    await render(
      new StubWorkflows(
        of({
          isSampleData: false,
          readAt: READ_AT,
          groups: [
            {
              projectId: 'eemo',
              projectName: 'EEMO-Cantilan-SDS',
              repository: 'clint/EEMO-Cantilan-SDS',
              workflows: [],
              readFailure: 'unauthorized' as const,
            },
          ],
        }),
      ),
    );

    expect(host.textContent).toContain('GitHub rejected the configured token');
    expect(host.textContent).not.toContain('No workflows found');
  });

  it('narrows to the workflows a filter matches and says how many that is', async () => {
    const deployment = Array.from(host.querySelectorAll<HTMLButtonElement>('.filter')).find(
      (button) => button.textContent?.trim() === 'Deployment',
    )!;
    deployment.click();
    await fixture.whenStable();

    expect(host.querySelectorAll('co-workflow-row').length).toBe(1);
    expect(host.textContent).toContain('Showing 1 of 4 workflows');
  });

  it('searches by workflow name and offers a way back when nothing matches', async () => {
    const search = host.querySelector<HTMLInputElement>('#workflow-search')!;
    search.value = 'restore';
    search.dispatchEvent(new Event('input'));
    await fixture.whenStable();

    expect(host.querySelectorAll('co-workflow-row').length).toBe(1);

    search.value = 'nothing matches this';
    search.dispatchEvent(new Event('input'));
    await fixture.whenStable();

    expect(host.textContent).toContain('No workflow matches these filters');
  });

  it('says the inventory could not be read instead of showing an empty repository', async () => {
    await render(new StubWorkflows(throwError(() => new Error('unreachable'))));

    expect(host.textContent).toContain('Workflow inventory unavailable');
    expect(host.textContent).not.toContain('No workflows found');
  });

  it('reports a repository with no workflows as empty rather than as unavailable', async () => {
    await render(new StubWorkflows(of({ isSampleData: false, readAt: READ_AT, groups: [] })));

    expect(host.textContent).toContain('No workflows found');
  });
});
