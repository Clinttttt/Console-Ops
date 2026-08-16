import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';

import { LogStreamDataSource } from '../../core/data/log-stream.data-source';
import { LOG_STREAM_FIXTURE } from '../../core/data/mock/log-stream.fixture';
import { LogsPage } from './logs-page';

describe('LogsPage', () => {
  let fixture: ComponentFixture<LogsPage>;
  let host: HTMLElement;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [LogsPage],
      providers: [
        provideRouter([]),
        {
          provide: LogStreamDataSource,
          useValue: { load: () => of(LOG_STREAM_FIXTURE) },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(LogsPage);
    await fixture.whenStable();
    host = fixture.nativeElement as HTMLElement;
  });

  function lines(): HTMLElement[] {
    return Array.from(host.querySelectorAll('co-log-stream .line'));
  }

  function lineFor(text: string): HTMLElement | undefined {
    return lines().find((line) => line.textContent?.includes(text));
  }

  function markers(): string[] {
    return Array.from(host.querySelectorAll('co-log-marker')).map(
      (marker) => marker.textContent?.replace(/\s+/g, ' ').trim() ?? '',
    );
  }

  function detail(): string {
    return host.querySelector('co-log-detail')?.textContent?.replace(/\s+/g, ' ').trim() ?? '';
  }

  function clickLevel(label: string): void {
    Array.from(host.querySelectorAll<HTMLButtonElement>('.level'))
      .find((button) => button.textContent?.trim() === label)
      ?.click();
  }

  it('says plainly that the screen is not observed state', () => {
    expect(host.querySelector('.preview-notice')).not.toBeNull();
    expect(host.textContent).toContain('Sample data');
    expect(host.textContent).toContain('does not ingest logs yet');
  });

  it('reads chronologically, oldest first, like a terminal', () => {
    const times = lines().map((line) => line.querySelector('.time')?.textContent?.trim());

    expect(times[0]).toBe('23:52:11.997');
    expect(times.at(-1)).toBe('23:54:51.006');
    expect(host.querySelector('.day-label')?.textContent).toContain('Aug 15, 2026');
  });

  it('keeps a line scannable: time, severity, source, message, and a short outcome', () => {
    const line = lineFor('GET /health completed');

    expect(line?.querySelector('.level')?.textContent?.trim()).toBe('INF');
    expect(line?.querySelector('.source')?.textContent?.trim()).toBe('HTTP');
    expect(line?.querySelector('.outcome')?.textContent?.trim()).toBe('200 · 7 ms');
    // Detail belongs behind selection, never in the stream.
    expect(line?.textContent).not.toContain('req_01HX7V5P4C6Q7R2S8T9U1V0X2');
    expect(line?.textContent).not.toContain('HttpRequestException');
  });

  it('falls back to the most telling property when nothing else was reported', () => {
    expect(lineFor('Container initialized')?.querySelector('.outcome')?.textContent).toContain(
      'Image spinner-api:1.23.4',
    );
    // An event with neither an outcome nor properties reads as nothing rather than a zero.
    expect(
      lineFor('Starting initialization')
        ?.querySelector('.outcome')
        ?.classList.contains('co-unavailable'),
    ).toBe(true);
  });

  it('shows deployment and revision markers as context, linked to the release', () => {
    expect(markers()[0]).toContain('Revision started 8a17c2f');
    expect(markers()[0]).toContain('spinner-api--000021');

    const deployment = markers()[1];
    expect(deployment).toContain('Deployment 9047c89');
    expect(deployment).toContain('View release');
    const link = host.querySelector<HTMLAnchorElement>('co-log-marker a');
    expect(link?.getAttribute('href')).toBe('/deployments');
  });

  it('opens no detail until an event is chosen', () => {
    expect(detail()).toContain('Select an event to inspect it');
    expect(host.querySelector('co-log-stream .line.is-selected')).toBeNull();
  });

  it('describes the chosen event, including correlation and structured properties', async () => {
    lineFor('Provider request required a retry')!.click();
    await fixture.whenStable();

    expect(detail()).toContain('Warning');
    expect(detail()).toContain('Payments');
    expect(detail()).toContain('Provider request required a retry');
    expect(detail()).toContain('4f2b9c7e6d1a4c7bb8e2f9a1d3b6c8e0');
    expect(detail()).toContain('req_01HX7V5P4C6Q7R2S8T9U1V0WZ');
    // Structured logging is the point: the template and its values are both shown.
    expect(detail()).toContain('Provider request required a retry, attempt {Attempt}');
    expect(detail()).toContain('Attempt');
    expect(detail()).toContain('1.84 s');
  });

  it('keeps a stack trace collapsed until asked for, and never in the stream', async () => {
    lineFor('Payment provider returned an error')!.click();
    await fixture.whenStable();

    expect(detail()).toContain('HttpRequestException');
    expect(host.querySelector('.stack-trace')).toBeNull();

    host.querySelector<HTMLButtonElement>('.trace-toggle')!.click();
    await fixture.whenStable();

    expect(host.querySelector('.stack-trace')?.textContent).toContain(
      'at Spinner.Payments.ProviderClient.ChargeAsync',
    );
  });

  it('says when no stack trace was captured rather than showing an empty block', async () => {
    lineFor('Connection attempt timed out')!.click();
    await fixture.whenStable();

    expect(detail()).toContain('NpgsqlException');
    expect(detail()).toContain('No stack trace was captured');
    expect(host.querySelector('.trace-toggle')).toBeNull();
  });

  it('reports facts the event did not carry as unknown', async () => {
    lineFor('Starting initialization')!.click();
    await fixture.whenStable();

    expect(detail()).toContain('Not provided');
    expect(detail()).toContain('Not reported');
  });

  it('dismisses the detail without changing the stream', async () => {
    const before = lines().length;
    lineFor('Charge authorized')!.click();
    await fixture.whenStable();
    host.querySelector<HTMLButtonElement>('.dismiss')!.click();
    await fixture.whenStable();

    expect(detail()).toContain('Select an event to inspect it');
    expect(lines().length).toBe(before);
  });

  it('narrows by severity and drops markers that would explain nothing', async () => {
    clickLevel('ERR');
    await fixture.whenStable();

    expect(lines().length).toBe(2);
    expect(
      Array.from(host.querySelectorAll('.line-item')).every(
        (item) => item.getAttribute('data-level') === 'error',
      ),
    ).toBe(true);
    expect(markers().length).toBe(0);
    expect(host.textContent).toContain('2 errors');
  });

  it('colours each dot with the shared status level rather than the log level', () => {
    const dotFor = (text: string): string | null =>
      lineFor(text)?.querySelector('.co-dot')?.getAttribute('data-level') ?? null;

    // `.co-dot` speaks in operational levels, so an untranslated `info` would render as unknown grey.
    expect(dotFor('GET /health completed')).toBe('healthy');
    expect(dotFor('Provider request required a retry')).toBe('warning');
    expect(dotFor('Payment provider returned an error')).toBe('down');
  });

  it('narrows by source kind', async () => {
    const select = host.querySelector<HTMLSelectElement>('#co-log-source');
    select!.value = 'runtime';
    select!.dispatchEvent(new Event('change'));
    await fixture.whenStable();

    expect(lines().length).toBe(4);
    expect(lines().every((line) => line.textContent?.includes('Runtime'))).toBe(true);
  });

  it('searches messages, correlation ids, and property values', async () => {
    const search = host.querySelector<HTMLInputElement>('#co-log-search');
    search!.value = '2048';
    search!.dispatchEvent(new Event('input'));
    await fixture.whenStable();

    // Reaches events whose only mention of the order is a structured property.
    expect(lines().length).toBe(3);
    expect(host.textContent).toContain('Showing 3 of 12 events');
  });

  it('offers an empty view a way back rather than a dead end', async () => {
    const search = host.querySelector<HTMLInputElement>('#co-log-search');
    search!.value = 'nothing matches this';
    search!.dispatchEvent(new Event('input'));
    await fixture.whenStable();

    expect(host.textContent).toContain('No events match this view');
    host.querySelector<HTMLButtonElement>('.reset')!.click();
    await fixture.whenStable();

    expect(lines().length).toBeGreaterThan(0);
  });

  it('pauses the stream so a line can be read without it moving', async () => {
    expect(host.textContent).toContain('Following new events');

    host.querySelector<HTMLButtonElement>('.live')!.click();
    await fixture.whenStable();

    expect(host.textContent).toContain('Paused');
    expect(host.textContent).not.toContain('Following new events');
  });

  it('scopes the stream from one field with one dropdown', () => {
    const scope = host.querySelector('.scope');
    const selects = scope!.querySelectorAll('select');

    // One choice, made once: a second dropdown in the same field would ask for it twice.
    expect(selects.length).toBe(1);
    expect(selects[0].id).toBe('co-log-scope');
    expect(scope?.querySelector('.project')?.textContent?.trim()).toBe('Spinner API');
    expect(scope?.querySelector('co-environment-tag')?.textContent?.trim()).toBe('Production');
  });

  it('lists the project and environment pairs, and reads back the chosen one', async () => {
    const scope = host.querySelector<HTMLSelectElement>('#co-log-scope');
    expect(Array.from(scope!.options).map((option) => option.textContent?.trim())).toEqual([
      'Spinner API / Production',
      'Spinner API / Staging',
      'StallTrack / Production',
    ]);

    scope!.value = 'project-spinner:env-staging';
    scope!.dispatchEvent(new Event('change'));
    await fixture.whenStable();

    expect(host.querySelector('.project')?.textContent?.trim()).toBe('Spinner API');
    expect(host.querySelector('co-environment-tag')?.textContent?.trim()).toBe('Staging');
  });

  it('follows a change of project as well as environment', async () => {
    const scope = host.querySelector<HTMLSelectElement>('#co-log-scope');
    scope!.value = 'project-stalltrack:env-stalltrack-production';
    scope!.dispatchEvent(new Event('change'));
    await fixture.whenStable();

    expect(host.querySelector('.project')?.textContent?.trim()).toBe('StallTrack');
    expect(host.querySelector('co-environment-tag')?.textContent?.trim()).toBe('Production');
  });

  it('carries none of the state that already has a home', () => {
    // No release history, no health summary, no version sync, no uptime on this screen.
    expect(host.textContent).not.toContain('Version sync');
    expect(host.textContent).not.toContain('Uptime');
    expect(host.textContent).not.toContain('In Sync');
    expect(host.querySelector('co-deployment-timeline')).toBeNull();
  });
});
