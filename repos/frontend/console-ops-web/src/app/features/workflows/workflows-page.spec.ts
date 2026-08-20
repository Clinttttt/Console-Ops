import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Observable, Subject, of, throwError } from 'rxjs';

import {
  ManualRunSupportReading,
  WorkflowBranches,
  WorkflowDispatchAccepted,
  WorkflowRiskLevel,
  WorkflowRiskReading,
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
      defaultBranch: 'master',
      readFailure: null,
      workflows: [
        {
          id: '101',
          name: 'Deploy production',
          path: '.github/workflows/deploy-production.yml',
          state: 'active',
          classification: 'deployment',
          manualRun: 'supported',
          risk: 'normal',
          riskDecidedAt: '2026-08-19T06:00:00.000Z',
          executable: true,
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
          risk: 'unclassified',
          riskDecidedAt: null,
          executable: false,
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
          risk: 'destructive',
          riskDecidedAt: '2026-08-19T05:00:00.000Z',
          executable: true,
          latestRun: null,
        },
      ],
    },
    {
      projectId: 'spinner',
      projectName: 'Spinner API',
      repository: 'clint/Spinner',
      defaultBranch: 'main',
      readFailure: null,
      workflows: [
        {
          id: '404',
          name: 'Security scan',
          path: '.github/workflows/security-scan.yml',
          state: 'disabled',
          classification: 'unclassified',
          manualRun: 'unavailable',
          risk: 'unclassified',
          riskDecidedAt: null,
          executable: false,
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
  riskWrites: { projectId: string; workflowPath: string; level: string }[] = [];
  dispatches: {
    projectId: string;
    workflowId: string;
    reference: string;
    inputs: Readonly<Record<string, string>>;
    confirmation: string | null;
  }[] = [];

  constructor(
    private readonly inventory: Observable<WorkflowInventory> = of(INVENTORY),
    private readonly jobs: Observable<readonly WorkflowRunJob[]> = of(JOBS),
    private readonly branches: Observable<WorkflowBranches> = of({
      defaultBranch: 'master',
      branches: ['master', 'release/2026-08'],
      hasMore: false,
    }),
    private readonly manualRun: Observable<ManualRunSupportReading> = of({
      manualRun: 'supported' as const,
      definitionPath: '.github/workflows/deploy-production.yml',
      inputs: [],
    }),
    private readonly dispatchResult: Observable<WorkflowDispatchAccepted> | null = null,
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

  override loadBranches(): Observable<WorkflowBranches> {
    return this.branches;
  }

  override dispatch(
    projectId: string,
    workflowId: string,
    request: {
      reference: string;
      inputs: Readonly<Record<string, string>>;
      confirmation: string | null;
    },
  ): Observable<WorkflowDispatchAccepted> {
    this.dispatches.push({ projectId, workflowId, ...request });
    return (
      this.dispatchResult ??
      of({
        status: 'requested' as const,
        workflowId,
        reference: request.reference,
        requestedAt: '2026-08-19T07:06:00.000Z',
      })
    );
  }

  /** Replaceable so a test can hold the write open and watch what the screen says meanwhile. */
  riskResult: Observable<WorkflowRiskReading> | null = null;

  override setRisk(
    projectId: string,
    workflowPath: string,
    level: WorkflowRiskLevel,
  ): Observable<WorkflowRiskReading> {
    this.riskWrites.push({ projectId, workflowPath, level });
    return this.riskResult ?? of({ workflowPath, level, decidedAt: '2026-08-19T07:06:00.000Z' });
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
        of({ defaultBranch: 'master', branches: ['master'], hasMore: false }),
        throwError(() => new Error('rate limited')),
      ),
    );
    await select('Deploy production');

    const detail = host.querySelector('co-workflow-detail')!;
    expect(detail.textContent).toContain('Unknown');
    expect(detail.textContent).not.toContain('Not available');
  });

  it('will not offer to run a workflow whose risk nobody has marked', async () => {
    await select('Database backup');
    const detail = host.querySelector('co-workflow-detail')!;

    expect(detail.textContent).toContain('will not run this until its risk is marked');
    // The marking control is the only way forward, and the API decided this workflow is not executable.
    expect(detail.querySelectorAll('.risk-option').length).toBe(3);
  });

  it('says what running a marked workflow will ask for, before it is asked', async () => {
    await select('Deploy production');
    expect(host.querySelector('co-workflow-detail')?.textContent).toContain(
      'confirmation naming the workflow and branch',
    );

    await select('Database restore');
    // Marked destructive and dispatchable, so the stronger confirmation is stated before it is asked for.
    expect(host.querySelector('co-workflow-detail')?.textContent).toContain(
      "ask for the workflow's name to be typed",
    );
  });

  it('records a risk marking against the workflow path an operator can recognise', async () => {
    await select('Database backup');
    const destructive = Array.from(host.querySelectorAll<HTMLButtonElement>('.risk-option')).find(
      (option) => option.textContent?.trim() === 'Destructive',
    )!;

    destructive.click();
    await fixture.whenStable();

    expect(dataSource.riskWrites).toEqual([
      {
        projectId: 'eemo',
        workflowPath: '.github/workflows/database-backup.yml',
        level: 'destructive',
      },
    ]);
  });

  it('shows which marking is in force', async () => {
    await select('Deploy production');

    const active = Array.from(host.querySelectorAll('.risk-option')).find((option) =>
      option.classList.contains('is-active'),
    );
    expect(active?.textContent?.trim()).toBe('Normal');
    expect(host.querySelector('co-workflow-detail')?.textContent).toContain('Marked ');
  });

  it('shows a marking as soon as the API records it, without blocking the choice', async () => {
    const inventory = new Subject<WorkflowInventory>();
    const risk = new Subject<WorkflowRiskReading>();
    await render(new StubWorkflows(inventory.asObservable()));
    inventory.next(INVENTORY);
    await fixture.whenStable();

    dataSource.riskResult = risk.asObservable();
    await select('Database backup');
    const destructive = Array.from(host.querySelectorAll<HTMLButtonElement>('.risk-option')).find(
      (option) => option.textContent?.trim() === 'Destructive',
    )!;
    destructive.click();
    await fixture.whenStable();

    expect(host.querySelector('co-workflow-detail')?.textContent).toContain('Saving the marking');

    // The write returns, and the marking shows immediately from what the API recorded - without waiting for a
    // provider read, which is what made the choice unclickable and put the old refusal back on screen.
    risk.next({
      workflowPath: '.github/workflows/database-backup.yml',
      level: 'destructive',
      decidedAt: '2026-08-20T09:00:00.000Z',
    });
    risk.complete();
    await fixture.whenStable();

    const detail = host.querySelector('co-workflow-detail')!;
    expect(detail.textContent).not.toContain('Saving the marking');
    expect(detail.textContent).not.toContain('will not run this until its risk is marked');
    expect(
      Array.from(detail.querySelectorAll<HTMLButtonElement>('.risk-option')).every(
        (option) => !option.disabled,
      ),
    ).toBe(true);
  });

  it('offers Logs on every row and Run only where a run is allowed', () => {
    const row = rowFor('Database backup')!;
    const logs = Array.from(row.querySelectorAll('.co-inline-link')).find(
      (link) => link.textContent?.trim() === 'Logs',
    );

    // One link to this workflow's runs, and no second label claiming to show provider log text.
    expect(logs).not.toBeUndefined();
    expect(row.textContent).not.toContain('Run logs');

    // Marked and dispatchable, so Run is a real control.
    expect(rowFor('Deploy production')!.querySelector('button.run')).not.toBeNull();
    // Unmarked, so it is present but refuses, and says why.
    const unmarked = rowFor('Database backup')!.querySelector('.run')!;
    expect(unmarked.tagName).not.toBe('BUTTON');
    expect(unmarked.getAttribute('title')).toContain('risk');
  });

  it('asks for a branch and confirmation before starting a normal workflow', async () => {
    rowFor('Deploy production')!.querySelector<HTMLButtonElement>('button.run')!.click();
    await fixture.whenStable();

    const dialog = host.querySelector('co-workflow-run-dialog')!;
    // Branches the repository reports, with the registered one selected: nothing is typed from memory.
    const branch = dialog.querySelector<HTMLSelectElement>('#run-reference')!;
    expect(Array.from(branch.options).map((option) => option.value)).toEqual([
      'master',
      'release/2026-08',
    ]);
    expect(branch.value).toBe('master');
    expect(dialog.querySelector('#run-confirmation')).toBeNull();

    dialog.querySelector<HTMLButtonElement>('.primary')!.click();
    await fixture.whenStable();

    expect(dataSource.dispatches).toEqual([
      {
        projectId: 'eemo',
        workflowId: '101',
        reference: 'master',
        inputs: {},
        confirmation: null,
      },
    ]);
  });

  it('will not start a destructive workflow until its name is typed', async () => {
    rowFor('Database restore')!.querySelector('.run');
    await select('Database restore');
    host
      .querySelector('co-workflow-detail')!
      .querySelectorAll<HTMLButtonElement>('.co-inline-link')
      .forEach((link) => {
        if (link.textContent?.trim() === 'Run workflow') {
          link.click();
        }
      });
    await fixture.whenStable();

    const dialog = host.querySelector('co-workflow-run-dialog')!;
    expect(dialog.textContent).toContain('Marked destructive');

    const run = dialog.querySelector<HTMLButtonElement>('.primary')!;
    expect(run.disabled).toBe(true);

    const confirmation = dialog.querySelector<HTMLInputElement>('#run-confirmation')!;
    confirmation.value = 'Database restore';
    confirmation.dispatchEvent(new Event('input'));
    await fixture.whenStable();

    expect(dialog.querySelector<HTMLButtonElement>('.primary')!.disabled).toBe(false);
    dialog.querySelector<HTMLButtonElement>('.primary')!.click();
    await fixture.whenStable();

    expect(dataSource.dispatches[0].confirmation).toBe('Database restore');
  });

  it('keeps the panel open and shows why when the provider refuses', async () => {
    await render(
      new StubWorkflows(
        of(INVENTORY),
        of(JOBS),
        of({
          defaultBranch: 'master',
          branches: ['master'],
          hasMore: false,
        }),
        of({
          manualRun: 'supported' as const,
          definitionPath: '.github/workflows/deploy-production.yml',
          inputs: [],
        }),
        throwError(() => ({
          error: {
            detail:
              "GitHub refused the run. The configured token needs write access to this repository's actions.",
          },
        })),
      ),
    );

    rowFor('Deploy production')!.querySelector<HTMLButtonElement>('button.run')!.click();
    await fixture.whenStable();
    host
      .querySelector('co-workflow-run-dialog')!
      .querySelector<HTMLButtonElement>('.primary')!
      .click();
    await fixture.whenStable();

    // The refusal belongs where the operator asked, not on a page behind the panel.
    const dialog = host.querySelector('co-workflow-run-dialog');
    expect(dialog).not.toBeNull();
    expect(dialog?.textContent).toContain('needs write access');
  });

  it('says a run was requested rather than claiming one is going', async () => {
    rowFor('Deploy production')!.querySelector<HTMLButtonElement>('button.run')!.click();
    await fixture.whenStable();
    host
      .querySelector('co-workflow-run-dialog')!
      .querySelector<HTMLButtonElement>('.primary')!
      .click();
    await fixture.whenStable();

    // The provider accepts without reporting a run, and the stub's newest run predates the request.
    expect(rowFor('Deploy production')?.textContent).toContain('Requested');
  });

  it('offers a run action only where the provider reported manual dispatch', () => {
    const deploy = rowFor('Deploy production')!.querySelector('.run')!;
    expect(deploy.textContent?.trim()).toBe('Run');
    // Marked and dispatchable, so it is a control rather than a label.
    expect(deploy.tagName).toBe('BUTTON');

    // Refuses, and names the risk reason first: an unmarked workflow would not be run even if it were enabled.
    const refused = rowFor('Security scan')!.querySelector('.run')!;
    expect(refused.tagName).not.toBe('BUTTON');
    expect(refused.getAttribute('title')).toContain('risk');
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
              defaultBranch: 'master',
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
