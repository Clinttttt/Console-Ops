import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';

import { LogEvent, LogStream } from '../../core/contracts/log-stream';
import { LogStreamDataSource } from '../../core/data/log-stream.data-source';
import { LogStreamStore } from '../../core/state/log-stream.store';
import { LogsPage } from './logs-page';

/**
 * A payload captured verbatim from `GET /api/logs` against a real Azure workspace, parsed at runtime so
 * TypeScript cannot quietly supply a field the server does not send.
 *
 * This exists because hand-written spec objects hid a real defect: the page selects items on the `kind`
 * discriminator, the API omitted it, and every event was silently dropped - green tests, blank screen. A
 * test that renders the wire shape is the only one that could have caught it.
 */
const CAPTURED_PAYLOAD = `{
  "observedAt": "2026-08-16T11:16:36.3308693+00:00",
  "scopes": [
    {
      "projectId": "01a00172-257b-70b8-b8e9-47cad8333adc",
      "projectName": "Spinner",
      "environment": { "id": "01a00172-257a-78d4-bcae-a57b279d84d3", "name": "Production", "kind": "production" },
      "provider": "azureContainerApps"
    }
  ],
  "scope": {
    "projectId": "01a00172-257b-70b8-b8e9-47cad8333adc",
    "projectName": "Spinner",
    "environment": { "id": "01a00172-257a-78d4-bcae-a57b279d84d3", "name": "Production", "kind": "production" },
    "provider": "azureContainerApps"
  },
  "window": {
    "from": "2026-08-15T11:16:36.3308693+00:00",
    "to": "2026-08-16T11:16:36.3308693+00:00",
    "hours": 24,
    "truncated": true
  },
  "items": [
    {
      "kind": "event",
      "id": "57017fe2adf97723f6ba533a",
      "occurredAt": "2026-08-16T11:16:06.4285904+00:00",
      "receivedAt": "2026-08-16T11:16:07.7157678+00:00",
      "level": "information",
      "levelIsDerived": true,
      "source": "Microsoft.EntityFrameworkCore.Database.Command",
      "sourceKind": "application",
      "message": "Executed DbCommand (3ms) [CommandType='Text', CommandTimeout='30']",
      "stackTrace": null,
      "stream": "stdout",
      "revision": "spinner-api-stg--0000043",
      "host": "spinner-api-stg--0000043-59c74869b7-hn8m7"
    },
    {
      "kind": "event",
      "id": "1f74e21117668e7e318ff0ea",
      "occurredAt": "2026-08-16T11:08:06.2313863+00:00",
      "receivedAt": "2026-08-16T11:08:06.9593146+00:00",
      "level": "unknown",
      "levelIsDerived": false,
      "source": null,
      "sourceKind": "application",
      "message": "SELECT n.\\"Id\\", n.\\"AttemptCount\\"",
      "stackTrace": "          FROM \\"NotificationOutbox\\" AS n",
      "stream": "stdout",
      "revision": "spinner-api-stg--0000043",
      "host": "spinner-api-stg--0000043-59c74869b7-hn8m7"
    }
  ]
}`;

