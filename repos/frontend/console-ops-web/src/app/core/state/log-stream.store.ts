import { DestroyRef, Injectable, computed, inject, signal, untracked } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import {
  LogStream,
  LogStreamItem,
  LogStreamNoise,
  LogStreamScope,
  LogStreamWindow,
} from '../contracts/log-stream';
import { LogStreamDataSource, LogStreamRequest } from '../data/log-stream.data-source';

/** Load outcome, kept explicit so the UI can distinguish "waiting" from "could not be determined". */
export type LogStreamLoadState = 'loading' | 'loaded' | 'unavailable';

/** Why a read produced nothing, so the screen can name the cause instead of showing an empty stream. */
export type LogStreamFailure =
  'notConfigured' | 'credential' | 'provider' | 'apiUnavailable' | null;

/** What reading further back can offer: nothing said yet, a page available, one in flight, or the end. */
export type LogStreamOlderPages = 'unknown' | 'available' | 'reading' | 'exhausted';

/**
 * Holds the log stream for the Logs screen.
 *
 * Unlike the other stores this one is parameterized: the scope and the search are pushed down to the
 * provider, because a 24-hour window can hold far more lines than a page and filtering locally would hide
 * matches further back.
 */
@Injectable({ providedIn: 'root' })
export class LogStreamStore {
  private readonly dataSource = inject(LogStreamDataSource);
  private readonly destroyRef = inject(DestroyRef);

  private readonly current = signal<LogStream | null>(null);
  private readonly loaded = signal<readonly LogStreamItem[]>([]);
  private readonly state = signal<LogStreamLoadState>('loading');
  private readonly olderState = signal<'idle' | 'reading' | 'exhausted'>('idle');
  private readonly failure = signal<LogStreamFailure>(null);

  readonly loadState = this.state.asReadonly();
  readonly failureReason = this.failure.asReadonly();

  /**
   * Every page read for the current scope, newest first, merged by id.
   *
   * Pages accumulate rather than replace so scrolling back does not undo itself, and so the 30-second
   * re-read cannot discard what the operator paged in.
   */
  readonly items = this.loaded.asReadonly();

  /** `true` while an older page is in flight, so the screen can say so without blanking the stream. */
  readonly readingOlder = computed(() => this.olderState() === 'reading');

  /**
   * What paging backwards can offer right now, as one value rather than a pair of booleans the screen would
   * have to reason about. `unknown` covers "no stream yet", so nothing is said about older lines before a
   * first page exists.
   *
   * Console Ops reads a day at a time; once the day before the oldest line holds nothing new it reports
   * `exhausted` rather than walking backwards through empty windows forever.
   */
  readonly olderPages = computed<LogStreamOlderPages>(() => {
    if (this.state() !== 'loaded' || this.loaded().length === 0) {
      return 'unknown';
    }

    switch (this.olderState()) {
      case 'reading':
        return 'reading';
      case 'exhausted':
        return 'exhausted';
      default:
        return 'available';
    }
  });

  readonly scopes = computed<readonly LogStreamScope[]>(() => this.current()?.scopes ?? []);
  readonly scope = computed<LogStreamScope | null>(() => this.current()?.scope ?? null);
  readonly window = computed<LogStreamWindow | null>(() => this.current()?.window ?? null);

  /** What was left out to make the stream readable. `null` until a stream exists. */
  readonly noise = computed<LogStreamNoise | null>(() => this.current()?.noise ?? null);

  /** Reference clock for relative times and the day grouping. `null` until a stream exists. */
  readonly observedAt = computed(() => this.current()?.observedAt ?? null);

  private lastRequest: LogStreamRequest = {
    projectId: null,
    environmentId: null,
    search: null,
    before: null,
    includeNoise: false,
    since: null,
  };

  /** Where the next tail starts: the last response's own composition time. 
ull until a stream exists. */
  private cursor: string | null = null;

  /** Guards against a slow tail overlapping the next interval. */
  private tailing = false;

  // No read in the constructor: the screen states which scope and search it wants and reads once for it.
  // Reading here as well sent two identical provider queries for every open of the Logs page.

