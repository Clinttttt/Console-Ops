import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';

import {
  DeploymentEnvironmentObservation,
  DeploymentListItem,
  DeploymentRegistry,
} from '../../core/contracts/deployment-registry';
import { DeploymentRegistryDataSource } from '../../core/data/deployment-registry.data-source';
import { EnvironmentScopeStore } from '../../core/state/environment-scope.store';
import { DeploymentsPage } from './deployments-page';

const OBSERVED_AT = '2026-08-14T09:30:00Z';

function environment(
  overrides: Partial<DeploymentEnvironmentObservation> & {
    name: string;
    kind: DeploymentEnvironmentObservation['environment']['kind'];
  },
): DeploymentEnvironmentObservation {
  const { name, kind, ...rest } = overrides;
  return {
    environment: { id: `env-${name.toLowerCase()}`, name, kind },
    isCurrent: true,
    firstObservedAt: '2026-08-14T09:15:00Z',
    healthBefore: 'healthy',
    healthBeforeObservedAt: '2026-08-14T08:45:00Z',
    healthAfter: 'healthy',
    healthAfterObservedAt: '2026-08-14T09:15:00Z',
    versionCheck: 'inSync',
    versionCheckObservedAt: '2026-08-14T09:15:00Z',
    ...rest,
  };
}

function deployment(overrides: Partial<DeploymentListItem>): DeploymentListItem {
  return {
    id: 'deployment-1',
    projectId: 'project-spinner',
    projectName: 'Spinner API',
    provider: 'githubActions',
    repository: 'spinner/api',
    branch: 'main',
    commitSha: '8a17c2f9abcdef0123456789abcdef0123456789',
    commitShortSha: '8a17c2f',
    result: 'passed',
    workflowFile: 'deploy-spinner-api.yml',
    workflowName: 'Deploy Spinner API',
    workflowUrl: 'https://github.com/spinner/api/actions/runs/1421',
    runNumber: 1421,
    triggeredBy: 'ci-bot',
    startedAt: '2026-08-14T09:09:42Z',
    completedAt: '2026-08-14T09:12:00Z',
    deployedAt: '2026-08-14T09:12:00Z',
    durationSeconds: 138,
    recordedAt: '2026-08-14T09:15:00Z',
    environments: [environment({ name: 'Production', kind: 'production' })],
    ...overrides,
  };
}

/**
 * The screen is checked against a payload shaped exactly like the API's, including the cases that
 * matter operationally: a bad release, a release nobody has seen running, and a superseded one.
 */
const REGISTRY: DeploymentRegistry = {
  observedAt: OBSERVED_AT,
  deployments: [
    deployment({}),
    // Same project and environment, one release earlier: no longer current.
    deployment({
      id: 'deployment-2',
      commitSha: '71be1290abcdef0123456789abcdef0123456789',
      commitShortSha: '71be129',
      runNumber: 1420,
      workflowUrl: 'https://github.com/spinner/api/actions/runs/1420',
      deployedAt: '2026-08-14T06:30:00Z',
      completedAt: '2026-08-14T06:30:00Z',
      startedAt: '2026-08-14T06:28:00Z',
      durationSeconds: 120,
      environments: [
        environment({
          name: 'Production',
          kind: 'production',
          isCurrent: false,
          firstObservedAt: '2026-08-14T06:35:00Z',
          healthAfterObservedAt: '2026-08-14T06:35:00Z',
        }),
      ],
    }),
    // Health failed on the first check after the release was seen running.
    deployment({
      id: 'deployment-3',
      projectId: 'project-stalltrack',
      projectName: 'StallTrack',
      repository: 'stalltrack/api',
      commitSha: '62cd8210abcdef0123456789abcdef0123456789',
      commitShortSha: '62cd821',
      workflowFile: 'deploy-stalltrack.yml',
      workflowName: 'Deploy StallTrack',
      workflowUrl: 'https://github.com/stalltrack/api/actions/runs/311',
      runNumber: 311,
      triggeredBy: 'developer',
      deployedAt: '2026-08-13T22:10:00Z',
      completedAt: '2026-08-13T22:10:00Z',
      startedAt: '2026-08-13T22:08:00Z',
      durationSeconds: 120,
      environments: [
        environment({
          name: 'Staging',
          kind: 'staging',
          healthAfter: 'unhealthy',
          firstObservedAt: '2026-08-13T22:15:00Z',
        }),
      ],
    }),
    // The run passed, but no environment has ever reported this commit.
    deployment({
      id: 'deployment-4',
      projectId: 'project-amyl',
      projectName: 'AMYL',
      repository: 'amyl/api',
      branch: 'develop',
      commitSha: '19cf2810abcdef0123456789abcdef0123456789',
      commitShortSha: '19cf281',
      workflowFile: null,
      workflowName: null,
      workflowUrl: null,
      runNumber: null,
      triggeredBy: null,
      deployedAt: '2026-08-12T11:00:00Z',
      completedAt: null,
      startedAt: '2026-08-12T11:00:00Z',
      durationSeconds: null,
      environments: [],
    }),
  ],
};

