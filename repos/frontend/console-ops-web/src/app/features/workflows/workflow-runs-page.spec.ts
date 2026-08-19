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
import { WorkflowRunsPage } from './workflow-runs-page';

const READ_AT = '2026-08-19T07:05:00.000Z';

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
          name: 'CI',
          path: '.github/workflows/ci.yml',
          state: 'active',
          classification: 'unclassified',
          manualRun: 'unknown',
          latestRun: null,
        },
      ],
    },
  ],
};

const HISTORY: WorkflowRunHistory = {
  workflowId: '101',
  hasMore: true,
  runs: [
    {
      id: '938',
      number: 938,
      status: 'completed',
      conclusion: 'passed',
      branch: 'master',
      commitSha: '2ac8bf0f4c1e9d7a3b5c8e2f1a4d6b9c0e3f7a21',
      commitShortSha: '2ac8bf0',
      trigger: 'push',
      actor: 'Clinttttt',
      startedAt: '2026-08-19T06:00:00.000Z',
      completedAt: '2026-08-19T06:04:18.000Z',
      durationSeconds: 258,
      runUrl: 'https://github.test/run/938',
      jobs: [],
    },
    {
      id: '937',
      number: 937,
      status: 'inProgress',
      conclusion: null,
      branch: 'master',
      commitSha: 'd713a12b4c6e8f0a2c4e6b8d0f2a4c6e8b0d2f46',
      commitShortSha: 'd713a12',
      trigger: 'schedule',
      actor: null,
      startedAt: '2026-08-19T06:58:00.000Z',
      completedAt: null,
      durationSeconds: null,
      runUrl: null,
      jobs: [],
    },
  ],
};

const JOBS: readonly WorkflowRunJob[] = [
  { name: 'Backend', status: 'completed', conclusion: 'passed', durationSeconds: 161 },
  { name: 'Frontend', status: 'completed', conclusion: 'failed', durationSeconds: 97 },
];

class StubWorkflows extends WorkflowsDataSource {
  runRequests: { projectId: string; workflowId: string }[] = [];
  jobRequests: { projectId: string; runId: string }[] = [];

  constructor(
    private readonly history: Observable<WorkflowRunHistory> = of(HISTORY),
    private readonly jobs: Observable<readonly WorkflowRunJob[]> = of(JOBS),
  ) {
    super();
  }

  override load(): Observable<WorkflowInventory> {
    return of(INVENTORY);
  }

  override loadRuns(projectId: string, workflowId: string): Observable<WorkflowRunHistory> {
    this.runRequests.push({ projectId, workflowId });
    return this.history;
  }

  override loadRunJobs(projectId: string, runId: string): Observable<readonly WorkflowRunJob[]> {
    this.jobRequests.push({ projectId, runId });
    return this.jobs;
  }

  override loadManualRunSupport(): Observable<ManualRunSupportReading> {
    return of({ manualRun: 'unknown', definitionPath: '.github/workflows/ci.yml' });
  }
}

