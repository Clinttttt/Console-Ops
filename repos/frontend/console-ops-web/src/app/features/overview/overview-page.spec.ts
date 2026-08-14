import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';

import { DashboardOverviewDataSource } from '../../core/data/dashboard-overview.data-source';
import { DASHBOARD_OVERVIEW_FIXTURE } from '../../core/data/mock/dashboard-overview.fixture';
import { EnvironmentScopeStore } from '../../core/state/environment-scope.store';
import { OverviewPage } from './overview-page';

describe('OverviewPage', () => {
  let fixture: ComponentFixture<OverviewPage>;
  let host: HTMLElement;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [OverviewPage],
      providers: [
        {
          provide: DashboardOverviewDataSource,
          useValue: { load: () => of(DASHBOARD_OVERVIEW_FIXTURE) },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(OverviewPage);
    await fixture.whenStable();
    host = fixture.nativeElement as HTMLElement;
  });

  function projectRows(): HTMLElement[] {
    return Array.from(host.querySelectorAll('co-project-surfaces tbody tr'));
  }

  it('renders the V1 pipeline as GitHub, GitHub Actions and Application', async () => {
    const pipeline = host.querySelector('co-pipeline-flow');

    expect(pipeline?.textContent).toContain('GitHub');
    expect(pipeline?.textContent).toContain('GitHub Actions');
    expect(pipeline?.textContent).toContain('Application');
    expect(pipeline?.textContent).toContain('Health & version');
    expect(pipeline?.textContent).not.toContain('Azure DevOps');
    expect(pipeline?.textContent).not.toContain('.NET on Azure');
  });

  it('lists every project surface with its environment and health', () => {
    const rows = projectRows();

    expect(rows.length).toBe(3);
    expect(rows[0].textContent).toContain('Spinner API');
    expect(rows[0].textContent).toContain('Production');
    expect(rows[0].textContent).toContain('Healthy');
    expect(rows[0].textContent).toContain('103 ms');
    expect(rows[2].textContent).toContain('AMYL');
    expect(rows[2].textContent).toContain('Local');
    expect(rows[2].textContent).toContain('Not configured');
  });

  it('reports unavailable facts honestly instead of inventing them', () => {
    const amyl = projectRows()[2];
    const matrix = host.querySelector('co-system-state');

    expect(amyl.textContent).toContain('Not configured');
    expect(matrix?.textContent).toContain('N/A');
    expect(matrix?.textContent).toContain('Unknown');
    expect(matrix?.textContent).toContain('No version endpoint');
  });

  it('renders the system state matrix with one column pair per project', () => {
    const groupHeaders = Array.from(
      host.querySelectorAll('co-system-state thead tr:first-child th.group'),
    ).map((header) => header.textContent?.trim());

    expect(groupHeaders).toEqual([
      'Spinner API (Production)',
      'StallTrack (Production)',
      'AMYL (Local)',
    ]);
    expect(host.querySelectorAll('co-system-state tbody tr').length).toBe(5);
  });

  it('shows recent activity newest first in UTC', () => {
    const entries = Array.from(host.querySelectorAll('co-recent-activity .entry'));

    expect(entries.length).toBe(4);
    expect(entries[0].textContent).toContain('Spinner API health recovered');
    expect(entries[0].textContent).toContain('06:22 PM');
    expect(entries[3].textContent).toContain('StallTrack health failed');
  });

  it('keeps V1 source, workflow, sync and observation provenance explicit', () => {
    const spinner = DASHBOARD_OVERVIEW_FIXTURE.projects[0];
    const amyl = DASHBOARD_OVERVIEW_FIXTURE.projects[2];
    const rowKeys = DASHBOARD_OVERVIEW_FIXTURE.systemState.rows.map((row) => row.key);

    expect(spinner.source.commitSha).not.toBeNull();
    expect(spinner.source.observedAt).not.toBeNull();
    expect(spinner.workflow.state).toBe('passed');
    expect(spinner.versionSync.state).toBe('inSync');
    expect(amyl.workflow.state).toBe('notConfigured');
    expect(amyl.deployedVersion).toBeNull();
    expect(amyl.versionSync.state).toBe('notConfigured');
    expect(rowKeys).toEqual([
      'api',
      'dependency:database',
      'dependency:redis',
      'ci',
      'versionSync',
    ]);
    expect(DASHBOARD_OVERVIEW_FIXTURE.summary.uptime).toBeNull();
  });

  it('narrows projects and matrix columns to the selected environment scope', async () => {
    TestBed.inject(EnvironmentScopeStore).select('local');
    await fixture.whenStable();

    const rows = projectRows();
    expect(rows.length).toBe(1);
    expect(rows[0].textContent).toContain('AMYL');

    const groupHeaders = host.querySelectorAll('co-system-state thead tr:first-child th.group');
    expect(groupHeaders.length).toBe(1);
    expect(groupHeaders[0].textContent).toContain('AMYL (Local)');
  });

  it('reports an empty scope instead of showing nothing', async () => {
    TestBed.inject(EnvironmentScopeStore).select('staging');
    await fixture.whenStable();

    expect(host.querySelector('co-project-surfaces')?.textContent).toContain(
      'No registered projects in Staging.',
    );
    expect(projectRows().length).toBe(0);
  });
});