  /**
   * Reads a scope.
   *
   * Only the first read of a scope shows a loading state; re-reading the same scope leaves the stream on
   * screen. A failed read keeps nothing on screen, because a stale window shown as current would be a lie
   * about what is happening now - the failure is named instead.
   */
  read(request: LogStreamRequest): void {
    const isSameScope =
      request.projectId === this.lastRequest.projectId &&
      request.environmentId === this.lastRequest.environmentId &&
      request.search === this.lastRequest.search &&
      // Asking for the noisy lines back is a different question, so its answer replaces rather than merges.
      request.includeNoise === this.lastRequest.includeNoise;
    this.lastRequest = request;

    // Untracked so a caller inside a reactive context cannot end up depending on this store's own state.
    if (!isSameScope || untracked(this.state) !== 'loaded') {
      this.state.set('loading');
    }

    this.dataSource
      .load(request)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (stream) => {
          this.current.set(stream);
          this.cursor = stream.observedAt;
          // A different scope or search is a different stream, so paged-in history does not carry over.
          this.loaded.set(isSameScope ? merge(untracked(this.loaded), stream.items) : stream.items);
          this.olderState.set(isSameScope ? untracked(this.olderState) : 'idle');
          this.failure.set(null);
          this.state.set('loaded');
        },
        error: (error: unknown) => {
          this.current.set(null);
          this.loaded.set([]);
          this.olderState.set('idle');
          this.failure.set(classify(error));
          this.state.set('unavailable');
        },
      });
  }

  /**
   * Reads the window before the oldest line held, and keeps both.
   *
   * A failed older read leaves the stream alone: the operator keeps what they were reading, and the screen
   * offers the page again rather than claiming the history ended there.
   */
  readOlder(): void {
    const held = untracked(this.loaded);
    if (held.length === 0 || untracked(this.olderState) === 'reading') {
      return;
    }

    this.olderState.set('reading');
    this.dataSource
      .load({ ...this.lastRequest, since: null, before: held[held.length - 1].occurredAt })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (stream) => {
          const combined = merge(untracked(this.loaded), stream.items);
          // Nothing new means the day before the oldest line held nothing, which is where paging stops.
          this.olderState.set(
            combined.length === untracked(this.loaded).length ? 'exhausted' : 'idle',
          );
          this.loaded.set(combined);
        },
        error: () => this.olderState.set('idle'),
      });
  }

  refresh(): void {
    this.read(this.lastRequest);
  }

  /**
   * Follows the scope: reads only what has happened since the last read and adds it to what is held.
   *
   * This is what `Live` does instead of re-reading the window. A tail asks the provider for seconds rather
   * than a day, so following a stream costs a fraction of refreshing it, and nothing already on screen is
   * disturbed - including pages the operator scrolled back to.
   *
   * The cursor is the last response's own composition time, less an overlap, because a provider ingests a
   * line slightly after it was written: advancing the cursor to exactly the last read would step over lines
   * that were not queryable yet. Overlapping re-reads a few of them, and merging by id makes that free.
   */
  tail(): void {
    const cursor = this.cursor;
    if (cursor === null || untracked(this.state) === 'unavailable' || this.tailing) {
      return;
    }

    this.tailing = true;
    this.dataSource
      .load({
        ...this.lastRequest,
        before: null,
        since: new Date(Date.parse(cursor) - TailOverlapMs).toISOString(),
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (stream) => {
          this.tailing = false;
          this.cursor = stream.observedAt;
          // The window and the scope stay as the full read left them: a tail covers seconds, and reporting
          // that as the window would tell the operator they are looking at seconds of history.
          this.current.update((held) =>
            held === null
              ? stream
              : {
                  ...held,
                  observedAt: stream.observedAt,
                  noise: addNoise(held.noise, stream.noise),
                },
          );
          this.loaded.set(merge(untracked(this.loaded), stream.items));
        },
        // A failed tail leaves the stream alone. The next interval tries again from the same cursor, so a
        // dropped poll delays lines rather than losing them.
        error: () => {
          this.tailing = false;
        },
      });
  }
}

/**
 * How far back a tail reaches beyond the last read.
 *
 * Ingestion delay measured against a real workspace was under two seconds; this is slack for that plus clock
 * skew between the emitter and the provider. Re-read lines are discarded by id.
 */
const TailOverlapMs = 30_000;

/** Adds what a tail left out to what the window already left out, so the stated count stays truthful. */
function addNoise(held: LogStreamNoise, incoming: LogStreamNoise): LogStreamNoise {
  if (incoming.hiddenCount === 0) {
    return held;
  }

  const counts = new Map(held.categories.map((entry) => [entry.category, entry.count]));
  for (const entry of incoming.categories) {
    counts.set(entry.category, (counts.get(entry.category) ?? 0) + entry.count);
  }

  return {
    excluded: held.excluded,
    hiddenCount: held.hiddenCount + incoming.hiddenCount,
    categories: [...counts.entries()]
      .map(([category, count]) => ({ category, count }))
      .sort((left, right) => right.count - left.count),
  };
}

/**
 * Merges pages into one newest-first stream, keyed by id.
 *
 * Merging by id rather than by time because the provider's window bound is inclusive and two lines can
 * share a millisecond: a boundary line would otherwise appear twice, or be dropped by an exclusive cursor.
 */
function merge(
  held: readonly LogStreamItem[],
  incoming: readonly LogStreamItem[],
): readonly LogStreamItem[] {
  if (held.length === 0) {
    return incoming;
  }

  const seen = new Set(held.map((item) => item.id));
  const added = incoming.filter((item) => !seen.has(item.id));
  if (added.length === 0) {
    return held;
  }

  return [...held, ...added].sort((left, right) => right.occurredAt.localeCompare(left.occurredAt));
}

/**
 * Turns a failure into something the screen can explain. The API reports a stable code, so a missing log
 * source is never blamed on the network.
 */
function classify(error: unknown): LogStreamFailure {
  const response = error as { status?: number; error?: { code?: unknown } } | null;
  const code = response?.error?.code;

  if (code === 'Logs.NoLogSourceConfigured' || code === 'Logs.ScopeNotFound') {
    return 'notConfigured';
  }
  if (code === 'Logs.Unauthorized' || code === 'Logs.WorkspaceNotFound') {
    return 'credential';
  }
  if (response?.status === 0) {
    return 'apiUnavailable';
  }
  return 'provider';
}
