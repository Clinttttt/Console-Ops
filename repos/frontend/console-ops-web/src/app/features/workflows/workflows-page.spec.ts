import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';

import { WorkflowInventory } from '../../core/contracts/workflows';
import { MockWorkflowsDataSource } from '../../core/data/mock/mock-workflows.data-source';
import { WorkflowsDataSource } from '../../core/data/workflows.data-source';
import { WorkflowsPage } from './workflows-page';

describe('WorkflowsPage', () => {
  let fixture: ComponentFixture<WorkflowsPage>;
  let host: HTMLElement;
  let inventory: WorkflowInventory;

  async function render(dataSource: unknown = new MockWorkflowsDataSource()): Promise<void> {
    TestBed.resetTestingModule();
    await TestBed.configureTestingModule({
      imports: [WorkflowsPage],
      providers: [{ provide: WorkflowsDataSource, useValue: dataSource }],
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
    inventory = await new Promise((resolve) => {
      new MockWorkflowsDataSource().load().subscribe(resolve);
    });
  });

  it('says the screen is showing sample data rather than read automation', () => {
    // The payload carries the claim, so no screen can present examples as observed workflows.
    expect(inventory.isSampleData).toBe(true);
    expect(host.textContent).toContain('Sample data');
    expect(host.textContent).toContain('not automation Console Ops has read');
  });

  it('groups workflows under the project that owns them', () => {
    const groups = Array.from(host.querySelectorAll('.group'));

    expect(groups.length).toBe(2);
    expect(groups[0].textContent).toContain('EEMO-Cantilan-SDS');
    expect(groups[0].textContent).toContain('6 workflows');
    expect(host.textContent).toContain('Showing 8 of 8 workflows');
  });

  it('calls a workflow a deployment only where one was configured', () => {
    expect(rowFor('Deploy production')?.textContent).toContain('Primary deployment · Production');

    // "Database backup" is maintenance to a reader and unclassified to Console Ops, which cannot prove it.
    expect(rowFor('Database backup')?.textContent).toContain('Unclassified');
    expect(rowFor('Database backup')?.textContent).not.toContain('Primary deployment');
  });

  it('reports a workflow that never ran as having no run, not as a failure', () => {
    const row = rowFor('Database restore');

    expect(row?.textContent).toContain('No run recorded');
    expect(row?.textContent).not.toContain('Failed');
  });

  it('does not describe a running run by an outcome it has not reached', () => {
    const row = rowFor('Deploy production');

    expect(row?.textContent).toContain('In progress');
    expect(row?.textContent).not.toContain('Passed');
  });

  it('reports a disabled workflow as disabled rather than as failed', () => {
    const row = rowFor('Security scan');

    expect(row?.textContent).toContain('Disabled at the provider');
    expect(row?.textContent).not.toContain('Failed');
  });

  it('offers a run action only where the provider reported manual dispatch', () => {
    // Supported, so the affordance exists - named as a later phase rather than acting.
    const backup = rowFor('Database backup')!.querySelector('.run');
    expect(backup?.textContent?.trim()).toBe('Run');
    expect(backup?.classList.contains('is-unavailable')).toBe(true);

    // Reported unavailable: no action at all, rather than a button that cannot work.
    expect(rowFor('CI')!.querySelector('.run')).toBeNull();
    expect(rowFor('CI')?.textContent).not.toContain('Manual run unknown');

    // Never asked: stated as unknown, which is not the same as knowing it cannot be run.
    expect(rowFor('Publish Collector APK')?.textContent).toContain('Manual run unknown');
  });

  it('names run history and workflow logs as planned instead of linking nowhere', () => {
    const row = rowFor('CI')!;
    const runs = Array.from(row.querySelectorAll('.co-inline-link')).find(
      (link) => link.textContent?.trim() === 'Runs',
    );
    const logs = Array.from(row.querySelectorAll('.co-inline-link')).find(
      (link) => link.textContent?.trim() === 'Run logs',
    );

    expect(runs?.classList.contains('is-unavailable')).toBe(true);
    expect(logs?.getAttribute('title')).toContain('Application logs are on the Logs screen');
  });

  it('names starting a workflow as a later phase instead of offering a control that does nothing', () => {
    const dispatch = host.querySelector('.dispatch')!;

    expect(dispatch.textContent).toContain('Run workflow');
    expect(dispatch.classList.contains('is-unavailable')).toBe(true);
    expect(dispatch.tagName).not.toBe('BUTTON');
  });

  it('opens nothing until a workflow is chosen', () => {
    expect(host.querySelector('co-workflow-detail')).toBeNull();
    expect(host.textContent).toContain('Select a workflow to inspect it');
  });

  it('reports the selected workflow and where its latest run has reached', async () => {
    await select('Deploy production');
    const detail = host.querySelector('co-workflow-detail')!;

    expect(detail.textContent).toContain('.github/workflows/deploy-production.yml');
    expect(detail.textContent).toContain('#535');
    expect(detail.textContent).toContain('2ac8bf0');
    expect(detail.textContent).toContain('Clinttttt');
    expect(detail.textContent).toContain('Deployment');
    // A run with no end has no duration, and the panel says so instead of implying one.
    expect(detail.textContent).toContain('Still running');

    const jobs = Array.from(detail.querySelectorAll('.job'));
    expect(jobs.length).toBe(3);
    expect(jobs[0].textContent).toContain('Prepare');
    expect(jobs[0].textContent).toContain('1m 12s');
    expect(jobs[1].textContent).toContain('In progress');
    expect(jobs[2].textContent).toContain('Waiting');
  });

  it('states an unknown manual run in the detail rather than implying it is unavailable', async () => {
    await select('Publish Collector APK');

    expect(host.querySelector('co-workflow-detail')?.textContent).toContain('Unknown');
  });

  it('narrows to the workflows a filter matches and says how many that is', async () => {
    const deployment = Array.from(host.querySelectorAll<HTMLButtonElement>('.filter')).find(
      (button) => button.textContent?.trim() === 'Deployment',
    )!;
    deployment.click();
    await fixture.whenStable();

    expect(host.querySelectorAll('co-workflow-row').length).toBe(2);
    expect(host.textContent).toContain('Showing 2 of 8 workflows');
    expect(rowFor('CI')).toBeUndefined();
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
    const clear = Array.from(host.querySelectorAll<HTMLButtonElement>('.co-inline-link')).find(
      (button) => button.textContent?.trim() === 'Clear filters',
    )!;
    clear.click();
    await fixture.whenStable();

    expect(host.querySelectorAll('co-workflow-row').length).toBe(8);
  });

  it('narrows to one project without claiming the others are gone', async () => {
    const project = host.querySelector<HTMLSelectElement>('#workflow-project')!;
    project.value = 'spinner';
    project.dispatchEvent(new Event('change'));
    await fixture.whenStable();

    expect(host.querySelectorAll('.group').length).toBe(1);
    expect(host.textContent).toContain('Showing 2 of 8 workflows');
  });

  it('says the inventory could not be read instead of showing an empty repository', async () => {
    await render({ load: () => throwError(() => new Error('unreachable')) });

    expect(host.textContent).toContain('Workflow inventory unavailable');
    expect(host.textContent).not.toContain('No workflows found');
  });

  it('reports a repository with no workflows as empty rather than as unavailable', async () => {
    await render({
      load: () => of({ isSampleData: false, readAt: '2026-08-19T07:05:00.000Z', groups: [] }),
    });

    expect(host.textContent).toContain('No workflows found');
    expect(host.textContent).not.toContain('Sample data');
  });
});
