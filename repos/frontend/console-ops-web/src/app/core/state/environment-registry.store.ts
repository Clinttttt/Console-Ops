import { DestroyRef, Injectable, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { EnvironmentListItem, EnvironmentRegistry } from '../contracts/environment-registry';
import { EnvironmentRegistryDataSource } from '../data/environment-registry.data-source';

/** Load outcome, kept explicit so the UI can distinguish "waiting" from "could not be determined". */
export type EnvironmentRegistryLoadState = 'loading' | 'loaded' | 'unavailable';

/** Holds the environments reported across projects for the Environments screen. */
@Injectable({ providedIn: 'root' })
export class EnvironmentRegistryStore {
  private readonly dataSource = inject(EnvironmentRegistryDataSource);
  private readonly destroyRef = inject(DestroyRef);

  private readonly current = signal<EnvironmentRegistry | null>(null);
  private readonly state = signal<EnvironmentRegistryLoadState>('loading');

  readonly loadState = this.state.asReadonly();
  readonly environments = computed<readonly EnvironmentListItem[]>(
    () => this.current()?.environments ?? [],
  );

  /** Reference clock for relative times. `null` until an observation exists. */
  readonly observedAt = computed(() => this.current()?.observedAt ?? null);

  constructor() {
    this.refresh();
  }

  refresh(): void {
    this.state.set('loading');
    this.dataSource
      .load()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (registry) => {
          this.current.set(registry);
          this.state.set('loaded');
        },
        error: () => {
          this.current.set(null);
          this.state.set('unavailable');
        },
      });
  }
}
