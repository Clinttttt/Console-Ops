import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
  untracked,
} from '@angular/core';

import { LogEvent, LogStreamItem, LogStreamScope } from '../../core/contracts/log-stream';
import { autoRefresh } from '../../core/state/auto-refresh';
import { LogStreamStore } from '../../core/state/log-stream.store';
import { LogFilters, LogLevelFilter, LogSourceFilter } from './components/log-filters';
import { LogDetail } from './components/log-detail';
import { LogStreamView } from './components/log-stream';

/**
 * Logs screen: the forensic workspace.
 *
 * It answers what the application and its runtime actually said around the time something happened, and
 * deliberately carries none of the state that already has a home. Project configuration, release history,
 * environment configuration, and health belong to their own screens; the only cross-screen material here is
 * the lightweight markers that explain a change in the stream.
 *
 * The scope and the free-text search are pushed down to the provider, because a 24-hour window can hold far
 * more lines than one page: filtering locally would quietly hide matches further back in the window.
 * Severity and source-kind narrow what is already on screen, which is honest because they are properties of
 * the fetched lines.
 */
@Component({
  selector: 'co-logs-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [LogDetail, LogFilters, LogStreamView],
  templateUrl: './logs-page.html',
  styleUrl: './logs-page.scss',
})
export class LogsPage {
  private readonly store = inject(LogStreamStore);

  protected readonly loadState = this.store.loadState;
  protected readonly failureReason = this.store.failureReason;
  protected readonly observedAt = this.store.observedAt;
  protected readonly scopes = this.store.scopes;
  protected readonly scope = this.store.scope;
  protected readonly window = this.store.window;

  protected readonly query = signal('');
  protected readonly level = signal<LogLevelFilter>(null);
  protected readonly source = signal<LogSourceFilter>(null);
  protected readonly live = signal(true);
  private readonly requestedScopeId = signal<string | null>(null);
  private readonly submittedQuery = signal('');

  /** Nothing is selected until an event is chosen: a panel nobody opened describes an arbitrary line. */
  private readonly selectedId = signal<string | null>(null);

  protected readonly scopeId = computed<string | null>(() => {
    const requested = this.requestedScopeId();
    if (requested !== null) {
      return requested;
    }

    const current = this.scope();
    return current === null ? null : keyFor(current);
  });

  constructor() {
    // A scope or a search is a different question for the provider, so it is asked again.
    effect(() => {
      const scopeId = this.requestedScopeId();
      const search = this.submittedQuery();
      const [projectId, environmentId] = scopeId === null ? [null, null] : scopeId.split(':');

      // Untracked: the store writes its own load-state signals, and reading them here would make this
      // effect depend on its own output and re-run forever.
      untracked(() =>
        this.store.read({
          projectId: projectId ?? null,
          environmentId: environmentId ?? null,
          search: search === '' ? null : search,
        }),
      );
    });

    // Re-reads the window while the screen is being looked at; the API holds the provider credential.
    autoRefresh(() => {
      if (this.live()) {
        this.store.refresh();
      }
    });
  }

  /**
   * The stream in view. Severity and source narrow the fetched lines; a marker is kept only while it still
   * explains something under it.
   */
  protected readonly items = computed<readonly LogStreamItem[]>(() => {
    const events = this.store
      .items()
      .filter((item): item is LogEvent => item.kind === 'event')
      .filter((event) => this.matches(event));
    if (events.length === 0) {
      return [];
    }

    return this.level() === null && this.source() === null ? this.store.items() : events;
  });

  protected readonly totalCount = computed(
    () => this.store.items().filter((item) => item.kind === 'event').length,
  );

  protected readonly errorCount = computed(
    () =>
      this.items().filter(
        (item) => item.kind === 'event' && (item.level === 'error' || item.level === 'critical'),
      ).length,
  );

  protected readonly selected = computed<LogEvent | null>(() => {
    const selectedId = this.selectedId();
    if (selectedId === null) {
      return null;
    }

    return (
      this.items().find(
        (item): item is LogEvent => item.kind === 'event' && item.id === selectedId,
      ) ?? null
    );
  });

  protected select(eventId: string): void {
    this.selectedId.update((current) => (current === eventId ? null : eventId));
  }

  protected dismissDetail(): void {
    this.selectedId.set(null);
  }

  protected setScope(scopeId: string): void {
    this.requestedScopeId.set(scopeId);
    this.selectedId.set(null);
  }

  /** Typing filters nothing on its own: the search is a provider query, so it is submitted. */
  protected setQuery(query: string): void {
    this.query.set(query);
  }

  protected submitQuery(): void {
    this.submittedQuery.set(this.query().trim());
    this.selectedId.set(null);
  }

  protected setLevel(level: LogLevelFilter): void {
    this.level.set(level);
  }

  protected setSource(source: LogSourceFilter): void {
    this.source.set(source);
  }

  protected setLive(live: boolean): void {
    this.live.set(live);
  }

  protected clearFilters(): void {
    this.query.set('');
    this.submittedQuery.set('');
    this.level.set(null);
    this.source.set(null);
  }

  protected retry(): void {
    this.store.refresh();
  }

  /**
   * Severity buckets: the toolbar offers three, and the contract carries seven. `INF` covers the
   * informational levels, `ERR` covers error and critical, and an unparsed line appears only under `All`.
   */
  private matches(event: LogEvent): boolean {
    const level = this.level();
    if (level === 'information' && !['information', 'debug', 'trace'].includes(event.level)) {
      return false;
    }
    if (level === 'warning' && event.level !== 'warning') {
      return false;
    }
    if (level === 'error' && !['error', 'critical'].includes(event.level)) {
      return false;
    }

    const source = this.source();
    return source === null || event.sourceKind === source;
  }
}

function keyFor(scope: LogStreamScope): string {
  return `${scope.projectId}:${scope.environment.id}`;
}
