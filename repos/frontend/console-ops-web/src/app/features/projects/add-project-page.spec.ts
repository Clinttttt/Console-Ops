import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';

import { AddProjectPage } from './add-project-page';

describe('AddProjectPage', () => {
  let harness: RouterTestingHarness;
  let host: HTMLElement;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [provideRouter([{ path: 'projects/new', component: AddProjectPage }])],
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

  it('starts with nothing set instead of implying a configured project', () => {
    expect(summary()).toContain('Not set');
    expect(summary()).toContain('Complete the required fields to compose a valid registration.');
  });

  it('mirrors the entered configuration in the setup summary', async () => {
    await completeRequiredFields();
    await type('#base-url', 'https://api.spinnerapp.com');

    expect(summary()).toContain('Spinner API');
    expect(summary()).toContain('clint/spinner (main)');
    expect(summary()).toContain('Production');
    expect(summary()).toContain('https://api.spinnerapp.com');
    expect(summary()).toContain('/health');
    expect(summary()).toContain('/version');
    expect(summary()).not.toContain('Complete the required fields');
  });

  it('rejects a repository that is not owner/name', async () => {
    await type('#project-repository', 'spinner');

    expect(host.textContent).toContain('Use the form owner/name');
    expect(summary()).toContain('Complete the required fields');
  });

  it('rejects a relative base URL', async () => {
    await completeRequiredFields();
    await type('#base-url', 'api.spinnerapp.com');

    expect(host.textContent).toContain('Enter an absolute URL');
  });

  it('refuses credentials embedded in the base URL', async () => {
    await completeRequiredFields();
    await type('#base-url', 'https://user:secret@api.spinnerapp.com');

    expect(host.textContent).toContain('Remove the credentials from the URL.');
    expect(summary()).not.toContain('secret');
  });

  it('accepts an endpoint path or an absolute endpoint URL', async () => {
    await completeRequiredFields();
    await type('#health-endpoint', 'health');
    expect(host.textContent).toContain('Use a path such as /health');

    await type('#health-endpoint', 'https://api.spinnerapp.com/health');
    expect(host.textContent).not.toContain('Use a path such as /health');
  });

  it('keeps the environment name in step with the selected kind', async () => {
    const local = Array.from(host.querySelectorAll<HTMLButtonElement>('.segment')).find(
      (segment) => segment.textContent?.trim() === 'Local',
    );
    local!.click();
    harness.detectChanges();

    expect(host.querySelector<HTMLInputElement>('#environment-name')?.value).toBe('Local');
  });

  it('reports Azure as a later phase rather than claiming a connection', () => {
    const providers = host.querySelector('.providers')?.textContent ?? '';

    expect(providers).toContain('GitHub');
    expect(providers).toContain('Configured');
    expect(providers).toContain('Azure');
    expect(providers).toContain('Later phase');
    expect(providers).not.toContain('Connected');
  });

  it('keeps submission unavailable until the registration slice exists', async () => {
    await completeRequiredFields();

    const submit = host.querySelector<HTMLButtonElement>('.primary');
    expect(submit?.disabled).toBe(true);
    expect(submit?.title).toContain('POST /api/projects');
  });

  it('offers a working way back to the registry', () => {
    expect(host.querySelector<HTMLAnchorElement>('.cancel')?.getAttribute('href')).toBe(
      '/projects',
    );
  });
});
