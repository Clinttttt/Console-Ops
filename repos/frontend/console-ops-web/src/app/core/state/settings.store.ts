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
    this.current.set(remember(snapshot, this.verifications));
    this.state.set('loaded');
  }

  /**
   * What the last probe established, for as long as this tab lives.
   *
   * A plain read does not contact a provider, so it always reports verification as not probed. Letting that
   * overwrite a verification the operator just performed made the screen forget it on every re-entry, which is
   * a different kind of dishonesty: Console Ops did check, and the result had a timestamp. The result is carried
   * forward with that timestamp, and dropped the moment the capability stops being configured.
   */
  private readonly verifications = new Map<string, Verification>();
}

interface Verification {
  readonly verification: 'verified' | 'failed';
  readonly verifiedAt: string | null;
  readonly failure: string | null;
}

function remember(
  snapshot: SettingsSnapshot,
  verifications: Map<string, Verification>,
): SettingsSnapshot {
  return {
    ...snapshot,
    integrations: snapshot.integrations.map((integration) => {
      if (integration.verification !== 'notProbed') {
        verifications.set(integration.id, {
          verification: integration.verification,
          verifiedAt: integration.verifiedAt,
          failure: integration.failure,
        });
        return integration;
      }

      const known = verifications.get(integration.id);
      if (known === undefined || integration.configuration === 'notConfigured') {
        // Configuration changed since the check, so what was verified no longer describes what is there.
        verifications.delete(integration.id);
        return integration;
      }

      return { ...integration, ...known };
    }),
  };
}
