import { DestroyRef, Injectable, computed, inject, signal, untracked } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { HealthSnapshot } from '../contracts/health';
import { HealthDataSource } from '../data/health.data-source';

export type HealthLoadState = 'loading' | 'loaded' | 'unavailable';

/**
 * Holds the recorded health of every monitored environment.
 *
 * Re-reads do not blank the screen: only the first read shows a loading state, and a failed re-read keeps the
 * last snapshot rather than replacing a working screen with an error.
 */
@Injectable({ providedIn: 'root' })
export class HealthStore {
  private readonly dataSource = inject(HealthDataSource);
  private readonly destroyRef = inject(DestroyRef);

  private readonly current = signal<HealthSnapshot | null>(null);
  private readonly state = signal<HealthLoadState>('loading');

  readonly loadState = this.state.asReadonly();
  readonly observedAt = computed(() => this.current()?.observedAt ?? null);
  readonly summary = computed(() => this.current()?.summary ?? null);
  readonly environments = computed(() => this.current()?.environments ?? []);
  readonly stateChanges = computed(() => this.current()?.stateChanges ?? []);
  readonly isSampleData = computed(() => this.current()?.isSampleData ?? false);

  read(): void {
    if (untracked(this.state) !== 'loaded') {
      this.state.set('loading');
    }

    this.dataSource
      .load()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (snapshot) => {
          this.current.set(snapshot);
          this.state.set('loaded');
        },
        error: () => {
          if (untracked(this.current) === null) {
            this.state.set('unavailable');
          }
        },
      });
  }
}
