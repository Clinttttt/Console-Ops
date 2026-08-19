import { DestroyRef, Injectable, computed, inject, signal, untracked } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { WorkflowInventory } from '../contracts/workflows';
import { WorkflowsDataSource } from '../data/workflows.data-source';

export type WorkflowsLoadState = 'loading' | 'loaded' | 'unavailable';

/**
 * Holds the workflow inventory for the Workflows screen.
 *
 * Re-reads do not blank the screen: only the first read shows a loading state, and a failed re-read keeps the
 * inventory already on screen rather than replacing a working page with an error.
 */
@Injectable({ providedIn: 'root' })
export class WorkflowsStore {
  private readonly dataSource = inject(WorkflowsDataSource);
  private readonly destroyRef = inject(DestroyRef);

  private readonly current = signal<WorkflowInventory | null>(null);
  private readonly state = signal<WorkflowsLoadState>('loading');

  readonly loadState = this.state.asReadonly();
  readonly groups = computed(() => this.current()?.groups ?? []);
  readonly readAt = computed(() => this.current()?.readAt ?? null);

  /** Whether what the screen is showing is sample data. Decided by the payload, never by the page. */
  readonly isSampleData = computed(() => this.current()?.isSampleData ?? false);

  readonly workflowCount = computed(() =>
    this.groups().reduce((total, group) => total + group.workflows.length, 0),
  );

  read(): void {
    if (untracked(this.state) !== 'loaded') {
      this.state.set('loading');
    }

    this.dataSource
      .load()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (inventory) => {
          this.current.set(inventory);
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
