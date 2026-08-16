import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Observable, of, throwError } from 'rxjs';

import { LogEvent, LogStream, LogStreamScope } from '../../core/contracts/log-stream';
import { LogStreamDataSource, LogStreamRequest } from '../../core/data/log-stream.data-source';
import { LogsPage } from './logs-page';

const OBSERVED_AT = '2026-08-16T10:30:00.000Z';

const SPINNER: LogStreamScope = {
  projectId: 'project-spinner',
  projectName: 'Spinner',
  environment: { id: 'env-production', name: 'Production', kind: 'production' },
  provider: 'azureContainerApps',
};

const STALLTRACK: LogStreamScope = {
  projectId: 'project-stalltrack',
  projectName: 'StallTrack',
  environment: { id: 'env-staging', name: 'Staging', kind: 'staging' },
  provider: 'azureContainerApps',
};

function event(overrides: Partial<LogEvent>): LogEvent {
  return {
    kind: 'event',
    id: 'event-1',
    occurredAt: '2026-08-16T10:25:05.930Z',
    receivedAt: '2026-08-16T10:25:06.930Z',
    level: 'information',
    levelIsDerived: true,
    source: 'Microsoft.EntityFrameworkCore.Database.Command',
    sourceKind: 'application',
    message: 'Executed DbCommand (3ms)',
    stackTrace: null,
    stream: 'stdout',
    revision: 'spinner-api-stg--0000043',
    host: 'spinner-api-stg-abc123',
    ...overrides,
  };
}

const STREAM: LogStream = {
  observedAt: OBSERVED_AT,
  scopes: [SPINNER, STALLTRACK],
  scope: SPINNER,
  window: { from: '2026-08-15T10:30:00.000Z', to: OBSERVED_AT, hours: 24, truncated: false },
  items: [
    event({}),
    event({
      id: 'event-2',
      occurredAt: '2026-08-16T10:26:11.100Z',
      level: 'warning',
      message: 'Provider request required a retry',
      source: 'Spinner.Payments',
    }),
    event({
      id: 'event-3',
      occurredAt: '2026-08-16T10:27:02.500Z',
      level: 'error',
      message: 'Payment provider returned an error',
      source: 'Spinner.Payments',
      stream: 'stderr',
      stackTrace: 'at Spinner.Payments.ProviderClient.ChargeAsync()',
    }),
    // A plain line of output: no prefix, so no level and no category were established.
    event({
      id: 'event-4',
      occurredAt: '2026-08-16T10:28:00.000Z',
      level: 'unknown',
      levelIsDerived: false,
      source: null,
      message: 'Now listening on: http://[::]:8080',
      receivedAt: null,
    }),
  ],
};