describe('LogsPage against the captured wire payload', () => {
  let fixture: ComponentFixture<LogsPage>;
  let host: HTMLElement;

  beforeEach(async () => {
    const payload = JSON.parse(CAPTURED_PAYLOAD) as LogStream;

    await TestBed.configureTestingModule({
      imports: [LogsPage],
      providers: [
        provideRouter([]),
        { provide: LogStreamDataSource, useValue: { load: () => of(payload) } },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(LogsPage);
    await fixture.whenStable();
    host = fixture.nativeElement as HTMLElement;
  });

  it('renders the events the API actually returns', () => {
    const lines = Array.from(host.querySelectorAll('co-log-stream .line'));

    expect(lines.length).toBe(2);
    expect(host.textContent).not.toContain('No events match this view');
    // Oldest first, so the stream reads like a terminal and new lines land at the bottom.
    expect(lines[0].textContent).toContain('SELECT n."Id"');
    expect(lines[1].textContent).toContain('Executed DbCommand');
  });

  it('reads the fields off the wire shape without reinterpreting them', () => {
    const lines = Array.from(host.querySelectorAll('co-log-stream .line'));

    expect(lines[1].querySelector('.level')?.textContent?.trim()).toBe('INF');
    // Categories are namespaces. The scannable column keeps the end, which is what tells emitters apart,
    // and the full value stays on the line's tooltip and in the detail rail.
    expect(lines[1].querySelector('.source')?.textContent?.trim()).toBe('Database.Command');
    expect(lines[1].querySelector('.source')?.getAttribute('title')).toBe(
      'Microsoft.EntityFrameworkCore.Database.Command',
    );
    // A continuation line with no prefix stays unclaimed, exactly as the provider left it.
    expect(lines[0].querySelector('.level')?.textContent?.trim()).toBe('LOG');
    expect(lines[0].querySelector('.source')?.classList.contains('co-unavailable')).toBe(true);
    expect(host.textContent).toContain('last 24h');
    expect(host.textContent).toContain('window holds more');
  });
});

/**
 * The same wire shape with the two markers the API composes: a recorded release, and a revision change the
 * log rows themselves reported. Both are pinned server-side by GetLogStreamTests; these tests cover what
 * the screen does with them.
 */
const MARKED_PAYLOAD = CAPTURED_PAYLOAD.replace(
  '"items": [',
  `"items": [
    {
      "kind": "marker",
      "id": "revision-spinner-api-stg--0000044-57017fe2adf97723f6ba533a",
      "occurredAt": "2026-08-16T11:16:06.4285904+00:00",
      "markerKind": "revision",
      "commitShortSha": null,
      "revision": "spinner-api-stg--0000044",
      "deploymentId": null
    },
    {
      "kind": "marker",
      "id": "deployment-8f14e45fceea167a5a36dedd4bea2543",
      "occurredAt": "2026-08-16T11:12:00.0000000+00:00",
      "markerKind": "deployment",
      "commitShortSha": "0f1e2d3",
      "revision": null,
      "deploymentId": "8f14e45f-ceea-167a-5a36-dedd4bea2543"
    },`,
);

describe('LogsPage markers', () => {
  let fixture: ComponentFixture<LogsPage>;
  let host: HTMLElement;

  beforeEach(async () => {
    const payload = JSON.parse(MARKED_PAYLOAD) as LogStream;

    await TestBed.configureTestingModule({
      imports: [LogsPage],
      providers: [
        provideRouter([]),
        { provide: LogStreamDataSource, useValue: { load: () => of(payload) } },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(LogsPage);
    await fixture.whenStable();
    host = fixture.nativeElement as HTMLElement;
  });

  it('shows a recorded release inline, with a way back to it', () => {
    const marker = Array.from(host.querySelectorAll('co-log-marker')).find((element) =>
      element.textContent?.includes('Deployment'),
    );

    expect(marker?.textContent).toContain('0f1e2d3');
    expect(marker?.querySelector('a')?.getAttribute('href')).toBe('/deployments');
  });

  it('names the revision it observed taking over', () => {
    const marker = Array.from(host.querySelectorAll('co-log-marker')).find((element) =>
      element.textContent?.includes('Revision first seen'),
    );

    // "First seen" rather than "started": console output shows which revision emitted a line, not when
    // that revision began serving, and during a rollout both revisions log at once.
    expect(marker?.textContent).toContain('spinner-api-stg--0000044');
  });

  it('keeps markers in their place in the stream rather than in a separate list', () => {
    const items = Array.from(
      host.querySelectorAll('co-log-stream .line, co-log-stream co-log-marker'),
    );

    // Oldest first: the older line, the release that followed it, then the newer line under its revision.
    expect(items.map((item) => item.tagName.toLowerCase() === 'co-log-marker')).toEqual([
      false,
      true,
      true,
      false,
    ]);
  });

  it('drops markers when the events they explain are filtered away', () => {
    Array.from(host.querySelectorAll<HTMLButtonElement>('.level'))
      .find((button) => button.textContent?.trim() === 'ERR')
      ?.click();
    fixture.detectChanges();

    // A marker with nothing left to explain would be context without a subject.
    expect(host.querySelectorAll('co-log-marker').length).toBe(0);
    expect(host.textContent).toContain('No events match this view');
  });
});

describe('LogsPage paging backwards', () => {
  let fixture: ComponentFixture<LogsPage>;
  let host: HTMLElement;
  let requests: (string | null)[];

  /** A page of two events ending at the given instant, ids derived so pages do not collide. */
  function page(endingAt: string): LogStream {
    const base = JSON.parse(CAPTURED_PAYLOAD) as LogStream;
    const minute = endingAt.slice(11, 16);
    return {
      ...base,
      window: { ...base.window, to: endingAt, truncated: true },
      items: [
        { ...(base.items[0] as LogEvent), id: `newer-${minute}`, occurredAt: endingAt },
        {
          ...(base.items[1] as LogEvent),
          id: `older-${minute}`,
          occurredAt: `${endingAt.slice(0, 14)}00:00.0000000+00:00`,
        },
      ],
    };
  }

  beforeEach(async () => {
    requests = [];
    const pages = new Map<string | null, LogStream>([
      [null, page('2026-08-16T11:30:00.0000000+00:00')],
      ['2026-08-16T11:00:00.0000000+00:00', page('2026-08-16T10:30:00.0000000+00:00')],
    ]);

    await TestBed.configureTestingModule({
      imports: [LogsPage],
      providers: [
        provideRouter([]),
        {
          provide: LogStreamDataSource,
          useValue: {
            load: (request: { before: string | null }) => {
              requests.push(request.before);
              // An unknown cursor means the window before it held nothing, which is where paging stops.
              return of(pages.get(request.before) ?? { ...page('x'), items: [] });
            },
          },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(LogsPage);
    await fixture.whenStable();
    host = fixture.nativeElement as HTMLElement;
  });

  function lineCount(): number {
    return host.querySelectorAll('co-log-stream .line').length;
  }

  function loadOlder(): HTMLButtonElement | null {
    return host.querySelector<HTMLButtonElement>('.older-action');
  }

  it('asks for the window before the oldest line it holds', async () => {
    expect(lineCount()).toBe(2);
    expect(requests).toEqual([null]);

    loadOlder()!.click();
    await fixture.whenStable();

    // The cursor is the oldest line's own instant, not a page number the provider knows nothing about.
    expect(requests).toEqual([null, '2026-08-16T11:00:00.0000000+00:00']);
  });

  it('keeps what was already read instead of replacing it', async () => {
    loadOlder()!.click();
    await fixture.whenStable();

    expect(lineCount()).toBe(4);
    const times = Array.from(host.querySelectorAll('co-log-stream .time')).map((element) =>
      element.textContent?.trim(),
    );
    // Oldest first, so a page read backwards lands above what was already on screen.
    expect(times).toEqual([...times].sort());
  });

  it('says when there is nothing older rather than offering the page again', async () => {
    loadOlder()!.click();
    await fixture.whenStable();
    loadOlder()!.click();
    await fixture.whenStable();

    expect(loadOlder()).toBeNull();
    expect(host.textContent).toContain('Nothing older in the day before this point');
    // The empty page changed nothing that was already read.
    expect(lineCount()).toBe(4);
  });

  it('survives a re-read of the newest window, so scrolling back is not undone', async () => {
    loadOlder()!.click();
    await fixture.whenStable();
    expect(lineCount()).toBe(4);

    // What the 30-second refresh does. Replacing here would throw away the pages the operator asked for.
    TestBed.inject(LogStreamStore).refresh();
    await fixture.whenStable();

    expect(lineCount()).toBe(4);
  });
});
