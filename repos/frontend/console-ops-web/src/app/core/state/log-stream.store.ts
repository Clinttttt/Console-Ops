import { DestroyRef, Injectable, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { LogStream, LogStreamItem, LogStreamScope } from '../contracts/log-stream';
import { LogStreamDataSource } from '../data/log-stream.data-source';

/** Load outcome, kept explicit so the UI can distinguish "waiting" from "could not be determined". */
export type LogStreamLoadState = 'loading' | 'loaded' | 'unavailable';

/** Holds the log stream for the Logs screen. */
@Injectable({ providedIn: 'root' })
export class LogStreamStore {
  private readonly dataSource = inject(LogStreamDataSource);
  private readonly destroyRef = inject(DestroyRef);

  private readonly current = signal<LogStream | null>(null);
  private readonly state = signal<LogStreamLoadState>('loading');

  readonly loadState = this.state.asReadonly();
  readonly items = computed<readonly LogStreamItem[]>(() => this.current()?.items ?? []);
  readonly scopes = computed<readonly LogStreamScope[]>(() => this.current()?.scopes ?? []);

  /** Reference clock for relative times and the day grouping. `null` until a stream exists. */
  readonly observedAt = computed(() => this.current()?.observedAt ?? null);

  constructor() {
    this.refresh();
  }

  /**
   * Reads the stream.
   *
   * Only the first read shows a loading state, so a re-read never blanks a stream someone is reading,
   * and a failed re-read keeps the events already on screen.
   */
  refresh(): void {
    const isFirstRead = this.state() !== 'loaded';
    if (isFirstRead) {
      this.state.set('loading');
    }

    this.dataSource
      .load()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (stream) => {
          this.current.set(stream);
          this.state.set('loaded');
        },
        error: () => {
          if (isFirstRead) {
            this.current.set(null);
            this.state.set('unavailable');
          }
        },
      });
  }
}