describe('LogsPage', () => {
  let fixture: ComponentFixture<LogsPage>;
  let host: HTMLElement;
  let requests: LogStreamRequest[];

  async function render(
    load: (request: LogStreamRequest) => Observable<LogStream> = () => of(STREAM),
  ): Promise<void> {
    requests = [];
    TestBed.resetTestingModule();
    await TestBed.configureTestingModule({
      imports: [LogsPage],
      providers: [
        provideRouter([]),
        {
          provide: LogStreamDataSource,
          useValue: {
            load: (request: LogStreamRequest) => {
              requests.push(request);
              return load(request);
            },
          },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(LogsPage);
    await fixture.whenStable();
    host = fixture.nativeElement as HTMLElement;
  }

  function lines(): HTMLElement[] {
    return Array.from(host.querySelectorAll('co-log-stream .line'));
  }

  function lineFor(text: string): HTMLElement | undefined {
    return lines().find((line) => line.textContent?.includes(text));
  }

  function detail(): string {
    return host.querySelector('co-log-detail')?.textContent?.replace(/\s+/g, ' ').trim() ?? '';
  }

  function clickLevel(label: string): void {
    Array.from(host.querySelectorAll<HTMLButtonElement>('.level'))
      .find((button) => button.textContent?.trim() === label)
      ?.click();
  }

  it('reads real logs and no longer claims to be sample data', async () => {
    await render();

    expect(host.querySelector('.preview-notice')).toBeNull();
    expect(host.textContent).not.toContain('Sample data');
    expect(lines().length).toBe(4);
  });

  it('states the window it read, because the provider keeps the logs', async () => {
    await render();

    expect(host.textContent).toContain('last 24h');
    expect(host.textContent).toContain('Showing 4 of 4 events');
    expect(host.textContent).toContain('1 error');
  });

  it('says the window holds more when the row cap cut the result', async () => {
    await render(() => of({ ...STREAM, window: { ...STREAM.window, truncated: true } }));

    expect(host.textContent).toContain('window holds more');
  });

  it('labels each severity, and leaves an unparsed line unclaimed', async () => {
    await render();

    expect(lineFor('Executed DbCommand')?.querySelector('.level')?.textContent?.trim()).toBe('INF');
    expect(lineFor('required a retry')?.querySelector('.level')?.textContent?.trim()).toBe('WRN');
    expect(lineFor('returned an error')?.querySelector('.level')?.textContent?.trim()).toBe('ERR');
    // A plain line of output is not information: it reads as a log line with no severity.
    const plain = lineFor('Now listening on');
    expect(plain?.querySelector('.level')?.textContent?.trim()).toBe('LOG');
    expect(plain?.querySelector('.co-dot')?.getAttribute('data-level')).toBe('unknown');
    expect(plain?.querySelector('.source')?.classList.contains('co-unavailable')).toBe(true);
  });

  it('marks a line written to standard error', async () => {
    await render();

    expect(lineFor('returned an error')?.querySelector('.outcome')?.textContent?.trim()).toBe(
      'stderr',
    );
  });

  it('opens no detail until an event is chosen', async () => {
    await render();

    expect(detail()).toContain('Select an event to inspect it');
  });

  it('says a parsed severity was read from the line rather than declared', async () => {
    await render();
    lineFor('required a retry')!.click();
    await fixture.whenStable();

    expect(detail()).toContain('Warning');
    expect(detail()).toContain('read from the line');
    expect(detail()).toContain('spinner-api-stg--0000043');
    // Console output has no correlation ids, and the rail says so instead of showing empty fields.
    expect(detail()).toContain('no trace or request id');
  });

  it('keeps folded continuation lines collapsed until asked for', async () => {
    await render();
    lineFor('returned an error')!.click();
    await fixture.whenStable();

    expect(host.querySelector('.stack-trace')).toBeNull();
    host.querySelector<HTMLButtonElement>('.trace-toggle')!.click();
    await fixture.whenStable();

    expect(host.querySelector('.stack-trace')?.textContent).toContain('ProviderClient.ChargeAsync');
  });

  it('reports facts the line did not carry as unknown', async () => {
    await render();
    lineFor('Now listening on')!.click();
    await fixture.whenStable();

    expect(detail()).toContain('No category in the line');
    expect(detail()).toContain('Not reported');
  });

  it('narrows severity over the fetched lines, without asking the provider again', async () => {
    await render();
    const before = requests.length;

    clickLevel('ERR');
    await fixture.whenStable();

    expect(lines().length).toBe(1);
    expect(lines()[0].textContent).toContain('returned an error');
    // Severity is a property of lines already on screen, so no new provider read is needed.
    expect(requests.length).toBe(before);
  });

  it('pushes the search down to the provider on submit, not on every keystroke', async () => {
    await render();
    const search = host.querySelector<HTMLInputElement>('#co-log-search')!;

    search.value = 'payment';
    search.dispatchEvent(new Event('input'));
    await fixture.whenStable();
    // Typing must not query: a window can hold far more lines than one page.
    expect(requests.at(-1)?.search).toBeNull();

    search.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter' }));
    await fixture.whenStable();

    expect(requests.at(-1)?.search).toBe('payment');
  });

  it('asks the provider again when the scope changes', async () => {
    await render();
    const scope = host.querySelector<HTMLSelectElement>('#co-log-scope')!;

    scope.value = 'project-stalltrack:env-staging';
    scope.dispatchEvent(new Event('change'));
    await fixture.whenStable();

    expect(requests.at(-1)?.projectId).toBe('project-stalltrack');
    expect(requests.at(-1)?.environmentId).toBe('env-staging');
  });

  it('explains a missing log source instead of showing an empty stream', async () => {
    await render(() =>
      throwError(() => ({ status: 404, error: { code: 'Logs.NoLogSourceConfigured' } })),
    );

    expect(host.textContent).toContain('No environment has a log source configured');
    expect(lines().length).toBe(0);
  });

  it('names a rejected Azure identity rather than blaming the window', async () => {
    await render(() => throwError(() => ({ status: 500, error: { code: 'Logs.Unauthorized' } })));

    expect(host.textContent).toContain('Azure rejected the identity');
    expect(host.textContent).toContain('az login');
  });

  it('distinguishes a failed read from a window that held nothing', async () => {
    await render(() => throwError(() => ({ status: 500, error: { code: 'Logs.Unavailable' } })));

    expect(host.textContent).toContain('This is not the same as no events');
  });

  it('carries none of the state that already has a home', async () => {
    await render();

    expect(host.textContent).not.toContain('Version sync');
    expect(host.textContent).not.toContain('Uptime');
    expect(host.querySelector('co-deployment-timeline')).toBeNull();
  });
});
