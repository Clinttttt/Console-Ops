import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';

import { LogEvent, LogStreamItem, LogStreamScope } from '../../core/contracts/log-stream';
import { LogStreamStore } from '../../core/state/log-stream.store';
import { LogFilters, LogLevelFilter, LogSourceFilter } from './components/log-filters';
import { LogDetail } from './components/log-detail';
import { LogStreamView } from './components/log-stream';

/**
 * Logs screen: the forensic workspace.
 *
 * It answers what the application and its runtime actually said around the time something happened, and
 * deliberately carries none of the state that already has a home. Project configuration, release history,
 * environment configuration, and health belong to their own screens; the only cross-screen material here
 * is the lightweight markers that explain a change in the stream.
 *
 * Design stage: Console Ops has no log ingestion, so the screen is fixture-backed and says so. Nothing
 * here was observed from a real application.
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
  protected readonly observedAt = this.store.observedAt;
  protected readonly scopes = this.store.scopes;

  protected readonly query = signal('');
  protected readonly level = signal<LogLevelFilter>(null);
  protected readonly source = signal<LogSourceFilter>(null);
  protected readonly live = signal(true);
  private readonly requestedScopeId = signal<string | null>(null);

  /** Nothing is selected until an event is chosen: a panel nobody opened describes an arbitrary line. */
  private readonly selectedId = signal<string | null>(null);

  /** Falls back to the first scope, because a stream always belongs to one project and environment. */
  protected readonly scopeId = computed<string | null>(() => {
    const requested = this.requestedScopeId();
    const scopes = this.scopes();
    if (requested !== null && scopes.some((scope) => keyFor(scope) === requested)) {
      return requested;
    }

    const first = scopes[0];
    return first === undefined ? null : keyFor(first);
  });

  protected readonly scope = computed<LogStreamScope | null>(
    () => this.scopes().find((scope) => keyFor(scope) === this.scopeId()) ?? null,
  );

  /**
   * The stream in view.
   *
   * A marker is kept only while it still explains something: filtering to errors should not leave a
   * deployment rule floating with nothing under it.
   */
  protected readonly items = computed<readonly LogStreamItem[]>(() => {
    const events = this.store
      .items()
      .filter((item): item is LogEvent => item.kind === 'event')
      .filter((event) => this.matches(event));
    if (events.length === 0) {
      return [];
    }

    const hasFilter = this.query().trim() !== '' || this.level() !== null || this.source() !== null;
    if (hasFilter) {
      return events;
    }

    return this.store.items();
  });

  protected readonly totalCount = computed(
    () => this.store.items().filter((item) => item.kind === 'event').length,
  );

  protected readonly errorCount = computed(
    () => this.items().filter((item) => item.kind === 'event' && item.level === 'error').length,
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

  protected setQuery(query: string): void {
    this.query.set(query);
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
    this.level.set(null);
    this.source.set(null);
  }

  protected retry(): void {
    this.store.refresh();
  }

  private matches(event: LogEvent): boolean {
    const level = this.level();
    if (level !== null && event.level !== level) {
      return false;
    }

    const source = this.source();
    if (source !== null && event.sourceKind !== source) {
      return false;
    }

    const query = this.query().trim().toLowerCase();
    return query === '' || matchesQuery(event, query);
  }
}

function keyFor(scope: LogStreamScope): string {
  return `${scope.projectId}:${scope.environment.id}`;
}

function matchesQuery(event: LogEvent, query: string): boolean {
  return (
    event.message.toLowerCase().includes(query) ||
    event.source.toLowerCase().includes(query) ||
    (event.correlation.traceId?.toLowerCase().includes(query) ?? false) ||
    (event.correlation.requestId?.toLowerCase().includes(query) ?? false) ||
    (event.exception?.type.toLowerCase().includes(query) ?? false) ||
    event.properties.some(
      (property) =>
        property.name.toLowerCase().includes(query) || property.value.toLowerCase().includes(query),
    )
  );
}
