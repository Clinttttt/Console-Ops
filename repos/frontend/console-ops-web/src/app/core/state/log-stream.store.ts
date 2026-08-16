import { DestroyRef, Injectable, computed, inject, signal, untracked } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { LogStream, LogStreamItem, LogStreamScope, LogStreamWindow } from '../contracts/log-stream';
import { LogStreamDataSource, LogStreamRequest } from '../data/log-stream.data-source';

/** Load outcome, kept explicit so the UI can distinguish "waiting" from "could not be determined". */
export type LogStreamLoadState = 'loading' | 'loaded' | 'unavailable';

/** Why a read produced nothing, so the screen can name the cause instead of showing an empty stream. */
export type LogStreamFailure =
  'notConfigured' | 'credential' | 'provider' | 'apiUnavailable' | null;

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
  private readonly state = signal<LogStreamLoadState>('loading');
  private readonly failure = signal<LogStreamFailure>(null);

  readonly loadState = this.state.asReadonly();
  readonly failureReason = this.failure.asReadonly();
  readonly items = computed<readonly LogStreamItem[]>(() => this.current()?.items ?? []);
  readonly scopes = computed<readonly LogStreamScope[]>(() => this.current()?.scopes ?? []);
  readonly scope = computed<LogStreamScope | null>(() => this.current()?.scope ?? null);
  readonly window = computed<LogStreamWindow | null>(() => this.current()?.window ?? null);

  /** Reference clock for relative times and the day grouping. `null` until a stream exists. */
  readonly observedAt = computed(() => this.current()?.observedAt ?? null);

  private lastRequest: LogStreamRequest = { projectId: null, environmentId: null, search: null };

  constructor() {
    this.read(this.lastRequest);
  }

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
      request.search === this.lastRequest.search;
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
          this.failure.set(null);
          this.state.set('loaded');
        },
        error: (error: unknown) => {
          this.current.set(null);
          this.failure.set(classify(error));
          this.state.set('unavailable');
        },
      });
  }

  refresh(): void {
    this.read(this.lastRequest);
  }
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
