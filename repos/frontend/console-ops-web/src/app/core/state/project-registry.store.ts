import { DestroyRef, Injectable, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { ProjectListItem, ProjectRegistry } from '../contracts/project-registry';
import { ProjectRegistryDataSource } from '../data/project-registry.data-source';

/** Load outcome, kept explicit so the UI can distinguish "waiting" from "could not be determined". */
export type ProjectRegistryLoadState = 'loading' | 'loaded' | 'unavailable';

/** Holds the registered projects for the Projects screen. */
@Injectable({ providedIn: 'root' })
export class ProjectRegistryStore {
  private readonly dataSource = inject(ProjectRegistryDataSource);
  private readonly destroyRef = inject(DestroyRef);

  private readonly current = signal<ProjectRegistry | null>(null);
  private readonly state = signal<ProjectRegistryLoadState>('loading');

  readonly registry = this.current.asReadonly();
  readonly loadState = this.state.asReadonly();

  readonly projects = computed<readonly ProjectListItem[]>(() => this.current()?.projects ?? []);

  /** Newest registrations first. Recency comes from the data, never from list order. */
  readonly recentlyAdded = computed(() =>
    [...this.projects()]
      .filter((project) => project.lifecycle === 'active')
      .sort((left, right) => right.registeredAt.localeCompare(left.registeredAt))
      .slice(0, 3),
  );

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
