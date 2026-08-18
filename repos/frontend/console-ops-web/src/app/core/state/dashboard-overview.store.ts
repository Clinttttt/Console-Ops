import { DestroyRef, Injectable, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { DashboardOverview } from '../contracts/dashboard-overview';
import { DashboardOverviewDataSource } from '../data/dashboard-overview.data-source';

/** Load outcome, kept explicit so the UI can distinguish "waiting" from "could not be determined". */
export type OverviewLoadState = 'loading' | 'loaded' | 'unavailable';

/**
 * Holds the dashboard overview for every surface that needs it (the shell footer and the Overview
 * page) so one observation is shared rather than fetched twice.
 */
@Injectable({ providedIn: 'root' })
export class DashboardOverviewStore {
  private readonly dataSource = inject(DashboardOverviewDataSource);
  private readonly destroyRef = inject(DestroyRef);

  private readonly current = signal<DashboardOverview | null>(null);
  private readonly state = signal<OverviewLoadState>('loading');

  readonly overview = this.current.asReadonly();
  readonly loadState = this.state.asReadonly();
  readonly summary = computed(() => this.current()?.summary ?? null);

  constructor() {
    this.refresh();
  }

  /**
   * Reads the overview.
   *
   * Only the first read shows a loading state. A scheduled re-read must not blank a screen someone is
   * looking at, and a failed re-read keeps the last observation rather than replacing it with nothing:
   * the previous reading is still the most recent thing Console Ops knows.
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
        next: (overview) => {
          this.current.set(overview);
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