describe('DeploymentsPage', () => {
  let fixture: ComponentFixture<DeploymentsPage>;
  let host: HTMLElement;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DeploymentsPage],
      providers: [
        {
          provide: DeploymentRegistryDataSource,
          useValue: { load: () => of(REGISTRY) },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(DeploymentsPage);
    await fixture.whenStable();
    host = fixture.nativeElement as HTMLElement;
  });

  function rows(): HTMLElement[] {
    return Array.from(host.querySelectorAll('co-deployment-timeline .card'));
  }

  function rowFor(text: string): HTMLElement | undefined {
    return rows().find((row) => row.textContent?.includes(text));
  }

  function dayLabels(): (string | undefined)[] {
    return Array.from(host.querySelectorAll('.day-label')).map((label) =>
      label.textContent?.trim(),
    );
  }

  function rail(): string {
    return host.querySelector('co-selected-deployment')?.textContent ?? '';
  }

  function verification(): string {
    return host.querySelector('co-deployment-verification')?.textContent ?? '';
  }

  /** Verification figures read as label/value pairs, so a loose substring cannot pass by accident. */
  function stats(): Record<string, string> {
    const entries: Record<string, string> = {};
    host.querySelectorAll('co-deployment-verification .stat').forEach((stat) => {
      const label = stat.querySelector('dt')?.textContent?.trim() ?? '';
      entries[label] = stat.querySelector('.value')?.textContent?.trim() ?? '';
    });
    return entries;
  }

  function clickView(label: string): void {
    Array.from(host.querySelectorAll<HTMLButtonElement>('.view'))
      .find((button) => button.textContent?.trim() === label)
      ?.click();
  }

  it('never presents recorded history as sample data', () => {
    expect(host.querySelector('.preview-notice')).toBeNull();
    expect(host.textContent).not.toContain('Sample data');
  });

  it('groups releases by day, newest first, with today named', () => {
    expect(dayLabels()).toEqual(['Today — Aug 14, 2026', 'Aug 13, 2026', 'Aug 12, 2026']);
    expect(rows().length).toBe(4);
  });

  it('shows the run identity and the environments observed running it', () => {
    const latest = rows()[0];

    expect(latest.textContent).toContain('Spinner API');
    expect(latest.textContent).toContain('Production');
    expect(latest.textContent).toContain('8a17c2f');
    expect(latest.textContent).toContain('main');
    expect(latest.textContent).toContain('deploy-spinner-api.yml #1421');
    expect(latest.textContent).toContain('Current');
    expect(latest.textContent).toContain('Passed');
    expect(latest.textContent).toContain('2m 18s');
  });

  it('claims no runtime revision, because V1 has no source for one', () => {
    expect(host.textContent).not.toContain('revision');
    expect(host.textContent).not.toContain('Revision');
  });

  it('derives the verdict from the reported facts rather than one flag', () => {
    // Health failed on the check after the release was first seen.
    expect(rowFor('StallTrack')?.textContent).toContain('Failed');
    // The run passed but nothing ever reported the commit: unverified, not failed and not silent.
    expect(rowFor('AMYL')?.textContent).toContain('Unverified');
    expect(rowFor('AMYL')?.textContent).toContain('Not observed running');
    expect(rowFor('AMYL')?.textContent).toContain('No workflow configured');
  });

  it('marks only the release an environment still reports as current', () => {
    expect(rowFor('8a17c2f')?.querySelector('.current')).not.toBeNull();
    expect(rowFor('71be129')?.querySelector('.current')).toBeNull();
  });

  it('describes the newest visible release in the rail until another is chosen', () => {
    expect(rail()).toContain('Spinner API');
    expect(rail()).toContain('deploy-spinner-api.yml #1421');
    expect(rail()).toContain('ci-bot');
    expect(rail()).toContain('In Sync');
  });

  it('brackets a release with the health observed either side of it', async () => {
    rowFor('StallTrack')!.click();
    await fixture.whenStable();

    expect(rail()).toContain('Health before');
    expect(rail()).toContain('Healthy');
    expect(rail()).toContain('Health after');
    expect(rail()).toContain('Unhealthy');
  });

  it('says plainly when a release was never observed running', async () => {
    rowFor('AMYL')!.click();
    await fixture.whenStable();

    expect(rail()).toContain('No environment has reported this commit');
    expect(rail()).toContain('Not reported');
  });

  it('narrows to failed verifications', async () => {
    clickView('Failed');
    await fixture.whenStable();

    expect(rows().length).toBe(1);
    expect(rows()[0].textContent).toContain('StallTrack');
  });

  it('narrows by environment through the shared scope', async () => {
    clickView('Staging');
    await fixture.whenStable();

    expect(TestBed.inject(EnvironmentScopeStore).scope()).toBe('staging');
    // Only the release observed running in Staging survives; the unobserved one is not assumed in.
    expect(rows().length).toBe(1);
    expect(rows()[0].textContent).toContain('StallTrack');
    expect(host.textContent).toContain('Showing 1 of 4 releases');
  });

  it('searches across project, repository, branch, commit, workflow, and trigger', async () => {
    const search = host.querySelector<HTMLInputElement>('#co-deployment-search');
    search!.value = 'developer';
    search!.dispatchEvent(new Event('input'));
    await fixture.whenStable();

    expect(rows().length).toBe(1);
    expect(rows()[0].textContent).toContain('StallTrack');
  });

  it('counts the verification summary from the records in view', () => {
    expect(verification()).toContain('Current releases');
    // Two of the four records are still the release their environment reports.
    expect(stats()['Current releases']).toBe('2');
    // Two of four verdicts pass: one failed health, one was never observed running.
    expect(stats()['Verification passed']).toBe('50%');
    // Mean of the three reported durations: (138 + 120 + 120) / 3.
    expect(stats()['Average run time']).toBe('2m 06s');
    expect(stats()['Releases (7d)']).toBe('4');
  });

  it('recounts the summary when the view narrows', async () => {
    clickView('Failed');
    await fixture.whenStable();

    expect(stats()['Verification passed']).toBe('0%');
  });

  it('keeps deploy, redeploy, and log access unavailable', () => {
    expect(host.querySelector<HTMLButtonElement>('.deploy')?.disabled).toBe(true);
    // Two disabled actions on every card: Logs and More.
    expect(host.querySelectorAll('.card .action.is-unavailable').length).toBe(9);
  });

  it('links a recorded workflow run and never a missing one', () => {
    const workflowLinks = Array.from(
      host.querySelectorAll<HTMLAnchorElement>('.card .card-actions a'),
    );

    expect(workflowLinks[0].getAttribute('href')).toBe(
      'https://github.com/spinner/api/actions/runs/1421',
    );
    expect(workflowLinks[0].getAttribute('rel')).toBe('noopener noreferrer');
    // The AMYL release has no trusted run link, so it contributes no anchor.
    expect(workflowLinks.length).toBe(3);
  });
});