describe('WorkflowRunsPage', () => {
  let fixture: ComponentFixture<WorkflowRunsPage>;
  let host: HTMLElement;
  let dataSource: StubWorkflows;

  async function render(stub: StubWorkflows = new StubWorkflows()): Promise<void> {
    dataSource = stub;
    TestBed.resetTestingModule();
    await TestBed.configureTestingModule({
      imports: [WorkflowRunsPage],
      providers: [provideRouter([]), { provide: WorkflowsDataSource, useValue: stub }],
    }).compileComponents();

    fixture = TestBed.createComponent(WorkflowRunsPage);
    fixture.componentRef.setInput('projectId', 'eemo');
    fixture.componentRef.setInput('workflowId', '101');
    await fixture.whenStable();
    host = fixture.nativeElement as HTMLElement;
  }

  function runRow(number: string): Element | undefined {
    return Array.from(host.querySelectorAll('.run')).find((run) =>
      run.querySelector('.number')?.textContent?.includes(number),
    );
  }

  beforeEach(async () => {
    await render();
  });

  it('reads the history of the workflow the route names', () => {
    expect(dataSource.runRequests).toEqual([{ projectId: 'eemo', workflowId: '101' }]);
    expect(host.querySelectorAll('.run').length).toBe(2);
  });

  it('names the workflow from the inventory rather than from the run payload', () => {
    // The runs endpoint answers with runs, so the heading comes from the inventory or says it is not known.
    expect(host.querySelector('.title')?.textContent).toContain('CI');
    expect(host.textContent).toContain('.github/workflows/ci.yml');
  });

  it('reports each run with what the provider said and nothing more', () => {
    const passed = runRow('938')!;
    expect(passed.textContent).toContain('Passed');
    expect(passed.textContent).toContain('master');
    expect(passed.textContent).toContain('2ac8bf0');
    expect(passed.textContent).toContain('4m 18s');
    expect(passed.textContent).toContain('Clinttttt');

    const running = runRow('937')!;
    // Still going: no outcome, and no duration that would imply an end.
    expect(running.textContent).toContain('In progress');
    expect(running.textContent).toContain('Still running');
    expect(running.textContent).not.toContain('Passed');
    // A scheduled run often has no person behind it, which is a fact rather than a gap.
    expect(running.textContent).toContain('No actor reported');
  });

  it('says the list is recent history when the provider reports more', () => {
    expect(host.textContent).toContain('Showing the 2 most recent runs');
    expect(host.textContent).toContain('The provider reports more');
  });

  it('reads the jobs of a run only when it is opened, and once', async () => {
    expect(dataSource.jobRequests).toEqual([]);

    runRow('938')!.querySelector<HTMLButtonElement>('.summary')!.click();
    await fixture.whenStable();

    expect(dataSource.jobRequests).toEqual([{ projectId: 'eemo', runId: '938' }]);
    const jobs = Array.from(runRow('938')!.querySelectorAll('.job'));
    expect(jobs.length).toBe(2);
    expect(jobs[0].textContent).toContain('Backend');
    expect(jobs[0].textContent).toContain('2m 41s');
    // The failing job is identifiable without opening the provider.
    expect(jobs[1].textContent).toContain('Failed');

    // Closing and reopening does not ask the provider again for a run already read.
    runRow('938')!.querySelector<HTMLButtonElement>('.summary')!.click();
    await fixture.whenStable();
    runRow('938')!.querySelector<HTMLButtonElement>('.summary')!.click();
    await fixture.whenStable();
    expect(dataSource.jobRequests.length).toBe(1);
  });

  it('offers the provider run only where one was reported', async () => {
    runRow('938')!.querySelector<HTMLButtonElement>('.summary')!.click();
    await fixture.whenStable();
    expect(runRow('938')!.querySelector('a[href="https://github.test/run/938"]')).not.toBeNull();

    runRow('937')!.querySelector<HTMLButtonElement>('.summary')!.click();
    await fixture.whenStable();
    // No URL was reported, so none is composed from ids and hoped to resolve.
    expect(runRow('937')!.querySelector('.jobs-panel a')).toBeNull();
  });

  it('names jobs that could not be read rather than showing a run without any', async () => {
    await render(
      new StubWorkflows(
        of(HISTORY),
        throwError(() => new Error('rate limited')),
      ),
    );

    runRow('938')!.querySelector<HTMLButtonElement>('.summary')!.click();
    await fixture.whenStable();

    expect(host.textContent).toContain('jobs of this run could not be read');
  });

  it('says the history could not be read instead of showing a workflow with no runs', async () => {
    await render(new StubWorkflows(throwError(() => new Error('unreachable'))));

    expect(host.textContent).toContain('could not read this workflow');
    expect(host.textContent).not.toContain('No runs recorded');
  });

  it('reports a workflow with no runs as such rather than as unavailable', async () => {
    await render(new StubWorkflows(of({ workflowId: '101', runs: [], hasMore: false })));

    expect(host.textContent).toContain('No runs recorded');
    expect(host.textContent).toContain('not the same as a run');
  });
});
