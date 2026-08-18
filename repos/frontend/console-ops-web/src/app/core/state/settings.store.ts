import { DestroyRef, Injectable, computed, inject, signal, untracked } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { SettingsSnapshot } from '../contracts/settings';
import { SettingsDataSource } from '../data/settings.data-source';

export type SettingsLoadState = 'loading' | 'loaded' | 'unavailable';

/** What the screen is waiting for, so one action's spinner cannot appear on the other's button. */
export type SettingsAction = 'probe' | 'collect' | null;

/**
 * Holds the Settings screen's snapshot.
 *
 * Both actions replace the snapshot rather than patching parts of it, because each returns Console Ops'
 * current state as a whole. A failed action leaves what is on screen alone: an operator who pressed Test
 * connection is asking a question, and failing to answer it must not discard what they were already reading.
 */
@Injectable({ providedIn: 'root' })
export class SettingsStore {
  private readonly dataSource = inject(SettingsDataSource);
  private readonly destroyRef = inject(DestroyRef);

  private readonly current = signal<SettingsSnapshot | null>(null);
  private readonly state = signal<SettingsLoadState>('loading');
  private readonly pending = signal<SettingsAction>(null);
  private readonly actionFailure = signal<string | null>(null);

  readonly loadState = this.state.asReadonly();
  readonly snapshot = this.current.asReadonly();
  readonly runningAction = this.pending.asReadonly();

  /** Why the last action failed, cleared when another is started. */
  readonly failure = this.actionFailure.asReadonly();

  readonly integrations = computed(() => this.current()?.integrations ?? []);
  readonly collection = computed(() => this.current()?.collection ?? null);
  readonly about = computed(() => this.current()?.about ?? null);
  readonly observedAt = computed(() => this.current()?.observedAt ?? null);

  read(): void {
    if (untracked(this.state) !== 'loaded') {
      this.state.set('loading');
    }

    this.dataSource
      .load()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (snapshot) => this.accept(snapshot),
        error: () => {
          this.current.set(null);
          this.state.set('unavailable');
        },
      });
  }

  /** Tests every integration's credentials. Explicit, because it contacts each provider. */
  probe(): void {
    this.run('probe', this.dataSource.probe(), 'The connection test could not be completed.');
  }

  collectNow(): void {
    this.run('collect', this.dataSource.collectNow(), 'Collection could not be started.');
  }

  private run(
    action: Exclude<SettingsAction, null>,
    work: ReturnType<SettingsDataSource['probe']>,
    failure: string,
  ): void {
    if (untracked(this.pending) !== null) {
      return;
    }

    this.pending.set(action);
    this.actionFailure.set(null);

    work.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (snapshot) => {
        this.pending.set(null);
        this.accept(snapshot);
      },
      error: () => {
        this.pending.set(null);
        this.actionFailure.set(failure);
      },
    });
  }

  private accept(snapshot: SettingsSnapshot): void {
    // No merging here any more. The API remembers what the last probe established, so every tab and every
    // reload sees the same verification with the instant it was checked.
    this.current.set(snapshot);
    this.state.set('loaded');
  }
}
