import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';

import { DeploymentRegistryDataSource } from '../../core/data/deployment-registry.data-source';
import { DEPLOYMENT_REGISTRY_FIXTURE } from '../../core/data/mock/deployment-registry.fixture';
import { EnvironmentScopeStore } from '../../core/state/environment-scope.store';
import { DeploymentsPage } from './deployments-page';

describe('DeploymentsPage', () => {
  let fixture: ComponentFixture<DeploymentsPage>;
  let host: HTMLElement;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DeploymentsPage],
      providers: [
        {
          provide: DeploymentRegistryDataSource,
          useValue: { load: () => of(DEPLOYMENT_REGISTRY_FIXTURE) },
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

  function clickView(label: string): void {
    Array.from(host.querySelectorAll<HTMLButtonElement>('.view'))
      .find((button) => button.textContent?.trim() === label)
      ?.click();
  }

  it('groups deployments by day, newest first, with today named', () => {
    expect(dayLabels()).toEqual(['Today — May 14, 2025', 'May 13, 2025', 'May 12, 2025']);
    expect(rows().length).toBe(8);
  });

  it('shows source, revision, verification, and duration for a deployment', () => {
    const latest = rows()[0];

    expect(latest.textContent).toContain('Spinner API');
    expect(latest.textContent).toContain('Production');
    expect(latest.textContent).toContain('8a17c2f');
    expect(latest.textContent).toContain('main');
    expect(latest.textContent).toContain('spinner-api--000021');
    expect(latest.textContent).toContain('Current');
    expect(latest.textContent).toContain('Passed');
    expect(latest.textContent).toContain('18 min ago');
    expect(latest.textContent).toContain('2m 18s');
  });

  it('derives the verdict from the reported facts rather than one flag', () => {
    const consoleOps = rows().find((row) => row.textContent?.includes('console-ops--000007'));
    const failedSpinner = rows().find((row) => row.textContent?.includes('spinner-api--000020'));
    const amyl = rows().find((row) => row.textContent?.includes('AMYL'));

    // Succeeded and healthy, but source moved on.
    expect(consoleOps?.textContent).toContain('Behind');
    // Health probe failed after the deployment.
    expect(failedSpinner?.textContent).toContain('Failed');
    // No version endpoint must not downgrade an otherwise passing deployment.
    expect(amyl?.textContent).toContain('Passed');
  });

  it('reports a local deployment without inventing a revision', () => {
    const amyl = rows().find((row) => row.textContent?.includes('AMYL'));

    expect(amyl?.textContent).toContain('No runtime revision');
    expect(amyl?.textContent).toContain('develop');
    // It is still the active local deployment, so the badge belongs there.
    expect(amyl?.querySelector('.current')).not.toBeNull();
  });

  it('describes the newest visible deployment in the rail until another is chosen', () => {
    expect(rail()).toContain('Spinner API / Production');
    expect(rail()).toContain('deploy-spinner-api.yml');
    expect(rail()).toContain('Succeeded');
    expect(rail()).toContain('In Sync');
    expect(rail()).toContain('ci-bot');
  });

  it('follows the row selection', async () => {
    const failed = rows().find((row) => row.textContent?.includes('spinner-api--000020'));
    failed!.click();
    await fixture.whenStable();

    expect(rail()).toContain('spinner-api--000020');
    expect(rail()).toContain('Failed');
  });

  it('keeps a card readable: scannable line plus a quieter source line', () => {
    const latest = rows()[0];

    expect(latest.querySelector('.project-name')?.textContent?.trim()).toBe('Spinner API');
    expect(latest.querySelector('.verdict')?.textContent).toContain('Passed');
    expect(latest.querySelector('.card-meta')?.textContent).toContain('main');
    expect(latest.querySelector('.card-meta')?.textContent).toContain('8a17c2f');
    expect(latest.querySelector('.card-meta')?.textContent).toContain('spinner-api--000021');
  });

  it('narrows to failed verifications', async () => {
    clickView('Failed');
    await fixture.whenStable();

    expect(rows().length).toBe(1);
    expect(rows()[0].textContent).toContain('spinner-api--000020');
  });

  it('narrows by environment through the shared scope', async () => {
    clickView('Staging');
    await fixture.whenStable();

    expect(TestBed.inject(EnvironmentScopeStore).scope()).toBe('staging');
    expect(rows().every((row) => row.textContent?.includes('Console Ops'))).toBe(true);
    expect(host.textContent).toContain('Showing 2 of 8 deployments');
  });

  it('searches across project, branch, commit, revision, and trigger', async () => {
    const search = host.querySelector<HTMLInputElement>('#co-deployment-search');
    search!.value = 'developer';
    search!.dispatchEvent(new Event('input'));
    await fixture.whenStable();

    expect(rows().length).toBe(2);
    expect(rows().every((row) => row.textContent?.includes('AMYL'))).toBe(true);
  });

  it('counts the verification summary from the records in view', () => {
    expect(verification()).toContain('Current deployments');
    // Five of the eight fixture records are the active deployment in their environment.
    expect(verification()).toContain('5');
    // Six of eight verdicts pass: one is Behind and one Failed, so the figure is not a round number.
    expect(verification()).toContain('75%');
    // Mean of the eight reported durations, 886s / 8.
    expect(verification()).toContain('1m 51s');
  });

  it('recounts the summary when the view narrows', async () => {
    clickView('Failed');
    await fixture.whenStable();

    expect(verification()).toContain('0%');
  });

  it('keeps deploy, redeploy, and log access unavailable', () => {
    expect(host.querySelector<HTMLButtonElement>('.deploy')?.disabled).toBe(true);
    // Two disabled actions on every card: Logs and More.
    expect(host.querySelectorAll('.card .action.is-unavailable').length).toBe(18);
  });

  it('links a recorded workflow run and never a missing one', () => {
    const workflowLinks = Array.from(
      host.querySelectorAll<HTMLAnchorElement>('.card .card-actions a'),
    );

    expect(workflowLinks[0].getAttribute('href')).toBe(
      'https://github.com/spinner/api/actions/runs/1421',
    );
    expect(workflowLinks[0].getAttribute('rel')).toBe('noopener noreferrer');
    // The two AMYL deployments have no workflow, so they contribute no link.
    expect(workflowLinks.length).toBe(6);
  });
});
