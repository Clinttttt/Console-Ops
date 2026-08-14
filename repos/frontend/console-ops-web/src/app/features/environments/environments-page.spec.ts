import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';

import { EnvironmentRegistryDataSource } from '../../core/data/environment-registry.data-source';
import { ENVIRONMENT_REGISTRY_FIXTURE } from '../../core/data/mock/environment-registry.fixture';
import { EnvironmentScopeStore } from '../../core/state/environment-scope.store';
import { EnvironmentsPage } from './environments-page';

describe('EnvironmentsPage', () => {
  let fixture: ComponentFixture<EnvironmentsPage>;
  let host: HTMLElement;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [EnvironmentsPage],
      providers: [
        {
          provide: EnvironmentRegistryDataSource,
          useValue: { load: () => of(ENVIRONMENT_REGISTRY_FIXTURE) },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(EnvironmentsPage);
    await fixture.whenStable();
    host = fixture.nativeElement as HTMLElement;
  });

  function cards(): HTMLElement[] {
    return Array.from(host.querySelectorAll('co-environment-groups .card'));
  }

  function cardProjects(): (string | undefined)[] {
    return cards().map((card) => card.querySelector('.project-name')?.textContent?.trim());
  }

  function groupHeadings(): (string | undefined)[] {
    return Array.from(host.querySelectorAll('.group-head')).map((heading) =>
      heading.textContent?.replace(/\s+/g, ' ').trim(),
    );
  }

  function rail(): string {
    return host.querySelector('co-selected-environment')?.textContent ?? '';
  }

  function clickView(label: string): void {
    Array.from(host.querySelectorAll<HTMLButtonElement>('.view'))
      .find((button) => button.textContent?.trim() === label)
      ?.click();
  }

  it('groups environments by kind and counts each group', () => {
    // No development environment is configured, so that group does not appear at all.
    expect(groupHeadings()).toEqual([
      'Production 3 environments',
      'Staging 1 environment',
      'Local 1 environment',
    ]);
    expect(cardProjects()).toEqual([
      'Spinner API',
      'StallTrack',
      'StockPilot',
      'Console Ops',
      'AMYL',
    ]);
    expect(host.textContent).not.toContain('Legacy Billing');
  });

  it('shows runtime, health, and version sync on one line per environment', () => {
    const spinner = cards()[0];

    expect(spinner.textContent).toContain('Production');
    expect(spinner.textContent).toContain('Azure Container Apps');
    expect(spinner.textContent).toContain('Healthy');
    expect(spinner.textContent).toContain('In Sync');
    expect(spinner.textContent).toContain('18 min ago');
  });

  it('highlights nothing and describes nothing until an environment is chosen', () => {
    expect(host.querySelectorAll('.card.is-selected').length).toBe(0);
    expect(rail()).toContain('Select an environment to inspect its configuration.');
  });

  it('describes the chosen environment and highlights only that card', async () => {
    cards()[0].click();
    await fixture.whenStable();

    expect(host.querySelectorAll('.card.is-selected').length).toBe(1);
    expect(rail()).toContain('Spinner API / Production');
    expect(rail()).toContain('spinner-api--000021');
    expect(rail()).toContain('5 / 5 configured');
    expect(rail()).toContain('14 sec ago');
  });

  it('reports a local environment without inventing version sync or a deployment', () => {
    const amyl = cards()[4];

    expect(amyl.textContent).toContain('Docker Desktop');
    expect(amyl.textContent).toContain('Running');
    expect(amyl.textContent).toContain('Not configured');
    expect(amyl.querySelector('.deployed')?.textContent?.trim()).toBe('—');
  });

  it('reports unknown revision and unchecked configuration honestly', async () => {
    cards()[4].click();
    await fixture.whenStable();

    expect(rail()).toContain('AMYL / Local');
    expect(rail()).toContain('Unknown');
    expect(rail()).toContain('Not checked');
  });

  it('narrows to a single group through the shared scope', async () => {
    clickView('Staging');
    await fixture.whenStable();

    expect(groupHeadings()).toEqual(['Staging 1 environment']);
    expect(cardProjects()).toEqual(['Console Ops']);
    expect(TestBed.inject(EnvironmentScopeStore).scope()).toBe('staging');
    expect(host.textContent).toContain('Showing 1 of 5 environments');
  });

  it('clears a selection that the filters removed', async () => {
    cards()[0].click();
    await fixture.whenStable();
    expect(rail()).toContain('Spinner API / Production');

    clickView('Local');
    await fixture.whenStable();

    expect(host.querySelectorAll('.card.is-selected').length).toBe(0);
    expect(rail()).toContain('Select an environment');
  });

  it('filters by project', async () => {
    const select = host.querySelector<HTMLSelectElement>('#co-environment-project');
    select!.value = 'amyl';
    select!.dispatchEvent(new Event('change'));
    await fixture.whenStable();

    expect(cardProjects()).toEqual(['AMYL']);
  });

  it('searches across project, environment, runtime, and URL', async () => {
    const search = host.querySelector<HTMLInputElement>('#co-environment-search');
    search!.value = 'docker';
    search!.dispatchEvent(new Event('input'));
    await fixture.whenStable();

    expect(cardProjects()).toEqual(['AMYL']);
  });

  it('shows archived environments as no longer monitored', async () => {
    host.querySelector<HTMLButtonElement>('.list-footer .co-inline-link')!.click();
    await fixture.whenStable();

    expect(cardProjects()).toEqual(['Legacy Billing']);
    expect(cards()[0].textContent).toContain('Unknown');
    expect(cards()[0].textContent).toContain('Not configured');
  });

  it('opens a configured application in a new tab and never a missing one', async () => {
    const open = host.querySelector<HTMLAnchorElement>('.actions a.icon-action');

    expect(open?.getAttribute('href')).toBe('https://api.spinnerapp.com');
    expect(open?.getAttribute('rel')).toBe('noopener noreferrer');

    host.querySelector<HTMLButtonElement>('.list-footer .co-inline-link')!.click();
    await fixture.whenStable();

    expect(host.querySelector('.actions a.icon-action')).toBeNull();
  });

  it('keeps environment creation and editing unavailable', () => {
    expect(host.querySelector<HTMLButtonElement>('.add')?.disabled).toBe(true);
    expect(host.querySelectorAll('.card .icon-action.is-unavailable').length).toBe(5);
  });
});
