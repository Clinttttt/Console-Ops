import { DestroyRef, Injectable, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { DeploymentListItem, DeploymentRegistry } from '../contracts/deployment-registry';
import { DeploymentRegistryDataSource } from '../data/deployment-registry.data-source';

/** Load outcome, kept explicit so the UI can distinguish "waiting" from "could not be determined". */
export type DeploymentRegistryLoadState = 'loading' | 'loaded' | 'unavailable';

/** Holds deployment history for the Deployments screen. */
@Injectable({ providedIn: 'root' })
export class DeploymentRegistryStore {
  private readonly dataSource = inject(DeploymentRegistryDataSource);
  private readonly destroyRef = inject(DestroyRef);

  private readonly current = signal<DeploymentRegistry | null>(null);
  private readonly state = signal<DeploymentRegistryLoadState>('loading');

  readonly loadState = this.state.asReadonly();
  readonly deployments = computed<readonly DeploymentListItem[]>(
    () => this.current()?.deployments ?? [],
  );

  /** Reference clock for relative times and the "today" grouping. `null` until an observation exists. */
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
